# ViCSI in-game settings - 2026-10-13

## Implementation

Settings is a functional entry in the existing UtilityMenuController: hold left X, aim the right stick at Settings, then release X. This preserves the existing camera-viewfinder/X arbitration and tool-wheel/Y binding. No additional menu button was assigned. The wheel retains all authored entries and callbacks; RegisterSettings adds one runtime listener recognized as functional by the wheel.

SettingsPauseMenu is authored on NPC_FirstResponder in CSI_Environment, with a scene-load fallback matching existing runtime UI bootstraps. It constructs SettingsPauseCanvas using ForensicUIStyle and WorldPanelPointer. The panel measures 620 x 580 units at 0.001 scale (0.62 x 0.58 m), preferred distance 1.05 m, local Y offset -0.12 m, minimum distance 0.65 m and wall clearance 0.12 m. HudFollowCamera shared layout handles geometry, priority and companion/deferred panels. Priority 650 exceeds dialogue 400 and normal information 300. Title, height heading and confirmation warnings are Inspector editable.

Right stick selects, A confirms, controller ray/trigger also selects. B cancels confirmation or closes Settings. The safe initial selection is Close; destructive confirmation defaults to Cancel. PlayerUIGate owns A, ToolInputLock blocks tools/evidence/grabs and guards held B/grip/trigger release, and LocomotionSuspender applies the existing modal walking/turning/jump policy. Opening/page-transition frame guards, held-A protection and the pointer's release guard require a fresh activation. The menu does not change Time.timeScale or pause real-time session/investigation clocks; it follows the current modal policy.

## Height

Extended the existing PlayerHeightOffset rather than scaling or replacing the rig. A comfort offset translates OVRCameraRig.TrackingSpace in local Y, moving head and both controller anchors together. No rig/world scale, controller-local pose, grab-distance setting or physics collider is scaled. Meta FirstPersonLocomotor continues its existing automatic head/capsule-height and collision handling. Upward changes additionally check a 0.12 m head-clearance sphere sweep against solid geometry.

Inspector defaults: minimum 1.40 m, maximum 2.00 m, step 0.10 m (updated per user request), fallback/default calibration 1.70 m. The previous scene-authored -0.40 m fixed offset is superseded by default floor calibration (scene heightOffset is now 0). On the first reliable tracked headset pose, the physical height becomes the baseline. Calibration is captured once, so normal crouching/head motion is preserved rather than continuously corrected. The displayed preferred height is the configured height at this baseline, not a live measure of every head movement.

PlayerPrefs key ViCSI.PlayerHeight.Target.v1 stores the chosen target locally. Each scene reuses that target over its tracked calibration. Height bootstrap also supplies the same owner in Main. Reset Height deletes the preference and sets comfort offset to zero, restoring headset calibration (or 1.70 m fallback in untracked diagnostics). Physical calibration is respected even when outside the user-adjustment range. Height is not part of session logging, grading, DTOs or Supabase uploads.

## Navigation and incomplete sessions

Restart confirmation says: Restart Scenario? / Your current investigation progress will be reset. Restart uses the existing full SceneManager.LoadSceneAsync scenario-reload contract documented by SceneTurnover and exercised by the existing scenario diagnostics. It does not implement a separate per-manager reset or retain progress. A new SessionLogger GUID and fresh scene-owned managers reset IOC/observation, vendor, body removal, evidence procedures/inventory, reasoning, ending and closeout. Height remains a local preference.

Lobby confirmation says: Return to Main Lobby? / Your current investigation session will end. Settings calls CompletionScreenUI.ReturnToLobbyFromSettings. Its common LoadMainLobby path uses the existing serialized menuSceneName (Main), also used by normal completion; Settings contains no separate hardcoded lobby destination. It clears StudentIdentificationHandoff. The menu releases its modal holds and the existing held-tool coordinator holsters equipped tools; scene teardown disables/destroys old physical interactions and transient UI.

Existing SessionLogger.OnDestroy calls EndSession, writes SessionEnded and flushes the local log. EndingManager queues an assessment upload only through normal completed scene-release flow. Restart/early lobby return do not call ending resolution, final score or QueueSessionForUpload. Diagnostic queue assertions verify no incomplete attempt is queued. Existing prior completed queue entries are not modified. A restarted scenario follows the existing pre-session identification policy; no new authentication policy was added. Lobby-start validation used the existing BeginIdentification fallback with its identity panel temporarily absent in the diagnostic, avoiding live identity/server work.

## Validation

Final SettingsPauseMenuTest (verified): 35 PASS / 0 FAIL, through actual synthetic XR X/A/B input, real shared-panel geometry, height transforms and real scenario/Main/scenario navigation. It covers held opening A, fresh A, modal/tool/jump suppression, +/- and clamps, reset, invalid input, head/hand translation and scale invariants, low-ceiling refusal, locomotor enabled/finite state after closing, actual tool re-equipment after height changes, cancel confirmations, fresh session IDs, local height persistence and reset-state/empty-upload-queue assertions. Added no-readable-slot/B-escape and disable/re-enable cleanup checks. Modal ownership is released even if the panel has already been disabled or destroyed. Preferences are restored in finally; upload transport, owner store and queue are isolated through AutomatedTestSandbox, reapplied before each new scene's Start.

The distant height-transform fixture temporarily disables locomotor catch-up, then restores it before the locomotion check. These are Editor transform/control-path assertions; tracked hands, real walking and headset rendering remain hardware acceptance items.

Standalone scorer tests: 6 PASS / 0 FAIL. Scoring definitions, weights, rules, hypothesis correctness, backend and upload schema were not changed by this task.

Accepted results: 349 Unity checks plus 6 standalone scorer checks = 355 PASS / 0 FAIL.

| Suite | Pass | Report folder |
|---|---:|---|
| SettingsPauseMenuTest | 35 | verified |
| NarrativeRoleFlowTest | 41 | regression |
| ScenarioPhaseFoundationTest | 19 | regression |
| ReasoningCheckpointTest | 60 | regression |
| EvidenceProcedurePanelTest | 41 | regression |
| SOCOCloseoutTest | 48 | verified |
| UIComfortInputTest | 55 | verified |
| GrabReachTest | 9 | regression |
| BodyRemovalTest | 41 | regression |

Exact paths/counts: docs/evidence/settings-pause/summary.json. menu-preview.png was inspected: height controls and all menu rows render clearly in the compact tablet layout. This is a diagnostic camera view, not a Quest capture.

Earlier attempts remain in run1, regression and final. The two failed closeout attempts had complete custody and no foreign modal owner, but queued closeout training dialogue prevented inventory review. Raising the three-second diagnostic deadline to 30 seconds alone did not fix it: the untracked Editor camera was at floor level and wall-safe geometry could withhold the dialogue. The final diagnostic supplies a standing-height camera, retaining real dialogue skip protection, phase gates and all closeout assertions; all 48 pass. No runtime closeout policy changed.

The first UIComfortInputTest failed one deferred-notification timer assertion. Its geometry fixture now disables unsolicited photography presentation, cancels delayed ActivateFirstPoint and records text/timer/defer diagnostics. The accepted final report confirms unchanged text, a preserved timer and all 55 passing checks. Notification runtime logic was not changed.

Final Editor verification: clean CSI_Environment outside Play, not compiling, scriptCompilationFailed=false, zero C# error entries, no pending runner/request or XR restore, modal lock count 0, no tool UI locks and LocomotionSuspender count 0. Standalone and Android XR initialization are enabled/restored. Settings is authored; height Inspector values match the documented defaults; the retired IOC_Start_Ground_Circle renderer remains disabled. Existing Unity-generated YAML empty-field trailing spaces were retained rather than rewriting unrelated scene content.

Changed runtime/source files: SettingsPauseMenu.cs (+meta), PlayerHeightOffset.cs, UtilityMenuController.cs, CompletionScreenUI.cs and CSI_Environment.unity. Diagnostics: SettingsPauseMenuTest.cs (+meta), SOCOCloseoutTest.cs and UIComfortInputTest.cs. Report, evidence manifest and existing handoff/task notes were updated. Existing unrelated workspace changes were preserved.

## Wiring and hardware acceptance

No manual Editor wiring is required. Inspector customization is available on the authored SettingsPauseMenu and existing PlayerHeightOffset. Main receives its height owner automatically. Changes remain local; no commit, push, build/install or deployment.

Quest 3S walkthrough should confirm menu access with a held camera, text/ray targeting and stick navigation, wall/close-wall/companion placement, held/released A/B/trigger/grip timing, seated/standing calibration and recentering, head/hand alignment, crouching, capsule/ceiling behavior and resumed physical grabs/locomotion. Restart and lobby-to-next-session identity flow should also be checked on hardware.

## Height-step follow-up

Updated the default and saved scene heightStep to 0.10 m per user request. Both +/- controls use this same step, retaining the existing bounds, calibration and persistence. Updated diagnostic assertions verify +0.10 m, -0.10 m and one fresh-A increment. Focused rerun: docs/evidence/settings-pause/step-010/SettingsPauseMenuTest.txt, 35 PASS / 0 FAIL. The full regression results above describe the original implementation run; they were not repeated for this step-only adjustment.
