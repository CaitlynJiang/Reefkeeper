"""Export the cel-shaded models (toonkit materials + inverted-hull outlines) for Unity.

    blender.exe -b --python export_toon_models.py -- stall|cat|both

- Every toon material's base colour (procedural patterns included) is baked into ONE atlas per model.
- The outline shells (solidify modifier, 'Outline' material) are kept as geometry on a second
  material, so Unity draws them with a flat outline shader.
- Final materials are named <Model>_Toon and <Model>_Outline; Unity remaps them by name.
- stall: all parts merged into one mesh (+ the sand/sea diorama base as a separate mesh).
- cat:   parts stay separate, parented to the armature bones, with every action exported as a clip.
Source .blend files are opened read-only (nothing is saved back over them).
"""
import math
import os
import sys

sys.dont_write_bytecode = True

import bmesh  # noqa: E402
import bpy  # noqa: E402

REEF = r"D:\GitHub\Reefkeeper\Reefkeeper\Assets\Reefkeeper"
MESH_DIR = os.path.join(REEF, "Meshes", "CatShop")
TEX_DIR = os.path.join(REEF, "Textures", "CatShop")
STALL = r"D:\blender-5.2.3\projects\stall\stall.blend"
CAT = r"D:\blender-5.2.3\projects\lucky_cat\lucky_cat_calico_anim.blend"
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
WHICH = argv[0] if argv else "both"


def is_outline(mat):
    return mat is not None and "Outline" in mat.name


def base_socket(mat):
    """The toon material's base colour: the A input of the MULTIPLY mix that feeds the emission."""
    nt = mat.node_tree
    em = next((n for n in nt.nodes if n.type == "EMISSION"), None)
    if em is None:
        bsdf = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
        return (bsdf.inputs["Base Color"].links[0].from_socket if bsdf and bsdf.inputs["Base Color"].is_linked
                else None), (tuple(bsdf.inputs["Base Color"].default_value) if bsdf else (0.8, 0.8, 0.8, 1)), None
    src = em.inputs["Color"]
    if src.is_linked:
        n = src.links[0].from_node
        if n.type == "MIX" and getattr(n, "blend_type", "") == "MULTIPLY":
            a = n.inputs[6]
            return (a.links[0].from_socket if a.is_linked else None), tuple(a.default_value), em
        return src.links[0].from_socket, None, em
    return None, tuple(src.default_value), em                    # glow / flat emission


def prepare_bake(mats, atlas, dummy):
    """Rewire each toon material to emit its plain base colour; give every material a bake target."""
    for m in mats:
        nt = m.node_tree
        img_node = nt.nodes.new("ShaderNodeTexImage")
        img_node.image = dummy if is_outline(m) else atlas
        nt.nodes.active = img_node
        if is_outline(m):
            continue
        sock, val, em = base_socket(m)
        out = next(n for n in nt.nodes if n.type == "OUTPUT_MATERIAL" and n.is_active_output) if any(
            n.type == "OUTPUT_MATERIAL" for n in nt.nodes) else nt.nodes.new("ShaderNodeOutputMaterial")
        e = nt.nodes.new("ShaderNodeEmission")
        if sock is not None:
            nt.links.new(sock, e.inputs["Color"])
        else:
            e.inputs["Color"].default_value = val
        e.inputs["Strength"].default_value = 1.0
        nt.links.new(e.outputs[0], out.inputs["Surface"])


def apply_all_modifiers(objs):
    dg = bpy.context.evaluated_depsgraph_get()
    for o in objs:
        me = bpy.data.meshes.new_from_object(o.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
        o.modifiers.clear()
        o.data = me


def decimate(objs, target):
    """Collapse-decimate every big part by the same ratio so the whole model lands near target tris
    (outline shells shrink with their surfaces)."""
    total = tris(objs)
    if total <= target:
        return
    ratio = target / total
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        if tris([o]) < 300:
            continue
        m = o.modifiers.new("Dec", "DECIMATE")
        m.ratio = max(0.08, ratio)
    apply_all_modifiers(objs)
    print(f"[toon] decimated {total} -> {tris(objs)} tris")


def unwrap_bake(objs, name, size):
    atlas = bpy.data.images.new(name, size, size, alpha=False)
    dummy = bpy.data.images.new(name + "_dummy", 8, 8, alpha=False)
    mats = {s.material for o in objs for s in o.material_slots if s.material}
    prepare_bake(mats, atlas, dummy)
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.hide_set(False)
        o.hide_render = False
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    # bake the parts' own scale into their meshes first: smart project sizes islands by mesh-space area,
    # so tiny scaled-down parts (eyes, whiskers) would otherwise hog the atlas
    # procedural patterns read Object coordinates; keep them in the part's ORIGINAL space by pointing
    # the texture-coordinate nodes at a helper empty that keeps the pre-apply transform
    users = {}
    for o in objs:
        for sl in o.material_slots:
            if sl.material:
                users.setdefault(sl.material, set()).add(o)
    for o in objs:
        if all(abs(v - 1.0) < 1e-4 for v in o.scale):
            continue
        for sl in o.material_slots:
            m = sl.material
            if not m or is_outline(m) or len(users[m]) > 1:
                continue
            coords = [n for n in m.node_tree.nodes if n.type == "TEX_COORD" and n.object is None]
            if coords:
                ref = bpy.data.objects.new(o.name + "_PatternSpace", None)
                bpy.context.scene.collection.objects.link(ref)
                ref.matrix_world = o.matrix_world.copy()
                for n in coords:
                    n.object = ref
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bpy.ops.object.mode_set(mode="EDIT")
    for o in objs:                                   # unwrap only the coloured faces, not the outline shells
        bm = bmesh.from_edit_mesh(o.data)
        if not bm.loops.layers.uv:
            bm.loops.layers.uv.new("UVMap")
        for f in bm.faces:
            m = o.material_slots[f.material_index].material if f.material_index < len(o.material_slots) else None
            f.select = not is_outline(m)
        bmesh.update_edit_mesh(o.data)
    bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.002)
    bpy.ops.uv.pack_islands(rotate=True, margin=0.002)
    bpy.ops.object.mode_set(mode="OBJECT")
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = 4
    bpy.ops.object.bake(type="EMIT", margin=6, use_clear=True)
    os.makedirs(TEX_DIR, exist_ok=True)
    atlas.filepath_raw = os.path.join(TEX_DIR, name + ".png")
    atlas.file_format = "PNG"
    atlas.save()
    # final, simple materials that Unity remaps by name
    toon = bpy.data.materials.new(name + "_Toon")
    toon.use_nodes = True
    t = toon.node_tree.nodes.new("ShaderNodeTexImage")
    t.image = atlas
    toon.node_tree.links.new(t.outputs["Color"], toon.node_tree.nodes["Principled BSDF"].inputs["Base Color"])
    line = bpy.data.materials.new(name + "_Outline")
    for o in objs:
        for s in o.material_slots:
            s.material = line if is_outline(s.material) else toon


def tris(objs):
    return sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in objs)


def export_fbx(objs, path, anim=False):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.hide_set(False)
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"MESH", "EMPTY", "ARMATURE"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", bake_space_transform=False,
        axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE", add_leaf_bones=False,
        use_armature_deform_only=False, bake_anim=anim, bake_anim_use_all_actions=anim,
        bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
        path_mode="STRIP", colors_type="NONE")


AWNING = ("St_Awning", "St_Valance", "St_ValanceTrim")   # the cloth that sways in Unity


def add_awning_sway(ob):
    """UV2 'Sway' for the Toon shader's cloth sway: x = weight, y = position across the width.
    The awning rests on the back beam (y 0.42) and the front beam (y -0.42): almost still between
    them, rising to 1 at the overhanging front edge (y -0.82); the valance keeps rising toward its
    scallop tips. Outline shells get the same weights because they sit on the same positions."""
    def smooth(a, b, x):
        t = max(0.0, min(1.0, (x - a) / (b - a)))
        return t * t * (3 - 2 * t)
    me = ob.data
    uv = me.uv_layers.new(name="Sway")
    weights = []
    for v in me.vertices:
        p = v.co
        if p.y < -0.79 and p.z < 2.16:                        # valance + trim hanging off the front
            w = 1.0 + 0.5 * max(0.0, min(1.0, (2.14 - p.z) / 0.32))
        else:                                                 # the striped sheet
            mid = max(0.0, min(1.0, (0.42 - p.y) / 0.84))
            w = 0.12 * math.sin(math.pi * mid) + smooth(-0.42, -0.82, p.y)
        weights.append((w, (p.x + 1.25) / 2.5))
    for loop in me.loops:
        uv.data[loop.index].uv = weights[loop.vertex_index]
    me.uv_layers.active_index = 0
    for i, layer in enumerate(me.uv_layers):
        layer.active_render = i == 0
    print(f"[toon] awning sway weights {min(w for w, _ in weights):.2f}..{max(w for w, _ in weights):.2f}")


def do_stall():
    bpy.ops.wm.open_mainfile(filepath=STALL)
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    apply_all_modifiers(meshes)
    base = [o for o in meshes if o.name in ("St_Sea", "St_Sand")]
    parts = [o for o in meshes if o not in base]
    decimate(parts, 90000)
    unwrap_bake(parts, "Stall", 2048)
    unwrap_bake(base, "Stall_Base", 256)
    awning = [o for o in parts if o.name in AWNING]
    body = [o for o in parts if o not in awning]
    out = []
    for group, name in ((body, "Stall"), (awning, "Stall_Awning"), (base, "Stall_Base")):
        if not group:
            continue
        bpy.ops.object.select_all(action="DESELECT")
        for o in group:
            o.select_set(True)
        bpy.context.view_layer.objects.active = group[0]
        bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
        bpy.ops.object.join()
        ob = bpy.context.view_layer.objects.active
        ob.name = ob.data.name = name
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        if name == "Stall_Awning":
            add_awning_sway(ob)
        out.append(ob)
    lo = min((ob.matrix_world @ v.co).z for ob in out[:1] for v in ob.data.vertices)
    print(f"[toon] stall {tris(out[:1])} tris, base {tris(out[1:])} tris, bottom z {lo:.3f}, dims {tuple(round(d, 2) for d in out[0].dimensions)}")
    # markers for Unity: where the cat stands (behind the counter), the empty basket, the empty wheelbarrow
    for name, pos in (("Cat_Anchor", (0.0, 1.0, 0.1)), ("Socket_Basket", (-1.62, -0.05, 1.02)), ("Socket_Cart", (2.15, -0.1, 0.55))):
        e = bpy.data.objects.new(name, None)
        e.location = pos
        bpy.context.scene.collection.objects.link(e)
        out.append(e)
    os.makedirs(MESH_DIR, exist_ok=True)
    export_fbx(out, os.path.join(MESH_DIR, "Stall.fbx"))


def do_cat():
    bpy.ops.wm.open_mainfile(filepath=CAT)
    arm = bpy.data.objects["Cat_Armature"]
    if arm.animation_data:
        arm.animation_data.action = None              # export the rest pose; clips come from the actions
    for pb in arm.pose.bones:
        pb.location = (0, 0, 0)
        pb.rotation_euler = (0, 0, 0)
        pb.scale = (1, 1, 1)
    bpy.context.view_layer.update()
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    apply_all_modifiers(meshes)
    # the diving helmet keeps its own materials (glass shader / flat brass) and its smooth sphere
    helmet = [o for o in meshes if any(s.material and "Helmet" in s.material.name for s in o.material_slots)]
    body = [o for o in meshes if o not in helmet]
    decimate(body, 45000)
    unwrap_bake(body, "LuckyCat", 1024)
    line = bpy.data.materials.get("LuckyCat_Outline")
    for o in helmet:
        for s in o.material_slots:
            m = s.material
            if m and "HelmetGlass" in m.name:
                m.name = "LuckyCat_Glass"
            elif m and "HelmetBrass" in m.name:
                m.name = "LuckyCat_Brass"
            elif is_outline(m) and line:
                s.material = line
    for a in bpy.data.actions:
        a.use_fake_user = True
    rig = bpy.data.objects.get("Cat_Rig")
    objs = ([rig] if rig else []) + [arm] + meshes
    print(f"[toon] cat {tris(meshes)} tris in {len(meshes)} parts, actions {[a.name for a in bpy.data.actions]}")
    os.makedirs(MESH_DIR, exist_ok=True)
    export_fbx(objs, os.path.join(MESH_DIR, "LuckyCat.fbx"), anim=True)


if __name__ == "__main__":
    if WHICH in ("stall", "both"):
        do_stall()
    if WHICH in ("cat", "both"):
        do_cat()
    print("[toon] done")
