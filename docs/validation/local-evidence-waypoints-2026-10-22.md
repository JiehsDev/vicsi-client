# Local evidence waypoint cleanup ? 2026-10-22

Removed the generic nearest-evidence/body waypoint and the stationary matched-card storage waypoint from ProcedureDestinationGuide. Photography, chalking, measuring, marking and recording at an evidence item no longer receive this extra action pin. Existing interaction prompts and procedure requirements remain in place.

Preserved entry-register and final-survey destinations, existing travel guides for gloves, drying, fingerprint comparison and the evidence box, scene-photography route guidance, and evidence discovery dots.

Updated ProcedureDestinationGuideTest to check all four subjects have no local action waypoint. Register navigation still exercises modal/capture suppression and input ownership. Validation results are recorded alongside the test report under docs/evidence/local-evidence-waypoints-2026-10-22.

Validation: ProcedureDestinationGuideTest passed 18 checks with 0 failures. Unity recompiled successfully with 0 C# errors after the current compilation. Test report: docs/evidence/local-evidence-waypoints-2026-10-22/run1/ProcedureDestinationGuideTest.txt.
