# Session record simplification — 18 October 2026

Implemented locally in the Unity project and `C:/Users/Roduf Eleu/Desktop/vicsi-dashboard`. No commit, push, deployment, seed execution, migration execution, or production write was performed. The working trees already contained unrelated changes; those were preserved.

## Source audit and question inventory

The audit preceded implementation and used current source rather than previous dashboard assumptions:

- `FinalDeductionAssessment.cs` and `Assets/Resources/DeductionScoring_1_7_0.json`: authoritative Unity scoring rules 1.7.0.
- `ReasoningCheckpointManager`, `ReasoningCheckpoints_LastDrink.asset`, and `HypothesisCheckpointManager`: contextual questions and separate workflow checkpoints.
- `EvidenceRelationshipsReview`, relationship definitions and current scoring JSON: review availability, locked answers and penalties.
- `EndingManager`, `ConclusionStateManager`, and conclusion assets: actual dynamically offered conclusion choices and resolved ending.
- `SessionLogger`, event call sites, session builder/upload service, API upload handler, event envelope, verifier, scoring registry and deduction scorer.
- Supabase migrations 009/010/011/014/017/019: relational session storage, event JSONB, atomic upload, verification, authorization and compatibility. This was a local schema-source audit, not an assertion that a remote database has applied every migration.

The exact question text, **every answer choice and its numerical weight**, and conditional conclusion choices are in [the scored inventory](../evidence/session-simplification/questions-and-formula.md). [The original code audit](../evidence/session-simplification/current-code-audit.json) preserves the pre-edit assets and event call sites. [The fresh frozen question records](../evidence/session-simplification/frozen-questions.json) additionally include the live contextual text shown before RC-05, including Paul's statement.

Actual assessed categories are **Reasoning**, **Evidence Relationships**, and **Final Conclusion**. There are 13 scored questions: six reasoning, six relationship review and one conclusion. HYP-01 and HYP-03 remain unscored workflow checkpoints. HYP-02 integrates RC-03 rather than adding a second assessed answer. Their initial choices, justification choices and training correctness flags are documented separately in [the complete workflow inventory](../evidence/session-simplification/workflow-questions.md); their deduction contribution is zero.

Reasoning option weights in displayed A/B/C/D order:

| Question | Subject | A | B | C | D |
| --- | --- | ---: | ---: | ---: | ---: |
| RC-01-PETER | Peter's nervous departure | 1 | 3 | 4 | 2 |
| RC-02-SCENE | Initial scene interpretation | 1 | 4 | 1 | 3 |
| RC-03-FINGERPRINT | Limits of Paul's knife fingerprint | 1 | 3 | 4 | 2 |
| RC-04-KNIFE-WOUND | Knife/wound consistency | 1 | 4 | 3 | 1 |
| RC-05-TIMELINE | Vendor observation versus Paul's account | 1 | 4 | 3 | 2 |
| RC-06-BLOODSTAIN | Bloodstain interpretation | 1 | 4 | 3 | 2 |

Each reasoning answer has maximum 4, with a fixed component denominator of 24. Unanswered questions contribute zero under the existing scorer.

The relationship questions concern bottle/contact, knife/wound, knife/print, Paul's timeline, Peter's departure, and bloodstain interpretation. Correct options respectively are C, C, B, A, A, B. Each correct answer earns 1; a wrong interpretation earns 0 and incurs the existing 1-point unsupported-interpretation penalty. The record displays that net contribution as **weight -1**, alongside correct weight 1. This represents the existing calculation, not a new negative-marking policy.

The conclusion prompt is `Submit your conclusion:`. Paul, Peter and Inconclusive are possible authored options. Snapshot only choices actually offered: Inconclusive is hidden when a supported case exists. Conditional normalized credits: supported Paul 1; premature Paul 0.25; Peter 0; Inconclusive with insufficient support 1; Inconclusive despite full support 0.25. The latter scorer case remains documented even though normal picker gating hides it after a supported case exists. Full support requires the authored four findings, four core supported relationships and discovered vendor statement.

## Existing calculation retained

```text
Reasoning             = 60 * earned checkpoint weight / 24
Evidence Relationships = 25 * max(0, earned relationship weight
                                  - unsupported count * 1) / 6
Final Conclusion      = 15 * contextual conclusion credit
Final Deduction Score = clamp(sum of the three components, 0, 100)
```

Unity's calculation was not replaced. The existing server deduction scorer still verifies the frozen assessment against registered versioned rules. Snapshot presentation weights are checked against those rules; they never become independent grade inputs. The dashboard reads stored verified component and total values. Its formula text explains those values without calculating another grade. The current path skips unused legacy evidence/procedural analytics; historical versions retain their existing scoring paths.

## Procedure timeline

One shared, versioned allowlist is mirrored exactly between `Assets/Resources/ProcedureTimeline_1.json` and dashboard `src/lib/scoring/data/procedureTimeline.v1.json`. Existing session elapsed milliseconds and original sequence numbers are preserved, including ties. Completed repeat work remains visible; duplicate copies of the same original sequence do not create another row. No procedure earns a score.

Retained completion/activity event types:

- SceneTurnoverCompleted, CSIForm1Received, IOCObservationRecorded, IOCAssessmentCompleted, SOCORequested, CSIForm2Completed, SOCOTurnoverCompleted.
- ScenePhotoCompleted, ScenePhotographyPhaseCompleted, ScenePhotographyCompleted.
- EvidenceTraceMarked, MeasurementCompleted, EvidencePairMeasured, SketchSubmitted, HypothesisSubmitted.
- EvidenceFingerprintProcessed, EvidenceSwabbed, FingerprintIdentified, EvidenceAddedToInventory, EvidenceDeposited, CustodyPassCompleted.
- WitnessStatementDiscovered, BodyDocumentationRecorded, BodyPreparedForRemoval, BodyRemoved.
- EvidenceInventoryConfirmed, FinalSceneSurveyCompleted, SceneReleaseConfirmed, InvestigationCompleted.
- EvidenceStatusChanged only for PhotographedRaw, ChalkMarked, Marked, PhotographedFinal, Logged, Collected, Sealed and Processed.

Descriptions use the actual target/marker/sample IDs, with current IOC/scene-photo labels from the workflow. Successful fingerprint processing and swabbing previously changed silent flags; two completion events now record those actual successful operations. Hypothesis submission is an unscored completed checkpoint activity.

UI opens/closes, hovering, tool selection, failed attempts, blocked transitions, readiness flags, automatic unlocks, debug/polling and internal state events do not appear. Assessment eligibility/submission/snapshot/conclusion events remain in the upload where required for verification but do not clutter the procedure timeline.

## Minimal upload and database impact

The implemented 1.7.0 wire structure is illustrated by [the actual fresh Unity request](../evidence/session-simplification/current-unity-upload.json):

```text
payloadVersion, sessionId
studentId, classId, isTestSession
scenarioId, scenarioVersion, groundTruthVersion, scoringRulesVersion, clientVersion
startedAtUtc, completedAtUtc, durationSeconds
selectedConclusionId, resolvedEndingId
hasDeductionAssessment, deductionAssessment  // existing calculated Unity result
includesRawEventLog: false
orderedEvents[]:
  sequenceNumber, timestampMs, eventType, targetId, payload
  completed procedure events plus necessary verification events
  DeductionAssessmentSnapshot.payload.assessment:
    existing frozen scorer input and necessary support/eligibility facts
    questionRecordVersion: 1
    questionRecords[]:
      id, category, dimension, question, wasAvailable, answeredSeconds
      options[]: id, text, weight, endingId where needed
      selectedAnswerId, selectedAnswerWeight, maximumWeight
```

Current uploads omit redundant legacy evidence/report/relationship/hypothesis result arrays, photography/measurement analytics, procedural violations, legacy client score summaries, finding/insight decorations and the duplicate essential-event array. Retained verification facts are technically necessary, not new dashboard sections. Current uploads do not truncate these records at the optional raw/debug event limit. Old-version serialization remains compatible.

**No database change is required.** The existing atomic upload already stores event payloads as JSONB; the additive versioned question snapshot resides there. Session associations, event rows and verified score categories continue using existing tables. No table, column, historical row, scoring version or ground-truth key was deleted or rewritten. The previously registered historical ground-truth snapshot lacks the later Paul-timeline relationship: it was intentionally preserved, while current 1.7.0 relationship rules remain authoritative for current grading.

Token-bound student/class/assignment/scenario identity, rate limiting, payload idempotency/conflict detection, atomic submission, retry identity checks and cookie-scoped RLS reads remain in place. Submitted display names cannot override server associations.

## Student and instructor pages

Student `/results`: Date, Scenario, Duration, Deduction Score, View. Session detail `/results/[sessionId]`: basic information, completed procedure timeline, every scored question with all option weights and highlighted selection/received weight, then stored component results and one final score.

Instructor `/class-results`: Student, Latest attempt/date, Scenario, Duration, Deduction Score, View. Shows the latest matching attempt per student/class. Existing basic search/class/date filters remain. `/class-results/students/[studentId]` provides that student's session history; `/class-results/[sessionId]` uses the same educational detail, with student/class identification.

Primary views no longer present analytics charts, score distributions, insights, remediation/common-error analytics, procedural percentages or scores, evidence accuracy, class/expert benchmarks, excessive KPI cards, graphs, training summaries or analytics navigation tabs. Historical/export support code remains available. Primary detail reads skip unused legacy child-table/analytics queries, and session lists bulk-read only the stored deduction components they need.

The layout uses restrained navy typography, off-white surfaces, subtle borders, compact tables and a simple chronological timeline. Responsive screenshots at 375/768/1366 pixels are in dashboard `docs/evidence/session-simplification/browser/`, including scrolled question and final-score views.

## Historical compatibility

Older uploads remain accepted. Older records display stored authoritative scores and original recorded answers. Where event logs are absent, existing hypothesis child rows and the stored conclusion provide a limited factual fallback, with time explicitly marked unrecorded. Missing question text, options and weights are never supplied from today's question bank. Corrupt optional snapshots fall back safely rather than crashing. A pending/review-required score is not presented as a verified final grade. No historical data was backfilled.

## Files changed for this task

Unity paths:

- `Assets/Resources/ProcedureTimeline_1.json` and generated `.meta`.
- `Assets/_Project/Scripts/Assessment/EducationalSessionRecord.cs` and `.meta`.
- `Assets/_Project/Scripts/Assessment/FinalDeductionAssessment.cs` (additive snapshot fields/capture; original formula retained).
- `Assets/_Project/Scripts/Assessment/AssessmentSessionBuilder.cs`, `AssessmentSessionUploadDto.cs`, `AssessmentUploadService.cs`.
- `Assets/_Project/Scripts/Logging/SessionEventType.cs` and `Assets/_Project/Scripts/CaseFile/EvidenceStateManager.cs` (completion instrumentation).
- Existing `EvidenceRelationshipsReviewTest.cs`, `BrotherPathTest.cs`, `FinalDeductionAssessmentTest.cs`; new `EducationalSessionRecordTest.cs` and `.meta`.
- `Tools/Audit/session-simplification-audit.mjs`, `Tools/Audit/session-workflow-question-audit.mjs`, this report and evidence artifacts.

Dashboard paths relative to `C:/Users/Roduf Eleu/Desktop/vicsi-dashboard`:

- `src/lib/sessionRecord.ts`, `src/lib/results.ts`.
- `src/lib/scoring/assessmentEventEnvelope.ts`, `types.ts`, `verify.ts`, `data/procedureTimeline.v1.json`.
- `src/components/SessionRecord.tsx`, `SessionTable.tsx`, `ResultFilters.tsx`, `AppShell.tsx`, `src/app/globals.css`.
- `src/app/results/page.tsx`, `src/app/results/[sessionId]/page.tsx`.
- `src/app/(dashboard)/class-results/page.tsx`, `[sessionId]/page.tsx`, `students/[studentId]/page.tsx`.
- `src/lib/__tests__/sessionRecord.fixture.ts`, `sessionRecord.test.tsx`, `sessionUploadApi.test.tsx`, and the existing dashboard list empty-state assertion.
- Existing scoring `lastDrink.test.ts` and `trainingSeparation.test.ts`: preserve immutable historical data checks while comparing current Unity content semantically; do not treat a generated timestamp or an intentionally changed logger/upload source hash as immutable scoring data.
- `Tools/testing/session-api-fixture.ts`, `tsx-loader.mjs`, `run-session-tests.mjs`, `render-session-fixtures.tsx`, `session-responsive.test.mjs`, `package.json`, browser evidence.

The upload API route, authentication policies and SQL migrations were inspected and reused. Pre-existing edits in those files are not claimed as changes made by this task.

## Validation and practical limits

| Requested check | Evidence / result |
| --- | --- |
| 1. Current Unity upload | Fresh real Unity managers build/serialize the request; isolated Unity transport accepts it. Same actual JSON passes the real API handler with an in-memory database substitute and server verification. No live HTTP/database upload was attempted. |
| 2. Timeline timestamps/order | Original retained sequence/timestamps match raw Unity logger records; tie/repeat ordering tested. |
| 3. No technical noise | Explicit allowlist tested against UI/tool/blocked/debug/unlock examples. |
| 4–8. Questions/options/weights/selection/earned weight | All 13 captured; 12 four-option questions plus actual offered conclusion choices. Fresh snapshot text/options/weights match pre-edit asset audit. Forged weights, omitted options, duplicate questions and changed selections reject. |
| 9–10. Components and final score | Fresh complete case exactly 60 + 25 + 15 = 100 in Unity/server. 576 real Unity scoring vectors cover fractional checkpoints, all 64 correct/incorrect review patterns and three conclusions: server agrees within float precision (< 0.00002), with exactly matching two-decimal display. No claim of bit-identical Unity float/JavaScript double storage. |
| 11–14. Student/instructor list/detail | Actual page/server-rendered paths tested with isolated authenticated fixtures. Browser checks cover both roles, selections and score; no live Supabase browsing asserted. |
| 15. Historical records | Missing snapshots/events/weights, child-only answers, corrupt optional snapshots and historical score rendering pass without fabricated data or crashes. |
| 16. Build/TypeScript | Production Next build and `tsc --noEmit` pass. ESLint exits 0: zero errors, seven existing warnings in legacy scoring tests/seed helper. |
| 17. Authentication/authorization | 27 existing token/authorization tests pass; API tests exercise unauthorized requests, binding, forged ownership, rate limits, duplicate receipt and conflict handling. Existing RLS code/policies retained; live SQL RLS execution was not performed. |

Recorded suites in `docs/evidence/session-simplification/`:

- Unity education fixture: **70 passing assertions**, zero failures, plus 576 exported scoring cases (`unity-record-tests.txt`).
- Unity authoritative scoring: **41 passing assertions**, zero failures (`unity-scoring-tests.txt`).
- Unity upload/retry/identity: **33 passing assertions**, zero failures (`unity-upload-service-tests.txt`).
- Session/API tests: **17 pass**, zero skipped (`record-api-tests.txt`).
- Responsive browser: **21 pass**, zero skipped (`browser-tests.txt`).
- Existing dashboard regressions: **42 pass** (`dashboard-existing-tests.txt`).
- Existing scoring regressions: **217 pass, two optional external-fixture tests skipped, zero failures** (`scoring-tests.txt`). Skips are the separate bottle-identity and custody upload fixtures, not the fresh current-session upload test.
- Existing authorization: **27 pass** (`authorization-tests.txt`).
- Build, TypeScript and lint output: `build.txt`, `typescript.txt`, `lint.txt`.

Unity's graphical Editor exhausted memory during repeated scene reloads on this machine. Affected test Editor processes were closed; verification completed successfully in fresh isolated headless Unity processes. This validates source compilation and local gameplay-manager fixtures, not a new headset/device playthrough. Closeout timestamps in the upload integration fixture are logged fixture completions; they are not presented as a manual run through each closeout control. The test sandbox keeps automatic production upload disabled.

No deployment has been attempted. A live staging upload and actual database-policy execution remain external integration checks, explicitly outside these local validation results.
