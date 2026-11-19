"""Opt-in development-player UI driver. Commands target UI/input only, never server/save state."""
import argparse
import json
from pathlib import Path
import time
import uuid


def state(directory):
    for _ in range(10):
        try:
            return json.loads((Path(directory) / "state.json").read_text(encoding="utf-8"))
        except (FileNotFoundError, PermissionError, json.JSONDecodeError):
            time.sleep(0.05)
    raise RuntimeError("Development UI probe state unavailable")


def send(directory, action="snapshot", name="", value="", number=0, number2=0, settle=0.25):
    folder = Path(directory)
    folder.mkdir(parents=True, exist_ok=True)
    request = dict(id=uuid.uuid4().hex, action=action, name=name, value=value, number=number, number2=number2)
    temporary = folder / "request.json.tmp"
    temporary.write_text(json.dumps(request, ensure_ascii=False), encoding="utf-8")
    temporary.replace(folder / "request.json")
    until = time.monotonic() + 12
    while time.monotonic() < until:
        current = state(folder)
        if current.get("request") == request["id"]:
            if current.get("error"):
                raise RuntimeError(current["error"])
            time.sleep(settle)
            return state(folder)
        time.sleep(0.1)
    raise TimeoutError("UI request was not consumed")


def summary(current):
    fields = ("page", "connected", "pending", "cursor", "inputBlocked", "width", "height", "players", "owner", "events", "canvases", "audioListeners", "depthTier", "travel", "hookMode", "tension", "stamina", "rescueProgress", "stockLoss", "wave", "x", "y", "z", "oxygen", "stress", "aimYaw", "aimPitch", "lastSuccess", "tetherActive", "isPulled", "tetherOwner", "tetherTarget", "tetherRefusal")
    result = {key: current.get(key) for key in fields}
    result.update({key: value for key, value in current.items() if key.startswith("harpoon")})
    if current.get("team"):
        team = json.loads(current["team"])
        result["team"] = {key: team[key] for key in ("biomass", "chitin", "scrap", "electronics", "reelLevel", "hullLevel", "highestDepth", "offering", "hungerRemaining", "hoistPhase", "hoistRemaining")}
        result["team"].update(stock=len(team["tank"]), cargo=len(team["cargo"]))
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("directory")
    parser.add_argument("action", nargs="?", default="snapshot")
    parser.add_argument("--name", default="")
    parser.add_argument("--value", default="")
    parser.add_argument("--number", type=float, default=0)
    parser.add_argument("--number2", type=float, default=0)
    parser.add_argument("--controls", action="store_true")
    args = parser.parse_args()
    current = send(args.directory, args.action, args.name, args.value, args.number, args.number2)
    output = summary(current)
    if args.controls:
        output["controls"] = current["controls"]
    print(json.dumps(output, ensure_ascii=False, indent=2))
