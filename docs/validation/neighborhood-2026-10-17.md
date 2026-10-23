# House surroundings — 2026-10-17

Updated the active `Assets/_Project/Scenes/Scenarios/CSI_Environment.unity` scene with a cream picket perimeter fence, wide entrance aligned to the existing stairs, front path, planted borders, shrubs, mailbox and trees. Extended the original modular street by 45 metres at each end using its existing asphalt, curb, sidewalk and streetlamp geometry. Added centre dashes and four varied neighboring exterior shells, with porches, windows, timber trim and planted plots.

All additions are under `Neighborhood_Exterior`. The authoring menu is **ViCSI > Scene Setup > Build Lightweight Neighborhood**. Its editor-only generator is `Assets/_Project/Editor/NeighborhoodSetup.cs`; the player contains saved meshes and built-in components only. Rebuilding replaces only this generated root; it preserves the original investigation scene objects. An initial live-scene snapshot is retained under `docs/evidence/neighborhood/before.unity`.

Performance safeguards:

- 14,372 added triangles, including both alternate house LOD meshes and ground meshes.
- 21 mesh renderers in the hierarchy, including four alternate LOD renderers. Each neighboring plot uses one shared opaque palette material and one renderer per active LOD.
- Four LOD groups switch to simple building/roof shells at distance and then cull. Garden and road geometry are combined by material rather than creating individual renderers for each fence picket, shrub or road section.
- No added runtime scripts, lights, rigidbodies, mesh colliders, shadow caster passes or transparency. Added streetlamps retain their metal frames and omit the source prefab's offset glass geometry.
- Opaque material copies correct the imported road pack's transparent queue for the new asphalt, curbs and iron, preserving original scene materials and textures.
- 17 static box colliders provide ground, road, sidewalk, property and building boundaries. Original house colliders and gameplay objects are preserved.

Validation checks pass the geometry/render budget, saved asset references, absence of costly components and clearance for exterior NPCs, equipment and a 0.6-metre-wide standing player along the entrance route. Screenshots were reviewed from an overview, street, house-front and entrance camera. This is an Editor geometry/component audit, not a headset frame-rate benchmark; actual Quest CPU/GPU frame times remain unmeasured.

The preservation audit compares every original serialized scene record to the initial snapshot, allowing only the scene-root list to gain the neighborhood root. Run `node docs/evidence/neighborhood/audit.mjs`. Machine-readable preservation results, validation output and preview images are retained in `docs/evidence/neighborhood/`.
