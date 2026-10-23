"""Small Phase 4 definition, historical compatibility and untouched-surface audit."""
from pathlib import Path
import json,hashlib,re
root=Path(__file__).resolve().parents[1]
backend=root.parent/'Desktop/vicsi-dashboard'
out=root/'docs/evidence/assessment-phase4'
results=[]
def check(label,good): results.append((label,bool(good)))
config=(root/'Assets/Resources/DeductionScoring_1_7_0.json').read_bytes()
rules=json.loads(config)
check('Unity/server use byte-identical single authored 1.7.0 scoring definition',config==(backend/'src/lib/scoring/data/csiEnvironment.v1_7.json').read_bytes())
check('Research weights are exactly 60/25/15 and require expert validation', [rules[k] for k in ('checkpointWeight','relationshipWeight','conclusionWeight')]==[60,25,15] and rules['validationStatus']=='RESEARCH_CONFIGURATION_REQUIRES_EXPERT_VALIDATION')
bank=json.loads((root/'docs/evidence/assessment-phase3/question-bank.json').read_text())
check('Same six checkpoint IDs, dimensions, triggers, response IDs and weights',len(rules['checkpoints'])==6 and all(c['id']==b[0] and c['trigger']==b[1] and c['findingId']==(b[2] or '') and c['category']==b[3]
    and [(r['id'],r['weight']) for r in c['responses']]==[(b[0]+'-'+letter,r[1]) for letter,r in zip('ABCD',b[5])] for c,b in zip(rules['checkpoints'],bank)))
check('Maximum checkpoint denominator is 24',rules['maximumCheckpointWeight']==4 and len(rules['checkpoints'])*rules['maximumCheckpointWeight']==24)
rels=list((root/'Assets/_Project/Data/Relationships/CSI_Environment').glob('*.asset'))
def value(text,key): return re.search(r'^  '+key+r': (.+)$',text,re.M)[1]
authored={value(p.read_text(),'relationshipId'):p.read_text() for p in rels}
check('All six authored relationship IDs/pairs/weights used, none invented',set(authored)=={r['id'] for r in rules['relationships']} and all(value(authored[r['id']],'sourceId')==r['sourceId']
    and value(authored[r['id']],'targetId')==r['targetId'] and float(value(authored[r['id']],'scoreWeight'))==r['weight'] for r in rules['relationships']))
support=(root/'Assets/_Project/Data/Requirements/REQ_BrotherCaseSupported.asset').read_text()
check('Full-chain scoring matches current supported-Paul requirement IDs',set(re.findall(r'findingId: (FIND-\S+)',support))==set(rules['supportedFindingIds'])
    and set(re.findall(r'relationshipId: (REL-\S+)',support))==set(rules['supportedRelationshipIds']) and 'evidenceId: '+rules['supportedStatementId'] in support)
baseline=json.loads((out/'protected-baseline.json').read_text())
changed=[p for p,h in baseline.items() if hashlib.sha256(Path(p).read_bytes()).hexdigest()!=h]
check('All pre-existing ground-truth and scoring definitions, content and authored gameplay assets unchanged',not changed)
# Phase 3 audited code baseline is applicable except the explicit score/DTO/end-review hooks.
previous=json.loads((root/'docs/evidence/assessment-phase3/protected-before.json').read_text())
allowed={'Scenario_CSI_Environment.asset','DeductionScorer.cs','CompletionScreenUI.cs','EvidenceBoardController.cs','EndingManager.cs','AssessmentSessionBuilder.cs','AssessmentSessionUploadDto.cs','AssessmentUploadService.cs'}
integration_paths={str(backend/'src/lib/scoring'/name) for name in ('registry.ts','types.ts','verify.ts')}
preserved={p:h for p,h in previous.items() if Path(p).name not in allowed and p not in integration_paths}
changed_previous=[p for p,h in preserved.items() if hashlib.sha256((root/p).read_bytes()).hexdigest()!=h]
check('Previously audited procedure gates, sequence, board/HYP/ending requirements remain unchanged',not changed_previous)
scenario=(root/'Assets/_Project/Data/Assessment/Scenario_CSI_Environment.asset').read_text()
check('Active scoring alone is 1.7.0; scenario 1.1.0 / ground truth 1.2.0 unchanged',all(k+': '+rules[k] in scenario for k in ('scenarioVersion','groundTruthVersion','scoringRulesVersion')))
registry=(backend/'src/lib/scoring/registry.ts').read_text()
check('Append-only registry retains 1.0.0-1.6.0 and old missing-version fallback',all('"'+v+'": CSI_ENVIRONMENT_SCORING_RULES' in registry for v in ['1.0.0','1.1.0','1.2.0','1.3.0','1.4.0','1.5.0','1.6.0','1.7.0'])
    and re.search(r'LATEST_SCORING_RULES_VERSION[^=]*=\s*\{\s*"CSI-ENVIRONMENT-001": "1.0.0"',registry))
ui=(root/'Assets/_Project/Scripts/Deduction/HypothesisCheckpointUI.cs').read_text()
check('Gameplay choice UI still renders no weights',not re.search(r'\.weight\b|maximumWeight',ui))
model=(root/'Assets/_Project/Scripts/Assessment/FinalDeductionAssessment.cs').read_text()
check('No procedure/time metric enters the new numerical model',not re.search(r'procedureOrderCompliance|timeScore|targetTimeSeconds|swabbingDone|fingerprintingDone|ScenePhotographyAssessment|MeasurementAssessment',model))
check('New model never advances procedures, relationships or conclusion state',not re.search(r'\.TryAdvance\(|\.Mark[A-Z]|\.CompleteAllSelected\(|\.TryConnect\(|AcceptEnding',model))
dto=(root/'Assets/_Project/Scripts/Assessment/AssessmentSessionUploadDto.cs').read_text()
check('Payload version remains 1 with additive essential events/category result',all(s in dto for s in ('public int payloadVersion = 1','deductionAssessmentEvents','hasDeductionAssessment','deductionAssessment = new()')))
check('Stable historical enum ordinals retained; snapshot is appended', (root/'Assets/_Project/Scripts/Logging/SessionEventType.cs').read_text().strip().endswith('DeductionAssessmentSnapshot\n}'))
passed=sum(p for _,p in results)
lines=[('PASS: ' if p else 'FAIL: ')+label for label,p in results]
lines.append(f'{passed} passed, {len(results)-passed} failed; {len(baseline)} current and {len(preserved)} prior protected files compared.')
(out/'content-and-scope-audit.txt').write_text('\n'.join(lines)+'\n')
print('\n'.join(lines))
if changed or changed_previous:print('Changed protected files:',changed+changed_previous)
raise SystemExit(0 if passed==len(results) else 1)
