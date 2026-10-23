# House entrance photography barrier

Added an invisible, editable doorway wall in `CSI_Environment`, named **House entrance - outdoor photography wall**.

- No additional obstruction during IOC turnover, assessment, assistance decision, SOCO request or handover. Existing briefing, gloves and access-register requirements remain in effect.
- Starting SOCO investigation enables the wall until every required Exterior and Entrance photograph is completed. In the current scene these are `scene-exterior-establishing` and `scene-entrance-approach`.
- Completing those outdoor shots opens entry immediately, while the interior photo route is still unfinished. The existing full-route requirement for evidence processing stays unchanged.
- The existing cordon return mechanism also prevents room-scale/teleport bypass into the house. Early crossing explains that exterior and entrance photographs must be completed.
- Collider position and dimensions are saved in the scene and editable in the Inspector. Initial saved collider state is disabled. The setup menu is **Tools > ViCSI > Scene Setup > Install House Photography Entry Wall**.

Current doorway wall: position approximately `(16.72, 2.40, 6.21)`, size `(1.17, 2.40, 0.18)` metres. It has no renderer or visible mesh. The player locomotion collision mask includes its layer. Outdoor photo stations remain outside the barrier and hot-zone footprint.

Validation: **151 checks passed, zero failures** in isolated Unity Play-mode sessions:

| Suite | Passed |
| --- | ---: |
| HousePhotographyEntryGateTest | 27 |
| IOCAssessmentGuideTest | 35 |
| ArrivalEntryTest | 42 |
| ScenePhotographyProcedureTest | 47 |

The new suite checks real IOC progression, doorway collision, room-scale return, both outdoor shutter completions, immediate unlocking and completion of the remaining interior route. Existing suites cover actual IOC gaze observations, briefing/PPE/check-in access and photography/evidence procedure requirements. Unity compiled with zero C# errors. Headset playtesting was not performed.

Reports, setup geometry and pre-change scene snapshot: `docs/evidence/house-photography-entry-2026-10-22/`. Tests use the existing offline sandbox. No commits, deployment or production data changes.
