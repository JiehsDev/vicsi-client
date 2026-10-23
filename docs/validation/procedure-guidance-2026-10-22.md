# Procedure flow, destinations and mandatory follow-up

## 1. Current implementation audit

The baseline is the current working tree and `docs/FEATURES_SINCE_LAST_COMMIT.md`. Existing changes are retained.

| Stage | Required action / destination | Existing owner |
| --- | --- | --- |
| Arrival | First Responder briefing / Form 1 | FirstResponderTurnover, NPCApproachGuide |
| IOC access | Clean gloves, sign register, enter secured house | ArrivalPPEStation, SceneAccessRegister, SceneCordon |
| IOC assessment | Kitchen observation point, victim and possible evidence observations | IOCAssessmentInteraction, IOCAssessmentGuide |
| SOCO request | Decision, arrival fade, approach IOC for Form 2 briefing | SOCOWorkflowInteraction, NPCApproachGuide |
| Scene photographs | Exterior, entrance, then indoor route | ScenePhotographyManager, ScenePhotographyGuide, HousePhotographyEntryGate |
| Evidence documentation | Body A / knife / spatter / bottle: markers, photographs, measurements and three Recorder statements as required by each definition | EvidenceStateManager, ProceduralGateValidator, evidence tools |
| Blood collection | Knife blood and spatter: fresh swab, collect, drying cabinet, dry, verify source and package, seal and explicitly store | SwabTool, SamplePreparationStation, EvidenceInventory |
| Glove replacement | Finish current sample first; dispose used pair, pick up and wear a fresh pair | ArrivalPPEStation, GloveStationGuide |
| Fingerprints | Knife / bottle: dust, lift to backing card; baseline stores first, then compares | FingerprintKitTool, FingerprintTapeTool, EvidenceInventory, FingerprintComparisonStation |
| Physical completion | Store individually packaged knife / bottle and blood / fingerprint samples; prepare documented body for removal | EvidenceInventory, BodyRemovalInteraction, SOCOCloseout |
| Custody pass | Review and transfer every stored item to evidence box | EvidenceBoxDepositZone, EvidenceBoxWaypoint |
| Session closeout | Inventory confirmation, final scene survey, IOC report, release | SOCOCloseout, SceneTurnover |

### Confirmed gaps / dependencies

- Destination guides exist separately for NPCs, IOC observation, scene photos, glove/preparation/comparison tables, vendor and evidence box. Register, next unfinished evidence and final survey lack a shared continuation cue.
- Dirty-glove protection currently prevents another swab, but is not a general restriction on unrelated investigation actions. Glove guidance is intentionally delayed until the current sample is stored; preserving a route to finish that sample is essential.
- Fingerprint storage currently calls `MarkFingerprinted`, which makes identification pending. Comparison requires `IsExplicitlyStoredSample` and secured inventory custody, and requires empty hands. Blocking storage without changing comparison would deadlock.
- Approved new fingerprint order: lift/secure backing card, compare while the card is still held, confirm match, explicitly store. Other procedures must wait; wrong comparisons cannot identify or store the card.
- Evidence-box prompt and waypoint currently require SOCOCloseout / EvidenceInventoryReview. SOCOCloseout itself requires accepted conclusion feedback. Physical completion alone therefore cannot activate the box. Missing feedback is also generic for some unfinished steps.
- Drying instructions are displayed permanently even with no applicable blood evidence. Fingerprint idle labels return whenever the examination panel closes; eligible stored samples include already-completed comparisons.
- Secured custody already includes both inventory and evidence-box custody, so early physical custody transfer can coexist with the later existing closeout phases without changing deduction scoring.

## Implementation sequence

1. Audit completed above.
2. Fill missing required-destination guidance and prioritize mandatory follow-up.
3. Enforce glove replacement at action entry points without blocking completion of the current sample, navigation, settings or cleanup.
4. Move fingerprint comparison before storage and enforce its continuation gate.
5. Repair custody-transfer readiness and explain exact remaining work.
6. Hide station action UI until its prerequisites and proximity are satisfied.
7. Run meaningful regression checks and a complete custody workflow; preserve editable scene objects and existing assessment calculations.

Validation results will be appended after implementation.

## Implemented behavior (steps 2?6)

- A scene-authored continuation waypoint fills the register, unfinished body/evidence and final-survey gaps. Existing FR/IOC/photo/table/vendor/box guides remain responsible for their own destinations. Mandatory current-sample work and glove changes take priority; a held fingerprint card takes priority over an optional requested fresh-swab pickup. The new guide resolves tasks at 5 Hz while following the view each frame and is hidden during photographic capture and modal UI.
- `ProcedureInterlock` reads live glove/sample custody. It blocks unrelated forensic actions, tool selection, measurement commits, evidence handling, interviews and deduction submissions. It does not own a modal UI or locomotion lock. The current blood sample can still be contained, dried, packaged, sealed with trigger or B, and stored; disposal alone does not clear the fresh-glove requirement.
- Fingerprint comparison accepts the secured held card. Opening comparison records completed recovery/processing without identifying a person. Wrong references cannot match or store; a correct match is required before explicit B storage. Cancellation retains the held card and mandatory follow-up. Already-stored unmatched legacy/development cards remain examinable, while completed comparisons no longer advertise an action. The comparison preset now starts with one held unmatched knife card.
- Evidence transfer is available during SOCO investigation as soon as all physical procedures and explicit storage are complete. It preserves the six actual custody items and leaves conclusion feedback, 60/25/15 scoring, final review and scene-release authorization unchanged. Missing feedback names the remaining procedure. Glove, swab-supply, drying and comparison stations also remain usable during SOCOCloseout; previously the latter stations only accepted SOCOInvestigation and could strand unfinished work.
- Drying instructions/progress appear only for an applicable carried sample at the cabinet, with clear UI ownership. The A action is unavailable while drying is in progress. Matching instructions appear only when comparison is eligible nearby and disappear once completed. Physical tables and permanent signs remain editable.

## Validation progress

- Step 3 accepted rerun: GloveChangeStationTest, 59 passed.
- Step 4: FingerprintStorageTest, 39 passed; BottleFingerprintProcedureTest, 43 passed.
- Step 5: EarlyCustodyPassTest, 9 passed; CustodyPassTest, 62 passed.
- Step 6: FingerprintStorageTest, 41 passed; SamplePreparationTest accepted rerun passed, including actual trigger sealing, hidden premature/completed drying UI and fresh-glove requirements before re-equipping stored packages.
- Initial failed reports are retained. Glove fixture updates remove assumptions about the former cordon position and previously permitted dirty-glove tool selection. The initial drying visibility implementation incorrectly used `Graphic.canvas` after disabling its Canvas; it now caches the actual Canvas component. The accepted drying rerun exercises this correction.
- Final cross-flow regression results will be recorded in the evidence summary after the running suites finish.

## Final validation

821 checks passed across 14 accepted suites, with zero failures. `docs/evidence/procedure-guidance-2026-10-22/summary.json` lists the exact accepted reports.

The extended BiologicalSamplingTest uses the actual documentation, sampling, preparation, fingerprint comparison and custody APIs, then completes actual body-removal confirmation and transfers all six real packages to the evidence box. The test confirms no unfinished physical job remains. SceneTestStartTest passes 208 checks across all 16 presets and confirms real owner records and assessment queues remain unchanged.

Final waypoint tests pass for registration and all four active evidence/body destinations, modal/capture suppression and read-only assessment behavior. Final survey coverage is in SOCOCloseoutTest. The standalone body-removal check now waits for the authoritative removal event at full black before inspecting renderer/custody/history state; no body-removal gameplay code was changed for that fixture correction. The new waypoint observer is enabled so Unity exposes it as Camera.main.

Comparison catalogue and saved monitor instructions now describe the held-card sequence and matching before storage; the obsolete empty-hands instruction is removed. FingerprintComparisonTest also verifies casework wording and text overflow.

Unity C# compilation completed with zero errors. Unrelated RectTransform auto-layout changes were restored from the pre-task scene snapshot. New guide objects/material and the updated monitor instructions remain saved and editable. A physical-headset walkthrough remains manual. No commit, push, deployment or production-data changes were made.
