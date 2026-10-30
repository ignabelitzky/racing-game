#!/usr/bin/env python3
"""Verify the Xbox/keyboard change against the last organized main, without Unity."""
import collections
import hashlib
import json
from pathlib import Path
import re
import subprocess
import uuid

ROOT = Path(__file__).resolve().parents[1]
BASE = 'b6245330e0f9ec22140bed35da20393735393000'
INPUT = 'Assets/_Project/Input/'
SCENE = 'Assets/_Project/Scenes/Sandbox.unity'
CLEANUP = json.loads((ROOT / 'docs/input-cleanup.json').read_text())
REMOVED = CLEANUP['removed_files']

def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT)

def original(path):
    return git('show', BASE + ':' + path)

def require(condition, message):
    if not condition:
        raise AssertionError(message)

entries = {}
for row in git('ls-tree', '-r', '-z', BASE).split(b'\0'):
    if row:
        info, path = row.split(b'\t', 1)
        entries[path.decode()] = info.split()[2].decode()
mutable = {INPUT + 'TumblerInput.inputactions', INPUT + 'Input.cs', SCENE}
unchanged = 0
removed_guids = set()
for path, sha in entries.items():
    if not path.startswith(('Assets/', 'Packages/', 'ProjectSettings/')):
        continue
    target = ROOT / path
    if path in REMOVED:
        require(not target.exists(), 'Deleted demo unexpectedly present: ' + path)
        before = original(path)
        require(hashlib.sha256(before).hexdigest() == REMOVED[path]['sha256'], 'Wrong cleanup baseline')
        if path.endswith('.meta'):
            removed_guids.add(re.search(rb'^guid: ([a-f0-9]{32})', before, re.M)[1].decode())
        continue
    require(target.is_file(), 'Missing original project file: ' + path)
    if path not in mutable:
        data = target.read_bytes()
        actual = hashlib.sha1(('blob ' + str(len(data)) + '\0').encode() + data).hexdigest()
        require(actual == sha, 'Unexpected change to retained project file: ' + path)
        unchanged += 1
require(set(REMOVED) <= set(entries), 'Deletion outside recorded baseline')

# All old actions and bindings retain identity. Only layout generalization and Axis metadata change.
before = json.loads(original(INPUT + 'TumblerInput.inputactions'))
after = json.loads((ROOT / INPUT / 'TumblerInput.inputactions').read_text())
require(len(after['maps']) == 1 and not after['controlSchemes'], 'Unexpected action maps or device restrictions')
old_map, new_map = before['maps'][0], after['maps'][0]
require(old_map['id'] == new_map['id'] and new_map['name'] == 'Gameplay', 'Action map identity changed')
expected_actions = before['maps'][0]['actions']
next(a for a in expected_actions if a['name'] == 'Throttle')['expectedControlType'] = 'Axis'
require(new_map['actions'] == expected_actions, 'Actions or action IDs changed')
old_bindings = old_map['bindings']
for binding in old_bindings:
    binding['path'] = binding['path'].replace('<XInputController>', '<Gamepad>')
require(new_map['bindings'][:len(old_bindings)] == old_bindings, 'Existing Xbox controls changed')
binding_ids = [b['id'] for b in new_map['bindings']]
require(len(set(binding_ids)) == len(binding_ids), 'Duplicate binding IDs')
source = (ROOT / INPUT / 'Input.cs').read_text()
embedded = re.search(r'InputActionAsset.FromJson\(@"(.*?)"\);', source, re.S)[1].replace('""', '"')
require(json.loads(embedded) == after, 'Generated input wrapper is stale')
require('//     version 1.8.2' in source, 'Wrapper must use the installed generator')

# Compare each serialized scene object, including all rigidbodies/wheels/vehicle settings.
def blocks(text):
    return {m[1]: m[0] for m in re.findall(r'(--- !u!\d+ &(-?\d+)[^\n]*\n.*?)(?=--- !u!|\Z)', text, re.S)}

def action_entry(block, action_id):
    return next(e for e in re.findall(r'  - m_PersistentCalls:\n.*?(?=  - m_PersistentCalls:|  m_NeverAutoSwitchControlSchemes:)', block, re.S)
                if 'm_ActionId: ' + action_id in e)

old_scene = original(SCENE).decode()
new_scene = (ROOT / SCENE).read_text()
old, new = blocks(old_scene), blocks(new_scene)
removed_component, router, vehicle = '2040198345356866092', '1824967746', '2040198345356866087'
require(set(old) - set(new) == {removed_component} and not set(new) - set(old), 'Unexpected scene object additions/removals')
for object_id, block in old.items():
    if object_id not in {removed_component, router, vehicle}:
        require(new[object_id] == block, 'Scene object changed: ' + object_id)
require(new[vehicle] == old[vehicle].replace('  - component: {fileID: ' + removed_component + '}\n', ''), 'Vehicle hierarchy changed')
switch_id = next(a['id'] for a in new_map['actions'] if a['name'] == 'SwitchDriveMode')
expected = old[router].replace(action_entry(old[router], switch_id), action_entry(old[removed_component], switch_id))
for action in new_map['actions']:
    expected = re.sub(r'(m_ActionId: ' + action['id'] + r'\n    m_ActionName: )[^\n]*',
                      r"\1'Gameplay/" + action['name'] + "'", expected)
normalize = lambda text: re.sub(r'[ \t]+$', '', text, flags=re.M)
require(normalize(new[router]) == normalize(expected), 'Serialized input callbacks changed unexpectedly')
require(removed_component not in new_scene, 'Dangling PlayerInput component reference')
require(new_scene.count('guid: 62899f850307741f2a39c98a8b639597') == 1, 'There must be one PlayerInput')

# GUID uniqueness and asset/meta pairing; retain references to all build-scene assets.
metas = collections.defaultdict(list)
for meta in (ROOT / 'Assets').rglob('*.meta'):
    require(meta.with_suffix('').exists(), 'Orphan meta: ' + str(meta))
    guid = re.search(rb'^guid: ([a-f0-9]{32})', meta.read_bytes(), re.M)[1].decode()
    metas[guid].append(meta)
require(all(len(paths) == 1 for paths in metas.values()), 'Duplicate asset GUID')
for path in (ROOT / 'Assets').rglob('*'):
    if path.suffix != '.meta':
        require(path.with_name(path.name + '.meta').is_file(), 'Missing meta: ' + str(path))
for guid in removed_guids:
    raw = bytes.fromhex(guid)
    patterns = [guid.encode(), guid.upper().encode(), guid.encode('utf-16le'), guid.encode('utf-16be'),
                raw, uuid.UUID(hex=guid).bytes_le, b''.join(raw[i:i+4][::-1] for i in range(0,16,4))]
    for root in ['Assets', 'ProjectSettings']:
        for path in (ROOT / root).rglob('*'):
            if path.is_file():
                content = path.read_bytes()
                require(not any(p in content for p in patterns), 'Retained reference to deleted demo: ' + str(path))
for scene, info in json.loads((ROOT / 'docs/scene-dependencies.json').read_text()).items():
    require((ROOT / scene).is_file(), 'Missing build scene')
    require(all((ROOT / dep).is_file() for dep in info['local_transitive_dependencies']), 'Missing scene dependency')

# Ignore only generated outputs; all Input System source and meta files must remain trackable.
ignored = ['Library/a', 'Temp/a', 'Obj/a', 'Build/a', 'Builds/a', 'Logs/a', 'UserSettings/a',
           'TestResults/a.xml', 'TestResults.xml', 'CodeCoverage/a.xml']
for path in ignored:
    result = subprocess.run(['git', 'check-ignore', '--no-index', '-q', path], cwd=ROOT)
    require(result.returncode == 0, 'Generated output not ignored: ' + path)
for path in [INPUT + 'Input.cs', INPUT + 'TumblerInput.inputactions', INPUT + 'Input.cs.meta',
             'Packages/manifest.json', 'Packages/packages-lock.json', 'ProjectSettings/EditorBuildSettings.asset',
             'tools/input/VerifyBindings.cs']:
    result = subprocess.run(['git', 'check-ignore', '--no-index', '-q', path], cwd=ROOT)
    require(result.returncode == 1, 'Required source ignored: ' + path)
require(not any(p.split('/')[0].lower() in {'library','temp','obj','build','builds','logs','usersettings','testresults','codecoverage'}
                for p in git('ls-files').decode().splitlines()), 'Generated output is tracked')
print(json.dumps({'baseline': BASE, 'byte_identical_retained_project_files': unchanged,
                  'intentional_input_and_routing_edits': sorted(mutable), 'asset_meta_guids': len(metas),
                  'removed_demo_files': len(REMOVED), 'removed_demo_bytes': sum(v['bytes'] for v in REMOVED.values()),
                  'preserved_action_ids': len(new_map['actions']), 'binding_count': len(binding_ids),
                  'unchanged_scene_objects': len(old)-3, 'errors': []}, indent=2))
