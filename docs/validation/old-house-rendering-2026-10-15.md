# Old house rendering and alignment repair — 2026-10-15

Repaired the existing `Harong` house in `CSI_Environment` after the user requested returning to the old house and removing black rendering artifacts around its ceiling and kitchen.

## Findings and changes

- Neighboring ceiling tiles selected different sets of lights under the mobile renderer's four additional lights per object. An eight-light comparison removed the abrupt dark rectangular patches. Changed only `m_AdditionalLightsPerObjectLimit` from 4 to 8 in `Assets/Settings/Mobile_RPAsset.asset`. This setting also applies to other scenes using that rendering asset.
- All 13 `Floor1` tiles and 14 `RoofFloor` tiles now follow the same 2.5 m grid anchored to the existing upstairs floor/stair landing. This closes the observed gaps and removes overlaps, including a tile displaced by 25 cm. Heights and staircase geometry remain unchanged.
- Assigned a separate matte plaster material to 28 ceiling/underside renderers, preserving the source material. Normal strength is 0.12 and smoothness 0.08, reducing the original harsh shiny noise.
- Aligned adjoining kitchen cabinet carcasses edge to edge on the main counter, side return and island. The sink follows its cabinet's small position correction. Gave existing doors and drawers 6 mm outward clearance from their carcasses, preserving their hinges and existing open/close components.

Disabling all shadows did not remove the problem; no shadow settings were disabled in the final scene. Neither the original furniture meshes nor their source materials were modified. The existing evidence, body, gameplay components, assessment data and new house remain intact.

## Validation

- 27 tile alignment checks pass, with no tile overlaps detected. Final Editor validation: outside Play, compilationFailed=False, scene dirty=False, additional-light limit=8.
- Compared the final saved scene to a snapshot of the live scene immediately before repair, preserving prior unsaved edits. Zero scene records added or removed. Only local positions and 28 ceiling material references changed: 31 prefab records and 17 direct transforms. All other record contents match the snapshot. Pipeline comparison confirms the single intended 4-to-8 change. See `preservation-check.json` and `scene-only.diff`.
- Entered normal XR-enabled Editor Play mode and rendered kitchen, ceiling, living room and upstairs views using diagnostic cameras at standing positions, 0.1 m near clipping and 4x MSAA. The kitchen and ceiling render cleanly without the earlier rectangular patches. These are live scene renders, not physical headset captures.
- Stopped Play mode and verified the saved scene remains clean. No gameplay regression sweep, build/install, live assessment upload, commit, push or deployment was performed.

The Play session also emitted existing `HudFollowCamera.PanelFits` diagnostics about `Collider.ClosestPoint` on non-convex MeshColliders. This shared UI issue is outside the visual repair; runtime logging is therefore not claimed to be error-free.

Physical Quest stereo appearance and frame time remain untested. The eight-light budget can increase GPU work on surfaces affected by more than four lights, so hardware performance still needs checking.

Evidence: `docs/evidence/old-house-rendering/`. Main preview: `kitchen-play.png`; additional views: `ceiling-play.png`, `living-play.png`, `upstairs-play.png`. Repeatable setup: `Assets/_Project/Editor/OldHouseRenderingSetup.cs`, menu `ViCSI/Scene Setup/Repair Old House Rendering and Alignment`. The setup refuses duplicate application; the original live scene snapshot remains in `Temp/old-house-before-repair.unity` for this session.
