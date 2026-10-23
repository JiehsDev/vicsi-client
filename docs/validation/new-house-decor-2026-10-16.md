# New house decoration — 2026-10-16

Inspected the user's live relocated kitchen in the rear ground-floor room before decorating. Preserved those unsaved changes in `Temp/new-house-before-decor.unity`, then saved the completed scene.

Added one editable `Vicsi_House/HomeDecor` group containing seven potted plants, a bordered living-room rug, stacked coffee-table books, three framed botanical prints and a small warm ceiling light in the relocated kitchen. Plants use shared curved-leaf geometry and cream/terracotta pots. The living-room prints coordinate with the greenery and sage rug border. Counter and table greenery sit on the current furniture surfaces.

All decoration meshes and materials are local assets in `Assets/_Project/Art/Decor/NewHouse`. Decorations total 6,104 triangles, have no colliders, and add one shadow-free point light. Shared foliage is instanced; no new textures or external downloads were required. Pieces are placed around room edges, on walls and on tabletops, clear of the doors and stair route.

Checked actual Editor camera renders of the living room, dining room and relocated kitchen. Corrected initial foliage generation/upload and leaf-facing issues before accepting the final previews. Final meshes have finite vertices and non-empty bounds. Unity reports compilationFailed=False and the saved scene is clean outside Play mode. No Play-mode suite or hardware test was needed for this visual-only addition; physical Quest appearance and frame time remain untested.

Compared the final scene against the live pre-decoration snapshot: 58 added serialized records, zero removed records, zero changes outside `Vicsi_House`. The only changed existing record is the house prefab instance, solely to attach `HomeDecor`. All existing kitchen/furniture transforms, original objects, materials, gameplay and evidence records remain unchanged.

Evidence: `docs/evidence/new-house-decor/living.png`, `dining.png`, `relocated-kitchen.png`, `final-editor-state.txt`, `preservation-check.json` and `scene-only.diff`.

Repeatable setup: `Assets/_Project/Editor/NewHouseDecorSetup.cs`, menu `ViCSI/Scene Setup/Decorate New House`. It refuses duplicate application; individual decorations can be moved under `HomeDecor`. No build/install, live upload, commit, push or deployment.
