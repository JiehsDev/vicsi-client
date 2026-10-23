# IOC shadow readability - 2026-10-12

The actual CSI_Environment render showed the IOC as a nearly black silhouette. Its imported color texture was present, but the glTF material multiplied it by a 0.604 tint and lighting/shadows concealed its face and coat.

Added IOC_Readable.mat alongside the IOC assets and assigned it as a renderer override on the existing linked model. It uses URP Lit, the original embedded base-color texture with white tint, metallic 0 and smoothness 0.3. Received shadows are disabled for this material, and texture-matched emission at 0.25 provides a modest minimum visible color in dark areas. Shadow casting remains enabled. The source GLBs and embedded materials are intact. The original embedded normal map is not used by the override: assigning it directly to URP Lit produced dark artifacts, so the final material uses mesh normals.

A local point light was trialed but removed; the final scene adds no lights. No global lighting, gameplay, dialogue, phase visibility or animation source was changed. The scene was already dirty when this task started; saving retained the user's current IOC position (2.21, 0.031, 1.60) and other current scene edits.

Before/after renders use the same diagnostic camera and actual scene lighting. The final image shows a readable face, brown coat, white shirt and dark trousers. Saved-scene reload confirmed IOC_Readable, emission/shadow keywords, IOC_Idle.controller, disabled root motion, IOC presentation role and unchanged current root position. Final Editor state: sceneDirty=false, playing=false, compiling=false, compilationFailed=false. Diagnostic cameras were removed.

Evidence: docs/evidence/ioc-lighting/BEFORE.png, AFTER.png and final-check.json. Physical Quest appearance remains untested. No full regression, build, install, commit, push or deployment.
