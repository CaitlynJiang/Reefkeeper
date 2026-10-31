"""Reefkeeper coral generator (Blender 5.2, background mode).

Builds five stylised coral species, each with 3-4 growth stages, bakes their
colours (gradient, spots, speckle) into one texture per stage and exports one
FBX per species variant for Unity.

    blender.exe -b --factory-startup --python coral_generator.py -- [options]

Options (all optional):
    --work DIR      .blend files and preview renders (default: <repo>/Blender/CoralGrowth)
    --only NAME     build only models whose name contains NAME (e.g. Tube)
    --no-bake       skip texture baking (fast shape iteration)
    --no-export     skip FBX/PNG export into the Unity project
    --preview       render preview sheets into --work

Every model is built from a fixed seed. A later stage keeps the pieces of the
earlier stages (slightly larger) and adds new ones, so swapping stages in Unity
reads as growth rather than as a different coral.
"""
import argparse
import math
import os
import random
import sys

import bpy
from mathutils import Matrix, Vector, noise

HERE = os.path.dirname(os.path.abspath(__file__))
REEF_ROOT = os.path.normpath(os.path.join(HERE, "..", "..", ".."))          # Assets/Reefkeeper
REPO_ROOT = os.path.normpath(os.path.join(REEF_ROOT, "..", "..", ".."))     # git root
MESH_DIR = os.path.join(REEF_ROOT, "Meshes", "CoralGrowth")
TEX_DIR = os.path.join(REEF_ROOT, "Textures", "CoralGrowth")

TEX_SIZE = 512


# ---------------------------------------------------------------- colours

def hexc(h):
    h = h.lstrip("#")
    srgb = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    # Blender colour attributes are linear.
    return Vector([c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4 for c in srgb])


def mix(a, b, t):
    t = max(0.0, min(1.0, t))
    return a.lerp(b, t)


def ramp(stops, t):
    """stops: [(t, colour), ...] sorted by t."""
    if t <= stops[0][0]:
        return stops[0][1].copy()
    for (t0, c0), (t1, c1) in zip(stops, stops[1:]):
        if t <= t1:
            return mix(c0, c1, (t - t0) / max(t1 - t0, 1e-6))
    return stops[-1][1].copy()


def smooth01(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


# ---------------------------------------------------------------- mesh builder

BODY, RIM, INNER, TIP, ROCK = range(5)


class Builder:
    """Collects vertices/faces and per-vertex paint for one mesh."""

    def __init__(self):
        self.v, self.f, self.smooth = [], [], []
        self.col, self.spot_col, self.dots, self.rings = [], [], [], []
        # radial stripes (disc coral): mask + position on the disc, normalised by its radius
        self.stripe, self.sx, self.sy = [], [], []
        # seaweed sway: weight (0 at the root, 1 at the tip) and per-blade phase -> exported as UV2
        self.sway_h, self.sway_p = [], []

    def vert(self, p, col, spot_col=None, dots=0.0, rings=0.0, stripe=0.0, sxy=(0.0, 0.0), sway=(0.0, 0.0)):
        self.v.append(tuple(p))
        self.col.append(col)
        self.spot_col.append(spot_col if spot_col is not None else col)
        self.dots.append(dots)
        self.rings.append(rings)
        self.stripe.append(stripe)
        self.sx.append(sxy[0])
        self.sy.append(sxy[1])
        self.sway_h.append(sway[0])
        self.sway_p.append(sway[1])
        return len(self.v) - 1

    def face(self, idx, smooth=True):
        out = []
        for i in idx:  # drop repeated corners (fans at poles)
            if i not in out:
                out.append(i)
        if len(out) >= 3:
            self.f.append(out)
            self.smooth.append(smooth)

    def to_object(self, name, collection):
        me = bpy.data.meshes.new(name)
        me.from_pydata(self.v, [], self.f)
        me.update()
        me.polygons.foreach_set("use_smooth", self.smooth)
        for attr, values in (("Col", self.col), ("SpotCol", self.spot_col)):
            ca = me.color_attributes.new(attr, "FLOAT_COLOR", "POINT")
            flat = []
            for c in values:
                flat += [c[0], c[1], c[2], 1.0]
            ca.data.foreach_set("color", flat)
        for attr, values in (("dots", self.dots), ("rings", self.rings), ("stripe", self.stripe), ("sx", self.sx),
                             ("sy", self.sy), ("sway_h", self.sway_h), ("sway_p", self.sway_p)):
            a = me.attributes.new(attr, "FLOAT", "POINT")
            a.data.foreach_set("value", values)
        ob = bpy.data.objects.new(name, me)
        collection.objects.link(ob)
        return ob


def lathe(b, profile, seg, place, paint, smooth=True, phase=0.0, squash=(1.0, 1.0)):
    """Revolve a profile around +Z.

    profile: [(r, z, part, h, jag)], walked bottom -> top -> (inside) down.
    place(p_local, theta, pt) -> world point.  paint(part, h, p_world) -> dict for Builder.vert.
    """
    rings = []
    for pt in profile:
        r, z = pt[0], pt[1]
        if r < 1e-6:
            p = place(Vector((0, 0, z)), 0.0, pt)
            rings.append([b.vert(p, **paint(pt[2], pt[3], p))])
            continue
        ring = []
        for j in range(seg):
            th = phase + 2 * math.pi * j / seg
            p = place(Vector((r * math.cos(th) * squash[0], r * math.sin(th) * squash[1], z)), th, pt)
            ring.append(b.vert(p, **paint(pt[2], pt[3], p)))
        rings.append(ring)
    for lo, hi in zip(rings, rings[1:]):
        for j in range(seg):
            a0, a1 = lo[j % len(lo)], lo[(j + 1) % len(lo)]
            b0, b1 = hi[j % len(hi)], hi[(j + 1) % len(hi)]
            b.face((a0, a1, b1, b0), smooth)


def element_frame(base, tilt, azimuth):
    """Matrix that stands an element at base, leaning by tilt toward azimuth."""
    return Matrix.Translation(base) @ Matrix.Rotation(azimuth, 4, "Z") @ Matrix.Rotation(tilt, 4, "Y")


def wobble(p, seed, amount, freq):
    if amount <= 0:
        return 1.0
    return 1.0 + amount * noise.noise(p * freq + Vector((seed * 7.31, seed * 3.17, seed * 1.93)))


def make_place(frame, height, seed, wob=0.06, freq=6.0, bend=0.0, jag_teeth=0, jag_amp=0.0):
    def place(p, th, pt):
        jag = pt[4]
        q = p.copy()
        # organic radial wobble, stronger mid-body, none at the very bottom
        k = wobble(Vector((math.cos(th), math.sin(th), q.z / max(height, 1e-3))), seed, wob, freq)
        q.x *= k
        q.y *= k
        if jag and jag_teeth:
            q.z += jag * jag_amp * (abs(math.sin(jag_teeth * th / 2)) - 0.55)
        t = q.z / max(height, 1e-3)
        q.x += bend * height * t * t
        return frame @ q
    return place


# ---------------------------------------------------------------- profiles

def tube_profile(H, R, wall, lip, depth):
    """Hollow tube with a soft rolled lip; returns (r, z, part, h, jag)."""
    return [
        (0.0, -0.02, BODY, 0.0, 0),
        (R * 0.92, -0.02, BODY, 0.0, 0),
        (R * 1.00, 0.06 * H, BODY, 0.06, 0),
        (R * 1.03, 0.30 * H, BODY, 0.30, 0),
        (R * 0.98, 0.60 * H, BODY, 0.60, 0),
        (R * 1.00, 0.85 * H, BODY, 0.85, 0),
        (R * 1.06, H - lip * 1.2, BODY, 0.97, 0),
        (R * 1.065, H - lip * 1.1, RIM, 1.0, 0),       # hard colour edge
        (R * 1.07, H - lip * 0.45, RIM, 1.0, 0),
        (R * 1.00, H, RIM, 1.0, 1),
        (R - wall * 0.5, H + lip * 0.15, RIM, 1.0, 1),
        (R - wall, H - lip * 0.2, RIM, 1.0, 1),
        (R - wall * 1.02, H - lip * 0.35, INNER, 1.0, 0),
        (R - wall * 1.05, H - depth * 0.6, INNER, 0.5, 0),
        (R * 0.45, H - depth, INNER, 0.0, 0),
        (0.0, H - depth * 1.02, INNER, 0.0, 0),
    ]


def barrel_profile(H, R, lip):
    return [
        (0.0, -0.02, BODY, 0.0, 0),
        (R * 0.55, -0.02, BODY, 0.0, 0),
        (R * 0.86, 0.08 * H, BODY, 0.08, 0),
        (R * 0.99, 0.28 * H, BODY, 0.28, 0),
        (R * 1.00, 0.48 * H, BODY, 0.48, 0),
        (R * 0.94, 0.68 * H, BODY, 0.68, 0),
        (R * 0.82, 0.84 * H, BODY, 0.84, 0),
        (R * 0.74, H - lip * 1.3, BODY, 0.95, 0),
        (R * 0.735, H - lip * 1.22, RIM, 1.0, 0),
        (R * 0.75, H - lip * 0.5, RIM, 1.0, 0.4),
        (R * 0.70, H, RIM, 1.0, 1),
        (R * 0.58, H - lip * 0.1, RIM, 1.0, 1),
        (R * 0.52, H - lip * 0.6, RIM, 1.0, 0.5),
        (R * 0.50, H - lip * 0.95, INNER, 1.0, 0),
        (R * 0.44, H - lip * 2.4, INNER, 0.6, 0),
        (R * 0.20, H - lip * 3.2, INNER, 0.2, 0),
        (0.0, H - lip * 3.3, INNER, 0.0, 0),
    ]


def finger_profile(H, R):
    return [
        (0.0, -0.02, BODY, 0.0, 0),
        (R * 1.05, -0.02, BODY, 0.0, 0),
        (R * 1.00, 0.20 * H, BODY, 0.2, 0),
        (R * 0.96, 0.50 * H, BODY, 0.5, 0),
        (R * 0.95, 0.75 * H, BODY, 0.75, 0),
        (R * 0.90, 0.88 * H, TIP, 0.88, 0),
        (R * 0.72, 0.96 * H, TIP, 0.96, 0),
        (R * 0.40, 1.00 * H, TIP, 1.0, 0),
        (0.0, 1.01 * H, TIP, 1.0, 0),
    ]


def pillar_profile(H, R):
    return [
        (0.0, -0.02, BODY, 0.0, 0),
        (R * 0.90, -0.02, BODY, 0.0, 0),
        (R * 0.98, 0.12 * H, BODY, 0.12, 0),
        (R * 1.04, 0.45 * H, BODY, 0.45, 0),
        (R * 1.02, 0.75 * H, BODY, 0.75, 0),
        (R * 0.94, 0.93 * H, BODY, 0.93, 0),
        (R * 0.78, 0.99 * H, BODY, 0.99, 0),
        (R * 0.40, 1.00 * H, BODY, 1.0, 0),
        (0.0, 1.00 * H, BODY, 1.0, 0),
    ]


def polyp_profile(L, r):
    return [
        (0.0, -0.01, TIP, 0.0, 0),
        (r * 0.70, -0.01, TIP, 0.0, 0),
        (r * 0.95, 0.35 * L, TIP, 0.35, 0),
        (r * 1.05, 0.68 * L, TIP, 0.68, 0),
        (r * 0.85, 0.90 * L, TIP, 0.9, 0),
        (r * 0.45, 0.99 * L, TIP, 0.99, 0),
        (0.0, 1.00 * L, TIP, 1.0, 0),
    ]


def rock_profile(H, R):
    return [
        (0.0, 0.0, ROCK, 0.0, 0),
        (R * 0.92, 0.0, ROCK, 0.0, 0),
        (R * 1.00, 0.45 * H, ROCK, 0.4, 0),
        (R * 0.86, 0.95 * H, ROCK, 0.9, 0),
        (R * 0.55, 1.00 * H, ROCK, 1.0, 0),
        (0.0, 1.00 * H, ROCK, 1.0, 0),
    ]


# ---------------------------------------------------------------- species

def growth(stage, appear, full_stage):
    """Size of an element that appeared at `appear`, seen at `stage`."""
    age = stage - appear
    span = max(full_stage - appear, 1)
    return 0.55 + 0.45 * smooth01(age / span)


def palette_paint(body_stops, rim=None, inner=None, tip_stops=None, spot=None, dots=0.0, rings=0.0, rock=None):
    def paint(part, h, p):
        if part == RIM:
            c = rim.copy()
        elif part == INNER:
            c = mix(inner * 0.72, inner, h)
        elif part == TIP:
            c = ramp(tip_stops, h)
        elif part == ROCK:
            c = mix(rock[0], rock[1], h)
        else:
            c = ramp(body_stops, h)
        # soft ground contact shade
        c = c * (0.82 + 0.18 * smooth01((p.z + 0.01) / 0.06))
        body = part == BODY
        return dict(col=c, spot_col=spot if spot is not None else c,
                    dots=dots if body else 0.0, rings=rings if body else 0.0)
    return paint


def build_tube(b, stage, rng, variant):
    pal = {
        "Magenta": dict(body=[(0, hexc("FF9AA6")), (0.45, hexc("FF5DA8")), (1, hexc("E44BC8"))],
                        rim=hexc("B06AF0"), inner=hexc("8E52E6"), spot=None, dots=0.0),
        "Lemon": dict(body=[(0, hexc("B8DE6C")), (0.35, hexc("EEF06A")), (1, hexc("FFF27A"))],
                      rim=hexc("CDEFA0"), inner=hexc("9CD86A"), spot=hexc("A6D460"), dots=1.0),
        "Blossom": dict(body=[(0, hexc("F9C9DB")), (0.5, hexc("FCE4EE")), (1, hexc("FFF0F6"))],
                        rim=hexc("A9A8F6"), inner=hexc("8E8CF0"), spot=hexc("F27FB4"), dots=1.0),
    }[variant]
    paint = palette_paint(pal["body"], pal["rim"], pal["inner"], spot=pal["spot"], dots=pal["dots"])
    # (appear, x, y, height, radius)
    tubes = [(1, 0.00, 0.02, 0.50, 0.060), (1, -0.09, -0.06, 0.30, 0.050), (2, 0.10, -0.05, 0.38, 0.052),
             (2, -0.12, 0.08, 0.22, 0.045), (3, 0.06, 0.12, 0.27, 0.046), (3, 0.15, 0.07, 0.15, 0.040),
             (3, -0.03, -0.14, 0.12, 0.042)]
    for i, (appear, x, y, H, R) in enumerate(tubes):
        if appear > stage:
            continue
        g = growth(stage, appear, 3)
        H, R = H * g, R * (0.75 + 0.25 * g)
        az = math.atan2(y, x) + rng.uniform(-0.3, 0.3)
        tilt = rng.uniform(0.05, 0.16) if i else rng.uniform(0.0, 0.05)
        frame = element_frame(Vector((x, y, 0)) * (0.8 + 0.2 * g), tilt, az)
        place = make_place(frame, H, seed=i + 11, wob=0.05, bend=rng.uniform(-0.04, 0.04))
        lathe(b, tube_profile(H, R, wall=R * 0.28, lip=R * 0.35, depth=min(H * 0.5, R * 3.5)), 22, place, paint,
              phase=rng.uniform(0, 1))


def build_barrel(b, stage, rng):
    paint = palette_paint([(0, hexc("8F79E6")), (0.6, hexc("A98DF0")), (1, hexc("B79CF4"))],
                          rim=hexc("FBE07A"), inner=hexc("F7C27E"), spot=hexc("C6B2FA"), rings=1.0)
    barrels = [(1, 0.00, 0.06, 0.40, 0.15), (2, -0.17, -0.06, 0.28, 0.13), (3, 0.17, -0.04, 0.30, 0.13),
               (4, 0.02, -0.16, 0.15, 0.08)]
    for i, (appear, x, y, H, R) in enumerate(barrels):
        if appear > stage:
            continue
        g = growth(stage, appear, 4)
        H, R = H * g, R * g
        az = math.atan2(y, x)
        frame = element_frame(Vector((x, y, 0)) * (0.75 + 0.25 * g), rng.uniform(0.02, 0.1), az)
        place = make_place(frame, H, seed=i + 3, wob=0.07, freq=3.0, jag_teeth=7, jag_amp=R * 0.12)
        lathe(b, barrel_profile(H, R, lip=R * 0.16), 26, place, paint, phase=rng.uniform(0, 1))


def build_finger(b, stage, rng):
    paint = palette_paint([(0, hexc("F4A84A")), (0.6, hexc("FAC75E"))],
                          tip_stops=[(0.85, hexc("FAC75E")), (1.0, hexc("FFF0C4"))])
    # rings of fingers: (appear, ring radius, count, height, lean)
    rings = [(1, 0.00, 1, 0.26, 0.00), (1, 0.05, 4, 0.22, 0.30), (2, 0.10, 6, 0.19, 0.55), (3, 0.15, 7, 0.15, 0.85)]
    k = 0
    for appear, rr, count, H0, lean in rings:
        if appear > stage:
            continue
        g = growth(stage, appear, 3)
        off = rng.uniform(0, math.tau)
        for j in range(count):
            az = off + math.tau * j / count + rng.uniform(-0.2, 0.2)
            H = H0 * g * rng.uniform(0.85, 1.1)
            R = 0.026 * (0.8 + 0.2 * g) * rng.uniform(0.9, 1.1)
            base = Vector((math.cos(az) * rr, math.sin(az) * rr, 0)) * (0.7 + 0.3 * g)
            frame = element_frame(base, lean * rng.uniform(0.8, 1.1), az)
            place = make_place(frame, H, seed=k, wob=0.06, bend=-0.08 * lean)
            lathe(b, finger_profile(H, R), 14, place, paint, phase=rng.uniform(0, 1), squash=(1.12, 0.88))
            k += 1


def build_pillar(b, stage, rng, variant):
    body = palette_paint([(0, hexc("86E3C8")), (0.5, hexc("8FD9EA")), (1, hexc("A6D8F7"))],
                         spot=hexc("B8E8F8"), rings=0.6)
    polyp_col = {"Pink": [(0, hexc("F2678F")), (1, hexc("FF9DB8"))],
                 "Coral": [(0, hexc("F0702E")), (1, hexc("FFB067"))]}[variant]
    polyp = palette_paint(None, tip_stops=polyp_col)
    H = [0.20, 0.33, 0.44][stage - 1]
    R = [0.09, 0.12, 0.14][stage - 1]
    place = make_place(Matrix.Identity(4), H, seed=5, wob=0.05, freq=4.0)
    lathe(b, pillar_profile(H, R), 11, place, body, phase=rng.uniform(0, 1))
    if stage == 1:
        return
    count = {"Pink": [0, 5, 8], "Coral": [0, 4, 5]}[variant][stage - 1]
    L = [0, 0.07, 0.10][stage - 1] * (1.0 if variant == "Pink" else 1.12)
    r = L * (0.42 if variant == "Pink" else 0.33)
    off = rng.uniform(0, math.tau)
    for j in range(count + 1):
        if j == count:
            base, tilt, az = Vector((0, 0, H * 0.99)), 0.0, 0.0
        else:
            az = off + math.tau * j / count
            base = Vector((math.cos(az) * R * 0.6, math.sin(az) * R * 0.6, H * 0.97))
            tilt = 0.7 if variant == "Pink" else 0.9
        frame = element_frame(base, tilt, az)
        pl = make_place(frame, L, seed=20 + j, wob=0.05)
        squash = (1.0, 1.0) if variant == "Pink" else (1.3, 0.75)
        lathe(b, polyp_profile(L * rng.uniform(0.9, 1.1), r), 12, pl, polyp, squash=squash)


def branch_tree(rng):
    """Skeleton nodes: (pos, radius, gen, parent). Two trunks, forking upward."""
    nodes = []

    def grow(start, parent, direction, length, radius, gen):
        steps = max(2, int(length / 0.045))
        pos, d = start, direction.normalized()
        idx = parent
        for s in range(1, steps + 1):
            # limbs spread first, then curl upward
            d = (d + Vector((0, 0, 0.17)) + Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), 0)) * 0.04).normalized()
            pos = pos + d * (length / steps)
            r = radius * (1.0 - 0.1 * s / steps)
            nodes.append((pos.copy(), r, gen, idx))
            idx = len(nodes) - 1
        if gen >= 4:
            return
        # gen0 trunk -> 3 limbs; then forks of 2; the last level only sometimes adds a short nub
        kids = {0: 3, 1: 2, 2: 2, 3: 2}[gen]
        side = Vector((-d.y, d.x, 0))
        if side.length < 1e-3:
            side = Vector((1, 0, 0))
        side.normalize()
        base_angle = rng.uniform(0, math.tau)
        for k in range(kids):
            if (gen == 2 and rng.random() < 0.12) or (gen == 3 and rng.random() < 0.6):
                continue
            axis = Matrix.Rotation(base_angle + k * math.tau / kids, 3, d) @ side
            spread = {0: (0.75, 0.95), 1: (0.5, 0.75), 2: (0.45, 0.7), 3: (0.7, 1.0)}[gen]
            nd = Matrix.Rotation(rng.uniform(*spread), 3, axis) @ d
            scale = {0: 2.3, 1: 0.8, 2: 0.75, 3: 0.45}[gen] * rng.uniform(0.9, 1.1)
            grow(pos, idx, nd, length * scale, max(radius * 0.8, 0.014), gen + 1)

    nodes.append((Vector((0, 0, 0.05)), 0.046, 0, -1))
    grow(nodes[0][0], 0, Vector((0, 0, 1)), 0.08, 0.044, 0)
    return nodes


def build_branch(b, stage, rng, collection, with_rock=True):
    # stage -> (max generation kept, overall scale)
    gen_max, scale = [(1, 0.5), (2, 0.7), (3, 0.87), (4, 1.0)][stage - 1]
    nodes = branch_tree(random.Random(42))
    keep = [i for i, n in enumerate(nodes) if n[2] <= gen_max]
    remap = {old: new for new, old in enumerate(keep)}
    verts = [tuple(nodes[i][0] * scale) for i in keep]
    edges = [(remap[nodes[i][3]], remap[i]) for i in keep if nodes[i][3] >= 0]
    me = bpy.data.meshes.new("skel")
    me.from_pydata(verts, edges, [])
    ob = bpy.data.objects.new("skel", me)
    collection.objects.link(ob)
    ob.modifiers.new("Skin", "SKIN")
    for new, old in enumerate(keep):
        r = nodes[old][1] * scale * (1.0 if stage > 1 else 1.15)
        me.skin_vertices[0].data[new].radius = (r, r)
    me.skin_vertices[0].data[0].use_root = True
    sub = ob.modifiers.new("Sub", "SUBSURF")
    sub.levels = sub.render_levels = 2
    dg = bpy.context.evaluated_depsgraph_get()
    ev = ob.evaluated_get(dg)
    em = bpy.data.meshes.new_from_object(ev)
    top = max(v.co.z for v in em.vertices) or 1.0
    paint = palette_paint([(0, hexc("EC5F2A")), (0.35, hexc("F5922E")), (0.75, hexc("FFC93F")), (1.0, hexc("FFE277"))])
    base = len(b.v)
    for v in em.vertices:
        p = v.co.copy()
        b.vert(p, **paint(BODY, (p.z - 0.04) / (top - 0.04), p))
    for poly in em.polygons:
        b.face([base + i for i in poly.vertices], True)
    bpy.data.objects.remove(ob)
    bpy.data.meshes.remove(me)
    bpy.data.meshes.remove(em)

    if not with_rock:
        return
    # low-poly stone base
    rock = palette_paint(None, rock=(hexc("7F879C"), hexc("B7BDCF")))
    stones = [(0.0, 0.0, 0.075, 0.15), (0.12, -0.05, 0.05, 0.09), (-0.11, 0.06, 0.045, 0.08)]
    rr = random.Random(9)
    for i, (x, y, H, R) in enumerate(stones[:1 + min(stage, 2)]):
        s = 0.75 + 0.25 * scale
        frame = element_frame(Vector((x * s, y * s, -0.012)), rr.uniform(0, 0.12), rr.uniform(0, math.tau))
        place = make_place(frame, H * s, seed=40 + i, wob=0.12, freq=2.0)
        lathe(b, rock_profile(H * s, R * s), 7, place, rock, smooth=False, phase=rr.uniform(0, 1))


# ---------------------------------------------------------------- models

# ---------------------------------------------------------------- disc (mushroom) table coral

DISC_BASE = [(0.0, hexc("6A4552")), (0.6, hexc("8A6264")), (1.0, hexc("A57E76"))]
DISC_MOUTH = hexc("F2AE3C")


def disc(b, centre, R, H, tilt, az, seed, ruffle=0.18, lobes=5, stripe_col=None, sockets=None):
    """A ruffled, radially striped disc on a short stem (mushroom coral). Built with lathe so the
    normals stay consistent: stem up -> underside out to the rim -> top surface back to the centre."""
    frame = element_frame(Vector(centre), tilt, az)
    inv = frame.inverted()
    thick = R * 0.2
    ph1, ph2 = seed * 1.37, seed * 2.11
    stripe_col = stripe_col or hexc("5CE3C8")

    def edge(th):
        return R * (1 + 0.07 * math.sin(3 * th + ph2) + 0.05 * noise.noise(Vector((math.cos(th) * 1.5, math.sin(th) * 1.5, seed))))

    def ztop(rn, th):
        dome = (1 - rn * rn) * R * 0.09
        ruf = ruffle * R * rn ** 3.2 * (math.sin(lobes * th + ph1) + 0.3 * math.sin((lobes + 2) * th + ph2))
        mouth = 0.016 * R * smooth01(1 - rn / 0.1)
        return H + dome + ruf + mouth

    rs = 0.045 / max(R, 1e-3) if R > 0.12 else 0.32   # stem radius, normalised by R
    stem_r = rs * R
    prof = [(0.0, -0.01, BODY, 0.0, "stem"), (stem_r * 1.45, -0.01, BODY, 0.0, "stem"),
            (stem_r * 1.05, 0.3 * H, BODY, 0.2, "stem"), (stem_r * 0.95, 0.75 * H, BODY, 0.4, "stem")]
    for k in range(9):                       # underside, out to the rim
        rn = rs + (1 - rs) * (k / 8) ** 0.9
        prof.append((rn, 0, BODY, rn, "bot"))
    prof.append((1.03, 0, RIM, 1.0, "rim"))
    for k in range(14):                      # top, back to the centre
        rn = 1 - (k / 13) ** 1.15
        prof.append((max(rn, 0.0), 0, TIP, rn, "top"))

    def place(p, th, pt):
        r, z, _, rn, code = pt
        if code == "stem":
            return frame @ Vector((r * math.cos(th), r * math.sin(th), z))
        e = edge(th)
        rr = min(rn, 1.0) * e if code != "rim" else 1.03 * e
        zz = ztop(min(rn, 1.0), th)
        if code == "bot":
            zz -= thick * (0.55 + 0.45 * (1 - rn))
        elif code == "rim":
            zz -= thick * 0.32
        return frame @ Vector((rr * math.cos(th), rr * math.sin(th), zz))

    def paint(part, rn, p):
        q = inv @ p
        th = math.atan2(q.y, q.x)
        if part == TIP:                       # top: striped, orange mouth, pale rim
            c = ramp(DISC_BASE, rn)
            if rn < 0.11:
                c = mix(DISC_MOUTH, c, smooth01((rn - 0.06) / 0.05))
            c = mix(c, hexc("CDEBDD"), smooth01((rn - 0.9) / 0.1) * 0.55)
            return dict(col=c, spot_col=mix(stripe_col, hexc("A6F4E2"), rn * 0.6),
                        stripe=smooth01((rn - 0.16) / 0.14), sxy=(rn * math.cos(th), rn * math.sin(th)))
        c = ramp(DISC_BASE, 0.25 + 0.4 * rn) * 0.72
        c = c * (0.82 + 0.18 * smooth01((p.z + 0.01) / 0.06))
        return dict(col=c)

    lathe(b, prof, 56, place, paint)
    if sockets is not None:                   # planting points on the mature top
        for rn, th in [(0.0, 0.0)] + [(0.52, ph1 + k * math.tau / 4) for k in range(4)]:
            e = edge(th)
            sockets.append(frame @ Vector((rn * e * math.cos(th), rn * e * math.sin(th), ztop(rn, th))))


def build_disc(b, stage, rng):
    b.sockets = []
    R, H, ruf = [(0.09, 0.045, 0.06), (0.17, 0.07, 0.075), (0.29, 0.095, 0.085)][stage - 1]
    disc(b, (0, 0, 0), R, H, 0.04, 0.3, seed=1, ruffle=ruf, lobes=7, sockets=b.sockets if stage == 3 else None)
    # smaller discs crowd around the main one, peeking out from under its rim
    for i, (appear, r, h, az, col) in enumerate([(2, 0.09, 0.035, 2.3, "4FD9B4"), (3, 0.13, 0.045, 4.4, "6FE6D2"),
                                                 (3, 0.08, 0.03, 0.7, "58D7C0")]):
        if appear > stage:
            continue
        g = growth(stage, appear, 3)
        d = R * 0.78 + r * g * 0.45
        disc(b, (math.cos(az) * d, math.sin(az) * d, -0.005), r * g, h * g, 0.42, az, seed=i + 3,
             ruffle=0.1, lobes=6, stripe_col=hexc(col))


# ---------------------------------------------------------------- tendril coral

TENDRIL_A = [(0.0, hexc("1C2672")), (0.35, hexc("4A4CC2")), (0.75, hexc("B08AE6")), (1.0, hexc("F6C4EE"))]
TENDRIL_B = [(0.0, hexc("1B3A7C")), (0.4, hexc("2EA2CF")), (0.8, hexc("8EE2EE")), (1.0, hexc("DBFBFF"))]


def build_tendril(b, stage, rng):
    """A bush of long, smooth tentacles: dark blue at the root, pink or aqua at the tips."""
    sc = [0.45, 0.72, 1.0][stage - 1]
    R0 = 0.11
    base = palette_paint([(0, hexc("161E5C")), (1, hexc("24348A"))])
    mound = [(0.0, -0.01, BODY, 0.0, 0), (R0 * sc, -0.01, BODY, 0.0, 0), (R0 * 0.85 * sc, 0.025, BODY, 0.5, 0),
             (R0 * 0.45 * sc, 0.045, BODY, 0.9, 0), (0.0, 0.05, BODY, 1.0, 0)]
    lathe(b, mound, 20, make_place(Matrix.Identity(4), 0.05, seed=2, wob=0.1, freq=3.0), base)
    tentacles = []
    for i in range(40):                       # same sequence every stage -> earlier tentacles stay put
        appear = 1 if i < 8 else (2 if i < 20 else 3)
        rr = R0 * 0.9 * math.sqrt(rng.random()) * (0.5 if appear == 1 else 1.0)
        tentacles.append((appear, rr, rng.uniform(0, math.tau), rng.uniform(0.32, 0.56) * (1.1 if appear == 1 else 1.0),
                          rng.uniform(0.12, 0.32), rng.random() < 0.38, rng.uniform(0.9, 1.15)))
    for i, (appear, rr, az, Hf, curl, aqua, thick) in enumerate(tentacles):
        if appear > stage:
            continue
        g = growth(stage, appear, 3)
        H = Hf * sc * (0.75 + 0.25 * g)
        r = 0.015 * thick * (0.75 + 0.25 * sc)
        lean = 0.12 + 0.85 * rr / R0
        swirl = (i * 2.399) % math.tau          # each tentacle bends a different way
        frame = element_frame(Vector((math.cos(az) * rr * sc, math.sin(az) * rr * sc, 0.02)), lean, az) @ Matrix.Rotation(swirl, 4, "Z")
        place = make_place(frame, H, seed=i + 50, wob=0.04, freq=3.0, bend=curl * (1.6 if i % 3 else -1.2))
        prof = [(0.0, 0.0, BODY, 0.0, 0), (r * 1.25, 0.0, BODY, 0.0, 0), (r * 1.05, 0.15 * H, BODY, 0.15, 0),
                (r * 0.95, 0.5 * H, BODY, 0.5, 0), (r * 0.92, 0.85 * H, BODY, 0.85, 0), (r * 0.95, 0.94 * H, BODY, 0.94, 0),
                (r * 0.85, 0.97 * H, BODY, 0.97, 0), (r * 0.62, 0.99 * H, BODY, 0.99, 0), (r * 0.32, 0.998 * H, BODY, 1.0, 0),
                (0.0, 1.0 * H, BODY, 1.0, 0)]
        lathe(b, prof, 12, place, palette_paint(TENDRIL_B if aqua else TENDRIL_A), phase=rng.uniform(0, 1))


# ---------------------------------------------------------------- seaweed (no stages; sways in Unity)

def ribbon(b, frame, H, W, stops, phase, ruffle=0.0, waves=0.03, cols=3, rows=26, taper=1.0, sway_base=0.0, sway_span=1.0,
           arch=0.0, leaf=False, cup=0.0):
    """A flat, tapering blade along local +Z (width along local Y). Double-sided in the shader.
    arch curls the blade toward local +X as it grows (fronds that bend over).
    leaf=True gives a rounded oval outline instead of a pointed blade; cup bows the sides forward."""
    grid = []
    for i in range(rows + 1):
        t = i / rows
        x = waves * math.sin(t * 5.0 + phase) * t + arch * H * t * t
        if leaf:
            w = W * math.sqrt(max(0.0, 1 - ((t - 0.55) / 0.56) ** 2)) + W * 0.12 * (1 - t)   # rounded tip, narrow stalk
        else:
            w = W * (math.sin(math.pi * min(1.0, 0.08 + t * 0.95)) ** 0.5) * (1 - t ** 6 * taper)
        row = []
        for c in range(cols):
            u = (c / (cols - 1)) * 2 - 1
            edge = abs(u)
            dx = ruffle * w * edge * math.sin(t * 38 + phase * 3 + u * 2)
            mid = 0.12 * w * (1 - edge) - cup * w * edge * edge   # slight midrib, sides bowed
            p = frame @ Vector((x + dx + mid, u * w, t * H))
            col = ramp(stops, t) * (0.85 if c == cols // 2 and cols > 2 else 1.0)
            row.append(b.vert(p, col=col, sway=(sway_base + sway_span * t, phase / math.tau)))
        grid.append(row)
    for i in range(rows):
        for c in range(cols - 1):
            b.face((grid[i][c], grid[i][c + 1], grid[i + 1][c + 1], grid[i + 1][c]))


def stem(b, frame, H, r, stops, phase, bend=0.1, sway_base=0.0, sway_span=1.0, Hmax=None):
    Hmax = Hmax or H
    pl = make_place(frame, H, seed=int(phase * 100), wob=0.0, bend=bend)
    prof = [(0, 0, BODY, 0, 0), (r, 0, BODY, 0, 0), (r * 0.8, 0.5 * H, BODY, 0.5, 0), (r * 0.5, 0.97 * H, BODY, 0.97, 0),
            (0, H, BODY, 1, 0)]

    def paint(part, h, p):
        return dict(col=ramp(stops, h), sway=(sway_base + sway_span * h, phase / math.tau))
    lathe(b, prof, 5, pl, paint)


def build_kelp(b, stage, rng):
    stops = [(0.0, hexc("244E44")), (0.4, hexc("357F66")), (0.85, hexc("6DB886")), (1.0, hexc("A6D98E"))]
    lathe(b, rock_profile(0.04, 0.07), 9, make_place(Matrix.Identity(4), 0.04, seed=3, wob=0.15, freq=2.0),
          palette_paint(None, rock=(hexc("3B3A2A"), hexc("5C5A3E"))), smooth=False)
    for i in range(8):                        # long, narrow, wavy ribbons
        az = rng.uniform(0, math.tau)
        frame = element_frame(Vector((math.cos(az) * 0.03, math.sin(az) * 0.03, 0.02)), rng.uniform(0.06, 0.3), az) \
            @ Matrix.Rotation(rng.uniform(-0.8, 0.8), 4, "Z")
        ribbon(b, frame, rng.uniform(0.7, 1.2), rng.uniform(0.032, 0.05), stops, rng.uniform(0, math.tau),
               ruffle=0.35, waves=0.09, cols=5, rows=40)


def build_seagrass(b, stage, rng):
    stops = [(0.0, hexc("1F6B6A")), (0.55, hexc("3FA88A")), (1.0, hexc("BFE27A"))]
    for i in range(34):
        a, d = rng.uniform(0, math.tau), 0.09 * math.sqrt(rng.random())
        frame = element_frame(Vector((math.cos(a) * d, math.sin(a) * d, 0)), rng.uniform(0.04, 0.35), rng.uniform(0, math.tau)) \
            @ Matrix.Rotation(rng.uniform(0, math.tau), 4, "Z")
        ribbon(b, frame, rng.uniform(0.22, 0.45), rng.uniform(0.008, 0.013), stops, rng.uniform(0, math.tau),
               waves=0.04, cols=2, rows=10)


def build_leafy(b, stage, rng):
    stem_stops = [(0, hexc("1E5B4C")), (1, hexc("2F7E62"))]
    leaf_stops = [(0, hexc("2C8A6C")), (0.7, hexc("5FC58F")), (1, hexc("9AE3A8"))]
    for i in range(6):
        a, d = rng.uniform(0, math.tau), 0.05 * math.sqrt(rng.random())
        H = rng.uniform(0.26, 0.42)
        tilt, az, bend = rng.uniform(0.05, 0.3), rng.uniform(0, math.tau), rng.uniform(-0.15, 0.15)
        frame = element_frame(Vector((math.cos(a) * d, math.sin(a) * d, 0)), tilt, az)
        ph = rng.uniform(0, math.tau)
        stem(b, frame, H, 0.0055, stem_stops, ph, bend=bend)
        for k in range(7):                    # paddle leaves alternating up the stem
            t = 0.2 + 0.75 * k / 6
            at = frame @ Vector((bend * H * t * t, 0, t * H))
            side = (k % 2) * math.pi + rng.uniform(-0.4, 0.4) + az
            lf = element_frame(at, rng.uniform(0.7, 1.1), side) @ Matrix.Rotation(math.pi / 2, 4, "Z")
            ribbon(b, lf, rng.uniform(0.045, 0.075) * (1.1 - 0.4 * t), rng.uniform(0.016, 0.024), leaf_stops, ph,
                   waves=0.0, cols=3, rows=6, taper=0.3, sway_base=t, sway_span=0.15)


def build_feather(b, stage, rng):
    stops = [(0, hexc("9C3450")), (0.6, hexc("E26A78")), (1, hexc("FFB3A6"))]
    for i in range(5):
        a, d = rng.uniform(0, math.tau), 0.04 * math.sqrt(rng.random())
        H = rng.uniform(0.3, 0.45)
        az, tilt, bend = rng.uniform(0, math.tau), rng.uniform(0.1, 0.45), rng.uniform(-0.25, 0.25)
        frame = element_frame(Vector((math.cos(a) * d, math.sin(a) * d, 0)), tilt, az)
        ph = rng.uniform(0, math.tau)
        stem(b, frame, H, 0.009, stops, ph, bend=bend)
        for k in range(11):                    # feathery side twigs, shorter toward the tip
            t = 0.18 + 0.78 * k / 10
            at = frame @ Vector((bend * H * t * t, 0, t * H))
            for side in (1, -1):
                tw = element_frame(at, 0.9 + 0.3 * rng.random(), az + side * math.pi / 2 + rng.uniform(-0.3, 0.3))
                stem(b, tw, H * 0.32 * (1.1 - t), 0.005, stops, ph, bend=0.3, sway_base=t, sway_span=0.2)


# ---------------------------------------------------------------- ground cover (terrain detail meshes)
# Small, low-poly clumps (~400 tris) meant to be painted thousands of times onto a terrain.

def build_clover(b, stage, rng):
    """Parasol clover: thin stems with soft four-lobed caps, pale peach to cream."""
    stem_stops = [(0, hexc("A88F6A")), (1, hexc("D8C49A"))]
    for i in range(8):
        a, d = rng.uniform(0, math.tau), 0.11 * math.sqrt(rng.random())
        H = rng.uniform(0.035, 0.1) * (1.2 - d * 2.5)        # short stems, taller in the middle: a soft mound
        R = rng.uniform(0.036, 0.056)                          # big overlapping caps
        tilt, az = rng.uniform(0.0, 0.35), rng.uniform(0, math.tau)
        base = Vector((math.cos(a) * d, math.sin(a) * d, 0))
        ph = rng.uniform(0, math.tau)
        bend = rng.uniform(-0.2, 0.2)
        frame = element_frame(base, tilt, az)
        stem(b, frame, H, 0.0045, stem_stops, ph, bend=bend)
        top = frame @ Vector((bend * H, 0, H))
        cap = Matrix.Translation(top) @ Matrix.Rotation(rng.uniform(-0.25, 0.25), 4, "X") @ Matrix.Rotation(rng.uniform(0, math.tau), 4, "Z")
        tint = rng.uniform(0, 1)
        centre = mix(hexc("E3A985"), hexc("EAB7A3"), tint)
        mid, edge = hexc("F1D3A8"), hexc("FBF0D6")
        sway = (1.0, ph / math.tau)
        c0 = b.vert(cap @ Vector((0, 0, R * 0.16)), col=centre, sway=sway)
        seg, rings = 14, []
        for rr, cc in ((0.55, mix(centre, mid, 0.6)), (1.0, edge)):
            ring = []
            for j in range(seg):
                th = math.tau * j / seg
                lobe = 0.7 + 0.3 * (0.5 + 0.5 * math.cos(4 * th))        # four smooth, rounded lobes
                r = R * rr * lobe
                z = R * (0.18 * (1 - rr * rr) - 0.1 * rr ** 2 * (0.5 + 0.5 * math.cos(4 * th)))   # domed, lobes droop
                ring.append(b.vert(cap @ Vector((r * math.cos(th), r * math.sin(th), z)), col=cc, sway=sway))
            rings.append(ring)
        for j in range(seg):
            k = (j + 1) % seg
            b.face((c0, rings[0][j], rings[0][k]))
            b.face((rings[0][j], rings[1][j], rings[1][k], rings[0][k]))


def build_fuzzmoss(b, stage, rng):
    """Purple leaf tuft: only foliage, no base. Short, frilly, rounded fronds fan out from the ground
    and curl over; deep purple at the root, lilac at the tips. The fuzz rim softens the edges."""
    for i in range(16):
        a = rng.uniform(0, math.tau)
        d = 0.035 * math.sqrt(rng.random())
        inner = d < 0.018
        stops = [(0.0, hexc("3E2A70")), (0.35, mix(hexc("6E4BB8"), hexc("7A4FB0"), rng.random())),
                 (0.8, hexc("B585E6")), (1.0, mix(hexc("E2B8F5"), hexc("F0C8EC"), rng.random()))]
        frame = element_frame(Vector((math.cos(a) * d, math.sin(a) * d, -0.003)),
                              rng.uniform(0.15, 0.45) if inner else rng.uniform(0.55, 1.05), a) \
            @ Matrix.Rotation(math.pi / 2 + rng.uniform(-0.4, 0.4), 4, "Z")     # leaf face turned toward the outside
        ribbon(b, frame, rng.uniform(0.06, 0.11) * (1.1 if inner else 1.0), rng.uniform(0.022, 0.032), stops,
               rng.uniform(0, math.tau), ruffle=0.12, waves=0.006, cols=4, rows=6,
               arch=rng.uniform(0.2, 0.5), leaf=True, cup=0.35)


SEAWEEDS = [
    # name, builder, speckle  (single stage, sways in Unity; Ground_* go to the Groundcover folder)
    ("Seaweed_Kelp", build_kelp, 0.05),
    ("Seaweed_Grass", build_seagrass, 0.04),
    ("Seaweed_Leafy", build_leafy, 0.05),
    ("Seaweed_Feather", build_feather, 0.05),
    ("Ground_Clover", build_clover, 0.04),
    ("Ground_FuzzMoss", build_fuzzmoss, 0.08),
]
SINGLE_FOLDERS = {"Seaweed": "Seaweed", "Ground": "Groundcover"}
TEX_SIZES_SINGLE = {"Ground_Clover": 256, "Ground_FuzzMoss": 512}
SINGLE_SPOT = {}                              # fleck density per model (cells per metre)

STRIPES = {"DiscCoral": 64}
TEX_SIZES = {"DiscCoral": 1024}   # fine stripes need more pixels

MODELS = [
    # name, stages, builder, speckle, spot scale
    ("DiscCoral", 3, lambda b, s, r, c: build_disc(b, s, r), 0.05, 18.0),
    ("TendrilCoral", 3, lambda b, s, r, c: build_tendril(b, s, r), 0.04, 30.0),
    ("BarrelSponge", 4, lambda b, s, r, c: build_barrel(b, s, r), 0.10, 14.0),
    ("BranchCoral", 4, lambda b, s, r, c: build_branch(b, s, r, c), 0.03, 10.0),
    ("PolypPillar_Pink", 3, lambda b, s, r, c: build_pillar(b, s, r, "Pink"), 0.08, 18.0),
    ("PolypPillar_Coral", 3, lambda b, s, r, c: build_pillar(b, s, r, "Coral"), 0.08, 18.0),
    ("FingerCoral", 3, lambda b, s, r, c: build_finger(b, s, r), 0.08, 20.0),
    ("TubeSponge_Magenta", 3, lambda b, s, r, c: build_tube(b, s, r, "Magenta"), 0.08, 22.0),
    ("TubeSponge_Lemon", 3, lambda b, s, r, c: build_tube(b, s, r, "Lemon"), 0.08, 22.0),
    ("TubeSponge_Blossom", 3, lambda b, s, r, c: build_tube(b, s, r, "Blossom"), 0.06, 20.0),
]


# ---------------------------------------------------------------- materials & bake

def paint_material(name, speckle, spot_scale, stripes=0):
    """Emission material: vertex colour + dots/rings (Voronoi) + radial stripes + speckle. Baked to a texture."""
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    N, L = nt.nodes, nt.links

    def attr(n, kind="Color"):
        a = N.new("ShaderNodeAttribute")
        a.attribute_name = n
        return a.outputs[kind]

    def math_node(op, a, bval=None, clamp=False):
        m = N.new("ShaderNodeMath")
        m.operation = op
        m.use_clamp = clamp
        if isinstance(a, float):
            m.inputs[0].default_value = a
        else:
            L.new(a, m.inputs[0])
        if bval is not None:
            if isinstance(bval, float):
                m.inputs[1].default_value = bval
            else:
                L.new(bval, m.inputs[1])
        return m.outputs[0]

    coords = N.new("ShaderNodeTexCoord").outputs["Object"]
    vor = N.new("ShaderNodeTexVoronoi")
    vor.inputs["Scale"].default_value = spot_scale
    vor.inputs["Randomness"].default_value = 0.85
    L.new(coords, vor.inputs["Vector"])
    dist = vor.outputs["Distance"]

    def band(lo, hi):
        mr = N.new("ShaderNodeMapRange")
        mr.interpolation_type = "SMOOTHSTEP"
        L.new(dist, mr.inputs["Value"])
        mr.inputs["From Min"].default_value = lo
        mr.inputs["From Max"].default_value = hi
        mr.inputs["To Min"].default_value = 1.0
        mr.inputs["To Max"].default_value = 0.0
        return mr.outputs["Result"]

    dot = math_node("MULTIPLY", band(0.20, 0.235), attr("dots", "Fac"))
    ring_outer = band(0.30, 0.33)
    ring_inner = band(0.24, 0.27)
    ring = math_node("MULTIPLY", math_node("SUBTRACT", ring_outer, ring_inner, clamp=True), attr("rings", "Fac"))
    spot = math_node("MAXIMUM", dot, ring)
    if stripes:
        # radial stripes from the disc centre, slightly wavy with distance
        sx, sy = attr("sx", "Fac"), attr("sy", "Fac")
        ang = math_node("ARCTAN2", sy, sx)
        rad = math_node("SQRT", math_node("ADD", math_node("MULTIPLY", sx, sx), math_node("MULTIPLY", sy, sy)))
        wav = math_node("ADD", math_node("MULTIPLY", ang, float(stripes)), math_node("MULTIPLY", math_node("SINE", math_node("MULTIPLY", rad, 9.0)), 0.7))
        s = N.new("ShaderNodeMapRange")
        s.interpolation_type = "SMOOTHSTEP"
        L.new(math_node("SINE", wav), s.inputs["Value"])
        s.inputs["From Min"].default_value = 0.25
        s.inputs["From Max"].default_value = 0.6
        spot = math_node("MAXIMUM", spot, math_node("MULTIPLY", s.outputs["Result"], attr("stripe", "Fac")))

    mixc = N.new("ShaderNodeMix")
    mixc.data_type = "RGBA"
    L.new(spot, mixc.inputs["Factor"])
    L.new(attr("Col"), mixc.inputs[6])
    L.new(attr("SpotCol"), mixc.inputs[7])

    nz = N.new("ShaderNodeTexNoise")
    nz.inputs["Scale"].default_value = 90.0
    nz.inputs["Detail"].default_value = 3.0
    L.new(coords, nz.inputs["Vector"])
    shade = math_node("ADD", math_node("MULTIPLY", math_node("SUBTRACT", nz.outputs["Fac"], 0.5), speckle * 2), 1.0)
    # sparse light flecks, like the references
    fleck_v = N.new("ShaderNodeTexVoronoi")
    fleck_v.inputs["Scale"].default_value = 160.0
    L.new(coords, fleck_v.inputs["Vector"])
    fleck = N.new("ShaderNodeMapRange")
    L.new(fleck_v.outputs["Distance"], fleck.inputs["Value"])
    fleck.inputs["From Min"].default_value = 0.05
    fleck.inputs["From Max"].default_value = 0.12
    fleck.inputs["To Min"].default_value = speckle * 1.5
    fleck.inputs["To Max"].default_value = 0.0

    mul = N.new("ShaderNodeMix")
    mul.data_type = "RGBA"
    mul.blend_type = "MULTIPLY"
    mul.inputs["Factor"].default_value = 1.0
    L.new(mixc.outputs[2], mul.inputs[6])
    comb = N.new("ShaderNodeCombineColor")
    for k in ("Red", "Green", "Blue"):
        L.new(shade, comb.inputs[k])
    L.new(comb.outputs[0], mul.inputs[7])

    add = N.new("ShaderNodeMix")
    add.data_type = "RGBA"
    add.blend_type = "SCREEN"
    L.new(fleck.outputs["Result"], add.inputs["Factor"])
    L.new(mul.outputs[2], add.inputs[6])
    add.inputs[7].default_value = (1, 1, 1, 1)

    emit = N.new("ShaderNodeEmission")
    L.new(add.outputs[2], emit.inputs["Color"])
    out = N.new("ShaderNodeOutputMaterial")
    L.new(emit.outputs[0], out.inputs["Surface"])
    return mat


def add_sway_uv(ob):
    """Second UV channel for the sway shader: u = weight (0 root .. 1 tip), v = per-blade phase."""
    me = ob.data
    h, p = me.attributes["sway_h"].data, me.attributes["sway_p"].data
    uv = me.uv_layers.new(name="Sway")
    for loop in me.loops:
        uv.data[loop.index].uv = (h[loop.vertex_index].value, p[loop.vertex_index].value)
    me.uv_layers.active_index = 0


def unwrap(ob):
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.01)
    bpy.ops.object.mode_set(mode="OBJECT")


def bake(ob, mat, path, size=TEX_SIZE):
    img = bpy.data.images.new(os.path.basename(path), size, size, alpha=False)
    node = mat.node_tree.nodes.new("ShaderNodeTexImage")
    node.image = img
    mat.node_tree.nodes.active = node
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.bake(type="EMIT", margin=6, use_clear=True)
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    return img


def preview_material(name, img):
    mat = bpy.data.materials.new(name + "_Preview")
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    bsdf.inputs["Roughness"].default_value = 0.55
    if img is not None:
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = img
        nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    else:
        a = nt.nodes.new("ShaderNodeAttribute")
        a.attribute_name = "Col"
        nt.links.new(a.outputs["Color"], bsdf.inputs["Base Color"])
    return mat


# ---------------------------------------------------------------- preview render

def render_sheet(objects_by_model, path, stages_layout):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x, scene.render.resolution_y = 1600, 1000
    world = bpy.data.worlds.new("PreviewWorld")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.98, 0.62, 0.62, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.9
    scene.world = world
    scene.view_settings.view_transform = "Standard"
    # lay objects out: rows = models, columns = stages
    rows = list(objects_by_model.items())
    gap = 0.66
    for r, (name, obs) in enumerate(rows):
        for c, ob in enumerate(obs):
            if stages_layout:
                ob.location = (c * gap, 0, -r * gap)
            else:
                ob.location = ((r % 4) * gap, 0, -(r // 4) * 0.8)
            ob.hide_render = not stages_layout and c != len(obs) - 1
    cam_data = bpy.data.cameras.new("Cam")
    cam_data.type = "ORTHO"
    cam = bpy.data.objects.new("Cam", cam_data)
    scene.collection.objects.link(cam)
    cam.rotation_euler = (math.radians(82), 0, 0)
    if stages_layout:
        scene.render.resolution_x, scene.render.resolution_y = 1200, 1600
    # frame everything that renders (x across, z up; the camera looks almost straight along +y)
    bpy.context.view_layer.update()
    pts = [ob.matrix_world @ Vector(c) for _, obs in rows for ob in obs if not ob.hide_render for c in ob.bound_box]
    lo = Vector((min(p.x for p in pts), 0, min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), 0, max(p.z for p in pts)))
    aspect = scene.render.resolution_x / scene.render.resolution_y
    w, h = hi.x - lo.x, hi.z - lo.z
    cam_data.ortho_scale = max(w, h * aspect) * 1.12 if aspect >= 1 else max(w / aspect, h) * 1.12
    cam.location = ((lo.x + hi.x) / 2, -6, (lo.z + hi.z) / 2 + 6 * math.tan(math.radians(8)))
    scene.camera = cam
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
    sun.data.energy = 3.0
    sun.rotation_euler = (math.radians(45), math.radians(15), math.radians(30))
    scene.collection.objects.link(sun)
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    for ob in (cam, sun):
        bpy.data.objects.remove(ob)


# ---------------------------------------------------------------- main

def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument("--work", default=os.path.join(REPO_ROOT, "Blender", "CoralGrowth"))
    ap.add_argument("--only", default="")
    ap.add_argument("--no-bake", action="store_true")
    ap.add_argument("--no-export", action="store_true")
    ap.add_argument("--preview", action="store_true")
    args = ap.parse_args(argv)

    os.makedirs(args.work, exist_ok=True)
    if not args.no_export:
        os.makedirs(MESH_DIR, exist_ok=True)
        os.makedirs(TEX_DIR, exist_ok=True)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 1
    scene.cycles.device = "CPU"

    # corals: (name, stages, builder, speckle, spot scale, seaweed?)
    entries = [(n, st, bld, sp, ss, False) for n, st, bld, sp, ss in MODELS]
    entries += [(n, 1, (lambda f: lambda b, s, r, c: f(b, s, r))(bld), sp, SINGLE_SPOT.get(n, 20.0), True) for n, bld, sp in SEAWEEDS]

    built = {}
    for name, stages, builder, speckle, spot_scale, seaweed in entries:
        if args.only and args.only.lower() not in name.lower():
            continue
        sub = SINGLE_FOLDERS.get(name.split("_")[0], "Seaweed")
        mesh_dir = os.path.join(MESH_DIR, sub) if seaweed else MESH_DIR
        tex_dir = os.path.join(TEX_DIR, sub) if seaweed else TEX_DIR
        if not args.no_export:
            os.makedirs(mesh_dir, exist_ok=True)
            os.makedirs(tex_dir, exist_ok=True)
        col = bpy.data.collections.new(name)
        scene.collection.children.link(col)
        obs, extras = [], []
        for s in range(1, stages + 1):
            b = Builder()
            # same seed for every stage, so earlier pieces keep their place
            builder(b, s, random.Random(sum(map(ord, name))), col)
            obj_name = name if seaweed else f"{name}_S{s}"
            ob = b.to_object(obj_name, col)
            mat = paint_material(obj_name, speckle, spot_scale, STRIPES.get(name, 0))
            ob.data.materials.append(mat)
            img = None
            if not args.no_bake:
                unwrap(ob)
                tex_path = os.path.join(tex_dir if not args.no_export else args.work, f"{obj_name}.png")
                img = bake(ob, mat, tex_path, TEX_SIZES.get(name, TEX_SIZES_SINGLE.get(name, TEX_SIZE)))
            if seaweed:
                add_sway_uv(ob)
            ob.data.materials.clear()
            ob.data.materials.append(preview_material(obj_name, img))
            for k, p in enumerate(getattr(b, "sockets", [])):   # planting points, child of this stage
                e = bpy.data.objects.new(f"Socket_Table_{k:02d}", None)
                e.empty_display_size = 0.03
                col.objects.link(e)
                e.parent = ob
                e.location = p
                extras.append(e)
            obs.append(ob)
            print(f"[coral] {name} stage {s}: {len(ob.data.vertices)} verts, {len(ob.data.polygons)} faces"
                  + (f", {len(getattr(b, 'sockets', []))} sockets" if getattr(b, "sockets", None) else ""))
        built[name] = obs
        if not args.no_export:
            bpy.ops.object.select_all(action="DESELECT")
            for ob in obs + extras:
                ob.select_set(True)
            bpy.context.view_layer.objects.active = obs[0]
            bpy.ops.export_scene.fbx(
                filepath=os.path.join(mesh_dir, f"{name}.fbx"), use_selection=True, object_types={"MESH", "EMPTY"},
                apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True,
                axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE", use_mesh_modifiers=True,
                add_leaf_bones=False, bake_anim=False, path_mode="STRIP", colors_type="NONE")

    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(args.work, "coral_growth.blend"))
    if args.preview and built:
        render_sheet(built, os.path.join(args.work, "preview_stages.png"), True)
        render_sheet(built, os.path.join(args.work, "preview_lineup.png"), False)
    print("[coral] done:", ", ".join(built))


if __name__ == "__main__":
    main()
