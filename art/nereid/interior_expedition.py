"""NEREID pressure-deck architecture, authored in the generator's Unity coordinates.

The lab, transfer deck and bow collar are made from purpose-built panel, rib and
duct meshes. The fixed pressure-glass and machinery openings are interface dimensions.
"""
import math
from mathutils import Vector


ARCH = 'Interior'
HULL = 'Nereid_Hull'
STEEL = 'Nereid_Steel'
DARK = 'Nereid_Dark'
CLADDING = 'Nereid_Trim'
AMBER = 'Nereid_Ochre'
GRATE = 'Nereid_Grate'
GLASS = 'Nereid_Window'
LAMP = 'Nereid_Lamp'
RUBBER = 'Nereid_Rubber'


def build(g):
    add = g['add']
    box = g['box']
    beam = g['beam']
    tube = g['tube']
    cylinder = g['cylinder']
    ring = g['ring']
    plate = g['plate']

    def block(name, pos, dims, material, basis=None):
        box(ARCH, name, pos, dims, material, basis)

    def strut(a, b, width, depth, material):
        beam(ARCH, a, b, width, depth, material)

    def duct(points, radius, material, sides=10, cap=True):
        tube(ARCH, points, radius, material, sides, cap)

    def pin(pos, radius, length, axis, material, sides=6):
        cylinder(ARCH, pos, radius, length, axis=axis, mat=material, sides=sides)

    def polygon(verts, material):
        plate(ARCH, verts, .009, material)

    def grate_tile(x, z, width, length, transfer=False):
        block('Recessed service channel', (x, -.093, z), (width, .083, length), DARK)
        for side in (-1, 1):
            block('Grille rim', (x + side * (width / 2 - .035), -.019, z),
                  (.07, .033, length), CLADDING)
            block('Grille rail', (x, -.019, z + side * (length / 2 - .035)),
                  (width - .14, .033, .07), STEEL)
        count = 7 if transfer else 6
        for i in range(count):
            zz = z + ((i + .5) / count - .5) * (length - .15)
            block('Raised transverse bearing', (x, -.014, zz), (width - .15, .032, .057), GRATE)
        for fraction in (-.22, .22):
            block('Recessed grille tie', (x + fraction * width, -.039, z),
                  (.04, .022, length - .13), STEEL)

    def deck():
        block('Pressure deck substrate', (0, -.151, -4.04), (11.83, .073, 24.03), DARK)
        for region, lower, upper, rows in (('lab', -8, 7.97, 9), ('transfer', -16.04, -8, 5)):
            dz = (upper - lower) / rows
            for row in range(rows):
                z = lower + (row + .5) * dz
                for column, (left, right) in enumerate(zip(
                        (-5.9, -4.24, -3.12, -1.44, 0, 1.44, 3.12, 4.24),
                        (-4.24, -3.12, -1.44, 0, 1.44, 3.12, 4.24, 5.9))):
                    x = (left + right) / 2
                    width = right - left - .032
                    if column in (1, 6) or (region == 'transfer' and column in (3, 4)):
                        grate_tile(x, z, width, dz - .04, region == 'transfer')
                        continue
                    block('Recessed deck sheet', (x, -.039, z), (width, .068, dz - .037),
                          STEEL if column in (2, 5) or (column + row) % 5 == 0 else GRATE)
                    for screw in (-1, 1):
                        for side in (-1, 1):
                            pin((x + side * (width / 2 - .115), -.001, z + screw * (dz / 2 - .101)),
                                .024, .014, (0, 1, 0), DARK)

        # A recessed inspection perimeter and warm stripe guide the cargo route.
        for i in range(36):
            if i % 9 == 8:
                continue
            a, b = (i + .08) * math.tau / 36, (i + .84) * math.tau / 36
            polygon([(r * math.cos(angle), .009, -3 + r * math.sin(angle))
                     for r, angle in ((2.47, a), (2.47, b), (2.54, b), (2.54, a))], AMBER)
        for z in (-8.02, -15.94):
            block('Stainless pressure threshold', (0, -.013, z), (2.77, .071, .38), DARK)
            for i in range(9):
                x = -.99 + i * .245
                polygon([(x-.075,.017,z-.133), (x+.042,.017,z-.133), (x+.134,.017,z+.126),
                         (x+.017,.017,z+.126)], AMBER)
        for side in (-1, 1):
            for z in (-13.2, -10.7, -6.6, -.2, 2.2, 5.2):
                x = side * (1.37 if z < -8 else 2.98)
                tip = z - .39
                polygon([(x,.011,tip), (x-.21,.011,tip+.27), (x-.072,.011,tip+.27),
                         (x-.072,.011,tip+.50), (x+.072,.011,tip+.50),
                         (x+.072,.011,tip+.27), (x+.21,.011,tip+.27)], AMBER)

    def pressure_window(side, z):
        inside = side * 5.89
        width, low, high, cut = 2.56, .87, 3.24, .24
        mid = (low + high) / 2
        contour = [(low, -width/2 + cut), (low, width/2 - cut),
                   (low + cut, width/2), (high - cut, width/2),
                   (high, width/2 - cut), (high, -width/2 + cut),
                   (high - cut, -width/2), (low + cut, -width/2)]

        def perimeter(x, yscale, zscale):
            return [(x, mid + (y - mid) * yscale, z + zz * zscale) for y, zz in contour]

        def sleeve(outside, lining, material):
            faces = [(i, (i+1)%8, 8+(i+1)%8, 8+i) for i in range(8)]
            if side < 0:
                faces = [tuple(reversed(face)) for face in faces]
            add(ARCH, material, outside + lining, faces)

        mouth = perimeter(inside - side * .22, 1.119, 1.098)
        rim = perimeter(inside - side * .028, 1, 1)
        seal = perimeter(side * 6.76, 1, 1)
        sleeve(mouth, rim, CLADDING)
        sleeve(rim, seal, STEEL)
        sleeve(perimeter(side * 6.923, 1.103, 1.097), perimeter(side * 6.923, 1, 1), DARK)
        sleeve(seal, perimeter(side * 6.821, .958, .962), RUBBER)
        glass = perimeter(side * 6.84, 1, 1)
        add(ARCH, GLASS, glass, [tuple(range(8)), tuple(reversed(range(8)))])
        for px, y, zz in mouth:
            pin((px - side * .025, y, zz), .042, .072, (1, 0, 0), DARK)
        block('Lower observation sill', (side * 5.72, .809, z), (.46,.132,2.87), DARK)
        block('Window drainage strip', (side * 5.48, .852, z), (.071,.048,2.30), AMBER)

    def service_luminaire(x, y, z, length, width):
        block('Cast lamp body', (x, y, z), (width + .18,.173,length + .245), DARK)
        block('Warm emitter diffuser', (x, y - .097, z), (width,.046,length), LAMP)
        for end in (-1, 1):
            block('Sealed lamp gland', (x,y-.085,z+end*(length/2+.03)), (width+.115,.129,.112), AMBER)
        for i in (-1,0,1):
            block('Protective light baffle', (x,y-.128,z+i*length*.29),(width+.065,.047,.037), STEEL)

    def lab():
        centers = (-6.45,-3.55,-.65,2.25,5.15)
        for side in (-1, 1):
            for z in centers:
                pressure_window(side, z)
                block('Observation side pressure sill', (side*5.978,.404,z), (.31,.81,2.91), HULL)
                block('Pale sloped service cassette', (side*5.784,.413,z+.17), (.11,.513,2.31), CLADDING)
                block('Recessed instrument service cover', (side*5.71,.397,z+.24), (.047,.285,1.97), DARK)
                block('Side toe rub strip', (side*5.56,.137,z), (.095,.093,2.76), AMBER)
                block('Lab window crown', (side*5.964,3.697,z), (.31,.833,2.9), HULL)
                block('Lab crown enamel insert', (side*5.769,3.546,z), (.119,.345,2.443), CLADDING)
                for i in range(5):
                    block('Lab cabinet vent', (side*5.683,.366,z+.74+i*.128),
                          (.043,.176,.056), GRATE)
                block('Window separator spine', (side*5.916,2.066,z+1.436),
                      (.246,2.77,.245), STEEL)
                for yy in (.268,3.361):
                    pin((side*5.661, yy, z-1.159),.044,.09,(1,0,0),DARK)
            block('Lab forward shoulder fairing', (side*5.977,2.0,7.23), (.26,4.0,1.28), HULL)

        for i, z in enumerate((-7.74,-5.18,-2.23,.71,3.65,6.61)):
            # Welded trusses carry the laboratory's high pressure crown. The lower
            # vertical flanges sit outside the gallery's route at port.
            for side in (-1, 1):
                foot = (side * 5.86,.08,z)
                shoulder = (side * 5.67,3.27,z)
                knee = (side * 4.57,4.392,z)
                ridge = (side * 3.37,4.54,z)
                strut(foot,shoulder,.24,.37,STEEL)
                strut(shoulder,knee,.305,.495,STEEL)
                strut(knee,ridge,.287,.47,CLADDING)
                strut((side*5.61,3.344,z-.313),(side*4.532,4.494,z-.30),.13,.095,AMBER)
                block('Welded truss gusset', (side*4.574,4.363,z),(.54,.315,.635), DARK)
                block('Rib foundation shear box', (side*5.802,.141,z),(.246,.28,.505), DARK)
                if i % 2 == 0:
                    service_luminaire(side*3.92,4.18,z+.26,1.42,.267)
            strut((-3.45,4.54,z),(3.45,4.54,z),.235,.43,CLADDING)
            block('Roof isolation saddle', (0,4.538,z),(1.15,.29,.551), DARK)

        for row in range(7):
            z = -6.825 + row * 2.268
            for side in (-1, 1):
                block('Inboard chamfered roof cell', (side*2.06,4.62,z),
                      (2.45,.162,2.186), HULL)
                block('Pale roof inspection hatch', (side*2.045,4.519,z+.26),
                      (1.62,.083,1.43), CLADDING)
                block('Inset roof cell gasket', (side*2.045,4.463,z+.26),
                      (1.19,.028,1.14), GRATE)
                strut((side*3.43,4.622,z),(side*5.64,3.653,z),.145,2.218,HULL)
            if row in (1,3,5):
                block('Crown water-service chase', (0,4.588,z),(.72,.198,1.95), DARK)
            else:
                block('Crown cover', (0,4.715,z),(1.77,.217,2.207), HULL)

        for side in (-1, 1):
            duct([(side*4.856,3.611,-7.84),(side*4.856,3.611,-5.12),
                  (side*4.596,3.782,-4.60),(side*4.596,3.782,6.99)],
                 .162, AMBER, 12)
            duct([(side*5.11,3.691,-7.8),(side*5.11,3.691,6.99)],.092,DARK,10)
            for z in (-7.68,-5.77,-4.36,-1.32,1.85,4.79,6.84):
                for x, y, radius in ((4.86 if z < -4.6 else 4.60,3.61 if z < -4.6 else 3.782,.205),
                                     (5.11,3.691,.124)):
                    pin((side*x,y,z),radius,.167,(0,0,1),DARK,10)

    def pressure_portal(z, upper, amber):
        for side in (-1, 1):
            block('Main pressure partition', (side*3.737,upper*.49,z),(4.411,upper,.257), HULL)
            block('Enamel pressure plate', (side*3.794,upper*.4,z+.159),(3.72,upper*.65,.112), CLADDING)
            block('Dark recessed service well', (side*3.733,upper*.44,z+.233),(2.79,upper*.47,.087), DARK)
            block('Horizontal anti-snag lip', (side*3.723,.23,z+.239),(3.66,.11,.067), STEEL)
            for s in (-1, 1):
                block('Framed upright sleeve', (side*1.51,1.283,z+s*.207),(.21,2.562,.192), STEEL)
                strut((side*1.53,2.48,z+s*.210),(side*1.304,3.065,z+s*.210),.206,.195,
                      AMBER if amber else CLADDING)
                block('Portal jamb shadow', (side*1.672,1.196,z+s*.243),(.117,2.391,.164), DARK)
                block('Portal upright marker', (side*1.518,1.251,z+s*.317),(.056,1.878,.031), AMBER)
            for oy in (.655,1.50,2.33):
                pin((side*1.804,oy,z+.377),.052,.066,(0,0,1),DARK)
        block('Reinforced portal header', (0,upper-.206,z),(3.06,.613,.274), HULL)
        for s in (-1, 1):
            strut((-1.173,3.081,z+s*.21),(1.173,3.081,z+s*.21),.236,.206,STEEL)
            block('Header guide light', (0,3.096,z+s*.331),(1.253,.079,.036), LAMP)

    def transfer():
        for side in (-1, 1):
            if side < 0:
                block('Port transfer tub pressure wall', (-5.985,1.845,-12.04),(.255,3.66,8), HULL)
            spans = [(-16.04,-14.18),(-13.82,-8.04)] if side < 0 else [(-16.04,-15.651),(-12.349,-8.04)]
            for lower, upper in spans:
                width = upper - lower
                z = (lower + upper) / 2
                if side > 0:
                    block('Transfer tub pressure wall', (side*5.985,1.845,z),(.255,3.66,width), HULL)
                if width < 1.1:
                    continue
                panels = max(2, int(width / 1.45))
                for i in range(panels):
                    at = lower + (i+.5) * width / panels
                    block('Transfer equipment enclosure', (side*5.733,1.22,at),(.208,1.931,width/panels-.087), CLADDING)
                    block('Aft cool-rolled service cover', (side*5.608,1.12,at),(.052,1.358,width/panels-.236), STEEL)
                    block('Aft pull handle', (side*5.562,1.115,at+.37),(.077,.337,.058), AMBER)
                    for j in range(5):
                        block('Heat vent', (side*5.565,1.469+j*.061,at-.213),(.035,.027,.473), DARK)
                block('Transfer conduit isolation tray', (side*5.65,2.746,z),(.398,.136,width-.073), DARK)
            if side > 0:
                block('Airlock upper sealed side', (5.982,3.514,-14),(.278,.67,3.31), HULL)
                for z in (-15.631,-12.369):
                    block('Machined airlock reveal', (6.317,1.512,z),(.922,3.134,.211), CLADDING)
                    block('Airlock guide jamb', (5.741,1.514,z),(.368,3.011,.225), AMBER)
                    block('Airlock guide guard', (5.518,1.52,z),(.104,2.689,.134), DARK)
                block('Upper airlock opening beam', (5.861,3.105,-14),(.541,.304,3.542), STEEL)
                block('Airlock lock-box threshold', (6.33,-.028,-14),(1.28,.082,3.39), GRATE)
            if side < 0:
                duct([(-5.47,2.672,-15.95),(-5.47,2.672,-8.22)], .138, AMBER)
                for z in (-15.69,-12.85,-10.11,-8.31):
                    pin((-5.47,2.672,z),.177,.129,(0,0,1),DARK,10)

        for i,z in enumerate((-15.08,-13.19,-11.30,-9.41)):
            # Lower amber lintels cue the shorter service rooms after the tall lab.
            strut((-5.639,3.252,z),(-3.962,3.723,z),.208,.347,STEEL)
            strut((-3.97,3.723,z),(3.98,3.723,z),.212,.351,STEEL)
            strut((3.963,3.723,z),(5.639,3.252,z),.207,.349,STEEL)
            block('Transfer ceiling cassette', (0,3.817,z),(8.29,.187,1.87), HULL)
            for side in (-1,1):
                strut((side*4.03,3.835,z),(side*5.883,3.328,z),.134,1.862,HULL)
                block('Transfer ceiling conduit tray', (side*3.24,3.597,z-.013),
                      (.915,.144,1.865), DARK)
                if (i+side) % 3 != 0:
                    service_luminaire(side*2.21,3.524,z-.118,1.306,.226)
        pressure_portal(-8.07,4.05,True)
        pressure_portal(-15.99,3.77,False)

    def feeding_collar():
        for side in (-1, 1):
            block('Bow chamber pressure shield', (side*4.4,1.964,8.042),
                  (2.94,3.948,.288), HULL)
            block('Bow access corridor plating', (side*4.58,1.882,7.837),
                  (2.37,3.663,.167), CLADDING)
            block('Bow access panel recess', (side*4.61,1.43,7.735),
                  (1.65,2.474,.097), DARK)
            for yy in (.28,.427,.58):
                block('Guarded bow pressure fin', (side*4.60,yy,7.656),
                      (1.56,.057,.074), STEEL)
        block('Enclosed feed passage lintel', (0,4.084,8.046), (6.03,1.001,.349), HULL)
        for z, rx, ry, width, material in ((7.82,2.78,1.97,.207,DARK),
                                           (7.67,2.71,1.907,.114,STEEL),
                                           (7.54,2.636,1.849,.061,AMBER)):
            ring(ARCH,(0,1.85,z),rx,ry,width,material,24)
        for i in range(16):
            angle=i*math.tau/16
            p=(2.907*math.cos(angle),1.85+2.045*math.sin(angle),7.601)
            pin(p,.058,.128,(0,0,1),DARK,7)
            if i not in (3,4,5,11,12,13):
                block('Bow collar clamp', (p[0],p[1],7.69),(.235,.173,.185),CLADDING)

    deck()
    lab()
    transfer()
    feeding_collar()
