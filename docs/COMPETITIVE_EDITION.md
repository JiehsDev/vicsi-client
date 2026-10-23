# Competitive edition

This working folder is the `competitive` Git branch. The research folder remains `C:\Users\Roduf Eleu\My project (1)` on `master`; the competitive folder is `C:\Users\Roduf Eleu\ViCSI Competitive`.

Open the desired folder through Unity Hub. There is no need to switch the research folder's branch or undo competitive changes. The GitHub tag `research-baseline-2026-10-23` and complete local archive `archive/research-complete-2026-10-23` remain available. Do not merge competitive-edition changes into the research branch.

## Player flow

- The lobby offers Start investigation, Leaderboard, Run history, and Game settings. Home, username entry, leaderboard and history share the original fixed lobby anchor, so looking around does not move the menu; returning from settings keeps the same location.
- Starting opens a controller-friendly username keyboard. Names use 2–20 letters, numbers, spaces, underscores or hyphens, starting with a letter or number.
- Gameplay, evidence processing, hints, questions, scoring rules and procedural closeout remain in place. The scene register uses the chosen username. Guidance speakers previously labeled Professor are displayed as Dispatch.
- A run begins when the investigation scene loads after username entry. Its timer stops at completed scene closeout. Explicit game settings/pause, lost application focus and application suspension pause this timer; ordinary in-game dialogues and investigation menus remain timed.
- The first final-review page counts up the investigation score, then slides the highlighted player row into its leaderboard position. It shows time taken, nearby ranks, and recorder notes out of 24. The roughly two-second reveal uses unscaled time and can be skipped with Next; returning to this page shows the settled result. Rank is based on the profile's best run; when an earlier best is retained, the screen says so. Recorder points remain separate from the investigation score.
- Every completed, scored run is retained in history. The leaderboard takes each local profile's best score and uses shorter time to break equal scores. Case, content and scoring versions identify comparable runs.
- Restarting creates a new run. Returning to the lobby or quitting without finishing closeout does not create a ranked result. Development scene-test starts do not rank.

## Demo rankings

Six labeled demo opponents appear by default (55-96 points, with sample completion times). The lobby's **Demo rankings: On/Off** button controls them. They are generated for display only, use the current rules, and never enter saved run history or count as played cases. Turning them off immediately restores rankings among real profiles. The result animation and lobby use the same ranking source.

An isolated editor preview is available with `-executeMethod CompetitiveResultsPreview.Run` (without `-quit`). It creates an unsaved preview scene and records `Temp/competitive-results-preview.png` plus a text result, without completing a case or saving player history.

## Separation

Product name: `ViCSI Competitive`. Android/desktop app ID: `ph.edu.parsu.vicsi.competitive`.

Run history lives under `<persistentDataPath>/competitive-v1/runs.json`; raw gameplay logs use its `logs` subfolder. Writes use a temporary file and replace the prior file with a backup. Unreadable existing history is preserved and new results remain available in memory rather than overwriting it. Storage failures are shown to the player.

Competitive startup bypasses student/pairing screens and disables student directory sync, instructor management and assessment upload startup/submission. No research session ownership is assigned. Existing course implementation types remain in source to avoid breaking scene references, but they are not part of the competitive player flow. This edition does not require a course server, student code or headset provisioning key.

## Local leaderboard scope

Rankings are **on this device**, not an online or authenticated cross-device leaderboard. Display names are local profiles, not secure accounts. The UI states that results are local. No server or dashboard deployment is included in this implementation.

## Validation

Run `dotnet run --project Tools/CompetitiveChecks` for ranking, identity-format and persistence checks.

An isolated Unity editor smoke test can be launched with:

```text
Unity.exe -batchmode -nographics -projectPath "C:\Users\Roduf Eleu\ViCSI Competitive" -executeMethod CompetitiveValidationRunner.Run -competitiveSmoke -logFile "Temp/competitive-editor.log"
```

It tests the actual lobby keyboard/leaderboard actions, scenario transition, identity bypass, isolated logs, paused timing, scene register and abandoning an unfinished run. Its history path is temporary and no research upload occurs. Report: `Temp/competitive-smoke.txt`. This is an offline integration fixture, not a human/headset playthrough or complete playable-build certification.

Accepted results on 2026-10-23:

- Ranking/persistence checks: **26 passed**, including score/time sorting, best result per profile, rules-version separation, invalid data rejection, duplicate completion and JSON persistence.
- Earlier Unity play-mode smoke: **29 passed**, including actual keyboard and leaderboard actions, scenario unlock without a course code, isolated logs/uploads, pause/focus timing, named scene-register entry, incomplete-run exclusion, and a synthetic scored closeout saved once with results text.
- Unity runtime/editor compilation completed without C# errors. Existing package/editor warnings remain.
- Research source remains separately preserved on `master` at `92cd962` and tag `research-baseline-2026-10-23`. This branch includes the competitive menu, results presentation, and authored register UI changes; existing evidence procedures and scoring rules are retained.

Saved text reports: [ranking checks](evidence/competitive-edition-2026-10-23/ranking-checks.txt), [Unity smoke checks](evidence/competitive-edition-2026-10-23/unity-smoke.txt).

Quest 3S release build, install, and user-assisted menu/initial investigation smoke test: [device report](evidence/competitive-headset-2026-10-23/README.md). A full scored case and persistence after restart remain to be checked on-device. The captured investigation view also shows a waypoint/narration overlap to fix.


## Marked checkpoint

Tag: `competitive-checkpoint-2026-10-23` on branch `competitive`.

Includes the competitive app identity and local run history, demo leaderboard and animated results, fixed lobby menu placement, and register UI cleanup. Latest UI checks: [results reveal](evidence/competitive-results-2026-10-23/README.md) and [register UI](evidence/register-ui-2026-10-23/README.md).

The installed competitive release APK predates the latest lobby, animated results, and register UI changes. Source and concise validation reports are versioned. Downloadable Asset Store binaries, builds, screenshots, raw device logs and temporary captures remain local.
