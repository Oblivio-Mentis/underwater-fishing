"""Original flat-colour NEREID defense creatures. Coordinates are Unity metres, +Z forward."""

from dataclasses import dataclass, field
from math import cos, pi, sin, sqrt
from pathlib import Path
import json


def add(a, b):
    return tuple(x + y for x, y in zip(a, b))


def neg(a):
    return tuple(-x for x in a)


def mul(a, scale):
    return tuple(x * scale for x in a)


def sub(a, b):
    return add(a, neg(b))


def dot(a, b):
    return sum(x * y for x, y in zip(a, b))


def cross(a, b):
    return (a[1]*b[2] - a[2]*b[1], a[2]*b[0] - a[0]*b[2], a[0]*b[1] - a[1]*b[0])


def unit(a):
    size = sqrt(dot(a, a))
    return mul(a, 1 / size) if size > 1e-8 else (0, 1, 0)


def average(points):
    return tuple(sum(coord) / len(points) for coord in zip(*points))


def paint(value):
    """Decode the sRGB swatches into linear vertex colours for Unity and Blender."""
    rgb = tuple(int(value[i:i+2], 16) / 255 for i in (0, 2, 4))
    return tuple(x / 12.92 if x <= .04045 else ((x+.055) / 1.055) ** 2.4 for x in rgb)


def mix(a, b, t):
    return add(mul(a, 1-t), mul(b, t))


@dataclass
class Surface:
    points: tuple
    normal: tuple
    color: tuple
    emissive: bool


@dataclass
class Part:
    name: str
    pivot: tuple = (0, 0, 0)
    parent: str | None = None
    faces: list = field(default_factory=list)

    def face(self, points, color, outward=None, emissive=False):
        points = tuple(points)
        normal = unit(cross(sub(points[1], points[0]), sub(points[2], points[0])))
        if outward is not None and dot(normal, outward) < 0:
            points = points[::-1]
            normal = neg(normal)
        self.faces.append(Surface(points, normal, color, emissive))

    def loft(self, rings, top, belly=None, sides=10, emissive=False):
        """A faceted tube. Rings are centre x/y/z, lateral radius, vertical radius."""
        belly = belly or top
        loop = []
        for i, ring in enumerate(rings):
            center, w, h = ring[:3], ring[3], ring[4]
            before, after = rings[max(0, i-1)], rings[min(i+1, len(rings)-1)]
            path = unit(sub(after[:3], before[:3]))
            up = (0, 1, 0) if abs(path[1]) < .92 else (0, 0, 1)
            right = unit(cross(up, path))
            vertical = cross(path, right)
            loop.append([add(center, add(mul(right, w*cos(angle)), mul(vertical, h*sin(angle))))
                         for angle in (2*pi*j/sides for j in range(sides))])
        for i, (first, second) in enumerate(zip(loop, loop[1:])):
            mid = average([rings[i][:3], rings[i+1][:3]])
            for j in range(sides):
                k = (j+1) % sides
                face = (first[j], first[k], second[k], second[j])
                light = .5 + .5*sin(2*pi*(j+.5)/sides)
                shift = .88 + .035*((j*7+i*3) % 6)
                self.face(face, mul(mix(belly, top, light), shift), sub(average(face), mid), emissive)
        self.face(loop[0], belly, sub(rings[0][:3], rings[1][:3]), emissive)
        self.face(loop[-1], top, sub(rings[-1][:3], rings[-2][:3]), emissive)

    def wedge(self, perimeter, ridge, color, underside=0.68, thickness=.04, normal=(0, 1, 0)):
        """Sculpted flat-shaded fin or carapace panel with a solid bevel."""
        perimeter = tuple(perimeter)
        center = average(perimeter)
        lower = sub(center, mul(unit(normal), thickness))
        for i, point in enumerate(perimeter):
            nxt = perimeter[(i+1) % len(perimeter)]
            self.face((point, nxt, ridge), mul(color, .85+.06*(i%4)), normal)
            self.face((nxt, point, lower), mul(color, underside), neg(normal))

    def horn(self, base, tip, radius, color, sides=5, end=.004):
        middle = mix(base, tip, .54)
        self.loft([(*base, radius, radius*.83), (*middle, radius*.55, radius*.42),
                   (*tip, end, end)], color, mul(color, .58), sides)

    def light(self, center, direction, size, light, socket):
        direction = unit(direction)
        self.loft([(*sub(center, mul(direction, size*.38)), size*1.09, size*.94),
                   (*center, size, size*.83),
                   (*add(center, mul(direction, size*.21)), size*.76, size*.62)],
                  socket, mul(socket, .43), 8)
        lens = add(center, mul(direction, size*.19))
        self.loft([(*lens, size*.70, size*.49),
                   (*add(lens, mul(direction, size*.18)), size*.51, size*.30)],
                  light, mul(light, .71), 8, True)


@dataclass
class Creature:
    name: str
    body: Part
    tail: Part
    tip: Part
    left: Part
    right: Part
    jaw: Part
    mouth: Part
    school: Part

    @classmethod
    def new(cls, name, tail, tip, fin, jaw, mouth):
        return cls(name, Part("Body"), Part("Tail", tail), Part("TailTip", tip, "Tail"),
                   Part("FinLeft", (-fin[0], fin[1], fin[2])),
                   Part("FinRight", fin), Part("Jaw", jaw),
                   Part("MouthSocket", mouth), Part("School"))

    def parts(self):
        return (self.body, self.tail, self.tip, self.left, self.right, self.jaw, self.mouth)


def side(direction, p):
    return (direction*p[0], p[1], p[2])


def swimming_fin(part, direction, outline, ridge, color, normal=(0, 1, 0), thickness=.045):
    part.wedge([side(direction, p) for p in outline], side(direction, ridge), color, .59,
               thickness, side(direction, normal))


def slit_eye(part, center, direction, width, color, socket, slant=.17):
    normal = unit(direction)
    along = unit(cross((0, 1, 0), normal))
    up = cross(normal, along)

    def lens_point(x, y, depth):
        return add(center, add(mul(along, x*width), add(mul(up, y*width), mul(normal, depth))))

    shape = [(-1, -slant), (-.59, .40), (.46, .28), (1, slant), (.36, -.35), (-.44, -.45)]
    rim = [lens_point(x*1.29, y*1.65, 0) for x,y in shape]
    part.wedge(rim, lens_point(.08, .31, width*.2), socket, .47, width*.34, normal)
    lens = [lens_point(x, y, width*.27) for x,y in shape]
    part.face(lens, color, normal, True)
    part.face([lens_point(x*.62, y*.58, width*.31) for x,y in shape],
              mix(color, paint("D8F0D9"), .34), normal, True)


def school_eye(part, x, y, z, color):
    for sign in (-1, 1):
        center = (sign*x, y, z)
        outward = (sign, .16, .4)
        rim = [add(center, (sign*.009, dy, dz)) for dy,dz in
               ((0, -.032), (.022, -.008), (.011, .035), (-.016, .024), (-.022, -.016))]
        tip = add(center, (sign*.025, 0, .004))
        for i, first in enumerate(rim):
            part.face((first, rim[(i+1)%len(rim)], tip), color, outward, True)


def rammer():
    c = Creature.new("Rammer", (0, 0, -.71), (0, .006, -1.13), (.40, -.10, .04),
                     (0, -.241, .39), (0, -.245, .96))
    steel, ventral, ivory, scale = map(paint, ("384345", "1C242A", "B6B09A", "635A4A"))
    obsidian, welt, eye = map(paint, ("282A2A", "7A563D", "FFC25B"))
    profile = [(0, 0, -.82, .105, .14), (0, .028, -.54, .248, .26),
               (0, .05, -.10, .424, .39), (0, .065, .30, .484, .415),
               (0, .075, .63, .438, .332), (0, .064, .855, .357, .245),
               (0, .079, .947, .308, .166)]
    c.body.loft(profile, steel, ventral, 12)
    c.body.wedge([(-.21,.184,.94),(-.12,.258,.905),(.12,.258,.905),(.21,.184,.94),
                  (.13,.118,1.012),(-.13,.118,1.012)],
                 (0,.176,1.055), scale, .70, .056, (0,.20,1))
    for z,y,w in [(-.44,.264,.097),(-.26,.338,.123),(-.037,.468,.157),(.243,.498,.187)]:
        c.body.wedge([(-w,y,z+.15),(0,y+.04,z+.19),(w,y,z+.15),
                      (w*.59,y-.022,z-.14),(-w*.66,y-.026,z-.125)],
                     (.012,y+.15,z+.042), obsidian, thickness=.073)
    def shell_point(sign, angle, z, spacing):
        first, second = next((a, b) for a,b in zip(profile, profile[1:]) if a[2] <= z <= b[2])
        _, y, _, width, height = mix(first, second, (z-first[2])/(second[2]-first[2]))
        return (sign*(width+spacing)*cos(angle), y+(height+spacing)*sin(angle), z)

    for s in (-1, 1):
        for k, (start, end) in enumerate(((-.46, -.18), (-.15, .12), (.15, .41),
                                          (.44, .64), (.67, .867))):
            # The armor stands clear of the fleshy body and follows its tapered section.
            # There is a deliberate dark seam between both ranks of opposing plates.
            for row, (lower, upper) in enumerate(((.22, .80), (.86, 1.54))):
                center = (lower+upper)*.5
                outline = [(lower, start+.041), (center, start), (upper, start+.016),
                           (upper, end-.027), (center, end), (lower, end-.019)]
                c.body.wedge([shell_point(s, a, z, .023) for a,z in outline],
                             shell_point(s, center, start+(end-start)*.4, .055),
                             mix(scale, ivory, .10+k*.085+row*.04), .46, .062,
                             (s*cos(center), sin(center), 0))
        slit_eye(c.body, side(s, (.436, .047, .675)), (s, .06, .35), .049, eye, obsidian, -.30*s)
        c.body.horn(side(s, (.426, .108, .681)), side(s, (.492, .13, .499)), .054, obsidian)
        c.body.horn(side(s, (.371, -.056, .833)), side(s, (.430, -.190, 1.021)), .114, scale)
        c.body.horn(side(s, (.383, -.143, .899)), side(s, (.447, -.337, 1.065)), .065, ivory)
        c.body.horn(side(s, (.443, -.026, .54)), side(s, (.613, -.155, .21)), .095, scale)
        c.body.horn(side(s, (.421, -.091, .30)), side(s, (.563, -.20, .008)), .075, welt)
        c.body.horn(side(s, (.358, -.125, .053)), side(s, (.487, -.196, -.202)), .053, obsidian)
        fin = c.left if s < 0 else c.right
        swimming_fin(fin, s, [(.35, -.071, .18), (.53, -.155, .051), (.86, -.246, -.37),
                              (.565, -.235, -.347), (.35, -.149, -.23)],
                     (.55, -.119, -.15), scale, thickness=.068)
        for i in range(5):
            z = .922-.088*i
            c.jaw.horn(side(s, (.204+.022*i, -.246, z)),
                       side(s, (.183+.018*i, -.129-.016*i, z+.073)), .035-i*.002, ivory)
            c.body.horn(side(s, (.186+.025*i, -.071, z-.011)),
                        side(s, (.166+.022*i, -.161+.01*i, z+.025)), .030-i*.003, ivory)
    c.jaw.loft([(0, -.211, .341, .256, .062), (0, -.304, .625, .296, .093),
                (0, -.298, .854, .242, .085), (0, -.218, .979, .153, .045)],
               steel, ventral, 12)
    c.tail.loft([(0, 0, -.658, .158, .166), (0, .025, -.89, .106, .139),
                 (0, .01, -1.158, .073, .092)], steel, ventral, 9)
    c.tip.loft([(0, .01, -1.075, .083, .106), (0, .013, -1.31, .038, .084)], steel, ventral, 8)
    c.tip.wedge([(0, .07, -1.10), (0, .44, -1.60), (0, .119, -1.51),
                 (0, -.052, -1.41), (0, -.30, -1.56), (0, -.17, -1.26), (0, -.08, -1.13)],
                (.058, .023, -1.31), scale, thickness=.056, normal=(1, 0, 0))
    c.school.loft([profile[i] for i in (0, 1, 2, 4, 6)], steel, ventral, 6)
    c.school.wedge([(0, .11, -.76), (0, .35, -1.49), (0, -.30, -1.46), (0, -.12, -.79)],
                  (.057, .003, -1.18), steel, thickness=.045, normal=(1, 0, 0))
    for z,y in ((-.03,.45),(.27,.49),(.55,.41)):
        c.school.wedge([(-.17,y,z+.08),(.17,y,z+.085),(0,y+.14,z-.068)],
                       (.015,y+.075,z+.022), scale, thickness=.037)
    school_eye(c.school, .44, .062, .68, eye)
    return c


def remora():
    c = Creature.new("Remora", (0, -.034, -.44), (.062, -.045, -.98),
                     (.105, -.008, .11), (0, -.125, .625), (0, .006, .939))
    hide, muscle, flesh, ridge, glow = map(paint, ("32343D", "1E182C", "6F3E58", "C0A394", "F0609A"))
    sockets, enamel = paint("3E2946"), paint("D3C9A6")
    front = [(0, -.044, -.55, .106, .113), (0, -.022, -.13, .163, .146),
             (0, .026, .283, .161, .169), (0, .007, .565, .178, .163),
             (0, .005, .749, .238, .214), (0, .001, .837, .207, .196)]
    c.body.loft(front, hide, muscle, 12)
    # Open recessed throat. Sucker crown segments leave a real hole with visible walls.
    lips = []
    for i in range(14):
        a, b = 2*pi*i/14, 2*pi*(i+1)/14
        points = [(cos(t)*radius, sin(t)*radius*.88, z)
                  for t, radius, z in ((a, .215, .819), (b, .215, .819),
                                       (b, .253, .899), (a, .253, .899))]
        inner = [(cos(t)*.159, sin(t)*.140, .932) for t in (a, b)]
        deep = [(cos(t)*.102, sin(t)*.09, .839) for t in (a, b)]
        lip = c.body if sin((a+b)*.5) > -.40 else c.jaw
        lip.face(points, mul(flesh, .8+.07*(i%5)), (cos(a), sin(a), .2))
        lip.face((points[3], points[2], inner[1], inner[0]), ridge, (0, 0, 1))
        lip.face((inner[0], inner[1], deep[1], deep[0]), muscle, (0, 0, 1))
        theta = .5*(a+b)
        crown = .018*sin(theta*5)
        lip.wedge((points[0], points[1], points[2], points[3]),
                  (cos(theta)*(.269+crown), sin(theta)*(.234+crown), .887+.015*(i%3)),
                  mix(flesh, ridge, .25), .54, .035, (cos(theta), sin(theta), .12))
        lip.horn((cos(theta)*.193, sin(theta)*.173, .929),
                 (cos(theta)*.102, sin(theta)*.082, 1.006-.018*(i%2)), .032, enamel)
        lip.horn((cos(theta+.10)*.127, sin(theta+.10)*.109, .897),
                 (cos(theta+.24)*.068, sin(theta+.24)*.062, .958), .019, ridge)
        lips.append((cos(a)*.101, sin(a)*.089, .837))
    c.body.face(lips, paint("141215"), (0, 0, 1))
    trunk = [(0,-.04,-.33,.135,.122),(.043,-.035,-.62,.114,.11),(.062,-.045,-.998,.076,.071)]
    taper = [(.062,-.045,-.90,.082,.08),(.119,-.013,-1.24,.047,.058),
             (.238,.039,-1.53,.024,.039),(.327,.068,-1.68,.004,.014)]
    c.tail.loft(trunk, hide, muscle, 10)
    c.tip.loft(taper, hide, muscle, 8)
    for sign in (-1,1):
        for segment, rings, positions in (
            (c.body,front,((.42,.035),(.208,.031),(-.019,.025),(-.228,.021))),
            (c.tail,trunk,((-.55,.018),(-.79,.018))),
            (c.tip,taper,((-1.117,.017),)),
        ):
            for z,radius in positions:
                a,b=next((a,b) for a,b in zip(rings,rings[1:]) if min(a[2],b[2])<=z<=max(a[2],b[2]))
                x,y,_,w,h=mix(a,b,(z-a[2])/(b[2]-a[2]))
                segment.light((x+sign*w*.95,y+h*.20,z),(sign,.35,.08),radius*.76,glow,muscle)
                segment.horn((x+sign*w*.71,y+h*.36,z-.011),
                             (x+sign*(w*.88+.051),y+h+.039,z-.126),radius*1.78,sockets)
                segment.horn((x+sign*w*.77,y-h*.36,z-.04),
                             (x+sign*(w+.035),y-h-.024,z-.166),radius*.99,flesh)
        c.body.horn(side(sign, (.157,.137,.515)),side(sign,(.224,.183,.748)),.06,sockets)
        fin = c.left if sign < 0 else c.right
        swimming_fin(fin, sign, [(.093, .007, .32), (.228, -.067, .135), (.27, -.035, -.43),
                                 (.132, .012, -.32), (.102, .043, .03)],
                     (.165, -.022, .058), flesh, thickness=.025)
    c.school.loft([(0, 0, -1.43, .014, .018), (0, 0, -.64, .10, .10), *front[1::2]],
                  hide, muscle, 6)
    c.school.loft([(0,.003,.63,.21,.19),(0,.005,.79,.265,.231),
                   (0,.001,.897,.207,.192)],flesh,muscle,7)
    school_eye(c.school, .158, .067, .58, glow)
    return c


def thief():
    c = Creature.new("Thief", (0, .038, -.429), (0, -.036, -.83), (.206, -.069, .146),
                     (0, -.13, .403), (0, -.11, .836))
    teal, belly, amber, edge, glow = map(paint, ("35605B", "1D2E30", "77816A", "B0BA95", "CAFC8F"))
    chitin, bone = map(paint, ("26383A", "CED4AA"))
    hull = [(0, .043, -.54, .091, .092), (0, .067, -.29, .203, .173),
            (0, .067, .055, .284, .205), (0, .075, .333, .252, .181),
            (0, .048, .48, .178, .117), (0, .023, .613, .118, .067)]
    c.body.loft(hull, teal, belly, 12)
    c.body.wedge([(-.173,.177,.266),(-.067,.17,.555),(0,.061,.734),(.067,.17,.555),
                  (.173,.177,.266),(.055,.247,.16),(-.055,.247,.16)],
                 (0,.225,.34),chitin, thickness=.038)
    c.jaw.loft([(0, -.105, .323, .125, .06), (0, -.134, .526, .126, .05),
                (0, -.052, .681, .052, .021), (0, -.012, .703, .022, .012)],
               edge, belly, 8)
    for s in (-1, 1):
        slit_eye(c.body, side(s, (.207, .132, .411)), (s, .20, .53), .069, glow, chitin, .31*s)
        c.body.horn(side(s, (.215,.18,.355)),side(s,(.30,.225,.109)),.055,chitin)
        c.body.horn(side(s, (.219,.004,.303)),side(s,(.323,-.053,.074)),.064,amber)
        c.body.horn(side(s, (.135,.001,.516)),side(s,(.09,-.130,.608)),.031,bone)
        c.jaw.horn(side(s,(.095,-.14,.549)),side(s,(.084,-.068,.64)),.026,bone)
        fin = c.left if s < 0 else c.right
        fin.loft([(*side(s, (.168, -.075, .155)), .075, .062),
                  (*side(s, (.329, -.151, .276)), .063, .05),
                  (*side(s, (.469, -.13, .47)), .047, .046),
                  (*side(s, (.46, -.094, .638)), .052, .049)], teal, belly, 8)
        for offset in (-.075, .091):
            claw = [(.458+offset*.48, -.092, .585), (.475+offset*1.4, -.074, .757),
                    (.473+offset*1.7, -.106, .94), (.413+offset*.67, -.153, 1.054),
                    (.386+offset*.24, -.215, 1.077)]
            fin.loft([(*side(s, point), rad, rad*.79)
                      for point, rad in zip(claw, (.065, .075, .043, .023, .004))], amber, chitin, 7)
            for i, (x,y,z) in enumerate(claw[1:4]):
                fin.horn(side(s,(x,y-.023,z)),
                         side(s,(x-offset*.6,y-.048,z+.043)), .024-i*.004, bone)
        fin.horn(side(s,(.387,-.109,.481)), side(s,(.553,-.035,.415)), .052, chitin)
        fin.horn(side(s,(.344,-.116,.355)), side(s,(.476,-.024,.229)), .043, amber)
        swimming_fin(fin, s, [(.2, -.013, .121), (.432, .014, .163), (.779, -.047, -.151),
                              (.538, -.046, -.302), (.366, -.016, -.225), (.25, -.06, -.17)],
                     (.423, .026, -.135), edge, thickness=.035)
    c.tail.loft([(0, .04, -.381, .141, .127), (0, .055, -.596, .084, .089),
                 (0, -.037, -.876, .054, .058)], teal, belly, 9)
    c.tip.wedge([(0, .014, -.741), (0, .273, -1.251), (0, .045, -1.09),
                 (0, -.201, -1.284), (0, -.106, -.915)],
                (.052, -.019, -.945), amber, thickness=.037, normal=(1, 0, 0))
    c.school.loft(hull[::2]+[hull[-1]], teal, belly, 6)
    for s in (-1, 1):
        swimming_fin(c.school, s, [(.185, .016, .249), (.56, -.035, .12), (.54, -.055, -.22),
                                    (.23, -.03, -.176)], (.41, .018, -.033), teal, thickness=.018)
    for s in (-1,1):
        c.school.horn(side(s,(.23,-.076,.14)),side(s,(.472,-.145,.97)),.062,amber,4,.008)
    school_eye(c.school, .204, .11, .41, glow)
    return c


def abductor():
    c = Creature.new("Abductor", (0, .10, -.63), (0, -.01, -1.05), (.217, -.037, .27),
                     (0, -.11, .432), (0, -.08, .935))
    mauve, ventral, web, claw, glow = map(paint, ("51405E", "272237", "614352", "B0A09C", "C8ACFF"))
    shell, hook = map(paint, ("382A46", "D5C9B3"))
    mantle = [(0, .064, -.83, .092, .119), (0, .13, -.58, .27, .256),
              (0, .152, -.31, .362, .325), (0, .117, -.011, .344, .30),
              (0, .048, .236, .241, .225), (0, -.015, .453, .244, .156),
              (0, -.038, .57, .185, .103)]
    c.body.loft(mantle, mauve, ventral, 12)
    c.body.wedge([(-.22,.221,.147),(-.256,.282,-.152),(-.09,.378,-.449),(.09,.378,-.449),
                  (.256,.282,-.152),(.22,.221,.147),(0,.146,.536)],
                 (0,.375,.059), shell, thickness=.047)
    c.jaw.loft([(0, -.102, .337, .168, .085), (0, -.163, .531, .141, .079),
                (0, -.123, .663, .058, .051), (0, -.08, .709, .016, .018)], claw, ventral, 8)
    for s in (-1, 1):
        slit_eye(c.body,side(s,(.217,.142,.414)),(s,.46,.52),.060,glow,shell,-.21*s)
        for x,y,z,radius in ((.174,.207,.395,.032),(.143,.223,.336,.018),(.201,.076,.532,.024)):
            c.body.light(side(s,(x,y,z)),(s,.48,.51),radius,glow,ventral)
        for i, (x,y,z) in enumerate(((.247,.194,.286),(.29,.264,.098),(.307,.314,-.114),(.284,.343,-.319))):
            c.body.horn(side(s,(x*.88,y-.054,z)),
                        side(s,(x+.089+.024*i,y+.08+.023*i,z-.125-.05*i)),.063-.004*i,shell)
        c.body.horn(side(s,(.183,-.014,.565)),side(s,(.106,-.116,.72)),.038,hook)
        c.jaw.horn(side(s,(.140,-.150,.56)),side(s,(.10,-.069,.684)),.032,hook)
        # Mantle vanes remain independent of the capture arms.
        swimming_fin(c.body, s, [(.198,.196,-.627),(.402,.193,-.813),(.751,.213,-.535),
                                 (.603,.127,-.511),(.712,.035,-.334),(.537,.055,-.348),
                                 (.56,-.006,-.157),(.32,.039,-.117),(.233,.083,-.256)],
                     (.427,.243,-.473), web, thickness=.072)
        for k in range(2):
            c.body.horn(side(s, (.311-.036*k, .239, -.29+.18*k)),
                        side(s, (.398-.019*k, .288, -.445+.15*k)), .046, web, 6)
        arm = c.left if s < 0 else c.right
        for i, curve in enumerate((
            ((.197, .016, .292), (.392, .04, .508), (.691, .007, .71),
             (.844, -.14, .949), (.811, -.291, 1.121), (.64, -.35, 1.198), (.52, -.312, 1.148)),
            ((.203, -.061, .283), (.379, -.183, .50), (.582, -.28, .708),
             (.605, -.417, .916), (.483, -.456, 1.028), (.338, -.407, 1.045)),
            ((.137, -.133, .255), (.222, -.24, .45), (.321, -.336, .687),
             (.268, -.398, .896), (.17, -.351, .969)),
        )):
            widths = (.103, .095, .079, .064, .067, .049, .013)
            arm.loft([(*side(s, point), widths[k]*(1-.19*i), widths[k]*(.83-.11*i))
                      for k, point in enumerate(curve)], mauve, ventral, 8)
            for k, point in enumerate(curve[2:-1], 2):
                y = point[1]-.019
                inner = point[0]-.044
                arm.horn(side(s, (inner, y, point[2])),
                         side(s, (inner-.087, y-.024, point[2]+.091)), .039,
                         mix(web, hook, .25+.18*(k-1)))
            last, before = curve[-1], curve[-2]
            arm.horn(side(s,before), side(s,(last[0]-.084,last[1]+.046,last[2]+.065)),
                     .048, hook, 6, .003)
    c.tail.loft([(0, .078, -.571, .193, .192), (0, .089, -.79, .126, .121),
                 (0, -.012, -1.013, .076, .083), (0, -.033, -1.155, .042, .069)],
                mauve, ventral, 10)
    c.tip.wedge([(0, -.04, -.991), (0, .152, -1.348), (0, -.004, -1.275),
                 (0, -.193, -1.325), (0, -.092, -1.082)],
                (.04, -.02, -1.168), web, thickness=.03, normal=(1, 0, 0))
    c.school.loft(mantle[::2], mauve, ventral, 7)
    for s in (-1, 1):
        swimming_fin(c.school, s, [(.179, .012, .294), (.72, -.058, .633), (.63, -.261, 1.125),
                                    (.405, -.297, .96), (.305, -.114, .667)],
                     (.406, -.049, .651), mauve, thickness=.039)
    for s in (-1,1):
        c.school.wedge([side(s,p) for p in ((.14,.24,-.63),(.70,.23,-.44),(.49,.055,-.21),(.22,.07,-.18))],
                       side(s,(.40,.19,-.42)),web,thickness=.045)
    school_eye(c.school, .232, .136, .409, glow)
    return c


def stalker():
    data = json.loads(Path(__file__).with_name("StalkerGeometry.json").read_text(encoding="utf-8"))
    colors = [paint(swatch) for swatch in data["palette"]]
    rig = data["parts"]
    c = Creature.new("Stalker", rig["Tail"]["pivot"], rig["TailTip"]["pivot"],
                     rig["FinRight"]["pivot"], rig["Jaw"]["pivot"], (0, -.391, 1.11))
    for part in c.parts():
        if part.name not in rig:
            continue
        geometry = rig[part.name]
        part.pivot = tuple(geometry["pivot"])
        points = [tuple(point) for point in geometry["verts"]]
        for swatch, *corners in geometry["faces"]:
            part.face([points[index] for index in corners], colors[swatch], emissive=(swatch == 9))

    slate, belly, crest, bone, glow, well = (
        paint(value) for value in ("3A384F","252331","514759","E7DBBC","51E9F5","221A2D"))
    # The eyes in the reference are painted. These sit in the original eye sockets.
    slit_eye(c.body, (.211,-.052,.992), (.77,.43,.46), .067, glow, well, -.28)
    slit_eye(c.body, (-.307,-.052,1.001), (-.74,.42,.46), .064, glow, well, .28)
    for sign in (-1,1):
        shift = .048 if sign < 0 else 0
        for index in range(2):
            c.body.wedge([side(sign,p) for p in (
                (.277+shift,-.209-index*.044,.806-index*.036),
                (.309+shift,-.236-index*.047,.734-index*.030),
                (.321+shift,-.273-index*.012,.700-index*.010))],
                side(sign,(.313+shift,-.228-index*.039,.726-index*.030)),
                crest, thickness=.019, normal=(sign,.05,.20))

    c.school.loft([(0, .052,-.652,.103,.138),(0,.024,.154,.323,.35),
                   (0,.003,.685,.31,.307),(0,-.075,1.27,.195,.10)],slate,belly,5)
    c.school.loft([(0,.042,-.634,.12,.128),(-.075,.302,-.927,.101,.103),
                   (-.119,.748,-1.033,.08,.08),(-.11,1.05,-1.117,.04,.057)],
                  slate, crest, 5)
    c.school.wedge([(-.417,1.221,-1.15),(-.105,1.149,-1.144),(.26,1.29,-1.20),
                    (.10,1.057,-1.234),(-.098,.99,-1.097),(-.341,1.03,-1.21)],
                   (-.087,1.094,-1.222),crest,thickness=.036,normal=(0,0,-1))
    c.school.wedge([(0,.336,.431),(0,.667,.316),(0,.563,.142),
                    (0,.613,-.223),(0,.417,-.227),(0,.437,-.525),(0,.277,-.604)],
                   (.044,.393,-.02),crest,thickness=.048,normal=(1,0,0))
    c.school.loft([(0,-.273,.729,.22,.159),(0,-.573,1.195,.135,.076)],slate,belly,5)
    for sign in (-1,1):
        swimming_fin(c.school,sign,[(.162,-.07,.625),(.387,-.271,.421),(.478,-.737,.152),
                                   (.263,-.459,.145),(.214,-.185,.214)],
                     (.308,-.270,.322),crest,thickness=.051)
        c.school.wedge([side(sign,p) for p in ((.203,-.222,.814),(.271,-.418,.72),
                        (.292,-.827,.418),(.221,-.405,.654))],
                       side(sign,(.263,-.433,.691)),slate,thickness=.021,normal=(sign,0,0))
        c.school.wedge([side(sign,p) for p in ((.115,-.151,1.161),(.158,-.16,1.18),(.142,-.313,1.245))],
                        side(sign,(.147,-.187,1.21)),bone,thickness=.014,normal=(sign,0,0))
    school_eye(c.school,.239,-.037,1.002,glow)
    # Bend the original FBX's upright tail back along the swim axis. The same deformation
    # moves both overlapping joint surfaces, both pivots and the school silhouette.
    base_y, base_z = c.tail.pivot[1:]

    def lowered(point):
        x, y, z = point
        progress = max(0, min(1, (-z + base_z) / .65))
        angle = -52*pi/180 * progress*progress*(3-2*progress)
        height, length = y-base_y, z-base_z
        return (x, base_y+cos(angle)*height-sin(angle)*length,
                base_z+sin(angle)*height+cos(angle)*length)

    for part in [*c.parts(), c.school]:
        part.pivot = lowered(part.pivot)
        faces, part.faces = part.faces, []
        for surface in faces:
            part.face(tuple(lowered(point) for point in surface.points), surface.color,
                      emissive=surface.emissive)
    return c


def creatures():
    return [rammer(), remora(), thief(), abductor(), stalker()]
