# House inner cordon and yard outer cordon — 2026-10-20

The current saved layout follows the user's whole-house scenario: the physical house is the hot zone, and the yard is the warm zone. This supersedes the earlier yard-wide inner boundary and the intermediate kitchen-only placement.

The inner tape attaches directly to the front doorway jambs at world Z=6.1867, X=16.2568–17.1868, at .65/.95 m above the 1.2 m porch floor. A compact sign reads `Inner Cordon / House`. The alternate ground-floor side door is marked with two seals. Interior kitchen tape is removed; existing structural doors and windows remain in place.

The access-control volume covers both storeys, the living/dining/kitchen areas and the projecting front wing. Separate authored bounds follow the stepped frontage so the porch and service yard remain outside. Briefing, gloves and deliberate check-in are still required. The access register now has an explicit exterior anchor, keeping its pen-and-paper table with the First Responder when the inner tape moves.

The separate `Outer Cordon - Yard` root places tape at the property gate, Z=.15, joining the front fence ends and leaving the 1.4 m central passage for the authorized player. The existing property fences form its remaining perimeter. Its sign reads `Outer Cordon / Police Only`. The evidence-box table, preparation/supply/comparison stations and register are within this yard staging area. This outer visual does not create a second inner-zone check-in or duplicate visit records; the existing player roles are authorized investigators.

Persistent authoring: `Tools > ViCSI > Scene Setup > Place House Inner And Yard Outer Cordons` in `CordonFenceLayoutSetup.cs`. It validates the saved scene, preserves a before-scene backup, records Undo, updates visuals/bounds and saves. The original helper class name is retained for compatibility; its old fence-spanning-inner and kitchen menu actions are replaced.

Tests were updated for the current layout. Arrival uses the camera's actual horizontal field of view rather than the old fixed 30-degree co-framing assumption. Entry logging permits proximity discovery (`Found`) while still forbidding evidence processing: crossing a real interior can legitimately reveal evidence without photographing, marking or collecting it. Original intermediate reports remain in `docs/evidence/kitchen-door-cordon-2026-10-20/`.

Native previews: [front door](../evidence/house-inner-yard-outer-cordons-2026-10-20/front-door.png), [zone overview](../evidence/house-inner-yard-outer-cordons-2026-10-20/zone-overview.png). Both were inspected in Unity renders. Headset traversal remains manual.

Saved scene SHA256: `F3D1753960C82B0B74FD5B431266A19F79A4D4CC1C253D84B686EEA5644CB6A6`.

Accepted validation: **90 PASS / 0 FAIL** — [ArrivalEntryTest: 42](../evidence/house-inner-yard-outer-cordons-2026-10-20/ArrivalEntryTest.txt), [SceneAccessRegisterTest: 48](../evidence/house-inner-yard-outer-cordons-2026-10-20/SceneAccessRegisterTest.txt). These cover the door tape, side seals, separate outer cordon, both storeys/front wing, porch/yard exclusion, exterior staging, denied SDK crossings, briefing/gloves/check-in, visit timing/jitter, persistence and restart. Code diff whitespace checks pass; the Unity catalog retains its existing serializer-style blank-field trailing spaces. Console reports existing evidence-number serialization and Meta bridge port warnings; there were no script compilation failures. Editor is outside Play mode, scene clean, with no pending suite request.
