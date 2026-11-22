"""Rounded, thick scallop shells (real-scallop look, simplified colour bands), stall style.
blender.exe -b --factory-startup --python build_scallop.py
Three colourways after the photo, each under its own empty (Shell_*), standing and facing -Y:
  Shell_1_White  white-pink    Shell_2_Coral  pink-coral    Shell_3_Red  deep red
"""
import math
import sys

sys.path.insert(0, r"D:\blender-5.2.3\tools")
import bpy  # noqa: E402
from toonkit import *  # noqa: E402,F403

OUT = r"D:\blender-5.2.3\projects\scallop\scallop.blend"
LINE = "#4A3328"
BG = "#EFE8D8"

RIBS = 11
SPAN = math.radians(80)    # half opening angle; outline r = cos(theta)^0.55 keeps it round
H = 0.4                   # dome height
RIB_AMP = 0.065
THICK = 0.17
HINGE_Y = -0.08

# centre, white band, middle band, rim
TIERS = [
    ("Shell_1_White", "#F3B3AE", "#FFF9F4", "#F8D6CF", "#FFFFFF", -2.0),
    ("Shell_2_Coral", "#EC8573", "#FFF4EA", "#F5B3A1", "#FFF8F0", 0.0),
    ("Shell_3_Red", "#C3424F", "#F5DDCB", "#D9666E", "#F8E9DA", 2.0),
]

reset_scene()


def crest(u):
    return 0.5 + 0.5 * math.cos(2 * math.pi * RIBS * u)


def shell_pt(u, v):
    th = -SPAN + 2 * SPAN * u
    outline = max(math.cos(th), 0.0) ** 0.55
    c = crest(u)
    rim = 1.0 + 0.022 * c                                   # gentle scallop on the rim
    r = (0.07 + 0.93 * v) * outline * rim
    side = 1 - 0.5 * abs(th / SPAN) ** 3
    dome = H * (1 - v ** 2) ** 0.6 * side
    ribs = RIB_AMP * (c - 0.5) * min(max(v - 0.2, 0.0) * 1.6, 1.0) * (0.4 + 0.6 * side)   # ribs grow with the spacing
    return (r * math.sin(th), r * math.cos(th) + HINGE_Y, dome + ribs)


def shell_mat(name, centre, band, middle, rim):
    def f(nt):
        x, y = sep(nt, obj_coord(nt))[:2]
        yy = fmath(nt, "SUBTRACT", y, HINGE_Y)
        rr = vmath(nt, "LENGTH", combine(nt, x, yy, 0.0)).outputs[1]
        th = angle_xy(nt, x, yy)
        # normalise by the round outline so bands follow the rim
        out = fmath(nt, "POWER", fmath(nt, "MAXIMUM", fmath(nt, "COSINE", th), 0.05), 0.55)
        s = fmath(nt, "DIVIDE", rr, out)
        t = fmath(nt, "DIVIDE", fmath(nt, "ADD", th, SPAN), 2 * SPAN)
        rays = fmath(nt, "MULTIPLY", stripes(nt, t, RIBS, 0.3, 0.15), 0.22)   # faint rays so the colour reads as a fan
        c = mix(nt, rays, centre, rim)
        edge = fmath(nt, "ADD", s, fmath(nt, "MULTIPLY", fmath(nt, "COSINE", fmath(nt, "MULTIPLY", t, 2 * math.pi * RIBS)), -0.03))   # rounded scallops on the colour edge
        return mix(nt, gt(nt, edge, 0.74), c, rim)     # two zones: coloured fan + pale rim
    return toon(name, f)


for name, centre, band, middle, rim_c, x in TIERS:
    root = new_obj(name)
    root.empty_display_size = 0.5
    root.location = (x, 0, 0.15)
    root.rotation_euler = (math.radians(74), 0, 0)
    tag = name.split("_", 2)[2]
    mat = shell_mat(f"{name}_Mat", centre, band, middle, rim_c)
    ear_mat = toon(f"{name}_Ear", middle)

    top = surface(f"{tag}_Shell", 136, 30, shell_pt, mat, parent=root, line=0.011)
    sol = top.modifiers.new("Thick", "SOLIDIFY")
    sol.thickness, sol.offset = THICK, -1.0
    sol.use_even_offset = True
    sub = top.modifiers.new("Round", "SUBSURF")
    sub.levels = sub.render_levels = 2            # rounds the thick rim

    for s in (-1, 1):                              # flat hinge wings
        box(f"{tag}_Ear{s}", (0.17 * s, HINGE_Y + 0.02, 0.0), (0.14, 0.075, 0.035), ear_mat,
            rot=(0, 0, 0.18 * s), bev=0.045, parent=root, line=0.01)
    ellipsoid(f"{tag}_Umbo", (0, HINGE_Y + 0.1, 0.13), (0.13, 0.14, 0.1), toon(f"{name}_Umbo", centre), parent=root, line=0.01)

finish_outlines(outline_material(LINE))

surface("Floor", 1, 1, lambda u, v: (-40 + 80 * u, -40 + 80 * v, 0), toon("Floor", "#EAD5A6"))
sun = stage(BG, world_strength=0.6, sun_energy=3.0, sun_rot=(0.85, 0.0, -0.45))
sun.data.use_shadow = False   # no soft rib shadows: clean cel bands from the rib normals only
camera((0, -7.0, 3.2), (0, 0, 0.55), lens=45, res=(1600, 800))

bpy.ops.wm.save_as_mainfile(filepath=OUT)
print("BUILT", OUT)
