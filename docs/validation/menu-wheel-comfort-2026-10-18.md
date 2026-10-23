# ViCSI menu-wheel comfort — 2026-10-18

## Inventory and scope

Project inspection found one non-tool radial-menu implementation, `UtilityMenuController`, with serialized instances in `CSI_Environment.unity` and `Tutorial_ToolTest.unity`. There are no separate menu-wheel prefabs. Both instances now use the same menu-only comfort profile. The CSI wheel varies with the IOC, investigation and closeout phases; tutorial has Notes, Item Logs and Album, plus runtime Settings where available. CSI's authored/runtime entries are IOC Checklist, Relationships (the existing EvidenceBoard listener resolves the authored Notes entry), Item Logs, Album, Submit Conclusion, Evidence Inventory, Closeout Inventory, Dialogue History, Witness Accounts, Evidence Procedure, Scene Sketch and Settings. Availability, order, actions and listeners are unchanged.

The Tool Wheel is `ToolWheelController` / `ToolWheelWedgeTemplate`, with authored `ToolWheelCanvas` hierarchies in the CSI and tutorial scenes. It is explicitly excluded. Its scripts, templates, scene records, typography, canvas scale, radius, selection/input behavior and shared dependencies were not changed. Hash preservation evidence covers all five project scene files and the Tool Wheel sources/metas.

## Dimensions and typography

| Property | Previous | Current |
|---|---|---|
| Ring diameter | 700 units / 0.420 m | 660 units / 0.396 m |
| Ring outer radius | 350 units / 0.210 m | 330 units / 0.198 m |
| Ring inner radius | 147 units / 0.0882 m | 138.6 units / 0.08316 m |
| Label-anchor radius | **504 units / 0.3024 m (outside ring)** | **250 units / 0.150 m (inside ring)** |
| Label rectangle | 150 × 60 units | 112 × 60 units |
| Label font | 20 units bold, selected up to 23 | 24 units bold, constant physical size |
| Nominal label font size in world space | 12 mm | 14.4 mm |
| Release hint | 16 units normal, below ring | 20 units bold, central dark hub |
| Inactive-label opacity | 35% authored | minimum 65% |
| Highlight motion | +20 units outward, scale 1.15 | no outward movement or resizing |
| Segment gap | up to 6 degrees | up to 2 degrees |
| Canvas scale | 0.0006 | unchanged |
| Headset distance | 0.65 m | unchanged |

The most significant footprint reduction is bringing labels back inside the wheel: their anchor radius is reduced by 50.4%, while the ring diameter reduces by 5.7%. At the existing distance, the ring spans approximately 33.9 degrees horizontally; opposing label anchors span 26.0 degrees (previously 49.9 degrees). These are geometric angular spans, not a measurement of optical readability through Quest lenses. The unchanged wall solver can still move or defer UI when surroundings require it.

No icons were used by these menu wheels. Existing black/white selected-state inversion is retained, without outward enlargement. Text uses explicit bold style, fixed font sizes, normal wrapping, sensible line spacing and no automatic shrinking. The center hint now has its own dark circular background. All visible menu content fits inside the measured root rectangle used by the shared wall-avoidance solver; previously labels and the bottom hint extended outside it.

`MenuWheelComfortProfile` centralizes dimensions, world scale/distance, text size, inactive contrast and compact display labels. Shortened labels include IOC Checks, Evidence Links, Conclude, Inventory, Closeout and Procedure; two-word labels use at most two short lines. These names affect presentation only. Underlying entries/events and full screen titles remain unchanged.

## Behavior and validation

The original input and ownership method bodies are unchanged: Awake/input bindings, device reconnection, OnEnable/OnDisable, Update, opening, closing, phase changes and directional selection. This retains left X hold/release, right-stick direction, 0.35 deadzone, confirmation, modal locking and reference-counted locomotion suspension. No shared `HudFollowCamera`, `PlayerUIGate`, `ToolInputLock`, locomotion, fixed HUD, scene or gameplay source was edited.

`MenuWheelComfortTest` exercises actual XR button/stick events, the tutorial 3/4-option layouts, CSI investigation/IOC/closeout layouts and an extra all-12-options stress layout. It checks actual TextMeshPro glyph corners within their own radial segments, line count/overflow, font size, selected colors, correct directional action index, deadzone retention, release-to-confirm, locks, locomotion suspension/restoration and the actual menu rectangle near a wall. Callback fixtures avoid executing gameplay actions while every index is tested; the existing Settings regression separately exercises the actual menu-to-Settings callback.

The initial stress test caught the long Checklist line crossing its segment in the extra 12-option layout; the displayed label was shortened to IOC Checks. A subsequent test-fixture correction supplies an active Meta Jump branch because desktop Play mode has no active tracked-controller locomotion branch. Earlier reports are preserved separately from accepted results. Final assertion counts and logs are recorded in `docs/evidence/menu-wheel-comfort/summary.json` after the run.

The existing `UIComfortInputTest` report ends with `=== ALL PASS ===`, while the existing batch runner recognizes only `=== PASS`; the task-local runner accepts either success header with zero failed assertions and preserves the raw runner exit status. Neither existing source was changed to normalize this reporting mismatch.

## Quest 3S acceptance still required

ADB reports no connected devices. No headset test or deployment was performed. Verify center/peripheral clarity, physical text size, required eye/head movement, 0.65 m viewing distance, stereo comfort, all phase layouts, stick segment accuracy, inactive-label readability and placement next to walls on Quest 3S. The geometric and input checks do not establish optical comfort.

No commit, push, deployment, production-data write, Tool Wheel change or unrelated gameplay modification was made.

## Final results

- MenuWheelComfortTest: **251 pass / 0 fail**, runner exit 0. This covers the exact tutorial label sets, investigation, IOC and closeout orderings plus the 12-option stress layout.
- UIComfortInputTest: **57 pass / 0 fail**, raw runner exit 1 solely because its existing ALL PASS header is not recognized by the existing runner. Shared wall placement, modal input and jump suppression assertions pass.
- SettingsPauseMenuTest: **28 pass / 7 fail**, raw runner exit 1. All menu opening/selection, Settings modal and wall-placement assertions pass. Seven height adjustment/persistence assertions fail.
- Original-menu control: **the same 28 pass / 7 fail with identical failure labels**. These height failures predate this visual change; height source/tests remain untouched. The exact accepted compact-menu source was restored afterward.
- **Zero C# compilation errors** across accepted/control logs.
- **1,333 pre-existing scoped source/data files unchanged**, including all five scene files, Tool Wheel scripts/templates, all other existing UI scripts, gameplay/data and project settings. Only the existing UtilityMenuController source changes; new menu-specific profile/test sources and their metadata are added. The final source hash and method-preservation audit are in preservation.json.

The seven pre-existing Settings height failures and physical Quest 3S acceptance remain outside the completed menu visual/input validation. No headset was connected.
