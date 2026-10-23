# BODY-A revised documentation and removal flow

## Prerequisites

Previous BODY-A implementation: Initial Photo -> Chalk -> Marker A -> Measurement -> Examine / Record Findings -> Final Photo -> Removal. Recording used a special exception at `Marked`; the final photograph then automatically advanced the body to `Logged`.

Revised BODY-A implementation: Initial Photo -> Chalk -> Marker A -> Final Photo -> Measurement -> Record BODY-A Findings -> Prepare Body for Removal -> existing removal transition -> Prepared for Transport / Removed. The shared evidence status order now handles the two photographs and recording normally. No evidence status ordinals or upload schema changed.

- Initial photography precedes chalk; chalk precedes Marker A.
- The final photograph follows Marker A, without a measurement or report dependency.
- The final photograph precedes fixed body measurements and both directions of any evidence-distance pair involving BODY-A. The authoritative measurement manager refuses early writes, and the tool gives the same body-specific guidance.
- Fixed measurements and discovered evidence distances block BODY-A recording. Existing global discovered-scene measurement prerequisites remain intact.
- Recorder confirmation retains all three existing report lines: location, position, and the sharp-object injury finding. These are recorded facts, with no separate examination action or prerequisite. Their IDs and downstream finding/knife/deduction references are preserved.
- Recording must reach `Logged` and set `bodyFindingsRecorded` before preparation; Marker A and current measurements are rechecked. Closeout still requires `Removed`.

The existing removal adapter, fade, tagging/wrapping/cadaver-bag/medico-legal transport training message, renderer/collider hiding and interrupted-transition cleanup are retained. The procedure map keeps this as its existing combined removal action; transport preparation and removal use the existing body-only milestones. BODY-A is neither swabbed, dragged/lifted, nor placed in an ordinary evidence bag. Its data identity, original-position anchors, notes, report lines and session/deduction references remain alive.

## Removed dependencies and guidance

Removed the `Marked -> Logged` body-report exception, the findings-before-final-photo gate, and automatic final-photo-to-Logged transition. Moved BODY-A's fixed-reference and evidence-distance asset rules from final photography to recording, with blocking enforcement. Removed the map's `Examine / Record Findings` label and separate condition/injury examination instruction. The historical narrative event/line IDs remain compatible, but the visible title is now `Document the Victim` and its text describes final photo, measurement and recording. The objective description includes the revised sequence.

Updated blocked-action text:

- "Place Body Marker A before completing the final body documentation photograph."
- "Complete the final body documentation photograph with Body Marker A in frame." (Recorder attempted at the marker stage.)
- "Complete the final body photograph before recording body measurements."
- "Measure/document the body position and required evidence distances before recording BODY-A findings."
- "Complete the required BODY-A report lines before confirming the recording."
- "Complete the required body documentation, measurement, and recording before preparing BODY-A for removal."

Guidance remains procedural and non-scored. `BodyDocumentationRecorded` is emitted after real `Logged` completion and now explicitly includes `scored=false`; existing preparation/removal event names and envelope handling are unchanged. No deduction scoring code, rules/version, other evidence definitions, or removal scene wiring were changed by this task.

## Files changed for this task

Data and presentation:

- `Assets/_Project/Data/Evidence/BODY-A_Victimsbody.asset`
- `Assets/_Project/Data/Objectives/Objectives_CSI_Environment.asset`
- `Assets/_Project/Data/Narrative/NarrativeEvents_CSI_Environment.asset`
- `Assets/Resources/ViCSIDialogueCatalog.asset`

Runtime:

- `Assets/_Project/Scripts/CaseFile/EvidenceStateManager.cs`
- `Assets/_Project/Scripts/Measurement/MeasurementStateManager.cs`
- `Assets/_Project/Scripts/Measurement/MeasurementStateManager.Pairs.cs`
- `Assets/_Project/Scripts/Interaction/MeasurementTool.cs`
- `Assets/_Project/Scripts/Interaction/RecorderTool.cs`
- `Assets/_Project/Scripts/UI/EvidenceProcedureMapResolver.cs`

Diagnostics (under `Assets/_Project/Scripts/Testing/`):

- `BodyRemovalTest.cs`, `EvidenceProcedureMapTest.cs`, `EvidenceProcedurePanelTest.cs`
- `MeasurementTestUtil.cs`, `MeasurementProcedureTest.cs`, `PerEvidenceMeasurementTest.cs`, `MeasurementToolTest.cs`, `SceneRelationalMeasurementTest.cs`
- `BiologicalSamplingTest.cs`, `ChalkProcedureTest.cs`, `StoryContentTest.cs`, `InconclusivePathTest.cs`
- `Tools/ValidateBodyRemovalWire.mjs` accepts an optional payload path so new validation can target its own export.

This report, validation artifacts and session/task handoff notes also record the continuation. Other existing working-tree edits predate this task.

## Validation

Final accepted results: **596 PASS / 0 FAIL across 14 Unity suites**, plus **33 PASS / 0 FAIL across two backend suites**, for **629 passing checks / zero failures**. Repeated attempts are not added together. Exact accepted report paths and counts are in `docs/evidence/body-a-revised-flow/summary.json`.

| Suite | PASS | FAIL |
| --- | ---: | ---: |
| BodyRemovalTest | 41 | 0 |
| EvidenceProcedureMapTest | 63 | 0 |
| EvidenceProcedurePanelTest | 41 | 0 |
| SOCOCloseoutTest | 48 | 0 |
| MeasurementProcedureTest (Normal) | 36 | 0 |
| PerEvidenceMeasurementTest | 33 | 0 |
| MeasurementToolTest | 37 | 0 |
| SceneRelationalMeasurementTest | 57 | 0 |
| BiologicalSamplingTest | 59 | 0 |
| CollectionFeedbackTest | 43 | 0 |
| FingerprintStorageTest | 25 | 0 |
| FinalDeductionAssessmentTest | 36 | 0 |
| ChalkProcedureTest | 39 | 0 |
| StoryContentTest | 38 | 0 |
| Actual Unity/backend event envelope and score isolation | 4 | 0 |
| Existing backend deduction integration | 29 | 0 |

`MeasurementProcedureTest` uses the actual photography, chalk, marker and measurement tools and resumed evidence lifecycle. It verifies BODY-A's new order and the unchanged Knife, Blood Spatter and Bottle flows, including knife swabbing/on-scene fingerprint matching and bottle lab matching. Wire tests use the refreshed actual Unity export at `docs/evidence/body-a-revised-flow/unity-payload.json`. Adding the body milestones and 100 blocked-removal attempts leaves the complete backend deduction result unchanged, and existing/historical envelope behavior is retained.

| Required validation | Coverage |
| --- | --- |
| 1. BODY-A starts with Initial Photograph | BodyRemovalTest, after the unchanged overall scene photography prerequisite |
| 2. Chalk requires initial photography | BodyRemovalTest direct rejection; ChalkProcedureTest real tool |
| 3. Marker A requires chalk | BodyRemovalTest direct rejection; ChalkProcedureTest real letter marker |
| 4. Final photograph requires Marker A | BodyRemovalTest rejection/guidance; actual-tool procedure run |
| 5. Measurement requires final photograph | BodyRemovalTest fixed references and both pair directions; measurement manager/tool suites |
| 6. Recording depends on measurement, with no separate examination action | BodyRemovalTest recording refusal, existing three-line Recorder confirmation and revised map |
| 7. Preparation waits for required documentation | BodyRemovalTest early refusal, final-photo-only refusal, availability after recording |
| 8. Existing removal transition works | BodyRemovalTest real confirmation, full-black fade, locks, transport message and cleanup |
| 9. Body visual disappears | BodyRemovalTest renderer/collider hiding and restart restoration |
| 10. Evidence data remains | BodyRemovalTest notes, report lines, prop identity, deduction snapshot and session export |
| 11. Closeout requires removal | BodyRemovalTest and SOCOCloseoutTest |
| 12. No body-swabbing requirement | BodyRemovalTest definition/grab checks; BiologicalSamplingTest refusal; final imported-asset snapshot |
| 13. No separate examination step in active UI/maps/guidance/tests | Revised map/panel assertions and source inspection; historical IDs and factual injury report line retained |
| 14. Other three evidence flows unchanged | Actual-tool procedure, map, biological sampling, collection, fingerprint and chalk suites |
| 15. Deduction scoring unchanged | Live before/after removal capture, FinalDeductionAssessmentTest, actual wire isolation and backend integration |
| 16. Unity compiles with zero C# errors | Successful final compiler build and final Editor compilation snapshot |

Initial attempts remain available for audit. They exposed stale map expectations, an older compiled panel fixture, and a fixture that advanced BODY-A after selecting the knife. Body progress correctly triggered the UI's existing focus-on-progress behavior, so subsequent knife-row assertions read body rows. Completing the body prerequisite before knife selection fixed the fixture without changing UI focus behavior. The older StoryContentTest fixture also omitted the existing locked RC-03 fingerprint response required before the vendor question, causing downstream interview/conclusion failures. The fixture now records that authored response through the real checkpoint manager; no vendor, discovery, conclusion or scoring rule was weakened. Its final retry passes all 38 checks. Two diagnostic captions and helper comments were corrected after their assertions passed; no assertions or runtime behavior changed in that wording cleanup.

The `run4` BODY-A startup timed out without executing test assertions after host paging-file exhaustion prevented Unity's external IL postprocessor from launching. No C# source error was reported. The clean scene was unloaded, unused Editor memory released, and the two derived asset-import workers recycled; compiler retry completed. This infrastructure attempt is excluded from accepted assertion counts. Remaining suites completed in smaller groups with memory cleanup between groups.

Final Editor snapshot: Unity 6000.5.6f1, Android target, `compilationFailed=false`, not compiling, outside Play/no Play transition, clean saved CSI_Environment, one evidence manager and one body action, no pending runner or XR restoration, and both Standalone/Android XR initialization enabled. BODY-A's imported definition requires removal and has no swabbing requirement. Snapshot: `docs/evidence/body-a-revised-flow/final-editor-state.json`. Targeted `git diff --check` passes; existing compiler warnings remain.

No manual Editor wiring is required. No commit, push, build/install, live assessment upload or deployment. Physical Quest acceptance of the revised flow remains pending.
