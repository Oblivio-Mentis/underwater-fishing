"""NEREID trench survey vessel. Hull-local metres, +Z toward the creature.

The drive openings, dorsal structure and pressure skin are constructed here.
Door, playable pressure shell and diving sightline contracts come from the scene.
"""
import math


# Half breadth and dorsal crown. The two drive bays share the wide aft stern.
PROFILE = (
    (-35.32, 6.08, 3.07), (-34.36, 6.82, 3.83), (-32.66, 7.05, 4.78),
    (-30.45, 7.22, 5.20), (-26.15, 7.37, 5.22), (-22.35, 7.41, 5.22),
    (-18.5, 7.41, 5.29), (-16.0, 7.39, 5.35), (-8.0, 7.37, 5.39),
    (7.60, 7.31, 5.39), (9.57, 6.54, 4.90), (11.46, 5.41, 4.35), (12.56, 4.12, 3.92),
)
PORTS = (-6.45, -3.55, -.65, 2.25, 5.15)
PORT_EDGES = tuple(z + spacing for z in PORTS for spacing in (-1.343, 1.343))
PANEL_EDGES = (-30.6, -27.3, -24.34, -22.21, -20.02, -17.23, -15.83, -12.18, -9.94, -8.04,
               -4.99, -2.09, .81, 3.71, 6.61, 8.36, 10.32)
HULL = "Nereid_Hull"
CERAMIC = "Nereid_Steel"
SEAM = "Nereid_Dark"
TRIM = "Nereid_Trim"
STRIPE = "Nereid_Ochre"


def _section(z):
    for (z0,w0,h0),(z1,w1,h1) in zip(PROFILE,PROFILE[1:]):
        if z<=z1:
            t=min(1,max(0,(z-z0)/(z1-z0)))
            return w0+(w1-w0)*t,h0+(h1-h0)*t
    return PROFILE[-1][1:]


def _rim(z):
    width,height=_section(z)
    upper=height/5.39
    near_ports=-8.18<=z<=7.17
    side=6.98 if near_ports else width
    keel=(-2.85 if z>-30.45 else -2.26)
    points=(
        (-width*.64,keel),(width*.64,keel),
        (width*.87,-2.06),(width*.974,-.58),(side,.855*upper),(side,3.391*upper),
        (width*.928,4.37*upper),(width*.647,height),
        (-width*.647,height),(-width*.928,4.37*upper),(-side,3.391*upper),(-side,.855*upper),
        (-width*.974,-.58),(-width*.87,-2.06),
    )
    return [(x,y,z) for x,y in points]


def _quad(add, material, a,b,c,d, two_sided=False):
    add("Exterior",material,[a,b,c,d],[(0,1,2,3)])
    if two_sided:
        add("Exterior",material,[d,c,b,a],[(0,1,2,3)])


def _blend(a,b,t):
    return tuple(x+(y-x)*t for x,y in zip(a,b))


def _hull(add):
    rows=sorted(set((*[p[0] for p in PROFILE], *PORT_EDGES, *PANEL_EDGES,-22.16,-25.04,-8.18,7.17)))
    for band,(z0,z1) in enumerate(zip(rows,rows[1:])):
        z=(z0+z1)*.5
        p,q=_rim(z0),_rim(z1)
        glazed=any(abs(z-bay)<1.342 for bay in PORTS)
        sea_lock=-15.83<=z<=-12.18
        stair=-25.04<=z<=-22.16
        for i in range(len(p)):
            j=(i+1)%len(p)
            if (glazed and i in (4,10)) or (sea_lock and i in (3,4)):
                continue
            if i==7 and stair:
                # Project the cover onto the two bands on either side of the precise companionway.
                for x0,x1 in ((p[j][0],-3.86),(-1.35,p[i][0])):
                    _quad(add,HULL,(x1,p[i][1],z0),(x0,p[i][1],z0),(x0,q[i][1],z1),(x1,q[i][1],z1))
                continue
            metal=SEAM if i in (0,1,13) else HULL
            _quad(add,metal,p[i],p[j],q[j],q[i],i in (4,10))
        if sea_lock:
            continue
        # Recessed lower shoulder in four-foot pressure-plated segments.
        for i in (3,11):
            j=i+1
            offset=(1 if i==3 else -1)*.045
            corners=[]
            for a,b in ((p[i],p[j]),(q[i],q[j])):
                for t in (.19,.72):
                    x,y,zz=_blend(a,b,t)
                    corners.append((x+offset,y,zz))
            _quad(add,CERAMIC,corners[0],corners[1],corners[3],corners[2])


def _underbelly(add,plate,beam,box):
    for side in (-1,1):
        # Long lift strakes take loads where the equipment is mounted, not at the window faces.
        for z0,z1 in ((-30.3,-26.4),(-24.75,-21.5),(-19.91,-16.8),(-11.8,-8.0),(-6.1,-2.2),(-.1,4.9)):
            xs=[(side*x,-1.93,z) for x,z in ((5.97,z0+.52),(6.8,z0+.30),(6.98,z1-.48),(5.88,z1))]
            plate("Exterior",xs,.167,SEAM)
            beam("Exterior",(side*6.61,-1.86,z0+.48),(side*6.74,-1.86,z1-.55),.085,.105,TRIM)

        rail=((side*5.96,-.41,-19.14),(side*7.35,-.42,-17.3),
              (side*7.42,-.40,-12.12),(side*6.98,-.36,-10.87))
        for a,b in zip(rail,rail[1:]):
            beam("Exterior",a,b,.24,.33,HULL)
        # Mount wells on both shoulders. Hatch clearance on the starboard side
        # runs from y .25 to 1.75 in the resulting model and stays completely open.
        for z in (-16.3,-11.82):
            beam("Exterior",(side*6.93,-.35,z),(side*6.03,3.94,z-.55),.192,.31,TRIM)
        for z in (-20.8,-17.9,-9.43,7.38):
            box("Exterior","Cold inspection cassette",(side*7.22,3.88,z),(.26,.26,.58),SEAM)
            box("Exterior","Potted deck-light lens",(side*7.35,3.874,z),(.046,.10,.36),"Nereid_Lamp")


def _dorsal(add, box):
    # Low avionics line rises above the shoulder with scarfed first and last caps.
    design=((-22.19,1.96,5.38,5.86),(-19.35,2.48,5.48,6.42),(-15.46,2.62,5.51,6.53),
            (-10.11,2.38,5.58,6.44),(-5.78,2.02,5.48,6.30),(-1.60,1.95,5.46,6.14),
            (3.91,1.69,5.47,5.99),(6.06,1.31,5.37,5.58))
    def polygon(section):
        z,w,seat,top=section
        return [(-w,seat,z),(w,seat,z),(w*.796,seat+.26,z),(w*.557,top,z),
                (-w*.557,top,z),(-w*.796,seat+.26,z)]
    for current,next_section in zip(design,design[1:]):
        a,b=polygon(current),polygon(next_section)
        for i in range(len(a)):
            j=(i+1)%len(a)
            _quad(add,CERAMIC if i in (1,4) else HULL,a[i],a[j],b[j],b[i])
    for section,flipped in ((design[0],True),(design[-1],False)):
        loop=polygon(section)
        add("Exterior",HULL,list(reversed(loop)) if flipped else loop,[tuple(range(len(loop)))])

    # Stacked sensors keep a clean long silhouette instead of a second bridge box.
    for z,w,y,length in ((-16.88,1.45,6.49,2.12),(-12.66,1.22,6.48,1.98),(-7.42,1.13,6.36,1.87),
                         (-2.58,.99,6.24,1.57),(2.08,.87,6.08,1.40)):
        box("Exterior","Gasket-seated deck instrument",(0,y,z),(w,.169,length),SEAM)
        box("Exterior","Survey fin saddle",(0,y+.095,z),(w-.204,.087,length-.178),TRIM)
        for side in (-1,1):
            box("Exterior","Amber locator inset",(side*w*.39,y+.151,z),(.077,.038,length*.68),STRIPE)


def _ports(add,beam,tube):
    for side in (-1,1):
        x=side*7.034
        for z in PORTS:
            # True open centre. Frames sit over the imported interior window flange.
            opening=[(.416,-1.098),(.416,1.098),(.544,1.35),(2.776,1.35),
                     (3.263,1.089),(3.263,-1.089),(2.776,-1.35),(.544,-1.35)]
            start=[(x,y,z+a) for y,a in opening]
            surround=[(side*7.105,1.835+(y-1.835)*1.09,z+a*1.068) for y,a in opening]
            for i in range(len(start)):
                j=(i+1)%len(start)
                vertices=[surround[i],surround[j],start[j],start[i]]
                if side>0:
                    vertices.reverse()
                _quad(add,CERAMIC,*vertices,True)
            beam("Exterior",(side*7.101,3.42,z-.99),(side*7.101,3.42,z+.99),.068,.112,STRIPE)

        # Raised lugs and pressure-channel transfer braces fold into the collar.
        z=-14.0
        nodes=[(side*7.22,4.12,z-2.15),(side*7.80,3.73,z-1.73),(side*8.70,3.44,z-1.71),
               (side*8.82,3.27,z-1.47)]
        arm=[(a,b,c+3.13) for a,b,c in nodes]
        for path in (nodes,arm):
            tube("Exterior",path,.128,HULL,8)


def _drive(add,box,beam,ring,cylinder):
    z0=-35.29
    # Upper strakes close the transom above and below the exhausts. The longitudinal
    # motor housings and their two bored openings define the new stern.
    for y0,y1,w0,w1 in ((-2.29,-1.02,3.82,6.00),(2.90,3.08,5.22,3.89)):
        a,b,c,d=[(-w0,y0,z0),(w0,y0,z0),(w1,y1,z0),(-w1,y1,z0)]
        _quad(add,HULL,d,c,b,a)
    _quad(add,HULL,(-.96,-1.04,z0),(-.96,2.94,z0),(.96,2.94,z0),(.96,-1.04,z0))
    for side in (-1,1):
        x=side*3.38
        layout=((-35.42,1.44,1.50),(-35.23,1.66,1.67),(-34.73,1.42,1.40),
                (-34.21,1.21,1.22),(-33.92,1.19,1.21))
        y=.91
        def loop(z,rx,ry):
            return [(x+math.cos(i*math.tau/16)*rx,y+math.sin(i*math.tau/16)*ry,z) for i in range(16)]
        for k,(a,b) in enumerate(zip(layout,layout[1:])):
            a,b=loop(*a),loop(*b)
            for i in range(len(a)):
                j=(i+1)%len(a)
                _quad(add,CERAMIC if k==0 else SEAM,a[j],a[i],b[i],b[j],True)
        # The head sits inside the transom, behind the cutout.
        ring("Exterior",(x,y,-35.25),1.61,1.63,.058,TRIM,16)
        cylinder("Exterior",(x,y,-33.76),.382,.50,axis=(0,0,1),mat=TRIM,sides=12)
        for i in range(7):
            angle=i*math.tau/7
            a=(x+math.cos(angle)*.30,y+math.sin(angle)*.30,-33.98)
            b=(x+math.cos(angle+.27)*1.11,y+math.sin(angle+.27)*1.13,-34.07)
            beam("Exterior",a,b,.228,.087,TRIM)
        # Shoulder outside the circular opening closes onto the hull edge.
        for i in (-1,1):
            t=side*5.91
            u=side*1.02
            center_top=y+i*1.70
            _quad(add,HULL,(t,center_top,z0),(u,center_top,z0),(u,center_top+i*.19,z0),(t,center_top+i*.19,z0),True)
        box("Exterior","Recessed stern locator",(side*5.52,1.14,-35.07),(.19,.163,.055),"Nereid_Lamp")


def _bow(add,box,beam,ring):
    # Retain the creature's existing axial mounting geometry.
    for z,rx,ry in ((7.93,2.94,2.058),(8.23,3.06,2.11),(10.46,3.88,2.47)):
        ring("Exterior",(0,1.85,z),rx,ry,.144,SEAM,24)
    for side in (-1,1):
        beam("Exterior",(side*6.87,.62,7.61),(side*4.12,1.65,12.53),.370,.60,HULL)
        beam("Exterior",(side*6.89,.66,7.62),(side*4.16,1.69,12.48),.09,.065,STRIPE)
        box("Exterior","Instrumented nose shoulder",(side*4.78,3.83,10.81),(.491,.372,.877),CERAMIC)


def build(api):
    """Populate the Exterior groups through the shared NEREID mesh helpers."""
    add,box,beam,tube,plate,ring,cylinder=(api[k] for k in ("add","box","beam","tube","plate","ring","cylinder"))
    _hull(add)
    _underbelly(add,plate,beam,box)
    _dorsal(add,box)
    _ports(add,beam,tube)
    _drive(add,box,beam,ring,cylinder)
    _bow(add,box,beam,ring)
