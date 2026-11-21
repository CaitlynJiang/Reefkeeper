"""Transparent PNG icons, one per Shell_* tier, shot straight at the shell face.
blender.exe -b shell_coin.blend --python export_icons.py -- [size]
Writes icon_<tier>.png next to the .blend, plus icons_sheet.png (all tiers side by side).
"""
import os
import sys

import bpy
import numpy as np
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
size = int(argv[0]) if argv else 512
folder = os.path.dirname(bpy.data.filepath)
scene = bpy.context.scene


def tree(o):
    yield o
    for c in o.children:
        yield from tree(c)


roots = sorted([o for o in scene.objects if o.name.startswith("Shell_") and o.parent is None], key=lambda o: o.name)
scene.objects["Floor"].hide_render = True
scene.render.film_transparent = True
scene.render.resolution_x = scene.render.resolution_y = size
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"

cam = bpy.data.objects.new("IconCam", bpy.data.cameras.new("IconCam"))
scene.collection.objects.link(cam)
cam.data.type = "ORTHO"
cam.data.ortho_scale = 2.0
scene.camera = cam

tiles = []
for r in roots:
    for other in roots:
        for o in tree(other):
            o.hide_render = other is not r
    mw = r.matrix_world
    face = (mw.to_3x3() @ Vector((0, 0, 1))).normalized()       # shell dome normal
    center = mw @ Vector((0, 0.38, 0.1))
    cam.location = center + face * 10
    cam.rotation_euler = (-face).to_track_quat("-Z", "Y").to_euler()
    path = os.path.join(folder, f"icon_{r.name.split('_', 2)[2].lower()}.png")
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    img = bpy.data.images.load(path)
    a = np.empty(size * size * 4, dtype=np.float32)
    img.pixels.foreach_get(a)
    tiles.append(a.reshape(size, size, 4))
    bpy.data.images.remove(img)
    print("ICON", path)

sheet = bpy.data.images.new("sheet", width=size * len(tiles), height=size, alpha=True)
sheet.pixels.foreach_set(np.concatenate(tiles, axis=1).ravel())
sheet.filepath_raw = os.path.join(folder, "icons_sheet.png")
sheet.file_format = "PNG"
sheet.save()
print("SHEET", sheet.filepath_raw)
