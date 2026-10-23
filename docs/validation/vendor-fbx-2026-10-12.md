# Vendor FBX connection - 2026-10-12

Connected the supplied `Assets/_Project/Art/Environments/MODELS/NPC/Vendor/Balut_Vendor_With_Basket_Idle.fbx` as a linked model instance at `NPC_Vendor_PLACEHOLDER/FacingPivot/Visual/Balut_Vendor` in CSI_Environment. Replaced the six primitive body children. Existing NPC root name, position, facing pivot, presentation references, prompt, collider and VendorWitnessInteraction remain in place. No dialogue, discovery, phase or scoring source changed.

The supplied clip is copied to Vendor_Idle.anim with looping enabled and assigned through Vendor_Idle.controller. Added an Animator on the model root with root motion disabled. Uniform scale is 0.8984174, fitted to the existing 1.82 m character height; a local Y offset grounds the first idle pose. Model children retain the former visual layer (Ignore Raycast).

The first render exposed missing imported textures. Unity's ModelImporter.ExtractTextures recovered five embedded image files into the adjacent Textures folder and automatically restored imported material references. The FBX itself was not edited. The final diagnostic render shows the supplied character colors and basket.

Validation used isolated Editor preview scenes and the actual AnimatorController playable: at 0, 3, 7, 14 and 28 seconds, normalized playback reached 2.009569 with looping enabled, changing character/basket poses and no root displacement. Sampled body minima stayed at or above the fitted floor and sampled height was approximately 1.82-1.83 m. Baked geometry was checked with BakeMesh(mesh, true) before transforming vertices to world coordinates, avoiding double application of the model scale.

Saved-scene reload confirmed the linked FBX, two renderers, controller, disabled root motion, sole replacement visual child, unchanged root position (-7, 0.031, -0.55), 2.25 m interaction range, live interview reference, Vendor presentation role and existing 1.82 m / 0.28 m capsule. Unity finishes outside Play mode, not compiling, compilationFailed=false and sceneDirty=false, with the vendor selected and framed.

Evidence: `docs/evidence/vendor-fbx/vendor-textured.png`, `idle-validation.json` and `saved-scene-check.json`. The earlier white render is retained as diagnostic evidence. No full regression, APK build, Quest validation, commit, push or deployment was performed. Scene lighting and headset appearance/performance remain untested.
