#!/usr/bin/env python3
"""Test the generated input wrapper with virtual devices in an isolated Unity project.
Does not open or upgrade the game. Requires Unity Editor and Input System 1.8.2.
"""
import argparse
import json
from pathlib import Path
import shutil
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--unity', type=Path, required=True, help='Unity Editor executable')
parser.add_argument('--input-package', type=Path, help='Optional extracted official Input System 1.8.2 package')
parser.add_argument('--regenerate', action='store_true', help='Copy importer-generated Input.cs back after successful tests')
parser.add_argument('--report', type=Path, help='Save the JSON input simulation report')
args = parser.parse_args()
project = Path(tempfile.mkdtemp(prefix='tumbler-input-test-'))
asset_path = Path('Assets/_Project/Input')
(project / asset_path).mkdir(parents=True)
(project / 'Assets/Editor').mkdir()
(project / 'Packages').mkdir()
(project / 'ProjectSettings').mkdir()
for name in ['TumblerInput.inputactions', 'TumblerInput.inputactions.meta', 'Input.cs', 'Input.cs.meta']:
    shutil.copy2(ROOT / asset_path / name, project / asset_path / name)
shutil.copy2(ROOT / 'tools/input/VerifyBindings.cs', project / 'Assets/Editor/VerifyBindings.cs')
package = '1.8.2' if not args.input_package else 'file:' + str(args.input_package.resolve())
(project / 'Packages/manifest.json').write_text(json.dumps({'dependencies': {
    'com.unity.inputsystem': package, 'com.unity.modules.uielements': '1.0.0'}}))
(project / 'ProjectSettings/ProjectSettings.asset').write_text(
    '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!129 &1\nPlayerSettings:\n  activeInputHandler: 1\n')
log = project / 'unity.log'
print('Isolated input harness:', project, flush=True)
result = subprocess.run([str(args.unity.resolve()), '-batchmode', '-nographics',
    '-projectPath', str(project), '-executeMethod', 'VerifyBindings.Run', '-logFile', str(log)], timeout=900)
if result.returncode or not (project / 'input-results.json').exists():
    print('\n'.join(log.read_text(errors='replace').splitlines()[-70:]))
    raise SystemExit('Input verification failed; log retained at ' + str(log))
report = json.loads((project / 'input-results.json').read_text())
generated = project / asset_path / 'Input.cs'
if args.regenerate:
    shutil.copyfile(generated, ROOT / asset_path / 'Input.cs')
elif generated.read_text() != (ROOT / asset_path / 'Input.cs').read_text():
    raise SystemExit('The repository wrapper is stale; rerun with --regenerate. Log: ' + str(log))
if args.report:
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps(report, indent=2))
shutil.rmtree(project)
