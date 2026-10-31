"""Render a seamless 4 s loop of the wreck's sail strips swaying.
blender.exe -b shipwreck/shipwreck.blend --python sail_clip.py -- OUT.mp4
"""
import math
import sys

import bpy
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = argv[0] if argv else r"D:\blender-5.2.3\projects\coral_growth\shipwreck\sail_sway.mp4"

scene = bpy.context.scene
sail = bpy.data.objects["Wreck_Sail"]
dg = bpy.context.evaluated_depsgraph_get()
pts = [sail.matrix_world @ Vector(c) for c in sail.bound_box]
centre = sum(pts, Vector()) / 8

cam = bpy.data.objects.new("ClipCam", bpy.data.cameras.new("ClipCam"))
cam.data.lens = 45
scene.collection.objects.link(cam)
ang, elev, dist = math.radians(-70), math.radians(14), 6.0
cam.location = centre + Vector((math.sin(ang) * math.cos(elev), -math.cos(ang) * math.cos(elev), math.sin(elev))) * dist
cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Y").to_euler()
scene.camera = cam

r = scene.render
r.engine = "BLENDER_EEVEE"
r.resolution_x, r.resolution_y, r.resolution_percentage = 960, 960, 100
img = r.image_settings
if hasattr(img, "media_type"):
    img.media_type = "VIDEO"
img.file_format = "FFMPEG"
r.ffmpeg.format = "MPEG4"
r.ffmpeg.codec = "H264"
r.ffmpeg.constant_rate_factor = "HIGH"
r.ffmpeg.ffmpeg_preset = "GOOD"
r.ffmpeg.audio_codec = "NONE"
r.use_file_extension = False
r.filepath = OUT
scene.render.fps = 24
scene.frame_start, scene.frame_end = 1, 96      # frame 97 == frame 1: seamless loop
bpy.ops.render.render(animation=True)
print("CLIP_DONE", OUT)
