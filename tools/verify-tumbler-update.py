#!/usr/bin/env python3
"""Verify the digital instruments, seven-speed drivetrain, steering and underbody expansion in Unity."""
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
parser.add_argument('--graphics', action='store_true', help='Also render native HDRP HUD/underbody inspection views')
args = parser.parse_args()
if not args.unity.is_file():
    parser.error('Unity executable not found')
verification = ROOT / 'Assets/_TumblerUpdateVerification'
if verification.exists() or verification.with_suffix('.meta').exists():
    parser.error('Temporary verifier path exists; refusing to overwrite it')
report = ROOT / 'TestResults/TumblerUpdate'
report.mkdir(parents=True, exist_ok=True)
font = ROOT / 'Assets/TextMeshPro/Resources/Fonts & Materials/LiberationSans SDF.asset'
font_bytes = font.read_bytes()
scene = ROOT / 'Assets/_Project/Scenes/Sandbox.unity'
scene_bytes = scene.read_bytes()
try:
    editor = verification / 'Editor'
    editor.mkdir(parents=True)
    shutil.copy2(ROOT / 'tools/tumbler-update/TumblerUpdateVerify.cs', editor)
    if args.graphics:
        shutil.copy2(ROOT / 'tools/tumbler-update/TumblerUpdatePreview.cs', editor)
    env = dict(os.environ, TUMBLER_UPDATE_RESULTS=str(report))
    result = subprocess.run([str(args.unity.resolve()), '-batchmode', '-nographics',
                             '-projectPath', str(ROOT), '-executeMethod', 'TumblerUpdateVerify.Run',
                             '-logFile', str(report / 'unity.log')], cwd=ROOT, env=env, timeout=300)
    if result.returncode:
        raise RuntimeError('Unity verification failed; inspect ' + str(report / 'unity.log'))
    counts = {}
    for name in ['edit', 'play']:
        lines = (report / (name + '.txt')).read_text().splitlines()
        if not lines or not all(line.startswith('PASS ') for line in lines):
            raise RuntimeError('Incomplete report: ' + name)
        counts[name + '_checks'] = len(lines)
    if args.graphics:
        graphics = ['-force-vulkan'] if sys.platform.startswith('linux') else []
        preview_env = dict(os.environ, TUMBLER_PREVIEWS=str(report / 'Previews'))
        preview = subprocess.run([str(args.unity.resolve()), '-batchmode', *graphics,
                                  '-projectPath', str(ROOT), '-executeMethod', 'TumblerUpdatePreview.Run',
                                  '-logFile', str(report / 'preview.log')], cwd=ROOT, env=preview_env, timeout=300)
        if preview.returncode:
            raise RuntimeError('Unity previews failed; inspect ' + str(report / 'preview.log'))
        counts['preview_images'] = len(list((report / 'Previews').glob('*.png')))
        if counts['preview_images'] != 8:
            raise RuntimeError('Incomplete native preview set')
    if scene.read_bytes() != scene_bytes:
        raise RuntimeError('Verification unexpectedly changed the scene on disk')
    print(json.dumps(dict(status='PASS', **counts, reports=str(report)), indent=2))
except (RuntimeError, subprocess.TimeoutExpired) as error:
    print(str(error), file=sys.stderr)
    sys.exit(1)
finally:
    if font.read_bytes() != font_bytes:
        font.write_bytes(font_bytes)
    shutil.rmtree(verification)
    verification.with_suffix('.meta').unlink(missing_ok=True)
