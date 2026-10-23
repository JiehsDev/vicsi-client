# Evidence custody pass

Implemented the approved bulk custody pass at the existing EvidenceHoldingCrate. Nothing is transferred by entering its trigger. During closeout, approach the box, press A, review the actual stored item list, then confirm. B cancels. The review uses the shared flexible-height, padded, scrolling, wall-safe VR panel layout.

The whole reviewed inventory is revalidated before mutation: phase, proximity, modal ownership, required processing, current item set, sealed/stored state, and authored deposit requirements. A changed or invalid item blocks the whole transfer. Confirmation locks custody in the evidence box, removes those entries from selectable inventory, and preserves their definitions, sample-source IDs, storage provenance and box identifier. Deposited bags and samples cannot be equipped again. Body handling stays separate; tools are excluded.

The existing closed crate renderer is hidden at runtime and replaced with an open tray of the same physical size. Actual transferred entries receive render-only copies of their existing bag, sealed blood container or fingerprint-card meshes. Bags stack on the left, blood containers line up on the right, and fingerprint cards occupy a separate rear row. Models are fitted inside reserved cells. No evidence scripts or interactive colliders are cloned. A failed visual preparation leaves inventory unchanged.

Cards rest face-up on a shallow rear shelf. The review has a scale-cancelling anchor so rotating it toward the headset cannot inherit the crate's nonuniform scale. Right-thumbstick scrolling and pointer scroll arrows are available; confirmation has a short opening-press guard. Successful custody events are flushed immediately.

Closeout now requires a completed custody pass before final inventory confirmation, final survey, IOC completion report and scene release. The existing conclusion and feedback paths remain in place. Custody is non-scored Training Analytics; deduction weights remain reasoning 60%, relationships 25%, final conclusion 15%.

Each item logs EvidenceDeposited with prior/new custody, source ID, sample type, destination box, timestamp and transfer ID. CustodyPassCompleted records the reviewed IDs and count. The Unity essential-event envelope and dashboard ingestion allowlist preserve these events when optional raw logs are disabled or capped. Existing session_events payload storage and the dashboard Evidence custody training row are reused; no Supabase schema migration or dashboard grading change is required.

The new Custody pass development preset is appended as enum value 11, preserving values 0–10. It starts beside the empty box with two sealed bags, two blood containers and two fingerprint cards in inventory. Restart recreates the scene, restoring all six stored items and clearing the box. Normal saved scene settings remain unchanged.

No manual Editor wiring is required: the existing EvidenceBoxDepositZone creates its review and contents renderer at runtime. Legacy visual-slot serialized fields and deposit events remain available for compatibility; the one-held-item interaction has been replaced.

## Validation

Accepted **340 Unity checks, 0 failures**:

| Suite | Passed | Accepted report |
| --- | ---: | --- |
| CustodyPassTest | 50 | final/CustodyPassTest.txt |
| SceneTestStartTest, all 12 presets | 143 | final/SceneTestStartTest.txt |
| SettingsPauseMenuTest, restart and back-to-lobby | 35 | final/SettingsPauseMenuTest.txt |
| SOCOCloseoutTest | 50 | run2/SOCOCloseoutTest.txt |
| EvidenceInventoryTest | 29 | run2/EvidenceInventoryTest.txt |
| AssessmentUploadServiceTest | 33 | run2/AssessmentUploadServiceTest.txt |

Dashboard: **44 passed, 0 failed, 0 skipped**, including the actual final Unity raw-disabled upload payload, custody-event deduplication, unchanged deduction grades with custody analytics present, 60/25/15 weights, existing relationship interpretations and vendor contradiction. TypeScript and scoped ESLint passed.

Final Unity 6000.5.6f1: compilationFailed=false, zero error-CS console entries, outside Play, clean CSI scene, no pending diagnostic/XR restoration. Standalone and Android XR startup enabled. Scene Test Start window selects Custody pass locally; the saved component remains NormalPlay. Existing XR/bridge console messages are separate from the C# compilation check.

An initial preset test failure came from its floor check ray hitting the newly teleported player capsule. The corrected diagnostic uses the prepared floor Y and independently verifies box approach XZ against the actual capsule and headset. All 143 preset checks then passed. Box visuals were also inspected after correcting card orientation and raising their shelf.

Results and screenshots are in ../evidence/custody-pass/. The accepted run and final compile proof are recorded in summary.json. Tests use the existing isolated offline assessment sandbox and do not write student sessions to live Supabase. Physical Quest controller/comfort validation and a live Supabase round trip remain manual checks.

The Unity build and dashboard ingestion update should be released together: the essential envelope now carries EvidenceDeposited and CustodyPassCompleted in addition to its previous event types. This implementation only changes the local workspaces.

No commit, push or deployment was performed.
