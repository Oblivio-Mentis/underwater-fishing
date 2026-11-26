"""Exercise the active industrial fishery in two independent Windows development players."""

import argparse
import json
import math
from pathlib import Path
import socket
import subprocess
import time
import uuid


def run(player, output, scenario=None):
    executable = player.resolve(strict=True)
    folder = output.resolve() / ("fishery-" + time.strftime("%Y%m%d-%H%M%S") + "-" + uuid.uuid4().hex[:8])
    folder.mkdir(parents=True)
    print(folder, flush=True)
    with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as udp:
        udp.bind(("127.0.0.1", 0))
        port = str(udp.getsockname()[1])
    startup = subprocess.STARTUPINFO()
    startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
    startup.wShowWindow = subprocess.SW_HIDE
    children = []

    def launch(role, *flags):
        args = [str(executable), "-raid-" + role, "-raid-local-port", port,
                "-raid-profile", str(uuid.uuid4()), "-screen-fullscreen", "0",
                "-screen-width", "960", "-screen-height", "540", "-force-d3d11",
                "-job-worker-count", "2", "-logFile", str(folder / (role + ".log")), *flags]
        children.append(subprocess.Popen(args, cwd=executable.parent, startupinfo=startup))

    try:
        flags = [scenario] if scenario else []
        launch("host", "-raid-fishery-trial", str(folder), "-raid-fishery-duo",
               "-raid-save", str(folder / "campaign.json"), *flags)
        deadline = time.monotonic() + 120
        while time.monotonic() < deadline:
            log = folder / "host.log"
            if log.exists() and "Local server is started for Tugboat" in log.read_text(encoding="utf-8", errors="replace"):
                break
            if children[0].poll() is not None:
                raise RuntimeError("Host exited before accepting LAN connections")
            time.sleep(.25)
        else:
            raise TimeoutError("Host failed to start its transport")
        guest = folder / "guest-probe"
        guest.mkdir()
        launch("client", "-batchmode", "-raid-ui-probe", str(guest))
        deadline = time.monotonic() + 240
        previous = None
        aimed = False
        clicked = False
        guest_saw_catch = False

        def guest_request(verb, **values):
            request = {"id": f"vacuum-{verb}", "action": verb, **values}
            staging = guest / "next.json"
            staging.write_text(json.dumps(request), encoding="utf-8")
            staging.replace(guest / "request.json")

        def guest_state():
            try:
                data = json.loads((guest / "state.json").read_text(encoding="utf-8-sig"))
                return data, json.loads(data.get("team") or "{}")
            except (OSError, json.JSONDecodeError):
                return {}, {}

        while time.monotonic() < deadline:
            try:
                report = json.loads((folder / "fishery.json").read_text(encoding="utf-8-sig"))
            except (OSError, json.JSONDecodeError):
                report = {}
            status = report.get("status")
            phase = report.get("phase")
            if phase and previous != phase:
                print(phase, flush=True)
                previous = phase
            if status == "failed":
                raise RuntimeError(report["error"])
            if phase == "guest observes and takes one vacuum fish":
                state, shared = guest_state()
                rig = next((item for item in shared.get("rigs", [])
                            if item["serial"] == report["vacuumSerial"]), None)
                if rig and any(item["id"] == report["vacuumCatch"] for item in rig["contents"]):
                    guest_saw_catch = True
                    point = report["vacuumOutlet"]
                    dx, dy, dz = point["x"]-state["x"], point["y"]-state["y"]-.6, point["z"]-state["z"]
                    if not aimed and 1 < dx*dx+dy*dy+dz*dz < 12:
                        guest_request("view", number=math.degrees(math.atan2(dx, dz)),
                                      number2=-math.degrees(math.atan2(dy, math.hypot(dx, dz))))
                        aimed = True
                    elif aimed and not clicked and state.get("request") == "vacuum-view":
                        guest_request("reticle_click")
                        clicked = True
            if status == "passed":
                image = folder / report["screenshot"]
                with image.open("rb") as shot:
                    if shot.read(8) != bytes.fromhex("89504e470d0a1a0a"):
                        raise ValueError("Invalid screenshot")
                if scenario:
                    if not report["twoDivers"]:
                        raise ValueError("The scenario requires an independent client")
                    if scenario == "-raid-gosha-den-only":
                        _, shared = guest_state()
                        if len(shared.get("goshaDen", [])) != 9:
                            time.sleep(.25)
                            continue
                        print("Independent client received all the saved den pieces.")
                    print(f"{len(report['checks'])} checks passed, frames and report in {folder}")
                    return
                if not report["twoDivers"] or not report["netCatch"] or not report["vacuumCatch"]:
                    raise ValueError("Two-owner field checks missing")
                _, shared = guest_state()
                rig = next((item for item in shared.get("rigs", [])
                            if item["serial"] == report["vacuumSerial"]), None)
                if not guest_saw_catch or not clicked or not rig:
                    raise ValueError("Guest did not see the live and claimed catch")
                if any(item["id"] == report["vacuumCatch"] for item in rig["contents"]):
                    time.sleep(.25)
                    continue
                print("Independent client saw the same vacuum count update after picking up the specimen.")
                print(f"{len(report['checks'])} passed checks, report and screenshot in {folder}")
                return
            if any(p.poll() is not None for p in children):
                raise RuntimeError("A player exited before completing the fishery report")
            time.sleep(.4)
        raise TimeoutError(f"Timed out waiting for {folder / 'fishery.json'}")
    finally:
        for child in children:
            if child.poll() is None:
                child.terminate()
            child.wait(timeout=15)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("player", type=Path, help="Path to the development UnderwaterFishing.exe")
    parser.add_argument("output", type=Path, help="Parent directory for per-run PNG, JSON, save and logs")
    scenarios = parser.add_mutually_exclusive_group()
    scenarios.add_argument("--bow-only", dest="scenario", action="store_const", const="-raid-bow-only",
                           help="Run the kraken and pipe presentation scenario")
    scenarios.add_argument("--gosha-den", dest="scenario", action="store_const", const="-raid-gosha-den-only",
                           help="Run the companion junk collection and den scenario")
    options = parser.parse_args()
    run(options.player, options.output, options.scenario)
