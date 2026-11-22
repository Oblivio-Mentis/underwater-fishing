"""Bake the CC0 Quaternius shoal assets into single-draw OBJ meshes.

Usage: python Tools/RAID016/Prepare-DecorativeFish.py <Animated Fish Pack ZIP>
"""
import argparse
from pathlib import Path, PurePosixPath
from zipfile import ZipFile

OUTPUT = Path(__file__).resolve().parents[2] / "Assets/_Game/Nereid/Resources/DecorativeFish"
MODELS = (
    ("Fish1", "Silver shoal fish", {"Top": 0.16, "Bottom": 0.50, "Fins": 0.83}),
    ("Fish2", "Rift shoal fish", {"Body": 0.16, "Front": 0.50, "Fins": 0.83}),
)


def source_obj(archive, model):
    for name in archive.namelist():
        path = PurePosixPath(name)
        if path.parent.name == "OBJ" and path.name == model + ".obj":
            return archive.read(name).decode("utf-8")
    raise ValueError(f"{model}.obj was not in the archive's OBJ folder")


def cook_obj(source, source_name, regions):
    # Preserve geometry and normals, centring each mesh along its existing +Z axis.
    # The first UV channel identifies one of the three original material regions.
    rows = [line.split() for line in source.splitlines() if line.strip()]
    points = [tuple(map(float, row[1:4])) for row in rows if row[0] == "v"]
    bounds = [(min(axis), max(axis)) for axis in zip(*points)]
    center = [(lo + hi) / 2 for lo, hi in bounds]
    size = bounds[2][1] - bounds[2][0]
    region_index = {name: i + 1 for i, name in enumerate(regions)}
    cooked = [
        f"# Quaternius Animated Fish Pack / {source_name}.obj / CC0-1.0",
        "# One submesh. UV.x records the original material group.",
        f"o {source_name}",
    ]
    cooked.extend("v " + " ".join(f"{(p[i] - center[i]) / size:.6f}" for i in range(3)) for p in points)
    cooked.extend(f"vt {value:.2f} 0.5" for value in regions.values())
    for row in rows:
        if row[0] in ("s", "vn"):
            cooked.append(" ".join(row))
        elif row[0] == "usemtl":
            region = region_index[row[1]]
        elif row[0] == "f":
            corners = []
            for corner in row[1:]:
                vertex, _, normal = corner.split("/")
                corners.append(f"{vertex}/{region}/{normal}")
            cooked.append("f " + " ".join(corners))
    return "\n".join(cooked) + "\n"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("archive", type=Path)
    args = parser.parse_args()
    OUTPUT.mkdir(parents=True, exist_ok=True)
    with ZipFile(args.archive) as archive:
        for source_name, model_name, regions in MODELS:
            path = OUTPUT / (model_name + ".obj")
            path.write_text(cook_obj(source_obj(archive, source_name), source_name, regions), "utf-8", newline="\n")


if __name__ == "__main__":
    main()
