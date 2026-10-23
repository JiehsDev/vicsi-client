# New house rendering and alignment — 2026-10-15

Repaired `Vicsi_House` in the existing `CSI_Environment` scene, following the old-house rendering repair.

## Changes

- Fitted upper-floor panels `Plane.007`, `.008` and `.009` to their neighboring panels and room boundaries. Removed the excessive lateral enlargement and overlapping edges. The middle panel spans the actual gap between its neighbors.
- Added one lightweight, downward-facing ceiling lining at Y=3.945 m. Six non-overlapping room rectangles follow the interior footprint and leave the stairwell open. This hides the imported slab-edge gaps without changing upstairs floor finishes or the source model. The imported floor meshes remain linked to their originals.
- Corrected the existing flat upstairs ceiling's facing, assigned matte plaster and positioned it at Y=6.93 m beneath the roof underside. Its derived mesh is stored separately from the supplied GLB; the exterior roof remains intact.
- Joined kitchen cabinet bodies edge to edge, grounded them on the Y=1.20 m floor, separated doors/drawers by 6 mm and kept the sink aligned with its supporting unit.
- Grounded the existing furniture, preserving the deliberately tipped chair rotations. Straightened and centered the living-room sofa, coffee table and TV console. The television is upright, faces the sofa and rests on the console.
- Created five local 3D material copies for furniture previously assigned 2D shaders. Preserved their textures, including mapping the TV's legacy `_MainTex` to `_BaseMap`. Shared source materials were not edited.
- Seated light fittings below their ceiling surfaces. Relocated one exactly duplicated upstairs fixture into the previously unlit upper room. Preserved the separate stairwell light's world position. No new lights were added; the existing eight-light renderer budget was retained.

## Verification

Normal XR-enabled Editor Play mode was entered and diagnostic-camera renders were checked for kitchen, kitchen ceiling, living room, living ceiling, upstairs room, upstairs hall, television and exterior. Captures use standing positions, 0.1 m near clipping and 4x MSAA. These are live scene renders rather than physical headset captures.

All 27 focused placement checks pass in Play mode: floor joins and room width, upright/supported television and living-room positions, furniture grounding, distinct active-light positions, ceiling-fitting placement and TV albedo assignment. Final compilation reports no failure. Earlier preview adjustments were corrected before the accepted Play-mode captures.

Saved-scene comparison against a copy of the live scene immediately before repair found 12 changed prefab records and 19 changed transforms, all beneath `Vicsi_House`. Four added serialized records form the single ceiling-lining object. No records were removed and no records outside the new house changed. The old house, gameplay records and assessment/evidence content remain unchanged. See `preservation-check.json` and `scene-only.diff`.

The final saved scene is clean outside Play mode. Physical Quest stereo appearance, collision/navigation and frame time remain untested. The existing shared-UI `Collider.ClosestPoint` diagnostic on non-convex MeshColliders also appeared in this Play session; this repair does not claim runtime logs are error-free.

No build/install, live assessment upload, commit, push or deployment was performed.

## Files

- Editor helper: `Assets/_Project/Editor/NewHouseRenderingSetup.cs`; menu `ViCSI/Scene Setup/Repair New House Rendering and Alignment` refuses duplicate application.
- Materials: `Assets/_Project/Art/Materials/NewHouse/`.
- Derived ceiling meshes: `Assets/_Project/Art/Environments/MODELS/House/Rendering/`.
- Evidence: `docs/evidence/new-house-rendering/`, including `new-house-living.png`, `new-house-kitchen.png`, `new-house-upstairs.png`, `play-inspect.txt` and `play-validation.txt`.
- Original live scene snapshot: `Temp/new-house-before-repair.unity`.
