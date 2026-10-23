# IOC observation point (2026-10-12)

Added the root scene object `IOC_Observe_Point` in `CSI_Environment`. Its normal Transform is at the opening survey stand location and faces the existing survey look direction. It is assigned to the existing `IOCAssessmentInteraction`, remains separate from `IOC_Start_Ground_Circle`, and is never moved by runtime code. The workflow draws an Editor-only radius sphere, center mark, and forward line to support placement.

`IOCAssessmentInteraction` exposes Inspector fields for IOC Observe Point, Observe Radius (1.5 m in the scene), Require Facing (enabled), Facing Angle (45 degrees), and Observe Prompt Text (`A – Observe`). Only the opening SceneSurvey interaction uses this gate. It is available only during IOC assessment, before its observation ID is recorded, and while UI input is unblocked. Distance is measured horizontally from the camera to the point. When facing is required, the camera's horizontal forward direction must be within the configured angle of the point's horizontal forward direction. Existing gaze, range, and obstruction checks for victim/evidence observations remain unchanged. Recording the survey observation clears its prompt/target using the existing observation state and event flow; other evidence observations remain available.

A fresh scene load resets the existing `SceneTurnover` observation state and restores the authored point Transform. The scene object stays active; once the survey observation is recorded, the existing state gate makes that survey interaction unavailable.

Validation: `ScenarioPhaseFoundationTest` passed, including the new checks for the point/radius/facing/text, rejection outside the radius and when facing away, survey recording and prompt clearing, the IOC briefing/handoff, SOCO transition, and fresh-session reset. The Unity Console C# error query returned zero errors after script refresh. Quest headset rendering was not tested.

Editor wiring is complete. Select `NPC_FirstResponder` to edit the `IOCAssessmentInteraction` fields, or select `IOC_Observe_Point` in the hierarchy to move/rotate it. Select `IOC_Start_Ground_Circle` to edit the separate yellow navigation marker.

Play Mode output: `docs/evidence/ioc-observation-point/ScenarioPhaseFoundationTest.txt`.
