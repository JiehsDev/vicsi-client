# VR-CSI Unity Setup — Session Handoff (continue in new window)

**Purpose:** This session got the Unity project itself into a verified-good state and fixed the Claude Code ↔ Unity MCP connection, but got stuck on a process-restart quirk that only a brand-new conversation can clear. Paste this whole document as your first message in the new session so it has full context without re-deriving anything.

---

## 0. What this project is (background, unchanged from thesis doc)

Rolando's thesis at Partido State University – Lagonoy Campus: a VR-based Crime Scene Investigation training simulator (Quest 3S, Unity) + web instructor dashboard, built around the CAMIL framework. Actual 12-week build is scoped down to **1 scenario** (indoor kitchen homicide), **2 playable roles** (Photographer, IOC/Case Analyst), **5 non-played roles simulated via STCS** (scripted comms, no AI/NPCs). Full 7-role/multiplayer is future work — do not re-expand scope.

**Important correction from an earlier handoff doc reviewed this project:** that doc claimed a Next.js + Supabase dashboard, `docs/DEV_GUIDE.md`, and a `vr-csi-simulator/` monorepo structure were "done." **None of that exists anywhere on this machine** — verified by exhaustive filesystem search. Treat any claim about the dashboard/Supabase as unverified/aspirational unless it's re-confirmed from an actual repo. The Unity-side claims in that doc were largely accurate and have now been independently re-verified in this session.

---

## 0a. STANDING RULE for every task: ad-hoc editor code is not a deliverable

**If a report calls something a feature, it is in a file, or it isn't a feature.**

Anything typed into Unity's `execute_code` (or any other scratch execution buffer) is
a *probe*, not an implementation. It vanishes when the session ends. It must never be
described in a report as something that was built, added, or now exists. Either write
it to a real source file that gets committed, or describe it accurately as a throwaway
check.

This is a rule governing **future reports**, not a note about past ones. It is here
rather than in §8 because it is not a debt on any one task.

### Why it is a rule

The same failure has now happened twice in this project, with an identical signature:

1. An ad-hoc greybox lifecycle walkthrough was typed into `execute_code`, reported as
   a working end-to-end test, and lost with the session. When the `Marked` status was
   later inserted and the old flow stopped being valid, there was no script left to
   fail — only the memory of one. `Assets/_Project/Scripts/Testing/GreyboxFlowTest.cs`
   was written to close exactly this, and says so in its own class comment.
2. A greybox debug-label visibility toggle was reported as existing from that same
   session. The radius-visualisation task went to reuse it, per an explicit
   instruction not to build a second switch, and it was not there — not in the working
   tree, not in the scene, and not in any commit on any branch in the repository's
   history (`git log --all -S` returns nothing; `GreyboxFlowTest.cs` is the only file
   ever added under `Testing/`).

The cost is not a lost test. It is that the *next* task gets planned around something
that does not exist, and only finds out by going looking.

### In practice

- Before writing that something was implemented, confirm the file exists on disk.
- Write helpers and test harnesses to real source files rather than evaluating them
  inline, whenever they are meant to persist or be reused.
- In reports, keep "I verified X with a throwaway probe" clearly separate from "I
  built X." The first is evidence; only the second is a deliverable.

---

## 1. Actual project location and structure (verified, not assumed)

- **Project root:** `C:\Users\Roduf Eleu\My project (1)` — the Unity project files live directly here, not under a `unity-client/` subfolder.
- **Git repo root is `C:\Users\Roduf Eleu` (the user's home directory)** — `My project (1)` is just an untracked subfolder inside that same repo, not its own repo. This matters: it's why Claude Code's `--scope local` MCP registrations resolve to the home folder, not to `My project (1)` specifically. This is expected behavior given the repo layout, not a bug — but it's worth asking Rolando at some point whether that's the intended git structure for a thesis submission, or whether `My project (1)` should become its own repo.
- `Assets/_Project/` now has the full documented folder structure (created this session):
  ```
  Scenes/                          — Main.unity here; Scenarios/ subfolder holds CSI_Environment.unity, Role_Photographer.unity, Role_IOC.unity
  Scenes/Scenario_Basic01_Indoor.unity  — exists, unexplained, not referenced in any doc seen so far — ask Rolando if it's live or a leftover
  Scripts/{Core,RoleSystem,Interaction,STCS,CaseFile,ProceduralGate,Deduction,Feedback,Logging,Networking}/
  Data/{Scenarios,Roles,Evidence,DialoguePools}/
  Prefabs/{Evidence,Tools,UI}/
  Art/{Environments,Props}/
  Audio/{VoiceLines,SFX}/
  UI/{HUD,Menus}/
  ```
  Only `Scripts/CaseFile`, `Scripts/RoleSystem`, `Scripts/UI` have actual content; everything else created this session is empty and ready.

## 2. Scripts confirmed to exist and match their described behavior

- `RoleConfig.cs` — static, `RoleId {None, Photographer, IOC}`, `OnRoleSelected` event, `SetRole()`
- `RoleSceneLoader.cs` — additive role-scene loader reading `RoleConfig.SelectedRole`
- `UIManager.cs` — lobby panel switching
- `EvidenceStateManager.cs` — singleton `Instance`, `Mark{Found,Photographed,Sketched,Logged,Collected,Processed}`, auto-promotes to `ReadyForCollection` on Logged, `OnEvidenceStatusChanged` event — fully matches spec, no changes needed
- `EvidenceStatus.cs`, `EvidenceDefinition.cs`, `EvidenceRecord.cs` — present, straightforward
- `STCSManager.cs` — **exists but is an empty stub** (bare `Start()`/`Update()`, no trigger table, no phrasing pools). Not started despite the filename existing.

**Confirmed NOT to exist anywhere in the project** despite being claimed "delivered" in an earlier doc: `HandVisualAnimator.cs`, `SupabaseUploader.cs`. No `GrabbableEvidenceBase` prefab existed before this session (folder was empty).

## 3. Unity Editor / Player Settings / packages — all verified correct for Quest 3S, nothing to change

Read directly from `ProjectSettings/ProjectSettings.asset` and `Packages/manifest.json`:

| Item | Value | Status |
|---|---|---|
| Android min/target SDK | 32 / 34 | ✅ meets current Meta minimum |
| Architecture | ARM64 only | ✅ |
| Scripting backend (Android) | IL2CPP | ✅ |
| XR Plugin Management | 4.7.0, OpenXR loader assigned to Android | ✅ |
| XR Interaction Toolkit | 3.5.1 installed | ✅ |
| Meta XR All-in-One SDK | 205.0.0 installed | ✅ |
| OpenXR Android features enabled | Meta XR Feature, Meta Quest Touch Pro Controller Profile, Hand Interaction Profile, Meta XR Foveation, Meta XR Subsampled Layout | ✅ sane current set |

One thing that could **not** be verified from disk (needs a manual glance in the Editor): which build target is *currently active* in File → Build Settings, and whether Color Space is Linear / Graphics API includes Vulkan. Worth a 10-second check but not expected to be wrong.

## 4. Scene state — `CSI_Environment.unity` (verified by reading the scene file directly; it's text-serialized YAML)

- `[BuildingBlock] Camera Rig` — **exists**
- `[BuildingBlock] Teleport Interactable` with `Teleport Collider` + `Teleport Hotspot` children — **exists**, but untested — don't assume it works
- **First Person Locomotor — does NOT exist yet**
- **Turn (snap/smooth) — does NOT exist yet**

## 5. The pending task: Building Blocks + GrabbableEvidenceBase prefab (not yet done)

### Part 1 — Movement Building Blocks
1. Open `Assets/_Project/Scenes/Scenarios/CSI_Environment.unity`.
2. **Window → MCP for Unity** panel confirms the menu is under "Window" not a top-level "Meta" menu in this install — for Building Blocks specifically it's the Meta panel, likely **Meta → Tools → Building Blocks** or **Window → Meta → Building Blocks** — check both, SDK is v205.x.
3. Movement category → **First Person Locomotor** → Add to Scene. It should reuse the existing `[BuildingBlock] Camera Rig` — if it offers to create a second rig, stop and don't (two rigs = broken input).
4. **Turn** → Add to Scene, same rig-reuse caution. Pick **Snap Turn** (comfort default).
5. **Teleport** — test the existing one first (see Part 3) before deciding whether to delete-and-redo it.
6. Check Console for errors after each add.

### Part 2 — `GrabbableEvidenceBase` prefab
1. Hierarchy → Cube, rename `GrabbableEvidenceBase`, scale ~`(0.1,0.1,0.1)` as placeholder. Keep the default Box Collider.
2. Add Rigidbody: Collision Detection → **Continuous**, Is Kinematic → unchecked, Use Gravity → checked.
3. Add **Grabbable** (Oculus.Interaction).
4. Add **Hand Grab Interactable**: Pointable Element → the Grabbable component, Rigidbody field → the Rigidbody component, confirm Colliders list picked up the Box Collider.
5. Add **Grab Interactable**: same two cross-references (Pointable Element → Grabbable, Rigidbody → Rigidbody).
6. Drag into `Assets/_Project/Prefabs/Evidence/` as an **Original Prefab**, then delete the scene instance, save scene.

### Part 3 — Play Mode checklist
Locomotion smooth-moves, Turn snap-turns, Teleport arc+hotspot works, Grab follows hand, Throw survives a hard wall-throw without tunneling (that's what Continuous CD is for), Console stays clean (no NullReferenceException — that's the signature of an unassigned Pointable Element/Rigidbody field).

**None of this has been executed yet** — it was blocked all session by the MCP connection issue below. Once the new session has live Unity tools, do this directly rather than narrating manual steps.

## 6. MCP for Unity connection — fixed on the backend, blocked only by process restart

This ate most of the session. Full state, so the new window doesn't redo the debugging:

- Package: `com.coplaydev.unity-mcp` ("MCP for Unity" v10.1.2 per its panel), already installed and compiled.
- Menu paths (verified from source): **Window → MCP for Unity → Toggle MCP Window** (`Ctrl+Shift+M`), **→ Local Setup Window**, **→ Edit EditorPrefs**.
- Registration is via the Claude CLI itself: `claude mcp add --scope local --transport http UnityMCP http://127.0.0.1:8080/mcp`. This is now **correctly registered** — confirmed multiple ways:
  - `claude mcp list` → `UnityMCP: http://127.0.0.1:8080/mcp (HTTP) - ✔ Connected`
  - `claude mcp get UnityMCP` → Scope: Local config, Status: ✔ Connected
  - A raw JSON-RPC `initialize` POST to `http://127.0.0.1:8080/mcp` returns a full valid MCP handshake from `mcp-for-unity-server` v3.4.7 with tools/resources capabilities.
- Earlier confusion (now resolved, don't rehash): the registration appears under the project key `C:/Users/Roduf Eleu` in `~/.claude.json`, which looked wrong but is actually **correct** — see Section 1's git-root note.
- **The one remaining problem:** the live conversation session that did all this debugging never itself picked up the working registration, even after the user reported "restarting." A separately-spawned `claude` CLI process (run via Bash mid-conversation) saw the server as healthy immediately — proving the config is fine and the problem is that *this specific conversation's backing process* was never actually cycled. Likely cause: whatever "restart" action was taken reconnected the UI to a still-alive backend session rather than fully terminating and relaunching it.

**Action needed in the new window:** as soon as this new conversation starts, run `ToolSearch` for `select:manage_gameobject,manage_components,manage_scene,find_gameobjects,read_console,manage_editor,execute_menu_item,batch_execute,manage_camera` to confirm Unity tools are now loaded. Given the CLI already confirms a healthy connection, this should just work in a genuinely fresh session — no further config changes should be needed. If it's *still* not showing up, the next thing to check is whether this interface (given the `ccd_*`/session-management tools present) keeps a persistent backend process alive across what looks like a "new conversation," in which case a full application/environment restart (not just a new chat) may be required.

## 7. Longer-term roadmap (unchanged from original thesis handoff, for orientation)

After the Building Blocks/prefab task above lands:
1. First real `EvidenceDefinition` asset (kitchen knife) populated into `EvidenceStateManager`'s list
2. Photographer camera tool — first thing to actually call `EvidenceStateManager.Mark*()`
3. Procedural Gate Validator
4. STCS trigger/phrasing system (currently just an empty `STCSManager.cs` stub)
5. IOC evidence board deduction mechanic (drag-connect graph, NOT multiple choice — deliberate, load-bearing design decision, don't relitigate)
6. Unity offline sync queue (`PendingSyncManager`/`ConnectivityWatcher`/`SyncWorker`) — **note: this targets a Supabase backend that could not be found anywhere on this machine**, so this step may need the dashboard/Supabase project restored or rebuilt first
7. Splash screen, station lobby (static dressing only, no NPCs), sci-fi menu skin — cosmetic, low priority
8. Dashboard: deploy to Vercel, add sign-out button — **contingent on locating or rebuilding the dashboard project, which doesn't currently exist locally**

### Things not to re-litigate
- Scope stays at 1 scenario / 2 roles / STCS-simulated teammates — no 7-role, no multiplayer, no real AI NPCs
- Deduction mechanic stays evidence-board drag-connect, not quiz-style
- Grab interaction stays on Meta ISDK Building Blocks (Grabbable/HandGrabInteractable/GrabInteractable), not raw XRI `XRGrabInteractable` — this was the live tension flagged in the original doc; this session's approach (Building Blocks path) is the de facto resolution
- No in-VR authenticated dashboard; in-headset "Scores" stays a QR/URL pointer to the web dashboard

## 7b. DO NOT "FIX": identification feedback is deliberately identical

Placing an evidence tent produces exactly the same cue — same tone, same haptic
pattern, same scale pop, same toast wording — whether it lands on the murder weapon,
on the designed distractor, or on bare floor. **This is deliberate and must not be
"corrected" during a polish pass.**

Differentiating them would reveal identification correctness in real time. A player
could tent every object in the room and listen for which ones sounded positive,
which is the same hint-giving the free-placement design has ruled out since the
guided-vs-open decision — arriving through a side channel instead of through a
highlight or a restriction. Silence counts as a cue: a confirmation that fires only
for real evidence is a complete identification oracle.

Two signals behave **oppositely** here, and conflating them is the likely mistake:

| Signal | Requirement | Why |
|---|---|---|
| Procedural refusal (out-of-order action, blocked marker reclaim) | **Must be obvious** — clearly distinct from an accepted action | Protocol compliance is a hard-gated, transparent requirement; signalling it leaks nothing about which objects matter |
| Identification correctness (was that really evidence?) | **Must be hidden** — indistinguishable outcomes | It is the competency being assessed |

Three places enforce this, each carrying a do-not-change comment:

- `EvidenceTentTool.PlaceTent` raises the cue **unconditionally**, after
  `RecordPlacement` has returned — never from inside either branch — and its toast
  names the tent number, never an evidence id.
- `FeedbackDirector.HandleStatusChanged` **skips `EvidenceStatus.Marked`**. It only
  ever runs for registered evidence (only registered evidence has a status to
  change), so a cue routed through it would be silent on a miss by construction.
- `EvidenceNotifier` has **no `Marked` entry**, for the same reason. A toast reading
  "EVD-018 marked" on a hit and showing nothing on a miss is the same oracle in text.

**Known remaining leak, not addressed here:** `EvidenceNotifier` still toasts
"EVD-014 found" on proximity. Walking the room and watching which objects raise a
toast identifies the full evidence roster without marking anything. That is
pre-existing behaviour and changing discovery feedback is a design decision, not a
bug fix — it needs an owner. See §8.

## 7c. Chain of custody: `Sealed` sits between `Collected` and `Processed`

Real procedure bags evidence, applies a tamper-evident seal at the scene, and breaks
that seal later at the lab to analyse it. Sealing is what protects custody across that
gap, and the lifecycle previously had no representation of it at all — `Collected` went
straight to `Processed`.

`EvidenceStatus.Sealed = 8` now sits between them, with `Processed` moved to `9`.

**The fingerprint requirement moved with it.** `MarkFingerprinted` requires the item to
be `Sealed`, not `Collected`. This is deliberate and load-bearing: leaving it on
`Collected` would have created two gates on the way to `Processed` that could be
satisfied in either order — seal-then-dust, or dust-then-seal — and a chain of custody
that can be established *after* the item was already opened and dusted is not a chain of
custody. The path is strictly:

    Collected -> Sealed -> (fingerprint, if requiresFingerprinting) -> Processed

`GreyboxFlowTest` asserts both halves: that `Processed` is refused straight from
`Collected`, and that `MarkFingerprinted` is refused while the item is only `Collected`.
Either assertion failing means the ordering has been broken.

Sealing is a **deliberate player action**, not an automatic bump when `Collected` is
reached — same reasoning that made tenting deliberate rather than passive like `Found`.
A step the game performs on the player's behalf records nothing about whether the
player knew to perform it, and a student who bags evidence and walks away without
sealing it has made a real chain-of-custody error the log has to be able to show.

## 7d. Notification priority: ambient prompts are always preemptable

One label, one canvas group, two classes of message competing for them:

- **State-driven** (`NotificationManager.Notify`) — gate-block reasons, confirmations,
  status changes. Reports an event that already happened.
- **Ambient** (`NotificationManager.ShowPrompt` / `HidePrompt`) — a standing affordance
  hint, true while some context holds ("[B] Pick Up Evidence Tent").

**A state-driven message always preempts an ambient prompt. Never the reverse.** The
prompt resumes on its own once the queue drains, if its context is still true.

The reasoning, so this is not re-inverted: an ambient prompt restates something still
true and still visible — walk two steps and it comes back. A state-driven message is
the only report of an event, and not showing it at the moment it fires does not delay
it, it loses it.

`NotificationUI` originally had this backwards: `ShowPrompt()` paused the toast queue,
and `Show()` refused to start it while a prompt was active. Standing anywhere near a
tent therefore swallowed **every** refusal the player triggered — silently, with no
error and no log. Pulling a trigger and getting nothing back reads as a broken tool
rather than a refused action, which is exactly how it was reported.

The rule is expressed as two classes of message, never as checks on message text:
everything posted through `Show()` outranks everything posted through `ShowPrompt()`,
whatever either says, including messages added later. `NotificationPriorityTest` (in
the scene, run via `RunAndLog()`) samples the real label every frame across a real
posting sequence and asserts both the preemption and the resume, because internal
state is not the question — what the player actually saw is.

## 7e. Real art wired for three evidence items (EVD-015, 017, 018)

`Evidence_Bloodspatterpattern`, `Evidence_Victimsmobilephone` and
`Evidence_Emptyliquorbottle` no longer render as greybox cubes. Each now uses the real
asset from `Assets/_Project/Art/Props/`:

- **`EVD-018` (bottle)** → `GIN_30K.glb`. Raw mesh already stands upright (Y is the
  tall axis in both the raw model and the old greybox), so no rotation was needed —
  only a per-axis scale fit to the footprint the old cube used.
- **`EVD-017` (phone)** → `PHONE_100K.glb`. Raw mesh is authored standing on end;
  rotated `-90°` on X to lie flat, long axis along Z, thickness along Y. First
  attempt at the scale mapping swapped which local axis corresponds to length vs.
  thickness after the rotation, producing a badly stretched phone — caught and fixed
  by an actual screenshot, not by trusting the arithmetic.
- **`EVD-015` (blood spatter)** → `BLOOD.png` as a decal, not a mesh swap. Checked the
  texture's alpha channel first (2048×1024, ~82% transparent / 13% opaque / 5% soft
  edge — a real spatter mask, not an opaque square) before treating it as usable.
  Applied to a flat `Quad` (saved as a real asset, `Art/Meshes/Quad_FlatDecal.asset`,
  since a primitive's runtime mesh is not scene-persistent) via a new
  `Art/Materials/Mat_BloodSpatter.mat` (URP Lit, alpha-blend, double-sided, `_ZWrite`
  off), rotated to face up and laid flush on the floor.

Every change was confirmed with an actual Scene View screenshot
(`manage_camera` action=screenshot) after each step, not just by reasoning about the
numbers — the phone mis-scale above would have shipped unnoticed otherwise. `EvidenceProp.evidenceId` and world XZ position were left untouched on all three; only the mesh, material, transform scale/rotation, and the aim `BoxCollider` (resized to the new visual bounds) changed. Re-verified after: raycast from directly above each item still resolves the correct `EvidenceProp` through the resized collider, `GreyboxFlowTest.RunFullFlow()` still passes, and the radius/overlap table is unchanged (only vertical position shifted by 1.5cm on the blood decal, negligible against a 1.5m radius).

`EVD-014` (knife) was already wired to `knife-model.glb` from an earlier, unrecorded
pass — found while checking whether the other Props assets were already in use. It
uses a different convention: mesh/material live on a child `Visual` object rather than
directly on the evidence root. Worth converging on one convention before wiring the
remaining item, `EVD-016` (handrail fragment), which still has no matching prop file
in `Art/Props/` and is still a plain greybox cube.

**`Assets/_Project/Art/Props/` was never in version control until this change.** Every
`.glb`/`.png` in that folder — including `GIN_30K.glb`, `PHONE_100K.glb`, `BLOOD.png`,
and the ones NOT touched this pass (`SOCO_VAN_100K.glb`, `camera-model.glb`,
`camera-model2.glb`, `knife-model.glb`) — exists only on this machine, with no history.
Same class of exposure `HeacyASF.unity` was flagged for earlier this session: losing
this machine loses the source art with no way to reconstruct GIN_30K/PHONE_100K's
scale-fit numbers above except by re-deriving them from scratch against a different
mesh export. Should be committed alongside everything else in `Art/Props/`, not just
the three files this task touched.

## 7f. `Sketched` redesigned around one shared master sketch; two tools were never reachable

`Sketched` was, until this pass, a trivial instant call to `MarkSketched` — no real
interaction behind it, same as tenting and fingerprinting before their own fixes. Real
crime-scene sketching produces one spatial document with every item's numbered marker
plotted onto it, not a per-item drawing (unlike photography, which genuinely is
per-item). `Sketched`'s position in the sequence and its gate (still requires
`Photographed`, transitively `Marked`) are unchanged.

New: `MasterSketchManager` (data singleton, same shape as `PhotoAlbumManager`) holds one
shared `List<SketchAnnotation>`; `MasterSketchUI` reviews it (`Show`/`Hide`/`Toggle`,
`PlayerUIGate`, `LocomotionSuspender` — `PhotoAlbumUI`'s *behavioral* contract reused,
not its hand-authored prefab, since there's no artist-authored floor-plan background to
inherit; the panel is built at runtime instead, the way `UtilityMenuController` builds
its wheel). Wired into the utility menu as a new "Sketch" entry. `SketchTool.TrySketch`
now stamps the aimed item's position onto the shared sketch (auto-projected, not
freehand — this isn't assessing drawing skill) before reporting `MarkSketched`, same
gate-then-report shape every other tool already used.

This depended on a gap that had never been closed: nothing persisted which tent number
an item was marked with (`EvidenceTentTool` only ever computed it transiently for the
reclaim flow). Added `EvidenceRecord.tentNumber` — set by `MarkTented`, cleared by
`TryReclaimMarker` on a genuine revert — so the sketch can label an item by the same
number the player already sees on its physical tent.

**Two tools were never placed in any scene.** Checking whether `GreyboxFlowTest`'s
direct calls were hiding the same reachability gap the collector-wheel incident found
(see §8) turned up that `SketchTool` had never been placed anywhere — no prefab, zero
instances in `CSI_Environment` or `Tutorial_ToolTest` — and neither had `RecorderTool`,
which is not dead code: it's the only path from `Sketched`/`Logged` to
`ReadyForCollection`, i.e. load-bearing for reaching `Collected`/`Sealed`/`Processed` at
all. Both were fully-implemented, gate-integrated aim-and-activate tools with nowhere to
exist in the world. Fixed by duplicating a working tool (`EvidenceCollectorMagnifier`,
chosen because its structure — no per-item visuals, no aim/raise animation — is the
closest match) rather than hand-authoring from scratch, specifically to inherit its
exact `InputActionReference` bindings (`XRI Left/Right Interaction/Activate`) and
Oculus Interaction setup (`Grabbable`/`HandGrabInteractable`/`GrabInteractable`) rather
than risk a subtly-wrong recreation. Placed as `SketcherSketchpad` (X=0.6) and
`RecorderDictaphone` (X=0.9), continuing the existing prop-shelf row
(`EvidenceTentDispenser` X=-0.6, `PhotographerCamera` X=0, `EvidenceCollectorMagnifier`
X=0.3, all Y=0.945 Z=-1).

Verified genuinely reachable, not just state-machine correct: `PlayerToolRegistry.GetTool`
resolves both after entering Play mode; `PlayerToolRegistry.ToggleEquip` (the exact call
the tool wheel makes on release) attaches each to the hand anchor for real; and — since
the whole point of this check is that a direct method call proves nothing about real
input, per the wheel-wiring correction earlier this session — a genuine simulated
trigger pull (an `OculusTouchController` device added via `InputSystem.AddDevice`,
`{TriggerButton}`-usage press queued via `StateEvent`/`QueueEvent`, **not** followed by
a manual `InputSystem.Update()` call, which would consume the event before Unity's own
autonomous frame loop — the one `SketchTool.Update()`/`RecorderTool.Update()` actually
run on — ever sees it; confirmed with a real wall-clock wait between queuing and
checking the result) drove `EVD-014` from `Photographed` through `Sketched` and
`Logged` to `ReadyForCollection` for real, through `SketchTool.TrySketch` and
`RecorderTool.TryLog` exactly as a player's controller would. `GreyboxFlowTest` then
re-ran clean (`PASS`, 5/5, master sketch shows 5 distinct annotations) in a fresh Play
session against the now-correct scene.

## 7g. Sketch auto-fire flag; Collect/Seal redesigned as two-handed bagging

**Part 1 - `EvidenceStateManager.autoSketchAfterPhotograph` (default `true`).** Temporary,
not a removal: `MasterSketchManager`/`MasterSketchUI`/`SketchTool` are byte-for-byte
unchanged and still fully functional the moment the flag flips back to `false`. When
`true`, a successful `MarkPhotographed` immediately calls `MarkSketched` for the same
item with the same tool - no `SketchTool` interaction, and deliberately no
`MasterSketchManager.RecordAnnotation` call, since there was no player action to derive
a sketch position from. Exists because tenting has a clear, distinct assessment signal
(a judgment call about identity) and sketching, as currently scoped, doesn't yet have
one of its own separate from documentation already covered by Photographed/Logged.

`GreyboxFlowTest` branches on the flag rather than checking only final state: with it
`true`, it asserts `Sketched` already applied immediately after `Photographed` alone
AND that no annotation exists; with it `false`, it exercises the exact real-interaction
path from the previous task unchanged. Re-run clean under both states.

**Part 2 - Collect/Seal are now a two-handed bagging gesture, not aim-and-press.**
`EvidenceCollectorTool` (raycast, single-button, self-disambiguating by record status)
is **deleted outright**, not repurposed - its interaction shape shared nothing with the
new one. `ToolType.EvidenceCollector` is unchanged; only the physical object and
gesture behind it changed (magnifying glass -> bag), so the tool wheel's flavor text
for that role was updated to match.

Investigated before building anything: evidence props (`EVD-015`-`018`) were NOT
grabbable at all before this task - no `Grabbable`/`HandGrabInteractable`/
`GrabInteractable`, no `Rigidbody`. Only `EVD-014` was, and it carried `ToggleGrab`
(sticky grab-until-second-press) - the right model for a *tool* held indefinitely, the
wrong one for "carry a few feet to a bag, release naturally on arrival," so it was
removed from `EVD-014` and not added to the other four. All five evidence props got
`Rigidbody` (kinematic, no gravity - the exact configuration `EVD-014`'s own
fall-through-world fix already proved stable), `Grabbable`, `HandGrabInteractable`,
`GrabInteractable` (fields wired via `SerializedObject`, copied from `EVD-014`'s own
already-working values rather than guessed - `HandGrabPoses`/`_grabSource` both confirmed
optional by reading the SDK source, so no hand-pose authoring was needed), and a new
`EvidenceGrabGate` component that keeps `HandGrabInteractable`/`GrabInteractable`
disabled until `EvidenceStateManager.OnEvidenceStatusChanged` reports `ReadyForCollection`
- consistent with every other action in this project waiting until the moment it's
actually valid.

`EvidenceBagTool` (replaces the old class on the same prop, renamed `EvidenceCollectorMagnifier`
-> `EvidenceBag`) reads left/right trigger HELD state (`IsPressed()`, not
`WasPressedThisFrame()` - continuous, not one-shot) via the same `XRI Left/Right
Interaction/Activate` references every other tool uses. A child `ReceivingZone` trigger
collider (`EvidenceBagReceiver` forwarding `OnTriggerEnter`/`OnTriggerStay`) checks, on
overlap: left trigger held (open), right trigger held AND `Grabbable.SelectingPointsCount
> 0` on the specific item (right hand genuinely grasping it, not just touching it),
and the item's status is `ReadyForCollection`. All three true -> `TryInsert`: gate-check
(same `ProceduralGateValidator` every tool uses), swap mesh/material to a runtime-generated
translucent placeholder cube (no art dependency), force-release whatever hand grab holds
it (`HandGrabInteractable`/`GrabInteractable.SelectingInteractors` -> `ForceRelease()`,
same technique `ToggleGrab` already uses), disable every collider and grab component on
it, parent it to the bag, call `MarkCollected`. Releasing the left trigger while
something is inserted calls `TrySeal` -> `MarkSealed`, then auto-detaches the item into
a new placeholder `EvidenceHoldingCrate` (no table/crate/station existed anywhere in the
scene to attach to instead - checked the full root hierarchy first), freeing the bag.
`insertedItem != null` guards insertion to exactly once per overlap.

**A real bug found only by physically verifying this, not by reading the code:** the
receiving zone's trigger collider never fired, because `PlayerTool.Awake()`'s
`SetEquippedVisualState(false)` disables *every* collider under the tool's GameObject
via `GetComponentsInChildren<Collider>(true)` - the zone included, since it's a child.
`EvidenceBagTool` now overrides `EquipToHand`/`Holster` to explicitly re-enable/disable
the receiving zone's collider in step with equip state, since it's a detector, not the
tool's own "don't let a hand grab this mid-air" collider that rule was written for.

**Verification, both parts, at increasing rigor:**
- `GreyboxFlowTest.RunFullFlow()` passes clean under both `autoSketchAfterPhotograph`
  states, and its Collect/Seal steps now route through `EvidenceBagTool.TryInsert`/
  `TrySeal` (public specifically for this) rather than `MarkCollected`/`MarkSealed`
  directly - the same discipline applied to the Sketched step last task.
- Grab-gating confirmed genuinely off before `ReadyForCollection` and on after, by
  reading `HandGrabInteractable.enabled`/`GrabInteractable.enabled` directly.
- The full physical gesture was driven for real in Play mode: genuine `InputSystem`
  device-level trigger presses (the proven recipe from the wheel-wiring work), a real
  collider-overlap (the item physically moved into the zone), and confirmed that
  overlapping with no trigger held, and with only the right trigger held, both produce
  no state change - only both together inserts. Releasing the left trigger afterward
  produced a real `Sealed` and a real reparent into the crate.
- **One disclosed substitution, not a full pass:** the controller-to-`GrabInteractor`
  activation chain (`ActiveStateTracker` -> `ControllerGrabInteractor`) reports `State:
  Disabled` in this headless, no-XR-device Editor session, so `ForceSelect` on it had no
  effect. "Right hand grasping" was instead driven directly through `Grabbable`'s own
  production pointer-tracking (`PointableElement.ProcessPointerEvent` with a real
  `Select` event) - genuinely produces `SelectingPointsCount > 0` through real SDK code,
  the same field `EvidenceBagTool` reads, but bypasses the controller-input layer
  specifically. Everything downstream of "is this genuinely grasped" was exercised for
  real; the controller-to-grasp link itself was not, and can't be in this environment
  without an actual or simulated XR device.

## 7h. Evidence grab rebound to an explicit grip action; hand assignment enforced

Closes the gap §8 used to carry as its top-priority headset-only item - see the
`[x] RESOLVED` entry there for the full technical account (what the grip signal
actually was, why it was untestable, what replaced it, and everything the simulated
verification confirmed). Two smaller, independently useful things came out of it:

- **`PlayerTool.PreferredHand`** (new, defaults to `Right`) - `ToolWheelController`
  now resolves which hand anchor to equip a tool to from the tool itself, rather than
  hardcoding `rightHandAnchor` for every role. `EvidenceBagTool` is the first override
  (`Left`). Adding a future left-hand tool means overriding one property, not teaching
  the wheel a new special case.
- **`RightHandOnlyFilter`** (new, `IGameObjectFilter`) - a small, reusable component for
  "this interactable may only be selected by the right hand's interactor," usable
  anywhere else in the project a similar restriction is ever needed, not just here.

`EvidenceBagTool.TryInsert`/`TrySeal` and everything after a grasp is confirmed are
byte-for-byte unchanged - `GreyboxFlowTest.RunFullFlow()` re-ran clean, confirming this
was a pure substitution of what feeds `IsFirmlyGrasped`, not a change to anything
downstream of it.

## 8. Open verification debts (added after the evidence-tenting / fingerprinting pass)

These are things that are *implemented and passing today* but whose real code path
has never actually executed. They are cheap to note now and expensive to rediscover
mid-headset-pass, so they are line items rather than "probably fine".

- [x] **RESOLVED — `GetComponentInParent<EvidenceProp>()` no longer decides which item
      a tent marks.** The parent-walk debt is gone because the call is gone: tent
      attribution is now proximity-based (`EvidenceProp.FindNearestWithinRadius`,
      nearest item whose own `interactionRadius` contains the placement point), so a
      prop's collider hierarchy no longer affects which item gets marked. The raycast
      still decides *where* the tent visually lands, which is a rendering concern with
      no scoring consequence.

- [x] **RESOLVED — discovery toasts are now gated per scenario, and OFF for
      `CSI_Environment`.** The tier question is answered for this scene: it is
      assessment-grade, no hints. `EvidenceNotifier.announceDiscovery` (serialized,
      defaults **true**) gates the `Found` toast; `CSI_Environment` sets it **false**.

      Deliberately data-driven rather than a hardcoded "Found is always silent" rule,
      because it is a per-scenario policy: `Tutorial_ToolTest` is a tool testbed where
      naming the item you walked up to is a teaching aid, not a leak, and it keeps the
      toast (field absent from its YAML, so it takes the `true` initializer). The
      default is `true` specifically so no other scene changes behaviour.

      For `CSI_Environment` the toast is suppressed **entirely** — no substitute cue,
      not even a nameless "something nearby". Directional information the player did
      not generate themselves is still a hint. `Found` is a passive backend signal with
      no required player-facing consequence, unlike marking or a blocked gate, which
      the player must be able to read.

      Presentation-only, same boundary as the marking-cue fix: `MarkFound` still fires,
      the record still advances, `SessionLogger` still writes its `EvidenceStatusChanged`
      entry, and the gate still opens. Only the sentence on screen is suppressed —
      verified against the session log.

      No `Generic` enum case was built. A non-naming presence cue is a plausible future
      option for some other scenario, but building an unused branch now would leave it
      to rot.

- [ ] **`interactionRadius` has never been tuned in a headset.** Every item is on the
      1.5 m default, inherited from the old hardcoded `EvidenceProp.noticeRadius` so
      that moving the number into data changed nothing about how the scene plays. That
      default was chosen for *discovery* ("close enough to notice this exists") and is
      now also doing *attribution* ("close enough that a tent here means this item").
      1.5 m is plausibly too generous for the second job — two tents a metre apart both
      resolve to the same item — but the right value is a playtest finding, not a guess.
      Turn on `GreyboxFlowTest.showEvidenceRadii`, walk the scene, and set them per item.
      **Treat 1.5 m as unverified until that happens.** Discovery and attribution
      sharing one number was a coincidence of the old hardcoded field, not a design
      decision; now that it is an explicit tunable it has to be shown to feel right for
      *both* jobs, not just the one it was originally sized for.

- [x] **RESOLVED — the controller-to-grab activation chain gap is genuinely closed,
      not just narrowed.** Traced (not guessed): the grab signal was
      `Oculus.Interaction.ControllerSelector` (GameObject `GripButtonSelector`, a
      child of each `ControllerGrabInteractor`) reading `ControllerButtonUsage.GripButton`
      via Meta's own `IController`/OVR abstraction - a genuinely separate pipeline from
      this project's `InputSystem`/XRI actions, confirmed by reading the SDK source, not
      assumed. Deeper still: the whole interactor is gated by `ActiveStateTracker`
      reading `ControllerRef.Active => IsConnected` - a controller-*presence* gate one
      layer beneath the grip button itself, which is the real reason `ForceSelect` was a
      no-op in the no-device Editor session that found this gap originally, and why
      fixing "what button" alone could never have closed it.

      Fixed by no longer trusting that opaque chain for the bagging feature's own gate:
      `EvidenceBagTool.IsFirmlyGrasped` now reads an explicit `rightGripAction`
      (`XRI Right Interaction/Select` - reused, not invented; the same action
      `ToggleGrab` already read for its release button) plus a real, self-computed
      proximity check (`Vector3.Distance` between the right hand anchor and the item,
      ≤15cm - deliberately tight, "real reach and touch," not `interactionRadius`-scale).
      Both halves are now things this project's own `InputSystem` device injection can
      drive for real, and both were: simulated right grip pressed near a
      `ReadyForCollection` item produced a genuine `Collected`, sealed, and detached to
      the crate - the full gesture, grasp signal included, with zero SDK-opaque
      substitution. Confirmed the negative cases too: grip held far from the item does
      nothing until real proximity; a simulated *left*-grip press does nothing at all
      (the check never reads it); releasing grip drops the "grasped" signal on the very
      next check, no stickiness (the check is stateless, recomputed fresh every time -
      there was never anything to latch).

      Hand assignment is now enforced, not incidental, on both sides: `EvidenceBagTool`
      overrides `PreferredHand`/`EquipToHand` and hard-refuses attaching to anything but
      the left hand anchor (confirmed by direct call, not just inspection - a right-hand
      attach attempt is a no-op); every evidence prop carries a new `RightHandOnlyFilter`
      (`IGameObjectFilter`, checking `IController.Handedness`) on its
      `GrabInteractable`/`HandGrabInteractable`, confirmed via the scene that there
      genuinely are separate left/right `GrabInteractor` instances in this rig (checked,
      not assumed) so the filter has something real to distinguish.

      `Grabbable`/`HandGrabInteractable`/`GrabInteractable` stay on evidence props
      unchanged, still real physical-carry components for whenever this runs on actual
      hardware (where `IsConnected` will be true and that whole chain will work as
      designed) - only the bagging feature's own gate stopped depending on them.

- [ ] **`EvidenceProp` is a runtime registry now, so scene-loading order matters.**
      Props register in `OnEnable` and unregister in `OnDisable`, and
      `InteractionRadius` deliberately does *not* cache while
      `EvidenceStateManager.Instance` is still null (its definition isn't reachable
      yet), falling back to the serialized `noticeRadius` for that window. If a scenario
      ever spawns evidence additively or after the managers, confirm the radius that
      ends up on the trigger collider is the one from the definition, not the fallback.

- [x] **RESOLVED — `EVD-014` (kitchen knife) no longer falls out of the world.** Two
      things were wrong and both are fixed. It was the only evidence prop in the scene
      with a `Rigidbody` at all (it is the only *grabbable* one — `Grabbable` +
      `HandGrabInteractable` + `GrabInteractable` + `ToggleGrab` — which is why it has
      one), and it was non-kinematic with gravity on: now `isKinematic = true`,
      `useGravity = false`, matching the kinematic-grabbable rule `PlayerTool` already
      applies to every tool for the same reason. Separately it was standing at
      z = 3.97, which is **1.06 m past the floor `Plane`'s edge** (the plane spans
      z ∈ [−7.09, 2.91]) — there was genuinely no floor under it, removed or otherwise.
      Moved 1.43 m to (1.28, 0.089, 2.70): on the plane, collider resting at
      y = −0.0002, and positioned so no two items' 1.5 m radii overlap (closest pair is
      now 3.11 m against a 3.0 m sum). Verified stable across a 33 s Play session with
      the `Rigidbody` asleep.

      The other four (`EVD-015`–`EVD-018`) have **no `Rigidbody` at all** and so carry
      no version of this risk — they cannot be simulated, cannot fall, and all four sit
      within the plane's bounds.

- [ ] **No VR-input pass on evidence tenting.** The placement branch was verified by
      invoking `RecordPlacement` directly and the reclaim rules by invoking
      `EvidenceTentPickup.TryReclaim` directly. The state machine, the gates and the
      logging are genuinely exercised; *nobody has pressed a physical trigger with the
      dispenser in hand.* Trigger binding, ghost preview alignment and pickup radius
      are unverified.

- [ ] **`FingerprintStationPlaceholder` is a placeholder, and it is the only caller of
      `MarkFingerprinted`.** It is now present in `CSI_Environment` under `_Managers`
      (it was briefly a gate with no key — `requiresFingerprinting` was blocking
      `Collected → Processed` on four of five items with nothing in any scene able to
      satisfy it). It is deliberately crude: proximity + trigger, processes a fixed id
      list at once, and cannot be failed. **A step that cannot be failed measures
      nothing**, so this must be replaced by a real dusting/lifting interaction before
      any procedural-compliance number derived from it means anything.

- [ ] **REACHABILITY IS NOT COVERED BY ANY AUTOMATED TEST — weigh "PASS" accordingly.**
      `GreyboxFlowTest` calls `EvidenceStateManager` methods directly. It never equips
      a tool, never opens the tool wheel, and never sends input. So a green run proves
      the **gated logic is correct**; it proves nothing about whether a player can
      **reach** that logic. Two live examples of the gap, both found by hand and
      neither by the test:

      1. `SketchTool` and `RecorderTool` are absent from `CSI_Environment`, so nothing
         a player can hold produces `Sketched` or `Logged`. Every collection-related
         PASS this project has recorded was validating a transition no player could
         actually trigger, because the run is hard-blocked two steps earlier.
      2. The notification priority inversion (§7d) silently discarded every refusal
         message while an ambient prompt was up. Nothing errored; the test could not
         see it, because the test never reads the UI.

      Treat an automated-only pass as evidence about logic, never about playability.
      Anything claimed as reachable needs a real input path exercised end to end.

      Example 1 update: `SketchTool` and `RecorderTool` are now placed in
      `CSI_Environment` (§7f) and confirmed reachable — but by hand, via a genuine
      simulated `InputSystem` device event, not by any automated test. The general
      point stands: `GreyboxFlowTest` still doesn't send input and still can't catch a
      third instance of this on its own. Don't add a fourth tool without placing it.

- [ ] **`AimTargetOutline` still leaks evidence-hood via a white box, independent of
      status.** While fixing the viewfinder's green confirmation (which now correctly
      requires the aim target be exactly `Marked` — see `PhotographTool.
      IsConfirmedForCapture`, `ViewfinderFrameMask.SetConfirmed`,
      `ViewfinderConfirmationFeedback`, and the now-corrected `AimIndicatorUI`, which
      used to read the much broader `CanCapture` and light up green for any evidence
      prop in frame regardless of status), a second, separate leak of the same shape
      was found and deliberately left alone: `AimTargetOutline` draws a white wireframe
      box around any `EvidenceProp` in frame — `Found` or not, `Marked` or not — so a
      player can already tell "this specific object is evidence" from the outline
      alone, before doing anything to earn that knowledge. Not fixed this pass because
      it's a different visual (aim-assist outline, not a status confirmation) with its
      own design tradeoff the outline may have been intentionally built for. Two ways
      to close it when this gets picked up: make the outline generic (show for
      whatever the raycast hits, evidence or not), or remove it now that the frame
      tint + dot already confirm the moment to act.

- [ ] **`GreyboxFlowTest` drives the state machine, not the tools.** It proves the
      sequence, the gates and the logging end to end (`Assets/_Project/Scripts/Testing/
      GreyboxFlowTest.cs`, run `RunFullFlow()`), which is exactly why the earlier
      ad-hoc version failing to exist mattered when `Marked` was inserted. It is not a
      substitute for playing the scenario.

- [x] **RESOLVED — `EvidenceScorer` no longer keeps its own copy of the lifecycle
      order.** The canonical sequence is now an exported artifact: the ground-truth
      export carries a `lifecycleSequence` field read (by reflection) straight from
      `EvidenceStateManager.RequiredSequence`, and the scorer has no hardcoded order
      left. A ground-truth file missing that field is a hard failure with a
      regenerate-it message, never a silent fallback. Remaining soft spot: the
      exporter reads a *private* field reflectively, so renaming `RequiredSequence`
      is not caught by the compiler — it fails loudly at export time instead.
