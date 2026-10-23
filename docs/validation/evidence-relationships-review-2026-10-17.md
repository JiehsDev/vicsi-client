# Evidence Relationships Review — 2026-10-17

The interactive board is replaced by a locked, one-question-at-a-time Evidence Relationships Review. The final results manifest identifies accepted runs separately from diagnostic attempts.

## Interaction and retained data

The existing utility-menu reference now opens **Relationships → Evidence Relationships Review**. One earned relationship is presented at a time with four interpretations, explicit selection and a separate confirmation. Confirmed answers cannot be edited. B closes the review; reopening resumes unanswered questions. No immediate correctness, points or answer-key feedback is shown.

The review reuses the reasoning checkpoints' question renderer with its flexible-height answer rows, 20-unit horizontal/16-unit vertical padding, bounded scrolling, shared wall-safe placement, controller navigation, pointer selection, input ownership and confirmation guards. It uses a separate renderer instance and separate response records; it does not create extra reasoning-checkpoint responses.

`EvidenceBoardController` is now a compatibility adapter. The old EvidenceBoardCanvas stays inactive; its manual navigation, pending-card selection, connection creation and line rendering implementation is retired. TryConnect is a no-op and new snapshots contain no board connections. Existing serialized UnityEvent references work without manual rewiring.

RelationshipDefinition assets, all six IDs, node/source/target IDs, RelationshipRecord/RelationshipStateManager, EvidenceNode, ConnectionLine, NodePairId, DeductionBoardPair, existing relationship-result records and legacy board snapshot fields are retained. Correct interpretations provide the existing Completed support used by conclusion requirements; incorrect interpretations remain locked responses without manufacturing that support. Legacy pair APIs cannot change a relationship once its review answer is locked.

## Questions

All six existing equally weighted relationships remain in the denominator. The four required groups cover Paul's knife print, knife/injury consistency, Paul's timeline versus `statement_vendor_paul_3am`, and blood-spatter reconstruction. The existing bottle/gathering and Peter/departure corroboration relationships are also retained. All question text and four choices per question are in [the authored question report](relationship-review-questions-2026-10-17.md).

Eligibility uses the existing authoritative finding/statement/evidence support rules. The timeline question cannot be answered before the independent vendor statement is discovered. RC-03 still gates that follow-up. Students must confirm all currently earned questions before submitting their final conclusion; later discoveries can add another required review question. This retains premature and inconclusive paths without requiring undiscovered facts.

## Scoring and storage

Deduction Performance remains exactly **Reasoning Checkpoints 60% + Evidence Relationships 25% + Final Conclusion 15%**. Procedural activities remain non-scored Training Analytics.

The additive `relationshipReviewVersion: 1` snapshot contract includes locked relationship ID, interpretation ID, response order and answer time. Both Unity and the server resolve IDs through the mirrored authored rules. Each supported correct interpretation earns its existing relationship weight; an incorrect interpretation uses the existing unsupported-relationship penalty. The existing six relationship weights and fixed denominator are unchanged. Manual completed IDs/board links cannot override review answers. Existing conclusion credit and checkpoint weighting are unchanged.

Historical snapshots with missing/zero review version retain their manual-board scoring path under 1.7.0; older scoring versions remain untouched. No historical snapshot is reinterpreted as a review response.

`RelationshipInterpretationSubmitted` events are retained in the essential assessment envelope when raw events are disabled/capped. The conclusion snapshot freezes the choices. The server checks each frozen answer against its unique pre-conclusion locked event and rejects malformed, duplicate, unknown or mismatched answers. The essential envelope cap increases from 32 to 48.

Existing relationship DTOs retain source/target/state and add interpretation metadata. Supabase's existing `session_relationships` records continue unchanged; interpretation IDs/order/time are stored in the existing `session_events` payload and frozen snapshot. No SQL migration or live database operation is required for the new event shape.

## Dashboard impact

The sibling dashboard workspace is `C:/Users/Roduf Eleu/Desktop/vicsi-dashboard`. The shared student/instructor result component keeps **Evidence Relationships**, shows selected interpretation text and confirmation order, and gives support feedback only in assessed results. Historical sessions retain readable recorded-state labels. Reasoning Development, Final Conclusion and the existing scored-component names/weights remain unchanged.

The scorer, event-envelope validator, presentation adapter, shared result panel and mirrored rules JSON are updated locally. This work does not deploy those changes.

## Validation notes

Accepted Unity results: **391 assertions passed, zero failures across 11 suites**. The final Editor probe reports no C# compilation failures, zero console entries matching `error CS`, a clean saved CSI_Environment outside Play mode, no pending diagnostic request, restored Standalone/Android XR startup, an inactive board and zero active EvidenceNode/ConnectionLine objects.

| Coverage | Accepted result |
| --- | --- |
| Review opens, valid choices, flexible heights/no overflow, explicit confirm, locking, no immediate correctness, saved responses, no board manipulation, late vendor discovery | EvidenceRelationshipsReviewTest: 41 pass |
| Exactly 60/25/15, six checkpoint records, reviewed relationship grading, invalid/missing response rejection, completed upload snapshot | FinalDeductionAssessmentTest: 41 pass |
| Six checkpoint interactions and RC-03/vendor dependency | ReasoningCheckpointTest: 62 pass; VendorFollowupTest: 17 pass |
| Final conclusion paths, stale picker guards, full scenario story | EndingManagerTest: 9 pass; BrotherPathTest: 56 pass; InconclusivePathTest: 11 pass; StoryContentTest: 38 pass |
| Closeout, finalized session, restart and lobby return | SOCOCloseoutTest: 48 pass; SettingsPauseMenuTest: 35 pass |
| Session payload and upload isolation | AssessmentUploadServiceTest: 33 pass |
| Backend regression suite | 209 pass, zero fail, 6 conditional skips |
| Current relationship scoring and integration, including all 4,096 six-question response combinations | 40 pass, zero fail |
| Shared student/instructor dashboard rendering | 29 pass, zero fail |
| Actual Unity wire artifacts: essential lock-event retention and completed client/server score parity | 2 pass, zero fail |
| Current Unity-scoped backend checks | 49 pass, zero fail, 1 conditional skip |
| TypeScript and scoped ESLint | Both exit 0 |

Backend runs overlap and are not summed as unique tests. All 15 requested validation areas are covered by the selected local checks, with physical headset and live database acceptance limits stated below.

The first Unity run was stopped after stale imported JSON supplied empty choices and exposed a repeated-open callback. The resource was explicitly imported, and the UI now guards missing content and only advances after a successful locked response. Accepted results are from later runs.

Initial backend runs exposed two stale unsupported-version fixtures naming the already-supported 1.7.0; these now use unsupported 1.8.0. A separate environment-enabled historical audit reports two pre-existing frozen-file mismatches: an older exported ground-truth fixture and old source hashes, including EvidenceStateManager, which this task did not edit. These attempts are retained separately from current review/scoring/layout acceptance. The ordinary backend suite and scoped current Unity checks have their own reports.

The current authored story flow passes all 38 checks. The full-case fixtures now confirm earned review interpretations instead of calling manual pair-link APIs, record only earned checkpoint answers, and acknowledge conclusion feedback before asserting closeout. The reasoning renderer and vendor dependency have independent interaction regressions; fixture helpers do not change production eligibility or auto-answer student questions.

A conclusion-picker regression covers facts arriving while the picker is open: a newly earned vendor relationship refuses the stale conclusion, returns to the review, and requires its locked interpretation before resubmission. Hiding an unconfirmed review preserves its unanswered state; changing a returned response copy cannot mutate the recorded answer.

After repeated scene/domain reloads the 8 GB validation machine's Editor grew to approximately 16.9 GB private memory and stopped responding. The saved scene had no unsaved marker. The stalled Editor was restarted for the final two case-flow reruns; prior accepted reports remain on disk. This environment failure is distinct from a failed gameplay assertion.

Physical Quest stereo, comfort and controller acceptance and a live Supabase upload are not asserted by local tests. No commit, push or deployment is authorized or performed.

## Editor wiring

None. RelationshipStateManager automatically installs the review component at runtime. The existing serialized utility-menu listener still calls EvidenceBoardController, which forwards to the review and updates its menu label. No scene or prefab resave is required for this feature. Legacy node and line scripts/assets remain dormant for serialization and historical data compatibility.

## Changed implementation

- Unity runtime: EvidenceRelationshipsReview, EvidenceBoardController, RelationshipStateManager, HypothesisCheckpointUI, EndingManager, FinalDeductionAssessment, CompletionScreenUI, UtilityMenuController, SessionEventType, AssessmentSessionBuilder, AssessmentSessionUploadDto, dialogue catalog and deduction rules JSON.
- Dashboard: mirrored deduction JSON, scoring types/scorer, assessment event envelope, presentation adapter and shared DeductionResultPanels. Existing Supabase tables, authentication, RLS, upload endpoint and student/instructor page routes are retained.
- Regression fixtures: new review UI/scoring tests and shared case-fixture helpers; migrated vendor, ending, supported/inconclusive and story diagnostics. Backend review scoring, upload parity, shared presentation and legacy-version tests cover the additive snapshot contract.

Exact accepted counts, report paths and final Editor state are recorded in [the validation manifest](../evidence/relationship-review/summary.json).
