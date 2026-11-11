"""Author eight deforming Majin flesh meshes and bake albedo/normal flipbooks in Blender."""
import argparse
import math
import random
import sys
from pathlib import Path

import bpy
from mathutils import Vector

args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
parser = argparse.ArgumentParser()
parser.add_argument('--output', required=True)
parser.add_argument('--limit', type=int, default=8)
parser.add_argument('--size', type=int, default=128)
options = parser.parse_args(args)
output = Path(options.output)
output.mkdir(parents=True, exist_ok=True)
frames_dir = output / 'frames'
frames_dir.mkdir(exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.name = 'Majin living flesh — authored deformation'
scene.render.engine = 'CYCLES'
scene.cycles.device = 'CPU'
scene.cycles.samples = 12
scene.cycles.use_denoising = False
scene.render.threads_mode = 'FIXED'
scene.render.threads = 8
scene.render.resolution_x = scene.render.resolution_y = options.size
scene.render.resolution_percentage = 100
scene.render.film_transparent = True
scene.render.image_settings.file_format = 'PNG'
scene.render.image_settings.color_mode = 'RGBA'
scene.render.image_settings.color_depth = '8'
scene.render.fps = 24
scene.frame_start, scene.frame_end = 1, 24
scene.view_settings.view_transform = 'Raw'
scene.view_settings.look = 'None'
scene.view_settings.exposure = 0
scene.view_settings.gamma = 1
scene.render.dither_intensity = 0

camera_data = bpy.data.cameras.new('Orthographic game bake camera')
camera = bpy.data.objects.new('Orthographic game bake camera', camera_data)
scene.collection.objects.link(camera)
camera.location = (0, -6, 1.6)
camera.rotation_euler = (Vector((0, 0, 0)) - camera.location).to_track_quat('-Z', 'Y').to_euler()
camera_data.type = 'ORTHO'
camera_data.ortho_scale = 2.25
scene.camera = camera

material = bpy.data.materials.new('Majin flesh — neutral albedo + camera normal AOV')
material.use_nodes = True
nodes = material.node_tree.nodes
nodes.clear()
links = material.node_tree.links
surface = nodes.new('ShaderNodeOutputMaterial')
emission = nodes.new('ShaderNodeEmission')
occlusion = nodes.new('ShaderNodeAmbientOcclusion')
occlusion.inputs['Distance'].default_value = .24
occlusion.samples = 12
noise = nodes.new('ShaderNodeTexNoise')
noise.inputs['Scale'].default_value = 21
noise.inputs['Detail'].default_value = 2
ramp = nodes.new('ShaderNodeValToRGB')
ramp.color_ramp.elements[0].color = (.67, .67, .67, 1)
ramp.color_ramp.elements[1].color = (.96, .96, .96, 1)
multiply = nodes.new('ShaderNodeMixRGB')
multiply.blend_type = 'MULTIPLY'
multiply.inputs[0].default_value = 1
links.new(noise.outputs['Fac'], ramp.inputs[0])
links.new(occlusion.outputs['Color'], multiply.inputs[1])
links.new(ramp.outputs['Color'], multiply.inputs[2])
links.new(multiply.outputs[0], emission.inputs['Color'])
links.new(emission.outputs[0], surface.inputs['Surface'])

geometry = nodes.new('ShaderNodeNewGeometry')
combine_normal = nodes.new('ShaderNodeCombineXYZ')
# Explicit camera basis avoids renderer-dependent camera-space normal conventions.
camera_basis = camera.rotation_euler.to_matrix()
for axis in range(3):
    dot = nodes.new('ShaderNodeVectorMath')
    dot.operation = 'DOT_PRODUCT'
    dot.inputs[1].default_value = camera_basis.col[axis]
    links.new(geometry.outputs['Normal'], dot.inputs[0])
    links.new(dot.outputs['Value'], combine_normal.inputs[axis])
encode = nodes.new('ShaderNodeVectorMath')
encode.operation = 'MULTIPLY_ADD'
encode.inputs[1].default_value = (.5, .5, .5)
encode.inputs[2].default_value = (.5, .5, .5)
aov_node = nodes.new('ShaderNodeOutputAOV')
aov_node.name = aov_node.aov_name = 'FleshNormal'
links.new(combine_normal.outputs[0], encode.inputs[0])
links.new(encode.outputs['Vector'], aov_node.inputs['Color'])
aov = scene.view_layers[0].aovs.add()
aov.name, aov.type = 'FleshNormal', 'COLOR'

# Blender 5 uses the scene compositor node-tree datablock explicitly.
tree = bpy.data.node_groups.new('Majin flesh bake compositor', 'CompositorNodeTree')
scene.compositing_node_group = tree
tree.interface.new_socket(name='Image', in_out='OUTPUT', socket_type='NodeSocketColor')
layers = tree.nodes.new('CompositorNodeRLayers')
composite = tree.nodes.new('NodeGroupOutput')
links_c = tree.links
links_c.new(layers.outputs['Image'], composite.inputs['Image'])
normal_output = tree.nodes.new('CompositorNodeOutputFile')
normal_output.directory = str(frames_dir)
normal_output.file_output_items.new('RGBA', 'Normal')
normal_output.save_as_render = False
normal_output.format.media_type = 'IMAGE'
normal_output.format.file_format = 'PNG'
normal_output.format.color_mode = 'RGBA'
normal_output.format.color_depth = '8'
normal_alpha = tree.nodes.new('CompositorNodeSetAlpha')
links_c.new(layers.outputs['FleshNormal'], normal_alpha.inputs['Image'])
links_c.new(layers.outputs['Alpha'], normal_alpha.inputs['Alpha'])
links_c.new(normal_alpha.outputs['Image'], normal_output.inputs[0])

def ellipsoid(name, location, scale, seed):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=3, radius=1, location=location)
    obj = bpy.context.object
    obj.name = name
    for vertex in obj.data.vertices:
        n = vertex.co.copy()
        wave = 1 + .095 * math.sin(n.x * 5.7 + seed) * math.cos(n.z * 4.3 - seed)
        wave += .045 * math.sin(n.y * 8.3 + n.z * 5 + seed)
        vertex.co = Vector((n.x * scale[0], n.y * scale[1], n.z * scale[2])) * wave
    return obj

meshes = []
profiles = [(.66, .38, .40), (.43, .39, .63), (.64, .27, .32), (.52, .38, .48),
            (.59, .34, .39), (.46, .34, .55), (.63, .30, .34), (.53, .40, .43)]
for variation, profile in enumerate(profiles):
    rng = random.Random(7421 + variation * 119)
    pieces = [ellipsoid(f'Flesh {variation + 1} core', (0, 0, 0), profile, variation * .73)]
    # Fused lobes, a folded rim and short torn nubs: gum anatomy, no human organs.
    for index in range(4):
        theta = index * 1.57 + variation * .31
        location = (math.cos(theta) * profile[0] * .75, -.10 + rng.uniform(-.08, .07), math.sin(theta) * profile[2] * .72)
        scale = (rng.uniform(.18, .31), rng.uniform(.16, .25), rng.uniform(.17, .30))
        pieces.append(ellipsoid(f'Flesh {variation + 1} lobe {index + 1}', location, scale, variation + index))
    for index in range(2):
        side = -1 if index == 0 else 1
        location = (side * profile[0] * .92, .02, rng.uniform(-.20, .25))
        pieces.append(ellipsoid(f'Flesh {variation + 1} torn lip {index + 1}', location,
                                (.22, .14, .095 + variation % 3 * .025), variation + 4))
    bpy.ops.object.select_all(action='DESELECT')
    for part in pieces:
        part.select_set(True)
    bpy.context.view_layer.objects.active = pieces[0]
    bpy.ops.object.join()
    obj = pieces[0]
    obj.name = f'Majin flesh {variation + 1:02} — pulse curl quiver'
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    remesh = obj.modifiers.new('Fused wet flesh lobes', 'REMESH')
    remesh.mode = 'VOXEL'
    remesh.voxel_size = .045
    remesh.use_smooth_shade = True
    bpy.ops.object.modifier_apply(modifier=remesh.name)
    smooth = obj.modifiers.new('Soft torn folds', 'SMOOTH')
    smooth.factor, smooth.iterations = 1.1, 3
    bpy.ops.object.modifier_apply(modifier=smooth.name)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    obj.data.materials.append(material)
    obj.shape_key_add(name='Basis')
    pulse = obj.shape_key_add(name='Slow living pulse')
    curl = obj.shape_key_add(name='Asymmetric contraction')
    shiver_a = obj.shape_key_add(name='Nervous twitch left')
    shiver_b = obj.shape_key_add(name='Nervous twitch right')
    for index, vertex in enumerate(obj.data.vertices):
        x, y, z = vertex.co
        pulse.data[index].co = (x * 1.09, y * 1.16, z * 1.06)
        curl.data[index].co = (x * .88 + .14 * math.sin(z * 4 + variation), y, z * .92 + .08 * math.cos(x * 4))
        shiver_a.data[index].co = (x + .045 * math.sin(z * 10 + variation), y + .03 * math.cos(x * 8), z + .035 * math.sin(x * 9))
        shiver_b.data[index].co = (x - .038 * math.cos(z * 12 + variation), y - .025 * math.sin(z * 7), z - .03 * math.cos(x * 10))
    channels = [
        (pulse, [(1, 0), (5, .28), (11, .86), (15, .35), (20, .13), (25, 0)]),
        (curl, [(1, 0), (4, .12), (9, .55), (14, .05), (19, .72), (23, .18), (25, 0)]),
        (shiver_a, [(1, 0), (3, .05), (4, .9), (6, .08), (10, .12), (11, .62), (13, 0), (17, .08), (18, .85), (20, .1), (25, 0)]),
        (shiver_b, [(1, 0), (6, .12), (7, .78), (9, .04), (14, .1), (15, .93), (17, .1), (22, .55), (24, .1), (25, 0)]),
    ]
    for key, points in channels:
        for frame, value in points:
            key.value = value
            key.keyframe_insert('value', frame=frame)
    obj.data.shape_keys.animation_data.action.name = f'MajinFlesh_AuthoredQuiver_{variation + 1:02}'
    subdivision = obj.modifiers.new('Smooth animation surface', 'SUBSURF')
    subdivision.levels = subdivision.render_levels = 1
    obj.hide_render = True
    meshes.append(obj)

scene['pipeline'] = 'Blender-authored shape-key animation -> albedo/camera-normal flipbooks -> Unity wet/translucent shader'
scene['animation_fps'] = 24
scene['frames_per_variant'] = 24
scene['variant_count'] = 8
scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=str(output / 'MajinFlesh.blend'))

for variation, obj in enumerate(meshes[:options.limit]):
    obj.hide_render = False
    for frame in range(1, 25):
        scene.frame_set(frame)
        scene.render.filepath = str(frames_dir / f'albedo-v{variation:02}-f{frame - 1:02}.png')
        normal_output.file_name = f'normal-v{variation:02}-f{frame - 1:02}-'
        bpy.ops.render.render(write_still=True)
        print(f'MAJIN_FRAME {variation} {frame - 1}', flush=True)
    obj.hide_render = True
for index, obj in enumerate(meshes):
    obj.hide_render = index != 0
    obj.hide_set(index != 0)
bpy.ops.object.select_all(action='DESELECT')
meshes[0].select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=str(output / 'MajinFlesh.blend'))
print('MAJIN_BLENDER_BAKE_COMPLETE', flush=True)
