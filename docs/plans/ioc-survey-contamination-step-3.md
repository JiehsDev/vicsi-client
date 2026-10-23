# Step 3 plan: IOC initial survey and contamination precautions

Status: implemented on 2026-10-19. See [validation report](../validation/ioc-survey-contamination-2026-10-19.md). This document retains the approved plan below; the final copy was shortened after native previews exposed panel overflow/countdown overlap. The three teaching points and existing interactions remain, with no scene or UI geometry changes. Physical Quest readability remains pending.

## Current behavior

After briefing, gloves and check-in, the existing IOC guide points to the authored yellow kitchen observation highlight, then the victim and possible evidence. The assessment requires a scene survey, victim observation and at least two of the three possible evidence observations. Gaze, distance, facing and line-of-sight checks belong to IOCAssessmentInteraction; authoritative progress belongs to SceneTurnover. Individual item observations can currently be recorded before the survey; the guide recommends survey first without enforcing that order.

The existing guidance already says to avoid disturbing anything. The knife observation explicitly says to leave it for SOCO. The survey response describes the intact cordon and apparently undisturbed body, but does not explain contamination precautions. Existing processing gates remain locked through the IOC assessment and SOCO request/turnover.

## Proposed student flow

1. Finish the existing briefing, glove exercise and access check-in, then enter through the controlled entrance.
2. Receive concise IOC guidance explaining visual assessment and preservation before approaching the existing kitchen observation highlight. Keep the guide, standing point and existing route.
3. Press A at the survey point under the existing position/facing rules. Hear the survey observations and a short reminder to limit contact and disturbance. This remains the existing survey observation, with no extra acknowledgement or quiz.
4. Observe John and at least two possible evidence items using the existing gaze-and-A interactions. Describe possible relevance without treating observation as forensic examination or collection.
5. Complete the existing checklist, decide to request SOCO, then receive the existing role handover before forensic processing becomes available.

## Proposed teaching copy

Arrival guidance, replacing the current IOC guidance text:

> Your entry is registered. Go through the living area to the rear ground-floor kitchen. Avoid touching surfaces or stepping on possible evidence. Stand on the yellow highlight, face John's body, and press A when Observe appears. This is a visual assessment: leave the body and items in place for SOCO.

Survey response, replacing the current survey observation text:

> The cordon is intact, and John's body appears undisturbed in the kitchen. Keep movement and contact to a minimum so you do not move traces or carry material between areas. Gloves do not prevent every kind of contamination. Leave the body and possible evidence in place for SOCO.

Bottle response, clarifying the existing invitation to take a closer look:

> There is an empty liquor bottle here. It may relate to the reported drinking, but that needs examination. Observe it without handling it and leave it in place for SOCO.

Use natural contractions and final dialogue timings appropriate to the existing presentation. Check that all text fits and stays visible long enough to read. Match the current evidence-display resolver wherever an item name is inserted dynamically.

## Implementation boundaries

- Update the three existing dialogue entries in ViCSIDialogueCatalog.asset. Preserve their IDs and procedural status. Check the catalog resolver and catalog tests for duplicate or embedded fallback text that also needs alignment.
- Keep survey-first as guidance. Preserve the existing allowed observation order and the requirement for the victim plus at least two possible evidence items. An enforced new order would need its own implementation decision.
- Inspect the interaction and authoritative processing gates for early manipulation. Reuse current locks; fix an actual bypass if demonstrated, with a targeted regression. Do not claim these gates simulate physical contamination detection.
- Keep the checklist's existing survey row as the completion indicator. Do not claim a button press proves a hazard assessment or certifies a contamination-free scene.
- Preserve the current emergency/medical-response account. Do not invent a completed hazard inspection, safe walking corridor, environmental clearance or undocumented responder actions.
- Keep the saved scene, observation geometry, cordon, item positions and guide graphics intact. No new arrows, floor markings or movement restrictions are proposed.
- Glove replacement between sources, clean collection equipment, swab preservation and developed-print photography belong to their later chronological processing steps. The current arrival gloves remain the implemented PPE exercise.
- Retain current scoring, observation IDs, phase values, event semantics, access records, upload contract and dashboard behavior. This step adds teaching to the existing procedural interaction.

## Acceptance checks for implementation

Exercise the actual normal sequence from briefing through gloves and registration to IOC observation. Verify missing prerequisites still block observations; survey range/facing and item line-of-sight still apply; repeats produce no duplicate progress. Confirm the guide still recommends the survey first while item-first observation remains valid.

Verify both possible-evidence combinations and assessment completion with exactly two observed items; all three remain optional. Confirm observations alone create no photograph, chalk, marker, sample, fingerprint, inventory, matched result or scored answer. Attempt evidence grab and processing during IOC assessment and SOCO turnover, then verify normal availability after completed SOCO handover.

Run the existing dialogue/catalog, arrival/PPE, entry, access-register, IOC guide, scenario-foundation, narrative-role and development-preset suites as relevant to the actual edits. Add focused diagnostic coverage only for an identified behavioral gap. Use fresh isolated Unity processes and preserve failed attempts. Inspect the actual dialogue rendering and measure reading duration; physical Quest readability remains a separate manual check.

For dialogue-only edits, audit the changed entry IDs and confirm the scene hash stays unchanged. Record actual results in a new validation report; the previous step's 452 passing assertions are historical evidence, not validation of this proposal.

## Source anchors

- Existing audit: docs/research/soco-procedure-audit-2026-10-18.md, finding 5 and scope clarifications. This plan translates that saved research into game behavior and makes no new claim about current manual compliance.
- Dialogue: Assets/Resources/ViCSIDialogueCatalog.asset, ioc-assessment-guidance, ioc-observation-survey and ioc-observation-bottle.
- Observation content/threshold: Assets/_Project/Data/Scenarios/IOC_Assessment_CSI_Environment.asset.
- Interaction validation: Assets/_Project/Scripts/SceneTurnover/IOCAssessmentInteraction.cs.
- Guide ordering: Assets/_Project/Scripts/SceneTurnover/IOCAssessmentGuide.cs.
- Progress/phase guards: Assets/_Project/Scripts/SceneTurnover/SceneTurnover.cs.
- Processing and handling gates: Assets/_Project/Scripts/CaseFile/EvidenceStateManager.cs and Assets/_Project/Scripts/Interaction/EvidenceGrabGate.cs.

This planning continuation changes documentation only. No Unity tests, build, install or live data operations were performed.
