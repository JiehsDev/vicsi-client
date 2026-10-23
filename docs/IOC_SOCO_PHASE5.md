# IOC/SOCO Phase 5 integration and controlled regression

Local validation on 2026-10-09, Unity 6000.5.6f1. No commit, push or deployment. Recovery checkpoint remains 015eafc350564945e59c390af9b802260a19fff6 on master. Earlier Phases 1–4 changes remain uncommitted and preserved.

1. **Overall integration result:** IOC/SOCO WORKFLOW EDITOR PASS / QUEST VALIDATION PENDING. One CSI_Environment and one authoritative SceneTurnover owner. Existing interaction components adapt presentation/input; they do not own another procedural state machine.

2. **Focused workflow:** 7 PASS / 0 FAIL, initially and again against final source. [Final report](evidence/ioc-soco-phase5/focused-final/run1/ScenarioPhaseFoundationTest.txt) exercises the real FR conversation/skipping, Form 1, intentional gaze observations without evidence mutation/movement, decision pointer, rejected/repeated choice, correct request, automatic arrival, Form 2, full three-line briefing/history, original photography/timer activation and same-scene reset. Seven grouped checks; no full investigation added.

3. **Batch A — startup, role, dialogue:** 150 PASS / 0 FAIL in final results.

| Suite / mode | Passed checks | Failed checks | Report |
|---|---:|---:|---|
| FirstResponderTurnoverTest | 44 | 0 | [result](../docs/evidence/ioc-soco-phase5/A/run2/FirstResponderTurnoverTest.txt) |
| DialogueHistoryTest | 24 | 0 | [result](../docs/evidence/ioc-soco-phase5/A/run2/DialogueHistoryTest.txt) |
| StoryContentTest | 32 | 0 | [result](../docs/evidence/ioc-soco-phase5/A/run3/StoryContentTest.txt) |
| ObjectiveStateManagerTest | 13 | 0 | [result](../docs/evidence/ioc-soco-phase5/A/run4/ObjectiveStateManagerTest.txt) |
| DialogueCatalogTest | 37 | 0 | [result](../docs/evidence/ioc-soco-phase5/A/run4/DialogueCatalogTest.txt) |

4. **Batch B — photography/investigation start:** 170 explicit checks PASS / 0 FAIL, plus Greybox's three collectible lifecycle groups PASS.

| Suite / mode | Passed checks | Failed checks | Report |
|---|---:|---:|---|
| ScenePhotographyProcedureTest | 40 | 0 | [result](../docs/evidence/ioc-soco-phase5/B/run1/ScenePhotographyProcedureTest.txt) |
| GreyboxFlowTest | 3 lifecycle groups | 0 | [result](../docs/evidence/ioc-soco-phase5/B/run1/GreyboxFlowTest.txt) |
| ProcedureFoundationTest | 30 | 0 | [result](../docs/evidence/ioc-soco-phase5/B/run1/ProcedureFoundationTest.txt) |
| EvidenceProcedureMapTest | 59 | 0 | [result](../docs/evidence/ioc-soco-phase5/B/run1/EvidenceProcedureMapTest.txt) |
| EvidenceProcedurePanelTest | 41 | 0 | [result](../docs/evidence/ioc-soco-phase5/B/run1/EvidenceProcedurePanelTest.txt) |

5. **Batch C — evidence procedures:** Two logical batches, at most five suite invocations per batch. C1: 140 PASS / 0 FAIL. C2: 141 PASS / 0 FAIL. BODY-A is examined in place; the knife, blood sample and bottle complete their appropriate custody paths.

| Suite / mode | Passed checks | Failed checks | Report |
|---|---:|---:|---|
| BiologicalSamplingTest | 32 | 0 | [result](../docs/evidence/ioc-soco-phase5/C1/run1/BiologicalSamplingTest.txt) |
| ChalkProcedureTest | 39 | 0 | [result](../docs/evidence/ioc-soco-phase5/C1/run1/ChalkProcedureTest.txt) |
| MeasurementProcedureTest:Normal | 36 | 0 | [result](../docs/evidence/ioc-soco-phase5/C1/run2/MeasurementProcedureTest.txt) |
| PerEvidenceMeasurementTest | 33 | 0 | [result](../docs/evidence/ioc-soco-phase5/C1/run2/PerEvidenceMeasurementTest.txt) |

| Suite / mode | Passed checks | Failed checks | Report |
|---|---:|---:|---|
| MeasurementProcedureTest:WrongOrder | 19 | 0 | [result](../docs/evidence/ioc-soco-phase5/C2/run1/MeasurementProcedureTest.txt) |
| CollectionFeedbackTest | 19 | 0 | [result](../docs/evidence/ioc-soco-phase5/C2/run1/CollectionFeedbackTest.txt) |
| EvidenceTraceMarkerTest | 17 | 0 | [result](../docs/evidence/ioc-soco-phase5/C2/run1/EvidenceTraceMarkerTest.txt) |
| EvidenceInventoryTest | 29 | 0 | [result](../docs/evidence/ioc-soco-phase5/C2/run1/EvidenceInventoryTest.txt) |
| SceneRelationalMeasurementTest | 57 | 0 | [result](../docs/evidence/ioc-soco-phase5/C2/run2/SceneRelationalMeasurementTest.txt) |

6. **Batch D — Recorder/findings, narrative and HYP:** 119 PASS / 0 FAIL. BrotherPathTest also covers deduction/conclusion behavior; there are no separate invented HYP suite names.

| Suite / mode | Passed checks | Failed checks | Report |
|---|---:|---:|---|
| EvidenceNotesTest | 33 | 0 | [result](../docs/evidence/ioc-soco-phase5/D/run2/EvidenceNotesTest.txt) |
| RequirementEvaluatorTest | 24 | 0 | [result](../docs/evidence/ioc-soco-phase5/D/run2/RequirementEvaluatorTest.txt) |
| NarrativeEventManagerTest | 10 | 0 | [result](../docs/evidence/ioc-soco-phase5/D/run2/NarrativeEventManagerTest.txt) |
| BrotherPathTest | 52 | 0 | [result](../docs/evidence/ioc-soco-phase5/D/run3/BrotherPathTest.txt) |

7. **Batch E — remaining relationships, conclusions and endings:** 33 PASS / 0 FAIL. Live board/picker coverage is additionally supplied by StoryContentTest and BrotherPathTest.

| Suite / mode | Passed checks | Failed checks | Report |
|---|---:|---:|---|
| RelationshipStateManagerTest | 14 | 0 | [result](../docs/evidence/ioc-soco-phase5/E/run1/RelationshipStateManagerTest.txt) |
| InconclusivePathTest | 10 | 0 | [result](../docs/evidence/ioc-soco-phase5/E/run1/InconclusivePathTest.txt) |
| EndingManagerTest | 9 | 0 | [result](../docs/evidence/ioc-soco-phase5/E/run3/EndingManagerTest.txt) |

8. **Batch F — session/event/upload:** 112 PASS / 0 FAIL. Offline mocks and isolated queues; no live endpoint contacted for validation.

| Suite / mode | Passed checks | Failed checks | Report |
|---|---:|---:|---|
| AssessmentUploadServiceTest | 33 | 0 | [result](../docs/evidence/ioc-soco-phase5/F/run1/AssessmentUploadServiceTest.txt) |
| SessionPersistenceTest | 12 | 0 | [result](../docs/evidence/ioc-soco-phase5/F/run1/SessionPersistenceTest.txt) |
| VrIdentityUploadBridgeTest | 25 | 0 | [result](../docs/evidence/ioc-soco-phase5/F/run1/VrIdentityUploadBridgeTest.txt) |
| OfflineQueueRetentionTest | 34 | 0 | [result](../docs/evidence/ioc-soco-phase5/F/run1/OfflineQueueRetentionTest.txt) |
| PreSessionFlowControllerTest | 8 | 0 | [result](../docs/evidence/ioc-soco-phase5/F/run1/PreSessionFlowControllerTest.txt) |

9. **Totals:** 31 distinct suites; 32 final configurations because MeasurementProcedureTest ran in both Normal and WrongOrder modes. 872 explicit PASS checks / 0 FAIL, plus 3 Greybox lifecycle groups PASS. Summaries and nested suite output are not double-counted. 41 total attempts including failures and reruns. Earlier failed reports remain for review. Machine-readable index: [results.json](evidence/ioc-soco-phase5/results.json).

10. **Intentional stale expectations:** FirstResponderTurnoverTest now expects IOC guidance after FR handover and professor guidance only at SOCO; it rejects repeated source dialogue without banning legitimate secured-scene references from different stages. SceneRelationalMeasurementTest updates four old “Victim's body” labels to the unchanged authored “John's body.” EndingManagerTest replaces legacy conclusionOptions/fixed submissionRequirement assumptions with isolated current Case conclusions and actual board submission. BrotherPathTest respects the current processed-count HYP-03 trigger and chooses uncertainty for HYP-01. No story text was changed to satisfy tests.

11. **Real regression found:** Accepting an ending left SceneTurnover in SOCOInvestigation and its investigation clock running; the terminal phase had no production caller.

12. **Real regression fixed:** EndingManager.FinishSubmission now calls ordered TryAdvance(InvestigationComplete) after showing an accepted ending. StoryContentTest verifies terminal phase and clock freeze. Resolution, conclusion requirements, story, scoring, review and upload schema are unchanged.

13. **Test infrastructure:** A low-RAM batch paused cleanly below 0.25 GB free RAM. The existing controlled runner was retained; after clean Editor shutdown, each remaining suite used one fresh Unity batch process, with graphics enabled and Play mode exited before shutdown. No processes killed or database locks removed. Added an explicit measurement-mode argument and single-suite process option. EvidenceNotesTest gets its required trace marker and a runner-compatible summary marker. BrotherPathTest now yields for actual checkpoint panels before submission. EndingManagerTest searches the inactive menu board. PowerShell execution policy was respected by running the reviewed helper as an inline ScriptBlock. Temporary catalog-override warnings are diagnostic noise; real catalog coverage passes. The pre-existing Meta TunnelingEffect missing-MeshFilter assertion, already recorded in Phase 1, remains a Quest preflight concern.

14. **FR → IOC:** Starts as IOC/IOCTurnover, objective directs the player to receive FR turnover; four preliminary catalog lines complete before IOCAssessment. Normal skip protection is honored. No direct FR → SOCO release.

15. **IOC assessment:** Existing seven-row checklist derives from central phase/form/observation state. Intentional survey, victim and two possible-evidence observations satisfy readiness; explicit completion reaches the decision. No evidence processing or movement results from observations.

16. **SOCO decision:** Continue without SOCO offers corrective guidance and stays in IOCSOCODecision; repeated wrong choice logs once. Request advances only to SOCORequested, with tools/photography/timing still locked.

17. **Form 1:** Prepared before IOC arrival; derived receipt follows completed FR conversation once. Notification and event recorded without editable fields or a fabricated form viewer.

18. **Form 2:** Derived accomplishment begins at acknowledged arrival/SOCOTurnover, before forensic processing. Feedback: “CSI Form 2 completed and submitted to the SOCO Team Leader.” No form-filling UI or “skipped” message.

19. **IOC → SOCO turnover:** Automatic local arrival restores entrance position while preserving settled rig height. All three IOC briefing lines display/skip and enter history once. IOC role and forensic gates remain until the briefing finishes; then ordered TryAdvance enters SOCOInvestigation. Cordon stays established and controlled access remains open.

20. **Role boundary:** IOC can receive turnover, walk, intentionally survey/observe and make the decision. EvidenceProp discovery and EvidenceStateManager procedure entry points remain gated, as do tool wheel/equipping, physical evidence grab prerequisites, photography, procedure UI, Recorder, sketch, HYP and board. All four evidence records remain NotFound through the tested procedural route. SOCO unlocks only at phase 5; downstream investigation prerequisites continue to apply.

21. **Timer:** InvestigationSeconds stays zero throughout all IOC/request/turnover phases, starts at SOCOInvestigation and now freezes at InvestigationComplete. Overall SessionLogger session time remains separate and unchanged; no new IOC timer or scoring metric.

22. **Existing SOCO regression:** Photography route/order, BODY-A documentation in place, measurements, knife blood trace/markers, swab containment/sealing, fingerprint kit/station paths, collection/grab feedback and inventory all pass. Existing internals and evidence positions/data remain unchanged.

23. **The Last Drink:** HYP-01 uncertainty, HYP-02 contact versus attack and HYP-03 combined physical evidence confirmed. Supported Paul requires the existing sharp wound, knife consistency, Paul print, reconstruction and relationships. Premature Paul and unsupported Peter resolve as authored. Inconclusive remains available for the incomplete case, is hidden after full support, and stale selection is rejected. Evidence/HYP assets, active truth 1.2.0 and archived truth 1.1.0 match the checkpoint.

24. **Session/event/upload:** Meaningful procedural events log through explicit actions/ordered transitions once; reinteraction/duplicate observations are rejected. Initialization does not log procedural completion and no per-frame/gaze log exists. The added assessment check preserves all twelve procedural event types in ordered raw export with scenario 1.1.0 / scoring 1.5.0 / truth 1.2.0, selected conclusion and ending, timing and unchanged assessment sections. Existing evidence/procedure, reports, HYP, relationships, identity/ownership and test-session fields remain intact. Five IOC objectives are excluded from existing objective scoring. Mock retry, persistence, identity/ownership and retention tests pass. No schema or backend change.

25. **Active stale-content sweep:** No active wife culprit/affair/gloves/retired phone storyline, false brother solution title, old wife ending, direct FR → SOCO handover, IOC mislabeling as SOCO, Form 4 request, duplicated source briefing or empty catalog entries found. Real catalog resolves all 188 referenced IDs and has no duplicate/empty entries. The legitimate ordinary-accidental-fall wound description is retained. Three historical theory-accidental-fall option IDs are internal compatibility keys with current visible text; RecorderDictaphone is the recorder's object name, not retired phone evidence. Archives untouched.

26. **Unity compile:** Zero current C# errors; final Editor error console contains zero entries. Fresh suite logs also contain no error CS compiler diagnostics. Existing package/serialization warnings and the previously documented runtime SDK assertion are distinguished from C# compilation.

27. **Final Editor state:** Interactive Editor reopened, idle outside Play mode in CSI_Environment. Scene dirty=false; no pending suite, nextSuite empty, exitRequested=false, xrRestorePending=false. Desktop XR InitManagerOnStart restored=true. No runtime scene edits saved.

28. **Quest:** SDK ADB available; final adb devices -l lists no device. QUEST HARDWARE VALIDATION: PENDING.

29. **Later hardware checklist:** IOC spawn/role; FR interaction; dialogue readability; Form 1 notification; IOC checklist ergonomics; observation-ray interaction; SOCO decision panel; Form 2 notification; repositioning/floor height; IOC briefing; role label change; first photography marker; tool wheel; evidence procedures; Recorder; HYP; deduction board; ending UI; session completion/upload; performance/frame stability. Review the existing wall-tunneling MeshFilter assertion during hardware preflight. No headset/build or live upload acceptance is claimed.

30. **Deferred presentation:** Visible IOC NPC/model; FR/IOC animation; walking/arrival; radio; clipboard/form handover; interactive forms; cinematic fades; environmental animation polish. Existing phase/actor hooks preserved; none implemented.

31. **Phase 5 files changed:**

- Assets/_Project/Editor/Phase5PlayModeBatchRunner.cs
- Assets/_Project/Scripts/Ending/EndingManager.cs
- Assets/_Project/Scripts/Testing/ScenarioPhaseFoundationTest.cs
- Assets/_Project/Scripts/Testing/FirstResponderTurnoverTest.cs
- Assets/_Project/Scripts/Testing/StoryContentTest.cs
- Assets/_Project/Scripts/Testing/AssessmentUploadServiceTest.cs
- Assets/_Project/Scripts/Testing/EvidenceNotesTest.cs
- Assets/_Project/Scripts/Testing/SceneRelationalMeasurementTest.cs
- Assets/_Project/Scripts/Testing/BrotherPathTest.cs
- Assets/_Project/Scripts/Testing/EndingManagerTest.cs
- Tools/RunIocSocoBatch.ps1 (local orchestration helper)
- docs/IOC_SOCO_PHASE5.md
- docs/evidence/ioc-soco-phase5/ (reports, memory records, local logs, result index)
- taskbycodex.md

Earlier Phases 1–4 uncommitted changes remain in the working tree. No Phase 5 scene/data/story/scoring/backend changes.

32. **Remaining blockers:** No failing Editor regression suite. Quest validation/release acceptance remains pending, including physical controller interaction, spatial ergonomics, performance and actual completion/upload. The existing SDK wall-tunneling assertion needs hardware preflight review. Live server connectivity was not tested by these offline suites.

**IOC/SOCO WORKFLOW EDITOR PASS / QUEST VALIDATION PENDING**

