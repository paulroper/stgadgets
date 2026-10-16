"""Work with a stgadgets recording: find addresses, inspect values, add markers, trim.

Points in a recording are a tick number, "start", "end", or a marker label (exact, or a unique prefix).

  search   Narrow down which addresses hold a value, the way cheat finders do. Each filter compares one byte
           between two points and keeps only the addresses that pass; filters apply in the order given.
             python3 -I recording.py search REC --changed start "coin inserted" --unchanged "coin inserted" end
             python3 -I recording.py search REC --equals end 3 --decreased "1st death" "2nd death"
  history  Every distinct value of one address over the recording, with ticks; good for placing markers.
             python3 -I recording.py history REC 0x20EB --type bcd1
  mark     Add (or move) a marker at a tick.
             python3 -I recording.py mark REC 812 "coin inserted"
  trim     Copy a slice of a recording (with its markers) to a new, smaller recording.
             python3 -I recording.py trim REC "coin inserted" end --out REC-trimmed
"""
import argparse, json, os, sys


class Recording:
    def __init__(self, path, region=None):
        self.path = path
        self.manifest = json.load(open(os.path.join(path, "manifest.json")))
        regions = self.manifest["regions"]
        self.region = next((r for r in regions if r["name"] == region), None) if region else regions[0]
        if self.region is None:
            sys.exit(f"no region {region!r}; have {[r['name'] for r in regions]}")
        self.base = int(self.region["start"], 0)
        self.size = int(self.region["size"], 0)
        self.data = open(os.path.join(path, self.region["file"]), "rb").read()
        self.ticks = self.manifest["ticks"]
        self.markers = self.manifest.get("markers") or []

    def tick(self, point):
        if point == "start":
            return 0
        if point == "end":
            return self.ticks - 1
        if point.lstrip("-").isdigit():
            return int(point)
        exact = [m for m in self.markers if m["label"] == point]
        matches = exact or [m for m in self.markers if m["label"].startswith(point)]
        if len(matches) != 1:
            sys.exit(f"point {point!r} matches {len(matches)} markers; markers are: {[m['label'] for m in self.markers]}")
        return matches[0]["tick"]

    def byte(self, tick, address):
        return self.data[tick * self.size + (address - self.base)]

    def value(self, tick, address, kind):
        size = 1 if kind == "u8" else 2 if kind in ("u16le", "u16be") else int(kind[3:])
        raw = self.data[tick * self.size + (address - self.base):][:size]
        if kind == "u8":
            return raw[0]
        if kind in ("u16le", "u16be"):
            return int.from_bytes(raw, "little" if kind == "u16le" else "big")
        digits = "".join(f"{b:02x}" for b in reversed(raw))
        return int(digits) if digits.isdigit() else None

    def save_manifest(self):
        self.manifest["markers"] = sorted(self.markers, key=lambda m: m["tick"])
        with open(os.path.join(self.path, "manifest.json"), "w") as f:
            json.dump(self.manifest, f, indent=2)


def search(rec, args):
    candidates = list(range(rec.base, rec.base + rec.size))
    tests = {
        "changed": lambda a, b: a != b, "unchanged": lambda a, b: a == b,
        "increased": lambda a, b: b > a, "decreased": lambda a, b: b < a,
    }
    for name, operands in args.filters:
        if name == "equals":
            tick, value = rec.tick(operands[0]), int(operands[1], 0)
            candidates = [a for a in candidates if rec.byte(tick, a) == value]
        else:
            t1, t2 = rec.tick(operands[0]), rec.tick(operands[1])
            candidates = [a for a in candidates if tests[name](rec.byte(t1, a), rec.byte(t2, a))]
        print(f"after --{name} {' '.join(operands)}: {len(candidates)} candidate(s)")

    points = [("start", 0)] + [(m["label"], m["tick"]) for m in rec.markers] + [("end", rec.ticks - 1)]
    print()
    print("address  " + "  ".join(f"{label[:12]:>12}" for label, _ in points))
    for a in candidates[: args.show]:
        print(f"0x{a:04X}   " + "  ".join(f"{rec.byte(t, a):>12}" for _, t in points))
    if len(candidates) > args.show:
        print(f"... {len(candidates) - args.show} more (raise --show, or add filters)")


def history(rec, args):
    address = int(args.address, 0)
    previous = object()
    for tick in range(rec.ticks):
        value = rec.value(tick, address, args.type)
        if value != previous:
            marks = [m["label"] for m in rec.markers if m["tick"] == tick]
            print(f"tick {tick:6}  {value!r:>8}" + (f"   <- {', '.join(marks)}" if marks else ""))
            previous = value


def mark(rec, args):
    tick = int(args.tick)
    if not 0 <= tick < rec.ticks:
        sys.exit(f"tick must be 0..{rec.ticks - 1}")
    rec.markers = [m for m in rec.markers if m["label"] != args.label] + [{"tick": tick, "label": args.label}]
    rec.save_manifest()
    print(f"marked tick {tick}: {args.label}")


def trim(rec, args):
    first, last = rec.tick(args.first), rec.tick(args.last)
    if not 0 <= first <= last < rec.ticks:
        sys.exit("bad range")
    os.makedirs(args.out, exist_ok=False)
    manifest = dict(rec.manifest)
    manifest["ticks"] = last - first + 1
    manifest["markers"] = [{"tick": m["tick"] - first, "label": m["label"]} for m in rec.markers if first <= m["tick"] <= last]
    manifest["notes"] = (manifest.get("notes", "") + f" Trimmed to ticks {first}..{last} of {os.path.basename(rec.path)}.").strip()
    for region in rec.manifest["regions"]:
        size = int(region["size"], 0)
        data = open(os.path.join(rec.path, region["file"]), "rb").read()
        open(os.path.join(args.out, region["file"]), "wb").write(data[first * size:(last + 1) * size])
    ticks = open(os.path.join(rec.path, "ticks.bin"), "rb").read()[first * 16:(last + 1) * 16]
    t0 = int.from_bytes(ticks[:8], "little", signed=True)
    rebased = b"".join(
        (int.from_bytes(ticks[i:i + 8], "little", signed=True) - t0).to_bytes(8, "little", signed=True) + ticks[i + 8:i + 16]
        for i in range(0, len(ticks), 16))
    open(os.path.join(args.out, "ticks.bin"), "wb").write(rebased)
    with open(os.path.join(args.out, "manifest.json"), "w") as f:
        json.dump(manifest, f, indent=2)
    print(f"wrote {manifest['ticks']} ticks to {args.out}")


class Filter(argparse.Action):
    """Collects --changed/--unchanged/--increased/--decreased/--equals in command-line order."""
    def __call__(self, parser, namespace, values, option_string=None):
        namespace.filters = (namespace.filters or []) + [(option_string.lstrip("-"), values)]


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = p.add_subparsers(dest="command", required=True)

    s = sub.add_parser("search", help="narrow down candidate addresses")
    s.add_argument("recording"); s.add_argument("--region")
    for name in ("changed", "unchanged", "increased", "decreased"):
        s.add_argument(f"--{name}", nargs=2, metavar=("FROM", "TO"), action=Filter, dest="filters")
    s.add_argument("--equals", nargs=2, metavar=("POINT", "VALUE"), action=Filter, dest="filters")
    s.add_argument("--show", type=int, default=20)
    s.set_defaults(run=search, filters=[])

    h = sub.add_parser("history", help="distinct values of one address over time")
    h.add_argument("recording"); h.add_argument("address"); h.add_argument("--region")
    h.add_argument("--type", default="u8", help="u8, u16le, u16be or bcdN (default u8)")
    h.set_defaults(run=history)

    m = sub.add_parser("mark", help="add a marker")
    m.add_argument("recording"); m.add_argument("tick"); m.add_argument("label")
    m.set_defaults(run=mark, region=None)

    t = sub.add_parser("trim", help="copy a slice into a new recording")
    t.add_argument("recording"); t.add_argument("first"); t.add_argument("last"); t.add_argument("--out", required=True)
    t.set_defaults(run=trim, region=None)

    args = p.parse_args()
    args.run(Recording(args.recording, args.region), args)


if __name__ == "__main__":
    main()
