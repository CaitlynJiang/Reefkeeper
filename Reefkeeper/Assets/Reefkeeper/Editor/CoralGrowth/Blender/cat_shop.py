"""Cat merchant shop: design A (wooden cart stall, barrel + empty basket on the left, empty push
cart on the right, jellyfish lantern, coral on the posts) under the big striped scallop roof of
design B. Chunky, rounded, sticker-like shapes. Plus the round calico cat merchant: big wide head
with cheek fluff, tiny robed body, curled tail. Every part of the cat is its own object with its
pivot where it bends (head, ears, arms, tail) so Unity can animate it procedurally, and every
expression is a separate face mesh (Face_*) that the game switches on and off.

blender.exe -b --factory-startup --python cat_shop.py -- [OUT_DIR] [only_view]
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
OUT = argv[0] if argv else "D:/blender-5.2.3/projects/coral_growth/cat_shop"   # work files stay out of Assets
ONLY = argv[1] if len(argv) > 1 else ""
os.makedirs(OUT, exist_ok=True)

C = cg.hexc
# shop palette: warm cartoon wood, teal cloth, coral + cream roof
WOOD = [(0.0, C("6E432C")), (0.5, C("8B5A3C")), (1.0, C("A8714B"))]
WOOD_DARK = C("5A3626")
CLOTH, CLOTH_DARK = C("3F7F8C"), C("2F6370")
CORAL, CREAM, SHELL_PINK = C("EE7A6C"), C("F6E7CF"), C("F4B6A8")
BASKET, BASKET_DARK = C("D9A866"), C("A87A3E")
IRON = C("4A3A32")
LANTERN = C("D7C6F5")
# cat palette (reference photo: dilute calico)
GREY, GREY_DARK = C("7D8290"), C("5F6472")
CREAMF, PEACH, WHITE = C("F3D3B0"), C("E9A673"), C("FBF3EA")
PINK, EYE, INK = C("E79A9A"), C("A9BE5E"), C("2E2622")
APRON, BELT, KELP = C("3F8C8F"), C("5B84C4"), C("5BA36B")
MOUTH, TONGUE, BLUSH = C("9E3A44"), C("F08C8C"), C("F2A0A0")

SHOP_PARTS, CAT_PARTS = [], []


# ---------------------------------------------------------------- object helpers

def link(ob):
    bpy.context.scene.collection.objects.link(ob)
    return ob


def apply_mods(ob):
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
    ob.modifiers.clear()
    old = ob.data
    ob.data = me
    if old.users == 0:
        bpy.data.meshes.remove(old)


def paint(ob, col, parts=None, smooth=True):
    """Bake modifiers, then write the 'Col' attribute: a colour, or fn(world_pos, world_normal)."""
    apply_mods(ob)
    me = ob.data
    for a in list(me.color_attributes):
        if a.name == "Col":
            me.color_attributes.remove(a)
    ca = me.color_attributes.new("Col", "FLOAT_COLOR", "POINT")
    mw, nw = ob.matrix_world, ob.matrix_world.to_3x3()
    flat = []
    for v in me.vertices:
        c = col(mw @ v.co, (nw @ v.normal).normalized()) if callable(col) else col
        flat += [c[0], c[1], c[2], 1.0]
    ca.data.foreach_set("color", flat)
    me.polygons.foreach_set("use_smooth", [smooth] * len(me.polygons))
    (SHOP_PARTS if parts is None else parts).append(ob)
    return ob


def keep(ob, parts=None):
    """For Builder meshes that already carry Col: bake modifiers and register."""
    apply_mods(ob)
    ob.data.polygons.foreach_set("use_smooth", [True] * len(ob.data.polygons))
    (SHOP_PARTS if parts is None else parts).append(ob)
    return ob


def soften(ob, width=0.015, seg=3):
    m = ob.modifiers.new("Soft", "BEVEL")
    m.width, m.segments, m.limit_method = width, seg, "ANGLE"
    return ob


def rbox(center, size, bevel=0.018, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=center, rotation=rot)
    ob = bpy.context.active_object
    ob.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return soften(ob, bevel)


def cyl(a, b, r, r2=None, verts=20, bevel=0.01):
    a, b = Vector(a), Vector(b)
    d = b - a
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r, radius2=r if r2 is None else r2, depth=d.length,
                                    location=(a + b) / 2)
    ob = bpy.context.active_object
    ob.rotation_euler = d.to_track_quat("Z", "Y").to_euler()
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    return soften(ob, bevel, 2) if bevel else ob


def ball(center, r, scale=(1, 1, 1), seg=32, rings=16):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=rings, radius=r, location=center)
    ob = bpy.context.active_object
    ob.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return ob


def tube(pts, r, r_end=None, name="Tube", res=3, smooth_path=True):
    cu = bpy.data.curves.new(name, "CURVE")
    cu.dimensions = "3D"
    cu.bevel_depth = 1.0
    cu.bevel_resolution = res
    cu.use_fill_caps = True
    sp = cu.splines.new("NURBS" if smooth_path else "POLY")
    sp.points.add(len(pts) - 1)
    for i, p in enumerate(pts):
        t = i / max(1, len(pts) - 1)
        sp.points[i].co = (p[0], p[1], p[2], 1.0)
        sp.points[i].radius = r + ((r if r_end is None else r_end) - r) * t
    if smooth_path:
        sp.use_endpoint_u = True
        sp.order_u = min(3, len(pts))
    cu.resolution_u = 6
    tmp = link(bpy.data.objects.new(name + "_c", cu))
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(tmp.evaluated_get(dg))
    bpy.data.objects.remove(tmp)
    bpy.data.curves.remove(cu)
    return link(bpy.data.objects.new(name, me))


def wood(seed=0, dark=0.0, grain_axis="z"):
    def f(p, n):
        q = Vector((p.x * 9, p.y * 9, p.z * 2.5)) if grain_axis == "z" else Vector((p.x * 2.5, p.y * 9, p.z * 9))
        g = 0.5 + 0.5 * noise.noise(q + Vector((seed * 3.1, seed, 0)))
        return cg.ramp(WOOD, 0.25 + 0.6 * g) * (0.92 + 0.08 * noise.noise(p * 3)) * (1 - dark)
    return f


def lathe_obj(profile, seg, center, col_fn, name, squash=(1, 1)):
    """profile: [(r, z)] bottom->top(->inside). col_fn(index_in_profile, p_world) -> colour."""
    b = cg.Builder()
    rings = []
    for k, (r, z) in enumerate(profile):
        ring = []
        for j in range(seg):
            th = 2 * math.pi * j / seg
            p = Vector(center) + Vector((r * math.cos(th) * squash[0], r * math.sin(th) * squash[1], z))
            ring.append(b.vert(p, col=col_fn(k, p, th)))
        rings.append(ring)
    for lo, hi in zip(rings, rings[1:]):
        for j in range(seg):
            b.face((lo[j], lo[(j + 1) % seg], hi[(j + 1) % seg], hi[j]))
    return b.to_object(name, bpy.context.scene.collection)


# ---------------------------------------------------------------- the shop

ROOF_HINGE = Vector((0.0, 0.78, 2.62))
ROOF_R, ROOF_SPAN, ROOF_RIBS = 1.62, 1.12, 11


def roof_point(theta, v):
    """Scallop roof: a fan of ribs from the hinge at the back, sloping down to the front edge."""
    edge = 1.0 + 0.035 * abs(math.cos(ROOF_RIBS * theta / ROOF_SPAN * math.pi / 2))   # wavy rim
    r = ROOF_R * v * edge
    x, f = r * math.sin(theta), r * math.cos(theta)
    y = ROOF_HINGE.y - f * 0.86
    z = ROOF_HINGE.z - f * 0.42 + 0.16 * math.sin(math.pi * min(v, 1.0)) * math.cos(theta) - 0.32 * math.sin(theta) ** 2 * v
    rib = math.cos(ROOF_RIBS * theta / ROOF_SPAN * math.pi)                                # corrugation
    z += 0.035 * rib * v
    return Vector((x, y, z)), rib


def roof_height(x, y):
    """Underside height of the roof above (x, y), for sizing the posts."""
    best = None
    for i in range(121):
        th = -ROOF_SPAN + 2 * ROOF_SPAN * i / 120
        for j in range(61):
            p, _ = roof_point(th, j / 60)
            d = (p.x - x) ** 2 + (p.y - y) ** 2
            if best is None or d < best[0]:
                best = (d, p.z)
    return best[1]


def build_roof():
    b = cg.Builder()
    nu, nv = 132, 26
    grid = []
    for i in range(nu + 1):
        th = -ROOF_SPAN + 2 * ROOF_SPAN * i / nu
        row = []
        for j in range(nv + 1):
            v = 0.04 + 0.96 * j / nv
            p, rib = roof_point(th, v)
            stripe = math.floor((th + ROOF_SPAN) / (2 * ROOF_SPAN) * ROOF_RIBS) % 2      # one stripe per rib
            col = CORAL if stripe else CREAM
            col = col * (0.9 + 0.1 * rib)
            if v > 0.93:
                col = SHELL_PINK if stripe else C("F2D3B8")                           # pale shell lip
            row.append(b.vert(p, col=col))
        grid.append(row)
    for i in range(nu):
        for j in range(nv):
            b.face((grid[i][j], grid[i + 1][j], grid[i + 1][j + 1], grid[i][j + 1]))
    ob = b.to_object("Shop_Roof", bpy.context.scene.collection)
    s = ob.modifiers.new("Thick", "SOLIDIFY")
    s.thickness, s.offset = 0.045, -1.0
    keep(ob)
    # hinge knuckle at the back
    paint(soften(rbox(ROOF_HINGE + Vector((0, 0.06, -0.01)), (0.42, 0.14, 0.12), 0.05)), C("E39A86"))


def wheel(center, r, axis="y", width=0.09):
    rot = (math.pi / 2, 0, 0) if axis == "y" else (0, math.pi / 2, 0)
    bpy.ops.mesh.primitive_torus_add(major_radius=r - 0.035, minor_radius=0.038, major_segments=48,
                                     minor_segments=12, location=center, rotation=rot)
    rim = bpy.context.active_object
    rim.scale = (1, 1, width / 0.076)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    paint(rim, wood(5, 0.05))
    c = Vector(center)
    for k in range(8):
        a = k * math.pi / 4
        d = Vector((math.cos(a), 0, math.sin(a))) if axis == "y" else Vector((0, math.cos(a), math.sin(a)))
        paint(cyl(c + d * 0.04, c + d * (r - 0.05), 0.016, 0.013, 10, 0), wood(6, 0.1))
    ax = Vector((0, 1, 0)) if axis == "y" else Vector((1, 0, 0))
    paint(cyl(c - ax * width * 0.75, c + ax * width * 0.75, 0.05, None, 20, 0.012), C("B9B2A4"))


def coral_sprig(base, direction, length, seed, col=CORAL):
    """Little branching coral (pink tube tree) growing off a post."""
    rng = random.Random(seed)
    d = Vector(direction).normalized()
    pts = [Vector(base)]
    for k in range(3):
        pts.append(pts[-1] + (d + Vector((rng.uniform(-.3, .3), rng.uniform(-.3, .3), 0.5))).normalized() * length / 3)
    paint(tube(pts, 0.034, 0.026, "Coral"), col)
    paint(ball(pts[-1], 0.03, (1, 1, 1), 16, 8), col * 1.15)               # chunky rounded tips
    for k in (1, 2):
        side = (d.cross(Vector((0, 0, 1))) * (1 if k == 1 else -1) + Vector((0, 0, 0.9))).normalized()
        tip = pts[k] + side * length * 0.45
        paint(tube([pts[k], pts[k].lerp(tip, 0.5) + Vector((0, 0, 0.02)), tip], 0.028, 0.022, "Coral"), col)
        paint(ball(tip, 0.025, (1, 1, 1), 16, 8), col * 1.15)


def build_shop():
    rng = random.Random(4)
    # --- cart body: vertical planks around a box, chunky rim
    X0, X1, Y0, Y1, Z0, Z1 = -0.78, 0.78, -0.36, 0.36, 0.34, 0.86
    n = 8
    for k in range(n):
        x0 = X0 + (X1 - X0) * k / n
        w = (X1 - X0) / n
        h = rng.uniform(-0.012, 0.012)
        paint(rbox(((x0 + w / 2), Y0 - 0.012, (Z0 + Z1) / 2 + h), (w - 0.008, 0.045, Z1 - Z0 + 0.02), 0.012), wood(k))
    paint(rbox((0, 0, (Z0 + Z1) / 2), (X1 - X0 - 0.02, Y1 - Y0, Z1 - Z0)), wood(11, 0.12))
    for x in (X0 - 0.01, X1 + 0.01):
        paint(rbox((x, 0, (Z0 + Z1) / 2), (0.05, Y1 - Y0 + 0.06, Z1 - Z0 + 0.04), 0.014), wood(12))
    paint(rbox((0, Y0 - 0.02, Z0 + 0.02), (X1 - X0 + 0.08, 0.06, 0.06), 0.02), wood(13, 0.05))       # bottom rail
    # counter top
    TOP = 0.95
    paint(rbox((0, 0, TOP - 0.04), (1.82, 0.9, 0.08), 0.025), wood(14))
    # wheels on the right end, legs on the left
    for y in (Y0 - 0.1, Y1 + 0.1):
        wheel((0.42, y, 0.36), 0.36, "y")
    paint(cyl((0.42, Y0 - 0.06, 0.36), (0.42, Y1 + 0.06, 0.36), 0.035, None, 16, 0), WOOD_DARK)       # axle
    for y in (Y0 + 0.06, Y1 - 0.06):
        paint(rbox((-0.66, y, 0.18), (0.08, 0.08, 0.36), 0.02), wood(15, 0.1))

    # --- cloth: top sheet + front drape with a zigzag hem and shell tassels
    paint(rbox((0, -0.05, TOP + 0.004), (1.3, 0.78, 0.012), 0.004), CLOTH)
    b = cg.Builder()
    teeth, xa, xb = 9, -0.62, 0.62
    cols, rows = teeth * 4, 6
    grid = []
    for i in range(cols + 1):
        u = i / cols
        x = xa + (xb - xa) * u
        tip = 1 - abs((u * teeth) % 1 * 2 - 1)            # 1 at a tooth point, 0 between
        depth = 0.17 + 0.09 * tip
        row = []
        for j in range(rows + 1):
            t = j / rows
            if t < 0.25:                                    # wraps over the counter edge
                a = t / 0.25 * math.pi / 2
                p = Vector((x, -0.45 + 0.03 * (1 - math.cos(a)) - 0.03, TOP + 0.01 - 0.03 * math.sin(a)))
                p = Vector((x, -0.42 - 0.035 * math.sin(a), TOP + 0.012 - 0.035 * (1 - math.cos(a))))
            else:
                k = (t - 0.25) / 0.75
                p = Vector((x, -0.455 - 0.012 * math.sin(math.pi * k), TOP - 0.03 - depth * k))
            row.append(b.vert(p, col=CLOTH if t < 0.92 else CLOTH_DARK))
        grid.append(row)
    for i in range(cols):
        for j in range(rows):
            b.face((grid[i][j], grid[i][j + 1], grid[i + 1][j + 1], grid[i + 1][j]))
    drape = b.to_object("Shop_Cloth", bpy.context.scene.collection)
    s = drape.modifiers.new("Thick", "SOLIDIFY")
    s.thickness = 0.01
    keep(drape)
    for k in range(teeth):
        x = xa + (xb - xa) * (k + 0.5) / teeth
        paint(ball((x, -0.47, TOP - 0.03 - 0.26 - 0.02), 0.022, (1, 0.8, 1.15), 16, 8), CREAM)

    # --- posts (front pair + one at the back holding the roof hinge), wrapped with coral
    posts = [(-0.74, -0.33), (0.74, -0.33)]
    for (x, y) in posts:
        top = roof_height(x, y) + 0.02
        paint(cyl((x, y, TOP - 0.02), (x, y, top), 0.045, 0.04, 16, 0.012), wood(int(x * 10) + 20))
        for z in (TOP + 0.25, top - 0.25):                                         # rope lashings
            paint(cyl((x, y, z - 0.025), (x, y, z + 0.025), 0.055, None, 16, 0.01), C("C9A46A"))
    paint(cyl((0, 0.86, 0.0), (0, 0.86, ROOF_HINGE.z - 0.04), 0.05, 0.045, 16, 0.012), wood(30))
    coral_sprig((-0.78, -0.33, 1.32), (-1, -0.3, 0.2), 0.26, 1)
    coral_sprig((0.78, -0.33, 1.58), (1, -0.3, 0.2), 0.24, 2)
    coral_sprig((-0.74, -0.37, 1.05), (-0.6, -1, 0.1), 0.16, 3, C("F6A15A"))

    # --- counter props: menu board with coral icons, potted anemone, little starfish
    board = rbox((-0.58, -0.16, TOP + 0.17), (0.28, 0.04, 0.3), 0.015, rot=(math.radians(-12), 0, math.radians(8)))
    paint(board, WOOD_DARK)
    face = rbox((-0.58, -0.185, TOP + 0.17), (0.23, 0.01, 0.25), 0.004, rot=(math.radians(-12), 0, math.radians(8)))
    paint(face, CREAM)
    mrot = Matrix.Rotation(math.radians(8), 4, "Z") @ Matrix.Rotation(math.radians(-12), 4, "X")
    for k, colr in enumerate((CORAL, C("B48AD8"), C("F6C35A"), C("7CC6B0"), C("F4B6A8"), C("8FC0E8"))):
        lx, lz = (k % 2 - 0.5) * 0.1, (1 - k // 2) * 0.072
        p = Vector((-0.58, -0.2, TOP + 0.17)) + (mrot @ Vector((lx, 0, lz)))
        paint(ball(p, 0.024, (1, 0.45, 1), 16, 8), colr)
    pot = lathe_obj([(0.0, 0.0), (0.07, 0.0), (0.085, 0.09), (0.095, 0.1), (0.08, 0.105), (0.0, 0.09)], 20,
                    (0.56, -0.12, TOP), lambda k, p, th: C("C9705A") if k < 4 else C("5A3A2A"), "Pot")
    keep(soften(pot, 0.005, 2))
    for k in range(9):
        a = k * math.tau / 9
        base = Vector((0.56 + 0.04 * math.cos(a), -0.12 + 0.04 * math.sin(a), TOP + 0.095))
        tip = base + Vector((0.07 * math.cos(a), 0.07 * math.sin(a), 0.13 + 0.03 * math.sin(k * 2.1)))
        paint(tube([base, base.lerp(tip, 0.5) + Vector((0, 0, 0.03)), tip], 0.014, 0.01, "Anemone"),
              lambda p, nrm: C("E58FC4").lerp(C("F7C6E2"), max(0.0, min(1.0, (p.z - TOP - 0.1) / 0.12))))
    star = cg.Builder()
    ctr = Vector((0.18, -0.475, 0.66))
    ring = []
    for k in range(10):
        a = math.pi / 2 + k * math.pi / 5
        r = 0.075 if k % 2 == 0 else 0.032
        ring.append(star.vert(ctr + Vector((r * math.cos(a), -0.012, r * math.sin(a))), col=C("F6A15A")))
    back = [star.vert(ctr + Vector((0, 0.01, 0)), col=C("E08A40"))]
    front = star.vert(ctr + Vector((0, -0.03, 0)), col=C("F6A15A"))
    for k in range(10):
        star.face((ring[k], ring[(k + 1) % 10], front))
        star.face((ring[(k + 1) % 10], ring[k], back[0]))
    keep(star.to_object("Starfish", bpy.context.scene.collection))

    # --- jellyfish lantern hanging from the roof's left front
    lx, ly = -1.0, -0.42
    hang = roof_height(lx, ly)
    paint(cyl((lx, ly, hang - 0.32), (lx, ly, hang + 0.02), 0.006, None, 6, 0), IRON)
    dome = lathe_obj([(0.0, 0.17), (0.06, 0.165), (0.11, 0.13), (0.14, 0.07), (0.15, 0.0), (0.13, 0.01), (0.09, 0.03), (0.0, 0.04)],
                     28, (lx, ly, hang - 0.5), lambda k, p, th: LANTERN if k < 5 else C("F6E8B8"), "Lantern")
    keep(dome)
    for k in range(6):
        a = k * math.tau / 6
        base = Vector((lx + 0.09 * math.cos(a), ly + 0.09 * math.sin(a), hang - 0.5))
        paint(tube([base, base + Vector((0.02 * math.cos(a + 1), 0.02 * math.sin(a + 1), -0.08)),
                    base + Vector((-0.015 * math.cos(a), -0.015 * math.sin(a), -0.17))], 0.012, 0.006, "Tentacle"),
              C("B49AE6"))

    # --- left: barrel with an EMPTY woven basket on top (items from the shop go here)
    bx, by = -1.2, -0.12
    barrel = [(0.0, 0.0), (0.24, 0.0), (0.29, 0.2), (0.3, 0.36), (0.29, 0.52), (0.24, 0.72), (0.22, 0.72), (0.0, 0.72)]
    keep(lathe_obj(barrel, 28, (bx, by, 0), lambda k, p, th: wood(int(th * 5))(p, None) * (0.82 if int(th / math.tau * 14) % 2 else 1.0), "Barrel"))
    for z, r in ((0.14, 0.278), (0.6, 0.272)):
        bpy.ops.mesh.primitive_torus_add(major_radius=r, minor_radius=0.018, location=(bx, by, z))
        paint(bpy.context.active_object, IRON)
    weave = lambda k, p, th: (BASKET if (int(th / math.tau * 28) + int(p.z * 40)) % 2 else BASKET_DARK * 1.2) if k < 5 else BASKET_DARK * 0.7
    basket = [(0.0, 0.0), (0.2, 0.0), (0.26, 0.08), (0.3, 0.17), (0.31, 0.19), (0.29, 0.19), (0.28, 0.17), (0.18, 0.03), (0.0, 0.03)]
    keep(soften(lathe_obj(basket, 36, (bx, by, 0.72), weave, "Basket", squash=(1.15, 0.9)), 0.004, 2))
    hb = [Vector((bx - 0.33, by, 0.9)), Vector((bx - 0.2, by, 1.12)), Vector((bx + 0.2, by, 1.12)), Vector((bx + 0.33, by, 0.9))]
    paint(tube(hb, 0.02, None, "Handle"), BASKET_DARK)
    sb = link(bpy.data.objects.new("Socket_Basket", None))
    sb.location = (bx, by, 0.76)

    # --- right: EMPTY wooden push cart for the baby corals
    cx, cy = 1.42, -0.1
    for (dx, dy, sx, sy) in ((0, -0.24, 0.62, 0.04), (0, 0.24, 0.62, 0.04), (-0.29, 0, 0.04, 0.5), (0.29, 0, 0.04, 0.5)):
        for z in (0.48, 0.6):
            paint(rbox((cx + dx, cy + dy, z), (sx, sy, 0.11), 0.014), wood(int(z * 100) + int(dx * 10)))
    paint(rbox((cx, cy, 0.42), (0.6, 0.5, 0.04), 0.012), wood(40, 0.15))
    wheel((cx - 0.22, cy - 0.3, 0.2), 0.2, "y", 0.07)
    wheel((cx - 0.22, cy + 0.3, 0.2), 0.2, "y", 0.07)
    for dy in (-0.2, 0.2):
        paint(cyl((cx + 0.25, cy + dy, 0.42), (cx + 0.27, cy + dy, 0.0), 0.022, None, 10, 0.006), wood(41))
        paint(cyl((cx + 0.2, cy + dy, 0.62), (cx + 0.55, cy + dy * 1.15, 0.86), 0.022, 0.018, 10, 0.006), wood(42))
    paint(cyl((cx - 0.25, cy - 0.27, 0.66), (cx - 0.25, cy - 0.27, 1.0), 0.016, None, 10, 0), wood(43))
    paint(rbox((cx - 0.25, cy - 0.29, 1.03), (0.2, 0.025, 0.12), 0.01), CREAM)
    coral_sprig((cx - 0.25, cy - 0.31, 1.0), (0, -0.1, 1), 0.07, 9)
    sc = link(bpy.data.objects.new("Socket_Cart", None))
    sc.location = (cx, cy, 0.45)

    # --- crate the cat stands on, behind the counter
    paint(rbox((0, 0.6, 0.34), (0.4, 0.3, 0.68), 0.02), wood(50, 0.1))
    anchor = link(bpy.data.objects.new("Cat_Anchor", None))
    anchor.location = (0, 0.6, 0.68)
    return anchor


# ---------------------------------------------------------------- the cat

# Lucky-cat merchant (model sheet): cream rounded-square head with an orange patch on the right,
# big rounded ears, sleepy half-lidded yellow eyes, red forehead dots, small white muzzle with a gold
# coin in the mouth, red collar with gold bells, yellow belly, ceramic-style target circles on arms
# and legs, green coin-pattern bundle on the back, short tail with orange/red bands.
FUR, FUR_SHADE = C("FFF0C8"), C("F2DFB0")
ORANGE, ORANGE_DARK = C("F5A132"), C("E07F1E")
EAR_IN, NOSE = C("E86A6A"), C("E58080")
IRIS, IRIS_DEEP = C("F7C948"), C("EFA22E")
DOT_RED, COLLAR = C("D2524E"), C("C0443F")
GOLD, GOLD_DARK = C("F6B93B"), C("D98A1A")
BELLY, TOE = C("FFD966"), C("FFE7A0")
BUNDLE, BUNDLE_DARK = C("5DAF8C"), C("3F8E6E")
TARGET = (C("FFD34D"), C("F5A132"), C("D2524E"))
INK = C("4A2E2A")
MOUTH, TONGUE = C("9E3A44"), C("F08C8C")

HEAD_C = Vector((0.0, 0.0, 0.545))
HA, HB, HC = 0.235, 0.165, 0.155              # head half-width (top), depth, height
_head_bvh = None


def head_surface(x, z, off=0.0):
    """Point and normal on the front of the head for face coordinates (x right, z up, metres)."""
    o = HEAD_C + Vector((x, -1.0, z))
    hit = _head_bvh.ray_cast(o, Vector((0, 1, 0)), 2.0) if _head_bvh else (None,)
    if hit[0] is None:
        p, n = HEAD_C + Vector((x, -HB, z)), Vector((0, -1, 0))
    else:
        p, n = hit[0], hit[1].normalized()
        if n.y > 0:
            n = -n
    return p + n * off, n


def face_patch(b, poly, col, off):
    """Flat decal hugging the head: polygon in face coordinates, filled with rings toward its centre."""
    cx = sum(p[0] for p in poly) / len(poly)
    cz = sum(p[1] for p in poly) / len(poly)
    centre = b.vert(head_surface(cx, cz, off)[0], col=col)
    rings = []
    for s in (0.34, 0.67, 1.0):
        rings.append([b.vert(head_surface(cx + (x - cx) * s, cz + (z - cz) * s, off)[0], col=col) for (x, z) in poly])
    m = len(poly)
    for k in range(m):
        b.face((centre, rings[0][k], rings[0][(k + 1) % m]))
    for lo, hi in zip(rings, rings[1:]):
        for k in range(m):
            b.face((lo[k], hi[k], hi[(k + 1) % m], lo[(k + 1) % m]))


def ellipse(cx, cz, rx, rz, n=24):
    return [(cx + rx * math.cos(k * math.tau / n), cz + rz * math.sin(k * math.tau / n)) for k in range(n)]


def cut(poly, z, above=True):
    """Keep the part of a convex polygon above (or below) a horizontal line z."""
    out = []
    m = len(poly)
    for k in range(m):
        a, c = poly[k], poly[(k + 1) % m]
        ina = a[1] >= z if above else a[1] <= z
        inc = c[1] >= z if above else c[1] <= z
        if ina:
            out.append(a)
        if ina != inc:
            t = (z - a[1]) / (c[1] - a[1])
            out.append((a[0] + (c[0] - a[0]) * t, z))
    return out


def face_line(pts2d, r, off=0.006, col=INK):
    pts = [head_surface(x, z, off)[0] for (x, z) in pts2d]
    return paint(tube(pts, r, None, "Line", res=2), col, CAT_PARTS)


EYES = (-0.075, 0.075)
EYE_Z = -0.012


def eye_sleepy(b, ex, cx, lid=0.004):
    """The sheet's eyes: wide, lid straight across, a yellow half-moon below."""
    face_patch(b, cut(ellipse(cx, EYE_Z, 0.048, 0.034, 40), EYE_Z + lid + 0.003, False), INK, 0.003)
    face_patch(b, cut(ellipse(cx, EYE_Z, 0.043, 0.029, 40), EYE_Z + lid, False), IRIS, 0.005)
    face_patch(b, cut(ellipse(cx, EYE_Z - 0.006, 0.032, 0.017, 32), EYE_Z - 0.006, False), IRIS_DEEP, 0.007)
    s = 1 if cx > 0 else -1
    ex.append(face_line([(cx - s * 0.052, EYE_Z + lid + 0.001), (cx, EYE_Z + lid + 0.004), (cx + s * 0.055, EYE_Z + lid - 0.004)], 0.0055, 0.01))


def eye_round(b, cx, rx=0.04, rz=0.042, pupil=0.017):
    face_patch(b, ellipse(cx, EYE_Z, rx + 0.005, rz + 0.005), INK, 0.003)
    face_patch(b, ellipse(cx, EYE_Z, rx, rz), IRIS, 0.005)
    face_patch(b, ellipse(cx, EYE_Z - 0.006, rx * 0.75, rz * 0.45), IRIS_DEEP, 0.006)
    face_patch(b, ellipse(cx, EYE_Z, pupil, pupil * 1.15, 16), INK, 0.008)
    face_patch(b, ellipse(cx + 0.012, EYE_Z + 0.014, 0.007, 0.007, 12), FUR, 0.010)


def coin(ex):
    """The koban held in the mouth."""
    p, n = head_surface(0.0, -0.098, 0.006)
    rot = Vector((0, -1, 0)).rotation_difference(n).to_euler()
    c = rbox(p, (0.08, 0.026, 0.05), 0.013, rot=tuple(rot))
    ex.append(paint(c, GOLD, CAT_PARTS))
    for dz in (0.008, -0.008):
        q, _ = head_surface(0.0, -0.098 + dz, 0.02)
        ex.append(paint(rbox(q, (0.05, 0.006, 0.004), 0.0015, rot=tuple(rot)), GOLD_DARK, CAT_PARTS))


def build_faces(parts):
    """One mesh per expression. Lines are tubes, filled shapes are decals."""
    faces = {}

    def new(name):
        return name, cg.Builder(), []

    def done(name, b, extra):
        ob = keep(b.to_object(name, bpy.context.scene.collection), CAT_PARTS) if b.v else None
        objs = ([ob] if ob else []) + extra
        CAT_PARTS[:] = [o for o in CAT_PARTS if o not in objs]
        out = join(objs, name)
        parts.append(out)
        faces[name] = out

    def open_mouth(b, depth=0.042, fang=False):
        m = [(0.03 * math.cos(math.pi - k * math.pi / 14), -0.083 - depth * math.sin(math.pi - k * math.pi / 14)) for k in range(15)]
        face_patch(b, m[::-1], MOUTH, 0.004)
        face_patch(b, ellipse(0.0, -0.083 - depth * 0.68, 0.016, depth * 0.24, 16), TONGUE, 0.006)
        if fang:
            face_patch(b, [(0.008, -0.084), (0.02, -0.084), (0.014, -0.096)], C("FFFFFF"), 0.007)

    n, b, ex = new("Face_Idle")                    # the sheet: sleepy eyes, coin in the mouth
    for cx in EYES:
        eye_sleepy(b, ex, cx)
    coin(ex)
    done(n, b, ex)

    n, b, ex = new("Face_Blink")
    for cx in EYES:
        s = 1 if cx > 0 else -1
        ex.append(face_line([(cx - s * 0.045, EYE_Z + 0.002), (cx, EYE_Z - 0.01), (cx + s * 0.047, EYE_Z)], 0.0058))
    coin(ex)
    done(n, b, ex)

    n, b, ex = new("Face_Happy")
    for cx in EYES:
        ex.append(face_line([(cx - 0.04, EYE_Z - 0.012), (cx, EYE_Z + 0.016), (cx + 0.04, EYE_Z - 0.012)], 0.0065))
    open_mouth(b)
    done(n, b, ex)

    n, b, ex = new("Face_Surprised")
    for cx in EYES:
        eye_round(b, cx)
    face_patch(b, ellipse(0.0, -0.1, 0.013, 0.017, 16), MOUTH, 0.004)
    done(n, b, ex)

    n, b, ex = new("Face_Wink")
    eye_round(b, EYES[0], 0.037, 0.038, 0.015)
    cx = EYES[1]
    ex.append(face_line([(cx + 0.035, EYE_Z + 0.018), (cx - 0.015, EYE_Z), (cx + 0.035, EYE_Z - 0.018)], 0.0062, 0.006))
    open_mouth(b, 0.034, fang=True)
    done(n, b, ex)

    n, b, ex = new("Face_Sleepy")
    for cx in EYES:
        s = 1 if cx > 0 else -1
        ex.append(face_line([(cx - s * 0.045, EYE_Z - 0.006), (cx, EYE_Z - 0.012), (cx + s * 0.047, EYE_Z - 0.004)], 0.0058))
    coin(ex)
    p, _ = head_surface(0.045, -0.06, 0.024)
    ex.append(paint(ball(p, 0.024, (1, 1, 1), 16, 8), C("D9F1F7"), CAT_PARTS))
    done(n, b, ex)

    n, b, ex = new("Face_Pout")
    for cx in EYES:
        eye_sleepy(b, ex, cx, lid=-0.008)
    coin(ex)
    face_patch(b, [(0.17 + 0.012 * math.cos(a), 0.06 + 0.016 * math.sin(a) - (0.012 if math.sin(a) > 0.7 else 0)) for a in [k * math.tau / 16 for k in range(16)]], C("9FD3EA"), 0.006)
    for cx in EYES:                                # cross little frown marks above the dots
        s = 1 if cx > 0 else -1
        ex.append(face_line([(cx + s * 0.035, EYE_Z + 0.045), (cx - s * 0.012, EYE_Z + 0.03)], 0.005, 0.008))
    done(n, b, ex)
    return faces


def head_paint(p, n):
    """Cream head, orange patch over the top right (front and back) running into the right ear."""
    q = p - HEAD_C
    sm = cg.smooth01
    w = 0.012 * noise.noise(q * 12)
    edge = q.z - (0.075 - 0.55 * (q.x - 0.05)) - w
    patch = sm(edge / 0.012) * sm((q.x + 0.02 - w) / 0.012)
    col = FUR.lerp(FUR_SHADE, 0.35 * sm((-q.z - 0.1) / 0.06))
    return col.lerp(ORANGE, patch)


def pivot(ob, p):
    """Move an object's origin to p (where it bends) without moving its geometry."""
    p = Vector(p)
    ob.data.transform(Matrix.Translation(-p) @ ob.matrix_world)
    ob.matrix_world = Matrix.Translation(p)
    return ob


def join(objs, name):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    if len(objs) > 1:
        bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    ob.name = ob.data.name = name
    return ob


def rounded_box(center, half, p=2.6, widen=0.0, seg=64, rings=40, taper_top=0.0):
    """Super-ellipsoid ("rounded cube") from a UV sphere. widen: wider toward the bottom."""
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=rings, radius=1, location=(0, 0, 0))
    ob = bpy.context.active_object
    for v in ob.data.vertices:
        d = v.co.normalized()
        r = (abs(d.x) ** p + abs(d.y) ** p + abs(d.z) ** p) ** (-1.0 / p)
        q = d * r
        k = 1 + widen * (-q.z) - taper_top * max(0.0, q.z)
        v.co = Vector((q.x * half[0] * k, q.y * half[1] * (1 + 0.5 * widen * (-q.z)), q.z * half[2])) + Vector(center)
    ob.data.update()
    return ob


def bvh_of(ob):
    from mathutils.bvhtree import BVHTree
    return BVHTree.FromObject(ob, bpy.context.evaluated_depsgraph_get())


def target_decal(ob, origin, direction, scale=1.0, parts=None):
    """Ceramic-style concentric circles (yellow, orange, red) stuck on a body part."""
    bvh = bvh_of(ob)
    hit = bvh.ray_cast(Vector(origin), Vector(direction).normalized(), 2.0)
    if hit[0] is None:
        return []
    p = ob.matrix_world @ hit[0]
    nrm = (ob.matrix_world.to_3x3() @ hit[1]).normalized()
    out = []
    for i, (r, col) in enumerate(zip((0.03, 0.02, 0.009), TARGET)):
        bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=r * scale, depth=0.004, location=p + nrm * (0.002 + 0.0025 * i))
        d = bpy.context.active_object
        d.rotation_euler = nrm.to_track_quat("Z", "Y").to_euler()
        out.append(paint(soften(d, 0.0015, 1), col, parts if parts is not None else CAT_PARTS))
    return out


def ear(sx):
    """Big rounded triangle ear: a beveled prism, pink inside."""
    def prism(w, h, t, col, y0):
        b = cg.Builder()
        pts = [(-w, 0.0), (w, 0.0), (0.18 * sx * w, h)]
        f = [b.vert(Vector((x, y0 - t, z)), col=col) for (x, z) in pts]
        k = [b.vert(Vector((x, y0 + t, z)), col=col) for (x, z) in pts]
        b.face(f[::-1])
        b.face(k)
        for i in range(3):
            j = (i + 1) % 3
            b.face((f[i], f[j], k[j], k[i]))
        ob = b.to_object("Ear", bpy.context.scene.collection)
        m = ob.modifiers.new("Round", "BEVEL")
        m.width, m.segments, m.limit_method = min(w, h) * 0.32, 5, "NONE"
        ob.modifiers.new("Sub", "SUBSURF").levels = 1
        return ob
    outer = paint(prism(0.098, 0.15, 0.024, FUR if sx < 0 else ORANGE, 0.0), FUR if sx < 0 else ORANGE, CAT_PARTS)
    inner = paint(prism(0.066, 0.105, 0.007, EAR_IN, -0.022), EAR_IN, CAT_PARTS)
    inner.location = (0.0, 0.0, 0.016)
    e = join([outer, inner], "Cat_Ear" + ("L" if sx < 0 else "R"))
    base = HEAD_C + Vector((sx * 0.135, 0.01, 0.095))
    e.matrix_world = Matrix.Translation(base) @ Matrix.Rotation(-sx * 0.22, 4, "Y")
    bpy.ops.object.select_all(action="DESELECT")
    e.select_set(True)
    bpy.context.view_layer.objects.active = e
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return pivot(e, base)


def build_cat(anchor):
    global _head_bvh
    P = CAT_PARTS
    # ---- body: rounded torso, yellow belly, two sturdy legs with toes and target circles
    torso = rounded_box((0, 0, 0.27), (0.14, 0.11, 0.11), p=2.3, widen=0.08, seg=48, rings=32)
    paint(torso, lambda p, n: BELLY if n.y < -0.3 and (p.x / 0.07) ** 2 + ((p.z - 0.255) / 0.06) ** 2 < 1 else FUR, P)
    legs = []
    for sx in (-1, 1):
        leg = rounded_box((sx * 0.067, 0.0, 0.1), (0.06, 0.07, 0.1), p=2.4, seg=32, rings=24)
        paint(leg, lambda p, n: TOE if p.z < 0.022 and n.y < 0.2 else FUR, P)
        legs.append(leg)
        legs += target_decal(leg, (sx * 0.067, -1.0, 0.115), (0, 1, 0))
    # collar + two gold bells + red cord ends
    bpy.ops.mesh.primitive_torus_add(major_radius=0.112, minor_radius=0.013, major_segments=48, minor_segments=10, location=(0, 0, 0.378))
    collar = bpy.context.active_object
    collar.scale = (1.0, 0.82, 1.0)
    paint(collar, COLLAR, P)
    bells = [collar]
    for sx in (-1, 1):
        c = Vector((sx * 0.026, -0.112, 0.338))
        bells.append(paint(ball(c, 0.03, (1, 0.9, 0.95), 24, 12), lambda p, n: GOLD_DARK if abs(p.z - 0.333) < 0.004 else GOLD, P))
        bells.append(paint(ball(c + Vector((0, -0.027, -0.012)), 0.0055, (1, 0.6, 1), 10, 6), INK, P))
        bells.append(paint(tube([Vector((sx * 0.012, -0.105, 0.37)), Vector((sx * 0.03, -0.122, 0.31)), Vector((sx * 0.04, -0.118, 0.27))], 0.007, 0.006, "Cord"), COLLAR, P))
    # green furoshiki bundle on the back with a gold coin pattern, coins peeking out
    centers = []
    for i in range(11):
        yv = 1 - 2 * (i + 0.5) / 11
        rr = math.sqrt(max(0.0, 1 - yv * yv))
        a = i * 2.39996
        centers.append(Vector((rr * math.cos(a), yv, rr * math.sin(a))))
    BC, BR = Vector((0, 0.14, 0.275)), Vector((0.15, 0.085, 0.12))

    def bundle_col(p, n):
        u = Vector(((p.x - BC.x) / BR.x, (p.y - BC.y) / BR.y, (p.z - BC.z) / BR.z)).normalized()
        ang = min(math.acos(max(-1.0, min(1.0, u.dot(c)))) for c in centers)
        if ang < 0.11:
            return BUNDLE_DARK
        if ang < 0.4:
            return C("FFD34D") if ang > 0.14 else GOLD_DARK
        return BUNDLE
    bundle = paint(ball(BC, 1, tuple(BR), 160, 80), bundle_col, P)
    extra = [bundle]
    for (dx, dz, tilt) in ((-0.045, 0.385, 0.4), (0.03, 0.392, -0.3), (0.0, 0.40, 0.1)):
        bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=0.032, depth=0.012, location=(dx, 0.145, dz), rotation=(math.pi / 2 + 0.3, tilt, 0))
        extra.append(paint(soften(bpy.context.active_object, 0.003, 2), GOLD, P))
    for sx in (-1, 1):
        extra.append(paint(tube([Vector((sx * 0.09, 0.1, 0.36)), Vector((sx * 0.115, 0.03, 0.392)), Vector((sx * 0.1, -0.06, 0.385))], 0.014, 0.013, "Strap"), BUNDLE, P))
    body = join([torso] + legs + bells + extra, "Cat_Body")

    # ---- tail: short, cream with orange and red bands at the tip
    tpts = [Vector((0, 0.09, 0.13)), Vector((0, 0.15, 0.125)), Vector((0, 0.195, 0.15)), Vector((0, 0.215, 0.185))]
    tail = paint(tube(tpts, 0.03, 0.026, "Cat_Tail"),
                 lambda p, n: DOT_RED if p.z > 0.172 else (ORANGE if p.z > 0.152 and p.y > 0.18 else FUR), P)

    # ---- arms: held up in front of the chest, paws curled, target circle on the upper arm
    arms = []
    for sx in (-1, 1):
        sh = Vector((sx * 0.13, -0.01, 0.345))
        elbow = Vector((sx * 0.155, -0.045, 0.27))
        paw = Vector((sx * 0.105, -0.105, 0.305))
        a = paint(tube([sh, elbow, paw], 0.04, 0.037, "Arm"), FUR, P)
        pw = paint(ball(paw + Vector((0, -0.005, 0.005)), 0.04, (1, 0.9, 1), 24, 12), FUR, P)
        pads = [paint(ball(paw + Vector((sx * -0.012 + dx, -0.038, 0.012 + dz)), 0.007, (1, 0.6, 1), 10, 6), TOE, P)
                for (dx, dz) in ((-0.014, 0.0), (0.0, 0.008), (0.014, 0.0))]
        decal = target_decal(a, elbow + Vector((sx * 0.6, 0.0, 0.03)), (-sx, 0, 0), 0.85)
        arms.append(pivot(join([a, pw] + pads + decal, "Cat_Arm" + ("L" if sx < 0 else "R")), sh))

    # ---- head: rounded square, wider at the cheeks; muzzle, nose, forehead dots, cheek dashes
    head = rounded_box(HEAD_C, (HA, HB, HC), p=2.7, widen=0.1, taper_top=0.04)
    paint(head, head_paint, P)
    bpy.context.view_layer.update()
    _head_bvh = bvh_of(head)
    deco_objs = []
    for sx in (-1, 1):
        p, n = head_surface(sx * 0.02, -0.068, -0.004)
        deco_objs.append(paint(ball(p, 0.026, (1.0, 0.75, 0.85), 24, 12), C("FFFDF4"), P))
    p, n = head_surface(0.0, -0.048, 0.01)
    deco_objs.append(paint(ball(p, 0.014, (1.15, 0.8, 0.8), 16, 8), NOSE, P))
    deco = cg.Builder()
    for sx in (-1, 1):
        face_patch(deco, ellipse(sx * 0.058, 0.075, 0.012, 0.012, 16), DOT_RED, 0.003)
    deco_objs.append(keep(deco.to_object("Deco", bpy.context.scene.collection), P))
    for sx in (-1, 1):
        for k in range(3):
            z = -0.035 - 0.017 * k
            x0, x1 = sx * (0.168 + 0.004 * k), sx * (0.2 + 0.006 * k)
            deco_objs.append(face_line([(x0, z), ((x0 + x1) / 2, z - 0.002), (x1, z - 0.004)], 0.0042, 0.004, ORANGE))
    head = join([head] + deco_objs, "Cat_Head")
    pivot(head, (0, 0.0, 0.38))
    ears = [ear(-1), ear(1)]
    faces = build_faces(P)

    # ---- hierarchy: root at the feet -> body -> head -> ears/faces; arms + tail on the body
    root = link(bpy.data.objects.new("CatMerchant", None))
    pivot(body, (0, 0, 0.1))
    pivot(tail, tpts[0])
    for o in arms + [tail, head]:
        o.parent = body
        o.matrix_parent_inverse = body.matrix_world.inverted()
    for o in ears + list(faces.values()):
        pivot(o, head.matrix_world.translation)
        o.parent = head
        o.matrix_parent_inverse = head.matrix_world.inverted()
    body.parent = root
    body.matrix_parent_inverse = root.matrix_world.inverted()
    root.matrix_world = Matrix.Translation(anchor.location) if anchor else Matrix.Identity(4)
    for name, o in faces.items():
        o.hide_render = name != "Face_Idle"
        o.hide_viewport = name != "Face_Idle"
    return root, faces, arms


# ---------------------------------------------------------------- previews

def assign_preview_mats():
    m = rc.vc_mat()
    for ob in bpy.context.scene.objects:
        if ob.type == "MESH" and "Col" in ob.data.color_attributes:
            ob.data.materials.clear()
            ob.data.materials.append(m)


def show_face(faces, which):
    for name, o in faces.items():
        o.hide_render = name != which


def render_set(views, size=1100):
    s = bpy.context.scene
    s.render.resolution_x = s.render.resolution_y = size
    rc.OUT = OUT
    for name, (ang, elev), center, extent in views:
        if ONLY and ONLY not in name:
            continue
        rc.render(name, center, extent, angle=ang, elev=elev)


if __name__ == "__main__":
    rc._mats.clear()
    scene = rc.reset()
    bs.soft_world(scene)
    front = bpy.data.objects.new("FrontFill", bpy.data.lights.new("FrontFill", "SUN"))   # preview only: lift the shade under the roof
    front.data.energy = 1.4
    front.data.color = (1.0, 0.95, 0.9)
    front.rotation_euler = (math.radians(70), 0, math.radians(-15))
    scene.collection.objects.link(front)
    anchor = build_shop()
    build_roof()
    root, faces, arms = build_cat(anchor)
    assign_preview_mats()
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "cat_shop.blend"))
    render_set([("shop_hero", (-30, 16), Vector((0.1, 0.0, 1.25)), 3.5),
                ("shop_front", (0, 8), Vector((0.1, 0.0, 1.25)), 3.4)])
    # the cat on its own, off to the side, for the model sheet and expressions
    root.matrix_world = Matrix.Translation((5.0, 0.0, 0.0))
    bpy.context.view_layer.update()
    cat_c = Vector((5.0, 0.0, 0.42))
    render_set([("cat_turn_front", (0, 6), cat_c, 1.0), ("cat_turn_side", (-90, 6), cat_c, 1.0),
                ("cat_turn_back", (180, 6), cat_c, 1.0), ("cat_turn_34", (-35, 10), cat_c, 1.0)], 700)
    for fname in ("Face_Idle", "Face_Happy", "Face_Surprised", "Face_Wink", "Face_Sleepy", "Face_Pout"):
        if ONLY and ONLY not in fname and ONLY != "faces":
            continue
        show_face(faces, fname)
        render_set([("cat_" + fname[5:].lower(), (-10, 8), cat_c + Vector((0, 0, 0.12)), 0.62)], 600)
    show_face(faces, "Face_Idle")
    print("[catshop] done")
