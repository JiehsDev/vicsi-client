# Evidence box waypoint — 2026-10-23

The existing evidence-box destination is installed in CSI_Environment and through EvidenceBoxDepositZone initialization. Updated its instruction to “Go to evidence box” with the existing transfer instruction and horizontal distance. Hidden guides now reset pin projection hysteresis before reappearing, and visibility reporting accounts for an inactive canvas object.

The existing readiness gate activates guidance once all required physical evidence procedures and explicit storage are complete, during investigation or closeout, before the custody transfer finishes. It hides beside the box so the transfer prompt takes over, during dialogue/modal UI and photograph capture, and after transfer. Looking away shows a viewport-edge arrow.

The real-scene CustodyPassTest passed all 62 checks both before and after the update, including ready/incomplete/completed timing, off-screen navigation, near-box prompt takeover, capture/UI suppression and the complete six-item custody transaction. Reports and the updated preview are in docs/evidence/evidence-box-waypoint-2026-10-23. Script compilation and whitespace checks passed. Physical headset validation was not performed.
