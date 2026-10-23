# Vendor shadow readability - 2026-10-12

Applied the IOC visibility treatment to the existing vendor and basket in CSI_Environment. Three Vendor_Readable materials copy the imported URP Lit materials, preserving their textures, tint and surface settings. Received shadows are disabled and texture-matched emission at 0.25 supplies minimum visible color; the untextured basket handle uses its original tint for the emission color. Original FBX/imported materials remain intact. No lights or global lighting changes were added.

Actual-scene before/after renders from the same diagnostic camera show the previously dark face, blue shirt, skin and basket clearly visible. Saved-scene reload confirms all three overrides retain their emission/shadow keywords, the vendor position is preserved, Vendor_Idle.controller remains assigned with root motion disabled, and the existing 2.25 m interaction range and interview reference remain intact. No dialogue, gameplay, animation or scoring source changed.

Final Editor state: sceneDirty=false, playing=false, compiling=false, compilationFailed=false. The vendor is selected and framed. Evidence: docs/evidence/vendor-lighting/BEFORE.png, AFTER.png and final-check.json. No full regression, build, Quest validation, commit, push or deployment.
