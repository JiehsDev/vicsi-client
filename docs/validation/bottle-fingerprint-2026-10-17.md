# Bottle on-scene fingerprint procedure — 2026-10-17

The bottle now follows initial photography, chalk, marker, final photography, measurement, on-scene fingerprint recovery, Recorder, collection and sealing. Dusting requires both fixed-reference measurements and the bottle's required distances to discovered evidence. The old bottle fingerprint station is disabled.

The original bottle carries three separate print visuals. Brushing reveals dark powder for contrast against its light surface. Lifting creates a source-linked held sample; pressing B to store it completes processing and automatically identifies John, Peter and Paul. Dusting and lifting alone reveal no identities. Identification is announced once and logged as an unscored event; report choices, findings and upload summaries retain all three identities. The result establishes handling without establishing when each person touched the bottle. The knife retains its separate fingerprint state and powder material.

Recorder accepts exactly three unique lines after sample storage. The bottle must then be inserted into the bag, sealed and separately stored with B. Restart clears the prints, identification and sample custody. The existing **Tools > ViCSI > Scene Test Start > Bottle fingerprinting** preset prepares the final photograph and measurements, equips the brush and leaves recovery to the player. See [the test-start guide](../DEVELOPMENT_TEST_STARTS.md).

## Verification

During this continuation, existing saved reports were recounted rather than rerun: **407 Unity checks and 33 backend checks PASS, zero failures**. The manifest selects one accepted result per suite, including the final powder-contrast reruns; it does not count duplicate runs. Exact paths and counts: [summary.json](../evidence/bottle-on-scene/summary.json).

Earlier attempts remain in `play/`: the bottle suite recorded three measurement-gate failures, and the preset suite recorded one equipment/isolation failure. Subsequent accepted reports pass those checks. Their retained reports are diagnostic history, not accepted results.

The scene preservation audit was rerun successfully: all four checks pass, with three existing records changed, 25 bottle-child records added and no removals. Bottle placement and visual overrides are preserved. [Preservation result](../evidence/bottle-on-scene/scene-preservation.json). The saved [powder preview](../evidence/bottle-on-scene/powdered-bottle-preview.png) was visually inspected.

A live read-only Editor probe confirmed clean saved CSI_Environment outside Play, no compilation failure, no pending runner/request/XR restoration, and both Standalone and Android XR initialization enabled. Probe code was verification only; the implementation and diagnostic reside in real source files.

Core task files include `Assets/_Project/Editor/BottleFingerprintSetup.cs`, `Assets/_Project/Scripts/Testing/BottleFingerprintProcedureTest.cs`, the shared fingerprint/state/inventory/upload code, bottle definition and scene, dialogue catalog, and `Tools/ValidateBottleFingerprintScene.py`.

Physical Quest brush/tape contact, controller timing and visual readability remain unverified. This continuation changed completion documentation and the evidence manifest only; it did not build/install, upload results, commit, push or deploy.
