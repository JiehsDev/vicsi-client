# Exterior photography standing-room correction — 2026-10-18

## Exact scene change

`Assets/_Project/Scenes/Scenarios/CSI_Environment.unity`, Transform fileID `1822672826`, hierarchy `ScenePhotography/ScenePhotoPoint_10_scene-exterior-establishing`:

| Property | Before | After |
|---|---|---|
| Local/world position | `(16.9, 0, -0.8)` | `(16.9, 0.03, -0.8)` |

Only the station parent's `m_LocalPosition.y` changed. Its rotation and scale are unchanged. Its existing target and zone visual inherit this 3 cm lift; their local transforms and references are unchanged. Target world position changes from `(16.8, 3.2, 7.5)` to `(16.8, 3.23, 7.5)`, preserving the relative viewing direction (0 degree difference).

## Inspection and clearance

The east-road sidewalk collider has center `(37.727, -0.06, -0.59)`, extents `(22.5, 0.09, 0.5)`, and top surface Y `0.03`. The station was authored at Y `0`, below that top. No sidewalk, house, fence, wall, prop, evidence, or other environment geometry was changed.

The actual Meta locomotion controller configuration supplies radius `0.25 m`, max step `0.3 m`, and skin width `0.005 m`; the audit uses a `1.7 m` standing capsule. The existing interaction standing radius is `0.75 m`, maximum head height `2.5 m`, and angle tolerance `30 degrees`, all unchanged.

The corrected station passes the capsule overlap query with no obstacles, all 16 footprint support rays, target visibility, `ContainsPlayer`, and actual controller movement from arrival. Grounded feet settle at approximately Y `0.0325`, above the sidewalk. Nearby colliders were inspected, including sidewalk, road, lawn, existing pavement and first responder. No wall/fence/prop intersection is found. This is automated Editor/play-mode validation, not a physical headset acceptance test.

## Validation artifacts

- `docs/evidence/exterior-photo-clearance/inspection.json`: original overlap, neighbors, controller settings, and candidate inspection.
- `docs/evidence/exterior-photo-clearance/after.json`: valid supported standing position, no capsule overlaps, visible target, interaction zone, successful arrival navigation, clean scene, no compilation failure.
- `docs/evidence/final-consolidation/layout-validation.txt`: all 62 environment checks pass, including all photography stations and arrival-to-kitchen traversal.
- `docs/evidence/final-consolidation/scene-validation.json`: both enabled build scenes have zero active/inactive missing scripts and zero broken serialized references.
- `docs/evidence/exterior-photo-clearance/preservation.json`: byte-level scene-record comparison and hashes of pre-existing source/data files.
- Focused suites: `PhotographyGuideTest` 26 pass / 0 fail; `PhotographyShutterOwnershipTest` 14 pass / 0 fail. Exterior shutter completion, angle rejection, route ownership, and separate evidence photography pass.
- `ScenePhotographyProcedureTest`: 44 pass / 3 fail. All seven photo points and the documentation objective complete; failures concern pre-route marker-number expectations, outside this station-only fix. Baseline control result is recorded in the task summary.

The added `Assets/_Project/Editor/ExteriorPhotoStationAudit.cs` is verification-only: it opens the scene, creates and destroys an isolated temporary probe, and writes reports without saving the scene. No gameplay source changes were made. No commits, pushes, deployments, dashboard changes, or production-data writes were performed.

## Final preservation and control result

The original-height control produced exactly the same 44 passing and 3 failing procedure assertions. Those marker-number assertion failures therefore predate this station fix; marker code/tests were left unchanged. The exact verified fixed scene bytes were restored after the control. All 739 other pre-existing source/data files in the task-start hash inventory remain unchanged. The scene has identical record IDs with only Transform 1822672826 position Y changed; no unrelated transforms/settings changed. Unity reports zero C# compilation errors. Detailed counts, failures and final scene hash are in `docs/evidence/exterior-photo-clearance/summary.json`.
