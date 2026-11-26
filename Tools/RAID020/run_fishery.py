"""Exercise the active industrial fishery in two independent Windows development players."""

import argparse
import json
from pathlib import Path
import socket
import subprocess
import time
import uuid


def run(player, output):
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
        launch("host", "-raid-fishery-trial", str(folder), "-raid-fishery-duo",
               "-raid-save", str(folder / "campaign.json"))
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
        launch("client")
        deadline = time.monotonic() + 240
        previous = None
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
            if status == "passed":
                image = folder / report["screenshot"]
                with image.open("rb") as shot:
                    if shot.read(8) != bytes.fromhex("89504e470d0a1a0a"):
                        raise ValueError("Invalid screenshot")
                if not report["twoDivers"] or not report["netCatch"] or not report["vacuumCatch"]:
                    raise ValueError("Two-owner field checks missing")
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
    options = parser.parse_args()
    run(options.player, options.output)
