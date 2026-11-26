"""Run a development Windows player with two or four independent LAN clients."""

import argparse
import datetime
import json
import pathlib
import socket
import subprocess
import time
import uuid


def reports(folder):
    result = {}
    for path in folder.glob("result-*.json"):
        try:
            report = json.loads(path.read_text(encoding="utf-8-sig"))
            result[report["seat"]] = report
        except (OSError, json.JSONDecodeError):
            # A process may be writing out the current phase this frame.
            continue
    return result


def run(player, players, output):
    executable = player.resolve(strict=True)
    stamp = datetime.datetime.now().strftime("%Y%m%d-%H%M%S")
    folder = output.resolve() / f"crew-{players}-{stamp}-{uuid.uuid4().hex[:6]}"
    folder.mkdir(parents=True)
    print(folder, flush=True)
    with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as sock:
        sock.bind(("127.0.0.1", 0))
        port = str(sock.getsockname()[1])
    shared = [
        "-batchmode", "-force-d3d11", "-screen-fullscreen", "0", "-screen-width", "1280",
        "-screen-height", "720", "-job-worker-count", "2", "-raid-local-port", port,
        "-raid-tech-crew", str(folder), "-raid-save", str(folder / "trial-save.json"),
        "-raid-tech-players", str(players),
    ]
    startup = subprocess.STARTUPINFO()
    startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
    startup.wShowWindow = subprocess.SW_HIDE

    def start(seat):
        role = "-raid-host" if seat == 0 else "-raid-client"
        arguments = [
            str(executable), *shared, role, "-raid-tech-seat", str(seat), "-raid-profile",
            str(uuid.uuid4()), "-logFile", str(folder / f"player-{seat}.log"),
        ]
        return subprocess.Popen(arguments, cwd=executable.parent, startupinfo=startup)

    processes = [start(0)]
    try:
        deadline = time.monotonic() + 115
        while not (folder / "seat-0").exists():
            if processes[0].poll() is not None or time.monotonic() >= deadline:
                raise RuntimeError(f"Host did not spawn. See {folder / 'player-0.log'}")
            time.sleep(.3)

        processes.extend(start(i) for i in range(1, players))
        previous = {}
        deadline = time.monotonic() + 210
        while time.monotonic() < deadline:
            current = reports(folder)
            for seat, report in sorted(current.items()):
                status = (report["status"], report["phase"])
                if previous.get(seat) != status:
                    print(f"seat {seat}: {status[0]} | {status[1]}", flush=True)
                    previous[seat] = status
                if report["status"] == "failed":
                    raise RuntimeError(f"seat {seat}: {report['error']}")
            if len(current) == players and all(r["status"] == "passed" for r in current.values()):
                return
            if all(process.poll() is not None for process in processes):
                break
            time.sleep(.5)
        raise RuntimeError(f"Incomplete run. Check peer reports and logs in {folder}")
    finally:
        for process in processes:
            if process.poll() is None:
                try:
                    process.wait(timeout=3)
                except subprocess.TimeoutExpired:
                    process.terminate()
                    process.wait()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("player", type=pathlib.Path, help="Path to UnderwaterFishing.exe")
    parser.add_argument("players", type=int, choices=(2, 4))
    parser.add_argument("output", type=pathlib.Path, help="Parent directory for per-run evidence")
    args = parser.parse_args()
    run(args.player, args.players, args.output)
