# Quest validation — 2026-10-10

Status: current checkpoint built, installed and launched; interactive headset acceptance pending.

## Build and installation

- Source checkpoint: `5238dd24a677412c9752473650f198d5e923cd69`.
- Unity: 6000.5.6f1; existing `QuestBuild.Build` entry point, Android target and enabled Main/CSI_Environment scenes.
- Output: `Builds/Android/ViCSI-5238dd2.apk`, 633,037,712 bytes.
- SHA256: `E1639F4FD9173E6E15E0ED45427D3FA79597EF7C2BCE4EF61A4DB3124ABAFB9B`.
- Unity result: Succeeded, 0 errors, 1129 warnings; elapsed 13 minutes 35 seconds. Build warnings include obsolete Unity/TMP APIs; this is not a warning-free build.
- Full build log: `Logs/quest-build-5238dd2.log` (generated, local).
- Connected hardware: Quest 3S, ADB serial `340YC10G7Q0DSN`.
- Package: `ph.edu.parsu.vicsi`, version 0.1.0, versionCode 1.
- `adb install -r` returned Success; app data preserved. Package lastUpdateTime: 2026-10-10 08:32:26.
- Launch activity: `com.unity3d.player.UnityPlayerGameActivity`; `am start -W` returned Status ok, COLD launch.
- Process remained present as PID 11070 at the initial startup capture.

## Observations and unresolved checks

Initial PID-filtered log is saved in `docs/evidence/quest-validation-2026-10-10/startup-logcat.txt`. OpenXR loader initialization and Meta headset extension support were logged. The log then records loss of focus, termination of the app window and APP_CMD_STOP; visible rendering and playable startup have not been confirmed. The user has been asked to wake/wear the headset and report the start-screen/IOC-session behavior.

The captured log contains a Java `ClassNotFoundException` for `com.google.android.play.core.assetpacks.AssetPackManager`, plus unsupported OpenXR-extension and orientation warnings. No AndroidRuntime fatal exception was captured in this initial log. These messages remain recorded for investigation; process presence does not establish that they are harmless or that gameplay is working.

Active Main and CSI_Environment scene configuration references point to the existing QuestDev asset (`2eefae22886d4265a27907c3b2bf4bf5`). The empty Production endpoint recorded in the prior Editor audit is a separate asset. No endpoint, credentials or live hosted data were changed. Pairing/upload/retry have not been tested.

The interactive Editor crashed during a normal close request; its crash trace included AppUINativePlugin. After approval to stop the remaining processes, the headless build completed and exited normally. Unity restored its temporary XR preload additions to Player Settings; tracked Assets and ProjectSettings remain at the checkpoint.

Use [quest-validation-checklist.md](quest-validation-checklist.md) for the full walkthrough. START / IOC is the current pending step. Readability, tracking, comfort, procedures, vendor interaction, endings, performance and upload/retry remain unverified on hardware. No checklist item has been marked passed solely from build/install logs.
