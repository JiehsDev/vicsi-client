# Plan: fresh swab supply and used-tool greying

Status: approved, implemented and desktop validated on 2026-10-19. Accepted 604 checks across eleven suites, zero failures; native wheel/supply views inspected. Physical Quest acceptance remains pending. [Validation report](../validation/fresh-swab-supply-2026-10-19.md). The following records the approved scope.

## Student experience

Add packaged sterile-swab supplies to the existing outside glove table, with a clearly labelled dispenser/tray. Keep the used glove bin separate. A blood-bearing swab stays with its sample container; do not teach disposal of the collected sample into the glove bin.

The Swab role stays in its current Tool Wheel position. It begins unavailable for new sampling until a fresh swab is obtained. This adds no entry/check-in prerequisite or extra arrival action. Students can obtain a swab during investigation; a first attempt without one identifies the outside supply table. After a source is sampled, its swab is consumed and cannot become clean through holstering, sealing, storage or glove replacement.

At the table, finish changing gloves first when needed. With clean worn gloves and empty hands, A picks up one packaged swab; a separate A opens/loads it into the existing sampling kit. B returns an unopened pending pack. Held A cannot both acquire and open it. Walking away or opening unrelated modal UI cancels an incomplete pickup without granting a fresh swab. Carry one unused swab at a time; table stock is unlimited for this exercise.

After collecting blood, retain the current tip-to-container and sealing/storage flow. Once the sample is safely stored, the student can use the same trip outside for fresh gloves and a new swab. Neither supply action automatically completes the other.

## Tool Wheel presentation

| Swab condition | Wheel presentation | Permitted action |
| --- | --- | --- |
| No fresh swab / finished used swab | Grey icon and muted wedge; centre explains “Fresh swab required — collect one at the outside table.” | Inspect the reason; releasing selection cannot equip or replenish it. |
| Fresh swab loaded | Normal icon and current description | Equip the existing Swab tool. |
| Blood loaded, not yet contained | Muted used indication; centre says “Sample loaded — finish this sample.” | Retain/re-equip the current kit to finish containment. No new sampling. |
| Sample contained / awaiting sealing or explicit storage | Muted used indication; centre says “Finish sealing and storing the current sample.” | Finish the existing held container through its normal controls; do not create a second kit or sample. |

Greying starts when sampling succeeds, while pending-sample recovery stays available. The fully unavailable state applies after the current sample is stored. This preserves the user's requested used-tool cue without stranding an exposed sample after a holster.

Keep the grey state on hover: it must not turn into the normal bright selectable wedge or perform the normal hover lift. Hover still reveals the reason. Add text alongside colour so the state is understandable without relying on grey alone. Refresh availability on wheel opening and while it is open; validate it again on release.

Reject an unavailable selection before holstering another tool or unequipping held evidence. Empty Hands and other tool roles retain their existing behaviour. Use one shared Swab availability/recovery query for the wheel, coordinator/registry equip paths and sampling action, so direct equip or physical grabs cannot provide a new clean swab.

## State and supply rules

- Track fresh-swab availability separately from `hasSample`, `awaitingSeal`, the source reference and glove cleanliness. Suggested lifecycle: Needs fresh swab → Ready → Sample loaded → Current sample completion → Needs fresh swab.
- Both `TrySwab` and `TrySampleRequiredSwab` require fresh gloves and an unused swab. Only a successful sampling action consumes it; rejected actions, aiming and incidental contact do not.
- Preserve original source identity throughout containment, sealing, storage and recovery. Keep the blood-bearing swab in the completed container. Remove the current automatic return to a clean tip in `TrySeal`; only confirmed acquisition/loading of a new swab restores the clean kit visual.
- Do not replace an exposed sample or load a fresh swab over a pending sample/container. Table interaction waits until the current sample is sealed and explicitly stored.
- Swab acquisition requires clean gloves. Glove replacement does not reload the tool, and obtaining a swab does not clear a glove-change requirement.
- Reuse one navigation destination for the table. Extend its needed-supplies condition/label to cover gloves, swabs or both. Guide there only after current sample storage; prioritize glove replacement, then the fresh swab action. Clear navigation once the required supplies are ready. Keep the existing doorway route and photo-hide scope.
- Session reload clears pending packs and used-tool state, with no carried unused swab. Development presets explicitly prepare the required supplies in the existing isolated sandbox. Keep case sequencing, collection credit, score weights, upload schema and dashboard unchanged; use append-only local non-scored supply events if needed.

## Code touchpoints found during inspection

- `SwabTool`: currently calls `ApplyCleanVisual` in both successful sealing branches. Separate completed-sample cleanup from clean-swab replenishment, retaining pending-sample recovery.
- `ToolWheelController`: `UpdateHighlight` currently dims only `HasTool == false`; `CloseWheel` checks existence before routing equip. Add current availability/reason without treating an exhausted tool as missing.
- `HeldItemCoordinator.ToggleEquipTool` and `PlayerToolRegistry.ToggleEquip`: validate exhaustion before changing what is held. Preserve loaded-kit recovery and ordinary holstering.
- The glove supply table/station and `GloveStationGuide`: packaged-swab presentation, deliberate acquisition, shared destination and action priority. Avoid two nearby station actions consuming one A press.
- Catalogue, local events, focused tests and existing sampling/development fixtures. Reuse current models/UI conventions where possible; a new Tool Wheel role is unnecessary.

## Acceptance

1. First pickup requires clean gloves, a safe completed previous sample and distinct A presses. B, walking away, held A and unrelated modal UI grant no swab.
2. Actual Tool Wheel input shows the grey used slot and readable reason. Selecting it cannot alter another held tool/evidence or grant clean-tool state. Hover and reopen preserve greying. Fresh supply restores normal appearance and equip behaviour.
3. Both sampling paths consume one fresh swab only on success, with unchanged evidence/source attribution. A second source refuses a used swab even through direct action/equip paths.
4. Holstering a blood-loaded kit does not lose the sample or strand it: re-equip, contain, seal and store remain possible. Stored samples retain their swab/model and custody state; the old clean tip does not reappear automatically.
5. Fresh gloves alone leave the used Swab slot grey. A new swab alone does not bypass a glove-change requirement. Both tasks share one outside waypoint and retire it only when ready.
6. Reload and every development preset start with their declared supply state. Re-run sampling, glove change, Tool Wheel/held-item ownership, entry/register, photo capture, guidance, presets and catalogue checks. Inspect native wheel states, station supplies, current-sample recovery and clean saved photos.

Preserve failed attempts and record exact results in a dedicated report. Physical Quest wheel legibility, controller comfort and supply-table reach remain manual acceptance.
