// Actual Unity event envelope -> existing backend adapter/scorer. No network or DB calls.
import { readFileSync } from 'node:fs';
import { dirname, resolve, join } from 'node:path';
import { homedir } from 'node:os';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { test } from 'node:test';
import assert from 'node:assert/strict';
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const backend = process.env.VICSI_DASHBOARD_ROOT || join(homedir(), 'Desktop', 'vicsi-dashboard');
const { mergeAssessmentEvents } = await import(pathToFileURL(join(backend, 'src/lib/scoring/assessmentEventEnvelope.ts')));
const { resolveSessionScoring } = await import(pathToFileURL(join(backend, 'src/lib/scoring/registry.ts')));
const { scoreDeductionEvents } = await import(pathToFileURL(join(backend, 'src/lib/scoring/deductionAssessmentScorer.ts')));
const body = JSON.parse(readFileSync(process.env.VICSI_BODY_PAYLOAD || join(root, 'docs/evidence/body-removal/unity-payload.json'), 'utf8'));
const reference = JSON.parse(readFileSync(join(root, 'docs/evidence/assessment-phase4/unity-completed-payload.json'), 'utf8'));
const milestones = body.deductionAssessmentEvents.filter(e => e.eventType.startsWith('Body'));
test('actual Unity body milestones ingest with raw upload disabled', () => {
  assert.equal(body.payloadVersion, 1);
  assert.deepEqual(body.orderedEvents, []);
  assert.deepEqual(milestones.map(e => e.eventType), ['BodyDocumentationRecorded', 'BodyPreparedForRemoval', 'BodyRemoved']);
  const merged = mergeAssessmentEvents(body);
  for (const event of milestones) assert.ok(merged.orderedEvents.some(e => e.sequenceNumber === event.sequenceNumber && e.eventType === event.eventType));
  assert.ok(body.evidenceResults.some(e => e.evidenceId === 'BODY-A' && e.finalStatus === 'Logged'));
  assert.ok(milestones.find(e => e.eventType === 'BodyRemoved').payload.some(p => p.key === 'scored' && p.value === 'false'));
});
test('capped raw log and duplicate milestone copies merge once', () => {
  const merged = mergeAssessmentEvents({ ...body, orderedEvents: milestones.slice(0, 1) });
  assert.equal(merged.orderedEvents.filter(e => e.eventType.startsWith('Body')).length, 3);
});
test('actual removal milestones and repeated blocked attempts change no server deduction result', () => {
  const rules = resolveSessionScoring(reference.scenarioId, reference.groundTruthVersion, reference.scoringRulesVersion).scoringRules.deduction;
  const context = { sessionId: reference.sessionId, scenarioId: reference.scenarioId, selectedConclusionId: reference.selectedConclusionId, resolvedEndingId: reference.resolvedEndingId };
  const original = mergeAssessmentEvents(reference).orderedEvents;
  const before = scoreDeductionEvents(original, context, reference.completedAtUtc, rules);
  let sequence = Math.max(...original.map(e => e.sequenceNumber)) + 1;
  const extra = milestones.map(e => ({ ...e, sequenceNumber: sequence++ }));
  const blocked = Array.from({ length: 100 }, () => ({ sequenceNumber: sequence++, timestampMs: 999999, eventType: 'BodyRemovalBlocked', targetId: 'BODY-A', payload: [{ key: 'scored', value: 'false' }] }));
  const merged = mergeAssessmentEvents({ ...reference, orderedEvents: [...original, ...blocked], deductionAssessmentEvents: [...reference.deductionAssessmentEvents, ...extra] });
  assert.deepEqual(scoreDeductionEvents(merged.orderedEvents, context, reference.completedAtUtc, rules), before);
});
test('historical envelope behavior and essential-event rejection remain unchanged', () => {
  const old = { ...body, scoringRulesVersion: '1.6.0' };
  assert.equal(mergeAssessmentEvents(old), old);
  assert.throws(() => mergeAssessmentEvents({ ...body, deductionAssessmentEvents: [{ ...milestones[0], eventType: 'unexpected-type' }] }));
});
