# IOC/SOCO Phase 1 foundation

Recovery checkpoint: `015eafc350564945e59c390af9b802260a19fff6` on `master`. No new commit, push or deployment.

The existing SceneTurnover facade is the sole procedural owner; FirstResponderTurnover registers it in early Awake in the same CSI_Environment. PreSessionFlowController retains student identity/interaction gating, independently from role progression. SceneCordon and FirstResponderHandoff still react to FR turnover; photography, investigation narrative and RoleSceneLoader now react to SOCO investigation start.

## Progression and integration contract

`IOCTurnover -> IOCAssessment -> IOCSOCODecision -> SOCORequested -> SOCOTurnover -> SOCOInvestigation -> InvestigationComplete`

New sessions start as IOC in IOCTurnover. PlayerRole switches to SOCO at SOCOTurnover. SceneTurnover.TryAdvance(next) accepts only the next legal transition; it rejects skips, repeats and backward transitions. FirstResponderTurnover completes only FR-to-IOC turnover, including its existing informational log entry. OnCompleted means that handover only. OnInvestigationStarted releases the existing SOCO investigation. OnPhaseChanged is the hook for later objectives, procedural interactions and meaningful logging; no phase-event schema was added.

Form1Prepared, Form1Received and Form2Accomplished derive from phase milestones, not independent mutable flags. Completing SOCOTurnover certifies that briefing and Form 2 are procedurally accomplished. These properties represent milestones only: no forms, form-entry UI or observation mechanic were implemented. This follows the [PNP CSI SOP](https://elibrary.judiciary.gov.ph/thebookshelf/showdocs/11/56392) and the stage definitions in the [provided CSI forms](https://www.scribd.com/document/534029902/CSI-Form-1-first-responders-form-1).

ToolInputLock derives its procedural restriction from IsInvestigationBlocked while preserving modal lock owners. PlayerUIGate remains the general/modal UI owner. Tool wheel and central tool equip, procedure map, sketch, board, Recorder entry, hypotheses and conclusion submission are gated at entry boundaries. EvidenceProp proximity and EvidenceStateManager lifecycle/processing mutations reject IOC processing. EvidenceStatus values are unchanged. Photography route/markers and investigation narrative wait for SOCO start.

Existing objectives default to SOCOInvestigation scope. The registered controller's existing turnover objective is the compatibility exception, completing at FR-to-IOC handover. Future objectives can choose AnyPhase or ExactPhase and query IsAvailableInCurrentPhase; phase changes refresh objectives. No IOC checklist or content assets were authored.

InvestigationSeconds begins only at SOCOInvestigation. SessionLogger's overall session clock, timing schema, identity and upload contracts remain unchanged. InvestigationComplete can stop the procedural timer through the same explicit transition hook; no ending resolution rules were changed.

New sessions continue to reload CSI_Environment through the existing completion-screen flow. Registration/domain initialization resets role phase and timer while scene reload resets evidence and other managers together. No partial phase-only reset API is exposed, avoiding old evidence surviving a reset.

## Focused verification

Only ScenarioPhaseFoundationTest was executed, through the existing controlled runner in the currently open Editor using its file-request entry. A separate batch process initially could not acquire the project already owned by that Editor; no process was killed and no locks were deleted. The connected Editor had a clean scene and was outside Play mode before the run.

Final result: **4 PASS / 0 FAIL**:

1. IOC awaiting turnover, Form 1 prepared, initial handover objective, stopped investigation clock.
2. IOC assessment/decision/request/SOCO turnover retain tool/UI/photo/objective/evidence restrictions; illegal phase skip rejected.
3. SOCO investigation releases tools, objectives, photography, procedure UI and evidence discovery; Form 2 accomplished and clock running.
4. Same-scene new-session reload restores IOC turnover, locks, form/timer/photo state and fresh evidence.

Three earlier iterations each passed 3 checks and failed the combined activation check. Diagnostics isolated the test's timing assumption: photography uses scaled game time, while scene initialization/Editor stalls delayed that clock relative to wall time. The test now awaits route activation with a 15-second monotonic real-time deadline rather than fixed sleeps or accumulated frame deltas. Existing game delay and content remain unchanged. Final route activation was about 9 wall-clock seconds after SOCO start (about 2 game-clock seconds).

The runner prepares legacy investigation suites explicitly in SOCO; the new focused suite starts untouched in IOC. FirstResponderTurnoverTest now asserts the IOC handover boundary before explicitly advancing to SOCO for its older investigation checks; that larger suite was not run in this phase. No full regression or hardware test was run.

Unity compilation completed with no current C# errors. Existing nullable-field serialization/obsolete API warnings, local bridge port conflicts and the scene's TunnelingEffect missing MeshFilter assertion were observed; these are outside this phase's architecture changes. Final Editor state: outside Play mode, no pending suite or queued continuation, original XR startup setting restored.

## Phase 2 handoff

Use the existing FR conversation completion and ordered phase API. Implement the detailed FR-to-IOC dialogue/Form 1 receipt next. The scenario currently stops at IOC assessment in normal play because IOC walkthrough/request/turnover interactions are deliberately not built. Do not use CompleteWithoutDialogue as a SOCO shortcut: it now means FR handover only. Manual legacy diagnostics needing forensic access must explicitly enter SOCO, as the controlled runner does.

Keep scene/evidence identity stable. The story, findings, reports, hypotheses, relationships, conclusion/ending rules, ground truth, scoring, backend, session DTOs and physical assets were not changed. No playable FR role, forms UI, NPC animations, reposition/fade presentation or new scene was added.

## Files changed

- `Assets/_Project/Editor/Phase5PlayModeBatchRunner.cs`
- `Assets/_Project/Scripts/CaseFile/EvidenceProp.cs`
- `Assets/_Project/Scripts/CaseFile/EvidenceStateManager.cs`
- `Assets/_Project/Scripts/Deduction/EvidenceBoardController.cs`
- `Assets/_Project/Scripts/Deduction/HypothesisCheckpointManager.cs`
- `Assets/_Project/Scripts/Ending/EndingManager.cs`
- `Assets/_Project/Scripts/Interaction/RecorderTool.cs`
- `Assets/_Project/Scripts/Narrative/NarrativeEventManager.cs`
- `Assets/_Project/Scripts/Objectives/Objective.cs`
- `Assets/_Project/Scripts/Objectives/ObjectiveStateManager.cs`
- `Assets/_Project/Scripts/Requirements/RequirementEnums.cs`
- `Assets/_Project/Scripts/Requirements/RequirementEvaluator.cs`
- `Assets/_Project/Scripts/RoleSystem/PlayerToolRegistry.cs`
- `Assets/_Project/Scripts/RoleSystem/RoleSceneLoader.cs`
- `Assets/_Project/Scripts/RoleSystem/ToolInputLock.cs`
- `Assets/_Project/Scripts/RoleSystem/ToolWheelController.cs`
- `Assets/_Project/Scripts/ScenePhotography/ScenePhotoPoint.cs`
- `Assets/_Project/Scripts/ScenePhotography/ScenePhotographyManager.cs`
- `Assets/_Project/Scripts/SceneTurnover/FirstResponderTurnover.cs`
- `Assets/_Project/Scripts/SceneTurnover/SceneTurnover.cs`
- `Assets/_Project/Scripts/Sketch/CrimeSceneSketchUI.cs`
- `Assets/_Project/Scripts/Testing/FirstResponderTurnoverTest.cs`
- `Assets/_Project/Scripts/UI/EvidenceProcedureUI.cs`
- `taskbycodex.md`
- `Assets/_Project/Scripts/Testing/ScenarioPhaseFoundationTest.cs`
- `Assets/_Project/Scripts/Testing/ScenarioPhaseFoundationTest.cs.meta`
- `docs/IOC_SOCO_PHASE1.md`
