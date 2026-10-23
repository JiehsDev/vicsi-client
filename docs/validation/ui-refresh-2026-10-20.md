# Investigation UI refresh — 2026-10-20

The evidence checklist now explains original photos without a ruler and final ruler photographs with the camera controls. Blood sampling includes containment, drying, explicit paper-package retrieval, sealing and B storage for both sources; sealed bags also require explicit storage before the procedure is shown complete. Completion reads the existing sample, preparation and custody owners. The body retains its separate documentation/removal flow. Drying feedback explains that menus must close for progress to resume.

Photo captures retain subject ID, display name, stage and UTC capture time alongside the existing image list/API. The album shows those labels, an empty state and controller hints; thumbnails and the large view preserve the texture aspect. The large view supports 1×–4× zoom through right-stick up/down, browsing through right-stick left/right, and panning through the left stick. A returns to the gallery; B closes. Gallery paging keeps the selected image visible. Successful scene-route shots now enter the album under their view names; rejected attempts do not create album entries or evidence-photo events.

The camera replaces the baked REC/4K/25FPS artwork with native still-photo corners and a subject/stage caption. Existing framing reasons, ruler controls and readiness feedback remain. Saved evidence pictures exclude that overlay as before.

The broad UI diagnostic now runs in the isolated sandbox, checks the actual anchored Notes canvas and reports completion using the batch runner's PASS marker. Older checklist fixtures were migrated to real sample preparation and storage rather than giving completion credit for recovery/sealing alone. Tests do not change grading or upload contracts.

## Validation evidence

Reports and native previews: `docs/evidence/ui-refresh-2026-10-20/`. Reproduction: `Tools/RunUIRefreshChecks.ps1`, with a new attempt name and the interactive Editor closed. Each suite runs in its own Unity process.

Final result: **331 PASS / 0 FAIL** in seven isolated fresh Unity processes, all exit 0.

| Suite | Accepted attempt | Pass |
| --- | --- | ---: |
| UIWorkflowRefreshTest | run4 | 24 |
| PhotographicScaleTest | run1 | 74 |
| PhotographyShutterOwnershipTest | run1 | 15 |
| EvidenceProcedureMapTest | run3 | 69 |
| EvidenceProcedurePanelTest | run4 | 46 |
| SamplePreparationTest | run4 | 46 |
| UIComfortInputTest | run4 | 57 |

The focused workflow suite exercises actual sample recovery, cabinet actions, progress, retrieval, sealing/storage, source isolation, album fit, raw XR navigation/zoom, actual left-stick panning through its bounds, caption fit and input release. The camera suite includes actual captures for all three subjects and standing-height photography. Ownership checks cover labelled successful scene shots and rejected attempts. The panel suite covers all four evidence checklists, geometry, scrolling, hit targets and locks. The cabinet suite covers both sources and real drying progress. The broad UI suite covers actual A ownership, Meta jump suppression, wall-safe placement, deferred timers and anchored reading panels.

`summary.json` lists the accepted paths and counts. Accepted logs contain no C# compilation errors, null-reference exceptions or missing-reference exceptions. These are Editor checks, not physical headset acceptance.

Inspected the native album gallery and inspection view and the scaled knife viewfinder. The first album preview exposed a clipped caption; final runs reserve more footer space and verify text bounds. `workflow-run1.txt` records an earlier request consumed before the new test assembly was imported; it is not an accepted test. Map run1 retained two incorrect test expectations about the bloodstain's authored measurement/photo order; production ordering was kept derived from its existing definition and those fixture expectations were corrected. Panel run3 retained an old body-name expectation and unrelated presentation interfering with reading-panel setup; its fixture now checks the current display name and isolates unsolicited producers while retaining pointer, placement, scrolling and lock assertions.

Controls: `docs/INVESTIGATION_UI_CONTROLS.md`. Saved scenario SHA256 remains `e441ee08004b03baf5be2ce53598e8542c4318bbb45ff690cd29429d78cadd3d`. No scene geometry, evidence definitions, scoring or upload contracts were changed.

Unity was reopened with Scene Test Start available. Final read-only inspection confirms CSI_Environment in Edit mode, no compilation in progress, no dirty scene or pending suite request, and all three serialized XR startup flags enabled.

The long-running interactive Editor was closed normally before fresh-process verification due to memory pressure; it reported a native shutdown crash and then exited without a forced kill. Subsequent accepted batch results record their own exit codes. Meta/MRUK shutdown, licensing and MCP port diagnostics remain in raw logs and are not evidence of UI test success. Physical Quest stereo readability, comfort and device performance require a headset acceptance run.

## Camera frame correction
The user requested the original camera image frame back. ViewfinderFrameMask was restored to its original artwork-rendering behavior; the replacement corners and header were removed. The existing aspect mask and green confirmation tint remain. The earlier camera-overlay description and preview above document the superseded version.
