# Rebuilds Assets/VFX/ReduxAura/redux_aura from Dragon Ball Redux's Union/Bundles/redux_aura:
# doubles the silhouette atlas and the lightning sheet, sharpens them, and gives the bundle its own
# internal name so Unity can load it next to Redux's copy. Needs: pip install UnityPy Pillow numpy lz4
#   python build_reduxaura.py <redux_aura source> <output file>
import sys

import numpy as np
import UnityPy
from PIL import Image, ImageFilter

ATLAS, LIGHTNING = "ReduxAura_ShapeAtlas", "2023-06-24_21-16-33 lightnign2"
FRAMES = 4
ALPHA8, RGBA32 = 1, 4


def sharpen_atlas(image):
    alpha = image.convert("RGBA").getchannel("A")
    width, height = alpha.size
    frame_width = width // FRAMES
    out = Image.new("L", (width * 2, height * 2))
    for i in range(FRAMES):
        # One frame at a time, so resampling never bleeds a neighbour into the frame edge.
        frame = alpha.crop((i * frame_width, 0, (i + 1) * frame_width, height))
        frame = frame.resize((frame_width * 2, height * 2), Image.LANCZOS)
        frame = frame.filter(ImageFilter.UnsharpMask(radius=7, percent=170, threshold=0))
        a = np.asarray(frame).astype(np.float32) / 255
        # Drop the faint fringe, so the spike tips end instead of dissolving.
        a = np.clip((a - .1) / .8, 0, 1)
        # The tallest spikes touch the frame top, where the shader cuts them with a hard line.
        h, w = a.shape
        top = np.clip(np.arange(h, dtype=np.float32) / (h * .06), 0, 1)
        side = np.clip(np.minimum(np.arange(w), np.arange(w)[::-1]) / (w * .01), 0, 1)
        a = a * (top * top * (3 - 2 * top))[:, None] * side[None, :].astype(np.float32)
        out.paste(Image.fromarray((a * 255 + .5).astype(np.uint8), "L"), (i * frame_width * 2, 0))
    return out.transpose(Image.FLIP_TOP_BOTTOM).tobytes(), width * 2, height * 2, 1


def sharpen_lightning(image):
    rgba = image.convert("RGBA")
    rgba = rgba.resize((rgba.width * 2, rgba.height * 2), Image.LANCZOS)
    rgba = rgba.filter(ImageFilter.UnsharpMask(radius=3, percent=120, threshold=0))
    width, height = rgba.size
    data, level, count = [], rgba.transpose(Image.FLIP_TOP_BOTTOM), 0
    while True:
        data.append(level.tobytes())
        count += 1
        if level.width == 1 and level.height == 1:
            break
        level = level.resize((max(1, level.width // 2), max(1, level.height // 2)), Image.BOX)
    return b"".join(data), width, height, count


def main(source, target):
    env = UnityPy.load(source)
    bundle = env.file
    old = next(k for k in bundle.files if k.startswith("CAB-") and "." not in k)
    new = "CAB-db51" + old[8:]
    resource = bundle.files[old + ".resS"]
    resource_bytes = resource.bytes if hasattr(resource, "bytes") else bytes(resource)
    for obj in bundle.files[old].objects.values():
        if obj.type.name != "Texture2D":
            continue
        tree = obj.read_typetree()
        name = tree["m_Name"]
        if name == ATLAS:
            data, width, height, mips = sharpen_atlas(obj.read().image)
            tree["m_TextureFormat"] = ALPHA8
        elif name == LIGHTNING:
            data, width, height, mips = sharpen_lightning(obj.read().image)
            tree["m_TextureFormat"] = RGBA32
        else:
            stream = tree["m_StreamData"]
            data = resource_bytes[stream["offset"]:stream["offset"] + stream["size"]]
            width, height, mips = tree["m_Width"], tree["m_Height"], tree["m_MipCount"]
        tree["m_Width"], tree["m_Height"], tree["m_MipCount"] = width, height, mips
        tree["m_CompleteImageSize"] = len(data)
        tree["image data"] = data
        tree["m_StreamData"] = {"offset": 0, "size": 0, "path": ""}
        obj.save_typetree(tree)
        print(name, width, height, "mips", mips, "bytes", len(data))
    # Every texture is inline now, so the old resource node only wastes space.
    del bundle.files[old + ".resS"]
    file = bundle.files.pop(old)
    file.name = new
    bundle.files[new] = file
    with open(target, "wb") as handle:
        handle.write(bundle.save(packer="lz4"))
    print("OK", target, list(UnityPy.load(target).file.files))


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
