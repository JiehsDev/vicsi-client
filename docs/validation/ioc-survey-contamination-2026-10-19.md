# IOC initial survey and contamination guidance

Implemented the approved chronological step 3 using the existing IOC interactions.

After check-in, guidance now says to look only, avoid touching surfaces or stepping on evidence, and use the existing yellow kitchen highlight and A-to-observe interaction. The survey response explains limiting movement/contact to avoid moving or transferring traces and that gloves do not prevent all contamination. The bottle observation now explicitly says to observe it without handling it and leave it for SOCO.

The opening guidance, survey and bottle lines have 23, 29 and 29 words, with respective durations of 14, 18 and 14 seconds (approximately 97–124 words per minute). The opening guidance retains its queued dialogue channel, skip and history behavior. Survey and bottle responses retain their existing tooltip channel: a newer tooltip can replace them, and they are not added to dialogue history. These timings provide reading time; they are not physical headset readability acceptance.

## Scope and preservation

The content audit compares the current catalog against the snapshot taken immediately before this task. Exactly three existing entries changed: `ioc-assessment-guidance`, `ioc-observation-survey` and `ioc-observation-bottle`; no IDs were added or removed. The only changed fields in those entries are text and duration. The foundation diagnostic's obsolete exact survey-text expectation was updated to check the new preservation teaching.

The saved scene retains SHA-256 `91d5c8c35425cdc32fec5ecb6bbc01ebfb07c368e227d62da5e10fdf12786005`. No scene wiring is needed. The survey point, evidence positions, cordon, guide, scoring and upload contract are unchanged.

Code inspection confirms that SceneTurnover retains the existing victim-plus-at-least-two-possible-items requirement. IOCAssessmentGuide recommends survey first, while IOCAssessmentInteraction permits other valid observation orders. EntryAccessReady guards authoritative observations. IsInvestigationBlocked and ToolInputLock preserve the IOC/SOCO handover boundary, and evidence grab readiness depends on the existing processing requirements. No demonstrated bypass required a gameplay-code change. These controls do not simulate contamination detection.

Glove changes between sources and clean collection equipment remain later processing improvements.

## Validation

Accepted: **485 PASS / 0 FAIL across ten fresh Unity suite processes**, all exit 0. The initial eight-suite regression contributed 406 assertions; after preview-driven copy shortening, the final catalog and phase-foundation rerun contributed another 79 (60 + 19). The table lists the eight distinct suites; their counts were unchanged in the final focused rerun.

| Suite | PASS | FAIL |
| --- | ---: | ---: |
| DialogueCatalogTest | 60 | 0 |
| ScenarioPhaseFoundationTest | 19 | 0 |
| IOCAssessmentGuideTest | 35 | 0 |
| ArrivalPPETest | 42 | 0 |
| ArrivalEntryTest | 26 | 0 |
| SceneAccessRegisterTest | 40 | 0 |
| NarrativeRoleFlowTest | 41 | 0 |
| SceneTestStartTest | 143 | 0 |

The existing suites cover real simulated observation input, range/facing and occlusion, duplicate rejection, alternative evidence satisfying the two-item threshold, retained evidence positions/statuses, decision/arrival/briefing processing locks, normal investigation unlock, real briefing/glove/check-in prerequisites, access crossings, narrative restart and all twelve development presets. The catalog suite verifies resolution, uniqueness, text routing, timing, generic long-text expansion and history behavior. No new gameplay tests were added for this content change.

Accepted suite logs have zero C# compilation errors and zero null/missing-reference exceptions. Existing SDK/MRUK diagnostics remain; this is not a warning-free SDK shutdown claim. Final project state has no owning Editor, pending runner request or temporary preview script. XR settings are unchanged. The [manifest](../evidence/ioc-survey/summary.json) records per-run counts, diagnostics, content/layout audits and source hashes.

The final native Direct3D previews of the three actual revised messages were inspected. All fit without shrinking their authored fonts: opening guidance is three lines at 20 points (68.34 preferred height / 120 available), survey is six lines at 29 points (199.14 / 239.14), and bottle is five lines at 29 points (165.79 / 205.79). The opening text clears the countdown bar; survey and bottle remain above theirs. See [opening guidance](../evidence/ioc-survey/preview/ioc-assessment-guidance.png), [survey](../evidence/ioc-survey/preview/ioc-observation-survey.png), [bottle](../evidence/ioc-survey/preview/ioc-observation-bottle.png) and [layout metrics](../evidence/ioc-survey/preview/layout.json).

Earlier previews are retained in `preview-attempt1`, `preview-attempt2` and `preview-attempt3`. The initial survey exceeded the tooltip height. The opening guidance's background has a vertical scale below one, so its countdown bar crossed longer text even when the root text rectangle passed the preferred-height check. Shortening the copy resolved both without changing scene/UI geometry or fonts. Existing arrows still guide the route; the opening message now focuses on the destination, action and preservation reminder. The fuller contamination lesson remains in the survey response.

The preview used the actual saved scene panels and the production DialoguePanelLayout.Fit method on disposable clones outside Play mode. It isolates typography from the live headset view. The final preview process exited 0. `Tools/IOCSurveyPreview.cs` retains the diagnostic source; its temporary Editor copy and generated meta file were removed. No scene or authored UI was saved by the preview.

Reproduce the Unity checks while no Editor owns the project:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/RunIOCSurveyChecks.ps1 -Attempt <new-folder>
python Tools/Audit/ioc-survey-audit.py
```

The runner uses a fresh process per suite, retains logs and refuses existing reports. The content baseline and audit are in `docs/evidence/ioc-survey/`.

Physical Quest readability remains manual acceptance. No build/install, live upload, commit, push or deployment is part of this task.
