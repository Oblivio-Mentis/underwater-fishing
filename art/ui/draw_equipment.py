"""Draw the additional NEREID inventory and monitor icons, in atlas order."""
from pathlib import Path
from PIL import Image, ImageDraw

CELL = 96
SUPERSAMPLE = 4
INK = "#132b33"
BONE = "#eff7ef"
ICE = "#93bdc3"
SHADE = "#729fa8"
PALE = "#dce8df"

def tile():
    image = Image.new("RGBA", (CELL * SUPERSAMPLE, CELL * SUPERSAMPLE))
    brush = ImageDraw.Draw(image)
    def points(xs):
        return [(int(x * SUPERSAMPLE), int(y * SUPERSAMPLE)) for x, y in xs]
    def polygon(xs, fill=BONE, edge=INK, weight=2):
        brush.polygon(points(xs), fill=fill)
        if edge:
            brush.line(points(xs + xs[:1]), fill=edge, width=weight * SUPERSAMPLE, joint="curve")
    def line(xs, color=INK, width=2):
        brush.line(points(xs), fill=color, width=width * SUPERSAMPLE, joint="curve")
    def ellipse(bounds, fill=BONE, edge=INK, width=2):
        brush.ellipse(tuple(int(v * SUPERSAMPLE) for v in bounds), fill=fill,
                      outline=edge, width=width * SUPERSAMPLE)
    return image, polygon, line, ellipse

def ball(p, l, e):
    e((12, 12, 84, 84), INK, None)
    e((15, 15, 81, 81))
    p([(18, 62), (30, 73), (48, 77), (63, 71), (75, 57), (80, 43),
       (80, 57), (73, 69), (62, 77), (47, 80), (31, 75)], ICE, None)
    p([(17, 39), (23, 46), (36, 49), (53, 46), (68, 35), (72, 26),
       (78, 33), (74, 43), (63, 53), (48, 60), (32, 61), (20, 56), (16, 50)], SHADE, None)
    l([(17, 42), (26, 50), (40, 53), (56, 49), (71, 38), (76, 30)], INK, 2)
    l([(20, 56), (32, 61), (48, 60), (65, 52), (76, 41)], INK, 2)
    l([(28, 30), (36, 24), (46, 21)], "#ffffff", 3)

def biomass(p, l, e):
    p([(12, 57), (17, 41), (34, 33), (49, 43), (53, 62), (46, 76),
       (27, 82), (15, 73)], PALE)
    p([(33, 29), (47, 17), (63, 18), (77, 31), (75, 48), (59, 58),
       (43, 51), (34, 42)], BONE)
    p([(54, 56), (63, 43), (78, 43), (86, 56), (86, 71), (74, 80),
       (58, 77), (51, 68)], BONE)
    p([(15, 67),(30, 72),(43, 66),(47, 56),(49, 68),(43, 74),
       (27, 80),(17, 73)], ICE, None)
    p([(60, 42),(72, 34),(75, 32),(73, 47),(59, 56),(46, 50)], ICE, None)
    p([(55, 67),(68, 71),(82, 61),(84, 69),(74, 78),(59, 76)], ICE, None)
    l([(29,44),(22,58),(31,65),(39,58)], SHADE, 3)
    l([(63,25),(51,31),(48,39),(55,44)], SHADE, 3)

def chitin(p, l, e):
    p([(13, 70),(18, 46),(29, 28),(44, 16),(64, 15),(79, 26),(85, 44),
       (77, 56),(67, 56),(56, 65),(45, 69),(35, 80),(20, 82)], BONE)
    p([(16,70),(29,66),(40,56),(51,50),(61,38),(72,33),(80,37),(84,44),
       (77,55),(67,55),(56,64),(45,68),(34,79),(20,81)], ICE, None)
    l([(20,48),(34,45),(45,49),(49,64)], INK, 3)
    l([(29,30),(43,29),(55,36),(63,56)], INK, 3)
    l([(44,18),(58,22),(69,31),(76,50)], INK, 3)
    l([(22,58),(30,54),(38,57)], SHADE, 2)
    l([(36,36),(45,36),(52,43)], SHADE, 2)
    l([(49,24),(57,27),(63,32)], SHADE, 2)

def scrap(p, l, e):
    p([(12,56),(19,41),(32,35),(45,42),(69,23),(82,26),(88,39),(61,61),
       (64,73),(54,79),(41,73),(24,81),(12,71)], BONE)
    p([(22,53),(40,49),(69,29),(79,32),(50,58),(47,67),(24,77),(13,70),(13,62)], ICE, None)
    p([(52,54),(62,47),(58,60),(63,71),(54,77),(45,72),(50,64)], SHADE, None)
    e((19,59,31,70),INK,None)
    e((69,30,80,41),INK,None)
    l([(35,58),(40,64),(50,59),(55,50)], INK, 3)
    l([(28,44),(36,43),(42,46)], "#ffffff", 3)

def electronics(p, l, e):
    p([(14,35),(30,23),(78,23),(85,34),(85,63),(69,75),(21,75),(13,65)], BONE)
    p([(16,61),(71,61),(83,54),(83,63),(69,73),(22,73),(15,64)], ICE, None)
    p([(34,36),(44,30),(66,30),(72,36),(72,49),(63,56),(41,56),(34,51)], INK)
    p([(40,38),(46,35),(63,35),(66,38),(66,47),(61,51),(44,51),(40,47)], SHADE, None)
    l([(45,41),(61,41),(61,45),(56,45)], PALE, 2)
    for x in (23,28,77,81):
        l([(x,39),(x,53)], SHADE, 2)
    for x in (34,43,52,61,70):
        l([(x,64),(x,72)], INK, 2)
    e((19,30,26,37),INK,None)

def wrench(p, l, e):
    p([(13,66),(16,59),(47,36),(47,26),(55,16),(66,13),(75,16),
       (63,26),(64,33),(71,36),(83,26),(84,37),(79,47),(68,52),(58,50),
       (29,78),(20,81),(14,75)], BONE)
    p([(20,72),(50,47),(58,43),(68,47),(76,42),(81,35),(78,46),(67,50),
       (58,48),(28,77),(20,79)], ICE, None)
    e((18,67,25,74),INK,None)
    l([(28,67),(54,45)], SHADE, 3)

def bathymetry(p, l, e):
    p([(12,27),(23,17),(77,17),(84,24),(84,71),(73,82),(17,82),(12,77)], BONE)
    p([(17,27),(25,21),(74,21),(79,26),(79,68),(72,76),(17,76)], INK, None)
    l([(22,64),(35,48),(48,57),(63,32),(74,38)], ICE, 4)
    l([(23,70),(64,70),(75,66)], PALE, 2)
    l([(28,29),(28,40),(32,43),(36,40)], PALE, 2)
    e((60,28,66,34),PALE,None)

def crate(p, l, e):
    p([(13,32),(25,20),(71,20),(83,31),(83,70),(73,78),(18,78),(13,72)], BONE)
    p([(15,36),(26,28),(72,28),(81,33),(81,44),(15,44)], ICE)
    p([(29,35),(66,35),(69,39),(66,44),(31,44),(28,41)], BONE)
    l([(24,45),(24,69),(30,74)], SHADE, 3)
    l([(71,46),(71,69),(66,75)], SHADE, 3)
    l([(34,59),(63,59)], INK, 3)

def draw(destination):
    painters = (ball, biomass, chitin, scrap, electronics, wrench, bathymetry, crate)
    atlas = Image.new("RGBA", (CELL * len(painters), CELL))
    for index, painter in enumerate(painters):
        image, polygon, line, ellipse = tile()
        painter(polygon, line, ellipse)
        atlas.paste(image.resize((CELL, CELL), Image.Resampling.LANCZOS), (index * CELL, 0))
    atlas.save(destination, optimize=True)

if __name__ == "__main__":
    import sys
    draw(Path(sys.argv[1]))
