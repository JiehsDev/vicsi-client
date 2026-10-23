# Evidence box destination waypoint — 2026-10-20

Added EvidenceBoxWaypoint to the evidence holder through EvidenceBoxDepositZone's runtime initialization. It shows a cyan location pin, “Evidence box”, transfer instruction and horizontal distance when all required evidence is fully processed and securely stored, during SOCOCloseout or EvidenceInventoryReview, before the custody pass completes. Looking away shows the existing stable viewport-edge arrow. The target is above the actual raised holder, so it follows the table placement.

The waypoint hides within the receiving area, where the existing A-button transfer prompt takes over. It also hides during modal UI, dialogue and photo capture, and after the pass completes or evidence becomes incomplete. It reads current state only and takes no input or custody/scoring ownership. A separate world-space canvas avoids the holder's imported nonuniform scale. Runtime guide objects and materials are released with their owning component.

PhotoCaptureListener uses a nested-safe hide/restore scope so the new guide does not enter saved photographs. CustodyPassTest now covers installation, ready/unfinished/completed visibility, near-box takeover, an off-screen arrow, uniform scale, UI/capture suppression and restoration, plus the existing six-item transfer/provenance/restart checks. The native preview is saved beside its report.

The first run caught inherited canvas scaling. The final guide uses an independent world-space root; its compiled assembly was checked before rerunning. The first report is preserved as CustodyPassTest-run1.txt.

No scene asset, evidence numbering, grading or dashboard changes were needed for the waypoint. Physical Quest comfort remains unverified.

Final validation: 62 PASS / 0 FAIL. Scene SHA-256 remains 7EDEE789FE6AEDBF8B724AC0B42ADC43D082FBCC66F147397633EE4CB9C7FB0F. git diff --check passed for touched code.
