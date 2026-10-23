# ViCSI assessment redesign — Phase 1

Phase 1 separates required procedural training from deduction assessment. Changes are local in the Unity project and the sibling `C:/Users/Roduf Eleu/Desktop/vicsi-dashboard` repository. No commit, push, deployment, historical migration, dashboard redesign, new tutorial popup, or new reasoning checkpoint was performed.

1. **Previous scoring structure.** There is no active `AssessmentManager` assigning procedure points in Unity. `AssessmentSessionBuilder` exports events/state; the dashboard's `verifySession` resolves explicit rules/ground truth, computes categories, and persists the overall score. The offline .NET EvidenceScorer produces diagnostic ratios without an overall grade. The separate in-VR `DeductionScorer` evaluates relationships plus elapsed time.

2. **Procedural contributors found.** Scoring 1.5.0 inherited the following overall weights: critical recall 2, relevant recall 1, precision 1, distractor fall rate 1, procedural compliance 1, scene photography 1, measurement 1, procedure order 1. Recall depends on final EvidenceStatus reaching `Marked`; precision depends on marker events, and non-evidence markers reduce it. Photography measures coverage/sequence/position/angle; measurement measures completion/accuracy/timing. Procedural compliance weights blocked transitions 1, non-evidence markers 1, blocked reclaim 0.5, rejected markers 1, against budget 10. Procedure order deducts 0.1 per eligible unique warning with existing overlap exclusions. Three report completion statements also contributed through documentation accuracy. Swabbing/bagging/sealing/storage/closeout had no individual point award; their state, events and elapsed training time still needed inspection. The board timer previously included all time since scorer Awake.

3. **Numerical contributions removed.** New rules 1.6.0 set those eight training-category weights to zero, excluding them from both numerator and denominator. Pure report completion statements are excluded from documentation assessment's numerator and denominator. Training time is excluded from the board timer. No procedure gate, completion requirement, evidence content or deduction weight was removed.

4. **Analytics preserved.** Existing status, tent, swab, fingerprint/lab flags, custody events, blocked attempts, warnings/prerequisites/context, order, timing, photography, fixed/pair measurement, report selections, closeout events and client procedural counts remain available. Historical procedural ratios and their formulae remain stored under existing category names for training review; zero aggregate weights identify their non-assessed role. Existing event throttling/deduplication remains unchanged; this phase does not add guidance event types or attempt logging where none existed.

5. **ProcedureOutOfOrder.** Still logs existing allowed-but-premature Warn actions once per rule/item, with unchanged fields. Blocked actions continue to log their existing refusal event instead of also logging ProcedureOutOfOrder. The server's existing deduplicated compliance ratio/count remains analytics with zero overall weight. Neither route lowers the new deduction grade.

6. **Progression and gates.** Photography, chalk/marker, measurement, swab-before-print, fingerprint processing, collection, containment, sealing, B storage, custody accountability, final survey, IOC completion report and release retain existing requirements. Previously authored Warn rules remain Warn; previously blocking rules remain blocking. No new bypass is introduced. EvidenceStateManager, ProceduralGateValidator, EvidenceProcedureMapResolver and SOCOCloseout are unchanged.

7. **Recorder/report classification.** `knife-print-on-handle`, `blood-spatter-swab-collection`, and `bottle-standard-procedure` attest only to evidence processing/collection and are non-scored under 1.6.0. All other current authored report choices remain assessed: selectable observations/findings, wound interpretation, stain distribution/mechanism, fingerprint significance/limits, and unsupported accusations. The original report options, prerequisites, correctness keys, UI and logged selections are unchanged. Exclusion is by evidence/line pair in new versioned rules, never by text/name heuristics.

8. **Reasoning preserved.** Existing HYP-01/02/03 reasoning classification and all interpretation/unsupported-claim choices still affect assessment. Server weights remain reasoning 1.5 and documentation 1. No new checkpoint or option was added.

9. **Deduction relationships.** Server relationship weight remains 1.5; the five current relationships, their weights and completion calculation are unchanged. The local board retains 0.5 required links, 0.3 contradiction avoidance, 0.2 time efficiency. Only its timing scope changes: accumulate while the board is open, pause when closed/disabled, resume without resetting. Board content and relationship mutation/submission behavior are unchanged.

10. **Final conclusion.** Server final-conclusion weight remains 2. The existing exact conclusion/ending-pair calculation, requirements, ending resolution and conclusion content are unchanged, including supported Paul, premature Paul, unsupported Peter and inconclusive outcomes.

11. **Overall behavior.** Procedural metrics can rise/fall as training analytics while deduction stays fixed. When all four assessment categories exist, the server calculates `(1.5 * reasoning + documentation + 1.5 * relationships + 2 * conclusion) / 6`, retaining four-decimal rounding and exclusion of unavailable categories. The original relative assessment weights are preserved. Marker-based metrics were excluded because the explicit requirement says placing a marker must not change the grade; they were not retargeted to new reasoning events. Full procedural completion remains necessary even though it earns no grade credit.

12. **Version decision.** ScenarioDefinition requires a scoring version bump for algorithm changes, and the registry requires append-only definitions. Added `csiEnvironment.v1_6.ts` and its registry entry; the active Unity scenario now explicitly exports `scoringRulesVersion: 1.6.0`. Scenario version stays 1.1.0 and ground-truth version stays 1.2.0. Missing-version fallbacks remain 1.0.0.

13. **Historical compatibility.** All existing versioned rules and ground-truth files remain byte-identical to their pre-edit snapshots. Versions 1.0.0–1.5.0 retain their original numerical behavior, report classification and version resolution. No saved result or event was migrated/recomputed. The offline historical diagnostic scorer is unchanged.

14. **Session compatibility.** Upload payloadVersion remains 1. No DTO field, event enum, builder field or schema was changed. Raw report choices and ProcedureOutOfOrder remain in orderedEvents; refused attempts remain in proceduralViolations and proceduralViolationCount. Live evidence, photography and measurement sections remain present. Server-only optional classification configuration is not an upload-schema addition.

15. **Player score text.** Removed `Score: …%` from the two post-case procedural review pages. Their titles now identify Photography/Measurement as Training, retaining completion/sequence/accuracy feedback. No authored +points or premature-action penalty message was found in active procedure feedback. Existing deduction feedback remains.

16. **Focused tests changed.** Added `trainingSeparation.test.ts`; updated scoringVersion/procedureOrder unsupported-version fixtures from 1.6.0 to 1.7.0. Extended ScenePhotographyProcedureTest with payload, wording and timer checks. Updated AssessmentUploadServiceTest's current-version fixture to 1.6.0. Existing BiologicalSamplingTest and SOCOCloseoutTest are reused unchanged.

17. **Checks and results.** See the recorded results below. Only selected scoring/gate/payload/closeout diagnostics were run; the full regression suite was not run. An initial backend version run had 31 pass/1 fail because its unsupported-version fixture named the newly registered 1.6.0; that fixture was corrected and the final run passes. The refresh MCP request timed out waiting 60 seconds for Editor readiness; subsequent actual compilation and Play diagnostics verify the result.

18. **Unity compile.** Compilation succeeded with zero C# errors. Final Editor compilation state and C# error scan are recorded in `docs/evidence/assessment-phase1`. Play diagnostics execute the new code. Existing SDK/runtime/obsolete-API messages are not counted as C# compile errors.

19. **Files changed by Phase 1.**

    Unity:
    - `Assets/_Project/Data/Assessment/Scenario_CSI_Environment.asset`
    - `Assets/_Project/Scripts/CaseFile/ProcedureStepRequirement.cs` (comments/tooltip only)
    - `Assets/_Project/Scripts/Logging/ProcedureLog.cs` (comment only)
    - `Assets/_Project/Scripts/Deduction/CompletionScreenUI.cs`
    - `Assets/_Project/Scripts/Deduction/DeductionScorer.cs`
    - `Assets/_Project/Scripts/Deduction/EvidenceBoardController.cs` (timer hooks only)
    - `Assets/_Project/Scripts/Testing/ScenePhotographyProcedureTest.cs`
    - `Assets/_Project/Scripts/Testing/AssessmentUploadServiceTest.cs`

    Sibling dashboard repository:
    - `src/lib/scoring/data/csiEnvironment.v1_6.ts` (new)
    - `src/lib/scoring/registry.ts` (only new scoring import/entry; preserves existing work)
    - `src/lib/scoring/types.ts`
    - `src/lib/scoring/evidenceScorer.ts`
    - `src/lib/scoring/verify.ts`
    - `src/lib/scoring/__tests__/trainingSeparation.test.ts` (new)
    - `src/lib/scoring/__tests__/scoringVersion.test.ts`
    - `src/lib/scoring/__tests__/procedureOrder.test.ts`

    Documentation/evidence:
    - This report and `docs/evidence/assessment-phase1/` artifacts.

    Pre-existing edits in both repositories were preserved. No scene asset was changed by Phase 1. Temporary MCP/test-request files are operational diagnostics, not gameplay changes.

20. **Phase 2 handoff.** Existing prerequisite guidance is intact; future explanations can use the unchanged block reasons/procedure map. Training percentages retain historical payload/category names for compatibility and are excluded by 1.6.0 weights. Version 1.6.0 must be present in the backend when this client is eventually released; deployment was expressly excluded here. No Phase 2 messages have been added.

21. **Classification ambiguities.** `body-a-position` mixes procedure documentation with the inferential limit “The body alone does not identify the attacker”; retain assessment because it measures a reasoning limit, and leave content splitting for a later phase. `bottle-fingerprint-pending` is retained because it requires withholding an unconfirmed identity inference. Location/trace/dimension statements remain assessed selected findings, distinct from automatically earning points for performing a tool action. Old IDs such as `knife-owner-speculation` now describe accurate interpretive limits, so classification uses inspected authored text rather than the legacy ID's wording. The local board timer was an indirect training penalty and has been limited to board review without changing deduction weights; its efficiency component remains until a later assessment redesign.

## Recorded checks

| Check | Passed | Failed |
| --- | ---: | ---: |
| `trainingSeparation.test.ts` | 22 | 0 |
| `scoringVersion.test.ts` | 10 | 0 |
| `procedureOrder.test.ts` | 16 | 0 |
| `ScenePhotographyProcedureTest` (extended) | 47 | 0 |
| `BiologicalSamplingTest` | 50 | 0 |
| `SOCOCloseoutTest` | 46 | 0 |
| `AssessmentUploadServiceTest` | 33 | 0 |
| **Final focused total** | **224** | **0** |

TypeScript type checking passed (exit 0). Targeted `git diff --check` passed in each repository. There are no skipped tests in the final backend run. Play checks use offline test transports in Windows Unity 6000.5.6f1, not Quest hardware or a live backend.

Exact backend commands, run from `C:/Users/Roduf Eleu/Desktop/vicsi-dashboard`:

```powershell
$env:VICSI_UNITY_PROJECT='C:/Users/Roduf Eleu/My project (1)'
node --conditions=react-server --experimental-strip-types --test src/lib/scoring/__tests__/trainingSeparation.test.ts src/lib/scoring/__tests__/scoringVersion.test.ts src/lib/scoring/__tests__/procedureOrder.test.ts
node node_modules/typescript/bin/tsc --noEmit --allowImportingTsExtensions --module esnext --moduleResolution bundler --target ES2022 --skipLibCheck --esModuleInterop src/lib/scoring/__tests__/trainingSeparation.test.ts src/lib/scoring/__tests__/scoringVersion.test.ts src/lib/scoring/__tests__/procedureOrder.test.ts
git diff --check -- src/lib/scoring/types.ts src/lib/scoring/evidenceScorer.ts src/lib/scoring/verify.ts src/lib/scoring/registry.ts src/lib/scoring/__tests__/scoringVersion.test.ts src/lib/scoring/__tests__/procedureOrder.test.ts
```

Unity: force refresh/compile using `Tools/Phase5Mcp.ps1` (`tools/call`, `refresh_unity`, scope all, compile request), then four separate fresh Play sessions through the existing `Phase5PlayModeBatchRunner` file-request mechanism. Each request specified exactly one suite above and `docs/evidence/assessment-phase1/<suite>.txt` as output. The runner enters CSI_Environment, activates AutomatedTestSandbox, invokes RunAllTests, writes Report and exits Play. After the checks, the Editor was restored to its original Main scene. Final-state inspection uses `EditorUtility.scriptCompilationFailed` and `EditorApplication.isCompiling`; `Logs/Editor.log` was also scanned for `error CS<number>`.

Evidence: `backend-focused-final.txt`, `scoring-typecheck.txt`, the four named Unity reports, `unity-results.json`, `preserved-hashes.json`, `compile-errors.json`, `final-editor-state.json`, and targeted diff-check artifacts in `docs/evidence/assessment-phase1/`. The initial stale-version failure and refresh timeout are retained separately for auditability.
