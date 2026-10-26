# Research / Competitive build selector

In either Unity project, open **ViCSI > Build Edition...**, select **Research** or **Competitive**, and press **Build [edition] Quest APK...**. Choose where to save the APK. Selection is remembered on this computer.

The selector builds the complete matching source project. The research and competitive projects remain separate so changing selection cannot replace research logic, menus, scenes or study data. Unity Play Mode and the normal Unity Build button still use the open project's edition; use this window to choose between editions.

- Research: `My project (1)`, app ID `ph.edu.parsu.vicsi`, default output `Builds/ViCSI-Research.apk`.
- Competitive: `ViCSI Competitive`, app ID `ph.edu.parsu.vicsi.competitive`, default output `Builds/ViCSI-Competitive.apk`.

The current project builds in its editor and prompts to save modified scenes. The other project builds using its existing QuestBuild entry point in a hidden Unity batch process; the window reports completion and offers **Show build log**. Background builds use saved source files. If the other source project is already open in Unity, build from its own window or close that editor first. **Open [edition] project in Unity** switches the editor project when you want to edit or play that version.

If either folder moves, use **Choose source project folder...**. The selector verifies the edition marker, Android app ID, Unity version and build entry point before starting. Folder locations and the selected tab are editor preferences, not shipped game settings. No runtime edition toggle is included in headset builds.

Validation: script compiled in Unity 6000.5.6f1; both actual source folders accepted; swapped editions and a missing folder rejected. No APK rebuilt or installed as part of adding this selector.

Build implementation follows Unity's [custom build script guidance](https://docs.unity.com/en-us/engine/6000.0/manual/building-and-publishing/build-customize-build-pipeline/build-script-build).
