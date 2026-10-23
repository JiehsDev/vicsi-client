# First Responder and IOC NPC approach guidance — 2026-10-18

Added the existing arrow/pin guidance style to the First Responder's scene handover and the visible IOC NPC's opening briefing. The new `NPCApproachGuide` is automatically attached by `NPCPresentation` for those two roles only. Vendor presentation is excluded.

The guide uses each actor's existing authored label/prompt anchor. In particular, the IOC arrow targets `NPC_IOC`, rather than the First Responder object that hosts the shared workflow component. No NPC, interaction zone, marker, evidence, scene reference or environment transform was moved.

## Player-facing behavior

| Existing phase | Guidance |
|---|---|
| IOCTurnover, before conversation | Arrow/pin toward First Responder, **Approach**, horizontal distance and **First Responder** |
| Within existing FR interaction range | **Speak with them**, distance, First Responder; existing A/trigger remains authoritative |
| FR conversation | NPC guidance hidden until handover; conversation and Form 1 behavior unchanged |
| IOCAssessment | Existing IOC assessment guide takes over, retaining Investigate here / Survey the scene / Examine this |
| SOCOTurnover, before approaching IOC | Arrow/pin toward visible IOC actor, **Approach**, horizontal distance and **IOC** |
| Inside existing IOC marker radius | Existing briefing panel takes over; NPC overlay hides to keep the panel clear |
| IOC briefing | NPC guidance stays hidden; existing briefing controls/dialogue remain authoritative |
| SOCO investigation and later | NPC guides remain hidden; original photography guide keeps its original behavior |

The IOC actor is deliberately retired after the opening briefing. Existing closeout code handles completion reporting without approaching that actor; this change preserves that convention and adds no late-game NPC navigation.

Both NPC guides reuse the unchanged photography arrow/pin mesh, overlay shader and projection/hysteresis helper. Their own HUDs use the same 1.6 m display distance, text styling and world scale as the existing guide. Labels use two short lines. Distance text updates about 6.7 times per second while projection follows the headset. General dialogue, queued dialogue, modal UI, hidden/disabled actors and disabled interaction controllers suppress the guide. It also hides immediately when the guide itself is disabled.

No input actions, raycasters, colliders, modal/locomotion owners, session events, scoring records or progression rules are added. Merely following, aligning with or refreshing a guide does not start a conversation. Readiness uses `FirstResponderTurnover.PlayerInRange` or the existing workflow interaction radius. The original controllers, marker configuration, dialogue content/sequence and confirmation controls are unchanged.

## Source scope and preservation

- New runtime component: `Assets/_Project/Scripts/SceneTurnover/NPCApproachGuide.cs`, plus metadata.
- `NPCPresentation`: three read-only anchor/controller accessors and a guarded Awake auto-attachment for FR/IOC only. Existing pose, facing, visibility and dialogue response methods are unchanged.
- New focused test: `Assets/_Project/Scripts/Testing/NPCApproachGuideTest.cs`, plus metadata.
- Editor runner: one exclusion keeps this NPC test at the real initial handover phase instead of auto-skipping it.

Task-start source/data hashes preserve 1,341 other existing scoped files. All scene files, original FR/workflow interaction logic, original IOC assessment and photography guides, NPC/evidence/marker transforms, menu/Tool Wheels, shared VR UI/input code, scoring, uploads and scenario data remain unchanged. No dashboard or production records were modified. Evidence: `docs/evidence/npc-approach-guide/preservation.json`.

## Validation

`NPCApproachGuideTest`: **33 pass / 0 fail**. This includes one automatic guide per eligible NPC, vendor exclusion, actual role anchors, distance/arrow/pin projection, no guide-generated session events, modal/dialogue suppression and restoration, disabled-controller/guide behavior, no hit targets/colliders, readable two-line text, native shader/mesh, real XR A outside and inside FR range, full FR conversation/handover, IOC range enforcement, panel handoff, real XR A briefing, full briefing completion, actor retirement, original photography handoff, preserved transforms/evidence states and once-only Form 1/Form 2/investigation events.

Existing foundation, IOC assessment-guide and photography-guide suites are run separately; accepted counts and compiler results are recorded in `docs/evidence/npc-approach-guide/summary.json` after completion. Physical headset visual acceptance remains manual.

No commit, push, deployment or production-data write was performed.

## Accepted final results

- NPCApproachGuideTest: **33 pass / 0 fail**, exit 0.
- ScenarioPhaseFoundationTest: **19 pass / 0 fail**, exit 0; includes original approach/briefing flow and scene restart.
- IOCAssessmentGuideTest: **35 pass / 0 fail**, exit 0; assessment guidance and observation behavior remain intact.
- PhotographyGuideTest: **26 pass / 0 fail**, exit 0; original photo route, projection and actual album-capture exclusion remain intact.
- Total: **113 pass / 0 fail**, **zero C# compile errors**.
- Final source/data hashes preserve 1,341 other existing scoped files.
- ADB reports no connected device; headset optical/readability acceptance remains pending.

During Play mode, the canvases appear under `NPC_FirstResponder/NPC approach guide` and `NPC_IOC/NPC approach guide`. Only the phase-appropriate guide is visible. No commit, push, deployment or production-data write.
