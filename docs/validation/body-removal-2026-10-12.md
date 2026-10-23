# BODY-A documentation and removal - 2026-10-12

## Implementation

Reused EvidenceStateManager, EvidenceRecord, EvidenceDefinition, the Recorder/report panel, EvidenceProcedureMapResolver/UI, EvidenceNoteComposer, SessionLogger, the existing event upload envelope, SOCOCloseout and the shared PlayerUIGate/ToolInputLock/LocomotionSuspender. BodyRemovalInteraction is a per-prop interaction/presentation adapter, following the existing physical NPC action and confirmation-panel patterns; it is not another state manager. Meta SDK OVRScreenFade supplies the camera-attached fade mesh/alpha. A small stereo-compatible URP shader/material replaces its legacy fixed-function fade material during this transition.

BODY-A alone opts in through requiresBodyRemoval. Existing chalk remains required. The practical sequence is initial photo, existing chalk outline, Body Marker A, required position/evidence-distance measurements, examination/Recorder findings, final photo, preparation confirmation and removal. The existing three body report lines (location, position, visible sharp-object wound) must all be confirmed; its minimum report count is now three. No new forensic claim or report answer was authored. There is no body swabbing, ordinary evidence bagging, grab, dragging, ragdoll or lifting animation.

The shared EvidenceStatus enum and RequiredSequence array are unchanged. For this body-only branch, recording at Marked stores bodyFindingsRecorded without pretending a final photo happened. The final photo then advances through PhotographedFinal to the existing Logged documentation terminal. Logged remains queryable by notes, findings, requirements and deduction systems. BodyRemovalStatus is a separate record-owned track: NotPrepared -> PreparedForTransport -> Removed. EvidenceStateManager alone changes that track and logs its events. No custody/inventory entry pretending the cadaver is portable evidence is created.

Preparation requires the real Logged documentation terminal, all three confirmed report IDs, Marker A, complete body fixed-reference measurements and currently available required evidence distances, and an enabled SOCO-processing phase. Existing photo/measurement/scene-photo gates remain active. The physical action appears near BODY-A only when ready, with tools/evidence put away and no other modal/dialogue. Early API requests use the existing guidance channel and a non-scored BodyRemovalBlocked event. Confirmation rechecks prerequisites and uses a fresh A/trigger after opening; B cancels before confirmation.

## Transition and scene result

Confirmation acquires the existing UI, tool and locomotion locks, temporarily suspends other enabled canvases, fades to black over 0.3 seconds, and commits preparation/removal only at full fade. A four-second camera-facing training panel states: "BODY-A was identified, wrapped, placed in a cadaver bag, and prepared for transport for medico-legal examination." The line is also retained in Dialogue History. The transition fades back over 0.3 seconds and restores canvases and owned locks. Disable/cancellation cleanup also restores view/input without committing an unfinished fade.

The existing BODY_100K renderer and authored body collider are disabled. The body GameObject, EvidenceProp, evidence record, original-position measurement anchors, marker and chalk outline remain alive. No suitable cadaver-bag asset was found, so no new bag model was created. Fresh scene/session reload restores the authored body and clears the new record flags.

## Logging, scoring and closeout

Appended BodyDocumentationRecorded, BodyPreparedForRemoval, BodyRemoved and BodyRemovalBlocked to SessionEventType, preserving previous ordinals. Preparation/removal/block payloads identify procedureId=body-removal and scored=false; preparation lists identification tagging, wrapping, cadaver bag and transport preparation. BodyRemoved records medico-legal-examination as its destination and flushes the existing logger. For active 1.7.0 sessions, the three completed milestones also enter the existing essential-event envelope so disabling/capping raw events cannot discard removal analytics. Blocked attempts remain in the normal procedural log, avoiding an unbounded essential-event list. The backend adapter's allowlist was extended only for those three milestones; its event validation, duplicate protection and 32-event bound remain unchanged. Payload version and backend storage/schema remain unchanged; no dashboard presentation or deployment was added.

The deduction scorer, configuration, weights, conclusion logic and assessment input models were not changed by this task. The real transition test compares before/after live deduction inputs and all numerical components. Procedure and blocked-removal events are not grade inputs.

SOCOCloseout now requires Logged AND Removed for the opted-in body. Documentation alone blocks accountability; missing feedback identifies body prerequisites or preparation, and completed review text states preparation for medico-legal transport/removal. Knife, spatter and bottle requirements remain on their existing branches.

## Files

- Assets/_Project/Data/Evidence/BODY-A_Victimsbody.asset
- Assets/_Project/Scenes/Scenarios/CSI_Environment.unity (persisted body action, visual/collider references and camera fade)
- Assets/_Project/Scripts/CaseFile/EvidenceDefinition.cs, EvidenceRecord.cs, EvidenceStateManager.cs, EvidenceNoteComposer.cs
- Assets/_Project/Scripts/Interaction/RecorderTool.cs and new BodyRemovalInteraction.cs (+ meta)
- Assets/_Project/Scripts/UI/EvidenceProcedureMapResolver.cs, EvidenceProcedureUI.cs
- Assets/_Project/Scripts/SceneTurnover/SOCOCloseout.cs
- Assets/_Project/Scripts/Logging/SessionEventType.cs
- Assets/_Project/Scripts/Assessment/AssessmentSessionBuilder.cs
- Assets/_Project/Art/Materials/BodyRemovalFade.mat and BodyRemovalFade.shader (+ metas)
- New BodyRemovalTest.cs (+ meta); updated BiologicalSamplingTest.cs, SOCOCloseoutTest.cs, EvidenceProcedureMapTest.cs and EvidenceProcedurePanelTest.cs
- Tools/ValidateBodyRemovalWire.mjs (actual Unity/backend wire and scoring-isolation checks)
- C:/Users/Roduf Eleu/Desktop/vicsi-dashboard/src/lib/scoring/assessmentEventEnvelope.ts (three procedural event names accepted; no scorer math changes)
- This report, docs/evidence/body-removal/ and session handoff notes.

Existing unrelated working-tree edits, including lobby, assessment and NPC presentation work, were preserved.

## Validation

Final focused result: **307 passed, 0 failed, 0 skipped**. Repeated developmental runs are not counted twice.

| Check | Passed | Evidence |
|---|---:|---|
| Existing Phase5PlayModeBatchRunner: BodyRemovalTest | 30 | BodyRemovalTest-final3.txt |
| Existing runner: BiologicalSamplingTest (real knife/spatter/bottle tools and custody) | 59 | BiologicalSamplingTest-final.txt |
| Existing runner: SOCOCloseoutTest | 48 | SOCOCloseoutTest.txt |
| Existing runner: EvidenceProcedureMapTest | 60 | EvidenceProcedureMapTest.txt |
| Existing runner: EvidenceProcedurePanelTest | 41 | EvidenceProcedurePanelTest-final.txt |
| Existing runner: FinalDeductionAssessmentTest | 36 | FinalDeductionAssessmentTest.txt |
| `node --conditions=react-server --experimental-strip-types --test Tools/ValidateBodyRemovalWire.mjs` | 4 | wire-tests.txt |
| Same Node flags in vicsi-dashboard: `src/lib/scoring/__tests__/deductionIntegration.test.ts` | 29 | backend-deduction-tests.txt |

Evidence is in docs/evidence/body-removal/. The wire checks read the actual JsonUtility export, unity-payload.json: three body milestones ingest with raw uploads disabled/capped; adding them and 100 blocked attempts leaves the complete server deduction result unchanged; historical envelope behavior and unknown-event rejection remain intact. `npx.cmd tsc --noEmit --incremental false` exited 0 with no diagnostics. Targeted tracked-source whitespace checks passed in both repositories.

Final Unity 6000.5.6f1 state (final-editor-state.json): Android target, compilationFailed=false, compiling=false, playing=false, sceneDirty=false; fade shader supported with no shader compiler messages; one EvidenceStateManager and one body action; saved references intact; body visible in fresh Edit state; both XR initialization settings restored to true; no pending runner or XR-restore state. No current C# compile errors. The IOC/vendor/lobby edits were retained. The body is selected and framed for inspection.

The new body test covers early rejection/guidance, real Recorder minimum/confirmation, final photograph gating, ray confirmation and opening-input protection, full fade/locks, renderer removal with retained identity/notes, event persistence and essential export, unchanged deduction inputs/components, closeout, unaffected other evidence states, duplicate protection, fresh reset, interrupted-fade cleanup and an inactive startup fade mesh. The training message render was inspected; body-removal-black.png records it. The fade component's initially unassigned startup renderer is explicitly disabled after SDK initialization so it cannot obscure normal gameplay.

Initial attempts and diagnostic corrections are retained separately. Early interaction tests were out of range after the existing scene-photography fixture repositioned the head; the test now re-approaches BODY-A without relaxing the real range gate. The body-collider reset assertion now compares the authored setting rather than assuming it starts enabled. An inaccessible test-only property reference was corrected to exercise the public Confirm handler; compilation is clean afterward. The first panel-label run used a stale compiled caption; a forced compile and final run pass. The temporary sequential-run helper also falsely matched the phrase "0 failures" as a failure; the underlying final panel report has 41 passes and zero failures. None of these initial attempts are represented as final passing evidence.

No manual Unity wiring is required: the saved CSI scene contains the body interaction and its references, plus a fade component under the actual main camera. Physical Quest stereo/readability/controller acceptance remains pending; no APK was built or installed. No full regression, live upload, database mutation, commit, push or deployment.
