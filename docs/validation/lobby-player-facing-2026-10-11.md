# Lobby player facing correction - 2026-10-11

The previous Editor-only tracking-space yaw workaround did not cover a tracked headset or player-build startup. This follow-up changes Main's actual startup player facing through the existing Meta FirstPersonLocomotor.

LobbyCanvasPlacement captures the authored lobby-facing direction, waits for initial frames, and submits one absolute rotation with no translation through the locomotor. This lets the locomotion capsule and player origin rotate together. The same correction runs once when the first tracked head position becomes available. It is included in player builds. Subsequent user turning remains unrestricted.

The canvas now stays on the authored room-facing side, rather than being moved toward whichever wall the startup camera faces. Removed the Editor-only forced TrackingSpace yaw; retained the temporary untracked Editor eye-height fallback and its restoration.

Verification with XR enabled in Main:

- Initial player root and camera: yaw 179.61 degrees into the lobby; TrackingSpace local yaw 0 degrees.
- Actual rendered menu visible, with no collider along the camera-to-panel-centre ray.
- Used the existing locomotor to deliberately turn toward the wall: root/camera yaw 0 degrees persisted, proving the menu component does not continuously force player facing.
- Restarted lobby initialization: root/camera returned to 179.61 degrees, TrackingSpace yaw remained 0, and the menu visibly rendered 1.500832 m ahead.
- Final compile succeeded and scriptCompilationFailed=False. An initial obsolete GetInstanceID compiler error was corrected before verification; existing package and XR warnings remain.

Evidence: docs/evidence/lobby-player-facing/startup-state.txt, startup-view.png, wall-fixture-state.txt, recovered-state.txt and recovered-view.png.

Physical headset startup/first-tracked-pose transition remains unverified. No scenario gameplay, authored scene geometry, navigation callback, session/upload logic, XR initialization setting, commit, push, build/install or deployment changed.
