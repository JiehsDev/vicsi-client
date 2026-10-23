# Knife bag receiving-zone alignment - 2026-10-17

The kitchen knife could be grabbed but was difficult to insert into the visible evidence bag because its receiving trigger still occupied an obsolete model position. Unity measured the paper bag opening at bag-local (0.070000, -0.023450, -0.089835), outside the detector centered at (0, 0, 0.12) with size (0.35, 0.35, 0.35). Evidence: docs/evidence/knife-bagging/geometry-before.txt.

Moved only the ReceivingZone transform to the current visible opening. Comparison against scene-before.unity.txt confirms one changed position line. Detector dimensions and all procedures/input/custody logic are unchanged.

Extended CollectionFeedbackTest to use synthetic XR left-trigger/right-grip actions and real physics overlap at the visible model opening, including an explicit containment assertion for both knife and bottle. This replaces direct TryInsert calls. Sealing and B storage continue through the suite's existing handlers. This is Editor input/physics verification, not tracked headset/controller simulation.

Final result: 47 PASS / zero failures (docs/evidence/knife-bagging/final.txt). Covers collection prerequisites, physical insertion, held sealed bag, explicit storage, bottle flow and scene reset. The baseline run aimed at the detector center passed, showing why center-based coverage missed the model mismatch. Initial synthetic fixtures failed before gesture execution: bitfield delta input, then boolean delta for float-format controls; corrected to FLT controls with float input. reproduction.txt retains the first failure and baseline.txt contains the corrected pre-fix center-target run.

Final Editor snapshot in final-editor-state.json: CSI scene clean outside Play, not compiling, no compilation failure, pending runner false, XR restore false, corrected detector contains the visual opening after reload. Physical Quest acceptance remains pending. No build/install, real upload, commit, push or deployment.
