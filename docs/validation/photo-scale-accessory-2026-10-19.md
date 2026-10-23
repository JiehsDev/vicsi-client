# Camera photographic scale — 2026-10-19

The approved ruler is a camera accessory. With the camera equipped and raised, first take the unscaled evidence photo and finish the item's marking prerequisites. Aim nearby and press right A to place the assisted 30 cm ruler; frame the entire item and ruler and use the existing shutter for the final photo. A removes it afterward. There is no additional Tool Wheel or inventory item. The measurement tape keeps its scene-distance role.

Knife EVD-014 and bloodstain EVD-015 use a flat ruler, and upright bottle EVD-018 uses an upright ruler. Authored offsets follow the current props without moving them. Placement waits for overall scene photography and the existing final-photo prerequisites; it is never automatic. Body and developed-print photography retain their current workflows. This implementation does not complete the wider photography-coverage redesign.

The final shutter rejects missing/wrong-item scales, clipped item or ruler bounds, blocked ruler sightlines, a blank reverse face and insufficient projected ruler length. Checks use the saved image's aspect rather than the headset aspect. Unscaled captures reject a placed ruler intersecting the frame. Successful scaled photos use 960 × 720 at the default 4:3 setting, with the physical ruler visible and viewfinder/transient feedback hidden and immediately restored. Other photos retain 480 × 360. Three append-only local accessory event types add no scoring, upload or dashboard fields.

Right A is guarded by camera ownership, range, raised state, unrelated modal UI and held-button consumption. Explicit removal preserves final-photo progress. Holstering, leaving the source, source movement, disabling and reload clear the accessory; lowering the camera alone preserves it. Runtime attachment requires no saved-scene wiring.

## Evidence and acceptance

Accepted **485 PASS / 0 FAIL** in nine fresh Unity processes, each exit 0. Accepted logs contain zero C# compile errors, shader errors, null-reference exceptions or missing-reference exceptions. Existing SDK teardown diagnostics remain.

| Suite | Pass | Accepted attempt |
| --- | ---: | --- |
| PhotographicScaleTest | 61 | run6 |
| PhotographyShutterOwnershipTest | 14 | run5 |
| ScenePhotographyProcedureTest | 47 | run5 |
| PhotographyGuideTest | 26 | run5 |
| MeasurementProcedureTest | 36 | run5 |
| BiologicalSamplingTest | 59 | run6 |
| ChalkProcedureTest | 39 | run7 |
| SceneTestStartTest | 143 | run7 |
| DialogueCatalogTest | 60 | run7 |

Final check: no Unity process owns the project, no pending file-runner requests, and XR startup remains enabled for Standalone and Android. XR settings have no task changes.

Final automated results are recorded in `docs/evidence/photo-scale/summary.json`. The focused suite exercises actual simulated A input and the real shutter/capture listener for all three items, including standing-height floor photography, different headset/saved aspects, guards, refusal paths, single capture/event behavior, output dimensions, cleanup, reload and no inventory/sample credit or prop movement.

Native Direct3D images in `docs/evidence/photo-scale/run5/` were inspected: three scaled captures and both camera prompt views. Numbered graduations face the camera, remain separate from chalk, and the saved photos exclude prompts. Prompt text fits its dark panel. Run6 also retains original/scaled pairs after correcting bloodstain framing bounds to its wider visible mesh; its final bloodstain image was inspected. A focused regression rejects a view that contains the old smaller placeholder and ruler but clips the visible stain.

`Tools/Audit/photo-scale-audit.py` verifies nine new catalog IDs with every pre-existing entry unchanged and no removals. Scene SHA256 remains `91d5c8c35425cdc32fec5ecb6bbc01ebfb07c368e227d62da5e10fdf12786005`, identical to the feature-start snapshot. The authored scene, photo lifecycle states, score weights and upload schema require no changes for the ruler.

## Diagnostics retained

Earlier attempts remain in run1–run4. Run1 encountered an incorrect test input value type, timed out, then exited with a native crash code; the crash cause was not established. Subsequent focused runs passed. Native images prompted correction of printed-face orientation, chalk overlap, z-fighting, number spacing and prompt contrast before final acceptance.

Regression fixtures now perform the ruler interaction before final photographs. ScenePhotographyProcedureTest had a stale sequential tent-preview expectation; it now verifies no placed marker/state/event progress on refusal. MeasurementProcedureTest and BiologicalSamplingTest had stale bottle-before-final-photo expectations; they now follow the existing authored final-photo-before-measurement order. Measurement checks verify the measurement gates before fingerprint work; sample-name checks retain the sample kind and source without depending on dash punctuation. These fixes do not change production marker, measurement or sample rules. Run5's sampling failure is retained.

Run6's chalk failure is retained. Its fixture used old-house floor/counter heights, sequential tent previews and the old bottle measurement order. It now compares guide points against physical supporting surfaces, checks actual placed-marker count and absent record number after refusal, and prepares body/bottle final photos before the knife requires their distance measurements. Production chalk geometry, marker numbering and measurement rules are unchanged.

Existing Meta/MRUK shutdown and allocation diagnostics may appear in batch logs. Physical Quest controller comfort, stereo readability and Android shader/device performance remain manual acceptance; no headset build/install was performed. No live upload, commit, push or deployment was requested or performed.
