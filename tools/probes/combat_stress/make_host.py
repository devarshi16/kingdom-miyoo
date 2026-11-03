#!/usr/bin/env python3
"""Inject isolated combat fixtures into a private host; never edits production code."""
import argparse
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__)
p.add_argument('output',type=Path)
a=p.parse_args()
root=Path(__file__).resolve().parents[3]
s=(root/'engine/Host/Program.cs').read_text()
helper=Path(__file__).with_name('Probe.csfrag').read_text()
for old,new in {
 '    static int Main(string[] argv)': helper+'\n    static int Main(string[] argv)',
 '            float dt = 1f / 30f;': '            QBefore(f);\n            float dt = 1f / 30f;',
 '            Coroutines.TickEndOfFrame();': '            QAfter(f);\n            Coroutines.TickEndOfFrame();',
}.items():
 if s.count(old)!=1:raise SystemExit('Host anchor changed: '+old.strip())
 s=s.replace(old,new)
with a.output.open('x') as f:f.write(s)
