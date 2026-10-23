# IOC observation relocation - 2026-10-13

Moved IOC_Observe_Point in CSI_Environment from (-3.80, 0.00, 9.50), yaw 13.63 degrees, to the open floor beside the TV toward the kitchen: (0.40, 0.00, 10.10). Final rotation is (0, 308.4537, 0) degrees; forward is (-0.783111, 0, 0.621882), toward John and the kitchen/main interior. The entrance candidate was obstructed by a chair and kitchen cabinet; the TV-side candidate has no body sightline obstruction. The point is no longer between the dining table and kitchen counter.

Observe Radius remains 1.5 m, Require Facing remains enabled, Facing Angle remains 45 degrees, and the exact prompt remains A – Observe. The existing serialized reference on NPC_FirstResponder is reused. IOC workflow, input ownership, observation IDs, dialogue route, timing and completion rules are unchanged. No manual Editor wiring or position adjustment is required.

Exact ioc-observation-survey dialogue:

> The scene remains secured and the cordon is intact. John's body appears undisturbed inside the house near the kitchen area.

## Files changed for this task

- Assets/_Project/Scenes/Scenarios/CSI_Environment.unity: observation-point position and facing.
- Assets/Resources/ViCSIDialogueCatalog.asset: scene-survey text only.
- Assets/_Project/Scripts/Testing/ScenarioPhaseFoundationTest.cs: saved-position/sightline/settings/text/restart checks; diagnostic observations; restrict preservation checks to identified evidence, excluding moving tool placeholders with empty IDs. Exercise completion independently of preservation assertions.
- This report, docs/evidence/ioc-observation-relocation artifacts, VR-CSI_SESSION_HANDOFF.md and taskbycodex.md.

Existing unrelated workspace changes were preserved. No commit, push, deployment, build/install or live upload.

## Validation

Final accepted reports: run4/ScenarioPhaseFoundationTest.txt (15 PASS / 0 FAIL) and run1/DialogueCatalogTest.txt (46 PASS / 0 FAIL): 61 PASS / 0 FAIL.

The workflow diagnostic uses the real FR/Form 1 handover, actual simulated right-controller A, observation selection, four observation events, completion/decision/request, automatic arrival, three-line IOC briefing, SOCO entry and a fresh scene reload. It verifies the radius and wrong-facing rejection, actionable A prompt, once-only observation/prompt clearing, unchanged evidence status and exact identified-evidence positions, configured point restored on restart, cleared survey state, and restored opening objective. BODY-A drift is exactly 0.000000 m.

Earlier attempts are retained in run1-run3. The old fixture checked every EvidenceProp, including an unnamed tool-local placeholder that moved 0.088910 m with the rig. That failed its preservation assertion and short-circuited assessment completion, producing downstream failures. Per-observation diagnostics confirmed all four observations and readiness were correct. A temporary 1 mm tolerance did not resolve the placeholder issue and was removed; final code preserves the original exact-position check for all identified evidence and exercises completion independently. No runtime workflow change was needed.

Inspected final-forward-view.png rendered from the authored point at 1.65 m eye height, exactly matching its horizontal facing (70-degree vertical camera FOV). John's head, torso and legs are clearly visible in the lower-middle forward view; the island partly occludes the feet. The kitchen and main interior are visible. Body sight target projects to viewport (0.500041, 0.298742), depth 4.342761 m. The eye-to-body ray has no unrelated collider hits. A standing capsule with radius 0.25 m and segment heights 0.40-1.40 m has no collider intersections. Evidence: final-state.json and final-forward-view.png. The earlier pitched candidate preview is retained as tv-view.png.

Unity 6000.5.6f1 / Android: final scriptCompilationFailed=false, isCompiling=false, outside Play Mode, no pending diagnostic or XR restore. Console has zero C# compiler errors. Existing unrelated bridge-port errors and WallPenetrationTunneling missing-MeshFilter assertions remain outside this task; compilation and all accepted checks pass.

No manual adjustment is required for this point or dialogue. An optional Quest walkthrough can confirm comfort across player heights and headset FOV; stereo hardware rendering was not tested in this Editor validation.
