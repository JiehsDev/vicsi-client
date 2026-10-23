# Adaptive dialogue layout ? 2026-10-19

Implemented text-sized upper dialogue and lower tooltip/ambient prompt panels. Short text uses a compact width (minimum 240 canvas units, capped by authored width); long text wraps at the authored maximum width (480 in this scene). Font auto-shrink is disabled, with a 24-unit minimum font size. Height fits all measured text and explicit paragraphs. Horizontal padding is 24 units on each side; vertical padding follows the existing setting with a 32-unit minimum total. Short replacements shrink again.

The edge facing the other channel remains fixed. The actual background images now match the measured panel rectangle: the upper background previously had a separate 0.78841 Y scale, and the lower background had extra size offsets. Countdown bars remain below the label inside its bottom padding. The shared placement solver can therefore use the visible rectangle's actual size. Queue, priority, countdown, skip, history, and tooltip replacement rules are unchanged.

The old maxPanelHeight value remains serialized but hidden; it no longer clips the panel at 240 units. Extremely long external passages can create tall panels. This change does not add pagination or scrolling.

## Accepted focused checks

| Suite | Attempt | Pass | Fail | Exit |
|---|---|---:|---:|---:|
| AdaptiveDialogueLayoutTest | run2 | 26 | 0 | 0 |
| DialogueCatalogTest | run1 | 60 | 0 | 0 |
| DialogueHistoryTest | run1 | 24 | 0 | 0 |
| DialoguePlacementTest | run5 | 13 | 0 | 0 |

Total: 123 PASS / 0 FAIL in these four suites. All 341 real catalog messages fit rendered text bounds. Checks include newlines, unbroken words, unchanged font size, compact/expanded transitions, repeat stability, both actual scene presenters, background bounds and countdown padding. The isolated placement fixture uses the actual presenters with unsolicited guidance suspended and a standing camera: long text, short replacements, and expanded text facing a wall all have visible safe slots, at least 5 cm separation, and clear measured rectangles.

The short/long native preview was inspected: [preview](../evidence/adaptive-dialogue/run2/short-and-long-dialogue.png). The queue/history suites ran before the final background-only normalization; the final visual geometry and padding are covered by focused run2 and placement run5.

## Broader test limitations

Full UIComfortInputTest attempts in run2 (headless) and run3 (D3D11) threw at its old Evidence Notes HudFollowCamera assumption and never produced a complete report; their owned processes were stopped after diagnosis. An exploratory extraction of its earlier dialogue/input prefix in run4 produced 36 PASS / 18 FAIL, including jump/input ownership and feedback placement assertions. These failures have not been attributed to a baseline or resolved, and are not counted in the accepted result. The temporary extraction was removed; UIComfortInputTest.cs is byte-identical to the pre-task baseline. The final standalone DialoguePlacementTest avoids its input/modal dependencies and verifies the two current presenters directly. No claim is made that the broad UI suite passes.

## Preservation and remaining checks

The baseline covers 365 existing project C# scripts plus the saved scene and dialogue catalog. Exactly four existing files changed: STCSNotificationUI.cs, NotificationUI.cs, DialogueCountdown.cs, DialogueTimingSettings.cs. The other 363 baseline files are hash-identical, including the scene and catalog. Saved scene SHA256 remains 91d5c8c35425cdc32fec5ecb6bbc01ebfb07c368e227d62da5e10fdf12786005. New files are two diagnostic suites/metas, a focused runner, audit script, plan, reports and evidence.

Accepted logs have no C# compilation, shader, null-reference or missing-reference errors; catalog fixture missing-ID warning counts exactly match the prior reference. Existing SDK/licensing/teardown diagnostics remain. No live Unity process or pending test request remains; XR startup flags are enabled and scoped XR status is clean. No scene save, build, install, upload, commit or deployment was performed. Physical Quest stereo readability, comfort and performance still need a headset check.

Reproduce via Tools/RunAdaptiveDialogueChecks.ps1 (four focused suites; use a fresh attempt directory). Audit: python Tools/Audit/adaptive-dialogue-results.py. Machine-readable results: docs/evidence/adaptive-dialogue/summary.json. Plan: docs/plans/adaptive-dialogue-layout.md.
