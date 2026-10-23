# Content-responsive VR options

Runtime-generated text rows now retain their readable fonts and grow to fit wrapped content. No manual Editor wiring, commits, pushes or deployment.

## Implementation and audit

`ResponsiveTextOption` measures TMP wrapped preferred height at the available text width, adds vertical padding and reports that height through `LayoutElement`. `ResponsivePanelLayout` supplies a vertical layout with equal-width rows, bounded panel height and masked vertical ScrollRects. It measures and places content synchronously when a screen assigns text; its cached LateUpdate catches later changes before rendering. ContentSizeFitter is disabled on lists owned by this layout so two components do not compete for container height.

Sizing: `max(minimum row height, ceil(TMP preferred height including inner margins) + 32)`. Row padding is 20 left/right and 16 top/bottom; TMP also gets a 2-unit inner inset to contain SDF glyph overhang. Footer hints use 12 top/bottom. Related option rows are separated by 12 units; recorder chips by 8. Panel edges use 40 horizontal units (reasoning 52) and 24 vertical units. Font size and line spacing are retained, normal word wrapping is enabled, and font auto-sizing is disabled. TMP masking and viewport RectMask2D prevent drawing outside the visible area.

Converted fixed rows/panels:

| Screen/component | Minimum row height | Panel minimum / maximum, UI units |
| --- | ---: | ---: |
| HypothesisCheckpointUI: hypothesis, reasoning and contextual answers | 78 | 640 / 900 |
| SettingsPauseMenu: text menu and confirmation buttons | 54 | 580 / 740 |
| SOCOWorkflowInteraction: scenario decisions | 65 | 380 / 740 |
| VendorFollowupInterview: conversation and follow-up action | 70 action; 48 body | 700 / 1000 |
| SOCOCloseout: readiness details and confirmation | 55 action; 48 body | 740 / 900 |
| BodyRemovalInteraction: instruction and preparation action | 65 action; 48 body | 620 / 800 |
| ReportLineChip / RecorderReportUI: candidate and selected report lines | 64 | Existing fixed panel and column viewports |

Reasoning retains its 0.00085 canvas scale, vendor retains 0.0006, and other panel scales/viewing distances remain as configured by their owners. Maximums are available on the runtime shared layout component for tuning. Header and footer have independently scrollable caps of 280 and 180 units for unusually long questions/confirmation wording. Normal questions and confirmation controls remain pinned while the answer list scrolls.

Timed dialogue already uses TMP preferred-height fitting (`DialoguePanelLayout`); dialogue choices routed through reasoning/decision panels adopt the new shared pattern. Existing history, witness accounts and recovered-message scroll systems are retained. Icon-only controls, radial/tool controls, height +/- controls, evidence-board diagram cards and the body transport fade remain intentionally fixed.

## Interaction and scrolling

Each overflowing area exposes fixed arrow controls to the existing world-panel pointer; each click scrolls 90 UI units. Reasoning and settings also allow horizontal thumbstick scrolling while their existing vertical selection remains available. Selecting an ordinary row brings its full hitbox into view. An exceptionally tall row aligns its top and can then be scrolled without repeatedly snapping back. Questions and confirmations remain outside the answer viewport.

`WorldPanelPointer` now checks ancestor RectMask2D rectangles before accepting a hit: an offscreen row cannot consume a ray over the question/footer. Button hit rectangles are the resized row containers, including padding. The existing PlayerUIGate, ToolInputLock, modal priorities, fresh-press guards and callback IDs remain the input authorities. Reasoning pointer selection preserves its select-then-confirm behavior; A submits the selected original response ID once. Shared HudFollowCamera sees final dimensions before its late placement pass and continues to place, stack or defer panels against walls.

## Validation

At 300-unit row width with 30-point text (previously all 78 units high):

| Sample | Previous height | Measured new height |
| --- | ---: | ---: |
| One line | 78 | 78 |
| Two lines | 78 | 105 |
| Four lines | 78 | 174 |
| Stress case, 36 wrapped lines | 78 | 1277, inside scrolling viewport |

The mixed-list fixture stays capped at 600 units. Actual contextual reasoning stays capped at 900 units; its answer text remains 26 points. Body scrolling leaves the header and confirmation/footer in place. Tests inspect rendered TMP character bounds, row spacing, full-row pointer hits, clipped/offscreen hits, programmatic and pointer scrolling, synchronous text replacement and real input actions. Short content does not unnecessarily scroll. Long vendor questions, closeout actions and body-removal instructions are measured independently in their actual runtime panel components. Simultaneous trigger and A advances one stage of a two-stage question; a fresh A submits the original second-stage ID once.

Final results: **443 Unity checks plus 6 scorer checks passed; zero failures.**

| Suite | Passed |
| --- | ---: |
| ResponsiveOptionLayoutTest | 41 |
| ReasoningCheckpointTest | 60 |
| UIComfortInputTest | 55 |
| SettingsPauseMenuTest | 35 |
| VendorFollowupTest | 16 |
| SOCOCloseoutTest | 48 |
| BodyRemovalTest | 41 |
| NarrativeRoleFlowTest | 41 |
| ScenarioPhaseFoundationTest | 19 |
| EvidenceProcedurePanelTest | 41 |
| DialogueCatalogTest | 46 |
| Node scorer audit | 6 |

[Machine-readable summary](../evidence/responsive-options/summary.json) lists the authoritative report for each suite. [Rendered reasoning preview](../evidence/responsive-options/reasoning-preview.png) shows the unequal rows and capped body viewport. [Final Editor status](../evidence/responsive-options/final-editor-state.json) confirms no C# errors or compilation failure, CSI scene clean, Edit mode, no pending diagnostic/XR restore, no UI/tool locks or suspended locomotion, and Standalone/Android XR startup restored. Existing Meta SDK/editor scene messages remain separate from C# compilation and were not changed.

Early runs are retained for traceability. The initial glyph-bounds failures were corrected by the inner TMP inset. One explicit four-line fixture was shortened because the inset correctly caused its final sentence to wrap to a fifth line. The vendor review fixture now clears unrelated queued BODY-A teaching before its second isolated review, as it already did for its first review. The catalog timer fixture now supplies an unobstructed camera because an untracked floor-level camera can correctly defer UI and pause its visible countdown. A stale test ray closure was corrected to capture its direction before a stage destroys the previous rows; the final focused run verifies the freshly compiled source. Production dialogue timing, phase gates, scoring and assessment definitions were not changed for these fixture corrections.

Quest-only follow-up: verify text comfort, arrow target size and horizontal-stick scroll speed with actual Quest 3S hand/controller rays, both seated and standing. Confirm comfort at the existing viewing distances and in narrow spaces. Automated Editor tests do not replace headset readability and tracking validation.
