"""Focused Phase 2 content and protected-surface audit. No Unity suite invocation."""
from pathlib import Path
import json, re, hashlib, subprocess

root = Path(__file__).resolve().parents[1]
output = root/'docs/evidence/assessment-phase2'
catalog = (root/'Assets/Resources/ViCSIDialogueCatalog.asset').read_text(encoding='utf-8')
entries = {}
for block in re.split(r'^  - id: ', catalog, flags=re.M)[1:]:
    id = block.splitlines()[0]
    entries[id] = block
ids = ['TRAIN-SOCO-TEAM','TRAIN-PHOTOGRAPHY','TRAIN-MARKER','TRAIN-TRACE-SOURCE','TRAIN-MEASUREMENT',
       'TRAIN-FINGERPRINT','TRAIN-BIOLOGICAL-SAMPLE','TRAIN-PACKAGING','TRAIN-CUSTODY','closeout-start','closeout-survey-guidance']
results = []
def check(label, passed): results.append({'label':label, 'passed':bool(passed)})
for id in ids:
    block = entries.get(id, '')
    check(id+' authored as skippable Professor guidance', bool(block) and '    type: 0' in block
          and 'speaker: Professor' in block.replace('"','') and '    skippable: 1' in block)
check('Catalog ids are unique', len(entries) == len(re.findall(r'^  - id: ',catalog,re.M)))
for id in ids:
    block = entries[id]
    text = re.search(r'^    text: (.*)$',block,re.M).group(1)
    check(id+' contains no score language or case-specific answers', not re.search(r'points? (earned|gained|lost|awarded)|score|penalt|[+-]5|\bPaul\b|\bJohn\b|guilty|innocent',text,re.I))
baseline = json.loads((output/'protected-before.json').read_text())
changed = [name for name, sha in baseline.items() if hashlib.sha256(Path(name).read_bytes()).hexdigest() != sha]
check('Assessment, deduction code, all authored procedure/objective data and historical scoring files unchanged', not changed)
adapter = (root/'Assets/_Project/Scripts/Dialogue/ProceduralTrainingGuidance.cs').read_text()
check('Training adapter has no score, evidence mutation or session-upload calls', not re.search(r'AssessmentManager|DeductionScorer|LogEvent\(|Mark[A-Z]|\.TryAdvance\(|AssessmentSessionBuilder',adapter))
check('Active scenario remains scoring 1.6.0', 'scoringRulesVersion: 1.6.0' in (root/'Assets/_Project/Data/Assessment/Scenario_CSI_Environment.asset').read_text())
lines = [f"{'PASS' if r['passed'] else 'FAIL'}: {r['label']}" for r in results]
passed = sum(r['passed'] for r in results)
lines.append(f'{passed} passed, {len(results)-passed} failed; {len(baseline)} protected files compared.')
(output/'content-and-scope-audit.txt').write_text('\n'.join(lines)+'\n',encoding='utf-8')
print('\n'.join(lines))
if changed: print('Changed protected files:', changed)
raise SystemExit(0 if passed == len(results) else 1)
