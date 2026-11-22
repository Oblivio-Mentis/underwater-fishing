import bpy,json
from mathutils import Vector
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
            verts.extend([(-p.x,p.z,-p.y)for p in (o.matrix_world@v.co for v in o.data.vertices)])
    lo=[min(p[i]for p in verts)for i in range(3)];hi=[max(p[i]for p in verts)for i in range(3)]
    expected=metadata['models'][name]
    assert max(abs(lo[k]-expected['unity_bounds_min'][k])for k in range(3))<.001,(name,lo,expected)
    assert max(abs(hi[k]-expected['unity_bounds_max'][k])for k in range(3))<.001,(name,hi,expected)
    result[name]={'unity_metre_bounds_min':lo,'unity_metre_bounds_max':hi,'root_scale2_world_bounds_min':[v*metadata['scene_root_scale']for v in lo],'root_scale2_world_bounds_max':[v*metadata['scene_root_scale']for v in hi],'imported_meshes':sum(o.type=='MESH'for o in bpy.data.objects)}
    # Reimported Blender coordinates remain right-handed. These rays explicitly
    # simulate the X conversion observed in the live Unity default importer.
    rays={}
    bpy.context.view_layer.update()
    for side in (-1,1):
        expected_origin=(side*5.4,.5,-14)
        origin=Vector((-expected_origin[0],-expected_origin[2],expected_origin[1]))
        direction=Vector((-side,0,0))
        hit,position,normal,index,obj,matrix=bpy.context.scene.ray_cast(bpy.context.evaluated_depsgraph_get(),origin,direction,distance=3)
        rays['starboard'if side>0 else'port']={'expected_unity_origin':expected_origin,'expected_unity_direction':[side,0,0],'hit':hit,'mesh':obj.name if obj else None,'expected_unity_hit':[-position.x,position.z,-position.y]if hit else None,'expected_unity_normal':[-normal.x,normal.z,-normal.y]if hit else None,'triangle_or_polygon_index':index}
    assert not rays['starboard']['hit'],(name,'STARBOARD hatch obstructed',rays)
    if name=='Interior':
        assert rays['port']['hit'],('PORT side should remain a sealed wall',rays)
        assert 'Nereid_Hull'in rays['port']['mesh'],rays
        assert abs(rays['port']['expected_unity_hit'][0]+5.86)<.035,rays
        assert rays['port']['expected_unity_normal'][0]>.99,rays
    result[name]['asymmetric_hatch_rays']=rays
    heights=(.25,.5,.62,1,1.75);depths=(-15,-14.5,-14,-13.5,-13);clear_count=0
    for height in heights:
        for z in depths:
            origin=Vector((-5.4,-z,height))
            hit,position,normal,index,obj,matrix=bpy.context.scene.ray_cast(bpy.context.evaluated_depsgraph_get(),origin,Vector((-1,0,0)),distance=3)
            assert not hit,(name,'Exit aperture/catwalk clearance',height,z,obj.name if obj else None)
            clear_count+=1
    result[name]['starboard_exit_clearance_grid']={'expected_unity_x_start':5.4,'expected_unity_x_end':8.4,'local_heights':heights,'local_depths':depths,'clear_rays':clear_count,'total_rays':len(heights)*len(depths)}
    tested=[]
    if name=='Interior':
        for x in (-1.13,-.5,0,.5,1.13):
            for height in (.35,.77,1.22,1.87,2.4):
                tested.append(((x,height,-15.45),(0,0,-1),1.21))
    else:
        for x in (-3.26,-2.95,-2.58,-2.26,-1.91):
            for height in (.36,.62,.92):
                start=(x,height,-16.95)
                tested.append((start,(0,4.85,-7.90),(4.85**2+7.90**2)**.5))
    for origin,axis,distance in tested:
        x,y,z=origin
        dx,dy,dz=axis
        hit,_,_,_,obj,_=bpy.context.scene.ray_cast(bpy.context.evaluated_depsgraph_get(),
            Vector((-x,-z,y)),Vector((-dx,-dz,dy)).normalized(),distance=distance)
        assert not hit,(name,'Machinery/bridge walk clearance',origin,obj.name if obj else None)
    result[name]['machinery_bridge_clear_rays']=len(tested)
print('ROUNDTRIP_VALIDATION',json.dumps(result))
(root/'Assets/_Game/Nereid/Art/Nereid_ExportCheck.json').write_text(json.dumps({'method':'Blender FBX export/reimport; expected Unity coordinates include the default importer X handedness conversion observed by root; ray tests prove the asymmetric hatch in that coordinate convention. Final live Unity verification remains with root.','result':result},indent=2)+'\n',encoding='utf-8')
