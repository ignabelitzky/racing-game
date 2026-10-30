#!/usr/bin/env python3
"""Compare the conservative asset reorganization and allowlisted cleanup against the recorded main commit.
Run from any directory: python3 tools/validate-organization.py [baseline-ref]
Uses only Python's standard library and Git. Does not launch or modify Unity.
"""
import collections
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys
import uuid

ROOT = Path(__file__).resolve().parents[1]
BASE = sys.argv[1] if len(sys.argv) > 1 else '0cf4539c1a5ea3ff6ea07b313cefeca4a6d3b532'
MOVES = json.loads((ROOT / 'docs/asset-moves.json').read_text())
CLEANUP = json.loads((ROOT / 'docs/cleanup-manifest.json').read_text())
REMOVED = CLEANUP['removed_files']
PACKAGES = CLEANUP['removed_packages']
GUID = re.compile(rb'guid:\s*([a-f0-9]{32})')

def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT)

entries = {}
for row in git('ls-tree', '-r', '-z', BASE).split(b'\0'):
    if row:
        info, path = row.split(b'\t', 1)
        entries[path.decode()] = info.split()[2].decode()
errors = []
old_guids = {}
new_guids = collections.defaultdict(list)
old_refs = collections.Counter()
new_refs = collections.Counter()
unchanged = 0
external = set()
checked = 0
baseline_orphan_metas = []
removed_guids = set()
removed_checked = 0
batch = subprocess.Popen(['git', 'cat-file', '--batch'], cwd=ROOT,
                         stdin=subprocess.PIPE, stdout=subprocess.PIPE)
for source, sha in entries.items():
    if not source.startswith(('Assets/', 'Packages/', 'ProjectSettings/')):
        continue
    batch.stdin.write((sha + '\n').encode())
    batch.stdin.flush()
    header = batch.stdout.readline().split()
    before = batch.stdout.read(int(header[2]))
    assert batch.stdout.read(1) == b'\n'
    dest = MOVES.get(source, source)
    path = ROOT / dest
    if dest in REMOVED:
        removed_checked += 1
        if path.exists() or hashlib.sha256(before).hexdigest() != REMOVED[dest]['sha256']:
            errors.append('Invalid allowlisted removal: ' + dest)
        if source.endswith('.meta'):
            match = re.search(rb'^guid: ([a-f0-9]{32})', before, re.M)
            if match:
                removed_guids.add(match[1].decode())
        continue
    if not path.is_file():
        errors.append('Missing original file: ' + dest)
        continue
    after = path.read_bytes()
    checked += 1
    expected = before
    if source == 'ProjectSettings/EditorBuildSettings.asset':
        expected = before.replace(b'Assets/Scenes/', b'Assets/_Project/Scenes/')
    elif source == 'Assets/Scripts/Input/TumblerInput.inputactions.meta':
        expected = before.replace(b'wrapperCodePath: Assets/Scripts/Input/Input.cs',
                                  b'wrapperCodePath: Assets/_Project/Input/Input.cs')
    elif source in ('Packages/manifest.json', 'Packages/packages-lock.json'):
        data = json.loads(before)
        for name in PACKAGES:
            data['dependencies'].pop(name, None)
        expected = (json.dumps(data, indent=2) + '\n').encode()
    elif source == 'ProjectSettings/GraphicsSettings.asset':
        expected = b''.join(line for line in before.splitlines(keepends=True)
                            if b'UnityEngine.Rendering.Universal.UniversalRenderPipeline:' not in line)
    elif source == 'ProjectSettings/SceneTemplateSettings.json':
        data = json.loads(before)
        data['dependencyTypeInfos'] = [item for item in data['dependencyTypeInfos']
            if not item['type'].startswith('UnityEngine.Rendering.PostProcessing.')]
        expected = (json.dumps(data, indent=4) + '\n').encode()
    if hashlib.sha256(expected).digest() != hashlib.sha256(after).digest():
        errors.append('Unexpected content change: ' + dest)
    if before == after:
        unchanged += 1
    if b'\0' not in before:
        old_refs.update((dest, g.decode()) for g in GUID.findall(expected))
    if b'\0' not in after:
        new_refs.update((dest, g.decode()) for g in GUID.findall(after))
    if source.endswith('.meta'):
        match = re.search(rb'^guid: ([a-f0-9]{32})', before, re.M)
        if match:
            old_guids[source] = match[1].decode()
batch.stdin.close()
batch.wait()
if removed_checked != len(REMOVED):
    errors.append('Removal manifest has paths outside baseline')
# Search both textual and common binary GUID encodings in every retained project file.
package_guids = set(CLEANUP['postprocessing_3_4_0']['package_guid_index'])
for path in list((ROOT / 'Assets').rglob('*')) + list((ROOT / 'ProjectSettings').rglob('*')):
    if not path.is_file():
        continue
    content = path.read_bytes()
    for guid in removed_guids | package_guids:
        raw = bytes.fromhex(guid)
        encodings = [guid.encode(), guid.upper().encode(), guid.encode('utf-16le'),
                     guid.encode('utf-16be'), raw, uuid.UUID(hex=guid).bytes_le,
                     b''.join(raw[i:i+4][::-1] for i in range(0, 16, 4))]
        if any(value in content for value in encodings):
            errors.append('Reference to removed asset/package: ' + str(path.relative_to(ROOT)) + ' ' + guid)
if old_refs != new_refs:
    errors.append('Serialized GUID reference graph changed')
for meta in (ROOT / 'Assets').rglob('*.meta'):
    match = re.search(rb'^guid: ([a-f0-9]{32})', meta.read_bytes(), re.M)
    if not match:
        errors.append('Missing meta GUID: ' + str(meta.relative_to(ROOT)))
        continue
    new_guids[match[1].decode()].append(str(meta.relative_to(ROOT)))
    if not meta.with_suffix('').exists():
        rel = str(meta.relative_to(ROOT))
        original = next((s for s, d in MOVES.items() if d == rel), rel)
        asset = original[:-5]
        if original in entries and asset not in entries and not any(
                p.startswith(asset + '/') for p in entries):
            baseline_orphan_metas.append(rel)
        else:
            errors.append('New orphan meta: ' + rel)
for path in (ROOT / 'Assets').rglob('*'):
    if path.suffix != '.meta' and not path.with_name(path.name + '.meta').is_file():
        errors.append('Missing meta: ' + str(path.relative_to(ROOT)))
for guid, paths in new_guids.items():
    if len(paths) > 1:
        errors.append('Duplicate GUID: ' + guid + ' ' + str(paths))
for source, guid in old_guids.items():
    if MOVES.get(source, source) not in new_guids.get(guid, []):
        errors.append('Original GUID no longer resolves: ' + source)
for dest, guid in old_refs:
    if guid not in new_guids and not guid.startswith('0000000000000000'):
        external.add(guid)
settings = (ROOT / 'ProjectSettings/EditorBuildSettings.asset').read_text()
for path, guid in re.findall(r'path: (.+)\n\s+guid: ([a-f0-9]{32})', settings):
    if not (ROOT / path).is_file() or path + '.meta' not in new_guids.get(guid, []):
        errors.append('Invalid build scene: ' + path)
meta = (ROOT / 'Assets/_Project/Input/TumblerInput.inputactions.meta').read_text()
wrapper = re.search(r'wrapperCodePath: (.+)', meta)[1]
if not (ROOT / wrapper).is_file():
    errors.append('Missing generated wrapper target')
for file in ['manifest.json', 'packages-lock.json']:
    json.loads((ROOT / 'Packages' / file).read_text())
json.loads((ROOT / 'Assets/_Project/Input/TumblerInput.inputactions').read_text())
manifest = json.loads((ROOT / 'Packages/manifest.json').read_text())['dependencies']
lock = json.loads((ROOT / 'Packages/packages-lock.json').read_text())['dependencies']
if any(name in manifest or name in lock for name in PACKAGES):
    errors.append('Removed pipeline/legacy package still installed')
for name, entry in lock.items():
    for dependency in entry['dependencies']:
        if dependency not in lock:
            errors.append('Missing package dependency: ' + name + ' -> ' + dependency)
if set(manifest) - set(lock):
    errors.append('Manifest packages absent from lock')
tracked = git('ls-files').decode().splitlines()
generated = [p for p in tracked if p.split('/')[0].lower() in
             {'library', 'temp', 'obj', 'build', 'builds', 'logs', 'usersettings'}]
if generated:
    errors.append('Tracked generated directories: ' + str(generated))
summary = {
    'baseline': BASE, 'original_project_files_checked': checked,
    'byte_identical_original_files': unchanged,
    'expected_configuration_edits': checked - unchanged,
    'allowlisted_files_removed': removed_checked,
    'removed_bytes_from_working_tree': sum(x['bytes'] for x in REMOVED.values()),
    'moved_tracked_paths': len(MOVES),
    'retained_original_meta_guids_preserved': len(old_guids),
    'current_meta_guids': len(new_guids),
    'serialized_guid_reference_occurrences': sum(old_refs.values()),
    'retained_external_or_unresolved_guids': len(external),
    'baseline_orphan_metas_remaining': baseline_orphan_metas,
    'errors': errors,
    'unity_runtime_tests': 'NOT RUN: exact Unity 6000.0.0f1 unavailable',
}
print(json.dumps(summary, indent=2))
sys.exit(bool(errors))
