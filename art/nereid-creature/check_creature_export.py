import bpy,json
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'Assets/_Game/Nereid/Art/Creature'
source=json.loads((OUT/'Nereid_BowCreature.json').read_text(encoding='utf-8'))
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(OUT/'Nereid_BowCreature.fbx'),use_anim=False)
verts=[];tris=0;names=[]
for o in bpy.data.objects:
    assert o.type=='MESH',o.type
    o.data.calc_loop_triangles();tris+=len(o.data.loop_triangles);names.append(o.name)
    verts.extend((p.x,p.z,-p.y)for p in(o.matrix_world@v.co for v in o.data.vertices))
low=[min(p[k]for p in verts)for k in range(3)];high=[max(p[k]for p in verts)for k in range(3)]
for a,b in zip(low,source['unity_bounds_min']):assert abs(a-b)<.02,(low,source)
for a,b in zip(high,source['unity_bounds_max']):assert abs(a-b)<.02,(high,source)
assert tris<100000 and len(names)<=6,(tris,names)
dg=bpy.context.evaluated_depsgraph_get()
trees=[BVHTree.FromObject(o,dg)for o in bpy.data.objects if o.type=='MESH']
clearance=[]
for x,y in [(0,3.5),(-4,3.5),(4,3.5),(0,.9),(0,6.1),(-2.8,1.8),(2.8,1.8),(-2.8,5.2),(2.8,5.2)]:
    hits=[]
    for tree in trees:
        p,n,index,distance=tree.ray_cast(Vector((x,-15.6,y)),Vector((0,-1,0)),20)
        if p is not None:hits.append(-p.y)
    closest=min(hits)if hits else None
    assert closest is None or closest>19.0,(x,y,closest)
    clearance.append({'unity_ray_start':[x,y,15.6],'first_surface_z':closest})
result={'method':'FBX exported and reimported in headless Blender; transformed back into Unity world coordinates','passed':True,'triangles':tris,'mesh_count':len(names),'unity_bounds_min':low,'unity_bounds_max':high,'mesh_names':names,'feeding_corridor_9_ray_check':clearance}
(OUT/'Nereid_BowCreature_ExportCheck.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print('CREATURE_ROUNDTRIP',json.dumps(result))
