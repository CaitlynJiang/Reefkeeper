# Run with Blender: blender -b --factory-startup --python build_blend.py
import bpy
from pathlib import Path
folder=Path(__file__).resolve().parent
source=folder.parent/'Currency/Scallop/scallop.blend'
bpy.ops.wm.read_factory_settings(use_empty=True)
with bpy.data.libraries.load(str(source),link=False) as (src,dst):
    dst.objects=[n for n in src.objects if n.endswith('_Shell')]
templates=list(dst.objects)
original=next((m for o in templates for m in o.modifiers if m.type=='SOLIDIFY' and m.use_flip_normals),None)
if original is None:
    raise RuntimeError('Inspect the scallop outline modifier before exporting.')
ink=next(m for m in original.id_data.data.materials if m and 'outline' in m.name.lower())
for index,name in enumerate(('Yellow','Orange','Teal')):
    bpy.ops.import_scene.gltf(filepath=str(folder/(name+'.glb')))
    objects=list(bpy.context.selected_objects)
    for obj in objects:
        if obj.type!='MESH': continue
        obj.name=name+'_'+obj.name
        obj.location.x+=(index-1)*.22
        # Weld GLB primitive seams before the shell is expanded.
        import bmesh
        bm=bmesh.new(); bm.from_mesh(obj.data)
        bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000001)
        bm.to_mesh(obj.data);bm.free()
        obj.data.materials.append(ink)
        mod=obj.modifiers.new(original.name,'SOLIDIFY')
        mod.use_flip_normals=original.use_flip_normals
        mod.use_rim=original.use_rim
        mod.offset=original.offset
        mod.thickness=.0004 if original.thickness>0 else -.0004
        mod.material_offset=len(obj.data.materials)-1
bpy.ops.wm.save_as_mainfile(filepath=str(folder/'VinylTrio.blend'))
