#!/usr/bin/env python3
"""Write an instrumented private host, without editing the production host."""
import argparse
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path, help="new private Program.cs path")
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[2]
    source = (root / "engine/Host/Program.cs").read_text()
    helper = Path(__file__).with_name("PortProbe.inc.cs").read_text()
    replacements = {
        "    static int Run(string[] argv)": helper + "\n    static int Run(string[] argv)",
        "            float dt = 1f / 30f;": "            EnemyNightBefore(f);\n            float dt = 1f / 30f;",
        "            Coroutines.TickEndOfFrame();": "            EnemyNightAfter(f);\n            Coroutines.TickEndOfFrame();",
    }
    for anchor, replacement in replacements.items():
        if source.count(anchor) != 1:
            raise SystemExit(f"Host changed: expected exactly one {anchor.strip()!r}")
        source = source.replace(anchor, replacement)
    with args.output.open("x") as output:
        output.write(source)


if __name__ == "__main__":
    main()
