"""Rig + mood animations for the lucky cat model (D:/blender-5.2.3/projects/lucky_cat/lucky_cat_calico.blend).

The model is rigid parts under the empty 'Cat_Rig'. This adds an armature, parents every part to a
bone without moving it, and keys one action per mood (24 fps):
  Idle (loop), Happy (loop), Beckon (loop, the lucky-cat wave), Surprised (one-shot),
  Sleepy (loop), Angry (loop), Sad (loop)
Saves a NEW file next to the source (…_anim.blend); the original is never overwritten.
With 'preview', also renders all clips back to back into an MP4 + a contact sheet.

blender.exe -b --python lucky_cat_anim.py -- [SRC_BLEND] [preview]
"""
import math
import os
import sys

sys.dont_write_bytecode = True

import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
SRC = argv[0] if argv and argv[0].endswith(".blend") else "D:/blender-5.2.3/projects/lucky_cat/lucky_cat_calico.blend"
PREVIEW = "preview" in argv
OUT = SRC.replace(".blend", "_anim.blend")
OUT_DIR = os.path.dirname(SRC)

# bone: (parent, pivot head, objects).  Every bone points straight up, so for all of them:
#   rot X+ = tip forward (toward the face side, -Y)   rot Y = turn about vertical   rot Z+ = lean toward -X
#   location Y = up,   scale Y = vertical stretch
BONES = {
    "Root":   (None,   (0, 0, 0.0),        []),
    "Body":   ("Root", (0, 0, 0.38),       ["Cat_Body", "Cat_Collar", "Cat_Bell_0", "Cat_Bell_1", "Cat_Sack", "Cat_Ingot_0", "Cat_Ingot_1"]),
    "Head":   ("Body", (0, 0, 0.93),       ["Cat_Head", "Cat_Muzzle_L", "Cat_Muzzle_R", "Cat_Nose",
                                            "Cat_Whisker_L0", "Cat_Whisker_L1", "Cat_Whisker_L2",
                                            "Cat_Whisker_R0", "Cat_Whisker_R1", "Cat_Whisker_R2"]),
    "Eye_L":  ("Head", (0.25, -0.466, 1.34), ["Cat_Eye_L"]),
    "Eye_R":  ("Head", (-0.25, -0.466, 1.34), ["Cat_Eye_R"]),
    "Coin":   ("Head", (0, -0.465, 1.145),  ["Cat_Coin"]),
    "Ear_L":  ("Head", (0.33, 0.0, 1.62),   ["Cat_Ear_L", "Cat_EarInner_L"]),
    "Ear_R":  ("Head", (-0.33, 0.0, 1.62),  ["Cat_Ear_R", "Cat_EarInner_R"]),
    "Arm_L":  ("Body", (0.31, -0.06, 0.84), ["Cat_Arm_L"]),
    "Arm_R":  ("Body", (-0.31, -0.06, 0.84), ["Cat_Arm_R"]),
    "Tail":   ("Body", (0, 0.3, 0.3),       ["Cat_Tail"]),
    "Leg_L":  ("Root", (0.15, -0.02, 0.34), ["Cat_Leg_L"]),
    "Leg_R":  ("Root", (-0.15, -0.02, 0.34), ["Cat_Leg_R"]),
}

CLIPS = []          # (name, start, end, loop)


def build_rig():
    scene = bpy.context.scene
    rig_empty = bpy.data.objects.get("Cat_Rig")
    arm_data = bpy.data.armatures.new("Cat_Armature")
    arm = bpy.data.objects.new("Cat_Armature", arm_data)
    scene.collection.objects.link(arm)
    if rig_empty:
        arm.parent = rig_empty
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    eb = {}
    for name, (parent, head, _) in BONES.items():
        b = arm_data.edit_bones.new(name)
        b.head = Vector(head)
        b.tail = Vector(head) + Vector((0, 0, 0.12 if name != "Root" else 0.25))
        b.roll = 0.0
        if parent:
            b.parent = eb[parent]
            b.use_connect = False
        eb[name] = b
    bpy.ops.object.mode_set(mode="OBJECT")
    arm.show_in_front = True
    bpy.context.view_layer.update()
    # parent the parts to their bones, keeping them exactly where they are
    for name, (_, _, objs) in BONES.items():
        for on in objs:
            ob = bpy.data.objects.get(on)
            if not ob:
                print("[cat] missing", on)
                continue
            mw = ob.matrix_world.copy()
            ob.parent = arm
            ob.parent_type = "BONE"
            ob.parent_bone = name
            bpy.context.view_layer.update()
            ob.matrix_world = mw
    for pb in arm.pose.bones:
        pb.rotation_mode = "XYZ"
    return arm


# ---------------------------------------------------------------- keying helpers

def key(arm, bone, f, rot=None, loc=None, scale=None):
    pb = arm.pose.bones[bone]
    if rot is not None:
        pb.rotation_euler = [math.radians(a) for a in rot]
        pb.keyframe_insert("rotation_euler", frame=f)
    if loc is not None:
        pb.location = loc
        pb.keyframe_insert("location", frame=f)
    if scale is not None:
        pb.scale = scale if len(scale) == 3 else (scale[0], scale[0], scale[0])
        pb.keyframe_insert("scale", frame=f)


def rest_all(arm, f):
    for b in BONES:
        key(arm, b, f, rot=(0, 0, 0), loc=(0, 0, 0), scale=(1, 1, 1))


def sq(y):
    """Volume-keeping vertical squash/stretch (bone Y is up)."""
    s = 1 / math.sqrt(y)
    return (s, y, s)


def eyes(arm, f, open_=1.0, size=1.0):
    for e in ("Eye_L", "Eye_R"):
        key(arm, e, f, scale=(size, open_ * size, size))


def new_action(arm, name, end, loop=True):
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    arm.animation_data_create()
    arm.animation_data.action = act
    for pb in arm.pose.bones:
        pb.rotation_euler = (0, 0, 0)
        pb.location = (0, 0, 0)
        pb.scale = (1, 1, 1)
    CLIPS.append((name, 0, end, loop))
    return act


def fcurves(act):
    """F-curves of an action, for both legacy and layered (Blender 4.4+/5.x) actions."""
    out = []
    if getattr(act, "layers", None):
        for layer in act.layers:
            for strip in layer.strips:
                for bag in getattr(strip, "channelbags", []):
                    out += list(bag.fcurves)
    elif hasattr(act, "fcurves"):
        out = list(act.fcurves)
    return out


def finish(act, loop):
    for fc in fcurves(act):
        for kp in fc.keyframe_points:
            kp.interpolation = "BEZIER"
            kp.handle_left_type = kp.handle_right_type = "AUTO_CLAMPED"
        if loop:
            fc.modifiers.new("CYCLES")


# ---------------------------------------------------------------- the moods

def clip_idle(arm):
    act = new_action(arm, "Idle", 96)
    rest_all(arm, 0)
    for f in (0, 48, 96):
        key(arm, "Body", f, scale=sq(1.0))
        key(arm, "Head", f, rot=(0, 0, 0))
    for f in (24, 72):
        key(arm, "Body", f, scale=sq(1.03))                     # breathe in
    key(arm, "Head", 48, rot=(2, 2, 3))
    key(arm, "Head", 24, rot=(-1, -1, -1.5))
    key(arm, "Head", 72, rot=(1, 1, -2))
    for f, a in ((0, -10), (24, 10), (48, -10), (72, 10), (96, -10)):
        key(arm, "Tail", f, rot=(0, a, 0))
    for f, a in ((0, 0), (24, -3), (48, 0), (72, -3), (96, 0)):   # arms rise a little with each breath
        key(arm, "Arm_L", f, rot=(a, 0, -a * 0.5))
        key(arm, "Arm_R", f, rot=(a, 0, a * 0.5))
    for e in ("Ear_L", "Ear_R"):
        key(arm, e, 0, rot=(0, 0, 0))
        key(arm, e, 96, rot=(0, 0, 0))
    key(arm, "Ear_R", 58, rot=(0, 0, 0))
    key(arm, "Ear_R", 61, rot=(-10, 0, 18))                      # ear twitch
    key(arm, "Ear_R", 64, rot=(0, 0, -4))
    key(arm, "Ear_R", 68, rot=(0, 0, 0))
    eyes(arm, 0)
    eyes(arm, 38)
    eyes(arm, 41, 0.08)                                          # blink
    eyes(arm, 44)
    eyes(arm, 96)
    finish(act, True)


def clip_happy(arm):
    act = new_action(arm, "Happy", 48)
    rest_all(arm, 0)
    for i in range(5):
        f = i * 12
        key(arm, "Root", f, loc=(0, 0, 0))
        key(arm, "Body", f, scale=sq(0.93))                     # land
        if f + 6 <= 48:
            key(arm, "Root", f + 6, loc=(0, 0.09, 0))           # hop (bone Y = up)
            key(arm, "Body", f + 6, scale=sq(1.06))
            key(arm, "Arm_L", f + 6, rot=(-55, 0, 15))
            key(arm, "Arm_R", f + 6, rot=(-55, 0, -15))
            key(arm, "Leg_L", f + 6, rot=(-8, 0, 0))
            key(arm, "Leg_R", f + 6, rot=(-8, 0, 0))
        key(arm, "Arm_L", f, rot=(-15, 0, 5))
        key(arm, "Arm_R", f, rot=(-15, 0, -5))
        key(arm, "Leg_L", f, rot=(0, 0, 0))
        key(arm, "Leg_R", f, rot=(0, 0, 0))
    for f, a in ((0, -8), (12, 8), (24, -8), (36, 8), (48, -8)):
        key(arm, "Head", f, rot=(-4, 0, a))
    for i in range(7):
        key(arm, "Tail", i * 8, rot=(0, 28 if i % 2 else -28, 0))
    for e, s in (("Ear_L", 1), ("Ear_R", -1)):
        for f in (0, 48):
            key(arm, e, f, rot=(8, 0, s * 6))
    for f in (0, 48):
        eyes(arm, f, 0.4)                                        # happy squint
    finish(act, True)


def clip_beckon(arm):
    act = new_action(arm, "Beckon", 48)
    rest_all(arm, 0)
    for i in range(4):                                           # the lucky-cat wave, left paw
        f = i * 16
        key(arm, "Arm_L", f, rot=(-65, 30, 48))
        if f + 8 <= 48:
            key(arm, "Arm_L", f + 8, rot=(-30, 30, 40))
    for f in (0, 48):
        key(arm, "Arm_R", f, rot=(-8, 0, -4))
        key(arm, "Body", f, rot=(0, 0, -4), scale=sq(1.0))
        key(arm, "Head", f, rot=(0, 4, -6))
        for e in ("Ear_L", "Ear_R"):
            key(arm, e, f, rot=(0, 0, 0))
    key(arm, "Body", 24, rot=(0, 0, -5), scale=sq(1.02))
    key(arm, "Head", 24, rot=(3, 6, -8))
    for f, a in ((0, -14), (24, 14), (48, -14)):
        key(arm, "Tail", f, rot=(0, a, 0))
    key(arm, "Ear_L", 30, rot=(6, 0, -10))
    key(arm, "Ear_L", 36, rot=(0, 0, 0))
    eyes(arm, 0)
    eyes(arm, 18)
    eyes(arm, 21, 0.08)
    eyes(arm, 24)
    eyes(arm, 48)
    finish(act, True)


def clip_surprised(arm):
    act = new_action(arm, "Surprised", 40, loop=False)
    rest_all(arm, 0)
    eyes(arm, 0)
    key(arm, "Body", 4, scale=sq(0.88))                          # anticipation crouch
    key(arm, "Root", 4, loc=(0, 0, 0))
    key(arm, "Head", 4, rot=(6, 0, 0))
    key(arm, "Root", 10, loc=(0, 0.2, 0))                        # jump
    key(arm, "Body", 10, scale=sq(1.1))
    key(arm, "Head", 10, rot=(-12, 0, 0))
    for side, s in (("L", 1), ("R", -1)):
        key(arm, "Arm_" + side, 4, rot=(-5, 0, 0))
        key(arm, "Arm_" + side, 10, rot=(-40, 0, s * 70))
        key(arm, "Arm_" + side, 24, rot=(-30, 0, s * 45))
        key(arm, "Arm_" + side, 40, rot=(0, 0, 0))
        key(arm, "Leg_" + side, 10, rot=(-12, 0, -s * 8))
        key(arm, "Leg_" + side, 16, rot=(0, 0, 0))
        key(arm, "Ear_" + side, 4, rot=(0, 0, 0))
        key(arm, "Ear_" + side, 10, rot=(10, 0, s * 12))         # ears shoot up
        key(arm, "Ear_" + side, 30, rot=(6, 0, s * 8))
        key(arm, "Ear_" + side, 40, rot=(0, 0, 0))
    key(arm, "Root", 16, loc=(0, 0, 0))
    key(arm, "Body", 16, scale=sq(0.9))                          # land
    key(arm, "Body", 21, scale=sq(1.02))
    key(arm, "Body", 26, scale=sq(1.0))
    key(arm, "Head", 26, rot=(-6, 0, 0))
    key(arm, "Head", 40, rot=(0, 0, 0))
    key(arm, "Root", 40, loc=(0, 0, 0))
    key(arm, "Body", 40, scale=sq(1.0))
    eyes(arm, 4, 0.7)
    eyes(arm, 9, 1.0, 1.3)                                       # eyes wide
    eyes(arm, 30, 1.0, 1.25)
    eyes(arm, 40)
    key(arm, "Coin", 4, loc=(0, 0, 0), rot=(0, 0, 0))            # the coin pops out of the mouth...
    key(arm, "Coin", 11, loc=(0, 0.12, 0.08), rot=(-60, 0, 25))
    key(arm, "Coin", 18, loc=(0, 0.02, 0.04), rot=(-20, 0, 8))
    key(arm, "Coin", 24, loc=(0, 0, 0), rot=(0, 0, 0))           # ...and gets caught again
    for f, a in ((0, 0), (10, 0), (12, 30), (16, -25), (20, 20), (26, -10), (40, 0)):
        key(arm, "Tail", f, rot=(0, a, 0))
    finish(act, False)


def clip_sleepy(arm):
    act = new_action(arm, "Sleepy", 96)
    rest_all(arm, 0)
    for f, a in ((0, 6), (34, 24), (40, 26), (44, 2), (50, 8), (96, 6)):   # nod off... snap awake
        key(arm, "Head", f, rot=(a, 0, a * 0.15))
    for f, y in ((0, 1.0), (32, 1.045), (64, 1.0), (96, 1.0)):
        key(arm, "Body", f, rot=(4, 0, 0), scale=sq(y))
    for f, o in ((0, 0.1), (40, 0.06), (44, 0.55), (48, 0.5), (56, 0.15), (96, 0.1)):
        eyes(arm, f, o)
    for e, s in (("Ear_L", 1), ("Ear_R", -1)):
        for f, a in ((0, 22), (40, 30), (44, 8), (56, 20), (96, 22)):
            key(arm, e, f, rot=(-8, 0, -s * a))
    for side in ("L", "R"):
        for f in (0, 96):
            key(arm, "Arm_" + side, f, rot=(8, 0, 0))
    for f, a in ((0, -5), (48, 5), (96, -5)):
        key(arm, "Tail", f, rot=(10, a, 0))
    finish(act, True)


def clip_angry(arm):
    act = new_action(arm, "Angry", 48)
    rest_all(arm, 0)
    for e, s in (("Ear_L", 1), ("Ear_R", -1)):
        for f in (0, 48):
            key(arm, e, f, rot=(-40, 0, -s * 22))                # ears flat back
    for f, a in ((0, 0), (4, 12), (8, -12), (12, 10), (16, -10), (20, 0), (48, 0)):
        key(arm, "Head", f, rot=(8, a, 0))                       # head shake, chin down
    for f, a in ((0, -32), (6, 32), (12, -32), (18, 32), (24, -32), (30, 32), (36, -32), (42, 32), (48, -32)):
        key(arm, "Tail", f, rot=(0, a, 0))                       # lashing tail
    for leg, f0 in (("Leg_L", 24), ("Leg_R", 36)):               # stomp, stomp
        key(arm, leg, 0, loc=(0, 0, 0), rot=(0, 0, 0))
        key(arm, leg, f0, loc=(0, 0, 0))
        key(arm, leg, f0 + 4, loc=(0, 0.07, 0), rot=(-10, 0, 0))
        key(arm, leg, f0 + 7, loc=(0, 0, 0), rot=(0, 0, 0))
        key(arm, leg, 48, loc=(0, 0, 0), rot=(0, 0, 0))
    for f, y in ((0, 1.0), (31, 0.95), (34, 1.0), (43, 0.95), (46, 1.0), (48, 1.0)):
        key(arm, "Body", f, scale=sq(y))
    for side, s in (("L", 1), ("R", -1)):
        for f, a in ((0, 20), (31, 30), (36, 20), (43, 30), (48, 20)):
            key(arm, "Arm_" + side, f, rot=(a, 0, -s * 10))      # stiff arms pushed down
    for f in (0, 48):
        eyes(arm, f, 0.55)                                       # glaring
    finish(act, True)


def clip_sad(arm):
    act = new_action(arm, "Sad", 96)
    rest_all(arm, 0)
    for f, a in ((0, -4), (48, 4), (96, -4)):
        key(arm, "Head", f, rot=(20, 0, a))                      # head hung low, slow sway
        key(arm, "Body", f, rot=(6, 0, a * 0.5), scale=sq(0.97))
    for e, s in (("Ear_L", 1), ("Ear_R", -1)):
        for f in (0, 96):
            key(arm, e, f, rot=(-15, 0, -s * 42))                # droopy ears
        key(arm, e, 48, rot=(-12, 0, -s * 38))
    for side in ("L", "R"):
        for f in (0, 96):
            key(arm, "Arm_" + side, f, rot=(12, 0, 0))
    for f, a in ((0, -3), (48, 3), (96, -3)):
        key(arm, "Tail", f, rot=(25, a, 0))                      # limp tail
    for f, o in ((0, 0.6), (60, 0.6), (63, 0.1), (66, 0.6), (96, 0.6)):
        eyes(arm, f, o)
    finish(act, True)


# ---------------------------------------------------------------- diving helmet

HELMET_C, HELMET_R, HELMET_CUT = Vector((0, 0, 1.40)), 0.80, 0.98   # clears the ears even mid-nod


def add_helmet(arm):
    """A simple glass bubble over the head on a brass neck ring, parented to the Body bone (the head
    nods inside it). Materials Cat_HelmetGlass / Cat_HelmetBrass are left un-baked: Unity gives the
    glass its own transparent shader and the ring a flat brass toon colour."""
    import bmesh
    scene = bpy.context.scene
    glass = bpy.data.materials.new("Cat_HelmetGlass")
    glass.use_nodes = True
    b = glass.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (0.75, 0.95, 1.0, 1)
    b.inputs["Roughness"].default_value = 0.05
    b.inputs["Alpha"].default_value = 0.15
    if hasattr(glass, "surface_render_method"):
        glass.surface_render_method = "BLENDED"
    brass = bpy.data.materials.new("Cat_HelmetBrass")
    brass.use_nodes = True
    brass.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.69, 0.42, 0.1, 1)
    outline = bpy.data.materials.get("Cat_Outline")

    # bubble: sphere cut flat just above the collar
    me = bpy.data.meshes.new("Cat_Helmet")
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=64, v_segments=32, radius=HELMET_R)
    bmesh.ops.translate(bm, vec=HELMET_C, verts=bm.verts)
    geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
    bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(0, 0, HELMET_CUT), plane_no=(0, 0, 1), clear_inner=True)
    for f in bm.faces:
        f.smooth = True
    bm.to_mesh(me)
    bm.free()
    bubble = bpy.data.objects.new("Cat_Helmet", me)
    scene.collection.objects.link(bubble)
    me.materials.append(glass)

    # brass ring where the glass meets the shoulders, with a toon outline like the rest of the cat
    ring_r = math.sqrt(HELMET_R ** 2 - (HELMET_CUT - HELMET_C.z) ** 2)
    bpy.ops.mesh.primitive_torus_add(major_radius=ring_r, minor_radius=0.045, major_segments=64, minor_segments=12,
                                     location=(0, 0, HELMET_CUT))
    ring = bpy.context.active_object
    ring.name = ring.data.name = "Cat_HelmetRing"
    ring.data.materials.append(brass)
    for p in ring.data.polygons:
        p.use_smooth = True
    if outline:
        ring.data.materials.append(outline)
        s = ring.modifiers.new("Outline", "SOLIDIFY")
        s.thickness, s.offset, s.use_flip_normals, s.use_rim = 0.012, 1.0, True, False
        s.material_offset = 1
    for ob in (bubble, ring):                       # ride on the body, keep exactly where they are
        mw = ob.matrix_world.copy()
        ob.parent = arm
        ob.parent_type = "BONE"
        ob.parent_bone = "Body"
        bpy.context.view_layer.update()
        ob.matrix_world = mw
    print(f"[cat] helmet: bubble r={HELMET_R}, ring r={ring_r:.2f} at z={HELMET_CUT}")


# ---------------------------------------------------------------- preview

def use(arm, act):
    arm.animation_data.action = act
    if hasattr(arm.animation_data, "action_slot") and act.slots:
        arm.animation_data.action_slot = act.slots[0]


def preview(arm):
    scene = bpy.context.scene
    # lay the clips end to end in the NLA for one video
    arm.animation_data.action = None
    track = arm.animation_data.nla_tracks.new()
    start, spans = 1, []
    for name, a, b, loop in CLIPS:
        act = bpy.data.actions[name]
        reps = 2 if loop and b <= 48 else 1
        strip = track.strips.new(name, start, act)
        if hasattr(strip, "action_slot") and act.slots:
            strip.action_slot = act.slots[0]
        strip.repeat = reps
        spans.append((name, start, start + (b - a) * reps))
        start += (b - a) * reps + 6
    scene.frame_start, scene.frame_end = 1, start
    cam = scene.camera
    cam.location = (2.4, -5.4, 1.5)
    cam.rotation_euler = (Vector((0, 0, 1.0)) - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.resolution_x = scene.render.resolution_y = 540
    scene.render.image_settings.media_type = "VIDEO" if hasattr(scene.render.image_settings, "media_type") else scene.render.image_settings.file_format
    try:
        scene.render.image_settings.file_format = "FFMPEG"
    except TypeError:
        pass
    scene.render.ffmpeg.format = "MPEG4"
    scene.render.ffmpeg.codec = "H264"
    scene.render.ffmpeg.constant_rate_factor = "HIGH"
    scene.render.filepath = os.path.join(OUT_DIR, "anim_preview.mp4")
    bpy.ops.render.render(animation=True)
    print("[cat] video", scene.render.filepath, spans)
    # contact sheet frames: 4 poses per clip
    scene.render.image_settings.media_type = "IMAGE" if hasattr(scene.render.image_settings, "media_type") else "PNG"
    scene.render.image_settings.file_format = "PNG"
    scene.render.resolution_x = scene.render.resolution_y = 360
    frames_dir = os.path.join(OUT_DIR, "anim_frames")
    os.makedirs(frames_dir, exist_ok=True)
    arm.animation_data.nla_tracks.remove(track)
    for name, a, b, loop in CLIPS:                 # sample each action directly
        act = bpy.data.actions[name]
        use(arm, act)
        for k in range(4):
            f = int(a + (b - a) * (k + 0.5) / 4)
            if name == "Beckon":
                f = (0, 4, 8, 12)[k]
            scene.frame_set(f)
            scene.render.filepath = os.path.join(frames_dir, f"{name}_{k}.png")
            bpy.ops.render.render(write_still=True)
    track = None
    # strip the NLA again so each action stays a clean, separate clip
    use(arm, bpy.data.actions["Idle"])
    scene.frame_set(0)


def main():
    bpy.ops.wm.open_mainfile(filepath=SRC)
    if "Cat_Armature" in bpy.data.objects:
        raise SystemExit("[cat] source already rigged; use the original model file")
    arm = build_rig()
    add_helmet(arm)
    for fn in (clip_idle, clip_happy, clip_beckon, clip_surprised, clip_sleepy, clip_angry, clip_sad):
        fn(arm)
    arm.animation_data.action = bpy.data.actions["Idle"]
    bpy.context.scene.render.fps = 24
    bpy.ops.wm.save_as_mainfile(filepath=OUT)
    print("[cat] saved", OUT, [c[0] for c in CLIPS])
    if PREVIEW:
        preview(arm)
        bpy.ops.wm.save_as_mainfile(filepath=OUT)


if __name__ == "__main__":
    main()
