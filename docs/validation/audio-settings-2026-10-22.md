# In-game ambience and SFX volume

Added **Ambience Volume** and **SFX Volume** to the existing in-game Settings menu, alongside player height and walking speed. Each has minus/plus controls in 10% steps from 0% to 100%; 0% mutes that channel. **Reset Sound Volumes** restores both to 100%. The existing scrollable layout keeps the controls readable and supports controller-ray selection and stick/A navigation. The menu opens at its first control.

Changes apply immediately, including to already playing sounds, and save locally through PlayerPrefs. They persist when Settings reopens, the scenario restarts or another scene loads. Reset removes only the two audio preferences.

`Assets/Resources/GameAudioMixer.mixer` contains separate Ambience and SFX groups, with exposed volume parameters. Default 100% is unity gain, preserving authored sound balance. Ambience sources, UI clicks, tool sources, feedback and footsteps are routed to the relevant groups. Runtime-created shared sound/feedback sources are also routed. Existing source volumes and ambience ducking are not repeatedly multiplied. Runtime routing preserves a source assigned to another explicit mixer.

The Settings rows and source routes are saved and editable in `CSI_Environment`; the mixer is editable in Unity's Audio Mixer window. The setup menu is **Tools > ViCSI > Scene Setup > Install In-Game Audio Settings**. The editor helper uses Unity's native mixer authoring API through reflection, following the [Unity source reference](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/Audio/Mixer/Bindings/AudioMixerController.cs); runtime mixing uses public AudioMixer APIs. No package-cache modifications.

## Validation

**138 accepted checks passed, zero failures:**

| Suite | Passed |
| --- | ---: |
| AudioSettingsTest (run2) | 22 |
| SettingsPauseMenuTest (run1) | 49 |
| SceneAmbienceTest (run1) | 28 |
| InteractionSfxTest (run1) | 39 |

The dedicated suite uses actual in-game pointer clicks, checks independent buses/muting/clamps, rejects non-finite values, verifies immediate preference storage, reset and display refresh, confirms source routing without compounded gain, and reloads the scenario to verify persistence. Existing suites cover controller input, wall-safe settings placement, height/walking speed and navigation, ambience transitions and interaction sounds. Final C# compilation succeeded. Tests restore the original audio preferences and use the offline sandbox.

The rendered in-game preview `docs/evidence/audio-settings-2026-10-22/settings-preview.png` was visually reviewed: both labelled volume rows and percentage controls fit within the scrollable panel.

Initial editor/template recovery evidence and the first audio-test attempt are retained. The initial test used an untracked desktop camera position where wall-safe layout withheld the panel; the accepted rerun uses the same clear camera fixture as SettingsPauseMenuTest. No gameplay placement or input gate was bypassed. Unrelated editor-generated UI layout changes outside Settings were restored from the pre-task scene snapshot.

Report summary: `docs/evidence/audio-settings-2026-10-22/summary.json`. Physical headset loudness testing remains outstanding. No deduction/procedure changes, commits, push, deployment or production-data modifications.
