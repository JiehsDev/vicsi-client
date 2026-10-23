# Lobby UI disappears in Play mode - 2026-10-11

Fixed the Main lobby's Editor preview in LobbyCanvasPlacement.cs. With normal XR initialization enabled, the rig settled onto the floor and its untracked eye pose reset the camera direction. The canvas remained active, but the camera saw the floor; centering the canvas after startup then put it behind the north wall. Earlier isolated layout checks did not reproduce this normal-XR startup.

Canvas placement now runs in LateUpdate after tracking, with a short startup window to handle pose changes. In the Editor only, an untracked zero-height eye gets a temporary 1.6 m tracking-space offset and the authored lobby-facing yaw. The yaw is maintained while the preview remains untracked. Tracking resumption or component disable restores the original tracking-space position and rotation. The fallback is excluded from player builds. Existing nonzero eye poses are retained.

Verified in Main Play mode with all XR initialization settings left enabled:

- Reproduced the original gray-screen render and zero-height camera.
- Reproduced wall occlusion after centering at the reset yaw.
- Final camera height: 1.5975 m; yaw: 179.61 degrees.
- Final canvas: active, centered 1.500833 m ahead; no collider between the camera and panel centre.
- Actual rendered capture shows ViCSI, Start Investigation, Instructor / Headset Management and the live status table.
- Disabling the component restored tracking-space local height from 1.6 m to 0 m; re-enabling it restored visible placement.
- Unity compilation succeeded; scriptCompilationFailed=False. Existing package/API/native XR warnings remain.

Evidence: docs/evidence/lobby-visibility-fix/final-play-view.png, final-play-state.txt, height-restore-check.txt and after-restore-state.txt. LobbyVisibilityProbe is an Editor-only, file-request diagnostic; it takes snapshots and tests temporary-height restoration.

Unity crashed during XR teardown after an early in-Play script reload. Restarted the Editor from the clean saved Main scene; subsequent verification used edits outside Play mode. No XR initialization setting, authored scene geometry, navigation callback, identity/session/upload logic or gameplay was changed for this fix. Normal lobby startup services were active; no session was submitted by this verification.

Physical headset tracking recovery and Quest readability remain unverified. No full regression, commit, push, build/install or deployment.
