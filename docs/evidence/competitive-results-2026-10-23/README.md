# Competitive result reveal and demo board ? 2026-10-23

Implemented in the competitive workspace only; no headset APK update in this change.

- First completed-case review page counts up the actual saved score, shows elapsed time, then animates the highlighted profile row into its best-run leaderboard position.
- Nearby ranks, scores and times appear in five bounded rows. Earlier personal-best retention is explicitly explained. Recorder points stay separate.
- Animation uses unscaled time, does not move the camera, can be skipped through normal review navigation, and settles immediately on reopening the same result.
- Six deterministic demo opponents (55?96 points) appear by default and are labeled DEMO. The home menu toggles them on/off. They participate only in the displayed board, never in saved history. The history insertion method rejects demo records.
- Existing player history, actual scoring, procedural closeout gates, and research project are preserved.

Validation:

- `dotnet run --project Tools/CompetitiveChecks/CompetitiveChecks.csproj`: 26 checks passed, covering score/time ordering, persistence, per-profile bests, demo isolation, rejection of demo saves, stable demo IDs and switching demo data off.
- `CompetitiveResultsPreview.Run`: Unity compilation succeeded; runtime preview confirmed score 82/100, rank 4 of 7 with demo opponents, rank 1 of 1 without them, and immediate settled results after reopening. No player run was saved by the preview.
- Rendered screenshot inspected locally (`Temp/competitive-results-preview.png`): score, rank, highlighted player, nearby ranks, DEMO labels, times and footer visible. Removed redundant score wording from the time line after inspection to improve spacing.
- Initial preview attempts sampled before animation completion; final check waits for the animation's completion state.
- Unity Editor startup also logged an unrelated SearchDatabase indexing exception and package/licensing diagnostics. This is an editor preview, not a completed headset playthrough.

Screenshots and temporary logs remain local and ignored by Git.
