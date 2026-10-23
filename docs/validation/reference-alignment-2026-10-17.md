# Tape receivers and aligned references - 2026-10-17

Enlarged all eight tape-reference receiving boxes to 0.80 x 0.90 x 0.40 m, centered 0.18 m out from the actual surface. Hook contact is forgiving while recorded distances continue to use the authored fixed planes. Each reference is now aligned with its evidence anchor along the wall/cabinet surface in the floor plan. Nearby items can share a surface patch; selection remains scoped to the currently selected evidence requirement.

Moved REF-BODYA-2 and REF-SPATTER-CAB off the rear cabinet front to the foot-side kitchen wall at world Z 11.632, facing into the kitchen. Stable IDs remain compatible. Aligned the knife's east-wall hook with its actual Z position and the bottle's cabinet-end hook with its actual Z position. Surface coordinates are raycast from the real house colliders by the persisted Synchronize Moved Kitchen Evidence authoring operation.

Updated measurement instructions, expected distances, Sketch reference feet and relationship labels. The new event geometry is new-house-v3; the local sibling dashboard registers its own frozen server-authoritative profile. v1/v2 and historical sessions remain unchanged. No scoring weights or tolerances changed.

| Evidence | Reference A distance | Reference B distance |
| --- | ---: | ---: |
| BODY-A | 2.066830 m | 1.267988 m |
| Knife | 0.789829 m | 1.747010 m |
| Blood pattern | 1.011989 m | 1.758171 m |
| Bottle | 0.332011 m | 0.591272 m |

Validation evidence: docs/evidence/reference-alignment/. CabinetReferenceTest checks each hook's alignment and off-center receiver contact, perpendicular reference pairs, correct reference selection and the new wall. MeasurementToolTest now physically positions the tape off-center inside the enlarged boxes before invoking the real trigger handler. PerEvidenceMeasurementTest verifies actual layout-tagged log events; CrimeSceneSketchTest covers placement, cards, submission and reset.

The initial runner invocation incorrectly treated a comma-separated suite list as one name; rerun with a PowerShell array. One initial measurement test used a stale loaded assembly still expecting new-house-v2; confirmed by inspecting its compiled string before refreshing. The refreshed tape and measurement reruns are authoritative. Earlier attempt reports are retained in play/.

Preservation audit (Tools/ValidateReferenceAlignment.py): no added or removed scene records; all 30 changed records belong to the references, Sketch, or the measurement manager's layout ID. Evidence/IOC/house placements and the previous bag receiver correction are preserved. Backend geometry/profile tests pass 7/7; TypeScript and scoped ESLint pass. Physical Quest acceptance remains pending. Dashboard edits are local source only; no deployment, build/install, live upload, commit or push.

Final accepted verification: 201 Unity checks plus 7 backend checks, zero failures. Exact report paths/counts in summary.json. Final saved CSI scene clean outside Play, no compilation failure or pending runner/XR restore; both XR startup settings enabled.
