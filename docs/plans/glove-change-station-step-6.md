# Plan: outdoor glove-change table and waypoint

Status: approved, implemented and desktop validated on 2026-10-19. Accepted 523 checks across nine suites, zero failures; final station/waypoint images and all nine capsule traversal segments verified. Physical Quest acceptance remains pending. [Validation report](../validation/glove-change-station-2026-10-19.md). The following records the approved scope; explicit fresh-swab supply remains separate.

## Intended student experience

One clearly labelled supply table outside the house provides initial gloves and replacement pairs. Expand the existing arrival-glove stand beside the controlled entrance rather than introduce a second glove location. Position the table on the clean/outside side of the cordon, clear of the walking route, First Responder and access-register interaction zones. Verify its final placement in a native scene preview before accepting it.

Use a recognisable disposable-glove dispenser box, a visible clean pair and a separate used-glove disposal receptacle beside the table. Label the station **Clean gloves / Change gloves**. Reuse the existing clean-glove model and hand appearance. The box, table and disposal receptacle are station props, with no evidence identity or inventory slot. Supply is unlimited for this training scenario.

1. Arrival keeps the existing briefing → pick up gloves → wear gloves → check in → entry flow. The new waypoint helps locate the same table after the briefing.
2. A successful biological sampling action marks the worn gloves **Needs change**. This is a deliberate simulation rule; it does not infer contamination from arbitrary hand collisions, aiming, proximity or rejected tool actions.
3. Let the student finish containing, sealing and storing the current sample. Show concise guidance: “Store this sample, then change gloves at the table outside before taking another sample.” Do not require travel while an exposed sample is in hand.
4. Once the sample is safely stored and hands/tools are free, show the glove-table waypoint. The student walks outside through the controlled entrance; existing access visits record the real exit and re-entry.
5. At the table, press A to remove/dispose of used gloves, A to pick up a fresh pair, then a separate A to wear them. Removing gloves alone does not complete the change. Display the relevant action at each step; do not stack all instructions together.
6. Wearing the new pair restores the clean state and blue glove appearance, confirms “Fresh gloves equipped,” and clears the waypoint. The student returns to the evidence through the existing entrance without signing in again.

Initial glove wear retains its two deliberate presses. Replacement adds an explicit disposal step. Held A cannot skip steps. B returns a pending clean pair; cancellation after disposal leaves hands ungloved and the station available. Walking away or opening unrelated modal UI cancels a pending pickup without granting clean-glove credit. Gloves remain independent of the Tool Wheel.

## Waypoint behaviour

- Use the established location-pin/off-screen-arrow style with the label **Clean gloves — outside**. At interaction distance, show the table action instead of a distant-navigation instruction.
- Show after the arrival briefing until initial wear, and after a stored biological sample when a replacement is needed. Hide when gloves are clean, before briefing, during unrelated modal/spoken UI and while completing an exposed sample's containment. A failed attempt to start the next sample should explain the glove requirement and retain the destination.
- Inside the house, provide a short authored ground-arrow route through the actual doorway and controlled entrance to the table. Reuse the vendor-path rendering/route patterns, with scene-specific points. Do not draw a straight route through walls, closed obstructions or the cordon tape.
- Give the required glove destination priority over optional vendor navigation while active. Other guides resume under their existing conditions afterward. Navigation owns no input, evidence discovery, movement or progression.
- Exclude the waypoint and route arrows from saved photographs using the existing nested-safe capture-hide pattern. The physical table remains part of the scene.

## State and integration

Introduce one session-local glove state: **Not worn**, **Clean**, **Needs change**. Keep initial wear/entry authorization separate from replacement history. Contaminated gloves remain physically worn: marking them Needs change must not trigger an entry-boundary return or repeat the arrival briefing/check-in. Deliberately removing gloves at the outside station leaves them Not worn, so entry still requires wearing a pair.

Use one shared state owner for the station, hand materials, prompts, waypoint and sampling gate. Extend/refactor `ArrivalPPEStation` rather than maintain competing arrival/replacement booleans. Keep runtime hand-material copies bounded and restore original materials on teardown.

Both `SwabTool.TrySwab` and `TrySampleRequiredSwab` check for clean worn gloves before accepting a new sample. Only a successful sample changes glove state; its current containment/sealing/storage remains allowed. Changing gloves does not reset a loaded swab, seal a sample, advance evidence, complete measurements or award collection credit.

This first implementation enforces fresh gloves before the next biological sample. It does not add speculative contamination penalties to every tool, add collision-based contamination, or redesign fingerprint/packaging procedures. The existing tool automatically resets its swab visuals/state after containment; an explicit fresh-swab pickup/disposal exercise should be planned separately. A glove change must never be presented as cleaning a used swab.

Keep existing case prerequisites, evidence/source attribution, scoring weights, upload schema and dashboard intact. Append local non-scored glove-use/change events with source/reason where relevant; keep the existing one-time arrival PPE event and existing event ordinals. Scene reload/new session clears replacement state. Development presets prepare explicit clean gloves only within their existing isolated-test path.

## Implementation touchpoints

- `ArrivalPPEStation`, its Resources prefab and `SceneCordon`: supply-table presentation, shared glove state, deliberate replacement interaction and entry compatibility.
- `SwabTool`: clean-glove gates and successful-sampling notification on both sample paths; retain current sample completion.
- A glove-station guide and authored house/entrance route, following `NPCApproachGuide` projection and `VendorPathGuide` route conventions.
- `PhotoCaptureListener`: hide/restore new navigation visuals during saved capture.
- Dialogue catalog and append-only local event types; focused diagnostics plus updates to existing sampling fixtures to perform legitimate changes.

## Acceptance checks

1. Native preview shows a recognisable glove box/pair, separate disposal receptacle and readable sign outside the house. Table/route do not obstruct the entrance, register, NPC approach or capsule movement. Nearby interactions cannot consume the same A press.
2. Original arrival and access-registration order still works. Initial gloves do not satisfy a later replacement automatically; a completed change does not repeat authorization. Real table trips create correct exit/re-entry records without duplicate sign-in.
3. Both successful sample paths mark Needs change once. Rejected sampling, aiming and incidental contact do not. Another sample is refused until fresh gloves are worn, with no photo, evidence, sample or inventory progress.
4. Current-sample containment/sealing/storage remains possible with used gloves. No deadlock requires abandoning an exposed sample. Fresh gloves do not erase or convert a loaded swab.
5. Actual simulated A input verifies separate disposal/pickup/wear presses, held-button protection, B cancellation, range/modal/free-hand guards, repeated visits and reload. Cancelled changes grant no clean state.
6. Waypoint/route appears and retires at the right times, remains useful when looking away or indoors, follows the doorway, yields to modal UI, and stays absent from saved photos. No persistent arrows remain after a successful change.
7. Re-run arrival PPE, entry boundary, access register, biological sampling, photographic scale/capture, guide, development-preset and catalog suites. Preserve failed attempts and inspect native station/waypoint/photo images. Record exact results and changed authored transforms.

Physical Quest acceptance covers controller comfort at the table, legibility, navigation across the entrance and glove appearance in stereo. No implementation or headset validation is claimed by this plan.
