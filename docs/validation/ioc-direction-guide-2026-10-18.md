# IOC-only direction guide — 2026-10-18

Implemented the approved photography-style guidance for the IOC initial assessment only. `IOCAssessmentInteraction.Awake` automatically adds one `IOCAssessmentGuide`; no scene edits, new standing points or evidence relocation are needed. The existing survey floor highlight remains.

The IOC guide reuses the unchanged `PhotographyGuideGraphic`, `PhotographyGuideOverlay` shader and `ScenePhotographyGuide.Project` projection/hysteresis helper. It creates its own read-only HUD at the same 1.6 m display distance as the photography guide, with an arrow for offscreen targets, a location pin for onscreen targets, high-contrast text, and an eligible-target color based on the existing `ObservationAtGaze` result. Displayed evidence names come from the existing centralized `EvidenceDisplay` mapping.

## Student experience

1. After the First Responder handover, the guide points toward the existing survey position: **Investigate here**, distance, and Initial scene survey.
2. Within the existing survey radius, it points toward the authored facing direction: **Survey the scene**. This is an orientation cue; no artificial destination distance is displayed here.
3. After surveying, it recommends an unfinished victim observation, then any needed possible-evidence observations, using **Examine this** and the readable evidence name. Outside the existing observation range it says **Move closer**. Evidence objects are targets to look at, not new standing positions.
4. It skips recorded observations and stops requesting possible evidence once the existing minimum is satisfied. Recommendations follow the authored order within each category; valid out-of-order observations still work normally.
5. It hides before assessment, during dialogue or queued dialogue, checklist/modal UI, on disable, and after assessment completion. The subsequent photography guide keeps its own original behavior and wording.

The guide creates no input actions, raycasters, collision objects, progress records, session events or modal/locomotion ownership. It never observes automatically or completes a phase. Existing A/trigger observation input, gaze/range/line-of-sight/facing validation, observation IDs, prerequisites, quota, scoring and uploads remain unchanged. Label updates are limited to about 6.7 Hz, with immediate updates when the target or instruction changes; arrow projection/readiness follows the current headset view.

## Source scope

- New runtime component: `Assets/_Project/Scripts/SceneTurnover/IOCAssessmentGuide.cs`, plus metadata.
- Existing runtime source: one auto-add line in `IOCAssessmentInteraction.Awake`.
- New focused Play-mode test: `Assets/_Project/Scripts/Testing/IOCAssessmentGuideTest.cs`, plus metadata.
- Existing Editor test runner: one exclusion prevents the IOC-specific test from being automatically advanced past IOC before it starts. Normal gameplay does not use that runner.

The task-start hash inventory confirms 1,337 other pre-existing scoped source/data files are unchanged. All scenes, scenario definitions, evidence positions, original photography sources/shader/graphics, Tool Wheel, menu wheels, shared input/placement code, scoring and upload code remain unchanged. Dashboard and production data were not edited. See `docs/evidence/ioc-direction-guide/preservation.json`.

## Validation

`IOCAssessmentGuideTest` passes **35 / 35** assertions: automatic attachment, pre-handover hiding, requested wording, distance/pin/arrow projection, correct survey direction, no automatic recording/logging, modal/checklist/dialogue suppression and restoration, disable behavior, no hit targets/colliders, native mesh/shader, actual XR A survey recording with jump suppression, readable labels and two-line fit, alternate observation order, occlusion/readiness, range feedback, two-of-three quota, phase transition, original SOCO photography wording, and preserved evidence transforms/states. Exactly four intentional IOC observations are recorded in its fixture.

The initial test report is preserved separately. One fixture used a catalog tooltip rather than spoken dialogue; another used a bloodstain viewpoint occluded by the body. The fixtures were corrected to use a real dialogue and a clear view accepted by the unchanged observation gate. No gameplay observation rules were relaxed.

Accepted regression counts and compiler results are recorded in `docs/evidence/ioc-direction-guide/summary.json` when the run completes. Quest/headset visual comfort still requires manual confirmation. No commit, push, deployment or production-data write was performed.

## Accepted final results

- IOCAssessmentGuideTest: **35 pass / 0 fail**, exit 0.
- ScenarioPhaseFoundationTest: **19 pass / 0 fail**, exit 0; includes original observation input, handover, SOCO transitions and scene restart.
- PhotographyGuideTest: **26 pass / 0 fail**, exit 0; original route, projection, guide visibility and exclusion from actual album capture remain valid.
- Total: **80 pass / 0 fail**, **zero C# compile errors**.
- Final hashes confirm only the two documented existing-source edits, with all other 1,337 scoped pre-existing source/data files unchanged.
- ADB found no connected device; physical headset acceptance remains pending.

During Play mode, find the new canvas at `NPC_FirstResponder/IOC direction guide`, under the existing object that hosts IOCAssessmentInteraction. This is guidance for the IOC assessment phase; it does not add navigation to the later Meet the IOC briefing or alter the First Responder conversation.
