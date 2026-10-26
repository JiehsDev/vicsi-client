# Interactive lobby seating

The office chair in Main now has a functional seated mode in both research and competitive source projects. Press the left controller's **X** button near the chair to sit; press **X** again to stand beside it. An action hint appears just below the fixed lobby menu. In the Unity editor only, F is an alternate input.

Sitting uses Meta FirstPersonLocomotor's AbsoluteEyeLevel teleport at the seat anchor with relative yaw toward the UI. It leaves headset tracking active, suspends joystick locomotion through the existing reference-counted helper, and restores that helper on standing, disabling or unloading. Standing uses the existing ground-level teleport and restores the prior facing direction. Walking away physically releases seated mode. Settings and other modal interfaces block the seat action; the competitive main menu's A/B ownership allows this separate X input. No session events, scores, study records or profile preferences are added.

Validated in the research Main scene in Unity Play Mode using the actual Meta rig and a simulated left XR controller:

- Sit and stand work; repeated calls in the same state are rejected.
- Simulated X input sits and stands; holding X does not toggle repeatedly.
- Seated eye height settles at approximately 1.32 m with the view facing the menu.
- An 8 cm tracked head movement remains an 8 cm view movement while seated.
- Standing returns to grounded feet and a 1.60 m editor preview eye height beside the chair.
- The lobby menu position stays fixed across the stand transition.
- A modal UI gate blocks seating and closing it permits seating again.
- Disabling the seated interaction hides the hint, releases its locomotion suspension, and restores normal grounding.
- Scripts compile successfully. The same runtime source and authored Main scene are copied into the competitive project.

The play check used AutomatedTestSandbox. Temporary editor XR initialization and Play Mode start-scene settings were restored after the check. This is editor validation; no new APK was built or tested on the headset.
