"""Focused Phase 3 authored content and unchanged-surface audit."""
from pathlib import Path
import re,json,hashlib
root=Path(__file__).resolve().parents[1]
out=root/'docs/evidence/assessment-phase3'
results=[]
def check(label, passed): results.append((label,bool(passed)))
asset=(root/'Assets/Resources/ReasoningCheckpoints_LastDrink.asset').read_text()
blocks=re.split(r'^  - id: ',asset,flags=re.M)[1:]
check('Exactly six unique checkpoint IDs; optional synthesis omitted',len(blocks)==6 and len(set(b.splitlines()[0] for b in blocks))==6)
bank=json.loads((out/'question-bank.json').read_text())
for block,expected in zip(blocks,bank):
 id,trigger,finding,category,prompt,responses=expected
 authored_prompt=json.loads(re.search(r'^    prompt: (.+)$',block,re.M)[1])
 texts=[json.loads(t) for t in re.findall(r'^      text: (.+)$',block,re.M)]
 check(id+' exact requested question and four response texts',authored_prompt==prompt and texts==[r[0] for r in responses])
 check(id+' exact internal 1-4 weights/stable IDs/category',list(map(int,re.findall(r'^      weight: (\d+)',block,re.M)))==[r[1] for r in responses]
       and re.findall(r'^    - id: (.+)',block,re.M)==[id+'-'+c for c in 'ABCD'] and 'category: '+category in block)
 check(id+' no numerical weights or evaluative labels in player-facing text',not re.search(r'\b[1-4]\b|correct!|incorrect!|\bpoints\b|\bweight\b',authored_prompt+' '.join(texts),re.I))
check('Early questions expose no later Paul finding or hidden offender fact',all('Paul' not in bank[n][4]+' '.join(t for t,w in bank[n][5]) for n in (0,1)))
check('Fingerprint integrates HYP-02 and withholds Phase 2 lesson', 'integratedHypothesisId: HYP-02' in blocks[2] and '- TRAIN-FINGERPRINT' in blocks[2])
check('Fingerprint, knife/wound and blood interpretations defer answer-revealing narrative',all('deferredDialogueIds: []' not in blocks[n] for n in (2,3,5)))
manager=(root/'Assets/_Project/Scripts/Assessment/ReasoningCheckpointManager.cs').read_text()
check('No checkpoint response mutates procedures, relationships, conclusions or scoring',not re.search(r'\.Mark[A-Z]|\.TryAdvance\(|\.CompleteAllSelected\(|\.TryConnect\(|AssessmentManager|DeductionScorer|AcceptEnding',manager))
check('Vendor eligibility uses physically discovered statement, not catalog availability', 'HasDiscovered(VendorFollowupInterview.StatementId)' in manager)
check('Scene uses actual IOC observation IDs',all(id in manager for id in ('ioc-possible-weapon','ioc-bloodstain-area')))
catalog=(root/'Assets/Resources/ViCSIDialogueCatalog.asset').read_text()
check('FR fact now includes reported nervous/sick behavior and no eyewitness attack', 'looking nervous or sick. He did not witness the attack.' in catalog)
phase2_catalog_blocks={b.splitlines()[0]:b for b in re.split(r'^  - id: ',catalog,flags=re.M)[1:]}
check('All eleven Phase 2 lessons remain authored',all(id in phase2_catalog_blocks for id in ['TRAIN-SOCO-TEAM','TRAIN-PHOTOGRAPHY','TRAIN-MARKER','TRAIN-TRACE-SOURCE','TRAIN-MEASUREMENT','TRAIN-FINGERPRINT','TRAIN-BIOLOGICAL-SAMPLE','TRAIN-PACKAGING','TRAIN-CUSTODY','closeout-start','closeout-survey-guidance']))
baseline=json.loads((out/'protected-before.json').read_text())
changed=[p for p,sha in baseline.items() if hashlib.sha256((root/p).read_bytes()).hexdigest()!=sha]
check('All protected authored procedure, finding, HYP, relationship, conclusion and assessment assets/code unchanged',not changed)
check('Scoring remains 1.6.0', 'scoringRulesVersion: 1.6.0' in (root/'Assets/_Project/Data/Assessment/Scenario_CSI_Environment.asset').read_text())
passed=sum(p for _,p in results)
lines=[('PASS: ' if p else 'FAIL: ')+label for label,p in results]
lines.append(f'{passed} passed, {len(results)-passed} failed; {len(baseline)} protected files compared.')
(out/'content-and-scope-audit.txt').write_text('\n'.join(lines)+'\n')
print('\n'.join(lines))
if changed: print('Changed:',changed)
raise SystemExit(0 if passed==len(results) else 1)
