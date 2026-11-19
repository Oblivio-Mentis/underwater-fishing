"""Project-authored NEREID architecture; Unity metres, +Z bow, +Y up.
Run Blender -b --factory-startup --python-exit-code 1 -P this_file.
Geometry, openings and materials are deterministic; no downloaded art.
"""
import bpy, math, json, sys
from pathlib import Path
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/_Game/Nereid/Art'
OUT.mkdir(parents=True,exist_ok=True)
PREVIEW=ROOT/'art/nereid'
INTERIOR_ONLY='--interior-only' in sys.argv
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

# Pressure hull: the shell is deliberately absent behind the actual glazing and aft exit.
S='Exterior'
bands=[(-31,.35,.42),(-29,2.5,2.2),(-26,4.8,3.9),(-22,6.35,4.75),(-17,7,5),(-8,7,5),(0,7,5),(8,7,5),(10.2,5.9,4.35),(12.0,3.6,3.1)]
# Subdivide for sheet-metal panel rhythm, retaining each exact opening boundary.
expanded=[]
for i in range(len(bands)-1):
    z0,w0,h0=bands[i];z1,w1,h1=bands[i+1];count=max(1,math.ceil((z1-z0)/2.7))
    cuts=[z0+(z1-z0)*j/count for j in range(count)]
    for cut in (-16,-15.65,-12.35,-8):
        if z0<cut<z1:cuts.append(cut)
    for z in sorted(set(cuts)):
        t=(z-z0)/(z1-z0);expanded.append((z,w0+(w1-w0)*t,h0+(h1-h0)*t))
expanded.append(bands[-1])
for bi in range(len(expanded)-1):
    z0,w0,h0=expanded[bi];z1,w1,h1=expanded[bi+1]
    for j in range(32):
        a=j*math.tau/32;b=(j+1)*math.tau/32;mid=(a+b)/2
        xs=math.cos(mid);ym=1+math.sin(mid)*(h0+h1)/2;zm=(z0+z1)/2
        window=(-8<=zm<=8 and abs(xs)>.75 and .0<ym<3.9)
        hatch=(-15.65<=zm<=-12.35 and xs>.72 and -.2<ym<3.9)
        if window or hatch:continue
        gap=.028;z0g=z0+gap;z1g=z1-gap
        verts=[(math.cos(a)*w0,1+math.sin(a)*h0,z0g),(math.cos(b)*w0,1+math.sin(b)*h0,z0g),(math.cos(b)*w1,1+math.sin(b)*h1,z1g),(math.cos(a)*w1,1+math.sin(a)*h1,z1g)]
        add(S,'Nereid_Hull'if (bi+j)%6 else 'Nereid_Steel',verts,[(0,1,2,3)])
        # Inner face gives shell a credible edge at its openings, without expensive Solidify.
        iv=[(x*.985,1+(y-1)*.985,z)for x,y,z in verts];add(S,'Nereid_Dark',iv,[(3,2,1,0)])
for z,w,h in expanded:
    pts=[]
    for i in range(65):
        a=i*math.tau/64;pts.append((math.cos(a)*(w+.025),1+math.sin(a)*(h+.025),z))
    # Thin seams rather than thick toy hoops.
    tube(S,pts,.035,'Nereid_Dark',6,False)
# Twin lower segmented saddle tanks and protective side rails.
for side in (-1,1):
    for k in range(5):
        z=-20.8+k*5.25
        tube(S,[(side*6.55,-1.4,z-2.5),(side*6.8,-1.45,z-2),(side*6.8,-1.45,z+2),(side*6.55,-1.4,z+2.5)],.96,'Nereid_Hull',16)
        for zz in (z-1.7,z+1.7):
            ring(S,(side*6.8,-1.45,zz),.99,.99,.065,'Nereid_Dark',20)
    # Folded hydrodynamic stabilisers at stern.
    plate(S,[(side*3.8,.35,-24),(side*10.7,.1,-25.8),(side*10.3,.1,-28.2),(side*3.2,.35,-27.1)],.2)
    plate(S,[(side*6.9,-1.6,-12),(side*8.2,-1.65,-13),(side*8.2,-1.65,4),(side*6.9,-1.6,5)],.12,'Nereid_Dark')
    # Exterior maintenance catwalk extends through the stern exit elevation.
    box(S,'side catwalk',(side*7.04,-.07,-8.5),(1.78,.14,29),'Nereid_Grate')
    for z in range(-22,7,3):
        beam(S,(side*7.87,0,z),(side*7.87,1.1,z),.055,.06,'Nereid_Trim')
    tube(S,[(side*7.87,1.12,-22.7),(side*7.87,1.12,6)],.045,'Nereid_Trim',8)
    tube(S,[(side*7.87,.57,-22.7),(side*7.87,.57,6)],.025,'Nereid_Dark',8)
    # Engine access louvers and external clamps near aft machinery.
    for z in (-22,-19):
        box(S,'engine access panel',(side*6.55,2.2,z),(.12,1.45,2.1),'Nereid_Dark')
        for k in range(9):box(S,'louver',(side*6.64,1.61+k*.14,z),(.16,.065,1.8),'Nereid_Trim')
    for z in (-20,-16,-11,-5,2,7):
        box(S,'exterior lamp housing',(side*6.97,3.35,z),(.33,.3,.7),'Nereid_Dark')
        box(S,'exterior lamp lens',(side*7.16,3.31,z),(.035,.15,.52),'Nereid_Lamp')

# Deck spine and streamlined survey tower: layered mass, window bridge, guarded top.
box(S,'upper technical spine',(0,5.75,-10),(3.7,.45,20),'Nereid_Dark')
for z in range(-19,0,2):box(S,'deck spine sheet',(0,6.02,z),(3.6,.08,1.92),'Nereid_Steel')
tower_sections=[(-12,1.3,6.0),(-11,2.15,6.0),(-5.5,2.15,6.0),(-4.1,1.2,6.0)]
for lo,hi in [(6.0,8.2),(8.2,8.65),(9.25,9.7)]:
    verts=[(-1.9,lo,-11.2),(1.9,lo,-11.2),(1.9,lo,-5.4),(1.25,lo,-4.3),(-1.25,lo,-4.3),(-1.9,lo,-5.4)]
    plate(S,[(x,(lo+hi)/2,z)for x,y,z in verts],hi-lo,'Nereid_Hull')
for side in (-1,1):
    for z in (-10.4,-8.7,-7,-5.5):
        box(S,'bridge window',(side*1.93,8.95,z),(.09,.6,1.28),'Nereid_Window')
        for yy in (8.61,9.29):box(S,'bridge window metal',(side*1.99,yy,z),(.12,.09,1.42),'Nereid_Trim')
        box(S,'bridge window mullion',(side*1.98,8.95,z-.7),(.12,.7,.09),'Nereid_Steel')
    tube(S,[(side*1.9,10.3,-11),(side*1.9,10.3,-5.5)],.04,'Nereid_Trim',8)
    for z in (-11,-9,-7,-5.5):beam(S,(side*1.9,9.7,z),(side*1.9,10.3,z),.055,.055)
cylinder(S,(.7,11.25,-9.3),.11,3.1,mat='Nereid_Dark',sides=12)
box(S,'periscope head',(.7,12.8,-9),(1.2,.2,.6),'Nereid_Steel')
cylinder(S,(-.6,10.8,-7.5),.08,2.4,mat='Nereid_Trim',sides=10)
box(S,'radio array',(-.6,11.95,-7.5),(2.5,.07,.17),'Nereid_Dark')
# Aft shrouded propulsor rings, solid silhouette separate from actual hatch at z-14.
ring(S,(0,1,-30.6),2.2,2.2,.27,'Nereid_Dark',32)
cylinder(S,(0,1,-31),.48,.9,axis=(0,0,1),mat='Nereid_Trim',sides=16)
for i in range(5):
    a=i*math.tau/5;tip=(math.cos(a)*1.85,1+math.sin(a)*1.85,-31.1)
    beam(S,(math.cos(a)*.35,1+math.sin(a)*.35,-30.9),tip,.38,.1,'Nereid_Steel')

# Interior. Actual opaque walls respect windows; large-medium-small rhythm carries the space.
S='Interior'
box(S,'pressure deck foundation',(0,-.18,-4.05),(11.8,.08,23.9),'Nereid_Grate')
for ix in range(8):
    for iz in range(16):
        x=-5.16+ix*1.475;z=-15.25+iz*1.49
        grate=ix in (1,2,5,6) or (iz<5 and ix in (3,4))
        if not grate:box(S,'deck tile',(x,-.031,z),(1.435,.055,1.45),'Nereid_Steel')
        else:
            # Open grating is genuine geometry over a dark underfloor, not just a printed checker.
            for k in range(8):box(S,'grate bearing bar',(x-.68+k*.193,-.025,z),(.065,.05,1.44),'Nereid_Grate')
            for k in range(5):box(S,'grate cross bar',(x,-.03,z-.68+k*.34),(1.43,.035,.045),'Nereid_Grate')
            for side in (-1,1):
                box(S,'grating tile perimeter',(x+side*.704,-.022,z),(.035,.05,1.45),'Nereid_Steel')
                box(S,'grating tile perimeter',(x,-.022,z+side*.707),(1.435,.05,.035),'Nereid_Steel')
        # Perimeter screws and edge bevels stay quiet at player distance.
        for sx in (-1,1):
            for sz in (-1,1):cylinder(S,(x+sx*.59,.003,z+sz*.59),.027,.018,mat='Nereid_Dark',sides=6)
# Floor threshold demarcations as modest yellow-black chevrons at doors.
for z in (-8.0,-15.7):
    box(S,'hatch threshold',(0,.012,z),(2.85,.025,.26),'Nereid_Dark')
    for i in range(11):box(S,'threshold safety mark',(-1.3+i*.26,.028,z),(.12,.01,.19),'Nereid_Ochre')
# Side glazing: clipped-corner thick pressure frames with deep reveals.
def window(side,z):
    x=side*5.87;wide=2.56;lo=.87;hi=3.24;cut=.24
    yz=[(lo,z-wide/2+cut),(lo,z+wide/2-cut),(lo+cut,z+wide/2),(hi-cut,z+wide/2),(hi,z+wide/2-cut),(hi,z-wide/2+cut),(hi-cut,z-wide/2),(lo+cut,z-wide/2)]
    cy=(lo+hi)/2
    inner=[(y,z0)for y,z0 in yz]
    outer=[(cy+(y-cy)*1.12,z+(z0-z)*1.105)for y,z0 in yz]
    v=[];faces=[]
    # Each eight-sided collar includes real depth and chamfer-like sloping reveal.
    for xx,poly in [(x-side*.03,outer),(x+side*.12,inner),(side*6.89,inner)]:v.extend((xx,y,zz)for y,zz in poly)
    for k in range(8):
        j=(k+1)%8;faces.extend([(k,j,8+j,8+k),(8+k,8+j,16+j,16+k)])
    add(S,'Nereid_Steel',v,faces if side>0 else [tuple(reversed(f))for f in faces])
    # Exterior flange closes the wall thickness and presents its own outward-facing surface.
    outer_front=[(side*6.925,y,zz)for y,zz in outer]
    inner_front=[(side*6.925,y,zz)for y,zz in inner]
    face_rim=[(k,(k+1)%8,8+(k+1)%8,8+k)for k in range(8)]
    add(S,'Nereid_Dark',outer_front+inner_front,[tuple(reversed(f))for f in face_rim]if side>0 else face_rim)
    # Glass lies in an actual unobstructed shell aperture.
    glass_face=tuple(range(8))
    add(S,'Nereid_Window',[(side*6.84,y,zz)for y,zz in inner],[glass_face,tuple(reversed(glass_face))])
    for k in range(8):
        y,zz=outer[k];cylinder(S,(x-side*.05,y,zz),.046,.06,axis=(1,0,0),mat='Nereid_Dark',sides=6)
    box(S,'deep window sill',(side*5.82,.77,z),(.32,.14,2.86),'Nereid_Dark')
for side in (-1,1):
    for z in (-6.45,-3.55,-.65,2.25,5.15):window(side,z)
    for z in (-6.5,-3.5,-.5,2.5,5.5):
        box(S,'lower wall armour',(side*5.97,.4,z),(.22,.76,2.92),'Nereid_Hull')
        box(S,'lower wall inset',(side*5.835,.42,z),(.035,.4,2.25),'Nereid_Dark')
        box(S,'wall toe rail',(side*5.68,.2,z),(.08,.09,2.7),'Nereid_Trim')
        box(S,'crown wall panel',(side*5.98,3.7,z),(.23,.9,2.92),'Nereid_Hull')
    # Stern inner shell, with exact starboard wet lock opening.
    for z0,z1 in ([(-16,-8)]if side<0 else [(-16,-15.65),(-12.35,-8)]):
        box(S,'stern side armour',(side*5.97,1.8,(z0+z1)/2),(.22,3.6,z1-z0),'Nereid_Hull')
    if side>0:box(S,'stern hatch overhead',(5.97,3.5,-14),(.22,.7,3.3),'Nereid_Hull')
# Arched ribs, interior octagonal profile and junction shoes.
for z in (-15.75,-12.4,-8,-5.2,-2.4,.4,3.2,6.0,7.75):
    h=3.8 if z<-8 else 4.6
    pts=[(-5.76,.05,z),(-5.76,h-1.15,z),(-4.68,h-.15,z),(4.68,h-.15,z),(5.76,h-1.15,z),(5.76,.05,z)]
    for i in range(len(pts)-1):beam(S,pts[i],pts[i+1],.22,.23,'Nereid_Trim')
    for side in (-1,1):
        box(S,'rib anchorage',(side*5.71,.2,z),(.33,.32,.4),'Nereid_Dark')
        for y in (1.05,2.0,3.05):
            if y<h-.4:cylinder(S,(side*5.615,y,z),.037,.04,axis=(1,0,0),mat='Nereid_Dark',sides=6)
        box(S,'rib junction gusset',(side*4.6,h-.17,z),(.54,.19,.37),'Nereid_Steel')
# Curved-overhead mass by chamfered ceiling panels and quiet inset service routes.
for z in [-14.4,-11.3,-6.6,-3.8,-1,1.8,4.6,7]:
    h=3.8 if z<-8 else 4.6
    box(S,'central ceiling panels',(0,h-.055,z),(8.95,.11,2.58),'Nereid_Hull')
    for side in (-1,1):
        beam(S,(side*4.6,h-.1,z-1.26),(side*5.8,h-1.18,z-1.26),.11,2.55,'Nereid_Hull')
        # Cable tray with grouped supporting crosspieces.
        box(S,'ceiling cable tray',(side*3.7,h-.24,z),(.42,.14,2.6),'Nereid_Dark')
        for k in range(4):box(S,'tray bracket',(side*3.7,h-.34,z-1+k*.67),(.55,.06,.055),'Nereid_Trim')
# Main service conduits use dark steel and ochre only on joint collars.
for side in (-1,1):
    for k in range(2):
        xx=side*(4.7+k*.24)
        tube(S,[(xx,3.54,-7.8),(xx,3.54,7.7)],.09 if k==0 else .055,'Nereid_Dark',10)
        for z in (-7,-4,-1,2,5,7):cylinder(S,(xx,3.54,z),.11 if k==0 else .073,.08,axis=(0,0,1),mat='Nereid_Ochre',sides=10)
        tube(S,[(xx,3.1,-15.7),(xx,3.1,-8.3)],.08,'Nereid_Dark',10)
# Caged warm lamps repeat at deliberate bays; emission surface is small and recessed.
for z in (-14.4,-10.5,-6.6,-1,4.6):
    h=3.8 if z<-8 else 4.6
    for side in (-1,1):
        x=side*2.7
        box(S,'lamp heavy backing',(x,h-.22,z),(.82,.16,.52),'Nereid_Dark')
        box(S,'lamp glass',(x,h-.335,z),(.62,.06,.35),'Nereid_Lamp')
        for off in (-.26,0,.26):box(S,'lamp cage',(x+off,h-.39,z),(.026,.035,.43),'Nereid_Steel')
        for off in (-.21,.21):box(S,'lamp cage rail',(x,h-.39,z+off),(.79,.035,.026),'Nereid_Trim')
# Portal from main reservoir room to the real aft compartment.
for side in (-1,1):box(S,'aft pressure bulkhead',(side*3.7,1.84,-8.08),(4.45,3.7,.2),'Nereid_Hull')
box(S,'aft portal upper',(0,3.95,-8.08),(3.1,.7,.2),'Nereid_Hull')
portal=[(-1.52,0,-7.9),(-1.52,2.55,-7.9),(-1.15,3.15,-7.9),(1.15,3.15,-7.9),(1.52,2.55,-7.9),(1.52,0,-7.9)]
for a,b in zip(portal,portal[1:]):beam(S,a,b,.17,.16,'Nereid_Trim')
for side in (-1,1):
    for y in (.55,1.4,2.25):box(S,'door latch',(side*1.53,y,-7.72),(.13,.22,.2),'Nereid_Ochre')
# Aft-most sealed engine partition and its broad recessed inspection hatch.
box(S,'aft engine bulkhead',(0,1.9,-15.98),(11.8,3.8,.18),'Nereid_Hull')
box(S,'engine hatch inset',(0,1.6,-15.84),(3.4,2.8,.08),'Nereid_Dark')
box(S,'engine hatch',(0,1.6,-15.77),(3.0,2.5,.09),'Nereid_Steel')
ring(S,(0,1.7,-15.66),.34,.34,.055,'Nereid_Trim',20)
# Starboard airlock collar; no plane crosses the actual 3.1m opening.
for z in (-15.63,-12.37):
    box(S,'wet lock upright',(5.81,1.52,z),(.35,3.04,.2),'Nereid_Trim')
    box(S,'wet lock recessed jamb',(6.4,1.52,z),(.9,3.04,.18),'Nereid_Dark')
box(S,'wet lock lintel',(5.85,3.12,-14),(.36,.24,3.5),'Nereid_Trim')
box(S,'wet lock threshold',(6.35,.005,-14),(1.3,.09,3.45),'Nereid_Grate')
for z in (-15.5,-12.5):box(S,'airlock edge marking',(5.61,1.5,z),(.035,2.7,.08),'Nereid_Ochre')
# Mouth pressure bulkhead: broad contour and a completely open central cavity.
for side in (-1,1):box(S,'bow pressure shoulder',(side*4.35,1.95,7.9),(2.85,3.9,.24),'Nereid_Hull')
box(S,'bow pressure crown',(0,4.05,7.9),(6,.95,.24),'Nereid_Hull')
ring(S,(0,1.85,7.74),2.7,1.9,.17,'Nereid_Dark',32)
ring(S,(0,1.85,7.61),2.58,1.79,.07,'Nereid_Trim',32)
for i in range(16):
    a=i*math.tau/16;cylinder(S,(math.cos(a)*2.75,1.85+math.sin(a)*1.94,7.55),.062,.09,axis=(0,0,1),mat='Nereid_Steel',sides=6)

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

# Authored exterior lettering in the tower-side blank steel panel.
for side in (-1,1):
    data=bpy.data.curves.new('NEREID hull stencil','FONT');data.body='NEREID  /  07';data.align_x='CENTER';data.size=.68;data.extrude=.001;data.space_character=1.15
    obj=bpy.data.objects.new('Exterior_NEREID_stencil',data);bpy.context.collection.objects.link(obj)
    # Text faces Blender local +Z; make it face the corresponding exterior X side.
    obj.location=(side*6.72,19.5,2.2);obj.rotation_euler=(math.pi/2,0,side*math.pi/2)
    obj.data.materials.append(MATS['Nereid_Trim']);bpy.context.view_layer.objects.active=obj;obj.select_set(True);bpy.ops.object.convert(target='MESH');obj.select_set(False);objects['Exterior'].append(obj)

bpy.context.scene.unit_settings.system='METRIC';bpy.context.scene.unit_settings.scale_length=1
meta={'coordinates':'Unity metres, x right, y up, z bow; geometry built in Blender(x,-z,y), exported FBX -Z forward/Y up','materials':list(PALETTE),'models':{},'openings':{'main_windows':'x=+-5.87 reveals to+-6.89; z=-7.8..6.55; y0.87..3.24','main_to_aft':'z=-8,width3.04,height3.15','aft_starboard_exit':'x5.8..7,z-15.6..-12.4,y0..3.0','bow_mouth':'z7.6,centre(0,1.85),width5.1,height3.58'}}
if INTERIOR_ONLY:
    meta['models']=json.loads((OUT/'Nereid_Architecture.json').read_text(encoding='utf-8'))['models']
for scope,obs in objects.items():
    if INTERIOR_ONLY and scope!='Interior':continue
    bpy.ops.object.select_all(action='DESELECT')
    for o in obs:o.select_set(True)
    bpy.context.view_layer.objects.active=obs[0]
    path=OUT/f'Nereid_{scope}.fbx'
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',bake_space_transform=True,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,mesh_smooth_type='FACE',add_leaf_bones=False,bake_anim=False,path_mode='STRIP')
    dg=bpy.context.evaluated_depsgraph_get();tris=0;verts=[]
    for o in obs:
        ev=o.evaluated_get(dg);m=ev.to_mesh();m.calc_loop_triangles();tris+=len(m.loop_triangles)
        verts.extend([(v.co.x,v.co.z,-v.co.y)for v in m.vertices]);ev.to_mesh_clear()
    meta['models'][scope]={'file':path.name,'objects':len(obs),'triangles':tris,'unity_bounds_min':[min(v[k]for v in verts)for k in range(3)],'unity_bounds_max':[max(v[k]for v in verts)for k in range(3)]}
(OUT/'Nereid_Architecture.json').write_text(json.dumps(meta,indent=2)+'\n',encoding='utf-8')
bpy.ops.wm.save_as_mainfile(filepath=str(PREVIEW/'Nereid_Architecture.blend'))

# Headless preview uses only generated camera/lights and is never exported into the game.
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.render.resolution_x=1280;scene.render.resolution_y=720;scene.render.resolution_percentage=100
scene.world.color=(.05,.07,.09)
def aim(o,u):o.rotation_euler=(Vector((u[0],-u[2],u[1]))-o.location).to_track_quat('-Z','Y').to_euler()
def light(name,pos,power,col,size=5):
    d=bpy.data.lights.new(name,'AREA');d.energy=power;d.color=col;d.shape='DISK';d.size=size;o=bpy.data.objects.new(name,d);bpy.context.collection.objects.link(o);o.location=(pos[0],-pos[2],pos[1]);return o
camera=bpy.data.objects.new('Preview camera',bpy.data.cameras.new('Preview camera'));bpy.context.collection.objects.link(camera);scene.camera=camera;camera.data.lens=24
for z in (-6.6,-1,4.6):
    for side in(-1,1):
        l=light('Preview warm practical',(side*2.7,4.0,z),160,(1,.63,.35),1.0);aim(l,(side*2.7,0,z))
for z in (-6,1,6):
    l=light('Preview cool ocean',(7,3,z),260,(.34,.65,1),4);aim(l,(0,1,z))
for o in objects['Exterior']:o.hide_render=True
camera.location=(3.8,6.6,1.7);aim(camera,(-1.8,1.85,2.3));scene.render.filepath=str(PREVIEW/'preview_interior.png');bpy.ops.render.render(write_still=True)
if not INTERIOR_ONLY:
    for o in objects['Exterior']:o.hide_render=False
    camera.location=(30,-33,18);aim(camera,(0,1,-8));camera.data.lens=40
    l=light('Preview exterior key',(12,20,5),7000,(.45,.75,1),20);aim(l,(0,0,-7))
    l=light('Preview exterior rim',(-12,7,-14),5000,(.3,.65,1),15);aim(l,(0,1,-10))
    scene.render.filepath=str(PREVIEW/'preview_exterior.png');bpy.ops.render.render(write_still=True)
print('NEREID_EXPORT',json.dumps(meta))
