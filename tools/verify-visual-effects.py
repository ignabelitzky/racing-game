#!/usr/bin/env python3
"""Verify Tumbler contact marks, menu styles and Sandbox exposure in native Unity Play Mode."""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--unity', required=True, type=Path, help='Matching Unity Editor executable')
parser.add_argument('--graphics', action='store_true', help='Also compile/render HDRP and capture inspection views (requires a GPU)')
args = parser.parse_args()
if not args.unity.is_file():
    parser.error('Unity executable not found')
verification = ROOT / 'Assets/_VisualEffectsVerification'
if verification.exists() or verification.with_suffix('.meta').exists():
    parser.error('Temporary verifier path exists; refusing to overwrite it')
report = ROOT / 'TestResults/VisualEffects'
report.mkdir(parents=True, exist_ok=True)
font = ROOT / 'Assets/TextMeshPro/Resources/Fonts & Materials/LiberationSans SDF.asset'
font_bytes = font.read_bytes()
scenes = {p: p.read_bytes() for p in (ROOT / 'Assets/_Project/Scenes').glob('*.unity')}
try:
    editor = verification / 'Editor'
    editor.mkdir(parents=True)
    shutil.copy2(ROOT / 'tools/visual-effects/VerifyVisualEffects.cs', editor)
    graphics = ['-force-vulkan'] if args.graphics and sys.platform.startswith('linux') else []
    if not args.graphics:
        graphics = ['-nographics']
    env = dict(os.environ, VISUAL_EFFECTS_RESULTS=str(report))
    result = subprocess.run([str(args.unity.resolve()), '-batchmode', *graphics,
                             '-projectPath', str(ROOT), '-executeMethod', 'VerifyVisualEffects.Run',
                             '-logFile', str(report / 'unity.log')], cwd=ROOT, env=env, timeout=300)
    if result.returncode:
        raise RuntimeError('Unity verification failed; inspect ' + str(report / 'unity.log'))
    counts = {}
    for name in ['edit', 'play']:
        lines = (report / (name + '.txt')).read_text().splitlines()
        if not lines or not all(line.startswith('PASS ') for line in lines):
            raise RuntimeError('Incomplete report: ' + name)
        counts[name + '_checks'] = len(lines)
    if any(p.read_bytes() != data for p, data in scenes.items()):
        raise RuntimeError('Verification unexpectedly changed a scene on disk')
    print(json.dumps(dict(status='PASS', graphics=args.graphics, **counts, reports=str(report)), indent=2))
except (RuntimeError, subprocess.TimeoutExpired) as error:
    print(str(error), file=sys.stderr)
    sys.exit(1)
finally:
    if font.read_bytes() != font_bytes:
        font.write_bytes(font_bytes)
    shutil.rmtree(verification)
    verification.with_suffix('.meta').unlink(missing_ok=True)
