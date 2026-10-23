# Shared VR UI comfort and confirm ownership

Unity 6000.5.6f1, Android target / Quest 3S. Completed and rechecked on 2026-10-13. Source changes only. No commit, push, build/install, deployment, or live assessment upload. Automated diagnostics use the existing offline test sandbox. Existing unrelated working-tree changes are retained.

## Runtime UI inventory and placement decisions

| UI / channel | Placement and simultaneous behavior |
| --- | --- |
| Professor, teammate, witness dialogue; tutorial and procedural training lines (`STCSNotificationUI`) | Existing `HudFollowCamera` comfort profile joins shared layout. Alone it retains its own distance/offset. Beside a primary panel it occupies a measured, aligned slot above it. Existing training deferral, countdown, queue, skip, history and text are preserved. |
| Reasoning / hypothesis checkpoints | Existing size, scale, minimum distance, mask and smoothing remain. Primary below critical confirmations; takes the existing tool lock as well as its previous UI/locomotion locks. |
| Body-removal confirmation | Shared solver, critical priority; existing one-shot world anchoring retained. The fade mesh and body transition are unchanged. |
| SOCO inventory review, completion report and release confirmation | Shared solver, critical priority; existing world anchoring retained. Existing gates and confirmation flow unchanged. |
| Conclusion picker and completion/result feedback screens | Shared solver, critical priority; retain their distinct dimensions and 0.75 m profile. |
| Vendor interview question | Shared solver, dialogue-interaction priority; retains 1.05 m distance and offset. Physical vendor access remains unchanged. |
| IOC/SOCO decision / briefing interaction panel | Shared solver; original phase/radius availability retained. |
| IOC checklist | Shared solver through `WorldPanelPlacement`, retaining one-shot yaw-based placement and world anchoring. |
| Evidence Notes / procedure detail | Shared solver through `WorldPanelPlacement`, retaining preferred distance/drop, exact scale, and world anchoring. |
| Witness Accounts | Shared solver through `WorldPanelPlacement`; remains read-only, world anchored, and mutually exclusive via its existing gate. |
| Crime-scene sketch and its internal confirmation | Shared solver with retained world anchor; internal confirmation remains part of the same canvas. |
| Evidence inventory, photo album, evidence board | Existing authored followers opt into shared wall avoidance on opening; original sizes/distances retained. |
| Recorder report / notes composer | Shared solver, information priority; original authored/generated profile retained. |
| Dialogue History and Insights | Shared solver, information priority; existing exclusivity and controls retained. |
| Tool and utility radial wheels | Shared solver, information priority, retaining authored 0.65 m distance and wheel geometry. Existing hold/release selection and movement policy retained. |
| SOCO closeout objective strip | Read-only companion; existing hide-while-dialogue/modal behavior remains. |
| Scenario student identification and pairing | Shared solver for their independent world-space canvases. Lobby versions inside `LobbyCanvasPlacement` are excluded from independent movement. |
| Lobby main menu, instructor management, status table, keypad and confirmation rows | Intentionally retain their existing shared, fixed-height lobby canvas and internal table layout. Moving child panels independently would break desk alignment. |
| Small timed notifications, ambient A/B prompts, held-evidence Store prompt | Retain the existing near HUD/notification channel and prompt arbitration. These are nonblocking notifications, not extra A owners. Action owners are the relevant interaction components. |
| Camera viewfinder, aim indicator, measurement/chalk/marker guides, NPC/world labels, authored sketch-template surface | Remain attached to their tool, target or authored world location. Moving these relative to the head would misrepresent the tool/target or move scene content. |
| Development diagnostic overlay | Remains an optional developer HUD; no new gameplay input ownership. |

## Shared geometry and priority

`HudFollowCamera.Layout.cs` extends the existing `HudFollowCamera`, rather than introducing a scene-global manager or separate per-panel collision implementations. It calls the existing center-cast safe-placement solver, then validates full canvas rectangles using box sweeps, endpoint overlaps and corner/edge rays. The same environment mask, trigger exclusion, player/tool/evidence exclusions and wall clearance apply to primary and companion panels.

One layout pass runs after normal UI/head updates. It chooses a primary deterministically, retains each panel's own scale and preferred profile, and measures companion slots from actual world-space dimensions and pivots. Every visible group member shares one position interpolation and rotation. Every candidate and interpolated pose is validated for the whole group, including secondary-only obstructions. Unsafe interpolation snaps to the safe group endpoint.

The fallback side is retained until group membership/primary changes. Shorter readable distances, lateral offsets and a common turn toward a side opening are tried. Secondary panels can be deferred when they cannot fit. A panel with no physically safe readable candidate is withheld until space becomes available; the system does not deliberately place it through geometry. Spatially suppressed panels cannot accept raw A or pointer clicks.

The final real-canvas fixtures exposed a conservative sweep issue for the 1.20 x 0.68 m Evidence Notes panel: the full-sized test rectangle at the eyes overlapped the nearby wall at distance zero, although the actual side-positioned panel and every sightline were clear. Those artificial starting overlaps no longer reject a candidate. Nonzero sweep hits, endpoint overlaps and sightline rays remain checked, and a viewer actually inside solid geometry still rejects the candidate. Notes now finds a safe shared side pose without changing its authored size or distance.

Critical confirmations/results use priority 600, reasoning 500, dialogue/interview 400, information panels/wheels 300, and ambient world A actions 200. Existing opening gates and checkpoint deferral remain authoritative. If otherwise conflicting interactive canvases coexist, lower-priority canvases temporarily yield without losing their state. Timed dialogue can accompany the primary in a nonoverlapping slot; it does not independently choose another fallback side. Notification timers and the existing dialogue countdown/deferral rules are unchanged.

Inspector settings reused: preferred distance, local horizontal/vertical offset, minimum distance, wall clearance, environment mask, sphere radius, lateral fallback, position/rotation smoothing and replan thresholds. Added on `HudFollowCamera`: shared-layout opt-in, placement priority, companion opt-in, companion gap, and world-anchor mode. Generated panels receive their profile in their existing creation/opening paths; authored panels retain authored dimensions and distances. No manual scene or prefab wiring is required.

## A ownership and gameplay input

`PlayerUIGate` now also owns raw-confirm arbitration (`CreateConfirmAction`, `ConfirmOwnerToken`, `TryConsumeConfirm`, `IsUIConsumingConfirm`, `SuppressGameplayJump`). All 16 runtime right-A action readers register their actual availability predicate and priority. Disabled/destroyed owners cannot win; ordinary notifications never register A. Only the highest-priority eligible owner can consume a press.

The player's Meta `ActiveStateUnityEventWrapper` normally invokes `LocomotionActionsBroadcaster.Jump` during Update. A runtime-installed, per-source `UIConfirmJumpRelay` replaces only that persistent Jump callback in memory, queues it, and resolves it in late update after UI readers. Other callbacks and the package prefab remain intact. Thus an A action that opens or closes UI earlier in the frame can consume the press before gameplay receives it.

`Enter` guards the activation frame. A consumed press remains consumed while either the raw XR A action or OVR raw A reports held, through its release frame. Closing a panel, handing off between owners, or re-enabling suspended locomotion cannot reinterpret that gesture as jump. A later fresh press forwards the original Meta Jump action normally. Pointer input similarly requires a fresh trigger after opening, respects panel priority/visibility, and permits one pointer click per frame.

Dialogue currently uses timed progression, B skip and right-stick dismissal, not A. Those bindings and narrative content are preserved. IOC Observe, first-responder/vendor access, SOCO decisions, body confirmation, closeout, evidence deposit and all A-driven panels use the shared arbiter.

Vendor A ownership additionally requires an available question (`IsShowing && CanAsk`); the read-only account after discovery does not register an unavailable A action. Existing B-close and locomotion behavior remain intact.

Reasoning now takes `ToolInputLock`. Existing evidence/tool readers already honor that lock. Its modal-release guard also retains trigger/grip/store/wheel-button gestures until release, preventing the closing click from becoming a tool action. Both wheels respect it. Physical evidence grab components are temporarily disabled when unselected; a currently selected object is kept held. Sticky-grab release and evidence-tent pickup also defer to the modal lock. Evidence collection prerequisites, custody, scoring, deductions and phase state machines are not changed. Normal locomotion policy remains as previously defined per panel; ambient interaction ownership only intercepts jump.

## Validation and diagnostics

Recorded runs are in `docs/evidence/ui-comfort-input/`. `Tools/RunUIComfortChecks.ps1` submits each suite to the existing Editor runner in a fresh Play session. No pre-existing dedicated player/jump/input regression suite was found; `UIComfortInputTest` and its test-only input probe cover that missing path with a synthetic XR device and the actual Meta wrapper/broadcaster classes. The scene's runtime-installed Jump callback is also inspected. These checks do not claim physical Quest controller validation.

The initial `DialogueCatalogTest` reproduced both reported failures:

- B skip: diagnostic output recorded `owners=HypothesisCheckpointUI; ambient=False`. The test incorrectly assumed no UI owner after its phase fixture made a checkpoint eligible. The test now isolates checkpoint presentation; the runtime B arbitration is preserved.
- Vendor wording: the assertion required “sometime around three” and “recognized him”, while the existing authored line says it remembers seeing Paul near John's house around three and did not see where he went. Updated assertions preserve identity, place, approximate time and limits, with no catalog/text edits.

The IOC phase fixture additionally needed checkpoint isolation around its new multi-frame raw-A observation and an actual approach to the separately authored IOC ground marker before calling `BeginBriefing`. This fixes the fixture to respect existing range rules rather than weakening those rules.

Final selected results: **558 PASS / 0 FAIL across 15 suites**. These are the final reports for each suite, not a sum of repeated attempts. The machine-readable manifest lists the exact source report paths: `docs/evidence/ui-comfort-input/completion-2026-10-13/summary.json`.

| Suite | PASS | FAIL |
| --- | ---: | ---: |
| UIComfortInputTest | 41 | 0 |
| ReasoningCheckpointTest | 60 | 0 |
| DialogueCatalogTest | 46 | 0 |
| NarrativeRoleFlowTest | 41 | 0 |
| ScenarioPhaseFoundationTest | 12 | 0 |
| VendorFollowupTest | 16 | 0 |
| EvidenceProcedurePanelTest | 41 | 0 |
| EvidenceInventoryTest | 29 | 0 |
| CollectionFeedbackTest | 43 | 0 |
| BiologicalSamplingTest | 59 | 0 |
| FingerprintStorageTest | 25 | 0 |
| GrabReachTest (existing physical-input regression) | 9 | 0 |
| SOCOCloseoutTest | 48 | 0 |
| EndingManagerTest | 9 | 0 |
| CrimeSceneSketchTest | 79 | 0 |

The final comfort suite verifies actual dialogue, reasoning, Notes and inventory rectangles near walls; open-space profile preservation; coordinated companion slots; secondary-only obstacles; minimum readability; stable fallback; modal priority; real raw XR A and Meta Jump routing; activation/close/hold/release protections; reasoning locking; notification ownership; wheel and physical-grab locks; and trigger release before restoring tools.

Earlier failed attempts remain available for audit. The broad vendor run had one Witness Accounts reopening assertion failure; it did not reproduce in two fresh retries. The freshly compiled diagnostic confirms `showing=True`, `owners=WitnessAccountsUI`, one discovered account, and the correct summary without the question. The original failure's exact timing cause was not established. No narrative or discovery rules were weakened. One added inventory fixture initially searched only active objects, although the authored inventory starts inactive; corrected the diagnostic lookup. The Notes wall failure was a real conservative placement limitation and was corrected in the shared solver as described above. An older assembly was detected during one retry and explicitly reimported before accepting final results.

Final Editor snapshot: outside Play, no Play transition, not compiling, `scriptCompilationFailed=False`, clean `CSI_Environment` scene, no pending suite/request and no pending XR-settings restoration. Unity rebuilt `Assembly-CSharp` successfully; existing warnings remain. `git diff --check` passes for the changed scripts. No manual Editor wiring is required. The final 558-check total supersedes the overstated intermediate progress count.

## Quest-only acceptance

Still requires a new Quest build and physical testing: stereo readability at each panel's retained scale; near-wall/corner behavior, comfort of common side turns and companion height; smoothness/performance with room colliders; controller/OVR timing on open, hold, close and release; ray targeting after placement; physical two-hand grab retention; radial wheels and return to normal locomotion. No headset build/install was requested or performed here.
