"""Record a game's RAM from RetroArch into a stgadgets recording (docs/providers.md#recording-format-draft).

Polls READ_CORE_RAM back-to-back (RetroArch answers once per frame, so ~one snapshot per frame) and writes:
  <out>/<region>.bin   one snapshot per tick
  <out>/ticks.bin      per tick: int64 microseconds since start, int64 emulated frame (-1, unknown)
  <out>/manifest.json  written when recording stops
  <out>/changes.log    readable log of --watch values, markers and round-trip times

Stops after --seconds, on SIGINT/SIGTERM, or when a file named STOP appears in <out>.
To mark an event while recording, write its label to <out>/MARK (e.g. `echo "coin inserted" > <out>/MARK`).

Example (Space Invaders on FBNeo: READ_CORE_RAM offset 0 is 0x2000):
  python3 -I record.py --out packs/invaders/recordings/play-02 --base 0x2000 --size 0x400 --ram-base 0x2000 \
      --watch 0x20F8:bcd2:p1.score --watch 0x21FF:u8:p1.ships_reserve
"""
import argparse, json, os, signal, socket, struct, sys, time
from datetime import datetime, timezone

MAX_READ = 2048  # larger READ_CORE_RAM replies exceed macOS's UDP datagram limit and are dropped


def number(text):
    return int(text, 0)


def parse_watch(text):
    """ADDR[:TYPE[:NAME]], TYPE one of u8, u16le, u16be, bcdN."""
    parts = text.split(":")
    address = number(parts[0])
    kind = parts[1] if len(parts) > 1 else "u8"
    name = parts[2] if len(parts) > 2 else f"0x{address:X}"
    size = {"u8": 1, "u16le": 2, "u16be": 2}.get(kind) or (int(kind[3:]) if kind.startswith("bcd") else None)
    if size is None:
        raise argparse.ArgumentTypeError(f"unknown type {kind!r}")
    return name, address, size, kind


def decode(data, kind):
    if kind == "u8":
        return data[0]
    if kind == "u16le":
        return int.from_bytes(data, "little")
    if kind == "u16be":
        return int.from_bytes(data, "big")
    digits = "".join(f"{b:02x}" for b in reversed(data))  # bcdN: low byte first, as on the 8080
    return int(digits) if digits.isdigit() else None


class RetroArch:
    def __init__(self, host, port):
        self.sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self.sock.settimeout(0.5)
        self.addr = (host, port)

    def command(self, text, expect):
        self.sock.sendto(text.encode(), self.addr)
        while True:  # skip late replies to earlier, timed-out requests
            reply = self.sock.recv(65535).decode().strip()
            if reply.startswith(expect):
                return reply

    def read(self, offset, size):
        """Pipelined READ_CORE_RAM in chunks; returns bytes or raises."""
        chunks = [(o, min(MAX_READ, offset + size - o)) for o in range(offset, offset + size, MAX_READ)]
        for o, n in chunks:
            self.sock.sendto(f"READ_CORE_RAM {o:x} {n}".encode(), self.addr)
        pending = dict(chunks)
        result = {}
        while pending:
            parts = self.sock.recv(65535).decode().split()
            if len(parts) < 2 or parts[0] != "READ_CORE_RAM":
                continue
            o = int(parts[1], 16)
            if o not in pending:
                continue
            if len(parts) > 2 and parts[2] == "-1":
                raise RuntimeError(f"READ_CORE_RAM {o:x} failed: {' '.join(parts[3:])}")
            result[o] = bytes(int(x, 16) for x in parts[2:])
            del pending[o]
        return b"".join(result[o] for o, _ in chunks)


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--out", required=True, help="recording directory to create")
    p.add_argument("--base", type=number, required=True, help="first address to record (original hardware address)")
    p.add_argument("--size", type=number, required=True, help="bytes to record")
    p.add_argument("--ram-base", type=number, help="address that READ_CORE_RAM offset 0 maps to (default: --base)")
    p.add_argument("--region", default="ram", help="region name in the manifest (default: ram)")
    p.add_argument("--watch", type=parse_watch, action="append", default=[], help="ADDR[:TYPE[:NAME]] to log when it changes")
    p.add_argument("--seconds", type=float, default=600)
    p.add_argument("--notes", default="")
    p.add_argument("--host", default="127.0.0.1")
    p.add_argument("--port", type=int, default=55355)
    args = p.parse_args()

    ram_base = args.base if args.ram_base is None else args.ram_base
    offset = args.base - ram_base
    if offset < 0:
        sys.exit("--base is below --ram-base")
    for name, address, size, _ in args.watch:
        if not (args.base <= address and address + size <= args.base + args.size):
            sys.exit(f"--watch {name} (0x{address:X}) is outside the recorded range")

    os.makedirs(args.out, exist_ok=False)
    ra = RetroArch(args.host, args.port)
    try:
        status = ra.command("GET_STATUS", "GET_STATUS")
    except (TimeoutError, socket.timeout):
        sys.exit("No reply from RetroArch: is it running with network_cmd_enable = \"true\"?")
    fields = status.split(" ", 2)[2].split(",") if status.count(" ") >= 2 else []
    system, romset = (fields + [None, None])[:2]

    stop = False

    def request_stop(*_):
        nonlocal stop
        stop = True

    signal.signal(signal.SIGINT, request_stop)
    signal.signal(signal.SIGTERM, request_stop)

    ram_file = open(os.path.join(args.out, f"{args.region}.bin"), "wb")
    ticks_file = open(os.path.join(args.out, "ticks.bin"), "wb")
    log = open(os.path.join(args.out, "changes.log"), "w", buffering=1)
    stop_path, mark_path = os.path.join(args.out, "STOP"), os.path.join(args.out, "MARK")
    started_at = datetime.now(timezone.utc).isoformat(timespec="seconds")
    log.write(f"# {started_at} {status}\n")

    markers, rtts, previous = [], [], {}
    ticks, timeouts = 0, 0
    start = time.perf_counter()
    try:
        while not stop and time.perf_counter() - start < args.seconds and not os.path.exists(stop_path):
            t0 = time.perf_counter()
            try:
                ram = ra.read(offset, args.size)
                timeouts = 0
            except (TimeoutError, socket.timeout):
                timeouts += 1
                if timeouts >= 20:
                    log.write("# RetroArch stopped answering; stopping\n")
                    break
                continue
            t1 = time.perf_counter()
            rtts.append((t1 - t0) * 1000)
            ram_file.write(ram)
            ticks_file.write(struct.pack("<qq", int((t1 - start) * 1e6), -1))

            if os.path.exists(mark_path):
                label = open(mark_path).read().strip() or f"mark {len(markers) + 1}"
                os.remove(mark_path)
                markers.append({"tick": ticks, "label": label})
                log.write(f"{t1 - start:9.3f}s tick {ticks:6}  MARK {label}\n")

            current = {n: decode(ram[a - args.base:a - args.base + s], k) for n, a, s, k in args.watch}
            changed = {n: (previous.get(n), v) for n, v in current.items() if previous.get(n) != v}
            if changed:
                log.write(f"{t1 - start:9.3f}s tick {ticks:6}  " + ", ".join(f"{n} {a}->{b}" for n, (a, b) in changed.items()) + "\n")
            previous = current
            ticks += 1
    finally:
        ram_file.close()
        ticks_file.close()
        manifest = {
            "schema": 1,
            "content": {"romset": romset, "core": system, "provider": "retroarch"},
            "capturedAt": started_at,
            "frameSynced": False,
            "ticks": ticks,
            "regions": [{"name": args.region, "start": f"0x{args.base:X}", "size": f"0x{args.size:X}", "file": f"{args.region}.bin"}],
            "markers": markers,
            "notes": args.notes or f"Recorded via READ_CORE_RAM (offset 0 = 0x{ram_base:X}), one request per frame, not frame-synced.",
        }
        with open(os.path.join(args.out, "manifest.json"), "w") as f:
            json.dump(manifest, f, indent=2)
        r = sorted(rtts) or [0]
        summary = (f"{ticks} ticks in {time.perf_counter() - start:.1f}s; round-trip p50 {r[len(r) // 2]:.1f} ms, "
                   f"p95 {r[int(len(r) * 0.95)]:.1f} ms; {len(markers)} marker(s)")
        log.write(f"# done: {summary}\n")
        log.close()
        print(summary)


if __name__ == "__main__":
    main()
