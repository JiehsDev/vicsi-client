# Knife bag insertion and sealing

The reported knife stayed visible in the player's hand when brought into the open bag. The bag required the tracked right-controller anchor to be within 15 cm of the evidence. Physical grabbing uses the SDK controller grab point instead. Those positions can differ, so evidence accepted by the grab system could be refused by the bag.

`EvidenceBagTool` now resolves the right controller's `GrabInteractor` and measures from its grab point, using the same `RightHandOnlyFilter.GrabPoint` helper as physical grabbing. The tracked anchor remains a fallback for rigs without a controller grab interactor. Grip input, receiving-zone overlap, the left trigger, reach distance, procedural prerequisites, sealing, and explicit B storage remain required.

`CollectionFeedbackTest` now places the SDK grab point at the evidence while the controller anchor is outside the insertion reach. It also releases the simulated left trigger to exercise automatic sealing instead of calling `TrySeal` directly.

## Validation

- Original fixture: 46 passing checks. It placed the controller anchor directly at the opening and called sealing directly.
- New regression before the fix: 35 passing / 13 failing checks, including knife and bottle insertion and downstream custody checks.
- New regression after the fix: 48 passing / 0 failing checks, including physical receiver overlap, trigger-release sealing, closed-bag representation, B storage, and scene reset.
- Grab reach: 9 passing / 0 failing checks, including distant-item and left-hand refusal.
- Evidence inventory: 29 passing / 0 failing checks, including custody, storage, deposit, and modal guards.
- Final total: 86 passing / 0 failing checks across three suites.
- Unity compiled the updated C# with zero C# errors.

Reports are under `docs/evidence/knife-sealing-2026-10-22/`: `baseline`, `reproduction`, and `final`. Validation uses simulated controller input and SDK grab-point positioning in Unity Play mode; headset confirmation is still needed.
