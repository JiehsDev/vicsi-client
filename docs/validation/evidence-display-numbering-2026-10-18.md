# Evidence display numbering - 18 October 2026

Student-facing numbering is independent of persistent evidence IDs. This change is local to Unity and `C:/Users/Roduf Eleu/Desktop/vicsi-dashboard`. No commit, push, deployment, database migration or production-data write was performed.

| Stable internal ID | Student label | Physical item marker |
| --- | --- | --- |
| BODY-A | Body A – John | A |
| EVD-014 | Evidence 1 – Kitchen Knife | 1 |
| EVD-015 | Evidence 2 – Blood Spatter | 2 |
| EVD-018 | Evidence 3 – Empty Liquor Bottle | 3 |

## Central display mapping

`Assets/Resources/EvidenceDisplayNames_1.json` contains the versioned mapping. `EvidenceDisplay.cs` resolves labels, numbers, derived blood/fingerprint sample IDs and legacy IDs embedded in visible prose. `EvidenceDefinition.PlayerDisplayName` and the existing `EvidenceStateManager.GetDisplayName` use this mapping. Authored `displayName` fields and all serialized internal identifiers remain intact. Body-only procedural prose retains “Body A.” Derived inventory definitions retain sample prefixes and original provenance.

The dashboard mirrors the same mapping in `src/lib/data/evidenceDisplay.v1.json`, through `src/lib/evidenceDisplay.ts` and the existing `resolveEvidenceName`. An audit and integration test compare both JSON files. Historical records are formatted at read time; no stored text, IDs, events, timestamps, marker numbers, selections or grades are rewritten.

## Student-facing locations

Unity changes cover:

- Evidence discovery, interaction, swab/fingerprint, collection, sealing and storage notifications through the existing shared resolver and dialogue/prompt channels.
- Evidence notes and deduction-board evidence nodes; removed the raw-ID suffix from note headings.
- Inventory names, sample names, missing-icon text and held-evidence storage prompts.
- Evidence/Procedure list, item heading, step labels, next-action instructions and measurement names. List rows accommodate three-line numbered labels.
- Recorder report instructions and sketch evidence cards, legend and placement hints.
- Evidence Relationships Review and reasoning/hypothesis question/answer presentation through the shared checkpoint UI. Definition IDs, option IDs and dependency trees are unchanged.
- Custody-pass item review and final SOCO inventory/closeout review; removed duplicate raw-ID lines.
- Completion-screen evidence checklist and its existing shared evidence-label resolver.
- Live dialogue, legacy inline messages, tooltip/prompt text and dialogue history presentation.

Dashboard changes cover:

- Shared evidence-name resolver used by student and instructor/professor result views, evidence workflow lists and procedural notes.
- Main student and instructor session-detail procedure timelines, including IOC observations, measurements, derived fingerprint/blood samples and historical events.
- Frozen question and option prose and historical recorded-answer text. Formatting occurs after validation; frozen input and numerical grades remain unchanged.
- Legacy/training workflow timelines, evidence tables and blocked-procedure explanations.
- Instructor per-session CSV export adds `evidenceDisplayLabel` and `targetDisplayLabel`; existing machine identifier columns remain available for stable integration.

Lists of sessions themselves contain scenario/date/duration/score and do not expose evidence IDs, so they require no numbering change.

## Physical markers

The saved scene contains a numbered tent-template pool and a separate letter-tent tool. Previously the numbered dispenser chose the lowest unused pool slot, so numbering depended on placement order. It now reserves 1/2/3 for the mapped physical items. Aiming preview, held template, actual placed mesh, `tentNumber` event value and pickup slot agree. Trace, spare and non-evidence placements use the remaining pool (4–6); freeing a slot still requires the original per-marker recording rules. Body A still uses the existing lettered tool and its original procedure.

Development test-start helpers seed the same item numbers, avoiding misleading numbers in prepared starts. Neither the transition API nor procedure prerequisites were replaced with a new numbering gate. Historical marker numbers are displayed as actually recorded, even when different from the new evidence display number.

## Preserved references and upload behavior

The task-start SHA256 audit confirms all **130** existing serialized scenario/data/scene/scoring assets are unchanged. This includes the evidence definitions and GUIDs, procedure requirements, reasoning definitions, relationships, statements, measurement/sketch references and scoring rules. `REL-paul-timeline-contradiction`, `statement_vendor_paul_3am`, sample-source IDs and active evidence IDs retain their existing meaning.

Uploads continue using internal event targets and evidence/source IDs. The wire schema, upload authorization, grading rules and database schema are unchanged. No new display label is needed in uploaded data because the dashboard resolves existing IDs. The 60/25/15 deduction formula remains intact.

## Validation

**672 passing assertions across 15 Unity suites; zero failures in the accepted reports.** The accepted paths and counts are enumerated by `docs/evidence/evidence-display/summary.json` and verified by `Tools/Audit/evidence-display-summary.mjs`.

| Unity suite | Passing checks |
| --- | ---: |
| EvidenceDisplayTest | 33 |
| EvidenceTentPickupTest | 21 |
| EvidenceTraceMarkerTest | 21 |
| EvidenceProcedurePanelTest | 45 |
| InventoryUILayoutTest | 22 |
| BottleFingerprintProcedureTest | 40 |
| ReasoningCheckpointTest | 62 |
| EvidenceRelationshipsReviewTest | 41 |
| AssessmentUploadServiceTest | 33 |
| SOCOCloseoutTest | 50 |
| CustodyPassTest | 50 |
| EducationalSessionRecordTest | 70 |
| MeasurementToolTest | 41 |
| SceneRelationalMeasurementTest | 58 |
| CrimeSceneSketchTest | 85 |

The display suite places the bottle first, then spatter and knife, verifies the actual placed meshes against templates 3/2/1 and confirms Body A still accepts its letter. It also checks that notes hide IDs, prerequisites still refuse early recording and logger targets remain internal IDs. Measurement feedback, inventory and custody text fit checks pass. The sketch gold-ring screenshot was visually inspected; the numbered knife card and hint fit. The full-case fixture exports a fresh real-manager request and 576 Unity scoring vectors.

| Dashboard/build check | Accepted result |
| --- | --- |
| Fresh Unity upload, API and display tests | 21 pass; zero skipped |
| Existing dashboard regressions | 42 pass; zero skipped |
| General scoring regressions | 217 pass; two optional fixture skips |
| Fresh actual bottle-wire envelope | 4 pass; zero skipped |
| Fresh actual custody-wire envelope | 3 pass; zero skipped |
| Responsive browser | 21 pass; zero skipped |
| TypeScript, scoped ESLint, production Next build | Pass |
| Unity C# compilation | Zero C# errors in accepted compilation logs, including the final graphical run |

Both optional fixtures skipped by the general scoring command were subsequently supplied with fresh Unity payloads and passed their dedicated bottle/custody runs. Browser checks cover student, instructor and historical pages at 375/768/1366 pixels and explicitly reject raw active evidence IDs in rendered body text. Frozen questions, weights, selections and grades are checked for mutation; historical marker numbers remain as recorded. Fresh 576-case Unity/server scoring agreement passes the existing session tests.

Earlier attempts are retained separately. Stale diagnostic fixtures expected the former lab fingerprint step, redundant upload arrays or bottle measurements before final photography; they now honor the existing current procedure and minimal upload contract. A test-only null-conditional compilation error was corrected before the accepted clean runs. Offline custody validation positions an untracked observer inside the actual receiving bounds and still verifies rejection outside those bounds; no production range check was changed. A Play-mode attempt interrupted by script reload is excluded from acceptance. The old graphical Editor hit a native crash during a normal close after its scene was verified clean; its shutdown log is retained. Final validation completed in fresh isolated Unity processes, all now exited, with no pending requests and XR startup settings enabled.

Existing diagnostics refresh their shared local test-capture paths. Fresh task copies are recorded in `docs/evidence/evidence-display/capture-preservation.json`; this is isolated fixture output, not historical database data. Current UI previews are in `docs/evidence/evidence-display/browser/`. Direct-state developer fixtures may use legacy marker numbers; the real placement tool is independently tested for the new 1/2/3 assignment.

## Intentional internal-ID exposure and limits

Internal IDs remain in developer Inspector fields, scene object/asset names, gizmos, console/debug output, automated-test reports, local logs, stable upload payloads and structured CSV identifier columns. They are retained for diagnostics and machine references. Ordinary student labels and dashboard educational detail use the mapping; the CSV includes separate human labels alongside its technical columns.

No headset/device acceptance or live Supabase upload is claimed. Local tests use the existing isolated test sandbox and API/database substitutes. Existing Unity package warnings are distinct from C# compilation errors.
