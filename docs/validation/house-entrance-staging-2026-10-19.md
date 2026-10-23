# House entrance and front-yard staging ? 2026-10-19

User requested the cordon at the house entrance and tables inside the fence instead of on the sidewalk. The saved CSI_Environment scene now places the existing tape at the foot of the house steps (z=2.7). A short controlled-entry strip joins it to the inner protected footprint at z=3.0. The front-yard staging aisle is accessible before entry; briefing, gloves and confirmed check-in remain required to enter the secured house area.

The supply table, with its glove dispenser, disposal bin and fresh-swab tray, is at (18.2,.03,1.6). The child drying/packaging table is at (19.75,.03,1.6). The comparison desk is at (21.35,.03,1.6), using its own authored scene anchor rather than the evidence box's position. The evidence box is on the left yard at (14.4,.55,1.5). The First Responder stands beside the house entry at (18.15,.03,2.4), and the player starts on the yard walkway at (16.9,-.12,.65), with the original floor-to-origin offset retained. The existing register derives its position beside the entry.

Two named child anchors under SceneCordon_PLACEHOLDER make supply/comparison placement editable in the scene. Runtime legacy fallback placement remains available for scenes without anchors. The shared glove/swab/drying/comparison guide now exits through the house steps and ends in the yard service aisle at z=.75; its old street detour is removed. First seven route waypoints and arrow styling are unchanged.

## Validation

| Suite | Attempt | Pass | Fail | Exit |
|---|---|---:|---:|---:|
| HouseEntranceStagingTest | run2 | 32 | 0 | 0 |
| ArrivalEntryTest | run3 | 29 | 0 | 0 |
| ArrivalPPETest | run3 | 43 | 0 | 0 |
| SceneAccessRegisterTest | run3 | 40 | 0 | 0 |
| GloveChangeStationTest | run3 | 53 | 0 | 0 |
| FreshSwabSupplyTest | run3 | 52 | 0 | 0 |
| SamplePreparationTest | run3 | 46 | 0 | 0 |
| FingerprintComparisonTest | run4 | 50 | 0 | 0 |

Total: **345 PASS / 0 FAIL**, all eight accepted fresh Unity processes exit0. The focused staging suite checks actual runtime physical/visible footprints inside the fence, the accessible aisle versus protected entrance, the authored comparison anchor, and sixteen Meta capsule route segments: eight out of the house, then an approach and return for each of the four stations. All reach their targets with the actual controller radius, step, slope, layer and skin settings. Native yard and entrance renders were inspected, along with supply/preparation views and fingerprint comparison renders.

The arrival/PPE/register suites verify deliberate briefing, no early scene processing, rejected unauthorized crossings, glove pickup/wearing, check-in, observed entry/exit visits and reset. Glove/fresh-swab/drying suites verify the real supply interactions, waypoint behavior, sample preparation and custody. Fingerprint practice covers actual XR/ray controls, unlimited retries, source-specific completion, stored sample identity/case findings/score preservation, closure and reload. Its station separation check now measures the nonintersecting worktops with >.5m nearest-surface clearance from drying, rather than the old arbitrary box-proximity and >2m center-distance assumptions.

Preview: [front yard](../evidence/entrance-staging/run2/front-yard-staging.png), [house entry](../evidence/entrance-staging/run2/house-entrance.png), [comparison practice](../evidence/entrance-staging/run4/comparison-hint.png).

## Earlier attempts

The first authoring attempt rejected standing test positions below the existing Plane (3) surface and exited without saving; positions were corrected before application. The first staging test mistakenly required a solid collider on the deliberately trigger-only evidence box; it now measures the visible shell when no solid exists. ArrivalEntryTest initially received automatic authorization from the shared batch runner, conflicting with its new real check-in assertions; it now starts unregistered, consistently with the other arrival suites. PPE/register fixtures now visit the relocated stations instead of assuming they are within range at spawn. The first comparison regression retained the previous box-distance constraint; only that layout assertion was updated. Failed attempts and logs are retained separately.

## Preservation

The audit covers 383 pre-task files: all existing project C# scripts, Resources assets, the scene and shared route JSON. Exactly ten existing files changed (three runtime presenters/placement scripts, four diagnostics, the batch runner, scene and route); the other 373 are hash-identical. In the scene, exactly six existing serialized blocks changed: rig transform, cordon component/root, responder transform, arrival hotspot transform and evidence-box transform. Four new blocks represent two anchor GameObjects/transforms; no blocks were removed. Every other serialized block, including house geometry and investigation objects, is byte-identical. The dialogue catalog and Resources assets are unchanged.

New accepted scene SHA256: **e441ee08004b03baf5be2ce53598e8542c4318bbb45ff690cd29429d78cadd3d**. This is the intended scene update for the entrance/staging request. Case evidence definitions and internal measurement positions were not edited. Custody/deposit logic is unchanged; the evidence-box script changes only where its separate comparison desk spawns.

Accepted logs contain no C# compilation, shader, null-reference, missing-reference errors or missing dialogue IDs. Existing SDK/licensing/MCP/teardown diagnostics remain. Overwritten legacy PPE/register previews and example JSON were archived under run3/legacy-previews and restored byte-for-byte. The other backed-up legacy files remain unchanged. No live Unity owner or pending test request remains; XR startup flags are enabled and scoped XR status is clean.

Physical Quest reach, stereo comfort and performance remain a headset check. No build, install, upload, commit, push or deployment was performed.

Reproduce: powershell -NoProfile -ExecutionPolicy Bypass -File Tools/RunEntranceStagingChecks.ps1 -Attempt <fresh-directory>. Audit: python Tools/Audit/entrance-staging-results.py. Authoring: HouseEntranceStagingSetup.Apply. Plan: docs/plans/house-entrance-staging.md. Machine-readable results: docs/evidence/entrance-staging/summary.json.
