"""Author normalized piece-flight and contact curves as real Blender animation actions."""
from pathlib import Path
import json
import math
import bpy

source = Path(bpy.data.filepath)
scene = bpy.context.scene
collection = bpy.data.collections.get('Authored assembly trajectories')
if collection is None:
    collection = bpy.data.collections.new('Authored assembly trajectories')
    scene.collection.children.link(collection)
for obj in collection.objects:
    obj.animation_data_clear()
for action in list(bpy.data.actions):
    if action.name.startswith('MajinPiece_AuthoredFlightAndSnap_') and action.users == 0:
        bpy.data.actions.remove(action)
clips = []
for variation in range(8):
    name = f'Assembly trajectory {variation + 1:02}'
    obj = bpy.data.objects.get(name)
    if obj is None:
        obj = bpy.data.objects.new(name, None)
        collection.objects.link(obj)
    obj.animation_data_clear()
    obj.empty_display_type = 'ARROWS'
    obj.empty_display_size = .14
    obj.location = (3 + variation * .35, 0, 0)
    sign = -1 if variation % 2 else 1
    channels = {
        'approach': [(1, 0), (5, .025), (9, .13), (13, .37), (17, .74), (21, .97), (23, 1.018), (25, 1)],
        'side': [(1, 0), (6, sign * (.08 + variation * .012)), (12, sign * .055), (18, -sign * .018), (22, sign * .004), (25, 0)],
        'lift': [(1, 0), (7, .15 + variation % 3 * .035), (13, .20), (18, .055), (22, .006), (25, 0)],
        'twist': [(1, sign * (28 + variation * 4)), (7, sign * (45 + variation * 3)), (13, -sign * 13), (19, sign * 4), (23, -sign * 1.5), (25, 0)],
        'settle': [(1, 0), (19, 0), (21, .35), (22, 1), (23, .32), (24, .08), (25, 0)],
    }
    for channel, points in channels.items():
        if channel in obj:
            del obj[channel]
        obj[channel] = 0.0
        for frame, value in points:
            obj[channel] = float(value)
            obj.keyframe_insert(data_path=f'["{channel}"]', frame=frame)
    obj.animation_data.action.name = f'MajinPiece_AuthoredFlightAndSnap_{variation + 1:02}'
    clip = {key: [] for key in channels}
    for sample in range(64):
        frame = 1 + sample / 63 * 24
        scene.frame_set(math.floor(frame), subframe=frame % 1)
        for key in channels:
            clip[key].append(round(float(obj[key]), 6))
    clips.append(clip)
scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=str(source))
data = {'version': 1, 'samples': 64, 'authoring': 'Blender 5.2.3 LTS keyframed actions',
        'source': source.name, 'frames': [1, 25], 'fps': 24, 'variants': clips}
destination = Path(r'C:\Program Files (x86)\People Playground\Mods\Dragon Ball Singularity\Assets\VFX\MajinFlesh\assembly-motion.json')
destination.parent.mkdir(parents=True, exist_ok=True)
destination.write_text(json.dumps(data, separators=(',', ':')), encoding='utf-8')
(source.parent / 'assembly-motion.json').write_text(json.dumps(data, indent=2), encoding='utf-8')
print('BLENDER_ASSEMBLY_ACTIONS', len(clips), 'SAMPLES', len(clips[0]['approach']), flush=True)
