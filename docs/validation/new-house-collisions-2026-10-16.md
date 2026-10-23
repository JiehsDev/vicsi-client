# New house collisions — 2026-10-16

Added static, non-trigger collisions to `Vicsi_House` in the saved `CSI_Environment` scene: floors on both storeys, walls, doorframes, doors, windows, ceilings, entrance stairs, indoor stairs and furniture that lacked collision. Mesh colliders follow the actual geometry, preserving door openings and the stairwell. Existing cabinet, sofa and coffee-table colliders remain in place. Small decorations, curtains and light fittings remain passable.

Added 96 mesh colliders to existing objects and two invisible, thin collision ramps under `Vicsi_House/StairCollisionRamps`. Ramps follow the indoor stair nosings for smooth ascent/descent. The player's existing 0.25 m capsule radius, 0.30 m maximum step, 50-degree slope limit and collision mask are unchanged. House colliders are on layers included by that mask.

Initial checks passed at 1.4 m and 1.7 m player heights but caught a tread snag at 2.0 m. Ramps resolved ascent. Descent then exposed insufficient clearance under the upstairs floor edge. Extended the existing stairwell opening by 0.633 m along the lower flight, cutting the visible upstairs hall floor (`Plane.012`) and ceiling lining together. These use local mesh copies; the source GLB is untouched. The upper landing and return flight remain connected. No furniture or existing object transforms were moved.

Final verification: **42 PASS / 0 FAIL**, repeated after reopening the saved scene. An isolated capsule uses the project's actual `Oculus.Interaction.Locomotion.CharacterController`, copying the player's radius, skin width, step/slope limits and layer mask. Checks cover indoor ascent through both flights and their turning landing, descent, exterior entrance steps in both directions, player heights 1.4/1.7/2.0 m, ground/upper-storey floor support, wall blocking and architectural collider/mask configuration. The live player is never moved. These are Editor physics checks, not a headset/controller or full Play-mode acceptance run.

Final rendered stair and landing previews were inspected. Unity reports `compilationFailed=False`, `playing=False`, `dirty=False`. The house contains 109 colliders in total, including its 11 existing colliders. Compared against the live pre-change snapshot: 199 added serialized records, zero removed records, zero changes outside `Vicsi_House`. Original transform/material overrides are preserved; changed existing records contain collider additions, the ramp parent attachment and the two local stair-clearance mesh assignments. The source GLB SHA-256 remains `9abb37fec719fe205017f7c9ef0b8507ac5732f9967afb6fdda3e4ffdb03f2ab`.

Repeatable setup and checks: `Assets/_Project/Editor/NewHouseCollisionSetup.cs`; menu **ViCSI / Scene Setup / Add New House Collisions** and **Validate New House Collisions**. No manual scene wiring is needed.

Evidence: `docs/evidence/new-house-collisions/validation.txt`, `initial-validation.txt`, `ramp-validation-before-headroom.txt`, `added-colliders.txt`, `stairs.png`, `landing.png`, `final-editor-state.txt`, `preservation-check.json`, `house-prefab-ids.txt` and `scene-only.diff`.

Physical Quest navigation, stereo comfort and performance remain untested. No build/install, live upload, commit, push or deployment.
