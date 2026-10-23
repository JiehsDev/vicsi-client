# IOC NPC model wiring

Replaced the six primitive body parts under `NPC_IOC_PLACEHOLDER/FacingPivot/Visual` in `CSI_Environment.unity` with a linked instance of the supplied IOC model. The scene root is now `NPC_IOC`, and the editable model is `NPC_IOC/FacingPivot/Visual/IOC_NPC`.

The original `IOC_NPC.glb` requires EXT_meshopt_compression, which the installed Unity glTF importer cannot decode. Kept that source intact and generated `IOC_NPC_Unity.glb` in the same folder with glTF Transform CLI 4.5.1:

```powershell
npx.cmd --yes @gltf-transform/cli copy 'Assets/_Project/Art/Environments/MODELS/NPC/IOC/IOC_NPC.glb' 'Assets/_Project/Art/Environments/MODELS/NPC/IOC/IOC_NPC_Unity.glb'
```

The compatible model imports successfully with its materials, skeleton and authored animation asset. This task adds no animation controller or progression behavior. The model instance has uniform scale `0.009019411` and local position `(0.006823239, 0.96234167, -0.0011511142)` beneath the retained Visual root, fitting the existing 1.82 m body height and grounding the feet at the NPC root. Its front faces the existing FacingPivot forward direction.

NPCPresentation, its serialized references, root placement, collider and label remain unchanged. FacingPivot stays active outside Play mode for editing; the existing runtime phase visibility and briefing turns still control it. No gameplay scripts changed.

Focused checks passed: the compatible asset instantiated and rendered; the preview shows one grounded character; disk comparison confirms only the IOC root name and Visual child link changed among retained scene objects. Twenty-four obsolete primitive object/component blocks were removed and two model-prefab blocks added. Unity completed the requested script compilation with zero C# errors in the final compilation log. No full regression or Play-mode progression test was run. The Editor accepted the externally edited scene through Reload and finished with a clean scene title; the MCP connection subsequently disconnected, preventing a final live selection probe.

Evidence: `docs/evidence/ioc-npc-model/checks.json` and `preview.png`. The supplied material appears dark in the preview; actual scene/headset lighting remains to be checked. No commit, push, build, install or deployment.

Files changed for this task: CSI_Environment scene, new IOC_NPC_Unity.glb and its importer meta, this report/evidence, taskbycodex.md and VR-CSI_SESSION_HANDOFF.md. The user's original IOC_NPC.glb remains unchanged.

## Follow-up: embedded color texture compatibility

The initial model import created the mesh but failed to decode its embedded WebP color texture; the normal map was already PNG. Converted only that color image in IOC_NPC_Unity.glb to PNG using Tools/ConvertGlbWebPToPng.py. Original source, geometry bytes, materials/tint, skeleton, animation and scene wiring remain unchanged; the conversion preserves every decoded image pixel.

Unity now assigns both image_0 (color) and image_1 (normal), each 2048x2048, to mat0mat. The importer has zero report items, and the saved meta no longer contains the WebP failure. A front-lit preview confirms the brown coat, skin and clothing textures display correctly. Checks are recorded in docs/evidence/ioc-npc-model/texture-checks.json; preview.png is updated, and preview-before-texture.png preserves the previous capture. Editor remained outside Play mode, not compiling, with a clean scene. No C# gameplay changes, commit, push or deployment; Quest scene lighting remains unvalidated.

## Follow-up: supplied idle animation connected

The model includes a looping 6.333-second mixamo_com standing idle, but its Animator initially had no controller. Added IOC_Idle.anim and IOC_Idle.controller alongside the model, following the existing FirstResponder_Idle single-state controller pattern. The scene's IOC Animator references this controller, with root motion disabled. NPCPresentation, its phase visibility, briefing turns, root placement and interaction collider are unchanged.

The supplied animation's hip-height origin differs from the static pose. IOC_Idle.anim is a copy with only the hip Y curve shifted down by 93.32916 source-model units, retaining all other animation curves and the loop. This aligns the animated feet with the static model without changing either GLB. Assets/_Project/Editor/IOCNpcIdleSetup.cs provides ViCSI > Scene Setup > Connect IOC Idle to reproduce the assets and wiring from a clean saved CSI scene.

Focused Editor PlayableGraph playback confirmed the saved controller assignment, active motion through a full loop, unchanged model root position/rotation and feet within 0.25 mm of the static floor at the sampled points. idle-pose-2.png shows the aligned animated pose; idle-playback-check.json records results. A temporary probe initially lacked a Playables extension namespace and was corrected before the successful run. Final Unity compilation has zero C# errors; the Editor finishes outside Play mode with a clean scene. This was an isolated Editor playback check, not a full scenario Play-mode or Quest test. Scene diff is limited to the IOC model's controller override. No full regression, gameplay change, commit, push, build/install or deployment.
