# Recorder Documentation Accuracy implementation plan

Baseline: current working trees, including `docs/FEATURES_SINCE_LAST_COMMIT.md`. Preserve existing edits. Scope is BODY-A, EVD-014, EVD-015 and EVD-018 Recorder entries only. No commits, deployment, live uploads or production data writes.

1. Audit scenario assets, visible models, measurement gates, Recorder interactions, finding prerequisites and session/dashboard storage. **Completed; see audit below.**
2. Author a frozen version-1 catalog with six choices per subject, three at 2 points and three at 0. Exclude uncertain forensic interpretation. Preserve internal evidence IDs. Resolve the legacy finding-unlock dependency before changing that part of the flow.
3. Keep Recorder Pool/Notes interaction: select/deselect, maximum three, confirm exactly three, then lock. Never present accuracy, weights or points during investigation.
4. Freeze the six options and three selections in the existing single confirmed Recorder event. Derive the separate 6-per-subject, 24-total assessment from versioned statement IDs. Retain upload through the existing simplified event envelope.
5. Validate the optional Recorder snapshot on the server. Render a compact separate Recorder Documentation section in the shared student/professor SessionRecord. No database migration or combined grade. Historical entries remain unscored.
6. Run focused Recorder/assessment/upload tests, existing relevant regressions, dashboard server/rendering tests and TypeScript checks. Compile Unity. Inspect the six-choice UI and capture validation evidence.

## Audit completed before statement authoring

Implementation and validation are complete. All six planned steps were carried out; see [the final report](../validation/recorder-documentation-accuracy-2026-10-21.md), [exact choices](recorder-documentation-statements.md), and the accepted test evidence linked there. No ambiguous statement was used as a supported answer. No production records or main deduction formula were modified.

Sources: four active EvidenceDefinition assets; current measurement requirements; RecorderTool and RecorderReportUI; body renderer preview `docs/evidence/body-chalk-fit/body-top.png`; new-house migration/reposition validation; fingerprint comparison state and notes; narrative/finding prerequisites; current session serialization and shared dashboard SessionRecord.

### Body A – John / BODY-A

- Body lies on the rear ground-floor kitchen floor. The model is face up with bent/asymmetric limbs; reddish staining is visible on the shirt near the abdomen.
- The visible model alone does not establish wound depth, precise wound dimensions, cause of death, time of death or attacker identity.
- Authored narrow/deep sharp-object wound findings are revealed through the old Recorder line selection. They are not independent pre-recording proof and will not be used as a new correct statement.
- Scene and individual documentation photographs, Body Marker A and an outline precede recording. Measurements include the two fixed kitchen-wall references and required discovered-evidence pairs; the Recorder already displays recorded spatial measurements.
- Body documentation precedes wrapping/transport. No autopsy result or laboratory cause-of-death result exists at recording time.

### Evidence 1 – Kitchen Knife / EVD-014

- A kitchen knife lies exposed on the kitchen floor. Reddish residue is visible on its blade; the handle has a recoverable latent print.
- The game requires the blade-residue swab before print processing. Powder/brush development, tape lift and explicit storage establish a recovered print before Recorder logging.
- A correct comparison subsequently identifies Paul. Recording may occur before comparison, so identity is not a universally available Recorder fact.
- Fixed-reference/evidence-pair measurements and raw/final photographs are available. Existing prose says approximately 15 cm blade and 1.2 m from the victim, but neither number will be a correct choice: the measuring workflow measures scene positions, and positions have been edited since earlier prose.
- Handling evidence does not establish who used the knife during the attack, when it was used, or whether it caused the fatal injury.

### Evidence 2 – Blood Spatter / EVD-015

- Reddish stains form a pattern on the kitchen floor near the body. Documentation and positional measurements are available before recording.
- A swab is collected from this source, with drying, individual paper packaging, sealing and storage tracked separately. Recording does not establish DNA identity or a blood-group result; no such test exists in this flow.
- Later authored reconstruction communication discusses an attack area near John and explicitly says the pattern does not identify the attacker. That interpretation is tied to old report/finding selections; it is not a new Recorder answer.
- Exact attack direction, mechanism, timing and the person who deposited the stains cannot be assumed from appearance alone.

### Evidence 3 – Empty Liquor Bottle / EVD-018

- An upright, visibly intact, empty glass liquor bottle is on the rear ground-floor kitchen counter.
- The game develops and lifts three prints. After correct comparison, the explicitly revealed identities are John, Peter and Paul. Before comparison they are recovered prints, not confirmed identities.
- Final photography and fixed-reference/evidence-pair measurements precede the required on-scene processing and recording gates.
- The bottle does not establish intoxication, drinking time, who was last present, presence at the attack, or attacker identity.

## Integration hazards found

- Body logging/removal currently requires all three preferred report IDs. This would expose accuracy and block students choosing unsupported statements; the new interaction must accept any valid three.
- Legacy body-wound, knife/wound-consistency and spatter-reconstruction findings depend on selected report IDs. The user explicitly approved keeping these findings tied to recording completion, independently of which three new statements are chosen. The actual selections are retained separately; preferred answers are not inserted into the selection set. Existing authored finding/relationship/conclusion rules and the deduction formula remain unchanged.
- Existing pools contain 3 body, 7 knife, 6 spatter and 6 bottle choices, with different supported/unsupported counts and optional hidden choices. The new assessed pools must always have six choices and must not depend on hidden prerequisites.
- Current uploads preserve `ReportLinesSelected` events in the simplified timeline. Adding one versioned snapshot to that same event avoids six noisy timeline rows and avoids a database migration.
- Student and professor detail pages share `SessionRecord`, allowing one presentation implementation. Historical Recorder events do not have weights; never assign the new version's weights to those records.
