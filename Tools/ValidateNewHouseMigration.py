"""Audit the saved migration against its live pre-migration snapshot, without Unity."""
from pathlib import Path
import collections
import difflib
import json
import re

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'docs/evidence/new-house-investigation'

def records(text):
    return {m.group(2): (m.group(1), m.group(0)) for m in re.finditer(
        r'^--- !u!(\d+) &(\d+)[^\n]*\n.*?(?=^--- !u!|\Z)', text, re.M | re.S)}

before = (OUT / 'before/CSI_Environment.unity').read_text(encoding='utf-8-sig')
after = (ROOT / 'Assets/_Project/Scenes/Scenarios/CSI_Environment.unity').read_text(encoding='utf-8-sig')
a, b = records(before), records(after)
changed = sorted(k for k in a.keys() & b.keys() if a[k] != b[k])
removed = sorted(a.keys() - b.keys())
added = sorted(b.keys() - a.keys())

# Removed records must belong to the old Sketch template's descendants.
template_go = next(k for k, (_, text) in a.items() if re.search(r'^  m_Name: SketchTemplate_CSI_Environment$', text, re.M))
template_transform = next(k for k, (kind, text) in a.items() if kind == '224' and f'  m_GameObject: {{fileID: {template_go}}}' in text)
parents = {}
gameobjects = {}
for k, (kind, text) in a.items():
    parent = re.search(r'^  m_Father: \{fileID: (\d+)\}', text, re.M)
    go = re.search(r'^  m_GameObject: \{fileID: (\d+)\}', text, re.M)
    if parent and go:
        parents[k] = parent.group(1)
        gameobjects[go.group(1)] = k

def old_sketch_member(k):
    kind, text = a[k]
    go = re.search(r'^  m_GameObject: \{fileID: (\d+)\}', text, re.M)
    transform = gameobjects.get(k if kind == '1' else go.group(1) if go else '')
    seen = set()
    while transform and transform not in seen:
        if parents.get(transform) == template_transform:
            return True
        seen.add(transform)
        transform = parents.get(transform)
    return False

checks = []
def check(ok, label):
    checks.append({'pass': bool(ok), 'label': label})

check(all(old_sketch_member(k) for k in removed), 'Every removed scene record belongs to the replaced Sketch drawing')
old_evidence_ids = re.findall(r'^  evidenceId: (.+)$', before, re.M)
new_evidence_ids = re.findall(r'^  evidenceId: (.+)$', after, re.M)
check(collections.Counter(old_evidence_ids) == collections.Counter(new_evidence_ids), 'Scene evidence IDs and counts preserved')
def fixed_reference_ids(data):
    return sorted(re.search(r'^  referenceId: (.+)$', text, re.M).group(1)
                  for kind, text in data.values()
                  if kind == '114' and 'Assembly-CSharp::SceneReferencePoint\n' in text)
check(fixed_reference_ids(a) == fixed_reference_ids(b), 'Fixed measurement reference IDs and counts preserved')

for path in ['Assets/_Project/Data/Measurement/Measurements_CSI_Environment.asset',
             'Assets/_Project/Data/Sketch/Sketch_CSI_Environment.asset',
             'Assets/_Project/Data/Scenarios/IOC_Assessment_CSI_Environment.asset',
             'Assets/Resources/ViCSIDialogueCatalog.asset',
             'Assets/_Project/Data/Evidence/EVD-018_Emptyliquorbottle.asset',
             'Assets/_Project/Data/Evidence/BODY-A_Victimsbody.asset',
             'Assets/_Project/Data/DialoguePools/DialoguePool_Evidence018Logged.asset',
             'Assets/_Project/Data/DialoguePools/DialoguePool_Evidence018Photographed.asset']:
    original = (OUT / ('before/migration-original-' + Path(path).name)).read_text(encoding='utf-8-sig')
    current = (ROOT / path).read_text(encoding='utf-8-sig')
    for key in ['evidenceId', 'referenceId', 'requirementId', 'slotId', 'correctAnchorId', 'observationId', 'id', 'isAccurate', 'required', 'scoreWeight', 'relevance', 'requiresFingerprinting', 'requiresSwabbing', 'requiresChalk']:
        pattern = rf'^[ \t]*(?:- )?{key}:[ \t]*(.*)$'
        previous_values, current_values = re.findall(pattern, original, re.M), re.findall(pattern, current, re.M)
        if previous_values or current_values:
            check(previous_values == current_values, f'{Path(path).name}: {key} identities/rules preserved')
    (OUT / ('asset-' + Path(path).name + '.diff')).write_text(''.join(difflib.unified_diff(original.splitlines(True), current.splitlines(True), fromfile='before', tofile='after')), encoding='utf-8')

result = {
    'passed': sum(x['pass'] for x in checks),
    'failed': sum(not x['pass'] for x in checks),
    'checks': checks,
    'addedSceneRecords': len(added),
    'removedSceneRecords': len(removed),
    'removedOutsideOldSketch': [k for k in removed if not old_sketch_member(k)],
    'changedSceneRecordTypes': dict(collections.Counter(a[k][0] for k in changed)),
}
(OUT / 'preservation-audit.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
(OUT / 'migration-scene.diff').write_text(''.join(difflib.unified_diff(before.splitlines(True), after.splitlines(True), fromfile='live-before-migration', tofile='new-house-investigation')), encoding='utf-8')
print(json.dumps({k:v for k,v in result.items() if k != 'checks'}, indent=2))
raise SystemExit(1 if result['failed'] else 0)
