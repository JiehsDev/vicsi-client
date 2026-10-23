# Final Unity/dashboard consolidation — 2026-10-18

This pass changes presentation and validation only. No gameplay feature, internal ID, scoring rule, prerequisite, upload contract, database migration or production record was changed.

## Already implemented and retained

- Shared evidence labels and readable reasoning/relationship labels exist in Unity and vicsi-dashboard. Current checkpoint numbering follows the six authored definitions; legacy HYP numbering remains independently readable. The integrated HYP-02 fingerprint panel is current Reasoning Question 3.
- Current scoring 1.7.0 uses Reasoning 60%, Evidence Relationships 25%, Final Conclusion 15%. Procedures are not scored. Historical scoring versions keep their original semantics.
- Current uploads serialize a minimal educational wire record rather than the legacy analytics DTO. Source IDs and frozen assessment facts remain technical verification inputs.
- Both primary dashboard routes use SessionRecord with educationalOnly queries. Student navigation is My Sessions → View → session record; instructor navigation is Class Results → student session/history → session record. There are no primary charts, benchmarks, insight cards, procedural percentages or large KPI panels. Legacy analytics helpers remain as compatibility code, without primary-page imports.
- Timelines use an allowlist of meaningful completed actions, retaining original elapsed milliseconds and sequence numbers. UI opening, hovering, polling, blocked-attempt noise and debug identifiers are not timeline rows. Snapshot/authorization/verification events remain internal.

## Changed in this pass

- Unity prose now uses the complete Body A – John label, matching the dashboard. The lettered physical body marker remains A.
- The shared display JSON adds historical phone/glove labels copied from the archived authored assets. EVD-017 and EVD-019 remain their original technical IDs; they are not added to the active physical roster or renumbered. Numeric unknown EVD IDs render as Unrecognized evidence instead of leaking their raw identifier or borrowing an active item's number. Known sample labels still retain their source item.
- Removed duplicate dashboard evidence-name constants in favor of the shared JSON. Active labels remain exactly Body A – John; Evidence 1 – Kitchen Knife; Evidence 2 – Blood Spatter; Evidence 3 – Empty Liquor Bottle.
- Session-table accessible View labels now use scenario/date instead of a technical session UUID. Routing still uses the stable session ID.
- Added a captured-session integration regression through the actual upload handler and both actual detail routes. It checks every question, every option in order, its associated weight, exact selected option, earned/max weight, ordered timeline, component results, final score and absence of raw IDs/analytics.
- Added read-only source/scene audit tools and fresh validation artifacts. Existing gameplay/configuration data remain unchanged: SHA256 comparison preserves 147 existing serialized scene/data files; the only modified existing serialized file is the shared presentation JSON.

## Final record and upload structure

The educational page contains session information, elapsed-time procedure timeline, all scored deduction questions, answer text/weights, selected answers, earned/max weights, component deduction results, the calculation explanation and final deduction score. Historical pages explicitly identify unavailable question text/options/weights instead of inventing them.

The current wire has 19 top-level fields: payloadVersion; sessionId/studentId/classId; scenarioId/scenarioVersion/scoringRulesVersion/groundTruthVersion/clientVersion; isTestSession/includesRawEventLog/hasDeductionAssessment; startedAtUtc/completedAtUtc/durationSeconds; selectedConclusionId/resolvedEndingId; deductionAssessment; orderedEvents. Tokens, recipient binding, queue/retry/idempotency behavior remain in their existing transport/service layers. No credentials are exposed in the educational record.

orderedEvents contains completed procedures and verification events. DeductionAssessmentSnapshot carries the frozen question ID/category/dimension/text, all actual offered options and weights, selected ID/weight, maximum weight, availability and timing, alongside the original facts required by the authoritative scorer. There is no duplicate essential-event transport array in the current wire and no legacy evidence-result/measurement/scene-photography/violation analytics sections. The retained internal DTO still supports older formats and local queue metadata.

The current captured case has 13 scored questions: six reasoning, six relationship review and one final conclusion. It offers 50 options in this supported case (four per reasoning/relationship question and the actual two offered conclusion choices). Hidden conclusion choices are not fabricated. The capture's components are 60 + 25 + 15 = 100. The 576 Unity scoring vectors still match the backend's rounded components and totals.

The repository SQL RPCs accept omitted legacy child arrays using coalesce(..., '[]'::jsonb); session events retain stable target IDs and JSON payloads. Current score persistence uses the existing verified deduction categories. Local API/database adapters validate the round trip, authorization and retries. No live Supabase write or deployed-schema acceptance is claimed.

## Scoring and identifier boundaries

Reasoning = earned weight / 24 × 60. Relationships = max(0, earned relationship weights − unsupported interpretations × 1) / 6 × 25. Final Conclusion = context-dependent credit × 15. Authoritative code/resources were inspected and left unchanged; 219 scoring tests passed with no skips, including historical-file hashes and current scene geometry profile alignment.

No EVD/HYP/RC/REL IDs appear in the tested normal student/instructor record prose. Technical IDs intentionally remain in raw uploads, stored records, routing URLs, CSV technical ID columns, developer logs/inspector fields and diagnostic artifacts. CSV exports retain readable label columns beside their technical identifiers. Stable IDs are never replaced by presentation labels in joins, scoring, requirements or validation.

## New-house migration status

The migration is implemented in the saved investigation scene. Vicsi_House is active; Harong is retained and inactive. All four current physical evidence objects are present in the rear kitchen. Measurement references/expected distances and sketch anchors/plans agree with the current new-house-v3 layout and the dashboard's registered server geometry. Cordon and arrival-to-kitchen traversal checks pass.

Both enabled build scenes (Main and CSI_Environment) load with zero active or inactive missing scripts, zero broken serialized component references and no missing local fileID references. The read-only audit leaves the saved scene clean. The environment was not edited or saved by this task.

One environmental validation issue remains: the standing-room check at scene-exterior-establishing reports overlap with Sidewalk surface. The other 61 layout/traversal checks pass. This flag requires environment/collision-check follow-up and physical acceptance; it is not a missing reference or a scoring/upload failure. No environment change was made because this pass concerns consolidation. Functional full-case captures and all other completed scene checks continue to run.

Old migration/reference preservation scripts were also inspected/run. Their older baselines no longer represent the current completed project: the migration comparison reports added bottle print references and three bottle dialogue entries, with no retired IDs removed; the reference-only comparison predates later scene additions. Their failures were recorded rather than rewriting the scene or old snapshots to force a pass. This pass's own starting-state hashes and current Editor/reference audits are the relevant preservation evidence.

## Validation and remaining limits

Dashboard: 52 tests passed. Session/upload/actual-detail-route round trip: 19 passed. Scoring: 219 passed without skips. Responsive browser: 21 passed across student/instructor lists/details, historical/empty/loading states and three widths. TypeScript, scoped ESLint and optimized Next build passed. Browser DOM checks reject any numeric EVD ID plus HYP/RC/REL IDs; screenshots were visually inspected for instructor question/weight presentation and mobile student component/final-score presentation.

Fresh Unity functional validation passed 342 assertions in six suites with zero failures and zero C# compilation errors: evidence display 43, educational session 70, reasoning 62, relationship review 41, measurement tool 41 and sketch 85. Full paths/results are in `docs/evidence/final-consolidation/summary.json`. The separate environment audit has the one standing-room failure described above. An initial audit-only Editor helper used an API removed in Unity 6000.5; it was corrected to objectReferenceEntityIdValue before the successful scene inspection and subsequent runtime suites. The failed initial log is preserved separately.

No blocking issue has been found in the educational record, scoring, upload or dashboard integration. The sidewalk-overlap flag remains unresolved; headset/live-hosted acceptance is not part of these local tests. The existing research scoring resource's expert-validation status was not changed. No commits, pushes, deployments, seeds, migrations or production-data writes were performed.

Artifacts: `docs/evidence/final-consolidation/source-scene-audit.json`, `scene-validation.json`, `layout-validation.txt`, `upload-structure.json`, `legacy-baseline-differences.json` and the accepted suite reports listed in summary.json. Dashboard screenshots remain local fixtures under vicsi-dashboard/docs/evidence/session-simplification/browser.
