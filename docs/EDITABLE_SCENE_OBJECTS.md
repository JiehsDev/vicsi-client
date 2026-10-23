# Editing the scene objects

Open `Assets/_Project/Scenes/Scenarios/CSI_Environment.unity` outside Play mode. The glove/swab table, drying cabinet, logging table with pen and paper, fingerprint comparison desk and evidence-box tray are normal saved objects in the Hierarchy. Move and resize those objects directly; edit their renderer materials or label components in the Inspector.

Use **Tools > ViCSI > Editable Scene Objects** to locate the generated panels and guidance visuals. Each row identifies its owning controller and visual group. **Select** selects its scene objects and frames them in Scene view. **Show UI** exposes a normally hidden panel for editing; gameplay sets the appropriate visibility again when it starts. The window also opens the editable paper-swab package prefab.

The camera's assisted ruler is a saved scene object named **Assisted 30 cm photographic scale - editable**. It starts inactive because the camera controls placement. Select it in the Hierarchy, temporarily enable it to inspect its mesh/print, and edit its material. The camera's `Authored Ruler` reference connects it to the original placement/capture procedure. The camera frame remains the existing authored camera-frame UI.

Paper packages are instantiated from `Assets/Resources/EditablePaperSwabPackage.prefab`. Edit this prefab to change every package. Its source identity and preparation text are filled from the actual sample at runtime. Glove models and the sealed swab pack on the supply table similarly serve as the visible source for their temporary pickup previews. Evidence representations still use the corresponding evidence definition's editable held/deposited prefab.

The supply station's **Worn Glove Color** controls the temporary glove tint on the tracked hands.

`EditableSceneVisuals` components hold the bindings between each script's visual builder and these real objects. Runtime initialization reuses them to reconnect fields and input handlers, preserving authored transforms, materials, colours, fonts and visual layout settings. Existing interaction gates still decide when UI is visible; labels showing live progress, source identity, current evidence, distance, photos or visits still update with gameplay. Head-following panels and guidance markers still move to their computed display pose.

For the supply route, edit **Glove Station Guide > Authored Route** on the supply station. The vendor trail uses its existing route asset. These cues generate their changing arrow geometry from editable route data; their material and display roots are also authored objects.

Use **Materialize missing scene objects** after adding or updating a visual builder. It preserves a before-scene copy and updates the binding groups. Existing visual overrides are retained. Keep essential controller references and binding entries intact; disable decorative renderers or add children when changing presentation. Removing a required bound object requires rebuilding that group.

The setup lives in `Assets/_Project/Editor/EditableSceneVisualsSetup.cs`; the shared runtime binding code is `Assets/_Project/Scripts/UI/EditableSceneVisuals.cs`. Editor-created materials, meshes and generated textures are saved under `Assets/_Project/Art/EditableSceneVisuals/`, rather than existing only in Play mode.
