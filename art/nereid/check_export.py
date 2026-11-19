import bpy,json
from pathlib import Path
root=Path(__file__).resolve().parents[2]
result={}
metadata=json.loads((root/'Assets/_Game/Nereid/Art/Nereid_Architecture.json').read_text(encoding='utf-8'))
for name in ('Interior','Exterior'):
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=str(root/f'Assets/_Game/Nereid/Art/Nereid_{name}.fbx'),use_anim=False)
    verts=[]
    for o in bpy.data.objects:
        if o.type=='MESH':
            verts.extend([(p.x,p.z,-p.y)for p in (o.matrix_world@v.co for v in o.data.vertices)])
    lo=[min(p[i]for p in verts)for i in range(3)];hi=[max(p[i]for p in verts)for i in range(3)]
    expected=metadata['models'][name]
    assert max(abs(lo[k]-expected['unity_bounds_min'][k])for k in range(3))<.001,(name,lo,expected)
    assert max(abs(hi[k]-expected['unity_bounds_max'][k])for k in range(3))<.001,(name,hi,expected)
    result[name]={'unity_metre_bounds_min':lo,'unity_metre_bounds_max':hi,'root_scale2_world_bounds_min':[v*metadata['scene_root_scale']for v in lo],'root_scale2_world_bounds_max':[v*metadata['scene_root_scale']for v in hi],'imported_meshes':sum(o.type=='MESH'for o in bpy.data.objects)}
print('ROUNDTRIP_VALIDATION',json.dumps(result))
(root/'Assets/_Game/Nereid/Art/Nereid_ExportCheck.json').write_text(json.dumps({'method':'Blender FBX export and reimport, transformed back to Unity coordinates; root must still verify Unity-imported bounds','result':result},indent=2)+'\n',encoding='utf-8')
