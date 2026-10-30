#!/usr/bin/env python3
"""Run native Sandbox contact/grip checks in the project's installed Unity Editor."""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--unity', required=True, type=Path, help='Unity Editor executable matching ProjectVersion.txt')
args = parser.parse_args()
if not args.unity.is_file():
    parser.error('Unity executable not found')
verification = ROOT / 'Assets/_DrivingGroundVerification'
if verification.exists() or verification.with_suffix('.meta').exists():
    parser.error('Temporary verification path already exists; refusing to overwrite it')
report = ROOT / 'TestResults/DrivingGround'
report.mkdir(parents=True, exist_ok=True)
font = ROOT / 'Assets/TextMeshPro/Resources/Fonts & Materials/LiberationSans SDF.asset'
font_bytes = font.read_bytes()
scene = ROOT / 'Assets/_Project/Scenes/Sandbox.unity'
scene_bytes = scene.read_bytes()
try:
    editor = verification / 'Editor'
    editor.mkdir(parents=True)
    shutil.copy2(ROOT / 'tools/driving-ground/DrivingGroundVerify.cs', editor)
    env = dict(os.environ, DRIVING_GROUND_RESULTS=str(report))
    result = subprocess.run([str(args.unity.resolve()), '-batchmode', '-nographics',
                             '-projectPath', str(ROOT), '-executeMethod', 'DrivingGroundVerify.Run',
                             '-logFile', str(report / 'unity.log')], cwd=ROOT, env=env, timeout=300)
    if result.returncode:
        raise RuntimeError('Unity verification failed; inspect ' + str(report / 'unity.log'))
    counts = {}
    for name in ['edit', 'play']:
        lines = (report / (name + '-validation.txt')).read_text().splitlines()
        if not lines or not all(line.startswith('PASS ') for line in lines):
            raise RuntimeError('Incomplete native report: ' + name)
        counts[name + '_checks'] = len(lines)
    if scene.read_bytes() != scene_bytes:
        raise RuntimeError('Verification unexpectedly changed the scene on disk')
    print(json.dumps(dict(status='PASS', **counts, reports=str(report)), indent=2))
except (RuntimeError, subprocess.TimeoutExpired) as error:
    print(str(error), file=sys.stderr)
    sys.exit(1)
finally:
    # TMP can serialize a dynamic atlas during Play Mode. Preserve the user's exact font asset.
    if font.read_bytes() != font_bytes:
        font.write_bytes(font_bytes)
    shutil.rmtree(verification)
    verification.with_suffix('.meta').unlink(missing_ok=True)
