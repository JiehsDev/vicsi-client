# Supplied replacement knife model — 2026-10-16

Updated the existing `Evidence_KitchenKnife/Visual` mesh and material to the imported assets from `Assets/_Project/Art/Environments/MODELS/NEW/new-knife.glb`. Retained the existing renderer object and 0.15 scale; source bounds match the previous model within approximately 0.1 mm at scene scale. The supplied replacement has 18,640 vertices versus 416,620 in the previous mesh.

Knife root position/rotation, collider, evidence identity, grabbing/collection components, blood trace, fingerprint/contact zone/powdered print, measurement anchor and all procedure references remain unchanged. Source GLB assets were not edited. This is a scene-local prefab override rather than a change to the shared source prefab.

Verified the mesh/material references, rendered and inspected the replacement, and audited the saved scene against a live pre-change snapshot. No scene records were added or removed. The sole changed prefab record contains exactly the new mesh and material overrides. Editor remains clean outside Play with no compilation failure. No runtime code changed, so no additional Play test suite was run.

Evidence: `docs/evidence/new-knife-model/state.json`, `preservation.json`, `scene.diff`, `preview.png` and `before/CSI_Environment.unity`. Preview is an Editor authoring view, including the existing trace/fingerprint marker visuals. No build/install, upload, commit, push or deployment was performed.
