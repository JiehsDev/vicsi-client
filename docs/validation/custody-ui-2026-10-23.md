# Evidence custody UI — 2026-10-23

Reworked the evidence custody review panel in EvidenceBoxDepositZone. The panel now has a left-aligned heading, an item count and short instruction, a numbered evidence list, and separate gold transfer and blue cancel buttons in the footer. The item list no longer repeats “Sealed and stored” beneath every item. The footer indicates whether scrolling is needed.

Validation failures appear in a separate warning row above the list. The summary stops describing the evidence as ready while a warning is present. Both controller-button actions and pointer targets remain available. Reopening starts the list at the top. The existing custody transaction, live validation, input delay, UI ownership and proximity requirements remain in effect.

The original editable visual group is retained in its existing creation order. New elements use a separate BuildReviewDetails group, with functional layout applied after saved visual settings are restored. No scene-wide visual materialization was required.

The final real-scene CustodyPassTest run passed all 66 checks, including pointer cancel, separate warning placement, button sizing, stationary footer controls, item text overflow, existing custody validation and all six evidence transfers. Script compilation and whitespace checks passed. The rendered panel was visually inspected. Final results and normal/scrolled render captures are stored under docs/evidence/custody-ui-2026-10-23. Physical headset review was not performed.
