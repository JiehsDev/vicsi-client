# Temporary floating UI wall placement

## Pre-change audit

Inspected project-owned runtime UI source, scene-authored HudFollowCamera components, world-space canvas creation, camera parenting and manual camera/head-forward placement patterns before changing placement.

| Element / route | Pre-change placement and decision |
| --- | --- |
| NotificationCanvas / NotificationUI | Legacy HudFollowCamera: 1 m forward, camera-local Y -0.5 m, obstacle awareness off, shared layout off. Migrate. |
| EvidenceNotifier confirmation toasts (photographed, outlined, final photographed, recorded, collected, sealed, processed) | NotificationManager -> GameDialogue -> NotificationUI tooltip channel; same legacy canvas. Migrate centrally. |
| Tool/procedure-complete feedback, short contextual tooltips, timed notices | Catalog Tooltip entries and uncatalogued Confirmation text use NotificationUI. Migrate centrally without changing routing/content/history. |
| Ambient A/B, held evidence Store and deposit prompts | Same NotificationUI canvas, sometimes with a camera-local offset override; migrate centrally, retain arbitration and gameplay action owners. |
| Blocked-action guidance and warnings | Catalog entries retain their authored channel. Tooltip entries share the legacy canvas; Dialogue entries already use shared STCS placement. Migrate only the former. |
| Training and procedural milestone dialogue / STCSNotificationUI | Already calls ConfigureShared in Awake; no new collision system or channel changes needed. |
| Evidence board, inventory, album, wheels, student/pairing panels | Authored followers initially show shared=false in Edit mode, but the opening paths call ConfigureShared. Already migrated at runtime. |
| Sketch manual camera-forward initial pose and WorldPanelPlacement helper | Immediately register the pose with the existing shared solver and world anchor. Desired-position calculation is retained; these do not bypass collision validation. |
| BODY-A full-black transport message | Deliberate camera-attached blackout overlay while the environment is covered and other canvases are suspended; retain the existing transition, locks and transport representation. Its normal confirmation panel is already shared. |
| ViewfinderOverlay, shutter/confirmation frame, reticles, aim rays, chalk/measurement guides | Intentionally fixed/tool-local surfaces; retain their visual correspondence to the tool/target. |
| NPC labels, evidence/tent markers, sketch-template surface, scene photography gizmos | Physical/world-attached labels and guides, or Editor-only gizmos; retain authored world placement. |
| Lobby and optional developer overlay | Fixed lobby desk-height canvas and screen-space diagnostics; retain their distinct placement. |

The shared HudFollowCamera itself computes a desired head-forward pose as input to collision validation. That calculation is intentional. Hand/tool-local positioning, capture-camera parenting and geometric TransformPoint/InverseTransformPoint code are also intentional and are not floating feedback.

## Implementation and validation

NotificationUI now initializes its existing world-space canvas through `HudFollowCamera.ConfigureTemporaryFeedback`. This covers all of its timed procedure/result messages, tooltips, contextual notices and ambient prompts without editing each producer. Existing catalog routing, text, history and latest-wins/no-backlog behavior remain intact. Dialogue-routed guidance and training continue through the already shared STCS canvas. No independent raycast/occlusion system, global UI manager or input owner was added.

The scene and serialized label/countdown/canvas references are reused. Initialization runs in Awake in Editor Play and builds; Edit-mode authored follower values can still show the older defaults. No manual Editor wiring or scene save is required. Lobby-contained or non-world-space NotificationUI instances retain their fixed layout.

### Procedure feedback profile

| Setting | Value |
| --- | --- |
| Preferred forward distance | 0.90 m |
| Camera-local offset | X 0, Y -0.18 m, Z 0 |
| Minimum readable radial distance | 0.60 m |
| Wall clearance | 0.12 m |
| Center sphere-cast radius | 0.035 m |
| Environment mask | Existing serialized Default layer mask, value 1, matching the major panels |
| Collider filtering | Existing shared player/camera/tool/evidence/marker exclusions; triggers ignored |
| Position smoothing | 0.10 s |
| Rotation smoothing | 8 |
| Position / rotation replan thresholds | 0.05 m / 3 degrees |
| Stable lateral fallback increment | 0.32 m, using the existing candidate/turn search |
| Uniform canvas scale | 0.0011 |
| Nominal authored 480 x 120 panel | 0.528 x 0.132 m; existing text fit expands downward as needed |
| Dialogue companion slot | Measured below primary, 0.05 m edge-to-edge gap |
| Timed procedural feedback priority | 250 |
| Ambient prompt priority | 100 |

Critical/reasoning/dialogue/information priorities remain 600/500/400/300. Temporary feedback is a companion, so it cannot displace a major panel. Ordinary dialogue and the notification use one jointly validated pose/rotation/fallback side with nonoverlapping rectangles. Existing above-primary dialogue companions are unchanged; the shared solver gained an optional below-primary slot.

When PlayerUIGate has a modal/information owner, the notification canvas is withheld by the shared layout rather than participating in that panel's placement. Its latest timed message and remaining time are retained; a newer tooltip still replaces it rather than accumulating a queue. Timers pause while modal/layout deferral or zero fade alpha withholds the message, and resume when it can be displayed. A tight enclosure with no safe readable candidate also withholds the canvas. Ambient prompts retain their existing context/arbitration and return after timed feedback expires if still requested.

Temporary UI has no A action, no PlayerUIGate entry, no ToolInputLock entry and no locomotion lock. Its CanvasGroup is explicitly non-interactive and does not block raycasts. World interaction owners still own their A/B actions.

### Validation

Final accepted results: **189 PASS / 0 FAIL across five fresh-session suites**. Exact accepted report paths are in `docs/evidence/temporary-ui-wall-placement/summary.json`; repeated attempts are not added together.

| Suite | PASS | FAIL |
| --- | ---: | ---: |
| UIComfortInputTest | 55 | 0 |
| NotificationPriorityTest | 4 | 0 |
| DialogueCatalogTest | 46 | 0 |
| BodyRemovalTest | 41 | 0 |
| CollectionFeedbackTest | 43 | 0 |

The expanded geometry/input suite performs real initial-photograph completion transitions for the bottle, knife and body to reach the actual notification channel. It verifies normal open-space placement, a wall between eye and desired pose, a very close wall with a side opening, minimum readability, safe stable fallback under head jitter, no readable slot/canvas suppression and paused timer, resumption after the obstruction is removed, ambient priority, actual reasoning-panel deferral without overlap or A-owner changes, real dialogue/feedback lower-slot separation, and both rectangles' wall clearance. It also retains the existing actual XR/Meta A-to-Jump tests, activation/close/hold/release guards, dialogue/question geometry, Notes/inventory geometry, wheel/tool/grab locks and stable shared layout checks.

The notification-priority fixture initially failed because a delayed live scene-photography instruction replaced its test tooltip. The corrected diagnostic temporarily cancels that named pending instruction, isolates unsolicited checkpoint/training presentation, supplies an unobstructed eye pose for wall-aware readable-time testing, and restores the original camera references/presenters/instruction afterward. No runtime routing or input gate was weakened. The earlier result is retained under run1; the accepted retry has four passing checks. Dialogue/catalog regression confirms existing channels, durations, queue/replace behavior, skip/history and actual displayed text. Body-removal and collection tests verify that the new feedback does not break the blackout/transport transition, interaction restoration, evidence custody or B-store behavior.

Final Editor: Unity 6000.5.6f1, Android target, zero C# compilation errors (`compilationFailed=false`), not compiling, outside Play/no transition, clean saved CSI_Environment, one NotificationUI canvas, no pending runner or XR-settings restoration, and both Standalone/Android XR initialization restored to enabled. Snapshot: `docs/evidence/temporary-ui-wall-placement/final-editor-state.json`. Targeted `git diff --check` passes.

### Remaining direct placement patterns

The final whole-Assets source search is saved in `docs/evidence/temporary-ui-wall-placement/head-placement-search.txt`.

- WorldPanelPlacement and sketch: compute desired world anchors, immediately register them with HudFollowCamera; already collision-aware.
- HudFollowCamera and its shared partial: the desired-pose calculations inside the reused solver; intentional. Its legacy branch remains for the fixed viewfinder.
- BodyRemovalInteraction: full-black screen-attached transport overlay; intentionally retained, with the environment covered. Ordinary confirmation placement is already shared.
- ScenePhotoPoint: Editor gizmo sightline drawing; no runtime popup.

No remaining project-owned floating procedure/result notification channel requires migration. Physical object labels, marker labels, tool-local guides, reticles, capture camera, viewfinder, fixed lobby and diagnostic overlay remain unchanged for the reasons recorded in the pre-change audit.

### Files changed

- `Assets/_Project/Scripts/UI/NotificationUI.cs`
- `Assets/_Project/Scripts/Interaction/HudFollowCamera.Layout.cs`
- `Assets/_Project/Scripts/UI/PlayerUIGate.Confirm.cs` (placement constants only)
- `Assets/_Project/Scripts/Testing/UIComfortInputTest.cs`
- `Assets/_Project/Scripts/Testing/NotificationPriorityTest.cs`
- This report, evidence/summary/search/Editor snapshots and session/task handoff notes.

Quest-only acceptance remains: stereo text readability/contrast, lower-slot comfort, side-turn/fallback feel near real room geometry, performance during head motion and transient messages, and physical controller/ray interaction timing. No headset build/install, live assessment upload, commit, push or deployment was performed.
