# ViCSI Phase 2 — contextual procedural training

Implemented locally, on top of the completed Phase 1 changes. No commit, push, deployment, full regression, Phase 3 checkpoint, scoring-version change, deduction-board change or dashboard redesign.

1. **Existing guidance architecture.** `ViCSIDialogueCatalog` owns authored text and timing. `GameDialogue` resolves and routes it. `STCSNotificationUI` presents upper-panel instructor/team dialogue with priority queuing, skip support and Dialogue History. `NotificationUI` presents lower-panel latest-wins tooltips and persistent action prompts. Existing procedure-map, objective, tool and closeout feedback already use these channels. Dialogue is ordinarily nonmodal; separate panels own `PlayerUIGate`/tool locks.

2. **Architecture used.** Reused `Assets/Resources/ViCSIDialogueCatalog.asset`: nine new stable `TRAIN-*` entries and two expanded existing closeout entries. The new scene-local `ProceduralTrainingGuidance` adapter subscribes to existing success/phase events. It bootstraps only where evidence and instructor UI managers exist. Content stays in the catalog; the adapter does not mutate evidence, objectives, assessment, requirements or uploaded data. `SwabTool` adds a successful-containment event after the real held container exists, rather than teaching the lesson on swab contact or before a container creation failure.

3. **SOCO team role.** `TRAIN-SOCO-TEAM` is scheduled on `SOCOInvestigation` entry. It explicitly distinguishes one student's practice of several activities from responsibilities divided among trained team members in actual operations, including photography, recording, collection and specialized examination.

4. **Photography.** `TRAIN-PHOTOGRAPHY` is scheduled only after the required overall route is complete, not after each shutter release. It explains preservation of original appearance, position and condition, the photographer's function and as many photographs as reasonably necessary. Route points are explicitly identified as simulation practice rather than a universal photo count.

5. **Markers.** `TRAIN-MARKER` is scheduled at the first successful `Marked` transition. It connects the same item across photos, notes and evidence records and explains that a marker does not replace documentation.

6. **Complex trace/source.** `TRAIN-TRACE-SOURCE` is scheduled when the authored trace-bearing item is first marked, introducing the additional trace documentation before the separate trace tent/final photo. It distinguishes the knife object from sampled material and teaches provenance. The additional marker is framed as part of this exercise, without claiming a universal two-marker rule.

7. **Measurement.** `TRAIN-MEASUREMENT` waits for the first completed fixed-reference measurement requirement. Partial measurements and individual tape clicks do not trigger it. It teaches original location, spatial relationships and the measurement/sketch team's contribution to reconstruction.

8. **Fingerprint.** `TRAIN-FINGERPRINT` observes the existing successful fingerprint-completion event after development/lifting. It explains careful development and preservation by fingerprint personnel, followed by the general caution that a recovered print can establish contact but alone does not explain when or why. No suspect names, guilt assessment or answer recommendation is added.

9. **Biological sampling.** `TRAIN-BIOLOGICAL-SAMPLE` follows successful source-specific containment. It explains source identity, separate distinguishable sources, containment and collection versus interpretation. Knife and spatter samples share this one concept; their separate sample identities and required procedures are preserved.

10. **Packaging/sealing.** `TRAIN-PACKAGING` follows the first successful physical-item seal, excluding swab-collected items and examination in place. It explains that picking an item up does not complete collection, and that packaging, securing and identification protect condition and identity.

11. **Custody/storage.** `TRAIN-CUSTODY` requires a real `Held -> Inventory` transition with explicit sample/bag storage confirmation. Ordinary admission or unconfirmed storage cannot trigger it. It explains recording who handled evidence and where it went, while expressly stating that the simulation's storage button does not represent every real chain-of-custody step.

12. **Evidence inventory.** Reused `closeout-start` immediately at `SOCOCloseout` entry, before the existing inventory-review interaction. It explains documented/accounted-for items and samples and prevention of loss or separation from records. This preserves the old phase-entry dialogue timing instead of allowing the modal inventory review to bypass a deferred explanation.

13. **Final survey.** Reused `closeout-survey-guidance` immediately on `FinalSceneSurvey` entry. It teaches final documentation/oversight checks before reporting to the IOC. Survey area, orientation, acknowledgement and scene-release gates are unchanged.

14. **Blocked guidance.** Updated 23 existing catalog messages covering overall photography before alterations/sampling/collection, initial/final item photos, markers, measurement/spatial records, Recorder documentation, fingerprint development/lifting, trace source, sealing/storage and inventory accountability. Only authored text changed: no enforcement, sequence or blocked-attempt logging changed. Existing concise B action hints remain concise.

15. **Procedural score wording.** No active procedural point-award/penalty wording was found in the inspected catalog and procedural presentation paths; Phase 1 had already separated this. New and revised explanations contain no procedural numerical awards or penalties. Assessment wording is untouched.

16. **Deduplication.** Stable IDs use a session-local set before queuing. Repeated photographs, markings, successful fingerprint procedures, samples and custody transitions cannot add copies. Ordinary lessons wait for upper dialogue, menus, timed tooltips and persistent action prompts to clear. An incoming action hint/menu hides and pauses a displayed lesson. Higher-priority blocked-action dialogue can interrupt it even if it paused immediately after starting; the lesson remains in history rather than replaying for each item. The reused closeout phase lessons retain the original upper-channel timing and may accompany a short stage affordance.

17. **Dialogue History.** The existing history includes professor/system/team guidance, so instructor training belongs there. Lessons use category `Training`, speaker `Professor`, and their stable ID as source. They are recorded once on presentation, not once per triggering action. No new history UI, persistence or upload schema was created.

18. **Reset.** Fresh scene/session loading recreates the adapter, subscriptions, pending queue and deduplication set. A logger-session-ID change also clears its local set/queue. Existing real sampling reset diagnostics check that prior photography/sample concepts are cleared in the new scene.

19. **Answer leakage.** Explanations teach general preservation, provenance, spatial documentation and limits of contact evidence. They introduce no case-specific suspect, chosen relationship, conclusion, answer key or future checkpoint. Existing narrative content and deduction requirements remain unchanged.

20. **Focused diagnostics changed.** Added `ProceduralTrainingGuidanceTest` for authoring, nonmodal presentation, history, once-only scheduling, action-hint/menu deferral, pause/resume and blocked-feedback priority. Extended `BiologicalSamplingTest` with real milestone, containment-versus-contact, B storage, fingerprint/packaging, second-sample deduplication and reset checks. Extended `SOCOCloseoutTest` with stage-entry concept checks. Isolated `DialogueCatalogTest`'s temporary miniature catalog from the real milestone adapter. Updated expected warning strings in `ScenePhotographyProcedureTest`, `MeasurementProcedureTest` and `SceneRelationalMeasurementTest`; their gate assertions were not relaxed. Added `Tools/ValidateProceduralTraining.py` for content and protected-file hashing.

21. **Exact checks and counts.** See `docs/evidence/assessment-phase2/` for individual reports. Final run counts and commands are recorded below after execution. The full regression suite was not run. The static audit compares 195 existing assessment/deduction/procedure-data/backend-scoring files against their pre-Phase-2 hashes, including historical scoring definitions. Its initial broad scan incorrectly treated photographic “route points” as grading language; the audit was corrected to detect numerical score language and passed on rerun.

22. **Unity compilation.** Final Editor compilation state and C# console-error check are recorded below. Deprecated Unity APIs/native headset warnings are distinct from C# compile errors. The checks are offline Editor diagnostics; headset readability/ergonomics is not asserted from them.

23. **Files changed in this phase.**

    - `Assets/Resources/ViCSIDialogueCatalog.asset`
    - `Assets/_Project/Scripts/Dialogue/ProceduralTrainingGuidance.cs` and `.meta`
    - `Assets/_Project/Scripts/Interaction/SwabTool.cs`
    - `Assets/_Project/Scripts/STCS/STCSNotificationUI.cs`
    - `Assets/_Project/Scripts/SceneTurnover/SOCOCloseout.cs`
    - `Assets/_Project/Scripts/Testing/ProceduralTrainingGuidanceTest.cs` and `.meta`
    - `Assets/_Project/Scripts/Testing/BiologicalSamplingTest.cs`
    - `Assets/_Project/Scripts/Testing/SOCOCloseoutTest.cs`
    - `Assets/_Project/Scripts/Testing/DialogueCatalogTest.cs`
    - `Assets/_Project/Scripts/Testing/ScenePhotographyProcedureTest.cs` (warning strings only beyond its existing Phase 1 changes)
    - `Assets/_Project/Scripts/Testing/MeasurementProcedureTest.cs`
    - `Assets/_Project/Scripts/Testing/SceneRelationalMeasurementTest.cs`
    - `Tools/ValidateProceduralTraining.py`
    - This report and `docs/evidence/assessment-phase2/` validation artifacts.

    Existing Phase 1 and unrelated lobby changes remain uncommitted and were preserved.

24. **Phase 3 handoff.** Reuse the existing catalog/channels and stable training IDs; keep contextual reasoning checkpoints separate from the training adapter's deduplication state. Coordinate checkpoint modals with the existing UI gate and lesson pause behavior. Do not treat viewing/skipping a lesson as assessment. Scoring remains **1.6.0**, procedure scores remain zero, upload/historical schemas remain unchanged, and no new checkpoint has been implemented.

25. **Expert validation.** General documentation, team assignment, packaging/custody and final-survey principles were cross-checked against [NIJ's crime-scene training reference](https://www.ojp.gov/pdffiles1/nij/200160.pdf). These are general training principles, not certification of local SOCO practice. Have the project's forensic/SOCO experts validate local personnel terminology, the knife's game-specific second tent, the authored fingerprint/blood processing order, and sample container/packaging practices. [NIJ also specifies that actual practice must follow agency policies and applicable local requirements](https://nij.ojp.gov/topics/articles/crime-scene-investigation-guides-law-enforcement). No authored requirements were changed pending that validation.


Final executions:

| Focused check | Passed | Failed |
|---|---:|---:|
| ProceduralTrainingGuidanceTest | 20 | 0 |
| BiologicalSamplingTest | 59 | 0 |
| SOCOCloseoutTest | 47 | 0 |
| DialogueCatalogTest | 46 | 0 |
| ValidateProceduralTraining.py | 26 | 0 |
| **Final total** | **198** | **0** |

Each Unity diagnostic was requested separately through the existing `Phase5PlayModeBatchRunner`, using the following request shape with the four suite names above:

```powershell
@{suite='ProceduralTrainingGuidanceTest';output='docs/evidence/assessment-phase2/ProceduralTrainingGuidanceTest.txt'} |
    ConvertTo-Json | Set-Content -LiteralPath Tools/phase5-file-request.json -Encoding UTF8
```

Other exact checks:

```powershell
python Tools/ValidateProceduralTraining.py
git diff --check -- Assets/Resources/ViCSIDialogueCatalog.asset Assets/_Project/Scripts/Dialogue/ProceduralTrainingGuidance.cs Assets/_Project/Scripts/Interaction/SwabTool.cs Assets/_Project/Scripts/STCS/STCSNotificationUI.cs Assets/_Project/Scripts/SceneTurnover/SOCOCloseout.cs Assets/_Project/Scripts/Testing/BiologicalSamplingTest.cs Assets/_Project/Scripts/Testing/DialogueCatalogTest.cs Assets/_Project/Scripts/Testing/SOCOCloseoutTest.cs Assets/_Project/Scripts/Testing/ProceduralTrainingGuidanceTest.cs
```

Whitespace check: exit 0. Static audit: 26 pass / 0 fail, 195 protected files unchanged. `Logs/Editor.log` scan: zero `error CSxxxx` compiler errors. Final Editor-state evidence is saved as `final-editor-state.json`; it verifies compilation succeeded, scoring remains 1.6.0, and the original Main scene is restored clean outside Play mode.

The initial catalog diagnostic had 45 pass / 1 fail because the test restored the live training adapter before its final witness-display assertion. The fixture now remains isolated until the entire diagnostic finishes; the rerun passes. The initial training diagnostic had 19 pass / 0 fail; it was rerun with the additional immediate-pause priority check, for 20 pass / 0 fail. Initial reports are preserved separately. No gameplay, scoring or story content was changed to resolve that diagnostic interference.

The photography/measurement diagnostics with updated expected warning strings were not separately run in Phase 2. Actual photography, marking, measurement, sampling, fingerprinting, packaging and storage integration was exercised by `BiologicalSamplingTest`; closeout gates by `SOCOCloseoutTest`. No full regression or player build was run.

