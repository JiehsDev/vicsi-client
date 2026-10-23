# Display liquor bottles and fifth photo station ? 2026-10-16

Added two upright liquor bottles on Table19, the dining table beside the living room. Both reuse the existing imported GIN_30K mesh/materials as render-only objects under Vicsi_House/DisplayLiquorBottles. Their bases meet the tabletop at Y1.995332. They have no colliders, evidence scripts, grab components or collection behavior.

Moved the fifth station to (14.1, 1.2, 8.75), with its target centered over the table at (13.48, 2.145332, 6.99). Its floor marker follows the station. Updated the label and dialogue to ask for the table and both bottles. Existing photography guidance reads these transforms automatically. Stable station/instruction IDs, order 50, requirements, angle tolerance and scoring settings remain unchanged.

Validation: 62 layout checks and 47 ScenePhotographyProcedureTest checks PASS, zero failures. Verified standing clearance, floor support, unobstructed target and full route completion. Preview photo-point-5.png visually inspected. Scene audit shows ten added records for the two visual bottles/group, four changed records for the group attachment and fifth photo station/target, no deleted records. Existing evidence, measurements, body guide and other stations are unchanged. Catalogue diff contains only the fifth photo instruction/notes.

Saved scene clean outside Play. No compilation failure or pending diagnostics; Standalone/Android XR startup restored. Quest headset framing remains a device check. No build/install, upload, commit or push.

Evidence: docs/evidence/display-liquor-photography/ (preview, before snapshots, scene audit, dialogue diff, layout report, Play report, final editor state and summary).
