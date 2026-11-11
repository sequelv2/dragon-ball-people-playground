# Builds the frame-by-frame Super Saiyan aura from the user's reference animations (_Pipeline/SaiyanAuraSource):
#   aura.mp4      the gold aura, hand-drawn at 12 drawings a second
#   lightning.gif the lightning, 60 ms frames, drawn over a blue aura
# Each drawing is rebuilt so it stays sharp at any resolution: the outline becomes a signed distance field (the
# halo is drawn from it in the shader), the colours are kept as drawn and carried past the outline so filtering
# never darkens the edge, and the inner oval gets a distance field of its own so the game can open it up.
# The lightning is traced into jagged polylines (straight runs, sharp corners, zigzags) and stored as the
# distance to them plus the stroke's own thickness. Writes:
#   unity/Assets/SaiyanAura/aura_atlas.png    RGBA: colour, A outline distance
#   unity/Assets/SaiyanAura/inner_atlas.png   grey: distance to the inner oval
#   unity/Assets/SaiyanAura/bolt_atlas.png    R distance to the lightning, G its half thickness
#   ../Assets/scripts/Auras/SaiyanAuraFrames.cs   frame layout, ramp colours and the lightning timing
# Usage: python -I saiyan_flipbook.py [--preview <folder>]
import os, sys, json, math
import numpy as np
import cv2
from PIL import Image, ImageSequence

HERE = os.path.dirname(os.path.abspath(__file__))
MOD = os.path.dirname(HERE)
SOURCE = os.path.join(HERE, 'SaiyanAuraSource')
UNITY = os.path.join(HERE, 'unity', 'Assets', 'SaiyanAura')
CS = os.path.join(MOD, 'Assets', 'scripts', 'Auras', 'SaiyanAuraFrames.cs')
UP = 4              # working supersampling for outlines
SDF_RANGE = 24.0    # outline distance stored over +-24 frame pixels (the halo reaches that far)
BOLT_RANGE = 12.0   # lightning distance stored over 0..12 frame pixels
BOLT_WIDTH = 4.0    # lightning half thickness stored over 0..4 frame pixels
INNER_RANGE = 32.0  # distance to the inner oval stored over +-32 frame pixels
LOOP = 48           # drawings in the aura loop
PAD = 4             # empty pixels around every frame in the atlas
preview = sys.argv[sys.argv.index('--preview') + 1] if '--preview' in sys.argv else None


def smooth01(x, a, b):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


# ---------------------------------------------------------------- aura drawings
cap = cv2.VideoCapture(os.path.join(SOURCE, 'aura.mp4'))
video = []
while True:
    ok, f = cap.read()
    if not ok:
        break
    video.append(cv2.cvtColor(f, cv2.COLOR_BGR2RGB).astype(np.float32) / 255)
small = [cv2.resize(f, (f.shape[1] // 4, f.shape[0] // 4), interpolation=cv2.INTER_AREA) for f in video]
groups = [[0]]
for i in range(1, len(video)):
    if np.abs(small[i] - small[i - 1]).mean() * 255 > 4.0:
        groups.append([i])
    else:
        groups[-1].append(i)
# The first and last runs may be cut by the clip; average every held frame to lift compression noise.
drawings = [np.mean([video[i] for i in g], axis=0) for g in groups[1:-1]]
dsmall = [cv2.resize(d, (d.shape[1] // 4, d.shape[0] // 4), interpolation=cv2.INTER_AREA) for d in drawings]
step = np.mean([np.abs(dsmall[i] - dsmall[i - 1]).mean() for i in range(1, len(dsmall))])
best = None
for start in range(0, len(drawings) - LOOP + 1):
    wrap = np.abs(dsmall[start + LOOP - 1] - dsmall[start]).mean()
    if best is None or wrap < best[0]:
        best = (wrap, start)
start = best[1]
drawings = drawings[start:start + LOOP]
print('aura: %d drawings, loop %d..%d, wrap step %.4f vs mean step %.4f' % (len(groups), start, start + LOOP - 1, best[0], step))


def silhouette(img):
    # The flames and the body are bright in red; the halo between the tongues stays below .55.
    red = cv2.resize(img[..., 0], None, fx=UP, fy=UP, interpolation=cv2.INTER_CUBIC)
    red = cv2.GaussianBlur(red, (0, 0), UP * .35)
    mask = (red > .62).astype(np.uint8)
    # Keep the largest piece and fill any dark pocket inside it.
    count, labels, stats, _ = cv2.connectedComponentsWithStats(mask, 8)
    if count > 1:
        keep = 1 + int(np.argmax(stats[1:, cv2.CC_STAT_AREA]))
        mask = (labels == keep).astype(np.uint8)
    flood = mask.copy()
    h, w = flood.shape
    cv2.floodFill(flood, np.zeros((h + 2, w + 2), np.uint8), (0, 0), 1)
    mask = mask | (1 - flood)
    return mask


masks = [silhouette(d) for d in drawings]
union = np.zeros_like(masks[0])
for m in masks:
    union |= m
ys, xs = np.nonzero(union)
margin = 22 * UP
x0, x1 = (xs.min() - margin) // UP, (xs.max() + margin) // UP + 1
y0, y1 = max(0, (ys.min() - margin) // UP), min(video[0].shape[0], (ys.max() + margin) // UP + 1)
W, H = x1 - x0, y1 - y0
W += (-W) % 4; H += (-H) % 4
x1, y1 = x0 + W, y0 + H
print('crop', x0, y0, W, H)

def signed_distance(mask_up):
    inside = cv2.distanceTransform(mask_up, cv2.DIST_L2, cv2.DIST_MASK_PRECISE)
    outside = cv2.distanceTransform(1 - mask_up, cv2.DIST_L2, cv2.DIST_MASK_PRECISE)
    # Half a pixel either side puts the zero on the boundary itself; the blur irons out the pixel steps.
    sdf = (np.maximum(outside - .5, 0) - np.maximum(inside - .5, 0)) / UP
    sdf = cv2.GaussianBlur(sdf, (0, 0), UP * .5)
    return cv2.resize(sdf, (sdf.shape[1] // UP, sdf.shape[0] // UP), interpolation=cv2.INTER_AREA)


def largest_filled(mask):
    count, labels, stats, _ = cv2.connectedComponentsWithStats(mask, 8)
    if count > 1:
        mask = (labels == 1 + int(np.argmax(stats[1:, cv2.CC_STAT_AREA]))).astype(np.uint8)
    flood = mask.copy()
    cv2.floodFill(flood, np.zeros((mask.shape[0] + 2, mask.shape[1] + 2), np.uint8), (0, 0), 1)
    return mask | (1 - flood)


STEP = .25          # contour resampling step, frame pixels
SMOOTH = 2.2        # contour smoothing, frame pixels: irons out the video's compression wobble
CORNER = 55         # degrees of turn within +-2.5 px that make a tongue tip or the notch between two tongues...
REACH = 2.2         # ...when it also stands this far (frame pixels) off the chord 8 px either side
MAX_VERTICES = 510  # the shader takes up to 512 vertices per drawing


def circular_gauss(values, sigma):
    r = int(math.ceil(sigma * 3))
    kernel = np.exp(-.5 * (np.arange(-r, r + 1) / sigma) ** 2)
    kernel /= kernel.sum()
    padded = np.concatenate([values[-r:], values, values[:r]])
    return np.stack([np.convolve(padded[:, k], kernel, 'same')[r:-r] for k in range(values.shape[1])], 1)


def vectorize(mask_up):
    # The outline as a clean polygon in frame pixels: resampled evenly, smoothed along its length, and every
    # tongue tip and notch rebuilt as a hard corner where its two flanks meet, so the tips end in a point.
    contours, _ = cv2.findContours(mask_up, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
    c = (max(contours, key=cv2.contourArea).reshape(-1, 2).astype(np.float64) + .5) / UP
    closed = np.vstack([c, c[:1]])
    cum = np.concatenate([[0], np.cumsum(np.hypot(*np.diff(closed, axis=0).T))])
    n = int(cum[-1] / STEP)
    t = np.arange(n) * cum[-1] / n
    p = np.stack([np.interp(t, cum, closed[:, 0]), np.interp(t, cum, closed[:, 1])], 1)
    p = circular_gauss(p, SMOOTH / STEP)
    w = int(2.5 / STEP)
    a, b = p - np.roll(p, w, 0), np.roll(p, -w, 0) - p
    turn = np.abs(np.degrees(np.arctan2(a[:, 0] * b[:, 1] - a[:, 1] * b[:, 0], (a * b).sum(1))))
    # How far each point stands off the chord between the points 8 px before and after it: a real tongue or
    # notch stands well off it, a leftover wobble of the trace does not.
    reach_w = int(8 / STEP)
    before, after = np.roll(p, reach_w, 0), np.roll(p, -reach_w, 0)
    chord = after - before
    reach = np.abs(chord[:, 0] * (p - before)[:, 1] - chord[:, 1] * (p - before)[:, 0]) / np.maximum(np.hypot(*chord.T), 1e-6)
    w1, w2 = int(1.5 / STEP), int(6 / STEP)
    corners = []
    for i in np.argsort(-turn):
        if turn[i] < CORNER:
            break
        if reach[i] < REACH or any(min(abs(i - j), n - abs(i - j)) < w1 * 2 for j, _ in corners):
            continue
        flanks = []
        for side in (-1, 1):
            q = p[(i + side * np.arange(w1, w2)) % n]
            centre = q.mean(0)
            direction = np.linalg.svd(q - centre)[2][0]
            flanks.append((centre, direction))
        (c1, d1), (c2, d2) = flanks
        cross = d1[0] * d2[1] - d1[1] * d2[0]
        if abs(cross) < .05:
            continue
        s = ((c2 - c1)[0] * d2[1] - (c2 - c1)[1] * d2[0]) / cross
        point = c1 + d1 * s
        if np.linalg.norm(point - p[i]) > 5:
            continue
        corners.append((i, point))
    # Rebuild the loop as runs from one corner to the next. Each run is smoothed again, harder the longer it
    # is, with its two corner points pinned, so the flanks become clean curves and the points stay sharp.
    corners.sort(key=lambda c: c[0])
    if not corners:
        runs = [circular_gauss(p, 3 / STEP)]
    else:
        runs = []
        for k, (i, point) in enumerate(corners):
            j, next_point = corners[(k + 1) % len(corners)]
            start, end = i + w1 + 1, j - w1 - 1
            if k == len(corners) - 1:
                end += n  # the last run wraps round to the first corner
            idx = np.arange(start, end) % n if end > start else np.arange(0)
            run = np.vstack([point, p[idx], next_point]) if len(idx) else np.vstack([point, next_point])
            sigma = min(6, max(2, (len(run) * STEP) / 6)) / STEP
            r = min(int(sigma * 3), len(run) - 1)
            if r >= 1 and len(run) > 4:
                # Odd reflection about each end keeps the end points where they are.
                padded = np.vstack([2 * run[0] - run[r:0:-1], run, 2 * run[-1] - run[-2:-r - 2:-1]])
                kernel = np.exp(-.5 * (np.arange(-r, r + 1) / sigma) ** 2)
                kernel /= kernel.sum()
                smooth = np.stack([np.convolve(padded[:, c], kernel, 'same') for c in range(2)], 1)[r:-r]
                smooth[0], smooth[-1] = run[0], run[-1]
                run = smooth
            runs.append(run[:-1])
    poly = np.vstack(runs).astype(np.float32).reshape(-1, 1, 2)
    eps = .06
    while True:
        simple = cv2.approxPolyDP(poly, eps, True).reshape(-1, 2)
        if len(simple) <= MAX_VERTICES:
            return simple, len(corners)
        eps *= 1.3


def rasterize(poly):
    mask = np.zeros((H * UP, W * UP), np.uint8)
    cv2.fillPoly(mask, [np.round((poly * UP - .5) * 16).astype(np.int32)], 1, cv2.LINE_8, 4)
    return mask


shape_frames, polygons, boxes = [], [], []
for d, m in zip(drawings, masks):
    raw = d[y0:y1, x0:x1]
    poly, corner_count = vectorize(m[y0 * UP:y1 * UP, x0 * UP:x1 * UP].astype(np.uint8))
    polygons.append(poly)
    # The stored outline distance is only a guide for the halo and for finding the edge: the shader measures
    # the exact distance to the polygon near it.
    sdf = signed_distance(rasterize(poly))
    outline = np.clip(.5 - sdf / (2 * SDF_RANGE), 0, 1)
    # The glowing oval inside the tongues: everything more saturated than the cream (the yellow ring and the
    # fire inside it). The game keeps the ring and replaces the fire with its own effect, so the fighter stays
    # clear; this stores a signed distance to the oval's edge.
    fire = ((raw[..., 2] < .36) & (sdf < 0)).astype(np.uint8)
    fire = cv2.morphologyEx(fire, cv2.MORPH_CLOSE, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (15, 15)))
    fire = cv2.morphologyEx(fire, cv2.MORPH_OPEN, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (21, 21)))
    fire = largest_filled(fire)
    # The oval is drawn convex; its convex hull keeps bright pockets of the fire from biting into the ring.
    hull = cv2.convexHull(cv2.findNonZero(fire))
    fire = np.zeros_like(fire)
    cv2.fillConvexPoly(fire, hull, 1)
    bx, by, bw, bh = cv2.boundingRect(fire)
    boxes.append((bx, by, bx + bw, by + bh))
    fire_up = cv2.GaussianBlur(cv2.resize(fire.astype(np.float32), (W * UP, H * UP), interpolation=cv2.INTER_LINEAR), (0, 0), UP * 3) > .5
    inner_sdf = signed_distance(fire_up.astype(np.uint8))
    shape_frames.append(np.dstack([outline, np.clip(.5 - inner_sdf / (2 * INNER_RANGE), 0, 1), np.zeros_like(outline)]))
print('vertices per drawing: %d..%d' % (min(len(p) for p in polygons), max(len(p) for p in polygons)))
body_box = [float(v) for v in np.median(np.array(boxes, np.float32), 0)]
uys, uxs = np.nonzero(union[y0 * UP:y1 * UP, x0 * UP:x1 * UP])
outline_box = [uxs.min() / UP, uys.min() / UP, (uxs.max() + 1) / UP, (uys.max() + 1) / UP]
print('body box', body_box, 'outline box', outline_box)

# ---------------------------------------------------------------- lightning
def thin(mask):
    # Zhang-Suen thinning: reduces the drawn strokes to one-pixel centre lines, so the shader can draw the
    # line at a chosen width instead of the blurred width of the GIF.
    img = np.pad(mask.astype(np.uint8), 1)
    while True:
        changed = False
        for sub in (0, 1):
            p = [img[1:-1, 1:-1], img[:-2, 1:-1], img[:-2, 2:], img[1:-1, 2:], img[2:, 2:], img[2:, 1:-1], img[2:, :-2], img[1:-1, :-2], img[:-2, :-2]]
            p2, p3, p4, p5, p6, p7, p8, p9 = p[1:]
            b = sum(x.astype(np.int32) for x in p[1:])
            seq = [p2, p3, p4, p5, p6, p7, p8, p9, p2]
            a = sum(((seq[k] == 0) & (seq[k + 1] == 1)).astype(np.int32) for k in range(8))
            if sub == 0:
                c = (p2 * p4 * p6 == 0) & (p4 * p6 * p8 == 0)
            else:
                c = (p2 * p4 * p8 == 0) & (p2 * p6 * p8 == 0)
            remove = (p[0] == 1) & (b >= 2) & (b <= 6) & (a == 1) & c
            if remove.any():
                img[1:-1, 1:-1][remove] = 0
                changed = True
        if not changed:
            return img[1:-1, 1:-1]


gif =[np.array(f.convert('RGB')).astype(np.float32) / 255 for f in ImageSequence.Iterator(Image.open(os.path.join(SOURCE, 'lightning.gif')))]
PERIOD = 75
gif = gif[:PERIOD]
blue = (gif[0][..., 2] > .4).astype(np.uint8)
gy, gx = np.nonzero(blue)
gbox = [gx.min(), gy.min(), gx.max() + 1, gy.max() + 1]
# The bolts crackle round the fighter's body, so the blue aura's box maps onto the fighter: SaiyanAuraRig lays
# this frame out with the oval (body_box) 2.3 rig units tall, its bottom .06 under the soles, and the fighter
# 1.86 tall; the box is the fighter, a little wider than the shoulders and a little above the head.
bw, bh = body_box[2] - body_box[0], body_box[3] - body_box[1]
unit = bh / 2.3
soles = body_box[3] - .06 * unit
centre = (body_box[0] + body_box[2]) * .5
bx0, bx1 = centre - .42 * unit, centre + .42 * unit
by0, by1 = soles - (1.86 + .12) * unit, soles + .02 * unit
sx = (bx1 - bx0) / (gbox[2] - gbox[0])
sy = (by1 - by0) / (gbox[3] - gbox[1])
tx = bx0 - gbox[0] * sx
ty = by0 - gbox[1] * sy
print('lightning map scale', sx, sy)
RING = [(-1, 0), (-1, 1), (0, 1), (1, 1), (1, 0), (1, -1), (0, -1), (-1, -1)]


def trace(skeleton):
    # Walks the one-pixel centre lines into long paths. A pixel is an end or a junction by its crossing number
    # (how many separate runs of line touch it), not by its neighbour count, so the diagonal steps of a
    # staircase do not split a line; walking prefers straight neighbours over diagonal ones for the same reason.
    points = set(zip(*[v.tolist() for v in np.nonzero(skeleton)]))

    def crossing(p):
        ring = [(p[0] + dy, p[1] + dx) in points for dy, dx in RING]
        return sum(1 for k in range(8) if not ring[k] and ring[(k + 1) % 8])

    def around(p):
        straight = [(p[0] + dy, p[1] + dx) for dy, dx in RING[0::2] if (p[0] + dy, p[1] + dx) in points]
        diagonal = [(p[0] + dy, p[1] + dx) for dy, dx in RING[1::2] if (p[0] + dy, p[1] + dx) in points]
        return straight + diagonal

    nodes = [p for p in points if crossing(p) != 2]
    visited, paths = set(), []

    def walk(start):
        path, current = [start], start
        visited.add(start)
        while True:
            step = [n for n in around(current) if n not in visited]
            if not step:
                break
            current = step[0]
            visited.add(current)
            path.append(current)
            if crossing(current) != 2:
                break
        return path

    for start in sorted(nodes, key=crossing) + list(points):
        while any(n not in visited for n in around(start)):
            if start in visited and crossing(start) == 2:
                break
            path = walk(start)
            if len(path) < 2:
                break
            paths.append(path)
    return paths


def jagged(path, rng):
    # Straight runs with sharp corners, like the reference; a long straight run gets one more hard kink.
    poly = cv2.approxPolyDP(np.array([(x, y) for y, x in path], np.float32).reshape(-1, 1, 2), UP * 1.1, False).reshape(-1, 2)
    out = [poly[0]]
    for a, b in zip(poly[:-1], poly[1:]):
        length = float(np.linalg.norm(b - a))
        if length > UP * 11:
            normal = np.array([-(b - a)[1], (b - a)[0]], np.float32) / length
            out.append(a + (b - a) * rng.uniform(.35, .65) + normal * (1 if rng.random() < .5 else -1) * rng.uniform(1.2, 2.2) * UP)
        out.append(b)
    return np.round(np.array(out)).astype(np.int32)


rng = np.random.default_rng(7)
MAX_BOLT_POINTS = 256  # the shader takes up to 256 lightning corners per drawing
bolt_frames, bolt_lines, sequence = [], [], []
for g in gif:
    # Only the white-hot core: the blue glow is redrawn by the shader at full resolution.
    white = np.clip((g.min(-1) - .74) / .2, 0, 1)
    if white.sum() < 30:
        sequence.append(-1)
        continue
    up = cv2.resize(white, None, fx=UP, fy=UP, interpolation=cv2.INTER_CUBIC)
    M = np.array([[sx, 0, tx * UP], [0, sy, ty * UP]], np.float32)
    warped = cv2.warpAffine(up, M, (W * UP, H * UP), flags=cv2.INTER_LINEAR)
    stroke = (cv2.GaussianBlur(warped, (0, 0), UP * .3) > .45).astype(np.uint8)
    # Close the pinholes inside thick strokes, or thinning turns them into little loops.
    stroke = cv2.morphologyEx(stroke, cv2.MORPH_CLOSE, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (UP * 2 + 1, UP * 2 + 1)))
    skeleton = thin(stroke)
    # The stroke's own half thickness, carried from each centre line pixel to everything near it.
    thickness = cv2.distanceTransform(stroke, cv2.DIST_L2, cv2.DIST_MASK_PRECISE) / UP
    _, labels = cv2.distanceTransformWithLabels(1 - skeleton, cv2.DIST_L2, 5, labelType=cv2.DIST_LABEL_PIXEL)
    per_label = np.zeros(int(labels.max()) + 1, np.float32)
    per_label[labels[skeleton == 1]] = thickness[skeleton == 1]
    width_up = per_label[labels]
    width = cv2.resize(width_up, (W, H), interpolation=cv2.INTER_AREA)
    canvas = np.zeros_like(skeleton)
    lines = []
    for path in trace(skeleton):
        if len(path) >= 2:
            line = jagged(path, rng)
            cv2.polylines(canvas, [line.reshape(-1, 1, 2)], False, 1, 1, cv2.LINE_8)
            # The same line for the shader, in frame pixels, with the stroke's half thickness at each corner.
            xs = np.clip(line[:, 0], 0, W * UP - 1); ys = np.clip(line[:, 1], 0, H * UP - 1)
            lines.append(np.stack([(line[:, 0] + .5) / UP, (line[:, 1] + .5) / UP, width_up[ys, xs]], 1))
    # Longest lines first, so a frame with more corners than the shader takes loses only scraps.
    lines.sort(key=lambda l: -np.hypot(*np.diff(l[:, :2], axis=0).T).sum())
    points = []
    total = sum(len(l) for l in lines)
    if total > MAX_BOLT_POINTS:
        print('  lightning frame %d: %d corners, keeping the longest lines' % (len(bolt_frames), total))
    for line in lines:
        if len(points) + len(line) > MAX_BOLT_POINTS:
            continue
        for k, (x, y, w) in enumerate(line):
            points.append((x, y, w, 0 if k == 0 else 1))
    bolt_lines.append(np.array(points, np.float32))
    dist = cv2.distanceTransform(1 - canvas, cv2.DIST_L2, cv2.DIST_MASK_PRECISE) / UP
    dist = cv2.resize(dist, (W, H), interpolation=cv2.INTER_AREA)
    sequence.append(len(bolt_frames))
    bolt_frames.append(np.dstack([np.clip(dist / BOLT_RANGE, 0, 1), np.clip(width / BOLT_WIDTH, 0, 1), np.zeros_like(dist)]))
print('lightning: %d of %d frames lit, %d..%d corners each' % (len(bolt_frames), PERIOD, min(len(b) for b in bolt_lines), max(len(b) for b in bolt_lines)))


# ---------------------------------------------------------------- atlases
def atlas(frames, cols, channels):
    rows = math.ceil(len(frames) / cols)
    fw, fh = W + PAD * 2, H + PAD * 2
    out = np.zeros((rows * fh, cols * fw, channels), np.float32)
    for i, f in enumerate(frames):
        r, c = divmod(i, cols)
        f = f if f.ndim == 3 else f[..., None]
        # Edge pixels are repeated into the padding, so bilinear filtering never mixes neighbours.
        padded = cv2.copyMakeBorder(f, PAD, PAD, PAD, PAD, cv2.BORDER_REPLICATE)
        if padded.ndim == 2:
            padded = padded[..., None]
        out[r * fh:(r + 1) * fh, c * fw:(c + 1) * fw] = padded
    return out, cols, rows


shape_atlas, aura_cols, aura_rows = atlas(shape_frames, 8, 3)
bolt_atlas, bolt_cols, bolt_rows = atlas(bolt_frames, 8, 3)
os.makedirs(UNITY, exist_ok=True)
for old in ('aura_atlas.png', 'inner_atlas.png'):
    if os.path.exists(os.path.join(UNITY, old)):
        os.remove(os.path.join(UNITY, old))
Image.fromarray((shape_atlas * 255 + .5).astype(np.uint8), 'RGB').save(os.path.join(UNITY, 'shape_atlas.png'))
Image.fromarray((bolt_atlas * 255 + .5).astype(np.uint8), 'RGB').save(os.path.join(UNITY, 'bolt_atlas.png'))
# The outlines: drawing count, then per drawing its vertex count and x, y pairs in frame pixels (y down).
with open(os.path.join(UNITY, 'saiyanaura_polygons.bytes'), 'wb') as fh:
    fh.write(np.int32(len(polygons)).tobytes())
    for poly in polygons:
        fh.write(np.int32(len(poly)).tobytes())
        fh.write(poly.astype('<f4').tobytes())
# The lightning lines: drawing count, then per drawing its point count and x, y, half thickness, kind
# (0 starts a line, 1 continues it), frame pixels, y down.
with open(os.path.join(UNITY, 'saiyanaura_bolts.bytes'), 'wb') as fh:
    fh.write(np.int32(len(bolt_lines)).tobytes())
    for points in bolt_lines:
        fh.write(np.int32(len(points)).tobytes())
        fh.write(points.astype('<f4').tobytes())
print('atlases', shape_atlas.shape, bolt_atlas.shape)


def f(v):
    return ('%.4f' % v).rstrip('0').rstrip('.') + 'f'


cs = []
cs.append('// Generated by _Pipeline/saiyan_flipbook.py from the reference animations; do not edit by hand.')
cs.append('using UnityEngine;')
cs.append('')
cs.append('namespace DBPU')
cs.append('{')
cs.append('    public static class SaiyanAuraFrames')
cs.append('    {')
cs.append('        // One frame is Width x Height pixels plus Pad on every side; frames run left to right, top to bottom.')
cs.append('        public const int Width = %d, Height = %d, Pad = %d;' % (W, H, PAD))
cs.append('        public const int AuraFrames = %d, AuraColumns = %d, AuraRows = %d;' % (len(shape_frames), aura_cols, aura_rows))
cs.append('        public const int BoltFrames = %d, BoltColumns = %d, BoltRows = %d;' % (len(bolt_frames), bolt_cols, bolt_rows))
cs.append('        public const float DrawingsPerSecond = 12f, BoltFramesPerSecond = %s;' % f(1000 / 60))
cs.append('        // Ranges of the stored distances, in frame pixels: outline, inner oval, lightning, lightning half thickness.')
cs.append('        public const float OutlineRange = %s, InnerRange = %s, BoltRange = %s, BoltWidth = %s;' % (f(SDF_RANGE), f(INNER_RANGE), f(BOLT_RANGE), f(BOLT_WIDTH)))
cs.append('        // Boxes in frame pixels, y down: the oval body inside the tongues and everything the tongues reach.')
cs.append('        public static readonly Vector4 BodyBox = new Vector4(%s, %s, %s, %s);' % tuple(f(v) for v in body_box))
cs.append('        public static readonly Vector4 OutlineBox = new Vector4(%s, %s, %s, %s);' % tuple(f(v) for v in outline_box))
cs.append('        // The lightning drawing shown on each 60 ms step of the reference, -1 for none.')
cs.append('        public static readonly int[] BoltSequence = { %s };' % ', '.join(str(v) for v in sequence))
cs.append('    }')
cs.append('}')
with open(CS, 'w', encoding='utf-8', newline='\r\n') as fh:
    fh.write('\n'.join(cs) + '\n')
print('wrote', CS)

if preview:
    os.makedirs(preview, exist_ok=True)
    # Outlines at 8x: the traced video mask beside the cleaned polygon, over the upper left tongues.
    for i in (0, 20):
        zx, zy, zw, zh = 40, 40, 120, 150
        Z = 8
        trace_mask = cv2.resize(masks[i][y0 * UP:y1 * UP, x0 * UP:x1 * UP].astype(np.float32), (W * Z, H * Z), interpolation=cv2.INTER_LINEAR)
        clean = np.zeros((H * Z, W * Z), np.uint8)
        cv2.fillPoly(clean, [np.round((polygons[i] * Z - .5) * 16).astype(np.int32)], 255, cv2.LINE_AA, 4)
        a = (trace_mask[zy * Z:(zy + zh) * Z, zx * Z:(zx + zw) * Z] > .5).astype(np.uint8) * 255
        b = clean[zy * Z:(zy + zh) * Z, zx * Z:(zx + zw) * Z]
        Image.fromarray(np.concatenate([a, np.full((a.shape[0], 8), 128, np.uint8), b], 1)).save(os.path.join(preview, 'outline_%02d.png' % i))
    # Lightning decoded at 3x on black, beside the GIF frame it came from, both cropped to the bolt area.
    lit = [i for i, k in enumerate(sequence) if k >= 0]
    for n, gi in enumerate(lit[:4]):
        fr = cv2.resize(bolt_frames[sequence[gi]], (W * 3, H * 3), interpolation=cv2.INTER_LINEAR)
        b = fr[..., 0:1] * BOLT_RANGE * 3
        w = np.maximum(fr[..., 1:2] * BOLT_WIDTH * 3, 1.8)
        core = np.clip(w - b + .5, 0, 1)
        glow = np.exp(-np.maximum(b - w, 0) / 7) * (1 - core)
        img = core + glow * np.array([.55, .8, 1], np.float32)
        Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8)).save(os.path.join(preview, 'bolt_%d.png' % n))
        Image.fromarray((gif[gi] * 255).astype(np.uint8)).save(os.path.join(preview, 'gif_%d.png' % n))
