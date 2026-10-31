"""Stylised shipwreck, same look as the rack bases: chunky rounded planks, deep weathered wood,
moss cushions, green rope, tattered sail strips. Broken into three hull pieces + mast + props.

blender.exe -b --factory-startup --python shipwreck.py -- OUT_DIR [only_view]
"""
import math
import os
import random
import sys

sys.dont_write_bytecode = True

import bmesh  # noqa: E402
import bpy  # noqa: E402
from mathutils import Matrix, Vector, noise  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bases as bs  # noqa: E402

rc, cg = bs.rc, bs.cg
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = argv[0] if argv else "D:/blender-5.2.3/projects/coral_growth/shipwreck"  # keep work files out of Assets
ONLY = argv[1] if len(argv) > 1 else ""
os.makedirs(OUT, exist_ok=True)

L, W, D = 7.2, 1.2, 1.25          # hull length, half beam, depth (m)
WOOD = rc.WOOD
ROPE_COL = (0.08, 0.16, 0.07, 1)
SAIL = [(0.0, cg.hexc("8F8466")), (0.6, cg.hexc("766F52")), (1.0, cg.hexc("4F5C3A"))]


# ---------------------------------------------------------------- hull shape

def width(u):
    return W * max(0.0, math.sin(math.pi * (0.16 + 0.84 * u))) ** 0.55


def depth(u):
    return D * (0.8 + 0.2 * math.sin(math.pi * u))


def sheer(u):
    return 0.34 * (2 * u - 1) ** 2 + 0.05 * u      # gunwale rises toward bow and stern


def hull_pt(u, v, side):
    phi = v * math.pi / 2
    y = side * width(u) * math.sin(phi) ** 0.75
    z = sheer(u) - depth(u) * math.cos(phi) ** 1.25
    return Vector((L * (u - 0.5), y, z))


def hull_n(u, v, side):
    a = hull_pt(min(u + 1e-3, 1), v, side) - hull_pt(max(u - 1e-3, 0), v, side)
    b = hull_pt(u, min(v + 1e-3, 1), side) - hull_pt(u, max(v - 1e-3, 0), side)
    n = a.cross(b)
    n = n.normalized() if n.length > 1e-9 else Vector((0, side, 0))
    phi = v * math.pi / 2
    if n.dot(Vector((0, side * math.sin(phi), -math.cos(phi)))) < 0:
        n = -n
    return n


def hull_surf(side, inset=0.0):
    def f(u, v):
        n = hull_n(u, v, side)
        return hull_pt(u, v, side) - n * inset, n
    return f


def deck_surf(drop=0.07):
    def f(u, s):
        return Vector((L * (u - 0.5), s * width(u) * 0.96, sheer(u) - drop)), Vector((0, 0, 1))
    return f


# ---------------------------------------------------------------- pieces

def about(centre, offset, rot):
    """Rotate a hull piece about its own centre, then shift it."""
    return Matrix.Translation(Vector(offset)) @ Matrix.Translation(centre) @ rot @ Matrix.Translation(-centre)


def wood_col(rng, dark=0.0):
    return cg.ramp(WOOD, rng.uniform(0.3, 0.95)) * rng.uniform(0.82, 1.0) * (1 - dark)


def slab(b, surf, u0, u1, v0, v1, thick, col, rng, broken=(False, False), rows=None):
    """A chunky plank: a thick strip on a surface between u0..u1 (length) and v0..v1 (width).
    Broken ends get ragged corners."""
    rows = rows or max(2, int(abs(u1 - u0) * L / 0.22) + 1)
    rings = []
    for i in range(rows + 1):
        u = u0 + (u1 - u0) * i / rows
        corners = []
        for v in (v0, v1):
            uu = u
            if (i == 0 and broken[0]) or (i == rows and broken[1]):
                uu = u + rng.uniform(-0.035, 0.035)
            corners.append(surf(uu, v))
        (pa, na), (pb, nb) = corners
        o = thick * 0.5
        pts = [pa + na * o, pb + nb * o, pb - nb * o, pa - na * o]
        rings.append([b.vert(q, col=col) for q in pts])
    for i in range(rows):
        a, c = rings[i], rings[i + 1]
        for k in range(4):
            b.face((a[k], a[(k + 1) % 4], c[(k + 1) % 4], c[k]))
    b.face(list(reversed(rings[0])))
    b.face(rings[-1])


def planked(b, surf, u_lo, u_hi, bands, keep, rng, thick=0.08, v_lo=0.0, v_hi=1.0, seg=(0.14, 0.28)):
    for k in range(bands):
        v0 = v_lo + (v_hi - v_lo) * k / bands + 0.0015
        v1 = v_lo + (v_hi - v_lo) * (k + 1) / bands - 0.0015
        u = u_lo + rng.uniform(0.0, 0.05)
        while u < u_hi - 0.02:
            u1 = min(u + rng.uniform(*seg), u_hi)
            if keep(u, (v0 + v1) / 2, rng):
                slab(b, surf, u + 0.003, u1 - 0.003, v0, v1, thick, wood_col(rng), rng,
                     broken=(rng.random() < 0.3, rng.random() < 0.3))
            u = u1


def finish(b, name, matrix, subsurf=1):
    ob = b.to_object(name, bpy.context.scene.collection)
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(ob.data)
    bm.free()
    if subsurf:
        bev = ob.modifiers.new("Soften", "BEVEL")                 # soft board edges, boards keep their size
        bev.width = 0.018
        bev.segments = 3
        bev.limit_method = "ANGLE"
        ob.modifiers.new("Normals", "WEIGHTED_NORMAL")
        ob.data.polygons.foreach_set("use_smooth", [True] * len(ob.data.polygons))
    ob.matrix_world = matrix
    return ob


def beams(chains, name, matrix, color_fn, levels=1):
    """Rounded beams (ribs, keel, rails, mast...) from point chains: skin + subdivision."""
    verts, edges, radii, roots = [], [], [], []
    for pts, r0, r1 in chains:
        start = len(verts)
        roots.append(start)
        for i, p in enumerate(pts):
            t = i / max(1, len(pts) - 1)
            verts.append(tuple(p))
            radii.append(r0 + (r1 - r0) * t)
            if i:
                edges.append((start + i - 1, start + i))
    me = bpy.data.meshes.new(name + "_skel")
    me.from_pydata(verts, edges, [])
    ob = bpy.data.objects.new(name + "_skel", me)
    bpy.context.scene.collection.objects.link(ob)
    ob.modifiers.new("Skin", "SKIN")
    for i, r in enumerate(radii):
        me.skin_vertices[0].data[i].radius = (r, r * 0.85)
    for r in roots:
        me.skin_vertices[0].data[r].use_root = True
    ob.modifiers.new("Sub", "SUBSURF").levels = levels
    em = bpy.data.meshes.new_from_object(ob.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    bpy.data.objects.remove(ob)
    b = cg.Builder()
    for v in em.vertices:
        b.vert(v.co, col=color_fn(v.co))
    for p in em.polygons:
        b.face(list(p.vertices))
    out = b.to_object(name, bpy.context.scene.collection)
    out.matrix_world = matrix
    return out


def rib_chain(u, side_v_max=(1.0, 1.0), inset=0.1):
    pts = []
    for k in range(10, -1, -1):          # left gunwale -> keel
        v = k / 10 * side_v_max[0]
        pts.append(hull_pt(u, v, -1) - hull_n(u, v, -1) * inset)
    for k in range(1, 11):               # keel -> right gunwale
        v = k / 10 * side_v_max[1]
        pts.append(hull_pt(u, v, 1) - hull_n(u, v, 1) * inset)
    return pts


def wood_fn(rng_seed, dark=0.0):
    def f(p):
        g = 0.85 + 0.15 * (0.5 + 0.5 * noise.noise(Vector((p.x * 3, p.y * 3, p.z * 9)) + Vector((rng_seed, 0, 0))))
        return cg.ramp(WOOD, 0.35 + 0.4 * (0.5 + 0.5 * noise.noise(p * 0.7))) * g * (1 - dark)
    return f


def add_sway_nodes(ob, amount=0.25, flutter=0.035, period=4.0, height=1.5, current=(0.35, 0.2)):
    """Animate cloth/weed strips in Blender with Geometry Nodes, using the same motion as the Unity
    'Lit Sway' shader: reads the sway_h (0 at the fixed top, 1 at the free end) and sway_p (phase)
    attributes. A slow lean plus a faster flutter; loops every `period` seconds."""
    ng = bpy.data.node_groups.new("SailSway", "GeometryNodeTree")
    ng.interface.new_socket("Geometry", in_out="INPUT", socket_type="NodeSocketGeometry")
    ng.interface.new_socket("Geometry", in_out="OUTPUT", socket_type="NodeSocketGeometry")
    N, Lk = ng.nodes, ng.links
    gin, gout = N.new("NodeGroupInput"), N.new("NodeGroupOutput")
    setpos = N.new("GeometryNodeSetPosition")
    Lk.new(gin.outputs[0], setpos.inputs["Geometry"])
    Lk.new(setpos.outputs[0], gout.inputs[0])

    def attr(name):
        a = N.new("GeometryNodeInputNamedAttribute")
        a.data_type = "FLOAT"
        a.inputs["Name"].default_value = name
        return a.outputs["Attribute"]

    def op(kind, a, b=None):
        m = N.new("ShaderNodeMath")
        m.operation = kind
        for i, v in enumerate((a, b)):
            if v is None:
                continue
            if isinstance(v, (int, float)):
                m.inputs[i].default_value = float(v)
            else:
                Lk.new(v, m.inputs[i])
        return m.outputs[0]

    h = attr("sway_h")
    p = op("MULTIPLY", attr("sway_p"), math.tau)
    t = op("MULTIPLY", N.new("GeometryNodeInputSceneTime").outputs["Seconds"], math.tau / period)
    a = op("ADD", t, p)
    lean_x = op("ADD", op("SINE", a), current[0])
    lean_y = op("ADD", op("MULTIPLY", op("COSINE", op("ADD", a, op("MULTIPLY", p, 0.3))), 0.6), current[1])
    flut = op("SINE", op("ADD", op("ADD", op("MULTIPLY", t, 3.0), op("MULTIPLY", h, 5.0)), op("MULTIPLY", p, 3.0)))
    h2 = op("MULTIPLY", h, h)
    ox = op("ADD", op("MULTIPLY", op("MULTIPLY", lean_x, amount), h2), op("MULTIPLY", op("MULTIPLY", flut, flutter * 0.6), h))
    oy = op("ADD", op("MULTIPLY", op("MULTIPLY", lean_y, amount), h2), op("MULTIPLY", op("MULTIPLY", flut, flutter * 0.8), h))
    oz = op("MULTIPLY", op("ADD", op("MULTIPLY", ox, ox), op("MULTIPLY", oy, oy)), -0.5 / height)   # keep strip length
    comb = N.new("ShaderNodeCombineXYZ")
    for i, v in enumerate((ox, oy, oz)):
        Lk.new(v, comb.inputs[i])
    Lk.new(comb.outputs[0], setpos.inputs["Offset"])
    mod = ob.modifiers.new("Sway", "NODES")
    mod.node_group = ng
    return mod


def catenary(a, b, sag, n=10):
    a, b = Vector(a), Vector(b)
    return [a.lerp(b, i / n) - Vector((0, 0, sag * 4 * (i / n) * (1 - i / n))) for i in range(n + 1)]


# ---------------------------------------------------------------- build

def build():
    rng = random.Random(7)
    sockets = []
    wood_objs, sail_objs = [], []

    # -- bow: the big piece, lying on its side a little, keel buried
    M_bow = about(Vector((L * 0.3, 0, 0)), (0.35, -0.15, 1.0), Matrix.Rotation(math.radians(-6), 4, "Z")
                  @ Matrix.Rotation(math.radians(17), 4, "X") @ Matrix.Rotation(math.radians(-4), 4, "Y"))
    b = cg.Builder()
    for side in (-1, 1):
        def keep(u, v, r, side=side):
            if side == 1 and 0.74 < u < 0.88 and 0.3 < v < 0.8:      # a stove-in hole
                return False
            if u < 0.66 and r.random() < 0.35:                         # torn edge
                return False
            return r.random() > 0.06
        planked(b, hull_surf(side), 0.6, 1.0, 9, keep, rng)
    planked(b, deck_surf(), 0.62, 0.97, 7, lambda u, s, r: r.random() > (0.35 if u < 0.7 else 0.12), rng,
            thick=0.06, v_lo=-1.0, v_hi=1.0)
    wood_objs.append(finish(b, "Wreck_Bow", M_bow))
    chains = [(rib_chain(u), 0.065, 0.065) for u in [0.61 + 0.05 * k for k in range(8)]]
    chains.append(([hull_pt(0.6 + 0.4 * i / 12, 0, 1) - Vector((0, 0, 0.05)) for i in range(13)], 0.1, 0.08))       # keel
    for side in (-1, 1):
        chains.append(([hull_pt(0.62 + 0.38 * i / 10, 1, side) + Vector((0, 0, 0.04)) for i in range(11)], 0.06, 0.05))  # rail
    bow_tip = hull_pt(1.0, 1.0, 1) * 0 + Vector((L * 0.5, 0, sheer(1.0)))
    chains.append(([bow_tip, bow_tip + Vector((0.8, 0, 0.35)), bow_tip + Vector((1.5, 0, 0.62))], 0.08, 0.04))      # bowsprit
    wood_objs.append(beams(chains, "Wreck_BowFrame", M_bow, wood_fn(1, 0.12)))
    for u, s in ((0.8, -0.35), (0.86, 0.3), (0.74, 0.0)):                 # planting spots on the deck
        p, _ = deck_surf(0.03)(u, s)
        sockets.append(M_bow @ p)

    # -- mast, standing from the bow piece, leaning back
    mast_base = Vector((L * 0.14, 0, sheer(0.64) - 0.4))
    lean = Matrix.Rotation(math.radians(-9), 4, "Y")
    def up(z, x=0.0, y=0.0):
        return M_bow @ (mast_base + lean @ Vector((x, y, z)))
    mast = [up(z) for z in [0, 1, 2, 3, 4, 4.7]]
    chains = [(mast, 0.13, 0.08)]
    chains.append(([up(4.5) + Vector((0, 0, 0)), up(5.05, 0.05, 0.02)], 0.07, 0.035))                     # broken top
    chains.append(([up(3.25, 0.0, -1.5), up(3.3, 0.0, 0.0), up(2.75, 0.1, 1.35)], 0.07, 0.05))           # sagging yard
    chains.append(([up(1.9, 0.15, -0.2), up(1.0, 0.6, -1.1)], 0.06, 0.05))                                 # fallen lower yard
    ring = [up(3.9, 0.36 * math.cos(a), 0.36 * math.sin(a)) for a in [k * math.tau / 16 for k in range(17)]]
    chains.append((ring, 0.045, 0.045))                                                                    # crow's nest rim
    wood_objs.append(beams(chains, "Wreck_Mast", Matrix.Identity(4), wood_fn(3, 0.08)))
    nest = cg.Builder()
    nest_surf = lambda u, v: (up(3.82, (u - 0.5) * 0.72, (v - 0.5) * 0.72), (lean @ Vector((0, 0, 1))).normalized())
    for k in range(4):
        slab(nest, nest_surf, 0.08, 0.92, 0.1 + k * 0.2, 0.28 + k * 0.2, 0.05, wood_col(rng), rng)
    wood_objs.append(finish(nest, "Wreck_Nest", Matrix.Identity(4)))

    # rigging: shrouds + ratlines (rope ladders), green rope like the bases
    ropes = []
    for side in (-1, 1):
        tops = [up(3.75, -0.05 + 0.12 * i, 0.15 * side) for i in range(3)]
        bots = [M_bow @ (hull_pt(u, 1, side) + Vector((0, 0, 0.05))) for u in (0.58, 0.64, 0.7)]
        lines = [catenary(t, bt, 0.06, 12) for t, bt in zip(tops, bots)]
        ropes += [(ln, 0.018, 0.018) for ln in lines]
        for k in range(2, 11, 2):                                   # ratlines between shrouds
            ropes.append(([lines[0][k], lines[1][k], lines[2][k]], 0.012, 0.012))
    ropes.append((catenary(up(4.4), M_bow @ (bow_tip + Vector((1.45, 0, 0.6))), 0.25, 14), 0.02, 0.02))  # forestay
    ropes.append((catenary(up(4.3), up(3.25, 0, -1.45), 0.15, 8), 0.014, 0.014))
    rope_ob = beams(ropes, "Wreck_Rigging", Matrix.Identity(4), lambda p: Vector(ROPE_COL[:3]))

    # tattered sail strips hanging from the yard (sway data included for later)
    sb = cg.Builder()
    for i in range(10):
        t = (i + 0.5) / 10
        if rng.random() < 0.25:
            continue
        top = up(3.15 - 0.45 * max(0, t - 0.5) * 2, 0.08, -1.45 + 2.8 * t)
        frame = Matrix.Translation(top) @ Matrix.Rotation(math.pi, 4, "X") @ Matrix.Rotation(math.pi / 2, 4, "Z") \
            @ Matrix.Rotation(rng.uniform(-0.1, 0.1), 4, "Y")
        cg.ribbon(sb, frame, rng.uniform(0.8, 2.1), rng.uniform(0.11, 0.15), SAIL, rng.uniform(0, math.tau),
                  ruffle=0.25, waves=0.05, cols=3, rows=14, taper=0.4)
    sail = finish(sb, "Wreck_Sail", Matrix.Identity(4), subsurf=0)
    add_sway_nodes(sail)                       # strips drift with the current (loops every 4 s)
    sail_objs.append(sail)
    scene = bpy.context.scene
    scene.render.fps = 24
    scene.frame_start, scene.frame_end = 1, 96

    # -- midship: broken, ribs exposed, half buried
    M_mid = about(Vector((-L * 0.05, 0, 0)), (0.0, 0.1, 0.98), Matrix.Rotation(math.radians(4), 4, "Z")
                  @ Matrix.Rotation(math.radians(9), 4, "X"))
    b = cg.Builder()
    for side in (-1, 1):
        planked(b, hull_surf(side), 0.33, 0.58, 9, lambda u, v, r: r.random() < (0.85 if v < 0.55 else 0.55), rng)
    wood_objs.append(finish(b, "Wreck_Mid", M_mid))
    chains = []
    for k, u in enumerate([0.34 + 0.035 * k for k in range(7)]):
        lo, hi = rng.uniform(0.45, 1.0), rng.uniform(0.35, 1.0)        # some ribs snapped off
        chains.append((rib_chain(u, (lo, hi)), 0.07, 0.07))
    chains.append(([hull_pt(0.32 + 0.27 * i / 8, 0, 1) - Vector((0, 0, 0.05)) for i in range(9)], 0.1, 0.1))
    wood_objs.append(beams(chains, "Wreck_MidFrame", M_mid, wood_fn(5, 0.15)))

    # -- stern: separated, turned, with a raised stern castle
    M_st = about(Vector((-L * 0.37, 0, 0)), (-0.2, 0.85, 0.95), Matrix.Rotation(math.radians(28), 4, "Z")
                 @ Matrix.Rotation(math.radians(-13), 4, "X") @ Matrix.Rotation(math.radians(5), 4, "Y"))
    b = cg.Builder()
    for side in (-1, 1):
        planked(b, hull_surf(side), 0.0, 0.27, 9, lambda u, v, r: not (u > 0.22 and r.random() < 0.5) and r.random() > 0.08, rng)
    planked(b, deck_surf(), 0.02, 0.25, 6, lambda u, s, r: r.random() > 0.15, rng, thick=0.055, v_lo=-1.0, v_hi=1.0)
    # transom (flat back) and castle walls
    tr = lambda u, v: (Vector((-L * 0.5 + 0.02, (u - 0.5) * 2 * width(0.0) * 0.95, sheer(0.0) - depth(0.0) * 0.7 + v * (depth(0.0) * 0.7 + 0.75))), Vector((-1, 0, 0)))
    planked(b, tr, 0.05, 0.95, 8, lambda u, v, r: r.random() > (0.12 if v < 0.7 else 0.3), rng, seg=(0.2, 0.45))
    for side in (-1, 1):
        wall = lambda u, v, side=side: (Vector((L * (u - 0.5), side * width(u) * 0.97, sheer(u) + v * 0.75)), Vector((0, side, 0)))
        planked(b, wall, 0.0, 0.2, 5, lambda u, v, r: r.random() > 0.2, rng)
    castle = lambda u, s: (Vector((L * (u - 0.5), s * width(u) * 0.95, sheer(u) + 0.75)), Vector((0, 0, 1)))
    planked(b, castle, 0.0, 0.2, 6, lambda u, s, r: r.random() > 0.2, rng, thick=0.055, v_lo=-1.0, v_hi=1.0)
    wood_objs.append(finish(b, "Wreck_Stern", M_st))
    chains = [(rib_chain(u), 0.065, 0.065) for u in [0.04 + 0.05 * k for k in range(5)]]
    chains.append(([hull_pt(0.27 * i / 6, 0, 1) - Vector((0, 0, 0.05)) for i in range(7)], 0.1, 0.1))
    for side in (-1, 1):
        chains.append(([Vector((L * (0.2 * i / 5 - 0.5), side * width(0.2 * i / 5) * 0.97, sheer(0.2 * i / 5) + 0.8)) for i in range(6)], 0.06, 0.05))
        chains.append(([Vector((L * (u - 0.5), side * width(u) * 0.97, sheer(u) + z)) for u, z in ((0.0, 0.0), (0.0, 0.8))], 0.07, 0.07))
    wood_objs.append(beams(chains, "Wreck_SternFrame", M_st, wood_fn(9, 0.12)))
    for u, s in ((0.08, -0.3), (0.14, 0.35)):
        p, _ = castle(u, s)
        sockets.append(M_st @ (p + Vector((0, 0, 0.03))))

    # -- props: barrels and loose planks on the sand
    props = cg.Builder()
    barrel = [(0.0, 0.0, cg.BODY, 0, 0), (0.22, 0.0, cg.BODY, 0, 0), (0.27, 0.18, cg.BODY, 0, 0), (0.29, 0.34, cg.BODY, 0, 0),
              (0.27, 0.5, cg.BODY, 0, 0), (0.22, 0.68, cg.BODY, 0, 0), (0.0, 0.68, cg.BODY, 0, 0)]
    for (x, y, tilt, az) in ((-1.2, -1.5, 0.0, 0.3), (-0.65, -1.75, 0.05, 1.1), (2.9, -1.4, 1.45, 2.0)):
        frame = cg.element_frame(Vector((x, y, 0.0 if tilt < 1 else 0.27)), tilt, az)
        cg.lathe(props, barrel, 20, cg.make_place(frame, 0.68, seed=int(x * 10), wob=0.03),
                 lambda part, h, p: dict(col=cg.ramp(WOOD, 0.6) * 0.9))
    flat = lambda x, y, yaw: (lambda u, v: (Vector((x, y, 0.03)) + Matrix.Rotation(yaw, 3, "Z") @ Vector(((u - 0.5) * L, v * 0.18, 0)), Vector((0, 0, 1))))
    for (x, y, yaw) in ((0.6, -2.0, 0.4), (-2.0, -0.6, 1.3), (3.4, 1.4, -0.5), (-3.9, -0.4, 0.9)):
        slab(props, flat(x, y, yaw), 0.5 - 0.06, 0.5 + rng.uniform(0.05, 0.1), 0, 1, 0.06, wood_col(rng), rng, broken=(True, True))
    props_ob = finish(props, "Wreck_Props", Matrix.Identity(4))
    wood_objs.append(props_ob)
    hoops = []                                   # dark iron hoops on the barrels
    for (x, y, tilt, az) in ((-1.2, -1.5, 0.0, 0.3), (-0.65, -1.75, 0.05, 1.1), (2.9, -1.4, 1.45, 2.0)):
        frame = cg.element_frame(Vector((x, y, 0.0 if tilt < 1 else 0.27)), tilt, az)
        for z, r in ((0.12, 0.262), (0.56, 0.255)):
            hoops.append(([frame @ Vector((r * math.cos(a), r * math.sin(a), z)) for a in [k * math.tau / 24 for k in range(25)]], 0.022, 0.022))
    hoop_ob = beams(hoops, "Wreck_Hoops", Matrix.Identity(4), lambda p: Vector((0.1, 0.11, 0.09)))

    # sand drifts that bury the keel line (render only; Unity uses its own seabed)
    for (x, y, sx, sy, h) in ():
        bpy.ops.mesh.primitive_uv_sphere_add(segments=48, ring_count=24, location=(x, y, 0))
        s = bpy.context.active_object
        s.name = "SandDrift"
        s.scale = (sx, sy, h)
        s.data.materials.append(rc.flat_mat("SandDrift", (0.42, 0.5, 0.44, 1), 0.95))
        bpy.ops.object.shade_smooth()

    # materials + moss
    wood_mat = bs.relic_material("WreckWood", moss_up=(-0.1, 0.7), moss_amount=0.2)
    for ob in wood_objs:
        ob.data.materials.append(wood_mat)
    sail_mat = bs.relic_material("WreckSail", (0.22, 0.2, 0.13, 1), (0.34, 0.31, 0.2, 1), moss_up=(0.2, 0.9), moss_amount=0.1)
    for ob in sail_objs:
        ob.data.materials.append(sail_mat)
    rope_ob.data.materials.append(rc.flat_mat("Rope", ROPE_COL, 0.8))
    hoop_ob.data.materials.append(rc.flat_mat("Iron", (0.07, 0.08, 0.07, 1), 0.6))
    dg = bpy.context.evaluated_depsgraph_get()
    tmp = []
    for ob in [o for o in wood_objs if o is not props_ob]:     # keep the barrels readable
        me = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
        t = bpy.data.objects.new("tmp", me)
        t.matrix_world = ob.matrix_world
        bpy.context.scene.collection.objects.link(t)
        tmp.append(t)
    bs.moss_clumps(tmp, 2600, seed=11, min_up=0.5, size=(0.012, 0.03))
    for t in tmp:
        bpy.data.objects.remove(t)

    for i, p in enumerate(sockets):
        e = bpy.data.objects.new(f"Socket_Table_{i:02d}", None)
        e.empty_display_size = 0.08
        e.location = p
        bpy.context.scene.collection.objects.link(e)
    print(f"[wreck] objects: {len(wood_objs)} wood, sockets {len(sockets)}")


VIEWS = [("wreck_hero", (-28, 36), Vector((0.2, 0.3, 1.1)), 8.8),
         ("wreck_side", (-8, 14), Vector((0.2, 0.3, 1.4)), 8.6),
         ("wreck_top", (-60, 62), Vector((0.2, 0.3, 0.7)), 9.0),
         ("wreck_bow_close", (-55, 22), Vector((2.0, 0.0, 1.4)), 4.6)]

if __name__ == "__main__":
    rc._mats.clear()
    scene = rc.reset()
    bs.soft_world(scene)
    build()
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "shipwreck.blend"))
    rc.OUT = OUT
    for name, (ang, elev), center, size in VIEWS:
        if ONLY and ONLY not in name:
            continue
        rc.render(name, center, size, angle=ang, elev=elev)
