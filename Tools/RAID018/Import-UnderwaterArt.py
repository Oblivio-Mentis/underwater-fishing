"""Install the four locally acquired reef props used by the playable level."""
import hashlib
from pathlib import Path
import re
import sys
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[2]
MODELS = ("coral_fan", "coral_table", "sea_anemone", "ship_anchor")
RESOURCES = ROOT / "Assets/_Game/Nereid/Resources"
TARGET = RESOURCES / "ExpeditionAssets/ReodesuxzLocal"


def model_guid(name):
    return hashlib.md5(("RAID:LowPolyUnderwaterPack_v1.0:" + name).encode()).hexdigest()


def install(source):
    template = (RESOURCES / "NereidServiceFittings/Models/Prop_PipeHolder.fbx.meta").read_text(
        encoding="utf-8"
    )
    template = template.replace("materialImportMode: 2", "materialImportMode: 0")

    with ZipFile(source) as archive:
        models = {}
        for name in MODELS:
            model = archive.read(f"LowPolyUnderwaterPack_v1.0/FBX/{name}.fbx")
            if not model.startswith(b"Kaydara FBX Binary"):
                raise ValueError(f"{name} is not a binary FBX")
            models[name] = model

    TARGET.mkdir(parents=True, exist_ok=True)
    for name, data in models.items():
        path = TARGET / (name + ".fbx")
        path.write_bytes(data)
        metadata = re.sub(r"(?m)^guid: .*$", "guid: " + model_guid(name), template)
        path.with_suffix(".fbx.meta").write_text(metadata, encoding="utf-8", newline="\n")
    print(f"Installed {len(models)} locally owned reef resources in {TARGET}")


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("Usage: python Tools/RAID018/Import-UnderwaterArt.py <LowPolyUnderwaterPack_v1.0.zip>")
    install(Path(sys.argv[1]))
