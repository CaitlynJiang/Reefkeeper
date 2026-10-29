"""Three rack bases, no corals: B tiered tree, D stone cairn, E hanging nursery.
Surface: old mossy relic (moss on upward faces + puffy clumps, worn edges, wrapped rope).

blender.exe -b --factory-startup --python bases.py -- OUT_DIR [only]
"""
import math
import os
import random
import sys
sys.dont_write_bytecode = True  # keep __pycache__ out of the Unity Assets folder

import bmesh
import bpy
from mathutils import Matrix, Vector, noise

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rack_concepts as rc  # noqa: E402

_driftwood = rc.driftwood


def chunky_driftwood(paths, top_z):
    """Same skeletons, ~30% thicker, and roots end in rounded toes instead of spikes."""
    out = []
    for pts, r0, r1, mid in paths:
        going_down = Vector(pts[-1]).z < Vector(pts[0]).z
        out.append((pts, r0 * 1.3, max(r1 * 1.3, 0.034) if going_down else r1 * 1.3, mid))
    return _driftwood(out, top_z)


rc.driftwood = chunky_driftwood


def wide_cradle(pos):
    """Cradle with a wider neck, so the thicker branch end stays hidden inside it."""
    P = Vector(pos)
    prof = [(0.0, -0.075, 0, 0, 0), (0.052, -0.075, 0, 0, 0), (0.062, -0.045, 0, 0, 0), (0.082, -0.012, 0, 0, 0),
            (0.09, 0.004, 0, 0, 0), (0.074, 0.011, 0, 0, 0), (0.045, 0.0, 0, 0, 0), (0.0, -0.004, 0, 0, 0)]

    def fn(b):
        pl = cg.make_place(Matrix.Translation(P), 0.06, seed=int(P.z * 100), wob=0.08, freq=2.5)
        cg.lathe(b, prof, 24, pl, lambda part, h, q: dict(col=cg.ramp(rc.WOOD, 0.9)))
    rc.built(fn, "Cradle")


rc.cradle = wide_cradle

cg = rc.cg
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = argv[0] if argv else "D:/blender-5.2.3/projects/coral_growth/bases"  # keep work files out of Assets
ONLY = argv[1] if len(argv) > 1 else ""
os.makedirs(OUT, exist_ok=True)

MOSS_DARK = (0.05, 0.10, 0.03, 1)
MOSS_LIGHT = (0.17, 0.26, 0.07, 1)
ROPE = (0.10, 0.26, 0.09, 1)


# ---------------------------------------------------------------- material

def relic_material(name, base_rgb=None, base_light=None, moss_up=(-0.15, 0.7), moss_amount=0.25):
    """Weathered surface with moss on the upward faces.
    base_rgb None -> use the vertex colour 'Col' (the wood gradient)."""
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    N, L = nt.nodes, nt.links
    bsdf = N["Principled BSDF"]

    tex = N.new("ShaderNodeTexCoord").outputs["Object"]
    geo = N.new("ShaderNodeNewGeometry")

    def noise_tex(scale, detail=4.0, rough=0.55, vec=tex):
        n = N.new("ShaderNodeTexNoise")
        n.inputs["Scale"].default_value = scale
        n.inputs["Detail"].default_value = detail
        n.inputs["Roughness"].default_value = rough
        L.new(vec, n.inputs["Vector"])
        return n.outputs["Fac"]

    def mrange(val, a, b, lo=0.0, hi=1.0, smooth=True):
        r = N.new("ShaderNodeMapRange")
        if smooth:
            r.interpolation_type = "SMOOTHSTEP"
        L.new(val, r.inputs["Value"])
        r.inputs["From Min"].default_value = a
        r.inputs["From Max"].default_value = b
        r.inputs["To Min"].default_value = lo
        r.inputs["To Max"].default_value = hi
        return r.outputs["Result"]

    def math_op(op, a, b):
        n = N.new("ShaderNodeMath")
        n.operation = op
        n.use_clamp = True
        for i, v in enumerate((a, b)):
            if isinstance(v, float):
                n.inputs[i].default_value = v
            else:
                L.new(v, n.inputs[i])
        return n.outputs[0]

    def mix_rgb(fac, a, b, blend="MIX"):
        n = N.new("ShaderNodeMix")
        n.data_type = "RGBA"
        n.blend_type = blend
        for sock, v in ((n.inputs["Factor"], fac), (n.inputs[6], a), (n.inputs[7], b)):
            if isinstance(v, (tuple, list)):
                sock.default_value = v
            elif isinstance(v, float):
                sock.default_value = v
            else:
                L.new(v, sock)
        return n.outputs[2]

    # where moss grows: facing up, broken into blotches, a little creeping down the sides
    sep = N.new("ShaderNodeSeparateXYZ")
    L.new(geo.outputs["Normal"], sep.inputs["Vector"])
    up = mrange(sep.outputs["Z"], moss_up[0], moss_up[1])
    blotch = mrange(noise_tex(2.2, 5.0, 0.6), 0.38, 0.62)
    edge_break = mrange(noise_tex(14.0, 3.0), 0.3, 0.7, -0.25, 0.25, smooth=False)
    moss = mrange(math_op("ADD", math_op("MULTIPLY", up, math_op("ADD", blotch, moss_amount)), edge_break), 0.32, 0.58)

    # crevices darker, exposed rims worn lighter (AO works in EEVEE too)
    ao = N.new("ShaderNodeAmbientOcclusion")
    ao.inputs["Distance"].default_value = 0.08
    cavity = ao.outputs["AO"]

    if base_rgb is None:
        a = N.new("ShaderNodeAttribute")
        a.attribute_name = "Col"
        base = a.outputs["Color"]
    else:
        base = mix_rgb(mrange(noise_tex(6.0, 4.0), 0.3, 0.7), base_rgb, base_light or base_rgb)
    worn = mix_rgb(0.5, base, (0.55, 0.5, 0.42, 1), "SCREEN")
    worn = mix_rgb(math_op("MULTIPLY", mrange(noise_tex(9.0, 3.0), 0.55, 0.75), 0.3), base, worn)
    dirty = mix_rgb(mrange(cavity, 0.2, 0.8, 1.0, 0.0), worn, (0.03, 0.03, 0.02, 1), "MULTIPLY")
    dirty = mix_rgb(0.35, worn, dirty)

    moss_col = mix_rgb(mrange(noise_tex(38.0, 2.0), 0.35, 0.7), MOSS_DARK, MOSS_LIGHT)
    moss_col = mix_rgb(mrange(cavity, 0.4, 1.0), (0.04, 0.07, 0.02, 1), moss_col)
    col = mix_rgb(moss, dirty, moss_col)
    L.new(col, bsdf.inputs["Base Color"])

    bsdf.inputs["Roughness"].default_value = 0.85
    L.new(math_op("MULTIPLY", moss, 0.8), bsdf.inputs["Sheen Weight"])
    bsdf.inputs["Sheen Roughness"].default_value = 0.6
    bsdf.inputs["Sheen Tint"].default_value = (0.75, 0.95, 0.5, 1)

    bump = N.new("ShaderNodeBump")
    bump.inputs["Distance"].default_value = 0.004
    L.new(math_op("ADD", math_op("MULTIPLY", noise_tex(160.0, 2.0), moss), math_op("MULTIPLY", noise_tex(25.0, 6.0), 0.4)),
          bump.inputs["Height"])
    L.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    return m


def clump_material():
    m = bpy.data.materials.new("MossClump")
    m.use_nodes = True
    nt = m.node_tree
    b = nt.nodes["Principled BSDF"]
    a = nt.nodes.new("ShaderNodeAttribute")
    a.attribute_name = "Col"
    nt.links.new(a.outputs["Color"], b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = 0.9
    b.inputs["Sheen Weight"].default_value = 1.0
    b.inputs["Sheen Tint"].default_value = (0.8, 1.0, 0.5, 1)
    nz = nt.nodes.new("ShaderNodeTexNoise")
    nz.inputs["Scale"].default_value = 220.0
    bump = nt.nodes.new("ShaderNodeBump")
    bump.inputs["Distance"].default_value = 0.002
    nt.links.new(nz.outputs["Fac"], bump.inputs["Height"])
    nt.links.new(bump.outputs["Normal"], b.inputs["Normal"])
    return m


# ---------------------------------------------------------------- moss clumps

def moss_clumps(objs, count, seed=1, min_up=0.55, size=(0.006, 0.016)):
    """Puffy moss cushions scattered on the upward faces of the given objects."""
    rng = random.Random(seed)
    cands = []
    for ob in objs:
        mw = ob.matrix_world
        nm = mw.to_3x3()
        for p in ob.data.polygons:
            n = (nm @ p.normal).normalized()
            if n.z < min_up:
                continue
            c = mw @ p.center
            w = p.area * (n.z - min_up + 0.2) * max(0.0, noise.noise(c * 3.0) + 0.15) ** 2
            if w > 0:
                cands.append((w, c, n))
    if not cands:
        return None
    total = sum(c[0] for c in cands)
    acc, cum = 0.0, []
    for w, c, n in cands:
        acc += w
        cum.append(acc)
    tpl = bmesh.new()
    bmesh.ops.create_icosphere(tpl, subdivisions=2, radius=1.0)
    tv = [v.co.copy() for v in tpl.verts]
    tf = [[v.index for v in f.verts] for f in tpl.faces]
    tpl.free()
    b = cg.Builder()
    import bisect
    for i in range(count):
        k = bisect.bisect_left(cum, rng.random() * total)
        _, c, n = cands[min(k, len(cands) - 1)]
        r = rng.uniform(*size)
        frame = Matrix.Translation(c - n * r * 0.2) @ n.to_track_quat("Z", "Y").to_matrix().to_4x4() @ \
            Matrix.Rotation(rng.uniform(0, math.tau), 4, "Z")
        sx, sy, sz = r * rng.uniform(1.0, 1.3), r * rng.uniform(1.0, 1.25), r * rng.uniform(0.6, 0.9)
        shade = rng.uniform(0, 1)
        base = Vector(MOSS_DARK[:3]).lerp(Vector(MOSS_LIGHT[:3]), shade)
        o = len(b.v)
        for v in tv:
            q = Vector((v.x * sx, v.y * sy, max(v.z, -0.3) * sz))
            q *= 1 + 0.18 * noise.noise(v * 3 + Vector((i, i * 0.3, 0)))
            top = 0.75 + 0.35 * (v.z * 0.5 + 0.5)
            b.vert(frame @ q, col=base * top)
        for f in tf:
            b.face([o + j for j in f])
    ob = b.to_object("MossClumps", bpy.context.scene.collection)
    ob.data.materials.append(clump_material())
    return ob


# ---------------------------------------------------------------- rope bands

def rope_band(center, tangent, radius, turns=2, seed=0, oval=(1.0, 1.0)):
    """A few loops of twisted green rope tied around a limb."""
    t = tangent.normalized()
    q = t.to_track_quat("Z", "Y").to_matrix().to_4x4()
    for k in range(turns):
        off = Matrix.Translation(center + t * (k - (turns - 1) / 2) * 0.026)
        bpy.ops.mesh.primitive_torus_add(major_radius=radius * 1.0, minor_radius=0.011,
                                         major_segments=48, minor_segments=10)
        ob = bpy.context.active_object
        ob.matrix_world = off @ q @ Matrix.Rotation(0.08 * (k - 0.5) + seed * 0.2, 4, "X") @ Matrix.Diagonal((oval[0], oval[1], 1.0, 1.0))
        ob.data.materials.append(rc.flat_mat("Rope", ROPE, 0.8))
        bpy.ops.object.shade_smooth()


def along(pts, t):
    smp = rc.catmull(pts)
    i = int(t * (len(smp) - 2))
    return smp[i], (smp[i + 1] - smp[i])


# ---------------------------------------------------------------- stones (cairn)

def stone(center, rx, ry, h, seed, tilt=0.0):
    """Rounded, slightly squarish river stone; smooth (not low poly)."""
    bpy.ops.mesh.primitive_uv_sphere_add(segments=48, ring_count=24, radius=1.0, location=(0, 0, 0))
    ob = bpy.context.active_object
    me = ob.data
    off = Vector((seed * 1.7, seed * 0.9, seed * 2.3))
    for v in me.vertices:
        p = v.co
        # cushion shape: flatter top and bottom, rounder sides
        z = math.copysign(abs(p.z) ** 0.75, p.z)
        k = 1 + 0.12 * noise.noise(p * 1.4 + off) + 0.04 * noise.noise(p * 4 + off)
        v.co = Vector((p.x * rx * k, p.y * ry * k, z * h * 0.5 * (1 + 0.08 * noise.noise(p * 2 + off))))
    ob.location = center
    ob.rotation_euler = (tilt, tilt * 0.6, seed * 1.3)
    bpy.ops.object.shade_smooth()
    ob.modifiers.new("Sub", "SUBSURF").levels = 1
    return ob


# ---------------------------------------------------------------- bases

def strip_glow():
    for ob in list(bpy.data.objects):
        if ob.type == "MESH" and ob.data.materials and ob.data.materials[0] and ob.data.materials[0].name.startswith("Glow"):
            bpy.data.objects.remove(ob)


def wood_objects():
    return [o for o in bpy.data.objects if o.type == "MESH" and (o.name.startswith("Driftwood") or o.name.startswith("Cradle"))]


def apply_relic(objs, mat):
    for ob in objs:
        ob.data.materials.clear()
        ob.data.materials.append(mat)


def base_tree():
    center, size = rc.variant_tower(1)
    strip_glow()
    objs = wood_objects()
    apply_relic(objs, relic_material("RelicWood", moss_up=(-0.25, 0.55), moss_amount=0.28))
    trunk = [(0, 0, 0.0), (0.11, 0.0, 0.3), (-0.08, 0.04, 0.63), (0.09, -0.03, 0.98), (-0.05, 0.02, 1.3), (0.0, 0.0, 1.53)]
    for t, r in ((0.16, 0.11), (0.66, 0.075)):
        c, d = along(trunk, t)
        rope_band(c, d, r, turns=3 if t < 0.5 else 2, seed=t)
    moss_clumps([o for o in objs if o.name.startswith("Driftwood")], 1000, seed=3, min_up=0.4, size=(0.007, 0.018))
    return center, size


def base_cairn():
    stones = [
        stone((0.0, 0.0, 0.12), 0.46, 0.4, 0.26, 1, 0.03),
        stone((0.05, 0.02, 0.33), 0.33, 0.3, 0.2, 2, -0.06),
        stone((-0.02, -0.01, 0.5), 0.22, 0.2, 0.16, 3, 0.08),
        stone((0.01, 0.01, 0.62), 0.13, 0.12, 0.09, 4, -0.1),
        stone((0.42, -0.24, 0.07), 0.18, 0.15, 0.14, 5, 0.12),
        stone((-0.38, 0.26, 0.06), 0.14, 0.12, 0.12, 6, -0.15),
        stone((0.18, 0.38, 0.04), 0.09, 0.08, 0.08, 7, 0.2),
    ]
    mat = relic_material("RelicStone", (0.20, 0.13, 0.11, 1), (0.36, 0.25, 0.21, 1))
    apply_relic(stones, mat)
    # evaluate subsurf so clumps sit on the smooth surface
    dg = bpy.context.evaluated_depsgraph_get()
    tmp = []
    for s in stones:
        me = bpy.data.meshes.new_from_object(s.evaluated_get(dg))
        o = bpy.data.objects.new("tmp", me)
        o.matrix_world = s.matrix_world
        bpy.context.scene.collection.objects.link(o)
        tmp.append(o)
    moss_clumps(tmp, 1100, seed=5, min_up=0.45, size=(0.007, 0.02))
    for o in tmp:
        bpy.data.objects.remove(o)
    # a rope tied around the top two stones, like the reference relic
    rope_band(Vector((0.05, 0.02, 0.335)), Vector((0, 0, 1)), 0.325, turns=2, seed=0.0, oval=(1.0, 0.91))
    return Vector((0, 0, 0.32)), 1.55


def base_hanging():
    center, size = rc.variant_hanging(1)
    strip_glow()
    objs = wood_objects()
    apply_relic(objs, relic_material("RelicWood", moss_up=(-0.25, 0.55), moss_amount=0.28))
    for ob in bpy.data.objects:  # old buoys get a little moss too
        if ob.type == "MESH" and ob.data.materials and ob.data.materials[0] and ob.data.materials[0].name.startswith("Float"):
            ob.data.materials[0] = relic_material("RelicFloat", (0.62, 0.58, 0.42, 1), (0.8, 0.76, 0.58, 1))
    trunk = [(0, 0, 0.0), (0.06, 0.0, 0.45), (-0.05, 0.02, 0.95), (0.03, 0.0, 1.45), (0.0, 0.0, 1.8)]
    for t, r in ((0.18, 0.11), (0.6, 0.08)):
        c, d = along(trunk, t)
        rope_band(c, d, r, turns=3 if t < 0.5 else 2, seed=t)
    moss_clumps([o for o in objs if o.name.startswith("Driftwood")], 950, seed=7, min_up=0.4, size=(0.007, 0.018))
    return center, size


def soft_world(scene):
    """Hazy, bright underwater light so the moss reads soft (like the reference)."""
    scene.view_settings.view_transform = "Standard"
    for ob in scene.objects:
        if ob.type == "LIGHT" and ob.name == "Sun":
            ob.data.energy = 2.8
            ob.data.angle = math.radians(12)
        if ob.type == "LIGHT" and ob.name == "Fill":
            ob.data.energy = 0.9
    scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value = 1.0


JOBS = [
    ("base_B_tree", base_tree, [(-38, 20), (140, 16)]),
    ("base_D_cairn", base_cairn, [(-35, 24), (120, 30)]),
    ("base_E_hanging", base_hanging, [(-38, 18), (150, 14)]),
]

if __name__ == "__main__":
    for name, fn, views in JOBS:
        if ONLY and ONLY not in name:
            continue
        rc._mats.clear()
        scene = rc.reset()
        soft_world(scene)
        center, size = fn()
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, name + ".blend"))
        for i, (ang, elev) in enumerate(views):
            rc.OUT = OUT
            rc.render(f"{name}_{i + 1}", center, size, angle=ang, elev=elev)
