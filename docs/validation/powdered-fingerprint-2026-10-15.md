# Powdered fingerprint model - 2026-10-15

Connected `Assets/_Project/Art/Environments/MODELS/NEW/POWDERED_FINGERPRINT (1).glb` to the knife's existing `Evidence_LatentPrintOnKnifeHandle` marker in the saved CSI scene. The linked `PowderedFingerprintVisual` child uses the supplied mesh, material and fingerprint texture unchanged. Its footprint is 13.40 x 20.00 mm, centred at (-4.20900, 0.12620, 13.36700), facing upward just above the handle's original print surface. Uniform world scaling preserves model proportions. No colliders were added.

The child starts inactive. UV discovery retains the original latent mark; completed powdering activates the supplied model and hides the placeholder renderer. The existing UV visibility lock respects this presentation choice instead of re-enabling the gray box. Tape lifting disables the parent marker and its model together. Fresh scene reload restores the undusted presentation. Unwired markers retain the previous fallback. Contact zones, powder duration, procedural gates, tape/card custody, B storage, source IDs and assessment behavior are preserved.

Persistent wiring helper: `Assets/_Project/Editor/PowderedFingerprintSetup.cs`, menu `ViCSI/Scene Setup/Connect Powdered Fingerprint`. Runtime selection lives in `EvidenceFingerprintMarker`, with integration in `FingerprintKitTool` and `UVRevealable`.

Extended the existing `FingerprintStorageTest` with presentation checks across fresh UV discovery, sustained powdering, repeated UV reveal, failed lift, successful lift and restart. **33 PASS / 0 FAIL**, including existing real powder/tape/B-storage and scoring checks. Exact report: `docs/evidence/powdered-fingerprint/run1/FingerprintStorageTest.txt`.

`docs/evidence/powdered-fingerprint/preview.png` is a temporary bright diagnostic close-up of the actual scene handle and supplied print. It was rendered by a throwaway Editor probe; temporary camera/light and presentation changes were restored afterward. Visual inspection confirms fingerprint ridges, correct placement and absence of the placeholder box. This is not a Quest capture.

Final Editor compilation succeeded with no C# compilation failure; the suite returned to Edit mode with a clean CSI scene, no pending runner or XR restore. No build/install, live upload, commit, push or deployment. Physical Quest appearance remains untested.
