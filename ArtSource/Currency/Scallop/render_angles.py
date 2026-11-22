"""Render the original coral shell from six camera angles; never save over its blend."""
import math
from pathlib import Path
import bpy
import numpy as np
from mathutils import Vector, Quaternion

out = Path(bpy.data.filepath).parent / 'AnglePreviews'
out.mkdir(exist_ok=True)
scene = bpy.context.scene
roots = [o for o in scene.objects if o.name.startswith('Shell_') and o.parent is None]
shell = next(o for o in roots if o.name == 'Shell_2_Coral')
def family(obj):
    yield obj
    for child in obj.children:
        yield from family(child)
for root in roots:
    for obj in family(root):
        obj.hide_render = root != shell
if 'Floor' in scene.objects:
    scene.objects['Floor'].hide_render = True
scene.render.film_transparent = True
scene.render.resolution_x = scene.render.resolution_y = 512
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.image_settings.color_mode = 'RGBA'
scene.render.engine = 'BLENDER_EEVEE'
if scene.render.engine == 'CYCLES':
    scene.cycles.samples = 32
cam = bpy.data.objects.new('AnglePreviewCamera', bpy.data.cameras.new('AnglePreviewCamera'))
scene.collection.objects.link(cam)
cam.data.type = 'ORTHO'
cam.data.ortho_scale = 1.85
scene.camera = cam
bpy.context.view_layer.update()
matrix = shell.matrix_world.copy()
center = matrix @ Vector((0, .38, .1))
views = [('01_front',0,0,0), ('02_right_15',15,0,-5),
         ('03_right_30',30,0,-8), ('04_left_25',-25,0,8),
         ('05_tilt_25',25,-20,-10), ('06_side_45',45,-10,-5)]
tiles = []
for name, yaw, pitch, roll in views:
    y, p = math.radians(yaw), math.radians(pitch)
    direction = (matrix.to_3x3() @ Vector((math.sin(y)*math.cos(p), math.sin(p), math.cos(y)*math.cos(p)))).normalized()
    cam.location = center + direction * 10
    rotation = (-direction).to_track_quat('-Z','Y')
    cam.rotation_mode = 'QUATERNION'
    cam.rotation_quaternion = rotation @ Quaternion((0,0,1), math.radians(roll))
    scene.render.filepath = str(out / (name + '.png'))
    bpy.ops.render.render(write_still=True)
    img = bpy.data.images.load(scene.render.filepath)
    pixels = np.empty(512*512*4, dtype=np.float32)
    img.pixels.foreach_get(pixels)
    tiles.append(pixels.reshape(512,512,4).copy())
    bpy.data.images.remove(img)
    print('RENDERED', scene.render.filepath, flush=True)

# Blender image pixels have a bottom-left origin. Keep row order human-readable.
sheet = np.concatenate([np.concatenate(tiles[3:], axis=1), np.concatenate(tiles[:3], axis=1)], axis=0)
alpha = sheet[:,:,3:4]
background = np.array([.10,.24,.25],dtype=np.float32)
sheet[:,:,:3] = sheet[:,:,:3]*alpha + background*(1-alpha)
sheet[:,:,3] = 1
image = bpy.data.images.new('Scallop angle comparison', width=1536, height=1024, alpha=True)
image.pixels.foreach_set(sheet.ravel())
image.filepath_raw = str(out / 'comparison.png')
image.file_format = 'PNG'
image.save()
print('DONE', image.filepath_raw, flush=True)
