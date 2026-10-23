# Downloaded interaction sound effects

Downloaded and imported 22 sound clips, using the preceding no-attribution research and the user's instruction to exclude zipper, door and cabinet sounds. Files, scripts and the current CSI_Environment scene are saved locally. No commit, push, deployment, production data modification or paid download.

## Assets and provenance

Imported files: `Assets/_Project/Audio/InteractionSfx/`.

Editable sound bank: `Assets/Resources/InteractionSfxLibrary.asset`. Per-tool AudioSources and the scene's `Interaction SFX` object are saved in the hierarchy; all clips and gain settings can be edited. Authoring refresh menu: `Tools > ViCSI > Scene Setup > Install Interaction Sound Effects`. A before-scene copy is retained in the evidence folder.

The 22 clips comprise 13 action sounds and three variations each for concrete, wood and grass footsteps. The mono PCM WAVs total approximately 1.3 MB. Unity imports them as mono, 44.1 kHz, Vorbis, decompressed on load, with no play-on-awake. Long writing, paper and tape recordings are trimmed to short action excerpts; the chalk recording loops only during contact.

Sources are [Kenney Interface Sounds](https://kenney.nl/assets/interface-sounds), [Kenney Impact Sounds](https://kenney.nl/assets/impact-sounds), the three previously selected Freesound CC0 recordings, and the selected [Mixkit paper](https://mixkit.co/free-sound-effects/paper/) and [click](https://mixkit.co/free-sound-effects/click/) recordings. Kenney/Freesound files use CC0; Mixkit files use its Sound Effects Free License. No creator credit is required under those checked licenses. No extra claim of exclusive ownership or a guarantee against legal claims is made.

The Freesound originals require account login; their openly offered high-quality MP3 previews were obtained from the publisher's own pages/CDN under the recordings' same CC0 terms. The manifest explicitly records that fact. The Mixkit recordings are the original WAVs linked by its official download modals. Source archives, license copies, item/download-page copies, original selected recordings, source URLs, acquisition date, edits and original/converted SHA-256 hashes are retained under `docs/licenses/interaction-sfx-2026-10-22/` and the evidence folder. The manifest contains 22 unique assets, and original/converted hashes were verified. The Python downloader's temporary dependencies live outside Unity's disposable Temp directory.

## Wiring

| Interaction | Sound and behavior |
| --- | --- |
| UI A-confirm and pointer-trigger selection | Neutral selection click; overlapping UI dispatches share at most one click per frame. |
| Tool/utility wheel selection | Same neutral selection click on a successful tool toggle or available utility entry. |
| Existing generic registered/refused actions | Downloaded neutral confirm/refusal clips replace the generated tones in existing feedback slots; haptics retained. Refusal continues to mean a procedural block, not an incorrect assessment answer. |
| Recorder B select/deselect | Neutral selection click, independent of statement weight. |
| Recorder confirmation | Writing sound after any successfully saved three-statement entry, irrespective of documentation points. |
| Camera | Shutter at the existing successful-photo hook. |
| Flashlight/UV | Switch tap at the existing beam-toggle hook. |
| Chalk | Actual chalk recording through the existing loop hook. Menus stop the loop; the guide's real contact tolerance determines audible contact. Procedure coverage/threshold logic remains unchanged. |
| Measuring tape | Short mechanical excerpt when hooking/starting extension; holstering an extended tape plays the same short mechanical texture at the old tip position. |
| Register signing | Pen-writing excerpt after local registration saves successfully. |
| Sketch submission | Pen-writing excerpt on successful submission. |
| Album navigation | Soft page sound when changing the selected photograph. |
| Prepared blood-evidence package retrieval | Paper-handling excerpt on actual packaging completion. |
| Fingerprint lift | Tape-peel excerpt after a successful source-linked lift. |
| Existing swab container-close hook | Small plastic closure clip assigned; prepared paper evidence packages use the tape-sealing sound instead. |
| Evidence-bag insertion | Paper handling at the existing insertion hook; no zipper. |
| Numbered and body-letter evidence-tent placement | Soft object contact through the placement hooks, without varying by evidence validity. |
| Evidence-box deposit / bulk custody transfer | Soft placement sound after custody transfer, one sound for a bulk action. |
| Walking | Distance-based steps with surface variations. Stationary movement, height changes, teleports, airborne state and open menus do not generate footsteps. Camera aiming can retain slow walking. Cadence is distance-based and rate limited, so walking-speed preferences remain compatible. |

Spatial one-shots use a fixed eight-voice pool with no Doppler. UI is non-spatial. Gains are kept restrained. Surface choice uses terrain/collider material or object-name hints with concrete as fallback; this is not a complete authored surface map of every room.

Zipper sounds and door/cabinet hardware remain excluded. Glove donning, swab contact and powder brushing have no newly selected recordings and were not filled with unrelated effects. No procedure scoring, deduction weights, relationship rules or conclusion rules were changed.

## Validation

The first focused Play-mode audio diagnostic passed 38 checks: all action clips decoded to non-silent mono audio; all expected saved tool references were present; UI gain and frame deduplication were correct; spatial voice allocation stayed bounded; footsteps handled movement, stationarity, menus, teleport, height changes and rate limiting; menus stopped chalk audio; deduction remained 60/25/15. A further check now verifies tape audio for prepared paper-package sealing.

Recorder interaction passed 30 checks and actual measuring-tool regression passed 41 checks. The first broader sweep exposed stale test fixtures: chalk's multi-item flow needed the existing real supply-station visit between samples, and fingerprint-storage assertions still expected identity before comparison. The fixtures now visit the supply station and compare the print at the actual desk, preserving runtime gates and the user's prior requirement that storing a lift does not identify it. These earlier failed reports remain available for audit.

Final accepted results: **280 PASS / 0 FAIL** across seven suites:

| Suite | Passed | Evidence below `docs/evidence/interaction-sfx-2026-10-22/` |
| --- | --- | --- |
| InteractionSfxTest | 39 | `run2/InteractionSfxTest.txt` |
| RecorderReportLimitTest | 30 | `run1/RecorderReportLimitTest.txt` |
| MeasurementToolTest | 41 | `run1/MeasurementToolTest.txt` |
| ChalkProcedureTest | 39 | `run2/ChalkProcedureTest.txt` |
| FingerprintStorageTest | 35 | `run2/FingerprintStorageTest.txt` |
| SamplePreparationTest | 47 | `run2/SamplePreparationTest.txt` |
| SettingsPauseMenuTest | 49 | `run2/SettingsPauseMenuTest.txt` |

Unity runtime/editor scripts compiled successfully before the passing diagnostics. Source whitespace checks passed and all 22 original/converted file hashes matched the manifest. The imported list contains no zipper, door or cabinet files. Earlier failed attempts are retained and excluded from the passing total.

Validation is offline, using the project's existing isolated diagnostics. Physical headset listening remains a manual acceptance check for balance and perceived material realism; playback routing and decoded data checks are not a substitute for that listening check. Initial importer code was updated to Unity 6.5's sample-settings preload API before Play diagnostics.
