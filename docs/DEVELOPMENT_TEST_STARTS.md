# Start at a procedure for testing

Open `Assets/_Project/Scenes/Scenarios/CSI_Environment.unity`, then **Tools > ViCSI > Scene Test Start**.
Choose a starting point and click **Play selected start**. The choice is local to the current Unity Editor session; a new Editor session defaults to Normal play. It does not change the saved scenario.

Updated 2026-10-20. The menu groups activities separately from their saved enum values, so old selections still identify the same preset. Use the focused photography starts for ruler checks; **Photography done** means the overall scene route is finished, not that individual evidence has been photographed.

| Starting point | Activity left for you |
| --- | --- |
| Normal play | Entire scenario, including IOC |
| IOC done | Full scene photography route; camera equipped but not raised |
| Photography done | Evidence discovery, original photos, chalk/markers, scaled final photos and individual procedures |
| Knife + ruler photo | Knife original photo, chalk, item and trace markers ready; take the final scaled photo |
| Bloodstain + ruler photo | Bloodstain original photo, chalk and marker ready; take the final scaled photo |
| Bottle + ruler photo | Bottle original photo and marker ready; take the final scaled photo |
| All evidence procedures done | Measuring and sketching |
| Measurements done | Empty sketch with four elements and eight distance cards |
| Sketch done | Evidence Relationships Review and final conclusion submission |
| Investigation submitted | Six stored items; pass custody at the box, then inventory review, final survey, IOC report and release |
| Knife bagging | Knife ready to collect, evidence bag already equipped in the left hand |
| Bottle fingerprinting | Final photo and measurements prepared; recover and store the bottle prints |
| Body recording | Final body photo, Marker A and all scene measurements prepared; record findings, then prepare removal |
| All evidence measured | All four items documented and measured, real item/trace/letter tents placed; sampling, fingerprints and reports remain |
| Custody pass | Six sealed stored items, empty evidence box; confirm transfer, inspect bag stack and sample rows, then continue closeout |
| Fingerprint comparison practice | Two stored fingerprint cards; empty hands at the front-yard comparison desk, with no practice attempts completed |

For **Knife + ruler photo**, **Bloodstain + ruler photo**, or **Bottle + ruler photo**, the camera is equipped in the right hand and the subject remains Marked. Approach the selected item, press left X to raise the camera, aim at the item and press right A to place the ruler. Include the whole item and ruler until the frame turns green, then press the trigger. The prompt explains clipping, obstruction, wrong face or insufficient ruler size. A removes the ruler. Placement and the final photograph are deliberately left for you; no samples, reports, measurements or scored answers are completed. Other evidence starts undiscovered. Restart returns the subject to Marked with a held, lowered camera and no ruler.

For **Fingerprint comparison practice**, press A with empty hands at the outside desk. Compare each stored source, switch samples, zoom/rotate the views and retry incorrect choices. B closes. The exercise has no score or retry penalty. Restart restores two available sources and clears practice attempts/completions. Case identification and sealed custody are already prepared, and the custody pass remains unfinished.

For **Custody pass**, the student starts beside the evidence box. Press A to open the padded, scrollable review, then confirm the transfer. Both bags, both blood containers and both fingerprint cards must move together. Opening or cancelling the review does not transfer items. Final inventory confirmation and the final survey remain blocked until the pass succeeds. Restart restores all six items in inventory and clears the box. This preset uses the same isolated development session as the other starts; it does not upload student results.

For **All evidence measured**, the Recorder is equipped and you face the body from the kitchen approach. All fixed-reference and evidence-pair measurements are complete. All four items have their final photographs, chalk where required, and physical markers (including the knife trace and Body Marker A). No item is recorded, collected or removed. Sampling/fingerprints still follow normal requirements. An extra unused numbered tent near the approach can be picked up immediately; successful markers stay until their own item is recorded. Record the body's three findings to check Marker A first, then continue the remaining items. Restart restores the measurements, all unrecorded records and the six physical tents. Student uploads remain disabled.

This is also the start for testing the current blood-sample workflow: recover with a fresh swab, contain it, use the front-yard drying cabinet, explicitly retrieve its paper package, seal and press B to store. Refresh contaminated gloves and get a new swab at the supply station when required; those steps are not bypassed after the initial fixture setup. Fingerprint lifting/storage and report selection remain normal player actions. Use **Normal play** to test arrival PPE, access registration and supply pickup from an untouched session.

For **Body recording**, the Recorder is equipped and the player faces BODY-A from the kitchen approach. Aim at the body and press the trigger, select its three report lines, and confirm. Holster the Recorder, approach with empty hands, then press A and confirm preparation for removal. Photography, chalk, Marker A and the full measurement gate are prepared; body findings and removal remain unfinished. Other items have only the documentation needed for measurements, with no sample recovery, reports, collection or scored answers. Restart restores this unrecorded body exercise.

For the bagging fix, choose **Knife bagging** and click **Play selected start**. You start on the kitchen approach facing the knife. Its photography, marking, blood sampling, fingerprinting and report prerequisites are prepared; it remains loose and grabbable in its original position. The bag is already in your left hand. Grab the knife with right grip, hold the left trigger to open the bag, bring the knife into the visible opening, release the left trigger to seal, then press B to store. Restart selected test restores the loose knife and bag for another attempt. Other evidence remains unprepared, and no scored reasoning answers are supplied.

During a test, click **Restart selected test** in the window. This reloads the whole scene and applies the selection again. You can change to another test preset before restarting. In the headset, open the utility wheel (X), choose **Settings**, then **Restart selected test** and confirm. To return to normal play, stop Play, select Normal play and start again.

The evidence presets start on the clear kitchen approach, outside the bottle discovery radius. They do not find the bottle for you in Photography done; approach it to exercise its real Found trigger. IOC done starts at the first photo station. The normal tracked-head and hand poses are preserved through the SDK teleport path.

The later presets seed completed evidence records, two secured bags, two blood samples, two fingerprint cards, required body handling, report facts and the follow-up statement. Previously eligible reasoning prompts are skipped without inventing scored answers. The sketch remains empty until Sketch done. Investigation submitted uses a fallback result fixture to open real closeout; use Sketch done to test actual conclusion selection and feedback. These presets prepare the results of earlier procedures; they do not prove that the skipped interactions work.

For the measuring preset, original evidence props/measurement anchors remain in their authored positions even though collection is seeded complete. Normal late-measurement rules still apply; late warnings in that preset are expected. This fixture tests the tape and sketch interaction, rather than early-versus-late assessment accuracy. Use Photography done to test measurement order as part of the full evidence procedure.

A gold TEST MODE indicator identifies the active preset. Test mode isolates raw logs, ownership and upload queues in the existing temporary offline sandbox, before the uploader can resume old entries. Student results are not uploaded. Normal play keeps its existing behavior.

For a development headset build, select `_DevelopmentTesting` in the scene and set its `SceneTestStart.startingPoint` in the Inspector before building. Keep Normal Play for ordinary use. Non-development builds always ignore the test selection. The Editor window overrides the Inspector only within the Editor.

Implementation: `Assets/_Project/Scripts/Testing/SceneTestStart.cs`; Editor window: `Assets/_Project/Editor/SceneTestStartWindow.cs`. Integration diagnostic: `SceneTestStartTest`, runnable through the existing `Tools/RunUIComfortChecks.ps1` runner. The diagnostic reloads every preset and checks real bottle discovery, restart, stage state and sandbox isolation.

For **Bottle fingerprinting**, start on the clear kitchen approach facing the counter, with the brush equipped. Final photography and the bottle's fixed-reference measurements are prepared; other evidence remains undiscovered, so no evidence-distance pairs are skipped. Load powder from the jar, brush the bottle's print area, lift with tape, then press B to store the recovered sample. Storage leaves identities pending; compare the stored lift against the correct reference at the comparison desk to identify John, Peter and Paul. Continue with the Recorder (exactly three of six statements), then bag, seal and B to store the bottle. Restart restores the unprocessed print cluster and empty sample inventory.

Body recording and other unrecorded current subjects now use the six-choice Recorder: choose/deselect with B and confirm exactly three with A. Recording completion preserves existing findings regardless of the three selected statements. Documentation accuracy is assessed separately after the session, never during the investigation. Later presets may contain legacy seeded report IDs without a Recorder score; use the actual recording starts to test the new versioned selections.
