# Blood-swab procedure verification — 2026-10-20

Scope: current Unity implementation and Play-mode regression suites; no production code changes. This verifies the game procedure, not laboratory or forensic guidance. Physical headset interaction was not checked.

## Verified game flow

1. Finish scene photography and the source-specific original photograph, chalk/markers, measurements and final photograph. The knife includes its separate trace marker. Knife and bloodstain measurement/photo order follows their authored requirements.
2. Wear clean gloves and load a fresh swab from the outside supply station. Touch the correct blood-trace sampling target and press trigger. One accepted sample consumes one fresh swab and marks gloves used; rejected sampling does not consume it.
3. Touch the sample carrier with the loaded swab and press trigger. Containment records swabbingDone, preserves the source ID, puts its carrier in hand and starts NeedsDrying. It does not seal or store the sample.
4. Visit the protected outside cabinet. Press A to start drying. The game simulates five seconds of drying while the player is nearby and interaction is available. Walking away, menus and pending/spoken dialogue pause progress. The first A cannot also retrieve the package.
5. When dry, press A again to retrieve the swab in its individual source-labeled paper package. Drying and packaging alone do not seal or store it.
6. Seal with trigger, then press B to store. Alternatively, B invokes the same sealing action and stores the prepared package in one action. Neither route can seal or store a wet/unpackaged sample. Storage frees the hand and preserves the package and source.
7. Continue the source procedure: the knife retains its original physical evidence and separate blood sample; fingerprint work follows blood recovery. The bloodstain uses its existing evidence ID as its sample, with no duplicate swabbing. Its evidence report requires the sample to be sealed. Tool switching while an unstored carrier is held is blocked through the normal coordinator.
8. Before a different blood source, dispose of used gloves, pick up/wear clean gloves and load a fresh swab pack. Neither sealing nor glove replacement restores the spent swab by itself. The previous sample must be completed and stored before replacement supplies become available.
9. At closeout, store every required item, open the evidence-box custody review and confirm the transfer. Blood samples must be sealed and explicitly stored. Final custody transfer preserves each sample source and box provenance.

## Source identity and checkpoints

- Knife EVD-014 -> derived blood-sample:EVD-014. The original knife remains separate for fingerprint processing and bagging.
- Bloodstain EVD-015 -> EVD-015 sample, because collectedBySwab is enabled; no second sample/collection is invented.
- SOCOCloseout.SampleComplete requires source match, swabbingDone, swabSampleSealed, correct sample visual, explicit storage and secured inventory/box custody.
- The low-level knife fingerprint prerequisite checks containment/swabbingDone. Normal player tool switching additionally blocks while the carrier requires explicit storage. Those two guards serve different purposes.

## Dashboard gap

EvidenceSwabbed, EvidenceAddedToInventory and EvidenceDeposited already have completion-timeline mappings. SampleDryingCompleted and SamplePackaged are local logged events but absent from the shared procedure definition. EducationalSessionRecord.BuildMinimalUpload therefore omits them from current 1.7.0 uploads. Update both Unity Assets/Resources/ProcedureTimeline_1.json and the dashboard counterpart to show these completions; dashboard-only changes cannot recover old omitted records. Keep these procedure records outside deduction grading.

## Validation

Reports are in docs/evidence/blood-swab-verification-2026-10-20. Each suite ran separately in the existing editor through Phase5PlayModeBatchRunner with Play-mode reloads and offline test fixtures. Sampling tests use real tool methods; SamplePreparationTest also exercises cabinet controller input and actual timed drying, FreshSwabSupplyTest exercises actual B storage and supply input, and CustodyPassTest begins from a prepared six-item custody fixture. This is layered coverage, not one continuous physical-headset playthrough.

Results: 211 checks passed, 0 failed across four suites. Scene SHA-256 remains E441EE08004B03BAF5BE2CE53598E8542C4318BBB45FF690CD29429D78CADD3D.
