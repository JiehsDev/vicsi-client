# Arrival gloves before scene entry

Implements the user's revised first chronological improvement: First Responder briefing, PPE explanation, deliberate glove pickup/wear, then entry and IOC assessment.

## Student flow

The saved arrival and teleport hotspot are outside the visible cordon, facing the responder and a compact Controlled Entry Point sign. The First Responder's existing case information remains, with two short PPE lines inserted before the CSI Form 1 handover. The lines explain hand protection, contamination, clean equipment, changing gloves between unrelated items, avoiding personal contact with used gloves, and the exercise's assumption of other appropriate PPE. No new medical response or scene-disturbance facts are invented.

A small station beside the exterior entrance displays the existing CLEAN_GLOVES model. After briefing, A picks up a pair into a temporary right-hand preview; a separate A press wears it. B returns the pair. Leaving range cancels pickup. Modal UI and spoken dialogue prevent accidental pickup/confirmation, and held A cannot perform both actions.

Entry requires both briefing and worn gloves. Crossing the controlled entrance or protected scene area too early gives the appropriate instruction and returns the capsule and tracking origin to the last exterior position through the SDK's public positioning/reset methods. Tracked head/hand poses and facing are preserved. The check runs after deferred SDK teleports and clears accumulated origin motion so its end-of-frame sync cannot undo the return. This covers smooth movement, teleporting and room-scale head movement. Tape and posts stay closed and visible. IOC observations reject bare-handed attempts; the interior direction guide waits for gloves, and original assessment guidance follows wearing them.

Gloves are session state independent of tools and evidence inventory. Runtime material copies give the existing animated left/right hand meshes a blue glove appearance without replacing tracking or animation. Imported materials and controller/tool/evidence renderers retain their assets. Scene reload restores bare hands, the clean pair, briefing and entry requirements. The existing PPEEquipped event is logged once locally; the deduction rubric and upload/dashboard schemas are unchanged.

## Implementation and preservation

- ArrivalPPEStation owns pickup, confirmation, glove appearance and cleanup. Resources/ArrivalPPEStation.prefab references the existing clean-glove model.
- SceneCordon installs/positions the station outside the entrance and enforces entry; the station is its own scene root so hidden glove meshes cannot affect the cordon visibility check.
- FirstResponderTurnover inserts PPE lines without changing the serialized original four case-line IDs. The dialogue catalog supplies teaching, reminders and confirmation.
- SceneTurnover rejects IOC observation recording before gloves. IOCAssessmentInteraction/Guide defer interior navigation until gloves are worn.
- The validated arrival setup changes only the rig and teleport hotspot's existing local transforms, adds the entrance sign, and adds the entrance approach bounds. No evidence, NPC, photography or measurement geometry is moved.
- Later development test presets explicitly prepare gloves inside the existing isolated test sandbox. Normal Play and release builds start ungloved. The diagnostic runner excludes the arrival suites from this preparation.

The initial diagnostic failure is preserved in `docs/evidence/arrival-ppe/run1`. It exposed the old saved start past the visible tape, the missing near-tape boundary, and warning evaluation happening before movement correction. The validated outside arrival and unified late-frame boundary check corrected those issues. Preset attempts in run2/run3/run4 retain the batch-mode SDK end-of-frame issue: capsule teleport occurred but the headset stayed at arrival, with gloves already equipped and zero entry returns. BatchLocomotionFramePump now invokes the SDK's own end-of-frame sync only in the isolated Editor batch preset diagnostic; the original position assertions are retained. No package-cache code is edited. Subsequent accepted reports and the final manifest identify the final checks.

## Teaching references and limits

The wording follows the safety/contamination purpose of PPE and clean equipment in the [NIJ equipment training](https://nij.ojp.gov/nij-hosted-online-training-courses/collecting-dna-evidence-at-property-crime-scenes/evidence-collection/equipment), alongside the project's existing SOCO audit. It does not certify a complete hazard-specific PPE ensemble.

This step introduces initial glove use. Replacing contaminated gloves during later evidence processing remains a later planned improvement. Physical Quest comfort, hand appearance in each tracking/controller mode, and the boundary return require headset acceptance. No live upload, build/install, commit, push or deployment is performed by this implementation.

## Accepted validation, completed 2026-10-19

340 assertions pass with zero failures across seven suites. All seven Unity processes exit 0, with zero C# compilation errors in their logs:

| Suite | Passed | Accepted report |
|---|---:|---|
| ArrivalPPETest | 40 | run6/ArrivalPPETest.txt |
| ArrivalEntryTest | 26 | run6/ArrivalEntryTest.txt |
| FirstResponderTurnoverTest | 44 | run6/FirstResponderTurnoverTest.txt |
| ScenarioPhaseFoundationTest | 19 | run6/ScenarioPhaseFoundationTest.txt |
| IOCAssessmentGuideTest | 35 | run6/IOCAssessmentGuideTest.txt |
| NPCApproachGuideTest | 33 | run6/NPCApproachGuideTest.txt |
| SceneTestStartTest | 143 | run5/SceneTestStartTest.txt |

Reports are under `docs/evidence/arrival-ppe/`. The final arrival fixtures explicitly bind their simulated camera to the SDK player-eye reference, then restore it. The run5 arrival fixture's one failure retained the original eye reference after replacing Camera.main; correcting the fixture preserves the actual SDK entry rule and its original outside-return assertion.

The new suite exercises actual A/B input, held-button protection, cancellation/range/modal gates, the six-line briefing/history, bare-handed observation rejection, the capsule/head boundary and an actual SDK teleport, both hand materials, once-only PPE logging, actual Recorder equip/holster, and full scene restart. Native Direct3D rendering verifies visible glove pixels; `station-preview.png` was visually inspected after facing its instruction sign toward arrival.

`scene-preservation.json` compares the full saved scene with the pre-arrival snapshot: 15 added sign records, no removals, and exactly four changed existing records (rig transform, teleport hotspot transform, cordon bounds component, and cordon parent's child list). The authoring inspection independently verifies that only the rig and hotspot's local transforms changed, with supported standing clearance outside both entry areas. No other existing serialized object record changed.

The accepted manifest is `docs/evidence/arrival-ppe/summary.json`. Earlier diagnostic attempts are preserved and excluded from the accepted totals.
