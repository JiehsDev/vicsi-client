# Photography standing-circle visibility - 2026-10-18

The active blue photography circle now hides when the headset enters the existing standing zone. It reappears when the player leaves without successfully completing that photo, including after a rejected wrong-angle shutter. Completed and inactive points remain hidden.

## Implementation

Only runtime change: `Assets/_Project/Scripts/ScenePhotography/ScenePhotoPoint.cs`. LateUpdate refreshes the visual using the unchanged ContainsPlayer boundary, route highlighting, completion state and investigation phase gate. SetActive is only called when visibility actually changes. With no main camera, the existing active-marker behavior is retained.

All seven authored zone visuals are separate child objects, so hiding a circle leaves its ScenePhotoPoint component active to track exits. No scene, prefab, standing position, radius, camera target, angle threshold, sequence, input or interaction changes. The direction/framing guide, NPC/IOC guides, evidence dots, discovery logic, menus/Tool Wheel, scoring, session uploads and dashboard remain unchanged.

## Validation

Added ScenePhotoZoneVisibilityTest. It checks all seven stations: outside visibility, hidden inactive circles, enter without completion, wrong-angle rejection, exit/restoration, height limits, reentry with framing guidance, successful capture and permanently hidden completed circles. It verifies all seven circles have separate child visuals and the entire original photo route completes.

Updated only circle-visibility expectations in ScenarioPhaseFoundationTest and FirstResponderTurnoverTest to respect whether the player is already inside the standing zone; all route, input, phase and state expectations remain.

| Suite | Passed | Failed |
| --- | ---: | ---: |
| FirstResponderTurnoverTest | 44 | 0 |
| PhotographyGuideTest | 26 | 0 |
| PhotographyShutterOwnershipTest | 14 | 0 |
| ScenarioPhaseFoundationTest | 19 | 0 |
| ScenePhotoZoneVisibilityTest | 65 | 0 |

168 checks passed; zero failures; all five Unity processes exited 0. Zero C# compile errors in their logs. Logs: `docs/evidence/evidence-display/photo-zone-visibility-2026-10-18/`.

Task-start hash comparison: 1,390 other existing scoped source/data files unchanged, including all scene/prefab transforms. Three existing source files changed (one runtime class and two test expectations), plus the new focused test and its meta. See `docs/evidence/photo-zone-visibility/preservation.json` and `summary.json`.

No headset was connected for physical VR verification. Check circle entry/exit and a successful photo on Quest as a final visual check. No commit, push, deployment or production-data modification.
