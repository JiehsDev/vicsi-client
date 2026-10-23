# Body Recorder targeting and test start — 2026-10-17

The saved BODY-A solid BoxCollider is disabled. Its active BodyMarkerAcceptanceZone is a trigger, so RecorderTool's solid-only raycast hit the floor instead of resolving the body. Existing tests called TryOpenReport directly and missed this targeting failure.

RecorderTool now resolves the existing examined-in-place acceptance box explicitly, within the normal five-metre range and before the nearest solid obstruction. Direct solid-evidence targeting retains priority. Removed bodies and disabled zones are excluded. The pointer uses the same hit resolution as the trigger action. The body's physics settings remain untouched, and all report/measurement gates still apply.

Added **Tools > ViCSI > Scene Test Start > Body recording**, appended as enum value 9 to preserve existing preset values. It prepares scene photography, BODY-A chalk/Marker A/final photo and the required scene measurements; equips the Recorder and faces the player toward BODY-A from the clear kitchen approach. Other items have only the documentation needed for measurement. No report answers, samples, collection, removal or scored reasoning answers are seeded. The player records the three body findings, holsters the restored Recorder, approaches empty-handed, then confirms preparation/removal. Restart restores the unfinished exercise. Release builds continue to ignore presets; automated test isolation prevents student uploads.

## Validation

Accepted results: **184 PASS / zero failures**.

| Suite | Passed | Evidence |
| --- | ---: | --- |
| BodyRemovalTest | 43 | `docs/evidence/body-recorder/final/BodyRemovalTest.txt` |
| SceneTestStartTest | 116 | `docs/evidence/body-recorder/run1/SceneTestStartTest.txt` |
| RecorderReportLimitTest | 25 | `docs/evidence/body-recorder/run1/RecorderReportLimitTest.txt` |

BodyRemovalTest now equips and aims the real Recorder at the existing trigger box, tests an intervening solid obstacle, then exercises report confirmation and the removal lifecycle. SceneTestStartTest covers all ten presets and the new preset's fresh restart, unrecorded body state, equipped tool and lack of fabricated samples/answers. Existing three-line-limit tests pass.

The first body attempt passed the new aiming checks but failed twelve downstream removal checks because the Recorder remained equipped when the fixture requested removal. The final fixture performs the normal empty-hands step after closing the report, which restores the Recorder. The earlier report remains in `run1/`; only the accepted final body report is counted. See `docs/evidence/body-recorder/summary.json`.

Runtime changes: `RecorderTool.cs`, `EvidenceMarkerAcceptanceZone.cs`, and `SceneTestStart.cs`. Diagnostics: `BodyRemovalTest.cs`, `SceneTestStartTest.cs`. Usage guide: `docs/DEVELOPMENT_TEST_STARTS.md`.

Final live Editor: clean saved CSI_Environment outside Play, no compilation failure or pending runner/request/XR restoration, both XR startup settings enabled. Scene Test Start is open with Body recording selected locally. `git diff --check` passes. No scene/evidence-definition edits, build/install, real upload, commit, push or deployment. Physical Quest trigger/aiming acceptance remains pending.
