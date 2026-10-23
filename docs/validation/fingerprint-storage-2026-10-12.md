# EVD-014 fingerprint storage regression (2026-10-12)

## Root cause

The tape gesture visually swapped the tape to the lifted-print card, but the completion callback did not register a derived evidence item in `EvidenceInventory` or hand it to `HeldItemCoordinator`. The held tape remained a `Tool`; the existing B/Store prompt intentionally only accepts held `Evidence`. The knife state was also being marked fingerprinted/matched during lift, so the flow had no separate, player-confirmed collection step. The prompt itself needed no proximity collider: it reads the held evidence category and current item each frame.

## Repaired transition

`PhotographedFinal` + required blood sample documented + fingerprint revealed + tape lift now sets `fingerprintLifted=true`, removes the scene fingerprint mark, and registers `fingerprint-sample:EVD-014` as a derived inventory sample using the existing tape/backing-card prefab. The coordinator holsters the tape and equips this card as held evidence. Fingerprinting and matching remain incomplete at this stage. The standard right-hand B action stores the card through `ConfirmSampleStorage`; that advances the source knife's fingerprint processing, sends the print through the existing lab result path, and records Paul's match. Only then can RC-03 become eligible. The sample remains linked to EVD-014, while the knife remains in the scene for its report, collection, and sealing steps.

The procedure panel continues to show `fingerprint_process` with the substep “Store the lifted fingerprint (B)” until storage. The held prompt says “Press B to store the lifted fingerprint.” A refused lift leaves the mark and record unchanged and displays guidance. No score value changes; the sample event carries `scored=false`.

## Files changed for this repair

- `Assets/Resources/ViCSIDialogueCatalog.asset`
- `Assets/_Project/Scripts/CaseFile/EvidenceInventory.cs`
- `Assets/_Project/Scripts/CaseFile/EvidenceRecord.cs`
- `Assets/_Project/Scripts/CaseFile/EvidenceStateManager.cs`
- `Assets/_Project/Scripts/Interaction/FingerprintKitTool.cs`
- `Assets/_Project/Scripts/Interaction/FingerprintTapeTool.cs`
- `Assets/_Project/Scripts/RoleSystem/HeldItemCoordinator.cs`
- `Assets/_Project/Scripts/UI/EvidenceInventoryEntryView.cs`
- `Assets/_Project/Scripts/UI/EvidenceProcedureMapResolver.cs`
- `Assets/_Project/Scripts/UI/HeldEvidenceStorePrompt.cs`
- `Assets/_Project/Scripts/Testing/FingerprintStorageTest.cs`
- `Assets/_Project/Scripts/Testing/GreyboxFlowTest.cs`
- `Assets/_Project/Scripts/Testing/EvidenceProcedureMapTest.cs`

## Validation

- `FingerprintStorageTest`: 25 checks passed, including live dust/reveal/lift, blocked lift, the visible prompt, a simulated right-hand B input, explicit sample storage and source association, one non-scored session event, Paul match and RC-03 gating, no score change, knife availability, repeated storage, and reset.
- `EvidenceProcedureMapTest`: all checks passed; the fingerprint substep stays current until storage and then advances to recording.
- `BiologicalSamplingTest`: all checks passed; blood sample custody and knife, spatter, bottle, and body flows remain intact.
- Unity Editor compile state: idle, not compiling; current console query returned zero C# errors.
- No Unity Editor wiring is required; the existing scene tape already references the backing-card prefab and existing B input.
- Not built or run on a Quest headset in this validation pass.

Play Mode reports are in `docs/evidence/fingerprint-storage/`.
