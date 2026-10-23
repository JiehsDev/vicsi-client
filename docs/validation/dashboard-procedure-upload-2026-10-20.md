# Dashboard procedure upload alignment — 2026-10-20

Shared ProcedureTimeline_1.json now includes completed check-in, initial gloves, used-glove disposal, fresh gloves, fresh swab loading, scaled photo capture, simulated sample drying, paper packaging and optional unscored fingerprint practice. Mirrored exactly in the dashboard procedureTimeline.v1.json. Current minimal uploads automatically retain these events through the existing definition filter; no raw logs or new grading components were enabled.

Scene-release wording distinguishes IOC authorization (SceneReleaseConfirmed) from actual completed release (InvestigationCompleted). Historical omitted procedure events are not inferred/backfilled.

ProcedureTimelineUploadTest: 14 PASS, 0 FAIL. Actual minimal upload filter and JSON serialization preserve source-specific events and original timestamps/sequences; attempts and start/open events remain excluded. The saved fixture is an isolated filter test, not a complete assessment or a claim of a headset playthrough.

Dashboard verification: TypeScript passed; 23 current session/API/detail-route tests, 52 compatibility tests and 21 responsive-browser checks passed. Combined total with Unity: 110 checks, 0 failures. API tests prove procedure records do not change authoritative deduction points.

No scene asset, scoring rule, database or deployment change. CSI_Environment scene hash unchanged. Detailed dashboard report: C:/Users/Roduf Eleu/Desktop/vicsi-dashboard/docs/reviews/dashboard-workflow-fixes-2026-10-20.md.
