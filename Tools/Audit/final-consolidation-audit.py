"""Read-only source/data/scene audit; writes diagnostic JSON only."""
from pathlib import Path
import hashlib
import json
import os
import re

ROOT = Path(__file__).resolve().parents[2]
WEB = Path(os.environ.get('VICSI_DASHBOARD_PROJECT', str(Path.home() / 'Desktop/vicsi-dashboard')))
OUT = ROOT / 'docs/evidence/final-consolidation'
OUT.mkdir(parents=True, exist_ok=True)
baseline = json.loads((OUT / 'preservation-before.json').read_text(encoding='utf-8'))
changed = [name for name, digest in baseline.items()
           if hashlib.sha256((ROOT / name).read_bytes()).hexdigest() != digest]
allowed = ['Assets/Resources/EvidenceDisplayNames_1.json']
checks = {'only_display_data_changed': all(name in allowed for name in changed)}
for unity, dashboard in [('EvidenceDisplayNames_1.json', 'evidenceDisplay.v1.json'),
                         ('QuestionDisplayNames_1.json', 'questionDisplay.v1.json'),
                         ('ProcedureTimeline_1.json', '../scoring/data/procedureTimeline.v1.json')]:
    checks[unity + '_parity'] = json.loads((ROOT / 'Assets/Resources' / unity).read_text(encoding='utf-8')) == json.loads((WEB / 'src/lib/data' / dashboard).read_text(encoding='utf-8'))
rules = json.loads((ROOT / 'Assets/Resources/DeductionScoring_1_7_0.json').read_text(encoding='utf-8'))
weights = {key: rules[key] for key in ['checkpointWeight', 'relationshipWeight', 'conclusionWeight']}
checks['authoritative_weights_60_25_15'] = list(weights.values()) == [60, 25, 15]
order = re.findall(r'^  - id: (\S+)', (ROOT / 'Assets/Resources/ReasoningCheckpoints_LastDrink.asset').read_text(encoding='utf-8'), re.M)
checks['actual_current_question_order'] = order == json.loads((ROOT / 'Assets/Resources/QuestionDisplayNames_1.json').read_text(encoding='utf-8'))['current'] == [r['id'] for r in rules['checkpoints']]

scene_paths = re.findall(r'^    path: (.+)$', (ROOT / 'ProjectSettings/EditorBuildSettings.asset').read_text(encoding='utf-8'), re.M)
scenes = []
wanted = set()
for name in scene_paths:
    content = (ROOT / name).read_text(encoding='utf-8')
    guids = set(re.findall(r'guid: ([a-f0-9]{32})', content))
    guids = {g for g in guids if not g.startswith('0000000000000000')}
    wanted.update(guids)
    anchors = set(re.findall(r'^--- !u!\d+ &(-?\d+)', content, re.M))
    local = set(re.findall(r'\{fileID: (-?\d+)\}', content)) - {'0'}
    scenes.append({'scene': name, 'assetGuidCount': len(guids), 'guids': guids,
                   'missingLocalFileIds': sorted(local - anchors)})
# Unity already loaded both enabled build scenes and inspected every serialized
# component reference. Reuse that evidence instead of scanning unused asset packs.
editor = json.loads((OUT / 'scene-validation.json').read_text(encoding='utf-8'))
checks['build_scene_local_references_resolve'] = all(not s['missingLocalFileIds'] for s in scenes)
checks['build_scene_references_resolve_in_editor'] = all(not s['brokenReferences'] and not s['activeMissingScripts'] and not s['inactiveMissingScripts'] for s in editor['scenes'])
for scene in scenes:
    scene.pop('guids')
result = {'checks': checks, 'serializedFilesPreserved': len(baseline) - len(changed),
          'changedSerializedFiles': changed, 'weights': weights, 'questionOrder': order, 'scenes': scenes}
(OUT / 'source-scene-audit.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
print(json.dumps(result, indent=2))
raise SystemExit(0 if all(checks.values()) else 1)
