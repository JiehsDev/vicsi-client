# Quest controller-driven playthrough ? 2026-10-23

Status: remote playthrough stopped at the user's request; user will test physically while local log capture and automatic error checks run. Final score, completion time and leaderboard persistence remain unverified.

Device: Quest 3S, serial ending Q0DSN. Separate development application: `ph.edu.parsu.vicsi.competitive.playtest`. Research and competitive release installations were preserved.

Reached using Meta XR Operator button input: username entry (AA), start case, first responder briefing and receipt of CSI Form 1. No evidence state fixtures were used. Glove station has not yet been completed in this run.

Remote OpenXR input successfully reaches the game's Unity Input System UI. Simulated stick/pose inputs do not reach the OVR-based controller locomotion and hand anchors: one second of left-stick forward produced no meaningful rig movement. This limits this remote test and does not establish a physical-controller defect.

A development-only `QuestRemoteInputBridge` was built to route OpenXR controller input through Meta's UnityXR controller data source and update tracked hand anchors. Runtime activation is restricted to the separate competitive Playtest application. It does not advance scenario state, grant evidence, bypass procedure gates, teleport the player, or change scoring.

Screenshots and raw device captures remain local, per repository policy.


## Handoff to physical testing

User requested physical self-testing with log monitoring instead of continuing remote play. Force-stopped the Playtest app to clear remote overrides, removed the ADB remote-tool port forwarding, disabled the experimental remote setting, and restored normal proximity sensing. Opened the regular competitive release application; did not install the new bridge APK during the user's test. The bridge compiled but remains unverified at runtime.

Local capture: `Temp/competitive-live-logcat.log`. Background error/progression monitor: `Temp/monitor_competitive.py`, state `Temp/competitive-monitor-state.json`, findings `Temp/competitive-monitor-findings.jsonl`. All are ignored local artifacts. It checks for Unity errors, selected runtime exceptions/crashes, warnings and the most recent gameplay event every ten seconds. Capture continues between chat messages while ADB remains connected; findings are reviewed in chat when work resumes. No external notifications are sent.

Initial sampled release startup showed no Unity errors or crashes. Warnings include unsupported optional XR functions, unavailable haptics before controller readiness, and negative-scale BoxColliders on seven curtain objects. Collider warnings are potential geometry issues, not proof of a player-visible failure. The application then paused while the headset was idle.
