# Recorder Documentation Accuracy - implementation report

Implemented against the current Unity/dashboard working trees and `docs/FEATURES_SINCE_LAST_COMMIT.md`. No commit, push, deployment, database migration, live upload or production record modification was performed.

## Scope and scoring

Only BODY-A, EVD-014, EVD-015 and EVD-018 use this assessment. Each subject offers six statements: three supported at 2 points each and three unsupported at 0 points each. Exactly three selections are required, giving a maximum of 6 per subject and 24 total. No partial credit.

Witness statements, dialogue, relationship review and final conclusion are outside this assessment. Deduction remains Reasoning 60%, Evidence Relationships 25%, Final Conclusion 15%. Recorder points never enter that calculation and there is no combined overall percentage.

The user approved preserving existing body-wound, knife/wound-consistency and spatter-reconstruction finding availability after recording completion, regardless of the three chosen statements. Compatibility gates retain their original prerequisites; actual student selections are saved without inserting preferred answers. A zero-point body entry can still complete documentation and permit body removal.

## Audit and exact statement choices

The scenario audit was completed before authoring statements. The complete facts, measurements, photographs, processing prerequisites and comparison timing are in [the audit](../plans/recorder-documentation-accuracy.md). All 24 exact choices, their stable IDs and 2/0 weights are in [the statement catalog report](../plans/recorder-documentation-statements.md).

The audit found:

- **Body A - John:** face up on the rear ground-floor kitchen floor, reddish staining on the shirt near the abdomen. Marker A, outline, scene/individual photography and positional measurements precede recording. Measurements use the two kitchen-wall references and discovered-evidence pairs. No autopsy, independently established wound dimensions, cause or exact time of death is available.
- **Evidence 1 - Kitchen Knife:** on the kitchen floor, reddish residue visible on the blade. Swabbing precedes handle fingerprint development, tape lift and storage. Photographs and positional measurements are available. A later successful comparison identifies Paul; that identity may not be known when recording opens.
- **Evidence 2 - Blood Spatter:** a group of reddish floor stains near John; photographed and spatially documented. A swab is collected; drying, packaging, sealing and storage are distinct later processing actions. There is no DNA or blood-group test. Authored reconstruction information is revealed through the existing post-recording findings and does not identify the attacker.
- **Evidence 3 - Empty Liquor Bottle:** upright, empty, visibly intact glass bottle on the rear ground-floor kitchen counter. Photographs, positional measurements and print development/lifting precede recording. A successful later comparison reveals John, Peter and Paul. Those identities are not universally available before recording.

Excluded as ambiguous or unavailable at recording time: a deep/narrow wound or wound measurements inferred from the model; definitive cause of death; old approximate 15 cm blade and 1.2 m positional prose; fingerprint identities requiring a later comparison; who used the knife; who was present during the attack; exact handling/attack/death times; DNA identity; intoxication; and later reconstruction deductions. The retained correct choices report visible facts or processing already completed.

## Exact choices and answer weights

Supported statements below are worth 2 points; unsupported statements are worth 0. This answer key is documentation for review, never investigation feedback.

### Body A – John / BODY-A

- John is lying on the rear ground-floor kitchen floor. **2 points** (rec-body-location-v1)
- John's position proves that he died from an accidental fall. **0 points** (rec-body-fall-v1)
- John is lying face up. **2 points** (rec-body-position-v1)
- The body's appearance proves that Paul killed John. **0 points** (rec-body-attacker-v1)
- Reddish staining is visible on John's shirt near the abdomen. **2 points** (rec-body-staining-v1)
- The body's appearance establishes that John died at exactly 3:00 a.m. **0 points** (rec-body-time-v1)

### Evidence 1 – Kitchen Knife / EVD-014

- Paul used this knife to stab John. **0 points** (rec-knife-use-v1)
- The kitchen knife was found on the kitchen floor. **2 points** (rec-knife-location-v1)
- Reddish residue is visible along the knife blade. **2 points** (rec-knife-residue-v1)
- This knife has been conclusively established as the cause of John's death. **0 points** (rec-knife-death-v1)
- The recovered fingerprint establishes the exact time the knife was used. **0 points** (rec-knife-time-v1)
- A latent fingerprint was developed on the knife handle and recovered with lifting tape. **2 points** (rec-knife-print-v1)

### Evidence 2 – Blood Spatter / EVD-015

- The reddish stain pattern is on the kitchen floor near John's body. **2 points** (rec-spatter-location-v1)
- The stain pattern identifies Paul as the attacker. **0 points** (rec-spatter-attacker-v1)
- DNA testing has confirmed that these stains came from John. **0 points** (rec-spatter-dna-v1)
- The pattern consists of a group of reddish stains on the floor. **2 points** (rec-spatter-appearance-v1)
- A swab was collected from the reddish staining. **2 points** (rec-spatter-swab-v1)
- The stains establish the exact time John was attacked. **0 points** (rec-spatter-time-v1)

### Evidence 3 – Empty Liquor Bottle / EVD-018

- The fingerprints prove that Peter was present when John was attacked. **0 points** (rec-bottle-presence-v1)
- The bottle was found on the rear ground-floor kitchen counter. **2 points** (rec-bottle-location-v1)
- The glass liquor bottle is empty and visibly intact. **2 points** (rec-bottle-condition-v1)
- The empty bottle proves that John was intoxicated when he died. **0 points** (rec-bottle-intoxication-v1)
- Fingerprints were developed on the bottle and recovered with lifting tape. **2 points** (rec-bottle-prints-v1)
- The recovered fingerprints establish when each person handled the bottle. **0 points** (rec-bottle-time-v1)



## Recorder interaction

- Six choices with an explicit "Choose exactly 3 of 6" prompt.
- Existing thumbstick navigation, B select/deselect and A confirm retained.
- Distinct available/selected columns, selection state, scroll and count; selecting a fourth choice is refused. Confirmation requires exactly three distinct IDs from that subject.
- Confirmation saves all three actual choices and locks the entry.
- No correctness, points, score or answer weights appear in the active investigation UI. Ready-state color indicates selection count only.

Native Unity captures: [six-choice UI](../evidence/recorder-documentation-2026-10-21/recorder-six-choices.png), [three selected](../evidence/recorder-documentation-2026-10-21/recorder-three-selected.png).

## Storage, upload and validation

`Assets/Resources/RecorderDocumentation_v1.json` is the versioned answer catalog, mirrored exactly in the dashboard. Stable statement IDs are independent of display text.

`EvidenceRecord.recorderDocumentation` freezes the subject internal ID and readable label, catalog version, all six IDs/texts/selection flags/weights/earned points, subject earned points and maximum possible points. A single existing `ReportLinesSelected` event stores that snapshot plus the three selected IDs and count. It survives minimal uploads with raw event logs disabled. No database schema change is needed.

The dashboard server validates subject/version, six distinct options, canonical text/weights, three selected flags, selected IDs/count, earned sums and maximum; duplicate confirmations, duplicate metadata keys, foreign subjects and tampered data are rejected. It derives Recorder points independently of the deduction scorer.

The actual isolated Unity upload contains four records with a 0-point body entry and three 6-point evidence entries, totaling 18/24. It is checked by the dashboard parser. Local browser fixtures combine those real Recorder records with a current deduction fixture; API fixtures separately exercise the same versioned statement format through upload/read/render. These are automated composite fixtures, not a claimed single headset playthrough.

## Student and professor dashboard

The existing shared session component renders a compact Recorder Documentation section. Each subject lists the student's three statements, earned points per statement, and subtotal out of 6. The footer shows Recorder Documentation Score out of 24. Deduction Score remains visually separate with its existing percentage.

The simplified procedure timeline emits one meaningful Recorder action per subject. Duplicate generic Logged/body-documentation rows are suppressed when the corresponding Recorder action exists. No six-row statement timeline is created.

Screenshots: [student Recorder total](../evidence/recorder-documentation-2026-10-21/student-recorder-total.png), [professor mobile view](../evidence/recorder-documentation-2026-10-21/professor-recorder-mobile.png).

## Historical compatibility

Historical events and legacy `session_report_lines` entries remain readable. Known old IDs use an explicitly identified legacy wording lookup; unknown IDs are retained. Since old wording was not frozen, that limitation is stated. No weights or grades are assigned retroactively. Missing/invalid scoring data displays score unavailable, and mixed legacy/new records do not receive a fabricated full-session score. Existing row-level access restrictions still apply. No historical records are rewritten.

## Validation

Final accepted results:

| Check | Result | Evidence |
| --- | --- | --- |
| Recorder Documentation: catalog, actual UI confirmation/lock, state immutability, separate grade, upload | 59 passed, 0 failed | `run5/RecorderDocumentationAccuracyTest.txt` |
| Recorder navigation, select/deselect, fourth-choice refusal, exact-three confirmation | 30 passed, 0 failed | `run4/RecorderReportLimitTest.txt` |
| Body documentation/removal | 49 passed, 0 failed | `run4/BodyRemovalTest.txt` |
| Bottle photography/measurement/print/storage/comparison/recording | 42 passed, 0 failed | `run4/BottleFingerprintProcedureTest.txt` |
| Existing deduction assessment | 41 passed, 0 failed | `run4/FinalDeductionAssessmentTest.txt` |
| Upload service | 33 passed, 0 failed | `run4/AssessmentUploadServiceTest.txt` |
| Simplified procedure upload | 14 passed, 0 failed | `run4/ProcedureTimelineUploadTest.txt` |
| Current full-case fixture, frozen assessment, minimal record serialization and mock transport | 70 passed, 0 failed | `run5/EducationalSessionRecordTest.txt` |
| Dashboard current and legacy suites, including local Unity exports | 83 passed, 0 failed, 0 skipped | `dashboard-unity-final.log` |
| Scoring suite, including local Unity exports | 219 passed, 0 failed, 0 skipped | `dashboard-scoring-final.log` |
| Student/professor responsive browser checks at 375, 768 and 1366 widths | 21 passed, 0 failed | `dashboard-browser-final.log` |
| TypeScript | Passed | `dashboard-final.log` |
| Scoped ESLint | Passed | `dashboard-lint.log` |
| Unity runtime/editor compilation | Passed, zero C# compiler errors | Updated assemblies and `Logs/Editor.log` |
| Changed-file whitespace checks | Passed | Terminal check |

Evidence paths above are relative to `docs/evidence/recorder-documentation-2026-10-21/`. Earlier failed attempts are retained as diagnostic evidence, not counted as passing results. The old full-case fixture assumed that storing a fingerprint also identified it; it now uses the real comparison desk. Test failures report their fixture output immediately rather than timing out on a missing final-assessment record.

Verification uses isolated Play-mode fixtures, mock upload transports and local dashboard fixtures; a physical headset playthrough remains unperformed.

All 18 requested validation areas are covered by these checks: four-subject scope; six choices; three supported and three unsupported; exact-three interaction; 2/0 weighting; 6/24 maxima; no investigation correctness feedback; unchanged 60/25/15 calculation; upload retention; student/professor parity; simple timeline; unscored historical compatibility; successful Unity compilation; and existing Recorder/scoring/upload/dashboard regressions.
