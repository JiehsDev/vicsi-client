# Walking speed in existing Settings

Added Walking Speed beside the existing Player Height controls in the utility-wheel Settings panel. Minus/plus change the scene's normal movement multiplier by 10%, clamped to 50–200%. Reset Walking Speed restores 100% and clears its headset-local PlayerPrefs preference. Height and navigation controls retain their existing behavior; the panel uses its existing scrolling and pointer/thumbstick navigation.

`PlayerWalkingSpeed` preserves the scene's authored walking/running/crouching factors, applies the chosen multiplier, and reloads the preference on scenario/lobby changes. The camera's temporary aiming slowdown composes with the preference, so lowering the camera restores the latest chosen speed instead of an old cached value. No movement is enabled while menus suspend locomotion.

The new component and Settings controls are saved in CSI_Environment and editable in the Inspector/hierarchy. `Tools > ViCSI > Scene Setup > Refresh Walking Speed Settings` refreshes the template, preserving existing height/navigation objects and saving a before-scene backup in the evidence folder. Runtime bootstrapping also supplies the owner on the lobby's locomotor.

Validation: **SettingsPauseMenuTest: 49 PASS / 0 FAIL**, including actual XR/pointer controls, nonzero real locomotor factors, speed limits/reset, camera slowdown/restoration, existing height controls, modal safety, and persistence through actual scenario restart, lobby return and another session. Unity compiled successfully with zero C# compiler errors. [Accepted report](../evidence/walking-speed-settings-2026-10-21/run3/SettingsPauseMenuTest.txt); [native preview at 130%](../evidence/walking-speed-settings-2026-10-21/menu-preview.png), visually inspected. Source whitespace checks passed; unrelated Unity YAML formatting was retained.

Earlier diagnostic reports are retained: the saved template originally placed new rows after existing navigation, and tests needed to scroll target controls into view. The final template and runtime layout explicitly order Height, Walking Speed, then navigation. The distant collision-test fixture now sits beyond the expanded neighborhood. Reopening the menu refreshes live values and restores its navigation hint instead of stale status text.

User preferences are captured and restored by the diagnostic. Physical headset testing remains unperformed. Existing unrelated working-tree changes are preserved; no production data, commit, push or deployment.
