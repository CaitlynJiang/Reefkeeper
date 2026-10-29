"""Concept renders for the redesigned coral rack: base -> table coral -> corals on the table.

blender.exe -b --factory-startup --python rack_concepts.py -- OUT_DIR
Reuses the shapes from the Unity project's coral_generator.py. Concept only (no export).
"""
import math
import os
import random
import sys

import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, r"D:\GitHub\Reefkeeper\Reefkeeper\Assets\Reefkeeper\Editor\CoralGrowth\Blender")
import coral_generator as cg  # noqa: E402

OUT = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else os.path.dirname(os.path.abspath(__file__))
os.makedirs(OUT, exist_ok=True)

STEEL = (0.33, 0.37, 0.33, 1)       # olive-grey like the current nursery frame
CONCRETE = (0.50, 0.53, 0.52, 1)
SOCKET_GLOW = (1.0, 0.72, 0.3, 1)


# ---------------------------------------------------------------- scene

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    s = bpy.context.scene
    s.render.engine = "BLENDER_EEVEE"
    s.render.resolution_x = s.render.resolution_y = 1100
    s.view_settings.view_transform = "Standard"
    s.view_settings.look = "AgX - Medium High Contrast" if "AgX - Medium High Contrast" in [
        i.identifier for i in s.view_settings.bl_rna.properties["look"].enum_items] else "None"
    w = bpy.data.worlds.new("W")
    w.use_nodes = True
    nt = w.node_tree
    bg = nt.nodes["Background"]
    grad = nt.nodes.new("ShaderNodeTexGradient")
    coord = nt.nodes.new("ShaderNodeTexCoord")
    mapn = nt.nodes.new("ShaderNodeMapping")
    mapn.inputs["Rotation"].default_value = (0, math.radians(-90), 0)
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].color = (0.05, 0.22, 0.26, 1)
    ramp.color_ramp.elements[1].color = (0.32, 0.66, 0.68, 1)
    nt.links.new(coord.outputs["Generated"], mapn.inputs["Vector"])
    nt.links.new(mapn.outputs["Vector"], grad.inputs["Vector"])
    nt.links.new(grad.outputs["Fac"], ramp.inputs["Fac"])
    nt.links.new(ramp.outputs["Color"], bg.inputs["Color"])
    bg.inputs["Strength"].default_value = 1.0
    s.world = w
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
    sun.data.energy = 3.2
    sun.data.color = (1.0, 0.96, 0.88)
    sun.data.angle = math.radians(6)
    sun.rotation_euler = (math.radians(35), math.radians(10), math.radians(35))
    s.collection.objects.link(sun)
    fill = bpy.data.objects.new("Fill", bpy.data.lights.new("Fill", "SUN"))
    fill.data.energy = 0.8
    fill.data.color = (0.6, 0.85, 1.0)
    fill.rotation_euler = (math.radians(120), 0, math.radians(-140))
    s.collection.objects.link(fill)
    # sand disc
    bpy.ops.mesh.primitive_circle_add(vertices=96, radius=40, fill_type="NGON")
    sand = bpy.context.active_object
    sand.data.materials.append(flat_mat("Sand", (0.50, 0.60, 0.54, 1), 0.9))
    return s


_mats = {}


def flat_mat(name, color, rough=0.6, emit=0.0):
    key = (name, color, emit)
    if key in _mats:
        return _mats[key]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = color
    b.inputs["Roughness"].default_value = rough
    if emit:
        b.inputs["Emission Color"].default_value = color
        b.inputs["Emission Strength"].default_value = emit
    _mats[key] = m
    return m


def vc_mat():
    if "VC" in _mats:
        return _mats["VC"]
    m = bpy.data.materials.new("VC")
    m.use_nodes = True
    nt = m.node_tree
    b = nt.nodes["Principled BSDF"]
    b.inputs["Roughness"].default_value = 0.5
    a = nt.nodes.new("ShaderNodeAttribute")
    a.attribute_name = "Col"
    nt.links.new(a.outputs["Color"], b.inputs["Base Color"])
    b.inputs["Subsurface Weight"].default_value = 0.08
    _mats["VC"] = m
    return m


def link(ob):
    if not ob.users_collection:
        bpy.context.scene.collection.objects.link(ob)
    return ob


def place(ob, loc, scale=1.0, rotz=0.0, rot=None):
    ob.location = loc
    ob.scale = (scale, scale, scale)
    ob.rotation_euler = rot if rot is not None else (0, 0, rotz)
    return ob


# ---------------------------------------------------------------- rack parts

def cylinder(a, b, r, mat, verts=16):
    a, b = Vector(a), Vector(b)
    d = b - a
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=d.length, location=(a + b) / 2)
    ob = bpy.context.active_object
    ob.rotation_euler = d.to_track_quat("Z", "Y").to_euler()
    ob.data.materials.append(mat)
    bpy.ops.object.shade_smooth()
    return ob


def block(center, size, mat, bevel=0.03):
    bpy.ops.mesh.primitive_cube_add(location=center)
    ob = bpy.context.active_object
    ob.scale = (size[0] / 2, size[1] / 2, size[2] / 2)
    bpy.ops.object.transform_apply(scale=True)
    mod = ob.modifiers.new("B", "BEVEL")
    mod.width = bevel
    mod.segments = 3
    ob.data.materials.append(mat)
    return ob


def socket_cup(pos, mat):
    """A planting socket on the rack: a short cup the table coral plugs into."""
    p = Vector(pos)
    cylinder(p - Vector((0, 0, 0.035)), p, 0.045, mat, 20)
    cylinder(p, p + Vector((0, 0, 0.012)), 0.055, mat, 20)


def glow_ring(pos, r=0.035, normal=(0, 0, 1)):
    bpy.ops.mesh.primitive_torus_add(major_radius=r, minor_radius=r * 0.16, location=pos,
                                     major_segments=32, minor_segments=8)
    ob = bpy.context.active_object
    ob.rotation_euler = Vector(normal).to_track_quat("Z", "Y").to_euler()
    ob.data.materials.append(flat_mat("Glow", SOCKET_GLOW, 0.3, emit=1.2))
    return ob


# ---------------------------------------------------------------- corals

def built(fn, name):
    b = cg.Builder()
    fn(b)
    ob = b.to_object(name, bpy.context.scene.collection)
    ob.data.materials.append(vc_mat())
    return ob


def coral(kind, stage, loc, scale, rotz=0.0, seed=1):
    rng = random.Random(seed)
    col = bpy.context.scene.collection
    fns = {
        "tube_m": lambda b: cg.build_tube(b, stage, rng, "Magenta"),
        "tube_l": lambda b: cg.build_tube(b, stage, rng, "Lemon"),
        "tube_b": lambda b: cg.build_tube(b, stage, rng, "Blossom"),
        "barrel": lambda b: cg.build_barrel(b, stage, rng),
        "finger": lambda b: cg.build_finger(b, stage, rng),
        "pillar_p": lambda b: cg.build_pillar(b, stage, rng, "Pink"),
        "pillar_c": lambda b: cg.build_pillar(b, stage, rng, "Coral"),
        "branch": lambda b: build_branch_no_rock(b, stage, col),
    }
    return place(built(fns[kind], kind), loc, scale, rotz)


def build_branch_no_rock(b, stage, col):
    n_before = len(b.v)
    cg.build_branch(b, stage, random.Random(1), col, with_rock=False)
    # drop the stone base the standalone model has: keep only the first skinned part
    return b


TABLE_TOP = [(0.0, cg.hexc("9FD36A")), (1.0, cg.hexc("D6EE8C"))]
TABLE_BODY = [(0.0, cg.hexc("6E8E58")), (1.0, cg.hexc("7FAE6C"))]


def table_profile(R, H):
    BODY, TIP = cg.BODY, cg.TIP
    r0 = 0.03
    return [
        (0.0, -0.01, BODY, 0.0, 0),
        (r0 * 1.5, -0.01, BODY, 0.0, 0),
        (r0 * 1.1, 0.25 * H, BODY, 0.2, 0),
        (r0 * 1.15, 0.75 * H, BODY, 0.45, 0),
        (R * 0.30, 0.97 * H, BODY, 0.6, 0),
        (R * 0.75, 1.01 * H, BODY, 0.85, 0.3),
        (R * 0.97, H + 0.012, BODY, 1.0, 1),
        (R * 1.00, H + 0.026, TIP, 1.0, 1),
        (R * 0.93, H + 0.036, TIP, 1.0, 0.6),
        (R * 0.55, H + 0.040, TIP, 0.95, 0),
        (0.0, H + 0.042, TIP, 0.9, 0),
    ]


def table_coral(loc, R, H=0.09, seed=3, name="Table"):
    paint = cg.palette_paint(TABLE_BODY, tip_stops=[(0.85, cg.hexc("C4E583")), (1.0, cg.hexc("A8D873"))])
    frame = Matrix.Identity(4)
    pl = cg.make_place(frame, H, seed=seed, wob=0.05, freq=2.5, jag_teeth=11, jag_amp=0.014 + R * 0.03)
    ob = built(lambda b: cg.lathe(b, table_profile(R, H), 36, pl, paint), name)
    ob.location = loc
    return ob, Vector(loc) + Vector((0, 0, H + 0.04))   # top surface


def table_sockets(top, R, count, phase=0.0):
    """Socket layout on a plate: centre plus a ring at 60% radius."""
    pts = [top.copy()] if count % 2 == 1 else []
    ring = count - len(pts)
    for i in range(ring):
        a = phase + math.tau * i / ring
        pts.append(top + Vector((math.cos(a), math.sin(a), 0)) * R * 0.6)
    return pts


def skin_tree(nodes, squash, scale, stops, name):
    me = bpy.data.meshes.new("skel")
    verts = [tuple(Vector((n[0].x, n[0].y * squash, n[0].z)) * scale) for n in nodes]
    edges = [(n[3], i) for i, n in enumerate(nodes) if n[3] >= 0]
    me.from_pydata(verts, edges, [])
    ob = bpy.data.objects.new("skel", me)
    bpy.context.scene.collection.objects.link(ob)
    ob.modifiers.new("Skin", "SKIN")
    for i, n in enumerate(nodes):
        r = max(n[1] * scale * 0.45, 0.004)
        me.skin_vertices[0].data[i].radius = (r, r * 0.6)
    me.skin_vertices[0].data[0].use_root = True
    sub = ob.modifiers.new("Sub", "SUBSURF")
    sub.levels = 1
    em = bpy.data.meshes.new_from_object(ob.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    bpy.data.objects.remove(ob)
    top = max(v.co.z for v in em.vertices) or 1
    b = cg.Builder()
    paint = cg.palette_paint(stops)
    for v in em.vertices:
        b.vert(v.co, **paint(cg.BODY, v.co.z / top, v.co))
    for p in em.polygons:
        b.face(list(p.vertices))
    out = b.to_object(name, bpy.context.scene.collection)
    out.data.materials.append(vc_mat())
    return out


def sea_fan(loc, scale, rot, seed=5):
    """Flat fan (membrane + veins) for rims and undersides; a shade-loving species."""
    rng = random.Random(seed)
    paint = cg.palette_paint([(0, cg.hexc("C2453F")), (0.6, cg.hexc("EE6F5A")), (1, cg.hexc("FFA27E"))])
    R = 0.36

    def fn(b):
        n = 26
        for side in (1, -1):  # two faces so it reads from both sides
            base = b.vert(Vector((0, 0.004 * side, 0)), **paint(cg.BODY, 0.0, Vector((0, 0, 0.1))))
            ring = []
            for i in range(n + 1):
                a = math.radians(-75 + 150 * i / n)
                r = R * (0.82 + 0.18 * math.sin(i * 1.7 + seed)) * (0.9 + 0.1 * math.cos(a))
                p = Vector((math.sin(a) * r, 0.004 * side + 0.02 * math.sin(i * 0.9), math.cos(a) * r))
                ring.append(b.vert(p, **paint(cg.BODY, p.z / R, p)))
            for i in range(n):
                b.face((base, ring[i], ring[i + 1]) if side > 0 else (base, ring[i + 1], ring[i]))
        vein = cg.palette_paint([(0, cg.hexc("9E2F33")), (1, cg.hexc("D9524A"))])
        for k in range(7):  # radial veins
            a = math.radians(-60 + 120 * k / 6) + rng.uniform(-0.05, 0.05)
            L = R * rng.uniform(0.75, 0.92)
            frame = cg.element_frame(Vector((0, 0, 0)), a, 0.0)
            pl = cg.make_place(frame, L, seed=k, wob=0.0)
            prof = [(0, 0, cg.BODY, 0, 0), (0.008, 0, cg.BODY, 0, 0), (0.005, L * 0.9, cg.BODY, 0.9, 0), (0, L, cg.BODY, 1, 0)]
            cg.lathe(b, prof, 6, pl, vein)
    ob = built(fn, "Fan")
    ob.location, ob.rotation_euler = loc, rot
    ob.scale = (scale, scale, scale)
    return ob


def grass(center, radius, count, seed=2, height=0.28):
    rng = random.Random(seed)
    paint = cg.palette_paint([(0, cg.hexc("2E9FB8")), (0.6, cg.hexc("58D3B5")), (1, cg.hexc("C9F27A"))])

    def fn(b):
        for i in range(count):
            a = rng.uniform(0, math.tau)
            d = radius * math.sqrt(rng.random())
            H = height * rng.uniform(0.6, 1.1)
            frame = cg.element_frame(Vector((math.cos(a) * d, math.sin(a) * d, 0)), rng.uniform(0.05, 0.35),
                                     rng.uniform(0, math.tau))
            pl = cg.make_place(frame, H, seed=i, wob=0.0, bend=rng.uniform(0.1, 0.3))
            prof = [(0, 0, cg.BODY, 0, 0), (0.012, 0, cg.BODY, 0, 0), (0.009, 0.5 * H, cg.BODY, 0.5, 0),
                    (0.004, 0.95 * H, cg.BODY, 0.95, 0), (0, H, cg.BODY, 1, 0)]
            cg.lathe(b, prof, 5, pl, paint, squash=(1.6, 0.5))
    ob = built(fn, "Grass")
    ob.location = center
    return ob


def rock(center, R, H, seed):
    paint = cg.palette_paint(None, rock=(cg.hexc("6F7B78"), cg.hexc("A9B4AE")))
    rr = random.Random(seed)
    frame = cg.element_frame(Vector((0, 0, 0)), rr.uniform(0, 0.06), rr.uniform(0, math.tau))
    pl = cg.make_place(frame, H, seed=seed, wob=0.12, freq=2.0)
    ob = built(lambda b: cg.lathe(b, cg.rock_profile(H, R), 8, pl, paint, smooth=False), "Rock")
    ob.location = center
    return ob



# ---------------------------------------------------------------- organic frame (driftwood)

WOOD = [(0.0, cg.hexc("3E2A1E")), (0.5, cg.hexc("5A3D2B")), (1.0, cg.hexc("72503A"))]


def catmull(pts, step=0.03):
    pts = [Vector(p) for p in pts]
    ext = [pts[0] * 2 - pts[1]] + pts + [pts[-1] * 2 - pts[-2]]
    out = []
    for i in range(1, len(ext) - 2):
        p0, p1, p2, p3 = ext[i - 1], ext[i], ext[i + 1], ext[i + 2]
        n = max(2, int((p2 - p1).length / step))
        for k in range(n):
            t = k / n
            out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t
                              + (-p0 + 3 * p1 - 3 * p2 + p3) * t * t * t))
    out.append(pts[-1])
    return out


def driftwood(paths, top_z):
    """One continuous sculpted frame. paths: (points, r0, r1, mid). A later path starts at the
    nearest node of the earlier ones, so branches grow out of the trunk. Each limb is skinned as
    its own closed tube, then a voxel remesh fuses them, so forks melt together without seams.
    mid=True makes the radius thin in the middle (arches)."""
    import bmesh
    nodes, chains = [], []
    for pts, r0, r1, mid in paths:
        pts = [Vector(q) for q in pts]
        chain = []
        if nodes:
            parent = min(range(len(nodes)), key=lambda i: (nodes[i][0] - pts[0]).length)
            root, root_r = nodes[parent]
            if pts[-1].z >= pts[0].z:  # leave along the trunk first, then peel away
                lead = root + (Vector((0, 0, 0.09)) + (pts[1] - root).normalized() * 0.03)
                pts = [root, lead] + pts[1:]
            else:
                pts = [root] + pts[1:]
            chain.append((root, min(root_r, r0)))
            smp = catmull(pts)[1:]
        else:
            smp = catmull(pts)
        for j, q in enumerate(smp):
            t = j / max(1, len(smp) - 1)
            r = r0 + (r1 - r0) * (math.sin(math.pi * t) if mid else t)
            nodes.append((q, r))
            chain.append((q, r))
        chains.append(chain)

    bm = bmesh.new()
    for chain in chains:
        me = bpy.data.meshes.new("limb")
        me.from_pydata([tuple(c[0]) for c in chain], [(i, i + 1) for i in range(len(chain) - 1)], [])
        ob = bpy.data.objects.new("limb", me)
        bpy.context.scene.collection.objects.link(ob)
        ob.modifiers.new("Skin", "SKIN")
        for i, (_, r) in enumerate(chain):
            me.skin_vertices[0].data[i].radius = (r, r * 0.82)
        me.skin_vertices[0].data[0].use_root = True
        ob.modifiers.new("Sub", "SUBSURF").levels = 1
        tube = bpy.data.meshes.new_from_object(ob.evaluated_get(bpy.context.evaluated_depsgraph_get()))
        bm.from_mesh(tube)  # appends
        bpy.data.objects.remove(ob)
        bpy.data.meshes.remove(me)
        bpy.data.meshes.remove(tube)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    merged = bpy.data.meshes.new("frame_merged")
    bm.to_mesh(merged)
    bm.free()
    ob = bpy.data.objects.new("frame_tmp", merged)
    bpy.context.scene.collection.objects.link(ob)
    rm = ob.modifiers.new("Remesh", "REMESH")
    rm.mode = "VOXEL"
    rm.voxel_size = 0.009
    sm = ob.modifiers.new("Smooth", "SMOOTH")
    sm.factor = 0.7
    sm.iterations = 14
    em = bpy.data.meshes.new_from_object(ob.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    bpy.data.objects.remove(ob)
    b = cg.Builder()
    for v in em.vertices:
        q = v.co
        grain = 0.86 + 0.14 * (0.5 + 0.5 * cg.noise.noise(Vector((q.x * 9, q.y * 9, q.z * 26))))
        b.vert(q, col=cg.ramp(WOOD, q.z / top_z) * grain)
    for poly in em.polygons:
        b.face(list(poly.vertices))
    frame = b.to_object("Driftwood", bpy.context.scene.collection)
    frame.data.materials.append(wood_mat())
    return frame


def wood_mat():
    if "Wood" in _mats:
        return _mats["Wood"]
    m = bpy.data.materials.new("Wood")
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = 0.62
    a = nt.nodes.new("ShaderNodeAttribute")
    a.attribute_name = "Col"
    nt.links.new(a.outputs["Color"], bsdf.inputs["Base Color"])
    _mats["Wood"] = m
    return m


def cradle(pos):
    """Shallow wooden cup at a branch end; the table coral plugs into it."""
    P = Vector(pos)
    prof = [(0.0, -0.055, 0, 0, 0), (0.028, -0.055, 0, 0, 0), (0.05, -0.03, 0, 0, 0), (0.074, -0.008, 0, 0, 0),
            (0.08, 0.004, 0, 0, 0), (0.066, 0.009, 0, 0, 0), (0.04, 0.0, 0, 0, 0), (0.0, -0.004, 0, 0, 0)]

    def fn(b):
        pl = cg.make_place(Matrix.Translation(P), 0.06, seed=int(P.z * 100), wob=0.08, freq=2.5)
        cg.lathe(b, prof, 18, pl, lambda part, h, q: dict(col=cg.ramp(WOOD, 0.9)))
    ob = built(fn, "Cradle")
    ob.data.materials.clear()
    ob.data.materials.append(wood_mat())


def roots(base, angles, length=0.3, r=0.045):
    out = []
    for a in angles:
        d = Vector((math.cos(a), math.sin(a), 0))
        out.append(([base + Vector((0, 0, 0.06)), base + d * length * 0.5 + Vector((0, 0, 0.015)),
                     base + d * length + Vector((0, 0, -0.012))], r, 0.012, False))
    return out


# ---------------------------------------------------------------- planting helper

def plant_on(top, R, plan, phase=0.0, scale=0.6):
    """plan: list of (kind, stage) or None for an empty (glowing) socket."""
    pts = table_sockets(top, R, len(plan), phase)
    for i, (p, item) in enumerate(zip(pts, plan)):
        if item is None:
            glow_ring(p + Vector((0, 0, 0.004)), 0.03)
        else:
            kind, stage = item
            coral(kind, stage, p - Vector((0, 0, 0.01)), scale * (0.85 if p != top else 1.0), rotz=i * 1.3, seed=i + 7)


# ---------------------------------------------------------------- variants

def variant_pillar(phase=3):
    trunk = ([(0, 0, 0.0), (0.07, 0.0, 0.22), (-0.05, 0.03, 0.46), (0.02, 0.0, 0.7)], 0.075, 0.042, False)
    driftwood([trunk] + roots(Vector((0, 0, 0)), (0.5, 2.6, 4.5), 0.3), 0.75)
    cradle((0, 0, 0.75))
    if phase == 1:
        glow_ring((0, 0, 0.77), 0.05)
        return Vector((0, 0, 0.45)), 1.6
    R = 0.26
    _, top = table_coral((0, 0, 0.75), R)
    if phase == 2:
        plant_on(top, R, [None] * 5, 0.4)
    else:
        plant_on(top, R, [("pillar_p", 3), ("tube_m", 3), ("finger", 3), ("barrel", 2), ("tube_l", 2)], 0.4, 0.5)
        sea_fan((0.2, 0.1, 0.84), 0.55, (0, math.radians(40), math.radians(25)))
        grass((0, 0, 0), 0.32, 40, height=0.36)
    return Vector((0, 0, 0.62)), 1.75


def variant_tower(phase=3):
    # S-curved trunk with branches that sweep out and up, like a carved driftwood shelf
    trunk = ([(0, 0, 0.0), (0.11, 0.0, 0.3), (-0.08, 0.04, 0.63), (0.09, -0.03, 0.98), (-0.05, 0.02, 1.3),
              (0.0, 0.0, 1.53)], 0.095, 0.045, False)
    arms = [(0.50, 0.0, 0.40, 0.30), (0.92, 2.1, 0.34, 0.25), (1.30, 4.2, 0.28, 0.21)]
    paths, sockets = [trunk], []
    for z, ang, length, R in arms:
        end = Vector((math.cos(ang) * length, math.sin(ang) * length, z))
        paths.append(([Vector((0, 0, z - 0.24)), Vector((end.x * 0.45, end.y * 0.45, z - 0.22)),
                       Vector((end.x * 0.85, end.y * 0.85, z - 0.1)), end - Vector((0, 0, 0.05))], 0.055, 0.034, False))
        sockets.append((end, R))
    paths += roots(Vector((0, 0, 0)), (0.9, 2.9, 4.9), 0.38, 0.055)
    driftwood(paths, 1.58)
    for end, _ in sockets:
        cradle(end)
    cradle((0, 0, 1.58))
    sockets.append((Vector((0, 0, 1.58)), 0.19))
    plans = [
        [("barrel", 3), ("tube_b", 3), ("finger", 3), ("pillar_c", 2), ("tube_m", 2)],
        [("pillar_p", 3), ("finger", 2), ("tube_l", 3), None],
        [("branch", 3), ("tube_m", 2), None],
        [("pillar_c", 3), ("finger", 2), ("tube_b", 2)],
    ]
    stages_for_phase2 = [3, 2, 1, 1]
    for i, ((pos, R), plan) in enumerate(zip(sockets, plans)):
        if phase == 1:
            glow_ring(pos + Vector((0, 0, 0.02)), 0.05)
            continue
        if phase == 2:
            r = R * [1.0, 0.7, 0.45][3 - stages_for_phase2[i]]
            _, top = table_coral(pos, r, seed=i + 2)
            plant_on(top, r, [None] * (len(plan) if stages_for_phase2[i] == 3 else 1), i * 0.7)
            continue
        _, top = table_coral(pos, R, seed=i + 2)
        plant_on(top, R, plan, i * 0.7, 0.55 if i else 0.6)
    if phase == 3:
        # shade-lovers hang under the lowest table; fans on the outer rims
        sea_fan((0.40, 0.0, 0.47), 0.55, (math.radians(165), 0, math.radians(90)), seed=8)
        sea_fan((-0.25, 0.36, 0.95), 0.6, (math.radians(-35), 0, math.radians(30)), seed=9)
        grass((0, 0, 0), 0.45, 60, seed=4, height=0.4)
    return Vector((0, 0, 0.92)), 2.75


def variant_arch(phase=3):
    # one flowing piece: two legs rise and merge into an undulating beam
    beam = ([(-0.8, 0.02, -0.01), (-0.72, 0.0, 0.3), (-0.62, 0.0, 0.68), (-0.45, 0.0, 0.81), (-0.22, 0.0, 0.78),
             (0.0, 0.0, 0.81), (0.22, 0.0, 0.77), (0.45, 0.0, 0.81), (0.63, 0.0, 0.7), (0.72, 0.0, 0.3),
             (0.8, -0.02, -0.01)], 0.08, 0.05, True)
    curl = ([(0.0, 0.0, 0.79), (0.06, 0.03, 0.7), (0.14, 0.05, 0.67), (0.18, 0.04, 0.72)], 0.028, 0.012, False)
    paths = [beam, curl] + roots(Vector((-0.8, 0.02, 0)), (2.4, 3.8), 0.22, 0.04) + roots(Vector((0.8, -0.02, 0)), (-0.7, 0.7), 0.22, 0.04)
    driftwood(paths, 0.87)
    xs, Rs = (-0.45, 0.0, 0.45), (0.22, 0.27, 0.2)
    plans = [[("tube_l", 3), ("finger", 3), ("pillar_p", 2)],
             [("barrel", 4), ("tube_m", 3), ("pillar_c", 3), ("finger", 2), ("tube_b", 2)],
             [("tube_b", 3), ("pillar_p", 3), ("finger", 2)]]
    for i, x in enumerate(xs):
        pos = Vector((x, 0, 0.87))
        cradle(pos)
        if phase == 1:
            glow_ring(pos + Vector((0, 0, 0.02)), 0.05)
            continue
        _, top = table_coral(pos, Rs[i], seed=i + 5)
        plant_on(top, Rs[i], plans[i] if phase == 3 else [None] * len(plans[i]), i, 0.55)
    if phase == 3:
        # hanging garden under the beam: fans point down, glowing sockets on the beam's underside
        sea_fan((-0.22, 0.0, 0.81), 0.6, (math.radians(180), 0, 0), seed=11)
        sea_fan((0.24, 0.0, 0.81), 0.5, (math.radians(176), 0, math.radians(8)), seed=12)
        grass((0, 0, 0), 0.5, 55, seed=6, height=0.4)
    return Vector((0, 0, 0.6)), 2.3


def variant_cairn(phase=3):
    rock((0, 0, 0), 0.42, 0.22, 1)
    rock((0.06, 0.02, 0.2), 0.3, 0.2, 2)
    rock((-0.03, 0.0, 0.38), 0.2, 0.16, 3)
    rock((0.38, -0.2, 0), 0.18, 0.12, 4)
    if phase == 1:
        for p in ((0.05, 0.02, 0.56), (0.3, 0.18, 0.4), (0.38, -0.2, 0.13)):
            glow_ring(p, 0.05)
        return Vector((0, 0, 0.35)), 1.6
    _, top = table_coral((0.0, 0.0, 0.53), 0.24, seed=7)
    plant_on(top, 0.24, [("branch", 2), ("tube_m", 3), ("pillar_p", 3), ("finger", 2), ("barrel", 2)], 0.2, 0.42)
    coral("finger", 3, (0.3, 0.2, 0.36), 0.6, seed=3)
    coral("tube_b", 3, (0.38, -0.2, 0.11), 0.5, seed=4)
    coral("barrel", 3, (-0.32, 0.18, 0.15), 0.45, seed=5)
    sea_fan((-0.25, -0.12, 0.36), 0.6, (0, math.radians(-40), math.radians(-30)), seed=13)
    grass((0, 0, 0), 0.6, 60, seed=8, height=0.38)
    return Vector((0, 0, 0.45)), 1.9



# ---------------------------------------------------------------- hanging nursery

BALL = {"amber": [(0, cg.hexc("E06A2C")), (0.55, cg.hexc("F5A33A")), (1, cg.hexc("FFD86E"))],
        "rose": [(0, cg.hexc("CF4C78")), (0.6, cg.hexc("F27FA6")), (1, cg.hexc("FFB3C6"))]}


def skin_nodes(nodes, stops, name, levels=2):
    """nodes: (pos, radius, parent). Vertex-coloured mesh, gradient by distance from the root."""
    me = bpy.data.meshes.new("sk")
    me.from_pydata([tuple(n[0]) for n in nodes], [(n[2], i) for i, n in enumerate(nodes) if n[2] >= 0], [])
    ob = bpy.data.objects.new("sk", me)
    bpy.context.scene.collection.objects.link(ob)
    ob.modifiers.new("Skin", "SKIN")
    for i, n in enumerate(nodes):
        me.skin_vertices[0].data[i].radius = (n[1], n[1])
    me.skin_vertices[0].data[0].use_root = True
    ob.modifiers.new("Sub", "SUBSURF").levels = levels
    em = bpy.data.meshes.new_from_object(ob.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    bpy.data.objects.remove(ob)
    root = nodes[0][0]
    reach = max((n[0] - root).length for n in nodes) or 1
    paint = cg.palette_paint(stops)
    b = cg.Builder()
    for v in em.vertices:
        b.vert(v.co, **paint(cg.BODY, (v.co - root).length / reach, Vector((0, 0, 1))))
    for poly in em.polygons:
        b.face(list(poly.vertices))
    out = b.to_object(name, bpy.context.scene.collection)
    out.data.materials.append(vc_mat())
    return out


def ball_coral(clip, stage, seed=1, palette="amber"):
    """A hung branching fragment: with no ground below it grows in every direction into a ball."""
    rng = random.Random(seed)
    count, L = [(6, 0.045), (11, 0.075), (17, 0.1)][stage - 1]
    centre = Vector(clip) - Vector((0, 0, L * 0.55))
    nodes = [(centre, 0.026, -1)]
    for i in range(count):
        z = 1 - 2 * (i + 0.5) / count
        rr = math.sqrt(max(0.0, 1 - z * z))
        phi = i * 2.39996 + seed
        d = (Vector((rr * math.cos(phi), rr * math.sin(phi), z)) + Vector((0, 0, 0.3))).normalized()
        idx, pos = 0, centre
        for k in range(3):
            d = (d + Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), 0.6)) * 0.12).normalized()
            pos = pos + d * (L / 3)
            nodes.append((pos, 0.019 * (1 - 0.12 * k), idx))
            idx = len(nodes) - 1
        if stage >= 2:  # a short side fork near the tip, so the colony reads as a bushy ball
            side = d.cross(Vector((0, 0, 1)))
            side = side.normalized() if side.length > 1e-3 else Vector((1, 0, 0))
            fork = (d + side * rng.choice((-0.8, 0.8)) + Vector((0, 0, 0.3))).normalized()
            nodes.append((nodes[idx - 1][0] + fork * L * 0.38, 0.015, idx - 1))
    return skin_nodes(nodes, BALL[palette], "BallCoral")


def sea_whips(clip, count, length, seed=3):
    """Soft, drooping whips: they hang down and sway, so they suit cords and overhangs."""
    rng = random.Random(seed)
    paint = cg.palette_paint([(0, cg.hexc("7B4FC9")), (0.6, cg.hexc("B56AD8")), (1, cg.hexc("F09AD6"))])

    def fn(b):
        for i in range(count):
            az = rng.uniform(0, math.tau)
            H = length * rng.uniform(0.7, 1.1)
            frame = cg.element_frame(Vector((0, 0, 0)), math.pi - rng.uniform(0.15, 0.5), az)
            pl = cg.make_place(frame, H, seed=i, wob=0.0, bend=rng.uniform(0.15, 0.35))
            prof = [(0, 0, cg.BODY, 0, 0), (0.008, 0, cg.BODY, 0, 0), (0.006, 0.6 * H, cg.BODY, 0.6, 0),
                    (0.004, 0.97 * H, cg.BODY, 0.97, 0), (0, H, cg.BODY, 1, 0)]
            cg.lathe(b, prof, 6, pl, paint)
    ob = built(fn, "Whips")
    ob.location = clip
    return ob


def variant_hanging(phase=3):
    """Close to the original nursery: tiers of horizontal arms with fragments hanging on cords and
    floats on top, but made as one piece of driftwood."""
    cord_mat = flat_mat("Cord", (0.16, 0.17, 0.14, 1), 0.8)
    float_mat = flat_mat("Float", (0.80, 0.75, 0.56, 1), 0.45)
    clip_mat = flat_mat("Clip", (0.86, 0.84, 0.76, 1), 0.5)
    trunk = ([(0, 0, 0.0), (0.06, 0.0, 0.45), (-0.05, 0.02, 0.95), (0.03, 0.0, 1.45), (0.0, 0.0, 1.8)], 0.09, 0.04, False)
    tiers = [(0.86, 0.25, 0.5), (1.2, 1.85, 0.44), (1.52, 0.45, 0.36)]   # height, direction, half span
    paths, cords = [trunk], []
    for t, (z, ang, half) in enumerate(tiers):
        for sgn in (1, -1):
            d = Vector((math.cos(ang) * sgn, math.sin(ang) * sgn, 0))
            side = Vector((-d.y, d.x, 0)) * 0.06 * sgn
            arm = [Vector((0, 0, z - 0.16)), d * half * 0.3 + Vector((0, 0, z - 0.04)),
                   d * half * 0.65 + side + Vector((0, 0, z - 0.01)), d * half * 0.92 + Vector((0, 0, z + 0.04)),
                   d * half * 1.02 + Vector((0, 0, z + 0.11))]
            paths.append((arm, 0.05, 0.034, False))
            smp = catmull(arm)
            for f in (0.4, 0.66, 0.88):
                top = smp[int(f * (len(smp) - 1))] - Vector((0, 0, 0.022))
                cords.append((t, top, top - Vector((0, 0, 0.1 + 0.08 * ((f * 7 + t) % 1)))))
    paths += roots(Vector((0, 0, 0)), (0.3, 2.4, 4.4), 0.36, 0.06)
    driftwood(paths, 1.82)
    # buoyancy floats on a short line above the crown, like the original nursery
    cylinder((0, 0, 1.78), (0, 0, 2.03), 0.004, cord_mat, 6)
    for off, r in ((Vector((0, 0, 2.08)), 0.075), (Vector((0.09, 0.03, 2.01)), 0.05), (Vector((-0.07, -0.05, 2.02)), 0.05)):
        bpy.ops.mesh.primitive_uv_sphere_add(radius=r, location=off)
        bpy.context.active_object.data.materials.append(float_mat)
        bpy.ops.object.shade_smooth()
        if off.x:
            cylinder((0, 0, 2.0), off, 0.003, cord_mat, 6)

    # top tier gets the most light -> branching balls; the shaded bottom tier -> fans and whips
    plan = {
        2: [("ball", 3, "amber"), ("ball", 3, "rose"), ("ball", 2, "amber"), ("ball", 3, "amber"), None, ("ball", 2, "rose")],
        1: [("ball", 2, "rose"), ("whips", 0, None), ("ball", 3, "amber"), ("fan", 0, None), ("ball", 2, "amber"), None],
        0: [("fan", 0, None), ("whips", 0, None), ("ball", 1, "amber"), ("whips", 0, None), ("fan", 0, None), ("ball", 2, "rose")],
    }
    counters = {0: 0, 1: 0, 2: 0}
    for i, (tier, top, clip) in enumerate(cords):
        cylinder(top, clip, 0.003, cord_mat, 6)
        bpy.ops.mesh.primitive_uv_sphere_add(radius=0.011, location=clip, segments=12, ring_count=8)
        bpy.context.active_object.data.materials.append(clip_mat)
        item = plan[tier][counters[tier]]
        counters[tier] += 1
        if phase == 1 or item is None:
            glow_ring(clip - Vector((0, 0, 0.012)), 0.03)
            continue
        kind, stage, pal = item
        if phase == 2:  # young fragments a few weeks in
            stage = 1
        if kind == "ball":
            ball_coral(clip, stage, seed=i, palette=pal or "amber")
        elif kind == "whips":
            sea_whips(clip, 7 if phase == 3 else 3, 0.28 if phase == 3 else 0.12, seed=i)
        else:
            sea_fan(clip, 0.5 if phase == 3 else 0.25, (math.radians(180), 0, math.radians(i * 40)), seed=i)
    if phase == 3:
        grass((0, 0, 0), 0.45, 50, seed=12, height=0.38)
    return Vector((0, 0, 1.1)), 2.75


# ---------------------------------------------------------------- render

def render(name, center, size, angle=-38.0, elev=22.0):
    s = bpy.context.scene
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    cam.data.lens = 50
    s.collection.objects.link(cam)
    dist = size * 1.3
    a, e = math.radians(angle), math.radians(elev)
    cam.location = center + Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e))) * dist
    cam.rotation_euler = (center - cam.location).to_track_quat("-Z", "Y").to_euler()
    s.camera = cam
    s.render.filepath = os.path.join(OUT, name + ".png")
    bpy.ops.render.render(write_still=True)
    print("RENDERED", s.render.filepath)


JOBS = [
    ("variant_A_pillar", variant_pillar, 3, {}),
    ("variant_B_tower", variant_tower, 3, {}),
    ("variant_C_arch", variant_arch, 3, {"angle": -25, "elev": 26}),
    ("variant_D_cairn", variant_cairn, 3, {}),
    ("growth_1_base", variant_tower, 1, {}),
    ("growth_2_tables", variant_tower, 2, {}),
    ("growth_3_full", variant_tower, 3, {}),
    ("hanging_1_empty", variant_hanging, 1, {}),
    ("hanging_2_young", variant_hanging, 2, {}),
    ("hanging_3_grown", variant_hanging, 3, {}),
]

if __name__ == "__main__":
    only = sys.argv[sys.argv.index("--") + 2] if "--" in sys.argv and len(sys.argv) > sys.argv.index("--") + 2 else ""
    for name, fn, phase, kw in JOBS:
        if only and only not in name:
            continue
        _mats.clear()
        reset()
        center, size = fn(phase)
        render(name, center, size, **kw)
