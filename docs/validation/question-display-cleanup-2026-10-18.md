# Reasoning question display cleanup — 2026-10-18

Display terminology now resolves separately from stable checkpoint, response and relationship identities. No internal HYP, RC or REL IDs were renamed, removed or migrated.

## Inventory and classification

The initial targeted search found 66 matching lines across 22 Unity/tool files and 43 matching lines across 15 dashboard files. Full file-and-line inventories are in `docs/evidence/question-display/unity-usages.txt` and `dashboard-usages.txt`. These include:

- HYP-01, HYP-02 and HYP-03 in the authored legacy checkpoint asset and ground-truth JSON; HYP-02 in the current reasoning asset's integratedHypothesisId. These remain internal-only and unchanged.
- Checkpoint trigger queues, prerequisite/ending coordination, event targets, HypothesisSubmitted, assessment DTOs/builders, historical scoring mappings, versioned archived truth, session/history fixtures and test assertions. These continue using their stable IDs. Class/file names, editor menus, inspector tooltips, source comments and developer logs remain technical.
- Dynamic UI paths exposed technical IDs even without literal HYP strings in their source: dashboard question captions, historical answer headings and procedure descriptions. Instructor detailed reasoning showed RC IDs. Relationship question captions showed REL IDs. These are now resolved through presentation helpers; timeline event-debug strings are no longer displayed.
- The old phrases Hypothesis Checkpoint and Submit Hypothesis & Reasoning were primarily present as developer terminology rather than authored visible controls. The shared formatter also translates historical prose containing those phrases. Game panel headings now say Reasoning Checkpoint and final confirmation guidance says Confirm Answer.

## Central mapping and numbering

`Assets/Resources/QuestionDisplayNames_1.json` and dashboard `src/lib/data/questionDisplay.v1.json` mirror the current authored checkpoint order, legacy checkpoint order and authored relationship display names. `QuestionDisplay.cs` and `src/lib/questionDisplay.ts` provide display-only label/text resolution. Existing EvidenceDisplay/evidenceText calls compose this formatter so notes, prompts, notifications and review prose share the same rules. Regression tests compare both copies against the actual assets and registered scoring checkpoint order.

Current questions are Peter (1), initial scene (2), fingerprint (3), knife/wound (4), vendor timeline (5) and bloodstain (6). Unity prefers live current definition order. The HYP-02-hosted current fingerprint panel uses RC-03-FINGERPRINT and therefore displays Reasoning Question 3. A historical HYP-02 without a current definition remains Reasoning Question 2; the three legacy identities are not falsely treated as aliases for all six current questions. Historical raw IDs resolve without changing saved data or inventing question text/weights. Current response IDs can display a question label plus Answer A/B/C/D when response text is unavailable.

## UI and exports

- Game: HypothesisCheckpointUI headings, integrated/current/legacy prompts, options and justification text; confirm guidance; central evidence/notes/procedure text; NotificationUI, STCSNotificationUI and DialogueHistoryUI; CompletionScreenUI conclusion/results pages. Relationship review retains its own readable heading and authored prompt.
- Dashboard: shared SessionRecord for students/instructors, frozen question and answer prose, historical answer records, procedure timeline descriptions and captions; detailed reasoning and relationship panels; inherited legacy activity/training/finding/insight formatting. Existing session lists have no evidence/question-ID columns.
- Instructor CSV: adds checkpointDisplayLabel and relationshipDisplayLabel; targetDisplayLabel also recognizes checkpoint/relationship targets. Original technical columns remain available for integration/debugging.

## Upload, scoring and historical data

No session upload schema, builder, API validation, identifier, scoring rule, prerequisite or Supabase write was changed. Frozen questions/options, selected answers, weights and stored authoritative scores remain intact; all formatting happens when rendering. No production records were rewritten. Local diagnostic harnesses refresh their own fixture outputs only.

One dashboard capture test assumed the older dual-event-envelope upload and nonempty legacy relationship summary. It now verifies that the existing simplified upload carries five locked interpretation events in orderedEvents, preserves each event and response ID/order, and validates the choices against registered definitions. An upload-handler regression also confirms a submitted HYP-03 event retains its original technical target in storage while rendering readably for both roles with the same grade. This changes test expectations only, matching the existing Unity test and builder.

## Validation

Dashboard: 52 tests passed without skips; session/upload: 18 passed; responsive browser: 21 passed across student/instructor list/detail and historical/empty/loading fixtures at three widths. Browser DOM assertions reject HYP, RC and REL IDs as well as the active internal evidence IDs. TypeScript, scoped ESLint and optimized Next build passed. General scoring: 212 passed with seven optional capture checks skipped; all seven optional checks subsequently passed with the supplied Unity/capture paths, including historical scoring-file hashes and client/server relationship score parity.

Unity: 313 assertions passed across seven suites with zero failures and zero C# compilation errors: EvidenceDisplayTest 42, ReasoningCheckpointTest 62, EvidenceRelationshipsReviewTest 41, EducationalSessionRecordTest 70, AssessmentUploadServiceTest 33, ResponsiveOptionLayoutTest 41 and DialogueHistoryTest 24. Accepted report paths and totals are in `docs/evidence/question-display/summary.json`. The first attempt's test expectations were accidentally decoded with a Windows legacy encoding; those expectations were corrected to UTF-8 before rerunning. Failed-attempt logs remain separately preserved. The initial dashboard label test needed HTML apostrophe decoding; no rendering change was necessary. The relationship review test caught an unnecessary heading-casing change; the original uppercase readable review heading was restored before the accepted rerun. A layout assertion still expected the old word submit; it was updated to Confirm Answer while retaining the original interaction/state assertions.

No commit, push, deployment, seed, migration or production-data change. Headset interaction and live hosted/Supabase acceptance are not claimed by local tests.
