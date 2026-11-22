"""NEREID expedition vessel; model units, +Z bow, +Y up.
Run Blender -b --factory-startup --python-exit-code 1 -P this_file.
Geometry, openings and materials are deterministic; no downloaded art.
"""
import bpy, bmesh, math, json, sys
from pathlib import Path
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/_Game/Nereid/Art'
OUT.mkdir(parents=True,exist_ok=True)
PREVIEW=Path(__file__).resolve().parent
sys.path.insert(0,str(PREVIEW))
INTERIOR_ONLY='--interior-only' in sys.argv
EXTERIOR_ONLY='--exterior-only' in sys.argv
if INTERIOR_ONLY and EXTERIOR_ONLY:
    raise ValueError('Select one export scope or export both.')
EXPORT_ONLY='--export-only' in sys.argv
SCALE_COMPAT='expedition-v3'
ROOT_SCALE=2.0
FBX_X_EXPORT_SIGN=-1.0

def compensated_height(y,z):
    # The scene doubles the vessel in every axis. Keep lower work-height details
    # human-sized, while the arches and crown grow into the large industrial hall.
    # Bow structure blends back to the original height to meet the monster mouth.
    k=max(0.0,min(1.0,(7.5-z)/2.0))
    adjusted=y*.5 if y<=1.0 else y-.5
    return y*(1.0-k)+adjusted*k

def to_blender(u):
    # Unity's default FBX importer (bakeAxisConversion=False) changes handedness
    # by negating model X. Precompensate in the exported geometry so asymmetric
    # starboard/port features arrive on the intended Unity side.
    return Vector((FBX_X_EXPORT_SIGN*u[0],-u[2],compensated_height(u[1],u[2])))

assert compensated_height(.87,0)*ROOT_SCALE==.87
assert abs(compensated_height(4.6,0)*ROOT_SCALE-8.2)<1e-6
assert compensated_height(1.85,7.75)==1.85
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
for d in list(bpy.data.materials):bpy.data.materials.remove(d)
PALETTE={
'Nereid_Hull':(.105,.192,.214,1),'Nereid_Steel':(.19,.25,.25,1),
'Nereid_Dark':(.025,.047,.055,1),'Nereid_Trim':(.31,.36,.35,1),
'Nereid_Ochre':(.43,.285,.11,1),'Nereid_Grate':(.07,.1,.105,1),
'Nereid_Window':(.17,.32,.34,.19),'Nereid_Lamp':(1,.58,.23,1),
'Nereid_Rubber':(.028,.034,.034,1)}
MATS={}
for name,c in PALETTE.items():
    m=bpy.data.materials.new(name);m.diffuse_color=c;m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=c
    p.inputs['Metallic'].default_value=.62 if name not in ('Nereid_Window','Nereid_Lamp','Nereid_Rubber') else .05
    p.inputs['Roughness'].default_value=.43
    if name=='Nereid_Window':
        p.inputs['Alpha'].default_value=c[3];p.inputs['Roughness'].default_value=.15
        m.surface_render_method='DITHERED'
    if name=='Nereid_Lamp':
        p.inputs['Emission Color'].default_value=c;p.inputs['Emission Strength'].default_value=3
    MATS[name]=m

PARTS={}
def add(scope,mat,verts,faces):
    v,f=PARTS.setdefault((scope,mat),([],[]));start=len(v);v.extend(verts);f.extend(tuple(start+i for i in face)for face in faces)
def box(scope,name,pos,size,mat='Nereid_Steel',basis=None):
    axes=basis or ((1,0,0),(0,1,0),(0,0,1));p=Vector(pos);a=[Vector(x)for x in axes]
    v=[p+a[0]*size[0]*x/2+a[1]*size[1]*y/2+a[2]*size[2]*z/2 for x,y,z in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]]
    add(scope,mat,v,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)])
def beam(scope,a,b,width,depth,mat='Nereid_Steel'):
    a,b=Vector(a),Vector(b);t=(b-a).normalized();normal=Vector((0,0,1))
    if abs(t.dot(normal))>.95:normal=Vector((1,0,0))
    r=t.cross(normal).normalized();n=r.cross(t).normalized()
    box(scope,'beam',(a+b)/2,(width,(b-a).length,depth),mat,(r,t,n))
def tube(scope,points,r,mat='Nereid_Trim',sides=12,cap=True):
    points=[Vector(x)for x in points];v=[];faces=[]
    for i,p in enumerate(points):
        t=(points[min(i+1,len(points)-1)]-points[max(0,i-1)]).normalized()
        a=t.cross(Vector((0,1,0)) if abs(t.y)<.9 else Vector((1,0,0))).normalized();b=t.cross(a)
        for j in range(sides):v.append(p+(a*math.cos(j*math.tau/sides)+b*math.sin(j*math.tau/sides))*r)
    for i in range(len(points)-1):
        for j in range(sides):
            a=i*sides+j;b=i*sides+(j+1)%sides;c=b+sides;d=a+sides;faces.append((a,b,c,d))
    if cap:faces.extend([tuple(reversed(range(sides))),tuple((len(points)-1)*sides+j for j in range(sides))])
    add(scope,mat,v,faces)
def cylinder(scope,p,r,length,axis=(0,1,0),mat='Nereid_Trim',sides=12):
    t=Vector(axis)*length/2;tube(scope,[Vector(p)-t,Vector(p)+t],r,mat,sides)
def ring(scope,center,rx,ry,thickness,mat='Nereid_Steel',sides=32):
    x,y,z=center
    pts=[(x+rx*math.cos(i*math.tau/sides),y+ry*math.sin(i*math.tau/sides),z)for i in range(sides+1)]
    tube(scope,pts,thickness,mat,8,False)
def plate(scope,verts,depth,mat='Nereid_Hull'):
    # Arbitrary polygon in x/z, extruded vertically.
    n=len(verts);v=[(x,y-depth/2,z)for x,y,z in verts]+[(x,y+depth/2,z)for x,y,z in verts]
    faces=[tuple(reversed(range(n))),tuple(n+i for i in range(n))]
    faces.extend((i,(i+1)%n,(i+1)%n+n,i+n)for i in range(n));add(scope,mat,v,faces)

if not INTERIOR_ONLY:
    from expedition_exterior import build as build_exterior
    build_exterior(globals())
if not EXTERIOR_ONLY:
    from interior_expedition import build as build_interior
    build_interior(globals())

# Convert Unity-space mesh coordinates to Blender Z-up; one material batch per scope.
objects={'Interior':[],'Exterior':[]}
for (scope,mat),(vertices,faces)in PARTS.items():
    bverts=[(p[0],-p[2],p[1])for p in vertices]
    mesh=bpy.data.meshes.new(f'{scope}_{mat}');mesh.from_pydata(bverts,[],faces);mesh.update()
    obj=bpy.data.objects.new(f'{scope}_{mat}',mesh);bpy.context.collection.objects.link(obj);obj.data.materials.append(MATS[mat]);objects[scope].append(obj)
    # Use meaningful world planar UVs. Tiling URP materials can be remapped without re-unwrapping.
    uv=mesh.uv_layers.new(name='ArchitecturalUV')
    for poly in mesh.polygons:
        n=poly.normal;axis=max(range(3),key=lambda i:abs(n[i]));dims=[i for i in range(3)if i!=axis]
        for li in poly.loop_indices:
            v=mesh.vertices[mesh.loops[li].vertex_index].co;uv.data[li].uv=(v[dims[0]],v[dims[1]])
    if mat not in ('Nereid_Window','Nereid_Lamp'):
        bevel=obj.modifiers.new('Manufactured edge highlights','BEVEL');bevel.width=.014 if mat in ('Nereid_Grate','Nereid_Trim')else .027;bevel.segments=1;bevel.limit_method='ANGLE';bevel.angle_limit=.65;bevel.affect='EDGES'
        norm=obj.modifiers.new('Weighted face normals','WEIGHTED_NORMAL');norm.keep_sharp=True;norm.weight=40
        for p in mesh.polygons:p.use_smooth=True
    # Preserve the world origin, unit scale and deterministic hierarchy.
    obj.location=(0,0,0)

# Insert vertices at changes in the production height conversion before moving the mesh.
for obs in objects.values():
    for obj in obs:
        # Long shared deck/pipe faces need vertices on each deformation boundary.
        # Otherwise their endpoints interpolate across the entire room while a
        # separately built intermediate cuff follows F at its actual position.
        bm=bmesh.new()
        bm.from_mesh(obj.data)
        for co,normal in [((0,-5.5,0),(0,1,0)),((0,-7.5,0),(0,1,0)),((0,0,1),(0,0,1))]:
            bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=1e-6,plane_co=co,plane_no=normal,use_snap_center=False,clear_outer=False,clear_inner=False)
        if FBX_X_EXPORT_SIGN<0:
            bmesh.ops.reverse_faces(bm,faces=list(bm.faces))
        bm.to_mesh(obj.data)
        bm.free()
        for vertex in obj.data.vertices:
            p=vertex.co.copy()
            vertex.co=to_blender((p.x,p.z,-p.y))
        obj.data.update()

bpy.context.scene.unit_settings.system='METRIC';bpy.context.scene.unit_settings.scale_length=1
meta={'coordinates':'Expected Unity model-local metres before scene rootScale; x right, y up, z bow; Blender(-x,-z,y), FBX -Z forward/Y up, Unity default X handedness conversion',
      'scale_compat':SCALE_COMPAT,'scene_root_scale':ROOT_SCALE,
      'handedness':{'export_x_sign':FBX_X_EXPORT_SIGN,'unity_importer_bakeAxisConversion':False,'unity_x_from_blender_world_x':-1,'winding':'Reversed before the export reflection to preserve outward normals','asymmetric_feature':'STARBOARD hatch must be Unity+X; PORT Unity-X stays closed'},
      'vertical_compensation':{'k':'clamp((7.5-z)/2,0,1)','F':'(1-k)*y+k*(y*0.5 if y<=1 else y-0.5)','bow':'No deformation at z>=7.5; full compensation at z<=5.5','world_reference_heights':{'main_ledge':.87,'main_crown':8.2,'aft_crown':6.6,'bow_mouth_centre':3.7}},
      'materials':list(PALETTE),'models':{},
      'openings':{'main_windows':'local x=+-5.87 reveals to+-6.89; z=-7.8..6.55; full-compensated local y .435..2.74, rising smoothly near bow','main_to_aft':'local z=-8,width3.04,height2.65; world z=-16,width6.08,height5.3','aft_starboard_exit':'local x5.8..7,z-15.6..-12.4; world x11.6..14,z-31.2..-24.8,clearHeight~5','bow_mouth':'local z7.6,centre(0,1.85),width5.1,height3.58; world z15.2,centreY3.7,width10.2,height7.16'}}
metadata=OUT/'Nereid_Architecture.json'
if (INTERIOR_ONLY or EXTERIOR_ONLY) and metadata.exists():
    meta['models']=json.loads(metadata.read_text(encoding='utf-8'))['models']
for scope,obs in objects.items():
    if not obs:continue
    bpy.ops.object.select_all(action='DESELECT')
    for o in obs:o.select_set(True)
    bpy.context.view_layer.objects.active=obs[0]
    path=OUT/f'Nereid_{scope}.fbx'
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',bake_space_transform=True,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,mesh_smooth_type='FACE',add_leaf_bones=False,bake_anim=False,path_mode='STRIP')
    dg=bpy.context.evaluated_depsgraph_get();tris=0;verts=[]
    for o in obs:
        ev=o.evaluated_get(dg);m=ev.to_mesh();m.calc_loop_triangles();tris+=len(m.loop_triangles)
        verts.extend([(-p.x,p.z,-p.y)for p in (ev.matrix_world@v.co for v in m.vertices)]);ev.to_mesh_clear()
    lo=[min(v[k]for v in verts)for k in range(3)];hi=[max(v[k]for v in verts)for k in range(3)]
    meta['models'][scope]={'file':path.name,'objects':len(obs),'triangles':tris,'unity_bounds_min':lo,'unity_bounds_max':hi,'root_scale2_world_bounds_min':[v*ROOT_SCALE for v in lo],'root_scale2_world_bounds_max':[v*ROOT_SCALE for v in hi]}
assert sum(m['triangles']for m in meta['models'].values())<250000,meta['models']
metadata.write_text(json.dumps(meta,indent=2)+'\n',encoding='utf-8')
if EXPORT_ONLY:
    print('NEREID_EXPORT',json.dumps(meta))
    raise SystemExit(0)
bpy.ops.wm.save_as_mainfile(filepath=str(PREVIEW/'Nereid_Architecture.blend'))

# Headless preview uses only generated camera/lights and is never exported into the game.
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.render.resolution_x=1280;scene.render.resolution_y=720;scene.render.resolution_percentage=100
scene.world.color=(.05,.07,.09)
def aim(o,u):o.rotation_euler=(to_blender(u)-o.location).to_track_quat('-Z','Y').to_euler()
def light(name,pos,power,col,size=5):
    d=bpy.data.lights.new(name,'AREA');d.energy=power;d.color=col;d.shape='DISK';d.size=size;o=bpy.data.objects.new(name,d);bpy.context.collection.objects.link(o);o.location=to_blender(pos);return o
camera=bpy.data.objects.new('Preview camera',bpy.data.cameras.new('Preview camera'));bpy.context.collection.objects.link(camera);scene.camera=camera;camera.data.lens=24
for z in (-6.6,-1,4.6):
    for side in(-1,1):
        l=light('Preview warm practical',(side*2.7,4.0,z),160,(1,.63,.35),1.0);aim(l,(side*2.7,0,z))
for z in (-6,1,6):
    l=light('Preview cool ocean',(7,3,z),260,(.34,.65,1),4);aim(l,(0,1,z))
if not EXTERIOR_ONLY:
    for o in objects['Exterior']:o.hide_render=True
    camera.location=to_blender((3.8,1.7,-6.6));aim(camera,(-1.8,1.85,2.3));scene.render.filepath=str(PREVIEW/'preview_interior.png');bpy.ops.render.render(write_still=True)
if not INTERIOR_ONLY:
    for o in objects['Exterior']:o.hide_render=False
    camera.location=to_blender((30,18,33));aim(camera,(0,1,-8));camera.data.lens=40
    l=light('Preview exterior key',(12,20,5),7000,(.45,.75,1),20);aim(l,(0,0,-7))
    l=light('Preview exterior rim',(-12,7,-14),5000,(.3,.65,1),15);aim(l,(0,1,-10))
    scene.render.filepath=str(PREVIEW/'preview_exterior.png');bpy.ops.render.render(write_still=True)
print('NEREID_EXPORT',json.dumps(meta))
