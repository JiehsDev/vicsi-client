# Cordon layout correction — 2026-10-20

The saved CSI_Environment cordon now meets both existing side fences. The property fences complete the side and rear enclosure; the controlled entrance remains in the front yard, approximately 3.6 m before the doorway. This implements the correction identified in [the reference review](cordon-reference-review-2026-10-20.md).

Both tape heights (.65 m and .95 m) extend from world X=8 to the left entrance post at X=16.2, and from the right entrance post at X=17.6 to the fence at X=25, all at Z=2.7. Outer posts sit beside the fence junctions. The existing 1.4 m controlled entry is preserved. Security bounds now cover X=8–25, from the tape to the rear fence at Z=19.5. Glove, preparation, comparison, logging and evidence-box stations stay outside the secured area.

`Assets/_Project/Editor/CordonFenceLayoutSetup.cs` provides `Tools > ViCSI > Scene Setup > Connect Cordon To Property Fences`. It discovers physical fence colliders, validates the scene, preserves a before-scene backup, records Undo, updates tape/security bounds and saves the scene. Existing entry authorization uses those updated bounds.

Validation: **94 PASS / 0 FAIL** in accepted runs: ArrivalEntryTest 46 and SceneAccessRegisterTest 48. Checks cover tape/fence junctions, staging, front boundary samples, unauthorized SDK crossings near both sides and the rear, head/capsule return, briefing, gloves, deliberate check-in, visits, persistence and restart. The first arrival run recorded one starting-gaze failure; the retained second run passed with measured responder and entry angles of 17.8 degrees. No gaze threshold or arrival transform was changed to obtain the pass. Original reports are retained.

Native Unity overview and overhead renders were inspected. Editor returned to Edit mode with a clean scene; temporary capture scene/camera were closed. Physical headset traversal remains manual.

Evidence: [overview](../evidence/cordon-layout-fix-2026-10-20/cordon-overview.png), [overhead](../evidence/cordon-layout-fix-2026-10-20/cordon-overhead.png), [arrival report](../evidence/cordon-layout-fix-2026-10-20/ArrivalEntryTest-run2.txt), [register report](../evidence/cordon-layout-fix-2026-10-20/SceneAccessRegisterTest.txt), [summary](../evidence/cordon-layout-fix-2026-10-20/summary.json). Full before-scene backup and numeric layout report are in that evidence folder.

Saved scene SHA256: `0EE4453F0761012F9948E36F4B211C48C441ABDAD1542AE1639E29C61BDD1FAE`.
