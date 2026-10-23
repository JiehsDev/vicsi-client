// Two cross-runtime checks against the actual JsonUtility payload captured by the
// focused Unity suite. No upload/network/DB operation is performed.
import { readFileSync } from 'node:fs';
import { dirname, resolve, join } from 'node:path';
import { homedir } from 'node:os';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { test } from 'node:test';
import assert from 'node:assert/strict';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'..');
const backend=process.env.VICSI_DASHBOARD_ROOT || join(homedir(),'Desktop','vicsi-dashboard');
const { resolveSessionScoring }=await import(pathToFileURL(join(backend,'src/lib/scoring/registry.ts')));
const { scoreDeductionEvents }=await import(pathToFileURL(join(backend,'src/lib/scoring/deductionAssessmentScorer.ts')));
const { mergeAssessmentEvents }=await import(pathToFileURL(join(backend,'src/lib/scoring/assessmentEventEnvelope.ts')));
const payload=JSON.parse(readFileSync(join(root,'docs/evidence/assessment-phase4/unity-completed-payload.json'),'utf8'));
const rules=resolveSessionScoring(payload.scenarioId,payload.groundTruthVersion,payload.scoringRulesVersion).scoringRules.deduction;
const context={sessionId:payload.sessionId,scenarioId:payload.scenarioId,selectedConclusionId:payload.selectedConclusionId,resolvedEndingId:payload.resolvedEndingId};
test('actual Unity JsonUtility payload reproduces client component values on backend',()=>{
  const result=scoreDeductionEvents(mergeAssessmentEvents(payload).orderedEvents,context,payload.completedAtUtc,rules);
  assert.equal(result.status,'complete',result.reviewReasons.join(' '));
  assert.equal(result.totalScore,payload.deductionAssessment.totalScore);
  for(const [client,server] of [['reasoningComponent','reasoningCheckpointComponent'],['relationshipComponent','deductionRelationshipComponent'],['conclusionComponent','finalConclusionComponent']])
    assert.ok(Math.abs(payload.deductionAssessment[client]-result.categories[server])<.0001);
});
test('actual capped Unity payload still contains graded responses with raw log disabled',()=>{
  assert.equal(payload.orderedEvents.length,1);
  assert.ok(payload.deductionAssessmentEvents.some(e=>e.eventType==='ReasoningCheckpointSubmitted'));
  const result=scoreDeductionEvents(mergeAssessmentEvents({...payload,orderedEvents:[],includesRawEventLog:false}).orderedEvents,context,payload.completedAtUtc,rules);
  assert.equal(result.status,'complete',result.reviewReasons.join(' '));
  assert.equal(result.totalScore,payload.deductionAssessment.totalScore);
});
