Phase 4 implementation report — The Last Drink, 2026-10-12

**1. Exact previous 1.6.0 formula.** Two existing calculations were found. They were different and must not be confused.

The server's `overallScore` was a 0–1 ratio, displayed as a percentage:

`S16 = (1.5H + D + 1.5R + 2C) / sum(weights of defined categories)`.

When all four categories existed, the denominator was **6**. Undefined H or D was excluded from both numerator and denominator. R was defined for this case's nonempty five-relationship ground-truth roster. C was always defined, including 0 when absent/incorrect. The server rounded each applicable legacy ratio and the aggregate to four decimals.

| Input | Exact 1.6.0 meaning |
|---|---|
| H: reasoningAccuracy | Correct classified `HypothesisSubmitted.reasoningOptionId` events / all classified justification events. First-choice suspect/theory IDs were not graded. |
| D: documentationAccuracy | Accurate selected report lines / all classified selected report lines. Repeated selections were counted by the existing event replay. Excluded three pure completion lines: EVD-014/knife-print-on-handle, EVD-015/blood-spatter-swab-collection, EVD-018/bottle-standard-procedure. Unknown lines produced analytics warnings, not ratio denominator entries. |
| R: relationshipAccuracy | Sum of completed ground-truth relationship weights / total ground-truth weights. Ground truth 1.2.0 listed five weight-1 relationships; it omitted the later authored timeline contradiction. Unknown/unsupported relationships did not earn credit, but the server did not subtract them. Available/Selected states alone earned nothing. |
| C: finalConclusionAccuracy | 1 only for conclusion-brother + ending-brother-accused; 0 for Premature Paul, Peter, Inconclusive or absent/nonmatching IDs. |
| Training inputs | criticalRecall, relevantRecall, precision, distractorFallRate, proceduralCompliance, scenePhotography, measurement and procedureOrderCompliance all had aggregate weight **0**. Raw ratios/counts remained analytics. Phase 3 reasoning weights were ignored. |

The Unity post-case review instead displayed `U16 = 0.5 × completed correct relationship weight coverage + 0.3 × contradictory-link avoidance + 0.2 × board-review time efficiency`. Correct coverage used the actual current relationship assets (six weight-1 links). Avoidance was `1 - completed incorrect links / authored incorrect links`, defaulting to 1 when there were none. Time credit was 1 at ≤120 seconds, 0 at ≥300 seconds, linear between; its clock measured board review, not procedural processing. This separate Unity score did not include H, D or the final conclusion. Both historical paths remain available unchanged when 1.7.0 is not active.

**2. New exact 1.7.0 formula.** The frozen conclusion-time assessment uses:

`Q = sum(versioned weights of first locked checkpoint response IDs) / 24`

`L = max(0, supported completed relationship weight - unsupported current relationship count × 1) / 6`

`F = configured evidentiary-support credit for the selected conclusion/ending`

`Final Deduction Score = 60Q + 25L + 15F`, on **0–100**.

Six authored relationships currently have weight 1. Duplicate stable IDs/pairs cannot increase credit. No speed, procedure, legacy HYP/report ratio, improvement bonus or missing-category renormalization enters this formula. Invalid numerical configuration is rejected for review instead of hidden by a clamp; a final 0–100 safety clamp remains. The existing backend `verified_score` and `overallScore` retain their 0–1 storage convention; `deductionTotalScore` and component-point categories expose the 0–100 result without breaking historical display code.

**3. Version decision.** 1.7.0 is required because the grade's inputs, denominators and conclusion semantics changed. Added an append-only scoring definition/registry entry; 1.6.0 and every earlier definition were not overwritten. New Unity sessions explicitly send 1.7.0. Missing-version fallbacks remain at historical 1.0.0.

**4. Checkpoints.** All six Phase 3 IDs, prompts, options, response weights, categories and triggers are unchanged. The same 24-point maximum is used by both client and server. The server resolves weights from its own versioned response-ID map, ignoring uploaded `weight`, `maximumWeight` and client total. It compares the frozen response to the first chronological submitted event, preserving the response lock. No active-gameplay weights or correctness feedback were added.

**5. Missing/unanswered rule.** All six opportunities remain in the denominator for a submitted conclusion. Eligible but unanswered earns 0 for that opportunity. Never eligible because the corresponding discovery was not reached remains pending during an incomplete session; if the student concludes before reaching that information, it becomes an undiscovered/incomplete reasoning opportunity worth 0, not a smaller denominator. The result separately counts answered, eligible-unanswered and undiscovered opportunities.

Technical inconsistencies suppress the final grade and require review: facts satisfying a trigger without recorded eligibility; eligibility without matching trigger facts; missing checkpoint owner/UI/HYP coordinator; invalid/mismatched authored content; missing/duplicate/malformed states, inconsistent locked response events, or a missing conclusion-time snapshot. The client and server independently derive trigger readiness from frozen Form 1/IOC observations, actual confirmed finding IDs and physically discovered Vendor statement IDs. The current supported-Paul chain makes every checkpoint eligible, but deferred questions are not guaranteed answered before submission. Existing conclusion gates were not changed to force answers.

The system can detect these explicit state/owner/content failures; it cannot automatically diagnose every possible visual/input malfunction. An apparently healthy, eligible unanswered checkpoint is graded as unanswered. Eligibility and submission logs remain available for instructor investigation rather than pretending all missing responses have the same cause.

**6. Relationship component.** The scoring definition uses the **six existing authored relationships**, with their current IDs, node pairs and weight 1. It does not invent cards/relationships or award credit for card placement.

| Existing relationship | Support required for numerical credit |
|---|---|
| REL-knife-consistent-wound | Completed link plus FIND-BODY-SHARP-WOUND and FIND-KNIFE-WOUND-CONSISTENT |
| REL-paul-print-handled-knife | Completed link plus FIND-KNIFE-PRINT-BROTHER; the authored link means contact/handling, not guilt |
| REL-spatter-consistent-reconstruction | Completed link plus FIND-SPATTER-RECONSTRUCTION |
| REL-bottle-supports-brother | Completed contextual drinking/presence link plus the discovered bottle; its existing gameplay processing gate remains |
| REL-peter-departure-supported | Completed corroboration link plus received Form 1 |
| REL-paul-timeline-contradiction | Completed contradiction link plus physically discovered statement_vendor_paul_3am |

A completed known link without its supporting facts earns no credit and counts as unsupported. A current unmatched node-pair or unknown completed relationship also earns no credit and subtracts one relationship-weight unit. Duplicate pairs are counted once. Corrected/removed attempts do not leave a lasting deduction penalty. This is deduction quality, independent of procedural blocked attempts. All current authored relationship definitions are correct; unknown board pairings are stored visually without a matching semantic record, so a read-only board connection snapshot was added. No gameplay connection/finalization requirements were altered.

**7. HYP treatment.** HYP-01, integrated HYP-02 and HYP-03 remain intact, including justification choices, prerequisite/unlock logic and existing events. Their legacy justification-accuracy ratio contributes **0** to 1.7.0. RC-03 still records HYP-02's first fingerprint response; its later justification is not scored again. HYP-03 still supplies its existing synthesis interaction without a fourth hypothesis or a seventh checkpoint.

**8. Legacy report choices.** Existing observations, reasoning selections, unsupported assumptions, report recording and finding unlocks remain. The legacy documentationAccuracy ratio contributes **0** in 1.7.0, while its analytics remain exported/stored. This exclusion applies to all BODY-A, EVD-014, EVD-015 and EVD-018 report-line inputs, including body observations, knife interpretation/weapon overclaims, blood reconstruction/identity/fall overclaims, and bottle interpretation. The three Phase 1 procedural completion exclusions remain unchanged in their historical 1.6.0 definition. No report option was deleted or reworded.

**9. Double-counting analysis.** Checkpoints measure interpretation at discovery, relationships measure later supported integration, and the conclusion measures the final evidentiary decision. Those are intentionally separate moments. Legacy HYP justification accuracy (overlapping caution/fingerprint/synthesis) and report accuracy (overlapping finding interpretation and overclaims) are excluded from the new total to prevent triple-counting. Recall/precision/marker/fall-rate, procedural compliance, photography, measurement and procedure order remain excluded. Old Unity contradiction-avoidance and speed bonuses are also excluded. The new relationship quality deduction belongs inside the 25-point component, not an extra category or penalty on procedures.

**10. Conclusion implementation.** Uses unchanged conclusion/ending IDs and freezes the evidence/support state at accepted conclusion time. Full support requires exactly the existing REQ_BrotherCaseSupported chain: four analytical findings, the knife/wound, fingerprint/contact, spatter/reconstruction and Paul timeline contradiction completed relationships, and the physically discovered Vendor statement. Both implementations use the same authored ID configuration; the content audit compares it with the existing requirement asset.

**11. Supported Paul.** conclusion-brother + ending-brother-accused with the complete supported chain earns F=1, **15 points**. A claimed supported ending without that chain requires review rather than awarding suspect-guess credit.

**12. Premature Paul.** conclusion-brother + ending-premature-submission with an incomplete chain earns F=0.25, **3.75 points**. If a claimed premature ending conflicts with a fully supported chain, the result requires review. No additional total-score cap/bonus was invented.

**13. Peter.** conclusion-peter + ending-peter-unsupported earns F=0, **0 conclusion points**, because the existing case does not support that accusation. An earlier poor Peter checkpoint remains a chronological response, but does not permanently cap later improvement; for example, one weight-1 response followed by five weight-4 responses can still earn 92.5 with full relationships and supported Paul.

**14. Inconclusive.** CONCLUSION-INCONCLUSIVE + ending-inconclusive earns F=1 (**15 points**) when the authored supported-Paul chain is incomplete, and F=0.25 (**3.75**) if the full chain is already developed. This follows existing support-state semantics rather than treating every non-Paul choice as wrong. The actual picker already hides Inconclusive after full support; the lower-credit full-chain case handles externally stored/stale records without changing picker availability.

**15. Category outputs.** Added server categories `reasoningCheckpoints`, `deductionRelationships`, `finalConclusion` (ratios); `reasoningCheckpointComponent`, `deductionRelationshipComponent`, `finalConclusionComponent` (earned points); `deductionTotalScore`; earned/maximum/answered/eligible-unanswered/undiscovered checkpoint counts; `unsupportedDeductionRelationships`; and `deductionAssessmentComplete`. Legacy categories retain their existing keys for analytics/historical compatibility and have zero grade weight under 1.7.0. The additive client `deductionAssessment` supplies corresponding local component values, a final-score flag, complete/incomplete/requires_review status and review reasons. Only server recomputation supplies a verified grade.

**16. Procedures remain non-scored.** Photography, markers, measurements, swabbing, fingerprint development/lifting, packaging, sealing, B-storage, inventory/accountability, final survey, release, blocked actions and ProcedureOutOfOrder/procedureOrderCompliance are not numerical inputs. They remain required/gated/logged. No procedure gate/sequence, Phase 2 lesson or active procedural score language was changed; existing end-review headings already identify photography/measurement as training. Historical internal schema names remain.

**17. Board timer.** BeginReasoning/EndReasoning still accumulate board-review time. 1.7.0 uses it only as elapsed-time analytics; timeScore contributes zero and the end review no longer displays time-efficiency grade credit. Historical local scorer weights/timing remain on their original path. The live board says “Theory recorded.” instead of revealing a numerical score.

**18. Payload.** payloadVersion remains **1**, with all old fields retained. Added `deductionAssessmentEvents`, `hasDeductionAssessment` and `deductionAssessment`. A single append-only `DeductionAssessmentSnapshot` JSONL event stores frozen facts, selected conclusion/ending, checkpoint eligibility/IDs/order/timing, completed semantic relationships and current board pairs. It flushes before conclusion feedback. Essential eligibility/submission/snapshot/conclusion/completion events are exported independently of the optional raw-log flag/cap. This prevents a 5,000-event raw cap or disabled raw upload from silently erasing a graded response. Later closeout discoveries cannot rewrite the frozen assessment.

**19. Backend/registry.** Confirmed and edited only `C:/Users/Roduf Eleu/Desktop/vicsi-dashboard`; the obsolete csi-dashboard repository was not used. Added the append-only JSON/TS 1.7.0 definition and registry entry, a pure deduction scorer and a small ingest envelope adapter. The adapter merges essential events into the existing ordered event stream and rejects conflicting duplicates; existing generic session_events storage and numeric category RPC accept the new data. No database schema migration/API payload-version change was necessary, and no backend deployment/upload was performed. Verification binds 1.7.0 to existing scenario 1.1.0 / ground truth 1.2.0, suppressing grades for a mismatched pair. Culprit, evidence truth, relationship IDs and conclusion IDs are unchanged. The old ground-truth relationship omission is repaired only in the new scoring configuration, not by rewriting truth 1.2.0.

**20. Historical compatibility.** 1.0.0–1.6.0 definitions and all ground-truth definitions are byte-identical to the start of this phase. Historical sessions still resolve their explicit original versions; absent versions still resolve to 1.0.0. Verification branches to the new scorer only when rules include the 1.7.0 deduction definition. No archived outputs were rescored or migrated. The offline Tools/EvidenceScorer remains its existing diagnostic/analytics tool rather than becoming a second grade implementation.

**21. Worked examples.** These values were computed by the focused Unity fixtures, not estimated. Category weights and conclusion credits are currently research/system design choices requiring expert validation.

| Example | Checkpoint weights / earned | Relationship quality | Conclusion | Actual total |
|---|---|---|---|---:|
| A: Strong | 3,4,4,4,4,4 = 23/24 → 57.5 | Six supported weight-1 links, no unsupported pairs → 25 | Supported Paul → 15 | **97.5** |
| B: Mixed | 3,3,3,3,2,2 = 16/24 → 40 | Three supported links, no unsupported pairs → 12.5 | Premature Paul → 3.75 | **56.25** |
| C: Weak | 1,1,1,1,2,2 = 8/24 → 20 | No supported links, one unmatched pair; quality floors at 0 → 0 | Unsupported Peter → 0 | **20** |

All six maximum responses plus all supported relationships and Supported Paul produce **60+25+15=100**. A procedure error or corrected blocked action changes none of these arithmetic terms.

**22. Focused tests.** Added FinalDeductionAssessmentTest: pure normalization/conclusion fixtures, missing/technical state distinctions, unsupported/corrected pairs, malformed configuration/data, real ProcedureOutOfOrder logging, immutable snapshot, no state mutation, additive payload with raw disabled/capped, complete/incomplete behavior and real scene/session reset. Added backend deductionIntegration.test.ts for the new definition, 4,096 response combinations, canonical response-ID weights, locked event verification, semantic relationships, context-sensitive conclusions, envelope handling and historical isolation. Added ValidateDeductionIntegration.py for unchanged content/gates/historical definitions, exact configuration parity, six existing relationships and supported-chain IDs. The existing small Phase 1/3 scorer-isolation tests were reused; no full regression was run.

**23. Exact checks/counts.** Final focused results: **108 passed, 0 failed, 1 optional legacy audit skipped**. Counts below exclude compile/type/whitespace commands, which are reported separately.

| Exact invocation | Pass | Fail | Skip | Evidence |
|---|---:|---:|---:|---|
| Existing `Phase5PlayModeBatchRunner`, single file request: suite `FinalDeductionAssessmentTest` | 36 | 0 | 0 | [Unity runtime checks](../evidence/assessment-phase4/FinalDeductionAssessmentTest.txt) |
| `python Tools/ValidateDeductionIntegration.py` | 15 | 0 | 0 | [Content/scope audit](../evidence/assessment-phase4/content-and-scope-audit.txt); 134 current and 311 prior protected baseline entries compared |
| In vicsi-dashboard: `node --conditions=react-server --experimental-strip-types --test src/lib/scoring/__tests__/deductionIntegration.test.ts src/lib/scoring/__tests__/reasoningCheckpointSeparation.test.ts src/lib/scoring/__tests__/trainingSeparation.test.ts` | 55 | 0 | 1 | [Backend tests](../evidence/assessment-phase4/backend-focused-final.txt) |
| `node --conditions=react-server --experimental-strip-types --test Tools/ValidateDeductionWire.mjs` | 2 | 0 | 0 | [Actual Unity/backend wire parity](../evidence/assessment-phase4/wire-parity.txt); [Unity payload](../evidence/assessment-phase4/unity-completed-payload.json) |

The skipped test is Phase 1's optional external historical/gate hash audit, gated by VICSI_UNITY_PROJECT. Its unchanged-whole-file expectation for payload/end hooks is not the Phase 4 scope; the new explicit baseline audit verifies untouched historical definitions/gates and allows only the authorized integration surfaces. No full regression, Next production build, live upload, database mutation or physical-headset playthrough was run.

The initial Unity run passed 34 checks; the final 36-check run adds malformed-configuration/data coverage and an actual wire fixture. The first scope-audit iteration incorrectly included the intentionally changed registry/types/verification hooks in its untouched-file set; that scope filter was corrected, and the final audit passes. Repository-wide `git diff --check` reported pre-existing whitespace in Main and unrelated dashboard/parity files; targeted checks of Phase 4-modified tracked files pass in both repositories. Those unrelated edits were preserved.

**24. Unity compile.** **Zero C# errors** in Unity **6000.5.6f1**, active build target Android. Loaded Assembly-CSharp and six checkpoints; `compilationFailed=false`, `compiling=false`, `playing=false`, active scoring 1.7.0. [Compiled editor state](../evidence/assessment-phase4/compiled-editor-state.json), [restored final editor state](../evidence/assessment-phase4/final-editor-state.json). Main is restored without saving or overwriting scene edits. Existing baseline warnings remain. No APK build or physical VR readability validation was performed.

**25. Backend types/tests.** `npx.cmd tsc --noEmit` exited **0**, with **no TypeScript diagnostics**. The focused backend batch passed 55 tests, failed 0 and skipped the one optional legacy external hash audit described above; the actual C# JsonUtility payload also passed both cross-runtime parity checks. No production backend/dashboard page was redesigned and no backend was deployed.

**26. Phase 4 files.** Pre-existing Phase 1–3 and unrelated lobby/dashboard edits were preserved.

| File | Change |
|---|---|
| Assets/Resources/DeductionScoring_1_7_0.json (+ .meta) | Central authored 1.7.0 configuration |
| Assets/_Project/Scripts/Assessment/FinalDeductionAssessment.cs (+ .meta) | Models, canonical calculation and locked conclusion snapshot |
| Assets/_Project/Scripts/Assessment/ReasoningCheckpointManager.cs | Read-only readiness check for technical failure detection |
| Assets/_Project/Scripts/Assessment/AssessmentSessionUploadDto.cs | Additive essential events and assessment result |
| Assets/_Project/Scripts/Assessment/AssessmentSessionBuilder.cs | Export preserved essential records and new local categories |
| Assets/_Project/Scripts/Assessment/AssessmentUploadService.cs | Read-only active scoring-version accessor |
| Assets/_Project/Data/Assessment/Scenario_CSI_Environment.asset | New sessions select 1.7.0; other version fields unchanged |
| Assets/_Project/Scripts/Deduction/DeductionScorer.cs | 1.7.0 relationship-only branch; historical formula preserved |
| Assets/_Project/Scripts/Deduction/EvidenceBoardController.cs | Read-only current connection snapshot; neutral live result wording |
| Assets/_Project/Scripts/Deduction/CompletionScreenUI.cs | Existing final review uses 1.7.0 components; incomplete/review results withhold grade |
| Assets/_Project/Scripts/Ending/EndingManager.cs | One snapshot hook after accepting the unchanged ending |
| Assets/_Project/Scripts/Logging/SessionEventType.cs | Append snapshot enum member |
| Assets/_Project/Scripts/Testing/FinalDeductionAssessmentTest.cs (+ .meta) | Small Unity fixture/payload/reset suite |
| Tools/ValidateDeductionIntegration.py | Focused definition/content/scope audit |
| Tools/ValidateDeductionWire.mjs | Two actual Unity/backend wire parity checks |
| vicsi-dashboard/src/lib/scoring/data/csiEnvironment.v1_7.json and .ts | Identical authoritative configuration and new rules export |
| vicsi-dashboard/src/lib/scoring/registry.ts and types.ts | Additive registration, models and category keys |
| vicsi-dashboard/src/lib/scoring/deductionAssessmentScorer.ts | Pure final scorer and snapshot/event verification |
| vicsi-dashboard/src/lib/scoring/assessmentEventEnvelope.ts | Existing event-storage adapter |
| vicsi-dashboard/src/lib/scoring/verify.ts | Version-specific recomputation/completeness handling |
| vicsi-dashboard/src/app/api/v1/assessment-sessions/route.ts | Invoke essential-event adapter before existing ingest |
| vicsi-dashboard/src/lib/scoring/__tests__/deductionIntegration.test.ts | Focused backend checks |
| docs/validation/assessment-phase4-2026-10-12.md; docs/evidence/assessment-phase4/ | Report and check evidence |

**27. Dashboard work remaining.** Present the three new assessed categories and 0–100 total, separately label retained procedural/legacy ratios as Training Analytics/legacy assessment diagnostics, show chronology and unanswered/undiscovered/review states, and map new HYP-02 response IDs to labels. No dashboard UI was redesigned in this phase. Existing verified overall percentage keeps its storage/display convention.

**28. Expert validation remaining.** **60/25/15 is a research/system design configuration, not an expert-validated academic weighting.** Validate checkpoint rankings, equal relationship weights, the one-unit unsupported-pair deduction, fixed missing-opportunity denominator, 0.25 premature/full-chain-inconclusive credits, zero unsupported-Peter credit and the definition of an insufficient developed chain. Any later numerical changes should receive another append-only scoring version.

**29. Ambiguities.** Existing board links encode the interpretation in their authored semantic label, with no separate per-link interpretation-choice interaction; this phase therefore scores supported semantic completion, not a new explanation text. The bottle is contextual rather than decisive and Peter's link is corroboration; both still have their authored weight 1 pending expert review. Inconclusive sufficiency follows the existing full support tree, which includes completed relationships, not only possession of every physical finding. Eligible unanswered can reflect deferred presentation as well as a student's incomplete reasoning; identifiable technical failures require review, and remaining logs support instructor review. A student with excellent checkpoint/relationship results but an unsupported final accusation can still score up to 85 under the requested 15% conclusion share; no unrequested cap was added.

**30. Recommended Phase 5 cleanup.** Expert/content validation of the configuration; physical-headset end-review readability and deferred checkpoint timing; dashboard analytics/category labeling; an instructor workflow for requires_review/technical omissions; consideration of a dedicated assessment summary table only if event-based reporting needs it; and future supported-case/version authoring automation. Preserve old-version replay. No additional checkpoint, scoring bonus or story/procedure change is proposed as part of this completed phase.

Stopped at Phase 4. No full regression, dashboard redesign, commit, push, deployment or historical migration.
