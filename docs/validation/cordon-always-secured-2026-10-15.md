# Cordon remains secured - 2026-10-15

Removed both cordon state changes requested by the user: the entrance tape no longer opens at First Responder handover, and the perimeter/post renderers no longer disappear at final closeout. `SceneCordon.Awake` keeps the authored closed entrance active and the legacy open entrance inactive. There is no turnover subscription, opening method or renderer-removal method left in `SceneCordon`.

`CordonVisible` now verifies the closed entrance tape together with the perimeter and posts. IOC scene-security acknowledgement and scene-survey recording require the visible secured cordon and closed entrance rather than an open entrance. The existing tape has no blocking collider; access and all other interaction requirements are retained.

SOCO closeout still waits for the explicit IOC completion confirmation and its dialogue before completing the investigation and freezing/finalizing the session. Its existing `SceneRelease` phase and log event remain an administrative milestone with no cordon visual operation. Updated completion dialogue: "Your completion report is recorded. The investigation is complete. The cordon remains secured."

Existing focused diagnostics check that the entrance remains closed after handover, IOC/SOCO progression remains possible, completion retains the visible closed cordon and fresh reload preserves the secured presentation. FirstResponderTurnoverTest's fixture now disables the separately tested contextual reasoning managers: those managers intentionally defer its expected professor line until checkpoint completion. Initial failed runs remain under `run1` and `run2`; run2 used the previous compiled assembly. Source/assembly timestamps were checked before the accepted rerun.

Final accepted results: **111 PASS / 0 FAIL** against the compiled on-disk source:

- FirstResponderTurnoverTest: 44/0, `docs/evidence/cordon-always-secured/run3/FirstResponderTurnoverTest.txt`.
- ScenarioPhaseFoundationTest: 19/0, `docs/evidence/cordon-always-secured/final/ScenarioPhaseFoundationTest.txt`.
- SOCOCloseoutTest: 48/0, `docs/evidence/cordon-always-secured/final/SOCOCloseoutTest.txt`.

Final Editor verification: clean CSI scene outside Play, no compilation failure, no pending diagnostic/XR restore, and both XR initialization settings restored. Authored entrance closed visual active, opened visual inactive, cordon rendering.

No build/install, live upload, commit, push or deployment. Quest appearance is not covered by the Editor checks.
