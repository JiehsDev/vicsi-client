# Quest 3S validation, 2026-10-23

Scope: installed release APK and a user-assisted headset smoke test. This is not a completed investigation or a performance certification.

## Build and install

- Unity 6000.5.6f1, Android ARM64, IL2CPP, release build.
- APK: `Builds/ViCSI-Competitive.apk`, 682,302,413 bytes. Package `ph.edu.parsu.vicsi.competitive`, version 0.1.0 (1).
- Build succeeded with zero errors and 1,210 warnings. Full build took 27m 15s after the release compilation fix. See `build-summary.txt` for SHA256.
- Initial attempts exhausted memory while the research editor was open. With user approval, saved its open scene and closed that editor, then built in the separate competitive folder.
- Fixed release compilation by restricting `CompetitiveFlowSmokeTest` and `CrimeSceneSketchTest` to `UNITY_EDITOR || DEVELOPMENT_BUILD`. Both fixtures call a development-only glove helper. No gameplay implementation changed for this fix.
- `adb install -r` succeeded. Both `ph.edu.parsu.vicsi` and `ph.edu.parsu.vicsi.competitive` remain installed. Research app was not updated or removed.

## Observed passes

- Cold launch succeeded; competitive app stayed running and OpenXR reached `XR_SESSION_STATE_FOCUSED`.
- Stereo capture shows the competitive lobby title and all four menu options without visible text clipping.
- User explicitly confirmed: "Menu and controller buttons work" in response to the username and Leaderboard check.
- Stereo capture shows the username keyboard with an entered six-character name and Begin investigation selected.
- The investigation scene loaded and rendered on-device. Gameplay logs show scene entry, responder turnover, clean gloves equipped, and authorized scene-register entry using the selected username without a student ID.
- Device gameplay logs are under the competitive package's `files/competitive-v1/logs`, separate from research storage. No development preparation was used for the observed register entry.
- Captured post-startup and investigation log windows contained no Unity error entries or NullReferenceException, MissingMethodException, FATAL EXCEPTION, or SIGSEGV markers. These are sampled windows, not proof that every possible path is error-free.

## Findings and remaining checks

- Visual issue: near the body, the evidence waypoint label overlaps the narration panel. Captured in `06-investigation.png`; not fixed in this test session.
- Initial startup emitted a Play AssetPackManager class lookup error and unsupported optional OpenXR function warnings. The app continued to a rendered, interactive lobby and investigation. No startup crash was observed.
- A completed case is still needed to validate final score, elapsed time, leaderboard/history persistence after app restart, and score/time ranking on the actual headset. Those flows previously passed offline fixtures; this device session does not replace that remaining end-to-end check.
- Sustained frame rate, motion comfort, and every gameplay tool were not certified.

Screenshots, raw logcat, and pulled gameplay logs remain local and ignored by Git. The first two screenshot attempts were empty while the headset was asleep; `03-awake-menu.png` is a system overlay, not app validation. Valid app captures are `04-focused-menu.png`, `05-menu-check.png`, and `06-investigation.png`.
