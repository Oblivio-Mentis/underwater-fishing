"""Original flat-colour NEREID defense creatures. Coordinates are Unity metres, +Z forward."""

from dataclasses import dataclass, field
from math import cos, pi, sin, sqrt


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
                     (0, -.205, .43), (0, -.185, .97))
    steel, ventral, ivory, scale = map(paint, ("597579", "314347", "A7AFA0", "718687"))
    eye = paint("F2B36A")
    profile = [(0, 0, -.82, .105, .14), (0, .028, -.54, .248, .26),
               (0, .05, -.10, .424, .39), (0, .065, .30, .484, .415),
               (0, .075, .63, .438, .332), (0, .064, .855, .357, .245),
               (0, .055, .933, .266, .176)]
    c.body.loft(profile, steel, ventral, 12)
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
                             mix(scale, ivory, .14+k*.086+row*.04), .53, .047,
                             (s*cos(center), sin(center), 0))
        c.body.light(side(s, (.433, .085, .645)), (s, .16, .31), .040, eye, paint("253335"))
        c.body.horn(side(s, (.418, -.047, .55)), side(s, (.545, -.128, .18)), .074, scale)
        c.body.horn(side(s, (.429, -.090, .485)), side(s, (.533, -.17, .135)), .051, ivory)
        fin = c.left if s < 0 else c.right
        swimming_fin(fin, s, [(.35, -.071, .18), (.53, -.155, .051), (.86, -.246, -.37),
                              (.565, -.235, -.347), (.35, -.149, -.23)],
                     (.55, -.119, -.15), scale, thickness=.068)
        for i in range(4):
            c.jaw.horn(side(s, (.251-.023*i, -.168, .822-.079*i)),
                       side(s, (.246-.019*i, -.086, .819-.073*i)), .022, ivory)
    c.jaw.loft([(0, -.196, .385, .252, .065), (0, -.218, .64, .293, .105),
                (0, -.196, .849, .225, .07), (0, -.146, .948, .138, .036)],
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
    school_eye(c.school, .36, .09, .69, eye)
    return c


def remora():
    c = Creature.new("Remora", (0, -.034, -.44), (.062, -.045, -.98),
                     (.105, -.008, .11), (0, -.125, .625), (0, .006, .939))
    hide, muscle, flesh, ridge, glow = map(paint, ("384E50", "27283A", "6F555B", "947E6D", "DD9475"))
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
        lip.horn((cos(theta)*.174, sin(theta)*.153, .925),
                 (cos(theta)*.133, sin(theta)*.119, .952-.012*(i%2)), .025, ridge)
        lips.append((cos(a)*.101, sin(a)*.089, .837))
    c.body.face(lips, paint("141215"), (0, 0, 1))
    c.tail.loft([(0, -.04, -.33, .135, .122), (.043, -.035, -.62, .114, .11),
                 (.062, -.045, -.998, .076, .071)], hide, muscle, 10)
    c.tip.loft([(.062, -.045, -.90, .082, .08), (.119, -.013, -1.24, .047, .058),
                (.238, .039, -1.53, .024, .039), (.327, .068, -1.68, .004, .014)],
               hide, muscle, 8)
    for sign in (-1, 1):
        for k, (x, y, z, radius) in enumerate(((.16, .078, .42, .035), (.16, .04, .208, .031),
                                                (.156, .028, -.019, .025), (.128, .01, -.228, .021),
                                                (.103, -.015, -.55, .018), (.105, -.037, -.79, .018),
                                                (.119, -.021, -1.117, .017))):
            segment = c.body if k <= 3 else c.tail if k <= 5 else c.tip
            segment.light((sign*x+max(0, -z-.35)*.074, y, z), (sign, .35, .08),
                          radius, glow, muscle)
        fin = c.left if sign < 0 else c.right
        swimming_fin(fin, sign, [(.093, .007, .32), (.228, -.067, .135), (.27, -.035, -.43),
                                 (.132, .012, -.32), (.102, .043, .03)],
                     (.165, -.022, .058), flesh, thickness=.025)
    c.school.loft([(0, 0, -1.43, .014, .018), (0, 0, -.64, .10, .10), *front[1::2]],
                  hide, muscle, 6)
    school_eye(c.school, .16, .067, .58, glow)
    return c


def thief():
    c = Creature.new("Thief", (0, .038, -.429), (0, -.036, -.83), (.206, -.069, .146),
                     (0, -.13, .403), (0, -.11, .836))
    teal, belly, amber, edge, glow = map(paint, ("669997", "344C58", "A5916A", "9BB8AB", "BEF1C2"))
    hull = [(0, .043, -.54, .091, .092), (0, .067, -.29, .203, .173),
            (0, .067, .055, .284, .205), (0, .075, .333, .252, .181),
            (0, .048, .48, .178, .117), (0, .023, .613, .118, .067)]
    c.body.loft(hull, teal, belly, 12)
    c.jaw.loft([(0, -.105, .323, .125, .06), (0, -.092, .526, .126, .042),
                (0, -.052, .681, .052, .021), (0, -.012, .703, .022, .012)],
               edge, belly, 8)
    for s in (-1, 1):
        c.body.light(side(s, (.202, .133, .367)), (s, .32, .43), .073, glow, paint("295751"))
        c.body.horn(side(s, (.239, .085, .269)), side(s, (.317, .103, .082)), .04, amber)
        fin = c.left if s < 0 else c.right
        fin.loft([(*side(s, (.168, -.075, .155)), .075, .062),
                  (*side(s, (.329, -.151, .276)), .063, .05),
                  (*side(s, (.469, -.13, .47)), .047, .046),
                  (*side(s, (.46, -.094, .638)), .052, .049)], teal, belly, 8)
        for offset in (-.056, .065):
            claw = [(.46+offset*.72, -.097, .598), (.465+offset*1.8, -.096, .736),
                    (.432+offset*1.34, -.123, .857), (.382+offset*1.02, -.133, .89)]
            fin.loft([(*side(s, point), rad, rad*.75)
                      for point, rad in zip(claw, (.056, .058, .031, .004))], amber, belly, 7)
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
    school_eye(c.school, .196, .109, .378, glow)
    return c


def abductor():
    c = Creature.new("Abductor", (0, .10, -.63), (0, -.01, -1.05), (.217, -.037, .27),
                     (0, -.11, .432), (0, -.08, .935))
    mauve, ventral, web, claw, glow = map(paint, ("695D79", "332F48", "94767E", "A7938B", "ABCCFF"))
    mantle = [(0, .064, -.83, .092, .119), (0, .13, -.58, .27, .256),
              (0, .152, -.31, .362, .325), (0, .117, -.011, .344, .30),
              (0, .048, .236, .241, .225), (0, -.015, .453, .244, .156),
              (0, -.038, .57, .185, .103)]
    c.body.loft(mantle, mauve, ventral, 12)
    c.jaw.loft([(0, -.102, .337, .168, .085), (0, -.163, .531, .141, .079),
                (0, -.123, .663, .058, .051), (0, -.08, .709, .016, .018)], claw, ventral, 8)
    for s in (-1, 1):
        c.body.light(side(s, (.219, .113, .412)), (s, .42, .58), .057, glow, paint("302B48"))
        c.body.light(side(s, (.208, .045, .529)), (s, .27, .63), .026, glow, ventral)
        # Wrap-around mantle wings and ribs stay on the body, away from the animated capture arms.
        swimming_fin(c.body, s, [(.198, .196, -.627), (.402, .193, -.813), (.692, .158, -.48),
                                 (.519, .019, -.26), (.32, .039, -.117), (.233, .083, -.256)],
                     (.417, .226, -.39), web, thickness=.07)
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
                         side(s, (inner-.049, y-.012, point[2]+.058)), .033,
                         mix(web, claw, .14*(k-1)))
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
    school_eye(c.school, .22, .028, .398, glow)
    return c


def stalker():
    c = Creature.new("Stalker", (0, .018, -.52), (.01, .052, -.988), (.157, -.034, .12),
                     (0, -.074, .412), (0, -.044, .85))
    hide, belly, rib, edge, glow = map(paint, ("3B5360", "202936", "536670", "78828A", "B8D9EF"))
    spine = [(0, .035, -.66, .075, .106), (0, .072, -.41, .132, .135),
             (0, .087, -.015, .194, .179), (0, .089, .278, .192, .194),
             (0, .072, .536, .161, .151), (0, .059, .747, .114, .089),
             (0, .04, .845, .07, .043)]
    c.body.loft(spine, hide, belly, 10)
    # Tall segmented knife fin. Its dark opening is cut into the silhouette.
    for z, height in ((.124, .313), (-.104, .418), (-.29, .391), (-.469, .305)):
        c.body.wedge([(0, .19, z+.108), (0, height, z-.092), (0, height-.019, z-.18),
                      (0, .18, z-.244)], (.031, .236, z-.093), rib,
                     thickness=.027, normal=(1, 0, 0))
    for s in (-1, 1):
        c.body.light(side(s, (.147, .113, .566)), (s, .28, .53), .045, glow, belly)
        c.body.horn(side(s, (.16, .156, .536)), side(s, (.191, .176, .412)), .036, rib, 5)
        c.body.light(side(s, (.186, .11, .254)), (s, .21, .13), .014, glow, belly)
        fin = c.left if s < 0 else c.right
        swimming_fin(fin, s, [(.143, -.013, .258), (.359, -.026, .185),
                              (.68, -.166, -.498), (.455, -.114, -.331), (.17, -.029, -.138)],
                     (.344, .022, -.106), rib, thickness=.045)
        for i in range(5):
            z = .755-i*.064
            x = .072+.014*i
            c.jaw.horn(side(s, (x, -.061-.01*i, z)),
                       side(s, (x*.94, -.009-.003*i, z+.011)), .016, edge)
    c.jaw.loft([(0, -.071, .385, .126, .065), (0, -.104, .552, .143, .075),
                (0, -.085, .727, .101, .05), (0, -.037, .85, .049, .022)], rib, belly, 10)
    c.tail.loft([(0, .04, -.435, .127, .132), (0, .031, -.734, .096, .093),
                 (.01, .05, -1.045, .035, .058)], hide, belly, 9)
    c.tip.loft([(.01, .051, -.953, .047, .063), (.034, .065, -1.245, .021, .037),
                (.06, .124, -1.59, .004, .013)], hide, belly, 7)
    c.tip.wedge([(.011, .049, -.969), (.055, .376, -1.423), (.04, .14, -1.316),
                 (.038, -.167, -1.469), (.019, -.073, -1.133)],
                (.067, .045, -1.224), rib, thickness=.04, normal=(1, 0, 0))
    c.school.loft([spine[0], *spine[2::2]], hide, belly, 6)
    c.school.wedge([(0, .12, .195), (0, .41, -.182), (0, .295, -.61), (0, .103, -.65)],
                  (.036, .174, -.267), rib, thickness=.03, normal=(1, 0, 0))
    c.school.wedge([(0, .103, -.58), (0, .24, -1.42), (0, -.119, -1.42), (0, -.065, -.61)],
                  (.036, .046, -1.04), hide, thickness=.022, normal=(1, 0, 0))
    school_eye(c.school, .151, .107, .56, glow)
    return c


def creatures():
    return [rammer(), remora(), thief(), abductor(), stalker()]
