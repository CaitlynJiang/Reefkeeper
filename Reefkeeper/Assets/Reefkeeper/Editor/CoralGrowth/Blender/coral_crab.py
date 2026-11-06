"""Red antler coral (3 stages) and the coral crab that sometimes lives under it (Blender 5.2, background).

    blender.exe -b --factory-startup --python coral_crab.py -- [--preview] [--no-export]

The mature coral (stage 3) sits on a round red "rock" that is really the crab's shell: the crab
model carries exactly the same shell and antlers on its Body bone, and its Sleep pose puts them
where the stage-3 coral is, legs and claws folded away inside the shell. Swapping the coral for
the crab in Unity is therefore invisible until it wakes up.

Outputs (Unity project):
    Meshes/CoralGrowth/RedCoral.fbx           RedCoral_S1..S3 (same layout as the other corals)
    Textures/CoralGrowth/RedCoral_S1..3.png
    Meshes/CoralCrab/CoralCrab.fbx            rigid parts on an armature + clips Sleep, WakeUp, Walk
    Textures/CoralCrab/CoralCrab.png          one atlas for every crab part
Work file: <repo>/Blender/CoralGrowth/coral_crab.blend (+ preview_*.png with --preview).
Uses the helpers in coral_generator.py; never touches coral_growth.blend.

Crab space: the crab faces -Y, +Z is up, its Body bone sits at the bottom of the shell.
Bones all point straight up (roll 0), so in bone space: X = world X, Y = up, Z = world -Y.
    rot X-  = a forward-pointing part (claw) lifts      rot Y = turn about vertical
    rot Z+  = a +X part lifts (a -X part lowers)        location Y = up, scale Y = vertical
"""
import argparse
import math
import os
import random
import sys

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import bpy  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

import coral_generator as cg  # noqa: E402

CRAB_MESH_DIR = os.path.join(cg.REEF_ROOT, "Meshes", "CoralCrab")
CRAB_TEX_DIR = os.path.join(cg.REEF_ROOT, "Textures", "CoralCrab")

STAND = 0.07        # shell bottom height when standing
SLEEP_DROP = 0.064  # how far the body sinks when asleep (shell bottom ends ~6 mm above the sand)
SHELL_R, SHELL_SQ, SHELL_H = 0.15, 0.85, 0.10

# ---------------------------------------------------------------- palette

SHELL = [(0.0, cg.hexc("B32A40")), (0.35, cg.hexc("E2475A")), (0.75, cg.hexc("F7707A")), (1.0, cg.hexc("FF9AA0"))]
SHELL_SPOT = cg.hexc("9E1F37")
CORAL = [(0.0, cg.hexc("C9334A")), (0.4, cg.hexc("EE4D60")), (0.8, cg.hexc("FF7884")), (1.0, cg.hexc("FFB0B6"))]
CLAW = [(0.0, cg.hexc("8F1E36")), (0.6, cg.hexc("B92C47")), (1.0, cg.hexc("D44A5F"))]
CLAW_SPOT = cg.hexc("5E1024")
LEG = [(0.0, cg.hexc("A3253F")), (0.7, cg.hexc("CC3F55")), (1.0, cg.hexc("F07C88"))]
EYE, EYE_SHINE, MOUTH = cg.hexc("2A0A14"), cg.hexc("FFF4F4"), cg.hexc("4A1022")


def paint(col, spot=None, dots=0.0):
    return dict(col=col, spot_col=spot if spot is not None else col, dots=dots, rings=0.0)


# ---------------------------------------------------------------- geometry helpers

def ellipsoid(b, c, rad, colour, seg=14, rings=9, rot=None, spot=None, dots=0.0):
    rot = rot or Matrix.Identity(3)
    rows = []
    for i in range(rings + 1):
        v = math.pi * i / rings
        row = []
        for j in range(seg):
            if i in (0, rings) and j > 0:
                row.append(row[0])
                continue
            u = 2 * math.pi * j / seg
            p = c + rot @ Vector((math.sin(v) * math.cos(u) * rad[0], math.sin(v) * math.sin(u) * rad[1], math.cos(v) * rad[2]))
            h = 1.0 - i / rings
            row.append(b.vert(p, **paint(colour(h) if callable(colour) else colour, spot, dots)))
        rows.append(row)
    for i in range(rings):
        for j in range(seg):
            b.face((rows[i][j], rows[i + 1][j], rows[i + 1][(j + 1) % seg], rows[i][(j + 1) % seg]))


def add_skin(b, nodes, colour_of, subdiv=2, spot=None, dots=0.0):
    """Tubes along a node tree. nodes: [(pos, radius, parent)], parent -1 = root."""
    me = bpy.data.meshes.new("skel")
    me.from_pydata([tuple(n[0]) for n in nodes], [(n[2], i) for i, n in enumerate(nodes) if n[2] >= 0], [])
    ob = bpy.data.objects.new("skel", me)
    bpy.context.scene.collection.objects.link(ob)
    ob.modifiers.new("Skin", "SKIN")
    for i, (p, r, parent) in enumerate(nodes):
        sv = me.skin_vertices[0].data[i]
        sv.radius = (r, r)
        sv.use_root = parent < 0
    sub = ob.modifiers.new("Sub", "SUBSURF")
    sub.levels = sub.render_levels = subdiv
    em = bpy.data.meshes.new_from_object(ob.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    base = len(b.v)
    for v in em.vertices:
        p = v.co.copy()
        b.vert(p, **paint(colour_of(p), spot, dots))
    for poly in em.polygons:
        b.face([base + i for i in poly.vertices], True)
    bpy.data.objects.remove(ob)
    bpy.data.meshes.remove(me)
    bpy.data.meshes.remove(em)


def shell(b, m, scale=1.0):
    """The round carapace (body space: bottom at z=0). m maps body space to the output."""
    R, H = SHELL_R * scale, SHELL_H * scale
    prof = [(0.0, 0.0), (0.80, 0.0), (0.96, 0.10), (1.0, 0.30), (0.95, 0.52), (0.80, 0.74), (0.55, 0.90), (0.28, 0.98), (0.0, 1.0)]
    profile = [(r * R, z * H, cg.BODY, z, 0) for r, z in prof]
    place = cg.make_place(Matrix.Identity(4), H, seed=7, wob=0.045, freq=4.0)

    def put(p, th, pt):
        return m @ place(p, th, pt)

    def shade(part, h, p):
        return paint(cg.ramp(SHELL, h), SHELL_SPOT, 0.55 if h > 0.25 else 0.0)

    cg.lathe(b, profile, 20, put, shade, squash=(1.0, SHELL_SQ))


# ---------------------------------------------------------------- antlers

# (base on the shell, outward direction, beam length, radius, seed)  -- body space
ANTLERS = [
    (Vector((-0.05, 0.0, 0.088)), Vector((-1.0, 0.12, 0.75)), 0.2, 0.018, 11),
    (Vector((0.05, 0.0, 0.088)), Vector((1.0, 0.12, 0.75)), 0.2, 0.018, 23),
    (Vector((-0.01, 0.05, 0.088)), Vector((-0.15, 1.0, 1.1)), 0.13, 0.014, 37),
    (Vector((0.02, -0.03, 0.094)), Vector((0.35, -0.6, 1.4)), 0.09, 0.012, 41),
]


def antler_tree(base, direction, length, radius, seed):
    """Candelabra antler: [(pos, radius, gen, parent)].
    Gen 0 is the beam, arcing outward then up; gen 1 tines rise from it and the beam forks at its end;
    gen 2 are short side prongs near the tine tips."""
    rng = random.Random(seed)
    nodes = [(base.copy(), radius, 0, -1)]
    up = Vector((0, 0, 1))

    def grow(start, parent, d, length, radius, gen, tip_r):
        steps = max(3, int(length / 0.018))
        d = d.normalized()
        out = Vector((d.x, d.y, 0.0))
        out = out.normalized() if out.length > 1e-3 else Vector((1, 0, 0))
        side = Vector((-out.y, out.x, 0.0))
        tines = {0: (0.3, 0.52, 0.74), 1: (0.62,), 2: ()}[gen]
        tine_at = {max(1, round(f * steps)): k for k, f in enumerate(tines)}
        pos, idx = start.copy(), parent
        for s in range(1, steps + 1):
            curl = (0.1, 0.16, 0.2)[gen]
            d = (d + up * curl + Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), 0)) * 0.04).normalized()
            pos = pos + d * (length / steps)
            r = radius + (tip_r - radius) * s / steps
            nodes.append((pos.copy(), r, gen, idx))
            idx = len(nodes) - 1
            if s in tine_at and gen < 2:
                k = tine_at[s]
                if gen == 0:   # tines stand up off the beam, alternating a little to each side
                    td = (up * 1.0 + out * 0.15 + side * (0.35 if k % 2 else -0.35)).normalized()
                    grow(pos, idx, td, length * rng.uniform(0.45, 0.6), r * 0.85, 1, 0.0065)
                else:          # a short prong off a tine
                    td = (up * 0.6 + side * rng.choice((-1, 1)) * 0.8).normalized()
                    grow(pos, idx, td, length * rng.uniform(0.35, 0.45), r * 0.85, 2, 0.006)
        if gen == 0:   # the beam ends in a fork
            for k in (-1, 1):
                td = (d + side * k * 0.55 + up * 0.35).normalized()
                grow(pos, idx, td, length * rng.uniform(0.3, 0.38), r * 0.95, 1, 0.0065)

    grow(base, 0, direction, length, radius, 0, radius * 0.6)
    return nodes


TREES = [antler_tree(*a) for a in ANTLERS]


def antlers(b, m, gen_max, scale, radius_scale=1.0):
    """Antlers up to gen_max, scaled about their own bases by `scale`, then mapped by m.
    radius_scale is the final thickness factor (m's own scale does not reach the radii)."""
    for tree, (base, *_rest) in zip(TREES, ANTLERS):
        keep = [i for i, n in enumerate(tree) if n[2] <= gen_max]
        remap = {old: new for new, old in enumerate(keep)}
        nodes = []
        for i in keep:
            p, r, gen, parent = tree[i]
            q = m @ (base + (p - base) * scale)
            nodes.append((q, r * radius_scale, remap[parent] if parent >= 0 else -1))
        zs = [n[0].z for n in nodes]
        z0, z1 = min(zs), max(zs)
        add_skin(b, nodes, lambda p, z0=z0, z1=z1: cg.ramp(CORAL, (p.z - z0) / max(z1 - z0, 1e-3)))


# ---------------------------------------------------------------- the coral stages

STAGES = {
    # stage: (antler generations, antler scale, shell scale, shell sink)
    1: (0, 0.55, 0.55, 0.035),
    2: (1, 0.78, 0.78, 0.022),
    3: (2, 1.0, 1.0, -(STAND - SLEEP_DROP)),   # the sleeping crab's exact pose
}


def build_coral_stage(b, stage):
    gen_max, a_scale, s_scale, sink = STAGES[stage]
    m = Matrix.Translation((0, 0, -sink)) @ Matrix.Scale(s_scale, 4)
    shell(b, m)
    # antler bases ride on the scaled shell; the antlers themselves end up a_scale in size
    thick = 1.0 if stage > 1 else 1.2
    antlers(b, m, gen_max, a_scale / s_scale, a_scale * thick)


# ---------------------------------------------------------------- the crab parts (body space)

LEGS_Y = (-0.05, -0.012, 0.026, 0.062)
LEG_FAN = (-0.06, -0.02, 0.02, 0.055)


def crab_parts():
    """{bone: (parent bone, pivot in body space, Builder)}"""
    parts = {}

    b = cg.Builder()                                   # shell + coral: the Body bone
    shell(b, Matrix.Identity(4))
    antlers(b, Matrix.Identity(4), 2, 1.0)
    parts["Body"] = ("Root", Vector((0, 0, 0)), b)

    for side, sx in (("L", -1), ("R", 1)):
        eye_c = Vector((sx * 0.042, -0.118, 0.056))
        b = cg.Builder()
        ellipsoid(b, eye_c, (0.017, 0.009, 0.016), EYE, seg=12, rings=8)
        ellipsoid(b, eye_c + Vector((sx * 0.004, -0.008, 0.006)), (0.0045, 0.003, 0.0045), EYE_SHINE, seg=8, rings=5)
        parts[f"Eye_{side}"] = ("Body", eye_c, b)

        shoulder = Vector((sx * 0.075, -0.095, 0.022))
        palm = Vector((sx * 0.09, -0.19, -0.03))
        b = cg.Builder()
        add_skin(b, [(shoulder, 0.017, -1), ((shoulder + palm) / 2 + Vector((sx * 0.012, 0, 0.012)), 0.016, 0), (palm, 0.02, 1)],
                 lambda p: cg.ramp(CLAW, 0.4), subdiv=1)
        ellipsoid(b, palm, (0.046, 0.052, 0.038), lambda h: cg.ramp(CLAW, h), seg=16, rings=10, spot=CLAW_SPOT, dots=1.0)
        parts[f"Claw_{side}"] = ("Body", shoulder, b)

        hinge = palm + Vector((0, 0.012, 0.022))
        b = cg.Builder()
        ellipsoid(b, palm + Vector((0, -0.022, 0.03)), (0.034, 0.04, 0.017), lambda h: cg.ramp(CLAW, 0.5 + 0.5 * h),
                  seg=14, rings=8, rot=Matrix.Rotation(math.radians(-12), 3, "X"), spot=CLAW_SPOT, dots=1.0)
        parts[f"Pincer_{side}"] = (f"Claw_{side}", hinge, b)

        for k, (y, fan) in enumerate(zip(LEGS_Y, LEG_FAN)):
            hip = Vector((sx * 0.11, y, 0.022))
            knee = Vector((sx * 0.172, y + fan * 0.8, 0.058))
            tip = Vector((sx * 0.205, y + fan * 1.3, -STAND + 0.002))
            b = cg.Builder()
            add_skin(b, [(hip, 0.019, -1), (knee, 0.017, 0), (tip, 0.005, 1)],
                     lambda p, hip=hip: cg.ramp(LEG, (p - hip).length / 0.14), subdiv=2)
            parts[f"Leg_{side}{k}"] = ("Body", hip, b)

    b = cg.Builder()                                   # a small frown under the eyes
    ellipsoid(b, Vector((0, -0.122, 0.03)), (0.013, 0.004, 0.0045), MOUTH, seg=10, rings=6)
    parts["Mouth"] = ("Body", Vector((0, -0.12, 0.03)), b)
    return parts


# ---------------------------------------------------------------- materials & baking

def bake_atlas(objs, mat, path, size):
    """One shared emission material, unwrap all parts together, bake them into one image."""
    img = bpy.data.images.new(os.path.basename(path), size, size, alpha=False)
    node = mat.node_tree.nodes.new("ShaderNodeTexImage")
    node.image = img
    mat.node_tree.nodes.active = node
    for o in objs:
        o.data.materials.clear()
        o.data.materials.append(mat)
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.004)
    bpy.ops.uv.pack_islands(rotate=True, margin=0.004)
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.bake(type="EMIT", margin=6, use_clear=True)
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    return img


# ---------------------------------------------------------------- rig + animation

CLIPS = []


def build_rig(parts, collection):
    arm_data = bpy.data.armatures.new("CoralCrab_Armature")
    arm = bpy.data.objects.new("CoralCrab", arm_data)
    collection.objects.link(arm)
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = arm
    arm.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    eb = {}
    heads = {"Root": Vector((0, 0, 0))}
    order = ["Root"] + [n for n in parts if parts[n][0] == "Root"] + [n for n in parts if parts[n][0] not in ("Root",)]
    seen = set()
    for name in order:
        if name in seen:
            continue
        seen.add(name)
        parent = None if name == "Root" else parts[name][0]
        head = heads["Root"] if name == "Root" else parts[name][1] + Vector((0, 0, STAND))
        heads[name] = head
        bone = arm_data.edit_bones.new(name)
        bone.head = head
        bone.tail = head + Vector((0, 0, 0.2 if name in ("Root", "Body") else 0.04))
        bone.roll = 0.0
        if parent:
            bone.parent = eb[parent]
            bone.use_connect = False
        eb[name] = bone
    bpy.ops.object.mode_set(mode="OBJECT")
    for pb in arm.pose.bones:
        pb.rotation_mode = "XYZ"
    objs = []
    for name, (_, _, b) in parts.items():
        ob = b.to_object(f"Crab_{name}", collection)
        ob.location = (0, 0, STAND)
        bpy.context.view_layer.update()
        mw = ob.matrix_world.copy()
        ob.parent = arm
        ob.parent_type = "BONE"
        ob.parent_bone = name
        bpy.context.view_layer.update()
        ob.matrix_world = mw
        objs.append(ob)
    return arm, objs


def key(arm, bone, f, rot=None, loc=None, scale=None):
    pb = arm.pose.bones[bone]
    if rot is not None:
        pb.rotation_euler = [math.radians(a) for a in rot]
        pb.keyframe_insert("rotation_euler", frame=f)
    if loc is not None:
        pb.location = loc
        pb.keyframe_insert("location", frame=f)
    if scale is not None:
        pb.scale = scale if len(scale) == 3 else (scale[0],) * 3
        pb.keyframe_insert("scale", frame=f)


def sq(y):
    s = 1 / math.sqrt(y)
    return (s, y, s)


def new_action(arm, name, end, loop):
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


BONES = ["Root", "Body", "Eye_L", "Eye_R", "Mouth"] + [f"{p}_{s}" for p in ("Claw", "Pincer") for s in "LR"] \
    + [f"Leg_{s}{k}" for s in "LR" for k in range(4)]
LEGS = [f"Leg_{s}{k}" for s in "LR" for k in range(4)]


def side_of(bone):
    return -1 if "_L" in bone else 1


# Poses: every helper keys every channel it touches, so clips start and end exactly.

def pose_rest(arm, f):
    for bn in BONES:
        key(arm, bn, f, rot=(0, 0, 0), loc=(0, 0, 0), scale=(1, 1, 1))


def pose_sleep(arm, f, breath=1.0):
    key(arm, "Body", f, rot=(0, 0, 0), loc=(0, -SLEEP_DROP, 0), scale=sq(breath))
    for e in ("Eye_L", "Eye_R"):
        key(arm, e, f, scale=(1.0, 0.1, 1.0))
    key(arm, "Mouth", f, scale=(0.0, 0.0, 0.0))
    for leg in LEGS:                                  # folded up inside the shell
        key(arm, leg, f, rot=(0, 0, side_of(leg) * 40), scale=(0.25, 0.25, 0.25))
    for s in "LR":
        key(arm, f"Claw_{s}", f, rot=(0, 0, 0), loc=(0, 0.012, -0.05), scale=(0.3, 0.3, 0.3))
        key(arm, f"Pincer_{s}", f, rot=(0, 0, 0))


def clip_sleep(arm):
    act = new_action(arm, "Sleep", 96, True)
    pose_rest(arm, 0)
    pose_rest(arm, 96)
    for f, br in ((0, 1.0), (40, 1.03), (96, 1.0)):
        pose_sleep(arm, f, br)
    finish(act, True)


def clip_wake(arm):
    act = new_action(arm, "WakeUp", 96, False)
    pose_rest(arm, 0)
    pose_sleep(arm, 0)
    pose_sleep(arm, 6)
    # a shiver, then one eye peeks
    for f, a in ((8, 3), (10, -3), (12, 2.5), (14, -2), (16, 0)):
        key(arm, "Body", f, rot=(0, a, 0))
    key(arm, "Eye_R", 18, scale=(1, 0.1, 1))
    key(arm, "Eye_R", 22, scale=(1, 0.55, 1))
    key(arm, "Eye_R", 27, scale=(1, 0.55, 1))
    key(arm, "Eye_R", 30, scale=(1, 0.1, 1))
    key(arm, "Eye_L", 30, scale=(1, 0.1, 1))
    for e in ("Eye_L", "Eye_R"):                      # both open, blink, wide awake
        key(arm, e, 35, scale=(1, 1.0, 1))
        key(arm, e, 39, scale=(1, 0.12, 1))
        key(arm, e, 42, scale=(1.08, 1.1, 1.08))
        key(arm, e, 52, scale=(1, 1.0, 1))
    key(arm, "Mouth", 34, scale=(0, 0, 0))
    key(arm, "Mouth", 40, scale=(1, 1, 1))
    # rise: anticipation squash, push up with a stretch, settle
    key(arm, "Body", 40, loc=(0, -SLEEP_DROP, 0), scale=sq(1.0))
    key(arm, "Body", 46, loc=(0, -SLEEP_DROP - 0.004, 0), scale=sq(0.9))
    key(arm, "Body", 54, loc=(0, 0.014, 0), scale=sq(1.07))
    key(arm, "Body", 60, loc=(0, -0.004, 0), scale=sq(0.96))
    key(arm, "Body", 66, loc=(0, 0, 0), scale=sq(1.0))
    for i, leg in enumerate(LEGS):                    # legs unfold, front to back
        f0 = 44 + (i % 4)
        key(arm, leg, f0, rot=(0, 0, side_of(leg) * 40), scale=(0.25, 0.25, 0.25))
        key(arm, leg, f0 + 9, rot=(0, 0, side_of(leg) * 14), scale=(1.05, 1.05, 1.05))
        key(arm, leg, f0 + 14, rot=(0, 0, 0), scale=(1, 1, 1))
    for s in "LR":                                    # claws come out, then a big stretch
        c, p = f"Claw_{s}", f"Pincer_{s}"
        key(arm, c, 48, rot=(0, 0, 0), loc=(0, 0.012, -0.05), scale=(0.3, 0.3, 0.3))
        key(arm, c, 58, rot=(-8, 0, 0), loc=(0, 0, 0), scale=(1, 1, 1))
        key(arm, c, 66, rot=(-62, 0, side_of(c) * -18), loc=(0, 0.01, 0))
        key(arm, c, 76, rot=(-66, 0, side_of(c) * -22), loc=(0, 0.012, 0))
        key(arm, c, 86, rot=(0, 0, 0), loc=(0, 0, 0))
        key(arm, p, 58, rot=(0, 0, 0))
        key(arm, p, 66, rot=(-35, 0, 0))
        key(arm, p, 78, rot=(-35, 0, 0))
        key(arm, p, 81, rot=(4, 0, 0))                # snap shut
        key(arm, p, 84, rot=(0, 0, 0))
    key(arm, "Body", 66, rot=(0, 0, 0), scale=sq(1.0))
    key(arm, "Body", 72, rot=(-4, 0, 0), scale=sq(1.05))   # stretch, eyes squeezed
    key(arm, "Body", 80, rot=(0, 0, 0), scale=sq(1.0))
    for e in ("Eye_L", "Eye_R"):
        key(arm, e, 68, scale=(1, 1.0, 1))
        key(arm, e, 72, scale=(1, 0.25, 1))
        key(arm, e, 79, scale=(1, 0.25, 1))
        key(arm, e, 83, scale=(1, 1.0, 1))
    pose_rest(arm, 96)
    finish(act, False)


def clip_walk(arm):
    """Sideways scuttle toward +X, in place (Unity moves the crab). 24 frames."""
    act = new_action(arm, "Walk", 24, True)
    pose_rest(arm, 0)
    pose_rest(arm, 24)
    group_a = {"Leg_L0", "Leg_L2", "Leg_R1", "Leg_R3"}
    for leg in LEGS:
        s = side_of(leg)
        lead = 0 if leg in group_a else 12
        for f in range(0, 25, 3):
            ph = ((f - lead) % 24) / 24.0
            lift = max(0.0, math.sin(ph * 2 * math.pi)) * 26     # up during the first half of its step
            sweep = math.cos(ph * 2 * math.pi) * 9               # a little fore/aft swing so it reads as walking
            key(arm, leg, f, rot=(0, sweep, s * lift))
    for f in range(0, 25, 3):
        ph = f / 24.0 * 2 * math.pi
        key(arm, "Body", f, rot=(0, math.sin(ph) * 2.0, math.sin(ph * 2) * 2.5),
            loc=(0, 0.005 * abs(math.sin(ph * 2)), 0))
        for c in ("Claw_L", "Claw_R"):
            key(arm, c, f, rot=(-14 + 4 * math.sin(ph * 2 + 0.6), 0, 0))
    finish(act, True)


def use(arm, act):
    arm.animation_data.action = act
    if hasattr(arm.animation_data, "action_slot") and getattr(act, "slots", None):
        arm.animation_data.action_slot = act.slots[0]


# ---------------------------------------------------------------- preview renders

def setup_render(scene):
    scene.render.engine = "BLENDER_EEVEE"
    scene.view_settings.view_transform = "Standard"
    world = bpy.data.worlds.new("PreviewWorld")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.16, 0.3, 0.36, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 1.6
    scene.world = world
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
    sun.data.energy = 3.5
    sun.rotation_euler = (math.radians(40), math.radians(10), math.radians(25))
    scene.collection.objects.link(sun)
    ground = bpy.data.meshes.new("Ground")
    ground.from_pydata([(-3, -3, 0), (3, -3, 0), (3, 3, 0), (-3, 3, 0)], [], [(0, 1, 2, 3)])
    g = bpy.data.objects.new("Ground", ground)
    gm = bpy.data.materials.new("Sand")
    gm.use_nodes = True
    gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.85, 0.78, 0.6, 1)
    ground.materials.append(gm)
    scene.collection.objects.link(g)
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    cam.data.lens = 60
    scene.collection.objects.link(cam)
    scene.camera = cam
    return cam


def aim(cam, target, offset):
    cam.location = Vector(target) + Vector(offset)
    cam.rotation_euler = (Vector(target) - cam.location).to_track_quat("-Z", "Y").to_euler()


def render(scene, path, w=900, h=600):
    scene.render.resolution_x, scene.render.resolution_y = w, h
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


# ---------------------------------------------------------------- main

def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument("--work", default=os.path.join(cg.REPO_ROOT, "Blender", "CoralGrowth"))
    ap.add_argument("--no-export", action="store_true")
    ap.add_argument("--preview", action="store_true")
    args = ap.parse_args(argv)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 1
    scene.cycles.device = "CPU"
    scene.render.fps = 24
    for d in (cg.MESH_DIR, cg.TEX_DIR, CRAB_MESH_DIR, CRAB_TEX_DIR, args.work):
        os.makedirs(d, exist_ok=True)

    # ---- red coral, three stages
    col = bpy.data.collections.new("RedCoral")
    scene.collection.children.link(col)
    stages = []
    for s in (1, 2, 3):
        b = cg.Builder()
        build_coral_stage(b, s)
        ob = b.to_object(f"RedCoral_S{s}", col)
        mat = cg.paint_material(ob.name, 0.05, 24.0)
        ob.data.materials.append(mat)
        cg.unwrap(ob)
        img = cg.bake(ob, mat, os.path.join(cg.TEX_DIR if not args.no_export else args.work, f"{ob.name}.png"))
        ob.data.materials.clear()
        ob.data.materials.append(cg.preview_material(ob.name, img))
        stages.append(ob)
        print(f"[crab] RedCoral stage {s}: {len(ob.data.vertices)} verts, {len(ob.data.polygons)} faces")
    if not args.no_export:
        bpy.ops.object.select_all(action="DESELECT")
        for ob in stages:
            ob.select_set(True)
        bpy.context.view_layer.objects.active = stages[0]
        bpy.ops.export_scene.fbx(
            filepath=os.path.join(cg.MESH_DIR, "RedCoral.fbx"), use_selection=True, object_types={"MESH"},
            apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True,
            axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE", add_leaf_bones=False, bake_anim=False,
            path_mode="STRIP", colors_type="NONE")

    # ---- the crab
    ccol = bpy.data.collections.new("CoralCrab")
    scene.collection.children.link(ccol)
    arm, parts = build_rig(crab_parts(), ccol)
    mat = cg.paint_material("CoralCrab", 0.05, 24.0)
    tex = os.path.join(CRAB_TEX_DIR if not args.no_export else args.work, "CoralCrab.png")
    img = bake_atlas(parts, mat, tex, 1024)
    final = cg.preview_material("CoralCrab", img)
    for o in parts:
        o.data.materials.clear()
        o.data.materials.append(final)
    print(f"[crab] crab: {len(parts)} parts, {sum(len(o.data.polygons) for o in parts)} faces")

    for fn in (clip_sleep, clip_wake, clip_walk):
        fn(arm)
    use(arm, bpy.data.actions["Sleep"])
    scene.frame_set(0)
    if not args.no_export:
        bpy.ops.object.select_all(action="DESELECT")
        arm.select_set(True)
        for o in parts:
            o.select_set(True)
        bpy.context.view_layer.objects.active = arm
        bpy.ops.export_scene.fbx(
            filepath=os.path.join(CRAB_MESH_DIR, "CoralCrab.fbx"), use_selection=True,
            object_types={"MESH", "ARMATURE", "EMPTY"}, apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
            bake_space_transform=False, axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE",
            add_leaf_bones=False, use_armature_deform_only=False, bake_anim=True, bake_anim_use_all_actions=True,
            bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
            path_mode="STRIP", colors_type="NONE")

    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(args.work, "coral_crab.blend"))

    if args.preview:
        cam = setup_render(scene)
        for i, ob in enumerate(stages):                # stages left to right, the crab standing at the end
            ob.location = (i * 0.55 - 0.55, 0, 0)
        arm.location = (1.1, 0, 0)
        use(arm, bpy.data.actions["WakeUp"])
        scene.frame_set(96)
        aim(cam, (0.28, 0, 0.14), (0, -3.6, 1.0))
        render(scene, os.path.join(args.work, "preview_redcoral_stages.png"), 1400, 600)
        for ob in stages:
            ob.hide_render = True
        arm.location = (0, 0, 0)
        shots = [("WakeUp", f) for f in (0, 20, 36, 50, 58, 74, 96)] + [("Walk", f) for f in (0, 6, 12, 18)]
        for name, f in shots:
            use(arm, bpy.data.actions[name])
            scene.frame_set(f)
            aim(cam, (0, 0, 0.12), (0.45, -1.15, 0.45))
            render(scene, os.path.join(args.work, f"preview_crab_{name}_{f:02d}.png"), 520, 420)
        use(arm, bpy.data.actions["Sleep"])
        scene.frame_set(0)
    print("[crab] done")


if __name__ == "__main__":
    main()
