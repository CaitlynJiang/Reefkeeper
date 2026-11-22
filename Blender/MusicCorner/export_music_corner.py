"""Export the Lucky Cat counter props from D:/blender-5.2.3/projects/music_corner/music_corner.blend
(read-only on the .blend) to Reefkeeper/Assets/Reefkeeper/Meshes/MusicCorner/<root>.fbx:
MC_1_Turntable (record player), MC_2_Polaroid (instant camera), MC_3_Crate (record crate).
blender.exe -b D:/blender-5.2.3/projects/music_corner/music_corner.blend --python export_music_corner.py
Each file holds one root empty and its children, modifiers applied (outline shells keep their
"Outline" slot), textures embedded. Z-up as usual: the Unity importer bakes the axis conversion.
Unity side: Reefkeeper > Shop > Build counter props (CounterPropsBuilder.cs).
"""
import os
import bpy

OUT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Reefkeeper", "Assets",
                                    "Reefkeeper", "Meshes", "MusicCorner"))
for name in ("MC_1_Turntable", "MC_2_Polaroid", "MC_3_Crate"):
    root = bpy.data.objects[name]
    keep = {root} | set(root.children_recursive)
    for o in bpy.context.scene.objects:
        o.hide_set(False)
        o.select_set(o in keep)
    bpy.context.view_layer.objects.active = root
    path = os.path.join(OUT, name + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"EMPTY", "MESH"},
        use_mesh_modifiers=True, mesh_smooth_type="FACE", use_tspace=False,
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", global_scale=1.0,
        axis_forward="-Z", axis_up="Y", bake_space_transform=False,
        add_leaf_bones=False, bake_anim=False, path_mode="COPY", embed_textures=True)
    print("EXPORTED", path)
