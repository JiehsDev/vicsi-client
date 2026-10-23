# Recorder exact-three selection - 2026-10-17

RecorderReportUI now allows exactly three unique report lines per evidence report. A fourth B-button selection leaves the current three choices intact; removing a Notes line frees a slot. The panel prompt and full-state hint explain the rule and how to replace a line. Both real input and SelectLineForTest share the same insertion guard.

Confirmation independently rejects fewer/more than three lines or duplicate IDs; RecorderTool also enforces the count before any procedural transition, report-line registry update or logging. The existing serialized minimumLinesRequired field is retained for compatibility but no longer configures this UI's exact-three rule. No evidence candidate content, fact prerequisites, procedures, scoring weights or scene assets changed.

RecorderReportLimitTest exercises real synthetic XR B/stick input for adding, fourth-line refusal, removal and replacement; checks count-based confirmation, external overfill defense, fresh report state and callback guards. BodyRemovalTest checks the three required body report lines and actual documentation/removal flow. ResponsiveOptionLayoutTest covers shared UI layout/input behavior.

The first diagnostic invocation could not locate the new test because the file had not yet been imported into Unity's asset database; that report is retained separately. A forced asset import and rerun provide the authoritative limit results. No build/install, real upload, commit, push or deployment. Physical Quest interaction remains unverified.

Final accepted verification: 107 PASS / zero failures (limit25, body41, responsive UI41). Exact paths in docs/evidence/recorder-three-lines/summary.json. Final CSI scene clean outside Play; zero compilation failure; no pending runner/XR restore; Standalone/Android XR startup enabled.
