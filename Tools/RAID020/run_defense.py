"""Run the staged defense fixture with independent LAN peers on Windows."""

import argparse
import json
from pathlib import Path
import socket
import subprocess
import time
import uuid


def read_report(path):
    try:
        return json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, ValueError):
        return {}


def run(executable, destination, crew, combat=False, solo=False, film_depth=0):
    executable = executable.resolve(strict=True)
    output = destination.resolve() / ("defense-" + uuid.uuid4().hex[:8])
    output.mkdir(parents=True)
    print(output, flush=True)

    with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as probe:
        probe.bind(("127.0.0.1", 0))
        port = str(probe.getsockname()[1])

    startup = subprocess.STARTUPINFO()
    startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
    startup.wShowWindow = subprocess.SW_HIDE
    processes = []

    def launch(role, name, *options):
        args = [str(executable), "-raid-" + role, "-raid-local-port", port,
                "-raid-profile", str(uuid.uuid4()), "-screen-fullscreen", "0",
                "-screen-width", "1280", "-screen-height", "720", "-force-d3d11",
                "-job-worker-count", "2", "-logFile", str(output / (name + ".log")), *options]
        processes.append(subprocess.Popen(args, cwd=executable.parent, startupinfo=startup))

    try:
        if film_depth:
            stage = ("-raid-defense-film",) + (("-raid-film-depth-2",) if film_depth == 2 else ())
        else:
            stage = ("-raid-defense-solo",) if solo else ("-raid-defense-combat",) if combat else ()
        launch("host", "host", "-raid-save", str(output / "campaign.json"),
               "-raid-defense-trial", str(output), str(crew), *stage)
        deadline = time.monotonic() + 60
        while time.monotonic() < deadline:
            log = output / "host.log"
            if log.is_file() and "Local server is started for Tugboat" in log.read_text(
                    encoding="utf-8", errors="replace"):
                break
            if processes[0].poll() is not None:
                raise RuntimeError(read_report(output / "defense.json").get("error") or "Host closed before opening LAN transport")
            time.sleep(.2)
        else:
            raise TimeoutError("LAN transport did not open")

        guests = []
        for seat in range(1, crew):
            report = output / f"guest-{seat}.json"
            guests.append(report)
            launch("client", f"guest-{seat}", "-batchmode", "-nographics",
                   "-raid-defense-observe", str(report))

        deadline = time.monotonic() + 220
        last = None
        while time.monotonic() < deadline:
            report = read_report(output / "defense.json")
            phase = report.get("phase")
            if phase and phase != last:
                print(phase, flush=True)
                last = phase
            if report.get("status") == "failed":
                raise RuntimeError(report["error"])
            if report.get("status") == "passed":
                if film_depth:
                    clips = ("01-window", "02-outside", "03-shield", "04-breach")
                    if any(len(list((output / name).glob("*.png"))) < 30 for name in clips):
                        raise RuntimeError("Missing a complete defense film sequence")
                    print(f"Depth {film_depth}: {len(clips)} film sequences", flush=True)
                    return
                for path in guests:
                    state = read_report(path)
                    if combat:
                        catch_id = report.get("deliveredPredator")
                        if not catch_id or state.get("cagedPredator") != catch_id or state.get("deliveredPredator") != catch_id:
                            raise RuntimeError(f"Client did not see the same predator in the cage and tank: {path.name}: {state}")
                        continue
                    if state.get("phases", 0) & 62 != 62 or state.get("hits", 0) == 0 or state.get("species") != 31:
                        raise RuntimeError(f"Client did not observe the phases, live hits and all species: {path.name}: {state}")
                    if state.get("losses", 0) != report["losses"]:
                        raise RuntimeError(f"Client losses differ: {path.name}: {state}")
                screenshots = sorted(output.glob("*.png"))
                if len(screenshots) < (2 if solo else 1 if combat else 10):
                    raise RuntimeError(f"Expected 10 game frames, got {len(screenshots)}")
                print(f"{crew} peers, {len(report['checks'])} checks, {len(screenshots)} game captures", flush=True)
                return
            if processes[0].poll() is not None:
                raise RuntimeError("Host exited before completing the defense")
            time.sleep(.25)
        raise TimeoutError("Timed out waiting for the defense fixture")
    finally:
        for process in processes:
            if process.poll() is None:
                process.terminate()
        for process in processes:
            process.wait(timeout=10)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("player", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--crew", type=int, choices=(2, 3, 4), default=2)
    parser.add_argument('--combat', action='store_true')
    parser.add_argument('--solo', action='store_true')
    parser.add_argument('--film-depth', type=int, choices=(1, 2), default=0)
    options = parser.parse_args()
    run(options.player, options.output, 1 if options.solo or options.film_depth else options.crew,
        options.combat, options.solo, options.film_depth)
