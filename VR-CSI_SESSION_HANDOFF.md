# VR-CSI Unity Setup — Session Handoff (continue in new window)

**Purpose:** A living handoff, appended to across many sessions (not a single session's notes) — read it as the current source of truth for this Unity project's state, conventions, and open debts, not as a play-by-play. Paste this whole document as your first message in a new session so it has full context without re-deriving anything. Sections are numbered `1`–`8` for the original stable baseline, with lettered sub-sections (`7b`, `7c`, ... `7m` and counting) each appended by whichever session did that chunk of work — read them in order, since a later letter can supersede an earlier one's description of the same system (§7i onward supersedes §7f/7g's description of the evidence lifecycle, for example — see §7i for exactly what changed). **If you do a substantial chunk of work in this project, extend this document the same way** — the next lettered sub-section under the relevant numbered heading, or a new numbered section if it doesn't fit an existing one — rather than starting a separate handoff file. The original MCP-connection issue this doc was first written to solve (§6) was resolved long ago and does not need re-reading unless a similar MCP-connection problem resurfaces.

**§0a is not optional context — read it before touching anything.** It is the one standing rule every session that has touched this project has been held to, restated here because it has already cost real rework twice: nothing typed into `execute_code` (or any other scratch buffer) is a deliverable until it exists in a real, committed file. A live probe is evidence that something works; it is not the thing itself.

---

## 0. What this project is (background, unchanged from thesis doc)

Rolando's thesis at Partido State University – Lagonoy Campus: a VR-based Crime Scene Investigation training simulator (Quest 3S, Unity) + web instructor dashboard, built around the CAMIL framework. Actual 12-week build is scoped down to **1 scenario** (indoor kitchen homicide), **2 playable roles** (Photographer, IOC/Case Analyst), **5 non-played roles simulated via STCS** (scripted comms, no AI/NPCs). Full 7-role/multiplayer is future work — do not re-expand scope.

**Important correction from an earlier handoff doc reviewed this project:** that doc claimed a Next.js + Supabase dashboard, `docs/DEV_GUIDE.md`, and a `vr-csi-simulator/` monorepo structure were "done." **None of that existed on this machine at the time this note was written** — verified by exhaustive filesystem search.

**UPDATE, later session — the dashboard now genuinely exists, this correction is stale.** It lives in a completely separate repo/folder, not under this Unity project: `C:\Users\Roduf Eleu\Desktop\vicsi-dashboard`, remote `https://github.com/JiehsDev/vicsi-dashboard.git`, branch `main`. Next.js + Supabase, exactly as originally claimed — just not where the original doc said to look, and not at the time this note was first written. It is a session-based instructor dashboard (an `EvidenceTimeline`/`StudentSessionTable` view per play session, replacing an earlier per-student/class KPI-comparison layout), with its own `supabase/migrations/` and `supabase/seed/` scripts. Treat it as a real, actively developed sibling project — this Unity repo (`vicsi-client`, remote `https://github.com/JiehsDev/vicsi-client.git`) and the dashboard (`vicsi-dashboard`) are two separate git repos on the same machine, not one monorepo. Anything AI-side that needs to touch the dashboard is a separate `cd`/session into that folder, not a subfolder of this one.

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

**Likely stale — not re-verified.** This section describes the very first task ever
queued for this project. Everything documented from §7b onward (a fully working
evidence lifecycle, grabbable evidence props with real gating, a whole roster of
tools) could not exist if locomotion/grab were never resolved, so this was almost
certainly done at some point in a session that never updated this section to say so.
Left as-is rather than guessed-and-rewritten — if you land here believing movement or
grab doesn't work, verify against the live scene first; don't trust this heading's
"not yet done" at face value this many sessions later.

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

The **Crime Scene Sketch** (`Scripts/Sketch/`) follows the same rule and carries the
same do-not-change comments:

- An evidence element unlocks only at or past `PhotographedRaw` (the Evidence
  Procedure Map's rule), never on proximity-driven `Found`.
- `SketchEvidenceAnchor` drop zones have **no runtime visuals**. Every wrong evidence
  drop — another item's anchor or open board — gets the identical bounce and message,
  so probing the board never reveals where something else belongs.
- Measurement boxes appear only after their element is placed, and reference lines
  only after the distance is filed.

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

## 7i. `Sketched` is gone. Documentation flow is now two photographs around a chalk outline.

**This restructure is not otherwise documented anywhere in this handoff — it happened
in a session that never wrote itself up here.** What follows is the current, verified
state, not a session narrative like 7b–7h above. `SketchTool.cs`, `MasterSketchManager.cs`
and `MasterSketchUI.cs` (§7f/7g above) **no longer exist on disk at all** — confirmed by
a filesystem search, not inferred. Treat all of §7f and §7g as **historical background
on why certain design decisions exist (two-handed bagging, `PreferredHand`,
`RightHandOnlyFilter`), not as a description of the current lifecycle** — the sketch
mechanic they describe building has since been deleted outright.

`EvidenceStateManager.RequiredSequence` today is:

    NotFound -> Found -> PhotographedRaw -> ChalkMarked -> Marked -> PhotographedFinal
             -> Logged -> ReadyForCollection -> Collected -> Sealed -> Processed

Two photographs of the same item at two different, meaningful moments, not one:
`PhotographedRaw` (the undisturbed scene, before any outline or tent) and
`PhotographedFinal` (the documented scene, with the numbered tent in frame) — replacing
what used to be a single `Photographed` status. `ChalkMarked` sits between them: the
examiner chalks an outline around the item before the tent goes down. `PhotographTool`
now has two possible outcomes instead of one, decided by the item's current status, not
by a separate tool or action type. `RecorderTool` still exists and still produces
`Logged` (see §7m below for how it now gets there) — `Sketched` was retired rather than
replaced one-for-one, on the reasoning captured directly in
`EvidenceStateManager.cs`'s own comment on `RequiredSequence`: it added no assessment
signal of its own once `ChalkMarked` and the two photo states existed.

Because every tool checks `CanTransition`/`CanAdvanceTo` against `RequiredSequence`
rather than naming a predecessor status directly, this reorder is reportedly all it
took to change what every tool requires — no tool needed special-casing beyond
`PhotographTool` itself. Whoever did this restructure evidently followed the same
discipline this doc has documented elsewhere; there just isn't a session record of the
work itself, only its current, correct end state.

## 7j. Real-time chalk tracing built for `ChalkMarked` — the project's first continuous interaction

Every other tool up to this point (tent, bag, fingerprint kit, drag-and-drop report) is
a discrete single-trigger action: aim, press, done. `ChalkMarked` needed something a
real examiner actually does over time — trace an outline — so this is the first
mechanic in the project that tracks motion frame-by-frame rather than reacting to one
input event. Two new files carry it: `ChalkMarkTool.cs` (the grabbable prop, tracks
`chalkTip` position every frame while the trigger is held) and `EvidenceChalkGuide.cs`
(the per-item visual ring and coverage state).

**Design decisions, with the reasoning, so they aren't re-litigated:**

- **Distance-based tracing, not physics colliders.** A per-frame `Vector3.Distance`
  check from `chalkTip` against each guide point's position, not a trigger-collider
  ring. A thin ring of colliders against a small, fast VR-hand-speed tip is a classic
  tunneling risk (the tip can pass through between physics frames without ever
  overlapping); sampling a known Transform's current position every frame has no such
  gap.
- **No order or direction enforcement, and progress never resets on trigger release.**
  The guide tracks which of its points have ever been covered, permanently, regardless
  of which order they were hit in or whether the player let go and came back. This
  matches how a real examiner actually chalks an outline — not in one continuous
  unbroken stroke, and not necessarily starting from the same point twice.
- **Completion at a threshold, not literal 100% coverage.**
  `EvidenceDefinition.chalkCompletionThreshold` (default `0.9`) — the last few points
  of a real trace are easy to miss by a hair and shouldn't hard-block completion.
- **Tolerance scales with the item's own circle radius, not a fixed metres value.**
  `EvidenceChalkGuide.ToleranceFraction = 0.5f` (a covering touch must land within half
  the circle's own radius of a point). A fixed absolute tolerance would be far too
  generous for a small item's circle and far too tight for a large one — this roster's
  circle radii vary a lot (default `0.15f`, the knife overrides to `0.22f` — see
  `EvidenceDefinition.chalkCircleRadius`/`chalkPointCount` (default `24`)/
  `chalkCompletionThreshold`).
- **Nearest-active-guide resolution, no radius gate.**
  `EvidenceChalkGuide.FindNearestActive` picks whichever active guide's centre is
  closest to the tip, with no cutoff distance — deliberately different from
  `EvidenceProp.FindNearestWithinRadius` (used for tenting), because the chalk tip is
  expected to be right next to whatever's actually being traced; a radius gate would
  only risk a genuine trace near a small item's circle failing to resolve because the
  player's hand strayed past an arbitrary cutoff.
- **Guide visibility gated on tool-equip state, spawn gated on `PhotographedRaw`.** A
  guide is created the moment any item reaches `PhotographedRaw` (static event listener,
  same reactive-spawn pattern `SessionLogger`/`ProceduralGateValidator` already use),
  but its rings only render while the Chalk tool is actually equipped
  (`PlayerToolRegistry.OnToolGrabbed`/`OnToolHolstered`) — so the outline doesn't clutter
  the scene for tools that have nothing to do with it.
- **`transform.up` is not a reliable "away from surface" assumption across this roster.**
  Investigated, not assumed: three different rotation conventions are genuinely live —
  flat-decal items need `-forward`, the phone needs unnegated `forward`, upright items
  correctly use default `up`. Fixed by adding `EvidenceProp.SurfaceNormalAxis`
  (`LocalUp`/`LocalForward`/`LocalDown`/`LocalBack`) and
  `SurfaceNormalLocalAxis`/`SurfaceNormal` properties, authored per item; the chalk
  guide's circle is built in local space and rotated onto whichever axis the item
  declares, rather than hardcoding `Vector3.up`.
- **Guide built in the item's own local space, under its own transform.** So a
  grabbable item (the knife) drags its guide along if picked up mid-trace. A
  counter-scale is applied (`1/lossyScale` per axis) because some props in this roster
  are decal-style GameObjects scaled down non-uniformly purely for rendering size — an
  un-countered guide would inherit that scale and collapse to a few millimetres,
  silently defeating the tracing check for that item. Confirmed as a real bug during
  the build, not a hypothetical.
- **Detaches to world-space on completion (`MarkPermanent`), not kept as a child.** A
  chalk outline documents where the item was found, not a property of the item — it
  must stay at the original scene location even after the item is later collected and
  moved into the evidence crate. This also sidesteps a real interaction:
  `EvidenceBagTool.ApplyBaggedVisual` disables every child renderer under a prop that
  has an authored sealed-bag model (knife, phone); an undetached guide would have gone
  invisible the moment its own item was bagged.
- **Continuous connected-segment visual, not separate dot markers.** A
  `LineRenderer` per arc between adjacent points, recoloured (not repositioned) as its
  starting point is covered — reads as "a circle" at a glance regardless of coverage
  state, which separate dots with visible gaps did not.

`ChalkMarkTool.TryTraceAt` is public specifically so `GreyboxFlowTest` can drive it
directly with real guide-point positions (`EvidenceChalkGuide.GetPointPosition`), the
same discipline `SwabTool.TrySwab` already followed.

## 7k. Blood on the knife redesigned as a swab *precondition*, not a second `EvidenceProp`

**The problem, found by direct observation, not theory:** the knife (`EVD-014`) and a
co-located blood-on-blade item used to both be independently registered
`EvidenceProp`s in the same physical space. `PhotographTool.UpdateAimTarget` resolves
its aim target by **closest-to-camera among all in-frame, unoccluded, registered
`EvidenceProp`s** — a fundamentally different mechanism from tenting's radius-based
`EvidenceProp.FindNearestWithinRadius`. Two co-located props made that resolution
ambiguous: the user confirmed live that the camera was photographing the blood decal
instead of the knife. (A separate, unrelated bug briefly stacked speculative fixes on
top of this one before the real cause was found: `Evidence_KitchenKnife` had lost its
own `EvidenceProp` component entirely at some point — every system, not just
`PhotographTool`, was blind to it. Fixed by restoring the component; a good reminder
that "the chalk guide for the knife isn't appearing" and "the camera is aiming at the
wrong co-located item" can look identical from the player's chair and need separate
root-cause work.)

**The fix actually chosen (an architecture change, not a targeting patch):** the blood
trace is no longer an independent `EvidenceDefinition`/`EvidenceProp` at all. It is now
a *precondition* on the knife's own `Collected` transition —
`EvidenceDefinition.requiresSwabbing` (bool), mirroring `requiresFingerprinting`'s own
pattern but gating `Collected` instead of `Processed` (the physical sample has to be
taken before the item is bagged and removed from the scene, not after it's already at
the lab). `EvidenceRecord.swabbingDone` is the matching per-item flag.
`EvidenceStateManager.IsBlockedBySwabbing`/`MarkSwabbed` are the mirror image of
`IsBlockedByFingerprinting`/`MarkFingerprinted` — `MarkSwabbed` requires the item be
exactly `ReadyForCollection` (any earlier and there's nothing to swab yet in the
documented procedure; any later and the item is already being bagged, which real
procedure doesn't allow reopening for).

Physically, the blood decal GameObject
(`Evidence_KitchenKnife/Evidence_BloodOnKnifeBlade`) keeps existing at its original
co-located transform, but its old `EvidenceProp` component was replaced with a new,
much lighter one: `EvidenceSwabRequirement` (`evidenceId` pointing at the parent item,
a static registry/`FindByEvidenceId`, nothing else — deliberately **not** an
`EvidenceProp`, so it stays invisible to `PhotographTool`'s targeting and every other
system that walks `EvidenceProp.All`, which is exactly what removes the co-location
ambiguity at the source instead of patching the targeting logic). `SwabTool` was
extended with a second, parallel contact-tracking path for this
(`requiredSwabContact: EvidenceSwabRequirement`, alongside its original
`tipContact: EvidenceProp` for real evidence samples) and a new
`TryRequiredSwab(EvidenceSwabRequirement)` method — single-touch, calls
`MarkSwabbed` directly, no `hasSample`/contain/seal steps, since this swab exists only
to satisfy a precondition, not to produce a real collected sample of its own.

`EVD-019` (an earlier, short-lived independent `EvidenceDefinition` for this same blood
trace, created and then abandoned mid-session before this redesign was settled on) was
deleted outright — both its `.asset`/`.asset.meta` and its entry in
`EvidenceStateManager.sceneEvidenceDefinitions`/`GreyboxFlowTest.evidenceIds`. The
scenario's evidence roster is genuinely five items again (`EVD-014`–`EVD-018`), not six.

**Verified live** (Play mode, direct calls — see §0a on what this claim does and
doesn't cover): drove `EVD-014` through the full sequence to `ReadyForCollection`,
confirmed `MarkCollected` is refused with `IsBlockedBySwabbing`'s reason string,
called `EvidenceSwabRequirement.FindByEvidenceId("EVD-014")` +
`SwabTool.TryRequiredSwab` and confirmed it returns `Applied` and sets
`swabbingDone = true`, then confirmed `MarkCollected` now succeeds. **Not yet
re-verified:** `GreyboxFlowTest.RunFullFlow()` itself (it does have a `requiresSwabbing`
branch written for this — see its source — but hasn't been re-run as one integrated
call since this redesign landed; the check above drove the same calls by hand instead).

## 7l. `EvidenceGrabGate` made swab-aware — grabbing gated the same as collecting

Direct follow-up correction, from the user: "before the knife can be grabbed or
collected the blood in it should be first swabbed." §7k above only closed the
`Collected` half — `EvidenceGrabGate` (§7g/7h) still enabled physical grab
(`HandGrabInteractable`/`GrabInteractable`) the instant an item reached
`ReadyForCollection`, with no knowledge of the swab precondition at all. A player could
already pick the knife straight up and walk off with it before ever swabbing —
destroying the very trace the swab step exists to capture, and reaching that state
through a completely different code path (`EvidenceGrabGate`) than the one `IsBlockedBySwabbing`
protects (`EvidenceStateManager.SetStatus`).

Fixed by adding a new static event, `EvidenceStateManager.OnEvidenceSwabbed` (fired
from `MarkSwabbed` on a real `Applied` outcome, not on a harmless `Duplicate`) —
necessary because swabbing is a per-item **flag**, not a status in `RequiredSequence`,
so it was invisible to `EvidenceGrabGate`'s existing `OnEvidenceStatusChanged`
subscription (the item's status typically doesn't change at all at the moment
swabbing happens; it stays `ReadyForCollection` either way).
`EvidenceGrabGate` now subscribes to both events and funnels either one into a single
`RefreshGrabState()` that recomputes from the record's actual current state
(`status == ReadyForCollection && (!requiresSwabbing || swabbingDone)`) rather than
trusting whichever event fired — `ReadyForCollection` and the swab can genuinely arrive
in either order.

Verified live in Play mode: drove `EVD-014` to `ReadyForCollection` with swabbing not
yet done and confirmed both `handGrab.enabled`/`grab.enabled` read `false`; called
`SwabTool.TryRequiredSwab` and confirmed both flip to `true` on the very next check
with no status change in between; confirmed `MarkCollected` succeeds afterward. Not yet
confirmed in a real headset — only via a direct-call probe, per §0a.

## 7m. `RecorderReportUI`: real drag-and-drop replaced with thumbstick+button navigation — the Meta ray-interactor pipeline was never viable in this project

**Original design (now replaced):** `RecorderReportUI` (opened by `RecorderTool` once
an item reaches `PhotographedFinal` and has authored `reportLineOptions`) let the
player drag `ReportLineChip` cards from a candidate pool into a Notes zone, real
`IBeginDragHandler`/`IDragHandler`/`IEndDragHandler`/`IDropHandler` UI events routed
through `Oculus.Interaction.PointableCanvasModule` → uGUI's `EventSystem`. Its own doc
comment claimed this same pipeline was "already used successfully by the Deduction
Board's `EvidenceNode` in this exact scene." **That precedent turned out not to exist**
— `EvidenceNode`/`EvidenceBoardController` live in `Main.unity`, are hand-authored
entirely by hand in the Editor, and `Main.unity` has no standalone `PointableCanvas`
component anywhere (only the one `PointableCanvasModule` on the scene's `EventSystem`,
which alone does nothing without a `PointableCanvas`+`RayInteractable`+`ColliderSurface`
+`Collider` chain on the actual canvas). Nothing in this project had ever actually
proven that pipeline worked end to end.

**Root-cause chase (systematic-debugging, three real fixes, still broken, then a
different root cause entirely):**
1. Added a `PointableCanvas` to the runtime-built canvas. Still didn't drag.
2. Added the full chain a world-space canvas actually needs for a Meta ISDK ray
   interactor to reach it at all — `BoxCollider` (what `RayInteractor` physically
   raycasts against) → `ColliderSurface` (wraps it as an `ISurface`) →
   `RayInteractable` (what the ray interactor selects) → `PointableCanvas` (turns that
   selection into synthetic uGUI `PointerEventData`). Verified via a real
   `Physics.Raycast` from `Camera.main` hitting the new collider. Still didn't drag.
3. Holstered whatever tool was equipped while the panel is open, on the hypothesis that
   an equipped tool's own input was contesting the hand. Verified the holster/re-equip
   round-trip worked correctly. Still didn't drag.
4. Added `OnPointerEnter`/`OnPointerExit` hover-highlighting to `ReportLineChip` as a
   diagnostic, then **asked the user directly**: does a controller ray work normally
   elsewhere in the headset? Answer: yes. So the pipeline is alive in general — but
   asked to point the ray at a candidate-line card specifically and watch for the
   highlight: **it never fired.** The ray reaches other UI in the game; it never
   reached this canvas. Separately, live inspection of the scene's
   `[BuildingBlock] OVRComprehensiveInteractionRig` found its
   `BestHoverInteractorGroup`/`HoverInteractorsGate` had **all five** controller/hand
   interactor-mode branches inactive during an MCP-scripted Play session (no real
   device telemetry ever reaches Unity that way) — a separate, real finding, but
   ultimately a red herring for the actual headset bug once the user confirmed the ray
   works generally on real hardware.

Per the debugging skill's own rule (three real fixes, each structurally necessary, none
of them the actual cause — stop patching this pipeline, question whether it's the right
architecture at all): checked whether **anything else** in this project actually
depends on the Meta ray → `PointableCanvasModule` → uGUI pipeline. It doesn't.
`ToolWheelController` is pure `InputAction`/right-thumbstick angle math (several of its
`Image`s even have `raycastTarget = false`). `HypothesisCheckpointUI` is the same raw
right-thumbstick-nav + A-button-commit pattern. Neither touches a ray interactor at
all. **This project has no proven-working example of the ray/canvas pipeline
anywhere**, despite two separate doc comments (this one, and the Deduction Board's own)
having claimed one existed.

**Resolution: replaced drag-and-drop with the same thumbstick+button pattern every
other panel in this project already uses successfully**, sidestepping the ray pipeline
entirely rather than continuing to debug it:

- **Right thumbstick** moves a cursor up/down within whichever column (Pool/Notes) has
  focus, and left/right switches which column has focus (matching their physical
  side-by-side layout).
- **B (`<XRController>{RightHand}/secondaryButton`)** moves the focused line between
  Pool and Notes — the same action both directions (which column currently has focus
  decides which way), per the user's own request to make removing a line symmetric
  with adding one.
- **A (`<XRController>{RightHand}/primaryButton`)** confirms once the minimum line
  count is met.

`ReportLineChip` lost its drag/pointer-event interfaces entirely and gained a plain
`SetFocused(bool)` the panel calls directly — no uGUI event dependency left at all.
`ReportNotesDropZone.cs` (the old `IDropHandler` forwarder) was deleted outright,
confirmed unreferenced anywhere else first. `RecorderReportUI.BuildRuntimeUI` no longer
creates a `BoxCollider`/`ColliderSurface`/`RayInteractable`/`PointableCanvas`/
`GraphicRaycaster` chain at all — none of it does anything useful without a working
interactor behind it, and this panel no longer needs one.

**A real bug caught during verification, not shipped:** the disabled `ChipTemplate`
GameObject lives as an inactive sibling inside the same `Pool` container the real
cloned chips populate (pre-existing structure, unchanged). Indexing raw
`RectTransform` children (`GetChild(i)`) instead of the actual spawned-chip list let
the navigation cursor land on that invisible template at index 0, and a B-press there
would have moved the broken, uninitialized template into Notes. Fixed by building all
navigation off a filtered `GetChips(container)` helper that explicitly excludes
`chipTemplate` by reference, regardless of which container it's ever parented under.

**Verified live in Play mode** (direct calls via reflection into the private
navigation methods, plus `RecorderReportUI.Show`/`Confirm` — see §0a): opened the panel
for `EVD-014`, drove the cursor through both columns, moved lines back and forth
including a full add→remove→re-add round trip, reached `minimumLinesRequired`, and
confirmed `Confirm()` fires its callback and closes the panel. **Not yet confirmed on
the real headset** — this whole feature has now failed a live headset test once (the
old drag version) and been rebuilt on a completely different, never-headset-tested
input path. Test this specifically before assuming it works: does the cursor visibly
move on the pool/notes cards as the thumbstick is tilted, does B visibly move a card,
does A close the panel once enough lines are in Notes.

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

- [ ] **`RecorderReportUI`'s new thumbstick+B/A navigation (§7m) has never been tested
      on the real headset.** Verified only via direct calls (reflection into the
      private navigation methods) in an MCP-driven Play session — no real controller
      ever drove it. This exact feature has already failed one full headset test on
      its previous (drag-and-drop) implementation; don't assume the rebuild
      transferred cleanly just because the logic checks out. Watch specifically for:
      does the visual cursor/highlight move as the thumbstick tilts, does the tool
      correctly re-equip after Confirm/Hide (`heldToolRoleBeforeShow` round-trip).

- [ ] **`GreyboxFlowTest.RunFullFlow()` has not been re-run as one integrated call
      since the `requiresSwabbing` redesign (§7k) or since `EVD-019` was removed.**
      The `requiresSwabbing` branch it now contains was written for this and looks
      correct by inspection, and the same underlying calls were separately verified by
      hand (§7k/7l), but the full method itself — 5-item roster, no `EVD-019` — has not
      been executed end to end as a single run since. Cheap to close: open Play mode,
      call `GreyboxFlowTest.RunFullFlow()` (or trigger however it's wired in-scene),
      confirm 5/5 PASS with no `EVD-019` references anywhere in the report.

- [ ] **Chalk-tracing feel (§7j) has never been tuned in a headset.** `ToleranceFraction
      = 0.5f` and `LineWidthScale = 1.5f` in `EvidenceChalkGuide.cs`, and per-item
      `chalkCircleRadius`/`chalkPointCount`/`chalkCompletionThreshold` on
      `EvidenceDefinition`, are all currently whatever values were last set during
      development — functionally verified (tracing genuinely covers points and fires
      `MarkChalkMarked` at threshold) but never played by a real hand with a real
      controller to check whether the circle reads as the right size, the tolerance
      feels forgiving enough, or the line width is visible at headset distance. Same
      category of debt as the still-open `interactionRadius` item below.

- [ ] **Section 2's "scripts confirmed to exist" list is now badly out of date and
      should not be trusted for current architecture.** It predates `ChalkMarkTool`,
      `EvidenceChalkGuide`, `EvidenceSwabRequirement`, `EvidenceGrabGate`,
      `RecorderReportUI`, `ReportLineChip`, and everything else introduced across
      §7f–7m, and it still lists `SketchTool`/`MasterSketchManager`/`MasterSketchUI` as
      current when they've been deleted (§7i). Treat §7i onward, not §2, as the source
      of truth for the current evidence-lifecycle architecture. Nobody has gone back to
      correct §2 itself — it's left as-is here as a historical snapshot rather than
      silently rewritten, per this doc's own convention of appending deltas rather than
      editing history.

## 9. Current checkpoint and Quest continuation — 2026-10-10

The current local checkpoint is `5238dd2` (vendor deduction and presentation polish). The later IOC/SOCO and vendor work supersedes historical architecture/debt descriptions above; read `docs/IOC_SOCO_PHASE5.md` and `docs/validation/closing-audit-2026-10-09.md` for the verified current state. The closing Editor audit reports 909 passing checks, with hardware acceptance still pending.

The Quest 3S is now connected. `QuestBuild.Build` successfully built `Builds/Android/ViCSI-5238dd2.apk` with zero build errors, and `adb install -r` installed it without clearing app data. The package is `ph.edu.parsu.vicsi`. Launch succeeded at the Android activity level, but the initial log then showed a stopped app window; visible startup is awaiting the user's headset observation. An AssetPackManager class error and unsupported OpenXR-extension warnings were captured and remain unresolved, not evidence of a passed hardware test.

Continue from `docs/validation/quest-validation-2026-10-10.md` and the existing `docs/validation/quest-validation-checklist.md`. START / IOC is pending. Active scenes use QuestDev upload configuration; the separate Production asset still has an empty endpoint. Do not infer hosted-upload success from installation. No gameplay changes, commit, push or deployment were made during this build/install continuation.

### 9a. IOC checklist ergonomics — source change, not yet installed on Quest

The checklist no longer auto-shows at the far-left HUD offset. During IOCAssessment only, hold X to open the existing utility wheel, select IOC Checklist and release X; B closes its centered world-space progress reference. It uses WorldPanelPlacement (1.35 m forward / 0.14 m below eye level) and the existing UI/tool/locomotion locks. Phase exit closes it and removes menu availability. Observation methods, seven checklist predicates, scoring and workflow progression remain unchanged.

IOC ambient hints reuse the notification channel with centered X / −0.16 m Y at 1 m distance, and are suppressed during panels/dialogue or without a valid action. Timed tooltips and other prompts retain their authored placement. Final focused Editor probe: 9 PASS / 0 FAIL; final compile: zero C# errors. Temporary Assets diagnostic source and runner exemption were removed. Report and captures are in `docs/validation/ioc-ui-ergonomics-2026-10-10.md`. The installed 5238dd2 APK predates this fix; Quest visual/controller acceptance is pending a later build/install. No commit, push or deployment; stop after reporting this task.

### 9b. Witness Accounts and vendor access — source change, not installed on Quest

The utility wheel now opens read-only WITNESS ACCOUNTS rather than a Vendor Follow-up interview. It groups authored available Paul/Peter/Vendor accounts, hides undiscovered 3 AM testimony and locked knife-handling information, and reads the existing canonical vendor flag rather than storing new discovery state. Prepared Peter appearance/money catalog accounts remain hidden unless actually delivered in Dialogue History. The panel uses the existing centered 1.35 m world placement, right-stick scrolling, B-close and shared UI/tool/movement locks.

The unchanged follow-up question can only open through the physical roadside vendor within its existing 2.25 m range, and cannot be selected by the same A press that opened it. Its statement ID, answer, once-only discovery, board relationship and deduction support logic remain unchanged. VendorFollowupTest passed 11 focused checks / 0 failures with zero C# errors; the final Editor is outside Play and not compiling. Full report: `docs/validation/witness-ui-cleanup-2026-10-10.md`. Actual Quest readability, scroll/menu/controller behavior and movement restoration still require validation of a later build. No build/install, commit, push or deployment was performed for this task; stop after reporting.

### 9c. Active dialogue humanization — source content, not installed on Quest

Active FR/IOC/SOCO handovers, preliminary observations, witness accounts, professor/team guidance and endings now use natural speech with the same facts and uncertainty. The physical vendor question remains verbatim; its answer preserves recognition near the house around 3 AM without entry/attack claims. Two scene-card quotes match updated Paul text. Three existing ending radio strings now resolve from the central catalog; all other completion sequencing is unchanged. Existing HYP prompts/correct reasoning choices were polished, but no live feedback stage was added because none exists in the current implementation. Prepared Peter appearance/money lines and archived pools remain untouched.

DialogueCatalogTest passed 46 focused checks / 0 failures, including exact displayed-text/history recording. The pre-polish structural comparison passed 10 checks, preserving IDs, unlocks, correctness, timing, ending requirements, scene wiring and completion logic. Unity compiled with zero C# errors; only existing obsolete-API warnings remain. Four short photography labels received grammar cleanup after the Play run and passed the final static audit. Full report: `docs/validation/dialogue-humanization-2026-10-10.md`. No gameplay/scoring/procedure/phase/form/backend/schema change, full regression, commit, push, build/install or deployment. Subjective Quest tone/readability acceptance remains pending; stop after the report.

### 9d. IOC visual available for scene editing

NPC_IOC_PLACEHOLDER/FacingPivot is now active in the authored CSI_Environment scene, making its Visual children available outside Play mode. NPCPresentation.Start retains the original phase visibility rule, and the collider/label remain under their existing runtime control. No commit, push or deployment.

### 9e. Completed biological containers and B storage — source change

Valid required-swab insertion now holsters the loose swab/empty left container and equips the existing GLB-backed completed-container prefab through HeldItemCoordinator. EvidenceInventory owns both samples: blood-sample:EVD-014 is the knife's derived trace, while EVD-015 remains the existing collectedBySwab sample identity. Source swabbingDone/swabSampleSealed remain authoritative; no second sample-state owner or persistence schema was introduced. Source-derived Blood Sample names appear in the existing inventory.

The contextual prompt is “Press B to store in inventory.” B performs the existing seal operation if still required, then stores custody and frees the hand; trigger sealing remains available and never auto-stores. Modal/dialogue suppression and early B processing prevent close/skip input leaking into storage. The original spatter recording/seal and knife fingerprint gates remain unchanged; original evidence is not destroyed by sampling. Normal scene reset clears entries and restores the empty kit.

BiologicalSamplingTest passed 46 checks / 0 failures with final zero C# errors, including provided-model mesh identity, two source identities, B handler/prompt behavior, existing procedure gates/events and reset. Captures/report: `docs/validation/blood-sample-inventory-2026-10-10.md`. Final Editor is outside Play and not compiling. Actual Quest overlap, pose/scale, B input, inventory re-equip and dark material contrast remain pending. No SOCO closeout, full regression, scoring/workflow/backend/schema changes, commit, push, build/install or deployment; stop after the report.

### 9f. Supplied IOC character wired into the scene

CSI_Environment now uses NPC_IOC/FacingPivot/Visual/IOC_NPC instead of the six primitive body parts. The model is a linked IOC_NPC_Unity.glb instance, fitted to 1.82 m and grounded; existing NPCPresentation, collider, label, briefing facing and runtime phase visibility are unchanged. FacingPivot remains active for editing outside Play mode.

The supplied IOC_NPC.glb uses unsupported meshopt compression, so it remains intact alongside a glTF Transform 4.5.1 uncompressed copy consumed by Unity. Focused import/render/static checks passed and final compilation logged zero C# errors; no gameplay scripts or progression tests were changed. The Editor accepted the saved scene through Reload; MCP disconnected afterward, so final live selection verification was unavailable. The supplied dark material needs scene/Quest lighting inspection. Report: docs/validation/ioc-npc-model-2026-10-10.md. No commit, push, build/install or deployment.

### 9g. IOC color texture now imports correctly

The first IOC import had mesh geometry but could not decode its embedded WebP color image. IOC_NPC_Unity.glb now embeds that image as pixel-identical PNG; the original IOC_NPC.glb, geometry, material tint, skeleton, animation and scene remain unchanged. Unity assigns both 2048x2048 color and normal maps, with zero importer report items. Updated preview shows the authored brown coat, skin and clothing; Quest lighting remains to be checked. Conversion helper: Tools/ConvertGlbWebPToPng.py. Report update: docs/validation/ioc-npc-model-2026-10-10.md. No gameplay changes, commit, push or deployment.

### 9h. IOC supplied standing idle enabled

NPC_IOC/FacingPivot/Visual/IOC_NPC now uses IOC_Idle.controller with the supplied 6.333-second clip copied into IOC_Idle.anim. The copied hip Y curve is shifted by 93.32916 model units to align the export's animation origin with its static pose; both GLBs remain unchanged. Root motion is disabled, while original NPCPresentation visibility/briefing turns and the interaction collider remain intact. The Editor-only IOCNpcIdleSetup menu reproduces this setup.

Focused Editor PlayableGraph playback verified looping beyond one cycle, unchanged root position/rotation and sampled feet within 0.25 mm of the static floor. Evidence: docs/evidence/ioc-npc-model/idle-playback-check.json and idle-pose-2.png. Final Unity compilation has zero C# errors and Editor is clean outside Play mode. Scenario/Quest animation and lighting acceptance remain pending. No full regression, gameplay change, commit, push, build/install or deployment.

### 9i. Closed bag presentation and B inventory custody

EvidenceBagTool retains the authored right-grip/left-trigger-open insertion gesture and left-trigger-release sealing step. Insertion hides the loose evidence inside the still-open bag; successful sealing equips EvidencePaperBagClosed.prefab through HeldItemCoordinator and the existing inventory owner. That prefab links the supplied EVIDENCE_PAPER_BAG_CLOSED.glb without source modification. Knife/bottle definitions now reference it, retaining all procedural data and source names/IDs.

Fresh bag entries remain Held and are not counted/logged as stored until the existing B handler confirms bag storage, then unequips to Inventory. Tool/evidence switching cannot implicitly store pending bags. Shared modal/input handling and the contextual channel show �Press B to store in inventory� only for a held completed bag; the blood-sample storage branch is retained. Reset follows normal scene/inventory destruction and restores original props/open bag state.

Extended CollectionFeedbackTest passed 43 checks / 0 failures, including actual TryInsert/TrySeal, both GLB-backed visuals, separate IDs/names, modal suppression, B handler storage, re-equip and fresh reset. Final compilation has zero C# errors and Editor is outside Play/not compiling. Captures are bright diagnostic renders; Quest grip overlap, trigger/B hardware, held pose/size and lighting remain pending. Report: docs/validation/bagged-evidence-inventory-2026-10-10.md. No SOCO closeout, full regression, scoring/deduction/forms/backend/schema changes, commit, push, build/install or deployment.

### 9j. Blood sample flow reconfirmed after bag storage work

BiologicalSamplingTest now passes 50 checks / 0 failures, including actual source sampling/insertion, visible blood-bearing loose and contained tips, completed GLB model, contextual B prompt, modal suppression, explicit storage, hand release, independent EVD-014/EVD-015 source identities and fresh reset. The existing GreyboxFlowTest helper explicitly stores pending sealed bags through the coordinator so its knife procedure completion matches current custody behavior. These are diagnostic changes only; runtime sampling/procedure logic is unchanged. Final Editor state is outside Play, not compiling and scriptCompilationFailed=False. Report: docs/validation/blood-sample-confirmation-2026-10-10.md. Quest physical contact/controller input, pose and lighting remain pending; no SOCO closeout or deployment.

### 9k. Procedural completion follows conclusion feedback

The saved CSI scene now has SOCOCloseout with references to the four active evidence definitions, the existing completed container/closed-bag prefabs, IOC NPC and cordon. New appended phases are SOCOCloseout=7, EvidenceInventoryReview=8, FinalSceneSurvey=9, IOCCompletionReport=10 and SceneRelease=11; InvestigationComplete remains serialized 6, with chronological HasReachedPhase comparisons. Processing remains enabled in closeout, but accepted deduction cannot be resubmitted/rescored. Immediate ending feedback acknowledges into closeout; final review, EndSession and the unchanged upload call wait for release/completion. Four new objectives are excluded from scoring; previous objective blocks remain verbatim.

Accountability checks read existing body Logged state, required Processed/fingerprint states, source-specific samples, completed prefab references, explicit B-storage flags and final custody. Existing deposited metadata remains in the same inventory while deposited items are excluded from equipment/capacity. Closeout Inventory is a separate read-only utility entry; the original equipment inventory is preserved. A/trigger confirms review, B closes; final survey is a separate authored main-scene action, followed by approaching IOC within 2.25 m. IOC confirmation enters release, its reply resolves, cordon renderers disable, then completion freezes both clocks and finalizes logging/upload. Reset uses normal reload.

Final focused validation: SOCOCloseoutTest 46 PASS, EndingManagerTest 9 PASS, EvidenceInventoryTest 29 PASS, zero failures. Corrected two diagnostic-only issues (inactive board lookup and first-feedback wait deadline); final tests use real handlers with explicit procedure-state fixtures. Final Editor has zero C# errors, is not playing/compiling and sceneDirty=False. Report: docs/validation/soco-closeout-2026-10-10.md; evidence: docs/evidence/soco-closeout. Quest A/B/trigger, ray/readability, survey region, IOC framing and cordon release acceptance remain pending. No full regression, interactive forms, scoring weights/backend/schema changes, commit, push, build/install or deployment.
## Task 46 - Wire the evidence transport bin visual

Connected the supplied EVIDENCE_TRANSPORT_BIN.glb as a linked TransportBinVisual child of EvidenceHoldingCrate in CSI_Environment, replacing only the placeholder cube renderer. The model preserves its proportions at approximately 0.45 x 0.22 x 0.32 metres; the existing root position, deposit script, trigger, custody rules and inventory behavior are unchanged. Added EvidenceTransportBinSetup for repeatable Editor wiring. Focused scene comparisons, imported/rendered model inspection and saved-scene reload passed; Unity reports scriptCompilationFailed=False and sceneDirty=False. Quest appearance and approach/deposit input remain pending. No full regression, commit, push or deployment.

## Task 47 - Small final SOCO closeout verification

Sequentially reran SOCOCloseoutTest 46/0, EndingManagerTest 9/0, EvidenceInventoryTest 29/0, ScenarioPhaseFoundationTest 7/0, FirstResponderTurnoverTest 44/0, CollectionFeedbackTest 43/0 and BiologicalSamplingTest 50/0: final total 228 passed, zero failures. The initial phase-foundation attempt had one stale full-XYZ arrival expectation (1 cm floor-height settling); corrected only that diagnostic to verify restored X/Z/yaw and preserved height captured at arrival, retaining all original phase/event gates and adding deliberate yaw displacement. No runtime gameplay changes. Completion/result-ID/upload argument and cordon/text boundaries inspected; all diagnostic networking isolated, no live upload. Final Editor is outside Play, clean CSI_Environment, no queued/pending test, both XR initialization settings restored and zero C# errors. Verdict SOCO CLOSEOUT EDITOR VERIFIED / QUEST VALIDATION PENDING; safe for a local checkpoint, none created. Report: docs/validation/soco-closeout-final-verification-2026-10-11.md. No full regression, commit, push or deployment.

## Task 48 - Local checkpoint of verified SOCO closeout and custody

Prepared one local checkpoint named checkpoint: verified SOCO closeout and evidence custody, preserving the complete current authored scene/source dependency set since 5238dd2. Maintained validation reports capture the 228/0 focused result and zero C# errors; Quest acceptance remains pending. Raw test logs, telemetry, screenshots, Editor snapshots, snapshot-dependent one-off audit tooling, Unity caches/builds and per-machine credentials remain excluded. No gameplay changes, push, deployment or tag. Checkpoint scope and exclusions: docs/validation/soco-closeout-checkpoint-2026-10-11.md.


## Task 49 - Lobby UI organization and label/value tables

Reorganized Main into title, primary Start Investigation, quieter instructor access and a grouped headset-status table; aligned student confirmation and instructor student/session/status/settings details using reusable layout rows, fixed label widths, flexible values and one detail ScrollRect. Existing selection, security, service operations, session/upload behavior and gameplay were preserved; all 48 scene-authored button callbacks and original functional component fields were compared unchanged, with optional presentation references added only in Main.

InstructorManagementTest passed 55/0 in isolated Play mode, and isolated Main startup/Start/keypad clear/Back/instructor open-close checks passed; long name/class rendering and long-detail scrolling were inspected. Main startup camera-pose reset was caught and addressed through the existing WorldPanelPlacement helper (one-shot runtime canvas placement: 1.5008 m, 1.91 degrees below forward), without moving the head. Final Editor is clean Main outside Play, no pending test, XR settings restored, zero C# errors; Quest text/hover/scroll/modal validation remains pending. Report: docs/validation/lobby-ui-organization-2026-10-11.md. No full regression, commit, push or deployment.

### 9l. Lobby disappearance in normal Editor Play fixed

Task 49's isolated checks did not cover the normal XR startup: the untracked view reset to floor height and zero yaw, hiding the UI inside the floor or behind a wall. LobbyCanvasPlacement now runs after tracking and supplies an Editor-only untracked preview height/facing fallback, restoring its temporary tracking-space pose when disabled or real position tracking resumes. Final normal-XR Play capture shows the lobby menu and status table at 1.500833 m, with no intervening collider; height restoration/re-enable passed and compilation succeeded. Report: docs/validation/lobby-visibility-fix-2026-10-11.md. Quest/physical tracking recovery remain pending; no commit, push, build/install or deployment.

### 9m. Player startup facing supersedes the Editor yaw workaround

The player now turns toward the authored lobby through FirstPersonLocomotor during startup, with a further one-time correction on the first tracked head pose. This applies in builds. Removed forced Editor TrackingSpace yaw; the height fallback remains. The menu stays on the room-facing side. Verified actual player root/camera yaw 179.61 degrees and TrackingSpace yaw zero; a deliberate wall-facing locomotion fixture recovered after restarting initialization, with a rendered visible menu at 1.500832 m. Normal later turning stays unrestricted. Final compilation succeeded; real headset startup remains pending. Report: docs/validation/lobby-player-facing-2026-10-11.md. No commit, push, build/install or deployment.

### 9n. Fixed lobby panel height above the desk

The user clarified that the canvas was near the floor rather than disappearing. Main now fixes canvas center height to 1.65 m and keeps it upright; bottom edge is 1.10 m. Saved scene placement and LobbyLayoutSetup agree. This supersedes the earlier eye-relative Y and tilt. Play render shows the panel above the desk; a floor-level camera fixture no longer lowers or tilts it. Final compilation succeeded; Quest acceptance remains pending. Report: docs/validation/lobby-panel-height-2026-10-11.md. No commit, push, build/install or deployment.

### 9o. Lobby room restyled with furniture and warm light

Main now has an oak/navy/plaster interior, sage lounge seating, coffee table, rug, reception desk/chair, planters, framed prints, two pendants and perimeter strips with four shadow-free point lights. Original footprint and authored transforms are retained; all 48 button blocks are unchanged. Normal Play verified player facing into the room, menu center 1.65 m/bottom 1.10 m above the 0.945 m desk, clean scene and successful compilation. Editor setup can be repeated through ViCSI/Scene Setup/Restyle Lobby Room. Report: docs/validation/lobby-room-restyle-2026-10-11.md; previews: docs/evidence/lobby-room-restyle/. Physical headset performance/appearance pending; no commit, push, build/install or deployment.

### 9p. Assessment continuation state recovered on 2026-10-12

Assessment Phases 1-4 are implemented in the current Unity working tree. Phase 4 introduces append-only scoring version 1.7.0: reasoning checkpoints / relationships / final conclusion contribute 60 / 25 / 15 points, while procedures remain required, non-scored training. Report: docs/validation/assessment-phase4-2026-10-12.md. Recorded prior results: 108 passing focused checks, zero failures, one optional legacy audit skipped, zero C# errors and zero TypeScript diagnostics. These checks were not rerun during recovery.

Phase 5 dashboard presentation is also implemented in C:/Users/Roduf Eleu/Desktop/vicsi-dashboard. Its newer docs/validation/assessment-phase5-2026-10-12.md supersedes Phase 4's statement that dashboard presentation remains to be implemented. It records separate deduction/training sections, chronological checkpoint responses, support-aware relationship/conclusion feedback, historical-version isolation, CSV compatibility and development seed-source registration. Recorded focused validation: 91 passed, zero failed, one optional legacy audit skipped; TypeScript and scoped lint passed. The dashboard working tree remains uncommitted; seed scripts were not executed and nothing was deployed.

Remaining scopes include physical Quest validation, authenticated live dashboard validation, and research/expert validation of the assessment design. No expert approval, live registration, hardware acceptance, new checkpoint, push or deployment should be inferred from these reports. Recovery only synchronized this handoff; it did not change gameplay, scoring or dashboard source.
### 9q. BODY-A removal transition - 2026-10-12

BODY-A now records its three existing location/position/wound findings before its final photograph, retains the existing chalk and Marker A, and requires a body-only removal transition for SOCO closeout. EvidenceStateManager owns bodyFindingsRecorded and the separate BodyRemovalStatus track; the shared EvidenceStatus enum/sequence and deduction inputs/weights remain unchanged. Near the documented body with tools put away, press A, then confirm with a fresh A/trigger; B cancels before confirmation. Existing UI/tool/locomotion locks, Meta's fade mesh with a URP-compatible material and a four-second training message represent identification tagging, wrapping, cadaver bag and medico-legal transport. Only the body renderer/collider hide; notes, record, original-position anchors, chalk and marker remain. No body swab, ordinary bag tool or physical lifting/dragging.

BodyDocumentationRecorded, BodyPreparedForRemoval and BodyRemoved are preserved through the existing essential-event envelope; the vicsi-dashboard adapter accepts those three non-scored milestones. BodyRemovalBlocked stays a normal procedural-log event with no grade penalty. No dashboard page, scorer formula, schema, scoring version or other evidence asset was changed. Scene wiring is saved; no manual setup required.

Final focused verification: 307 PASS / 0 FAIL / 0 SKIP across six Unity suites, actual Unity/backend wire tests and backend deduction tests. TypeScript passed; current Unity C# compilation is clean, fade shader has no compiler messages, CSI scene is clean outside Play, one evidence manager/body action, XR settings restored and no pending runner. Report: docs/validation/body-removal-2026-10-12.md; evidence: docs/evidence/body-removal/. Quest visual/stereo/controller acceptance remains pending. No full regression, build/install, live upload, database mutation, commit, push or deployment.
### 9r. IOC retirement and fingerprint vendor lead - 2026-10-12

IOC presentation is visible only in SOCOTurnover (phase 4). The existing briefing coroutine drains all opening dialogue before advancing to SOCOInvestigation (phase 5), which disables its visible body, prompt and collider while retaining the actor root and references. Later phases do not restore it. Fresh scene/session reload restores the opening briefing. Closeout retains its confirmation/accountability/release gates but now uses a Professor-guided completion report without requiring proximity to the retired IOC; historical phase/method/event IDs remain compatible.

The existing FIND-KNIFE-PRINT-BROTHER plus locked RC-03-FINGERPRINT response jointly unlock the vendor question. Selection alone does not unlock it. Existing explanation/lesson precedes LEAD-FINGERPRINT-VENDOR through the current training/dialogue queue; both lead deferrals are authored on RC-03. The lead confirms contact while preserving unknown timing/purpose and explicitly rejects the print alone as proof of attack. It asks about anyone besides Peter without revealing the sighting. The initial fingerprint objective is factual to avoid teaching before assessment. Vendor answer records statement_vendor_paul_3am only after delivery and unlocks the unchanged REL-paul-timeline-contradiction. No scoring weights or procedures changed; all relevant discovery/response/history/visibility state resets through existing scene reload.

Final focused verification: 397 PASS / 0 FAIL (368 Unity checks across ten suites, 29 backend scoring checks). Covers real briefing, response locking, post-checkpoint guidance, physical vendor/board access, reset, BODY-A removal, knife/spatter/bottle procedures, final assessment, endings and remote completion reporting. Unity 6000.5.6f1 Android target has zero C# compilation errors; clean CSI scene outside Play; one evidence manager/vendor interview/closeout owner; IOC references intact; both XR initialization settings restored; no pending runner. Initial fixture failures and corrections are documented separately. Report with exact dialogue and file list: docs/validation/narrative-role-flow-2026-10-12.md. Evidence: docs/evidence/narrative-role-flow/.

No manual Editor wiring required. Physical Quest acceptance remains pending. No build/install, live upload, commit, push or deployment.
### 9s. EVD-014 fingerprint lift-to-store repair (2026-10-12)
Root cause: lifting only swapped the tape visual; it did not create held Evidence, so `HeldEvidenceStorePrompt` (which requires HeldCategory.Evidence) could not show B/Store. The tape stayed a Tool, and fingerprint completion/matching had been bundled into the lift.
Fixed flow: tape lift marks `fingerprintLifted`, creates the derived inventory item `fingerprint-sample:EVD-014` from the existing backing-card prefab, and equips it through `HeldItemCoordinator`; B explicitly stores it through `EvidenceInventory.ConfirmSampleStorage`. That completes the source knife's fingerprint processing and existing Paul match path; RC-03 and vendor follow-up gates remain unchanged. Knife stays in scene for remaining processing; no score change. The procedure map shows ?Store the lifted fingerprint (B)? until storage. No manual scene wiring.
Validation: FingerprintStorageTest (25 checks, actual right-secondaryButton input, session log source + scored=false payload, score invariance, reset), EvidenceProcedureMapTest, and BiologicalSamplingTest passed. Unity reports 0 current C# compile errors. Reports: `docs/validation/fingerprint-storage-2026-10-12.md` and `docs/evidence/fingerprint-storage/`. Quest headset build/test not performed.

### 9t. IOC starting ground marker (2026-10-12)
Created flat, collider-free 1.05 m yellow translucent/emissive ground cylinder `IOC_Start_Ground_Circle` under `NPC_IOC`, using `IOC_GroundGuidance_Yellow.mat`. It starts active in the scene. Existing `SOCOWorkflowInteraction.BeginBriefing()` hides it immediately before briefing lines start; its `SOCOInvestigation` phase callback also hides it before the IOC is retired. Scene reload resets the saved active state. No extra manager/trigger or scoring/timing changes; scene reference is wired, no manual Editor action.
Validation: `NarrativeRoleFlowTest` all 34 checks pass (start, briefing, handoff, restart; existing role flow intact); Unity idle with zero current C# errors. Details in `docs/validation/ioc-ground-marker-2026-10-12.md`, output in `docs/evidence/ioc-ground-marker/`. No Quest headset test performed.

### 9u. Shared VR UI comfort and confirm ownership - completed 2026-10-13

HudFollowCamera now coordinates visible world-space panels using actual canvas bounds, shared wall avoidance, deterministic priority and safe companion slots. Panels retain authored dimensions and preferred profiles. PlayerUIGate arbitrates all 16 runtime right-A readers; UIConfirmJumpRelay defers Meta Jump until UI input resolves. Opening, closing, held and release gestures cannot leak into jumping. Reasoning takes the existing tool lock; pointer, wheel and physical-grab handling respect modal ownership and release guards. No manual scene wiring is required.

Final recorded validation: 558 PASS / 0 FAIL across 15 suites. During this continuation, all 15 saved reports were recounted and matched the manifest. Accidentally serialized PowerShell metadata in the manifest was replaced with plain result strings. Tests were not rerun. The recorded final Editor snapshot is clean CSI_Environment outside Play, not compiling, no C# compilation failure, no pending runner and XR settings restored. Report: docs/validation/ui-comfort-input-2026-10-12.md. Exact report paths: docs/evidence/ui-comfort-input/completion-2026-10-13/summary.json.

Physical Quest readability, side-turn comfort, geometry/performance, controller timing, pointer targeting and two-hand grab acceptance remain pending a new build. No gameplay changes, build/install, live upload, commit, push or deployment during this continuation.

### 9v. BODY-A revised documentation order - 2026-10-13

BODY-A now follows Initial Photo -> Chalk -> Marker A -> Final Photo -> Measurement -> Record Findings -> Prepare for Removal -> existing transport/removal transition. Removed the body-only Marked-to-Logged report exception, findings-before-final-photo gate, automatic final-photo logging, and visible examination task/instruction. Fixed references and both directions of body evidence-distance measurements require the final photograph; body measurement asset rules now block recording rather than warn on final photography. Existing three report lines and downstream injury/finding/deduction references remain intact, with no separate examination prerequisite. Early Recorder/removal guidance matches the revised sequence. Documentation logging is emitted after real Logged completion with explicit scored=false.

The existing fade, identification/tagging, wrapping, cadaver bag, transport message, renderer/collider hiding and data retention are unchanged. Closeout still requires BODY-A removal. No body swab, normal evidence bag or manual dragging/lifting. Knife, Blood Spatter and Bottle flows and all deduction scoring rules remain unchanged. No manual Editor wiring required.

Final accepted verification: 596 Unity checks across 14 suites plus 33 backend checks = 629 PASS / 0 FAIL. Manifest: docs/evidence/body-a-revised-flow/summary.json. Full report and changed-file list: docs/validation/body-a-revised-flow-2026-10-13.md. Earlier fixture failures and host paging-file/compiler-launch failure are retained and explained in the report. Final Unity 6000.5.6f1 Android Editor has zero C# compilation errors, clean CSI_Environment outside Play, one evidence manager/body action, no pending runner/XR restore and both XR initialization settings enabled. Physical Quest acceptance remains pending; no build/install, live upload, commit, push or deployment.

### 9w. Temporary procedure-feedback wall placement - 2026-10-13

The pre-change audit identified NotificationCanvas as the remaining ordinary floating feedback gap: its HudFollowCamera had wall awareness/shared layout off. NotificationUI now reuses the shared solver for procedure-complete confirmations, timed tooltips/notices and ambient prompts. Runtime Awake applies a compact 0.90 m forward / -0.18 m local Y profile, 0.60 m minimum, 0.12 m clearance and 0.0011 scale; layer mask/filtering and fallback queries are the existing shared implementation. Timed priority is 250, ambient 100. Ordinary dialogue gets a measured lower companion slot with 0.05 m gap and shared safe pose/side; modal/information ownership defers the notification and pauses remaining time. Latest wins, no backlog. No A, tool or locomotion lock; no raycast blocking.

Other major panels already enroll on opening. Physical labels/markers, tool guides/reticles, viewfinder, fixed lobby/developer overlay and BODY-A's full-black transport overlay remain intentionally unchanged. No scene/reference wiring or manual Editor setup required. All remaining direct placement patterns are classified in docs/validation/temporary-ui-wall-placement-2026-10-13.md.

Final accepted results: 189 PASS / 0 FAIL across UIComfortInputTest 55, NotificationPriorityTest 4, DialogueCatalogTest 46, BodyRemovalTest 41 and CollectionFeedbackTest 43. Exact paths: docs/evidence/temporary-ui-wall-placement/summary.json. The priority fixture's first attempt was overwritten by a live delayed scene-photography instruction; the corrected isolated fixture passed. Final Unity 6000.5.6f1 Android Editor: no C# errors, clean CSI_Environment outside Play, one notification canvas, no pending runner/XR restore and both XR initialization settings enabled. Quest stereo/comfort/performance/controller acceptance remains pending. No build/install, live upload, commit, push or deployment.

### 9x. IOC observation relocation (2026-10-13)

IOC_Observe_Point now stands beside the TV toward the kitchen at (0.40, 0.00, 10.10), yaw 308.4537 degrees, forward (-0.783111, 0, 0.621882). Standing clearance and forward body sightline are unobstructed. Radius 1.5 m, Require Facing, angle 45 degrees, A - Observe and IOC workflow remain unchanged. Scene-survey dialogue now says: The scene remains secured and the cordon is intact. John's body appears undisturbed inside the house near the kitchen area.

Final validation: 61 PASS / 0 FAIL (ScenarioPhaseFoundationTest 15, DialogueCatalogTest 46), zero C# errors. Corrected the workflow fixture to exclude empty-ID tool placeholders from evidence preservation; real evidence and BODY-A have zero displacement. Exact forward screenshot, state and retained diagnostic attempts: docs/evidence/ioc-observation-relocation. Full report: docs/validation/ioc-observation-relocation-2026-10-13.md. No manual point/dialogue wiring, commit, push, deployment, build/install or live upload. Optional Quest comfort/FOV walkthrough remains hardware-only.

### 9y. Opening yellow IOC ground visual removed (2026-10-13)

Disabled the MeshRenderer on IOC_Start_Ground_Circle in the saved CSI_Environment scene. The yellow area no longer renders; the active object remains the existing invisible briefing-distance anchor, preserving the IOC approach and briefing workflow. Runtime code only changes object activation and cannot re-enable this renderer. Unity confirmed the scene saved successfully and is clean outside Play. Saved YAML independently confirms renderer m_Enabled=0 and the unchanged SOCOWorkflowInteraction anchor reference. No script changes or test rerun; no build/install, commit, push or deployment.
### 9z. IOC observation standing highlight (2026-10-13)

Added a yellow floor disc and facing arrow beneath IOC_Observe_Point beside the TV, using the existing yellow material with no colliders/shadows. The visual is raised to the actual 0.10 m interior floor; the observation anchor/radius/facing rules stay unchanged. IOCAssessmentInteraction shows the cue after FR handover and hides it after the scene survey or leaving assessment. Opening guidance explains where to stand, which way to face and pressing A. The old IOC_Start_Ground_Circle briefing renderer remains disabled. Final focused workflow validation: 19 PASS / 0 FAIL, zero C# compilation failure, clean Editor outside Play, no pending runner/XR restore. A later visual-only height correction was saved and verified in preview.png. Report: docs/validation/ioc-observe-standing-highlight-2026-10-13.md. Quest appearance untested; no build/install, live upload, commit, push or deployment.
### 10a. In-game Settings menu - 2026-10-13

Added SettingsPauseMenu on NPC_FirstResponder and one functional Settings entry in the existing left-X utility wheel. Hold X, choose Settings with the right stick, release X; navigate with the stick and A/trigger, B cancels/closes. SettingsPauseCanvas is a 0.62 x 0.58 m tablet using ForensicUIStyle, WorldPanelPointer and wall-safe shared HudFollowCamera at 1.05 m / -0.12 m Y / priority 650. PlayerUIGate, ToolInputLock and LocomotionSuspender own modal input; fresh press guards prevent opening/confirmation presses leaking. B can escape even when geometry withholds the panel. Disable/unload cleanup releases holds independently of panel lifetime. Real-time clocks retain the existing modal policy.

Extended PlayerHeightOffset to translate TrackingSpace without scaling head/hands/world. Range 1.40-2.00 m, step 0.05 m, 1.70 m fallback; first reliable tracked pose supplies physical calibration, reset removes the offset/preference. The old -0.40 m authored offset is superseded by calibrated floor tracking. PlayerPrefs key ViCSI.PlayerHeight.Target.v1 persists the comfort target across scene restart and Main; no height is logged/uploaded. Upward changes require head clearance. The Main height owner bootstraps automatically.

Restart confirmation uses the existing full scenario reload/reset contract and fresh session GUID. Lobby confirmation reuses CompletionScreenUI's authored Main destination through its common LoadMainLobby method and clears pending identification. Existing OnDestroy ends/flushes local session logs; incomplete attempts are not queued for upload. No per-manager reset, scoring change or new authentication policy.

Accepted validation: Settings 35, Narrative 41, Scenario foundation 19, Reasoning 60, Evidence procedures 41, Closeout 48, UI/input 55, Grab reach 9, BODY-A removal 41 = 349 Unity checks, plus 6 scorer checks: 355 PASS / 0 FAIL. Earlier failed attempts remain documented: closeout needed a standing-height untracked Editor camera and queued-dialogue wait; the UI fixture now isolates delayed photography instructions. Final Editor is clean CSI outside Play, zero C# errors, no pending runner/request/XR restore, zero modal/tool/locomotion holds and both XR settings restored. Old IOC briefing yellow renderer remains disabled. Report: docs/validation/settings-pause-menu-2026-10-13.md; exact results: docs/evidence/settings-pause/summary.json; preview: docs/evidence/settings-pause/menu-preview.png. No manual wiring; Quest tracking, recentering, comfort, physical input/grabs and identity flow remain hardware acceptance items. No commit, push, build/install, live upload or deployment.
### 10b. Settings height step updated (2026-10-13)

Per user request, changed PlayerHeightOffset's default and CSI_Environment's serialized heightStep from 0.05 m to 0.10 m. Both plus and minus use the same step. Existing bounds, calibration, reset, persistence and navigation stay unchanged. Updated the existing settings diagnostic expectations and the current settings report. Focused verification results are recorded under docs/evidence/settings-pause/step-010. No commit, push or deployment.
### 10c. Content-responsive VR text options (2026-10-13)

Added shared ResponsiveTextOption and ResponsivePanelLayout helpers. TMP wrapped preferred height plus padding drives equal-width LayoutElement rows without font shrinking. Migrated HypothesisCheckpointUI, SettingsPauseMenu confirmations/options, SOCOWorkflowInteraction decisions, VendorFollowupInterview, SOCOCloseout, BodyRemovalInteraction and runtime RecorderReportUI/ReportLineChip columns. Timed dialogue already measures preferred height; compact icons/tool/height controls remain fixed. Padding is 20 horizontal/16 vertical plus 2-unit TMP inset; gaps 12 (recorder 8). Panels grow to bounded maxima then scroll, with separate capped header/footer. Pointer hits respect RectMask2D clipping and resized row bounds. Reasoning preserves select/confirm and now guards simultaneous trigger+A against consuming both stages.

Verified one-line 78, two-line 105, four-line 174 UI-unit row heights at width 300/font 30; stress row 1277 scrolls inside capped panel. 443 Unity checks plus 6 scorer checks PASS, zero failures; zero C# errors. Vendor review fixture clears unrelated queued BODY-A teaching before its second review. Catalog timer fixture supplies an unobstructed untracked camera. Final focused diagnostic uses the freshly compiled on-disk source, including its safe captured ray direction. Editor clean CSI outside Play, both XR startup settings restored, zero UI/tool/locomotion holds, no pending diagnostic/restore. Report docs/validation/responsive-options-2026-10-13.md, authoritative results docs/evidence/responsive-options/summary.json, preview reasoning-preview.png in that folder. No manual wiring. Quest readability, arrow targets and scroll speed still need hardware acceptance. No commit, push, deploy, build/install or live upload. Settings height step remains 0.10 m.
### 10d. Supplied powdered fingerprint model (2026-10-15)

Connected POWDERED_FINGERPRINT (1).glb as a linked, initially inactive child of the knife fingerprint marker. The supplied mesh/material/texture replaces the gray placeholder only after successful powdering. UV discovery retains the latent mark; UV lock cannot restore the box. Tape lift removes the model with the marker; restart restores the undusted presentation. Model footprint 13.40 x 20.00 mm, upward at Y0.12620; existing contact zone and all procedure/custody/scoring rules retained. Persistent helper: PowderedFingerprintSetup.cs / ViCSI/Scene Setup/Connect Powdered Fingerprint. Extended FingerprintStorageTest: 33 PASS / 0 FAIL, zero compilation failure, clean Editor outside Play with no pending runner/XR restore. Report: docs/validation/powdered-fingerprint-2026-10-15.md; diagnostic handle preview: docs/evidence/powdered-fingerprint/preview.png. Quest appearance remains untested. No build/install, live upload, commit, push or deployment.

### 10e. Cordon stays closed and visible throughout (2026-10-15)

Per user instruction, removed entrance opening at FR turnover and cordon disappearance at closeout. SceneCordon keeps the closed entrance active and legacy opened visual inactive; there is no opening or renderer-removal method. Security acknowledgement/survey now require a visible closed cordon, and CordonVisible includes the entrance tape. Administrative completion preserves the existing milestone/logging/finalization flow while keeping all tape/posts in place; final dialogue confirms the cordon remains secured. Updated existing diagnostics: final 111 PASS / 0 FAIL (FR 44, scenario foundation 19, closeout 48). FR fixture now isolates contextual reasoning, which intentionally defers its expected professor line; failed/stale-assembly attempts retained. Final Editor clean CSI outside Play, zero compilation failure, no pending runner/XR restore, XR initialization restored. This supersedes all earlier handoff descriptions of entrance opening or cordon removal. Report: docs/validation/cordon-always-secured-2026-10-15.md. No build/install, live upload, commit, push or deployment.

### 10f. New house kitchen dining furniture (2026-10-15)

Added existing Table19 and two Chair02 linked prefabs under Vicsi_House/KitchenDiningSet. Compact light-wood table is 1.25 x 0.77 m, 0.76 m high; chairs face inward, grounded on the raised Y1.20 floor, with counter access retained. Existing fallen chair and other live scene edits preserved. Final render checked; compilationFailed=False, scene saved outside Play. Report: docs/validation/new-house-kitchen-2026-10-15.md; preview: docs/evidence/new-house-kitchen/preview.png. No build/install, commit, push or deployment; physical Quest appearance/navigation untested.

### 10g. Old house ceiling and kitchen rendering repair (2026-10-15)

Returned work to Harong per user request. Aligned 13 Floor1 and 14 RoofFloor tiles to the existing 2.5 m landing grid, joined kitchen cabinet carcasses, moved sink with its unit and separated fronts by 6 mm. Added scene-specific matte ceiling plaster; source meshes/materials and existing gameplay/evidence/new-house content preserved. The dark tile patches came from inconsistent four-light selection; Mobile_RPAsset now permits eight additional lights per object (the only pipeline change). Final live Editor Play renders checked for kitchen/ceiling/living/upstairs, 27 grid checks pass, zero unexpected scene-record changes or additions/removals. Scene saved clean outside Play, no compilation failure. Existing non-convex Collider.ClosestPoint UI diagnostics observed; Quest stereo and GPU cost remain untested. Report: docs/validation/old-house-rendering-2026-10-15.md; previews: docs/evidence/old-house-rendering/. No build/install, live upload, commit, push or deployment.

### 10h. New house rendering and alignment repair (2026-10-15)

Repaired Vicsi_House floor-panel overlaps, added a continuous first-floor ceiling lining with the stairwell open, corrected the upstairs ceiling facing/height and aligned its light fittings. Joined and grounded kitchen units, retained tilted chair rotations, aligned living-room furniture, stood TV on its console and restored its texture through local 3D material copies. Relocated a duplicate upstairs fixture while preserving the separate stair light; no lights added or pipeline changes. Accepted live Play renders and 27 focused placement checks pass; saved-scene comparison shows zero changes outside Vicsi_House, zero removed records, four added lining records. Source GLB and original materials preserved. Final scene clean outside Play, compilationFailed=False. Existing shared-UI non-convex ClosestPoint diagnostics remain; Quest stereo/navigation/performance untested. Report: docs/validation/new-house-rendering-2026-10-15.md; evidence: docs/evidence/new-house-rendering/. No build/install, live upload, commit, push or deployment.

### 10i. New house decoration after kitchen relocation (2026-10-16)

Inspected the live kitchen relocated to the rear ground-floor room. Added Vicsi_House/HomeDecor: seven plants, a living-room rug, books, three botanical prints and one warm shadow-free kitchen light. Shared procedural meshes total 6,104 triangles; zero decorative colliders. Editor renders checked, compilationFailed=False, saved scene clean outside Play. Compared against live pre-decoration snapshot: no existing kitchen/furniture/gameplay changes, only house prefab attachment plus 58 new decoration records. Report: docs/validation/new-house-decor-2026-10-16.md; previews: docs/evidence/new-house-decor/. Quest appearance/performance untested. No build/install, live upload, commit, push or deployment.

### New house collisions and climbable stairs (2026-10-16)

Added 96 scene-local static mesh colliders for the new house architecture and missing furniture collision, plus two invisible indoor stair ramps. Existing 11 house colliders and player movement settings are retained. Extended the stairwell floor/ceiling opening by 0.633 m to provide descent clearance at the 2.0 m height setting; only local mesh copies changed, source GLB untouched. Final actual-controller Editor physics verification after saved-scene reload: 42 PASS / 0 FAIL, covering both indoor flights/turning landing, descent, exterior stairs, heights 1.4/1.7/2.0 m, floor support, wall blocking and collision masks. Rendered previews checked. Zero removed records, zero changes outside Vicsi_House, existing transforms/material overrides preserved. Final Editor clean outside Play, compilationFailed=False. Report: docs/validation/new-house-collisions-2026-10-16.md; evidence: docs/evidence/new-house-collisions/. Quest input/stereo/navigation/performance untested. No build/install, live upload, commit, push or deployment.

### Investigation migrated into the new house (2026-10-16)

Moved the active CSI investigation into Vicsi_House; Harong is inactive and retained. BODY-A is in the rear ground-floor kitchen, with knife, blood pattern and counter bottle, eight real wall/cabinet measurement references, seven photo stations, arrival/NPCs, observation/survey targets and cordon relocated. Remade Sketch with kitchen detail plus both floor plans, actual architectural linework, hidden evidence anchors and preserved slot IDs. Revised incompatible location wording, retaining procedure gates, evidence IDs, deduction logic and custody flow. Fixed shared panel placement on non-convex mesh walls.

Final accepted Unity results: 570 Play checks in 13 suites, 63 layout, 42 stair/controller and 29 preservation checks, all pass. Backend new-house-v1 profile tests 5/5, TypeScript and scoped ESLint pass; broader scoring run has 192 pass / 4 pre-existing failures (historical export/key mismatch, two obsolete unsupported-1.7.0 fixtures, Phase 1 source-hash fixture). Historical measurements retain their old profile; new sessions log layoutId and use server-authoritative new-house geometry. Dashboard edits are local source only. Final saved CSI scene clean outside Play, no compilation failure/pending runner/XR restore, both XR startup settings enabled. Recovery snapshots and previews retained. Report: docs/validation/new-house-investigation-2026-10-16.md; authoritative manifest: docs/evidence/new-house-investigation/summary.json. Quest hardware acceptance pending. No build/install, live upload, commit, push or deployment.

### User-repositioned knife, blood pattern and IOC point (2026-10-16)

Preserved all three user placements and IOC rotation exactly. Updated knife references to east/rear kitchen walls and blood-pattern references to rear cabinet/west kitchen wall, with distinct reachable hook zones. Recomputed expected distances, updated Sketch targets/line feet/labels in place and kept the IOC cue grounded. RefineLayout now synchronizes without resetting evidence positions. Added local dashboard new-house-v2 profile; v1/historical geometry remains frozen. Final verification: 195 Play checks across five suites, 62 layout checks, 23 preservation checks and six backend profile tests pass, zero failures. TypeScript/scoped ESLint pass. Final saved CSI scene clean outside Play, no compilation failure/pending runner/XR restore, XR initialization restored. Report: docs/validation/new-house-reposition-2026-10-16.md; evidence/manifest: docs/evidence/new-house-reposition/. Quest hardware acceptance pending. No deployment, build/install, live upload, commit or push.

### Photography off-screen arrow and location pin (2026-10-16)

Added runtime-only ScenePhotographyGuide auto-attached by ScenePhotographyManager. Outside the current photo zone it points to the standing spot; inside it points to the authored camera target. Off-screen targets use an inset directional arrow, visible targets use a location pin, aligned views turn green. Behind-eye directions and a boundary deadband avoid mirrored guidance/flicker. Dark-backed labels distinguish Stand here / Face this view and show distance/view name. Native UI mesh and stereo-aware Resources overlay shader; no input/raycast/collision ownership. Hides before investigation, during other modal UI and after route completion. PhotoCaptureListener excludes the guide from actual album-camera renders and restores visibility in finally. No scene/evidence/IOC placement changes; saved scene byte-identical to pre-change snapshot. Existing photo sequence, capture/procedure rules, scoring and payloads unchanged.

Final accepted verification: 92 PASS / zero failures (guide 26 final run, photography procedure 47, IOC/scenario foundation 19). Rendered arrow/pin previews inspected. Final Editor clean CSI outside Play, no compilation failure/pending runner/XR restore, Standalone/Android XR restored. Report: docs/validation/photography-guide-2026-10-16.md; evidence and manifest: docs/evidence/photography-guide/. Quest stereo/comfort/performance acceptance pending. No manual scene wiring, build/install, upload, commit, push or deployment.

### Tape reference end stays fixed (2026-10-16)

MeasurementTool now snapshots the selected reference AnchorPosition when hooked, including height, instead of recomputing a sliding perpendicular foot from the hand every frame. Only the free end follows the hand. Both reference orders snap correctly; holster/resume still requires rehooking. Existing plane/point floor-plan readings, expected distances, tolerance, measurement layout profile, Sketch/scoring and scene placements unchanged; rendered segment length is not substituted for the authored measurement method. Extended actual tape-tool suite: 41 PASS / zero failures, including three horizontal/vertical endpoint checks, live reading, recording, other-reference-first, holster/resume, late measurements and event counts. Final Editor clean CSI outside Play, no compilation failure/pending diagnostic/XR restore, both XR startup settings restored; saved scene unchanged. Report: docs/validation/fixed-tape-hook-2026-10-16.md; manifest: docs/evidence/fixed-tape-hook/summary.json. Quest handling acceptance pending. No backend edits, build/install, upload, commit, push or deployment.

### Supplied new knife model (2026-10-16)

Replaced Evidence_KitchenKnife/Visual mesh/material with Assets/_Project/Art/Environments/MODELS/NEW/new-knife.glb through scene-local prefab overrides. Matching bounds permit the original 0.15 scale, position and rotation. Retained all evidence/grab/collider/trace/fingerprint/measurement objects and rules. Replacement has 18,640 vertices versus 416,620 previously. Rendered preview inspected; saved-scene audit shows only one changed prefab record containing the mesh/material overrides, no added/removed records. Final Editor clean outside Play, no compilation failure; no runtime code changes or additional Play suite. Report: docs/validation/new-knife-model-2026-10-16.md; evidence/preview: docs/evidence/new-knife-model/. No build/install, upload, commit, push or deployment.

### Body chalk guide fitted to actual silhouette (2026-10-16)

Refined the user's existing body guide to the current mesh pose with head/face/neck, arms/hands, waist/shorts, separated legs and feet. 135 editable point/segment pairs, approximately 2 cm silhouette clearance, level at the user's Y1.218 floor guide height, original 22 mm width/materials/tolerance/completion settings retained. Added rounded segment ends; connected every link and refreshed stale old-house world-space line endpoints. Preserved user body/guide transforms and all records outside the body guide; no runtime code or body definition changes. Authored guide is adopted by the existing runtime, retains its original inactive flag before procedure activation. Added 103 point/segment pairs (618 scene records), no removals.

Final validation: StoryContentTest 38 (real tracing/full physical lifecycle), BodyRemovalTest 41, five silhouette geometry checks and four preservation checks all pass. Full stroke remains outside the body, contour closed/non-self-intersecting, endpoints match all points. Final rounded-join preview inspected. Scene saved clean outside Play, no compilation failure/pending diagnostic/XR restore, XR startup settings restored. Report: docs/validation/body-chalk-fit-2026-10-16.md; manifest/preview/snapshots: docs/evidence/body-chalk-fit/. Authoring fit script: Tools/FitBodyChalkOutline.py (NumPy/Pillow, no game dependency). Quest tracing comfort/performance pending. No build/install, upload, commit, push or deployment.

### Sequential Play-test fixes: question, bottle, Sketch (2026-10-16)

1. Fingerprint: RC-03/HYP-02 now presents one contextual question and explicit confirmation, without the redundant legacy justification stage. RC-03 IDs/weights/gates unchanged; linked HYP-02 completion logged once, with no invented second answer. Stale answered integrated entries are skipped to avoid repeat/stalled queues. Verified first: ReasoningCheckpointTest 62, FingerprintStorageTest 33 PASS.
2. Bottle: EvidenceProp notice SphereCollider compensates model/parent scale so authored 1.5 m remains world-space (bottle was 0.225 m at scale0.15). TriggerStay retries after IOC gate unlock while already overlapping; record guard prevents repeat Found transitions. UV-only exceptions preserved. Model/solid collider/0.15 m grab reach unchanged. Verified second: actual physics EvidenceDiscoveryTest 10 and GrabReachTest 9 PASS.
3. Sketch: high-contrast gold/dark ring at actual snap radius appears immediately after the configured two wrong drops and persists during card return/next drag. Larger gold footer instruction visible; hint clears on success/cancel/switch/close and logs once. Verified third: CrimeSceneSketchTest 84 PASS, including actual rendered target gold pixels and non-overflowing caption. Screenshot visually inspected at docs/evidence/playtest-bugfixes/sketch/placement-hint.png.

Final total 198 PASS / zero failures. No scene/scenario/scoring-definition edits. Editor clean CSI outside Play, no compilation failure/pending runner/request/XR restore, XR startup settings restored. Report: docs/validation/playtest-bugfixes-2026-10-16.md; manifest: docs/evidence/playtest-bugfixes/summary.json. Quest recheck pending. No build/install, upload, commit, push or deployment.


### Display liquor table and fifth photo station (2026-10-16)

Added two render-only GIN_30K bottles to Table19 beside the living room, under Vicsi_House/DisplayLiquorBottles; tabletop-aligned, no colliders/EvidenceProp/grab behavior. Relocated existing fifth point scene-interior-right (order50) to (14.1,1.2,8.75), target (13.48,2.145332,6.99), label Table with liquor bottles. Floor marker and automatic photography arrow/pin use the new transforms. Updated photo-point-interior-right catalogue instruction and legacy fallback. Stable IDs, order, gates/scoring/tolerances unchanged; original kitchen evidence untouched.

62 layout checks + 47 photography procedure checks PASS, zero failures. Framing preview inspected; saved-scene audit: ten added visual/group records, four changed attachment/photo records, no removals. Editor clean outside Play, compilation good, no pending diagnostics, XR settings restored. Report docs/validation/display-liquor-photography-2026-10-16.md; evidence docs/evidence/display-liquor-photography/. Quest device recheck pending. No build/install, upload, commit or push.


### Major procedure test starting points (2026-10-16)

Added Tools > ViCSI > Scene Test Start: Normal play; IOC done -> photography; Photography done -> discovery/individual evidence work; All evidence procedures done -> measuring/sketch; Measurements done -> empty sketch with cards; Sketch done -> final reasoning/theory/conclusion; Investigation submitted -> closeout. Local Editor SessionState selection, default Normal; persisted _DevelopmentTesting SceneTestStart component defaults Normal for development headset builds. Release builds ignore presets. Only this object/component/root entry added to the scene, no other authored records changed.

Uses SDK FirstPersonLocomotor teleport for capsule and tracked rig, first photo station or clear kitchen approach (15.55,1.2,10.8) outside notice radii. No individual evidence presets. Later fixtures seed completed records/custody/body handling, permanent chalk guides, accurate report facts and vendor follow-up. Skipped checkpoints do not fabricate scored answers; past narrative gates complete silently and delayed STCS/teaching lines cannot replay. All evidence procedures done retains original props/anchors and normal late-measurement rules; use Photography done for real ordering checks. Measurements done starts an empty sketch with four elements/eight cards. Investigation submitted uses an unscored fallback fixture to enter real closeout.

Restart selected test reloads the full scene, via Editor window or X > Settings > Restart selected test. Normal play requires stopping Play and a fresh session. Gold TEST MODE indicator; existing sandbox isolates logs/owners/queue/network before default uploader startup, while preserving explicitly configured test queue seams. No student results uploaded.

Final 264 PASS / zero failures: SceneTestStartTest 77 (all presets/fresh restart/real bottle approach/chalk/delayed dialogue/unchanged real stores), photography47, reasoning62, Settings35, narrative10, uploader33. Initial spawn and explicit mock-queue conflicts corrected; older attempts retained. Final saved CSI scene clean outside Play, compilation successful, no pending diagnostics/requests/XR restore, XR startup restored. Editor window open on Normal play. Guide docs/DEVELOPMENT_TEST_STARTS.md; report docs/validation/scene-test-start-2026-10-16.md; manifest and evidence docs/evidence/scene-test-start/. Quest device check pending. No build/install, real upload, commit, push or deployment.

### Knife bag receiving-zone alignment (2026-10-17)

Fixed the saved EvidenceBag/ReceivingZone position to the visible paper bag opening: local (0.07, -0.02345, -0.089835), previously (0, 0, 0.12). Unity measured the visible opening outside the original 0.35 m receiving cube. Only that one scene transform line changed; the existing detector size, bag gesture, grab/procedure gates and custody rules remain intact.

CollectionFeedbackTest now uses synthetic left-trigger/right-grip input and actual physics overlaps at the visible opening for knife and bottle instead of directly calling TryInsert. Final 47 PASS / zero failures, covering prerequisites, opening alignment, insertion, sealing, explicit B storage and reset. The earlier test aimed at the invisible zone center passed despite its visual misalignment; initial synthetic-input fixture failures are retained/documented. Final Editor clean CSI outside Play, compilation successful, no pending runner/XR restore, corrected opening contained after reload. Report docs/validation/knife-bagging-2026-10-17.md; evidence docs/evidence/knife-bagging/. Quest hardware recheck pending. No build/install, real upload, commit, push or deployment.

### Knife bagging test start (2026-10-17)

Added Knife bagging to Tools > ViCSI > Scene Test Start (appended enum value 7). Prepares only the knife to ReadyForCollection with swab/print/report prerequisites; retains the loose original prop and real collection gates, equips the left-hand bag and faces the knife from the kitchen approach. Other evidence and inventory remain unprepared; no scored answers fabricated. Existing milestone presets retain their meaning. Restart restores the knife and equipped bag. Guide docs/DEVELOPMENT_TEST_STARTS.md.

SceneTestStartTest: 92 PASS / zero failures across all eight presets, including knife insertion/seal/storage/restart and normal-play restoration. Final clean CSI Editor outside Play, compilation good, no pending runner/XR restore, both XR startup settings true. Window left open on Knife bagging. Evidence docs/evidence/knife-bagging-start/. No build/install, real upload, commit, push or deployment.

### Tape hook receivers, aligned references and foot-side wall (2026-10-17)

All eight reference receivers enlarged to 0.8 x 0.9 x 0.4 m, projecting 0.18 m into the room. Each fixed hook aligns with its evidence anchor in the floor plan. Moved the body and blood-pattern cabinet references to the wall near the victim's feet (Z11.632), named Foot-side Kitchen Wall. Existing stable IDs retained. Knife east-wall and bottle cabinet-end hooks are aligned too. Synchronize Moved Kitchen Evidence persists this layout without resetting evidence placements. Updated expected distances, instructions and Sketch feet/labels. New sessions use new-house-v3; local dashboard registers frozen v3 geometry while historical/v1/v2 remain intact.

Final 201 Unity checks (references42, actual tape41, per-evidence34, Sketch84) plus seven backend profile tests PASS, zero failures. TypeScript/scoped ESLint pass. Initial invalid suite-list invocation and stale loaded v2 test expectation retained separately; final refreshed reruns authoritative. Preservation checks confirm only 30 reference/Sketch/layout-ID scene records changed, no additions/removals; evidence, house, IOC and prior bag receiver correction preserved. Clean CSI Editor outside Play, compilation good, no pending runner/XR restore, both XR startup settings enabled. Report docs/validation/reference-alignment-2026-10-17.md; manifest docs/evidence/reference-alignment/summary.json. Quest handling acceptance pending. No build/install, real upload, commit, push or deployment.

### Recorder exactly-three report lines (2026-10-17)

Recorder Notes now accepts exactly three unique lines per evidence item. Fourth B addition is blocked without replacing choices; remove a Notes line with B to free a slot. Prompt/full-state hint explain the cap. Actual input and test selection share the insertion guard; UI Confirm and RecorderTool callback independently reject wrong counts/duplicates before changing records or logging. Existing serialized minimumLinesRequired remains for compatibility but no longer configures this UI. No evidence options, prerequisites, scoring rules or scene changes.

Final 107 PASS / zero failures: RecorderReportLimitTest25 (actual synthetic XR B/stick add/refusal/remove/replace, confirm/overfill/duplicates, next-report reset), BodyRemovalTest41 and ResponsiveOptionLayoutTest41. Initial new test failed to resolve before explicit Unity asset import; final imported run is authoritative. Clean saved CSI outside Play, compilation successful, no pending runner/XR restore, both XR startup settings enabled. Report docs/validation/recorder-three-lines-2026-10-17.md; manifest docs/evidence/recorder-three-lines/summary.json. Quest hardware acceptance remains pending. No build/install, real upload, commit, push or deployment.

### Bottle on-scene fingerprints (2026-10-17)

The newer bottle task was missing from these notes. Bottle flow now requires final photography, fixed-reference and discovered-evidence measurements, powder/brush, tape lift and explicit B sample storage before Recorder and collection. Three separate dark powdered prints identify John, Peter and Paul only after storage, with one automatic announcement and unscored identification event. Findings and upload summaries preserve all three identities. Knife processing remains independent. Bottle fingerprinting is available in Scene Test Start; restart clears recovery and custody.

Continuation recounted accepted saved reports: 407 Unity checks plus 33 backend checks PASS, zero failures; suites were not rerun. Four scene preservation checks were rerun and pass; original bottle placement preserved. Powder preview inspected. Live Editor clean CSI outside Play, no compilation failure/pending diagnostic/request/XR restore, both XR startup settings enabled. Earlier failed attempts retained separately. Report: docs/validation/bottle-fingerprint-2026-10-17.md; authoritative counts/paths: docs/evidence/bottle-on-scene/summary.json. Quest handling/readability acceptance pending. No build/install, real upload, commit, push or deployment during continuation.
### Bottle measurement references at bottle height (2026-10-17)

Aligned REF-BOTTLE-1 and REF-BOTTLE-2, including their child receiving zones, to the bottle measurement anchor at world Y 2.117 m. Rear-wall reference was 2.25 m; cabinet-end reference was 1.80 m. Updated NewHouseInvestigationSetup with Align Bottle Reference Height and called it from Synchronize Moved Kitchen Evidence so synchronization preserves the height.

Live checks confirm both anchors/zones align and accept hook contact. Floor-plan distances remain 0.332011 m and 0.591272 m; measurement requirements are byte-identical. Snapshot comparison confirms only the two reference transform positions changed, no scene additions/removals. Evidence: docs/evidence/bottle-reference-height/preservation.json. Scene saved clean outside Play, scripts compiled successfully. No Play suite rerun for this height-only authoring change; no build/install, upload, commit, push or deployment.
### Photography points 6 and 7 exchanged (2026-10-17)

Swapped the standing positions of sequence 60 (scene-body-in-scene) and sequence 70 (scene-stair-incident-area): sixth now (16.25, 1.20, 12.10), seventh now (18.50, 1.20, 12.10). Floor markers follow their stations; each station retains its original subject target and faces it from the new position, keeping its instruction appropriate.

Verified swapped coordinates, original framing targets, aligned floor markers and clean saved scene. Captured the live scene before editing to preserve the user's existing unsaved work in the comparison; only four photography transform records changed, no additions/removals. Evidence: docs/evidence/photo-points-6-7-swap/. No runtime code changes or Play suite rerun; no build/install, upload, commit, push or deployment.
### Scene photography no longer advances evidence photography (2026-10-17)

PhotographTool now resolves scene-route shutter ownership before evidence capture. Unfinished station zones consume route presses, including wrong-angle/out-of-sequence attempts, without taking evidence photos or triggering evidence warnings. The final scene photo cannot also advance evidence. Separate evidence photos outside unfinished zones and after route completion retain existing behavior.

Added PhotographyShutterOwnershipTest (14 PASS); existing ScenePhotographyProcedureTest (47) and PhotographyGuideTest (26) also pass: 87 PASS, zero failures. First report recovered through the existing runner after a Play-mode script reload; subsequent suites collected normally. Final clean CSI outside Play, no compilation failure/pending runner/XR restore, both XR startup settings enabled. Report: docs/validation/photography-shutter-ownership-2026-10-17.md; exact results: docs/evidence/photography-shutter-ownership/summary.json. No scene edits, build/install, real upload, commit, push or deployment. Quest controller acceptance pending.
### Body Recorder targeting and test point (2026-10-17)

Diagnosed BODY-A's disabled solid collider: Recorder's trigger-ignoring ray hit the floor, while old tests bypassed aiming. Recorder now explicitly resolves the existing examined-in-place acceptance box before solid obstructions, with shared pointer/action targeting, normal range/gates and no change to body physics. Disabled zones/removed bodies are excluded; solid evidence retains priority.

Added Body recording to Scene Test Start (appended enum9), with final body photography, Marker A and scene measurements prepared, Recorder equipped and player facing BODY-A. No reports, samples, removal or scored answers are seeded. Player records all three findings, holsters, approaches empty-handed and confirms removal; restart restores the exercise. All ten presets verified.

Accepted results: BodyRemovalTest43, SceneTestStartTest116, RecorderReportLimitTest25 = 184 PASS, zero failures. Earlier body attempt passed real aiming but left Recorder equipped before removal; corrected fixture performs the empty-hands step and final full lifecycle passes. Clean saved CSI outside Play, compilation good, no pending runner/request/XR restore, XR startup enabled. Test window open on Body recording. Report docs/validation/body-recorder-2026-10-17.md; manifest docs/evidence/body-recorder/summary.json. Quest physical acceptance pending. No scene/definition edits, build/install, upload, commit, push or deployment.
### Evidence Relationships Review (2026-10-17)

Replaced interactive Evidence/Deduction Board with one earned relationship question at a time, four plausible interpretations, flexible-height padded answers, select plus confirm, immutable responses and neutral investigation feedback. Reuses the checkpoint renderer/wall-safe placement in its own instance. Old canvas/node/line interaction is inactive; EvidenceBoardController retains serialized callbacks as a review adapter. Relationship definitions/IDs, source-target/state records, legacy board fields and node/line classes remain for compatibility. All six current questions, including REL-paul-timeline-contradiction / statement_vendor_paul_3am, are authored in mirrored Unity/dashboard rules. No new scenario relationships.

Deduction stays 60% checkpoints / 25% Evidence Relationships / 15% final conclusion. Additive relationshipReviewVersion=1 scores immutable interpretation IDs using existing weights/penalty; historical missing/zero-version board snapshots retain their original path. Locked interpretation events survive raw-event omission/caps, freeze with the conclusion and are checked against the upload snapshot server-side. Existing Supabase tables and payload version remain; no SQL migration. Local sibling dashboard preserves Evidence Relationships and displays interpretations/order for both roles. Procedural training remains non-scored.

Conclusion submission requires all currently earned review answers. New discoveries while the conclusion picker is open return it to pending review. RC-03/vendor gates, supported/unsupported/inconclusive paths, closeout, restart and lobby workflow retained. No manual Editor wiring: runtime install + existing serialized menu adapter.

Accepted: 391 Unity assertions across 11 suites, zero failures; backend suite 209 pass/0 fail/6 conditional skips; scoring/integration 40 pass; dashboard presentation 29 pass; actual Unity upload/scoring parity 2 pass; current Unity-scoped backend 49 pass/1 skip. TypeScript and scoped ESLint exit0. Two optional historical frozen-file audits retain older truth/hash mismatches and were not rewritten. Repeated Editor reloads exhausted the 8 GB machine (16.9 GB private); restarted the stalled Editor and final supported/inconclusive reruns passed56/11. Final Editor: clean saved CSI outside Play, compilationFailed=false, zero error-CS console entries, no pending runner/request/XR restoration, Standalone/Android XR startup enabled, old board inactive and active nodes/lines0.

Report: docs/validation/evidence-relationships-review-2026-10-17.md. All24 choices: docs/validation/relationship-review-questions-2026-10-17.md. Accepted manifest: docs/evidence/relationship-review/summary.json (excludes superseded attempts). Physical Quest/live Supabase acceptance not performed. No commit, push or deploy.
### Flexible inventory UI (2026-10-17)

Replaced fixed 100-unit rows/narrow horizontal columns with measured stacked text in EvidenceInventoryEntryView. Rows retain20px horizontal/16px vertical padding, 8px detail gaps, 12px list gaps, readable28px names/24px details, independent dynamic heights and full-width wrapping. Duplicate narrow placeholder-name slot hidden; icon refs, marker/status/lock information and equipped/selection indicators retained. EvidenceInventoryUI installs shared ResponsivePanelLayout (240..740 height, bounded clipped scrolling, pinned header/footer, flexible empty state), keeps HudFollowCamera wall-safe placement, auto-scrolls selected rows and adds left/right scrolling plus controller-ray row/scroll/close controls. A equip/return and B close retained. Refresh preserves selection where possible and deactivates destroyed rows immediately. No scene/prefab resave or manual wiring, custody/procedure/scoring/dashboard changes.

InventoryUILayoutTest is a real source fixture using the authored inventory with six synthetic sealed items admitted through the public inventory API. It verifies rendered glyph containment for long names/reasons and narrower widths, padding/gaps, row growth, panel cap, scroll selection, clipped pointer targets, ray selection, locked action custody, close/reopen and empty restoration. Accepted22 PASS plus existing EvidenceInventoryTest29 PASS =51, zero failures. Zero C# console errors and compilationFailed=false. Editor clean saved CSI outside Play, no pending runner/request/XR restore, Standalone/Android XR startup enabled. Report docs/validation/inventory-flexible-ui-2026-10-17.md; manifest docs/evidence/inventory-flexible-ui/summary.json. Quest physical comfort/readability acceptance remains manual. No commit, push or deploy.
### Dialogue without speaker prefixes (2026-10-17)

Removed live-dialogue speaker prefixes from both display-generated catalog labels and legacy inline role/name labels: Dispatcher/Dispatch, IOC, SOCO, Recorder, Professor, First Responder, Vendor, witness names and similar current roles. New DialoguePresentation.SpokenText normalizes recognized inline labels and explicitly matching authored speakers before STCSNotificationUI queues the line. Live label and CurrentText show the sentence only. DialogueHistoryManager records the same sentence with speaker/source/category metadata separate; history retains attribution. Ordinary colons and 3:00 AM timestamps are preserved. Catalog/legacy assets, IDs, case facts, dialogue queues/priorities/timing/skips and assessment data unchanged. New behavior is in real source files, no scene wiring.

DialogueCatalogTest now checks ten inline-role paths, catalog speaker metadata, ordinary colons and the actual vendor sentence. DialogueHistoryTest expects prefix-free rendered text while checking attribution and recording. Accepted84 PASS/0 FAIL (catalog60/history24). Final Editor clean saved CSI outside Play; compilationFailed=false and zero error-CS entries; no pending diagnostic request/XR restoration; Standalone/Android XR startup enabled. Report docs/validation/natural-dialogue-2026-10-17.md; manifest docs/evidence/natural-dialogue/summary.json. No commit, push or deploy.
### Per-marker pickup after recording (2026-10-17)

User supersedes the earlier uniform-prompt/revert policy: a tent that applied no mark can return immediately; a successful item/trace marker waits until its own evidence is recorded (Logged or later), then shows the B pickup hint. No seconds-based delay. Existing placement already returns evidenceId only on TransitionResult.Applied, so extra tents beside marked evidence stay independent and immediately retrievable. EvidenceTentPickup now gates hint/action identically, filters eligible nearest candidates through the coordinator, protects prompt ownership when switching candidates, blocks direct early reclaim, and logs cleanup only once. It no longer calls state-rewind APIs; status, marker references, report lines and original events remain intact. Slots return on cleanup.

User explicitly confirmed Body Marker A inclusion. LetterTentTool installs the same pickup component, records its letter in analytics and frees the letter slot on cleanup. BodyRemovalInteraction yields its A hint to an eligible B marker hint and restores it after pickup; physical marker removal preserves recorded A and body-removal eligibility. Legacy tent-pickup-already-documented dialogue ID retained with natural guidance: Record the evidence before picking up its marker. No scoring/upload schema/dashboard changes, scene wiring, commit, push or deployment.

Accepted91 PASS/0 FAIL: numbered pickup21 and trace marker21 in docs/evidence/marker-pickup-recording/run2; full real letter/Recorder/body-removal/restart49 in run1. Initial numbered/trace fixtures omitted existing on-scene fingerprint prerequisite, so MarkLogged correctly failed; corrected fixtures complete fingerprinting and also verify it alone does not unlock pickup. Production recording gates unchanged. Final Editor clean saved CSI outside Play, compilationFailed=false, zero error-CS console entries, no pending request/XR restoration, Standalone/Android XR startup enabled. Report docs/validation/marker-pickup-recording-2026-10-17.md; manifest docs/evidence/marker-pickup-recording/summary.json. Quest proximity/hint comfort acceptance remains manual.
### All evidence measured test start (2026-10-17)

Appended SceneTestStartingPoint.AllEvidenceMeasured=10 without changing old values. Tools > ViCSI > Scene Test Start > All evidence measured starts after all four items have final photos, required chalk/markers and all fixed-reference/pair measurements, with no report lines or body findings recorded. Sampling/fingerprints remain unfinished and follow normal gates; no collection, inventory, body removal, sketch or scored answers seeded. Recorder equipped, player faces body from kitchen approach. Five real linked physical tents (three item markers, knife trace, Body Marker A) wait for their own recording; spare numbered tent near approach is immediately pickable. Guarded Editor/development helpers instantiate existing tent templates and register normal pickup components. Restart reloads all unrecorded items, measurements and six tents. Sandbox prevents real student uploads.

SceneTestStartTest now checks all11 presets plus this preset's state, marker ownership/unlock, actual spare reclaim and restart. First attempt passed116 existing checks but caught body's separate final-photo-before-measurement gate; fixture corrected, production gates unchanged. Accepted131 PASS/0 FAIL in docs/evidence/all-evidence-measured/run2/SceneTestStartTest.txt. Final Editor clean saved CSI outside Play, compilationFailed=false, zero error-CS entries, no pending diagnostics/XR restore, Standalone/Android XR startup enabled. Test window open with All evidence measured selected locally; saved component default remains NormalPlay. Guide docs/DEVELOPMENT_TEST_STARTS.md; report docs/validation/all-evidence-measured-start-2026-10-17.md; manifest docs/evidence/all-evidence-measured/summary.json. No manual wiring, commit, push or deploy. Quest physical placement/readability acceptance remains manual.
### Evidence custody pass (2026-10-17)

Implemented explicit bulk custody pass at EvidenceHoldingCrate. Approach during closeout, A opens the actual stored-item review, A confirms, B cancels. Shared flexible/padded/scrolling wall-safe layout, pointer arrows and right-stick scroll; metric UI anchor cancels the crate scale. Revalidates range, phase, current item set, complete processing, sealed/stored state and authored requirements before any custody mutation. No partial transfer, automatic proximity deposit or repeat confirmation. Deposited bags/samples are removed from selectable inventory and cannot be re-equipped; definitions, sample source IDs, storage history and box ID retained. BODY-A and tools remain separate.

Existing closed crate renderers are hidden at runtime; an open tray displays render-only copies of existing assets: stacked bags, lined-up blood containers and face-up fingerprint cards on a rear shelf. Models fit inside reserved cells without overlaps and have no evidence behaviours/colliders. Transfer logs each EvidenceDeposited plus CustodyPassCompleted and flushes immediately. Both events survive the essential upload envelope and local dashboard ingestion when raw events are disabled. Existing Evidence custody training row/session_events storage reused; no schema migration or grading change. Final inventory confirmation, survey, IOC report and scene release now require custody pass; deduction remains 60/25/15.

Appended CustodyPass=11 test start (six sealed stored entries and empty box, player beside it), preserving old enum values. Restart clears box and restores all six entries. Existing zone installs runtime UI/display; no Editor wiring or saved scene change. Local Scene Test Start window left on Custody pass; saved component remains NormalPlay.

Accepted340 Unity PASS/0 FAIL: final CustodyPass50, SceneTestStart143 and SettingsPauseMenu35; run2 closeout50, inventory29 and upload service33. Dashboard44 PASS/0 FAIL/0 skipped using the actual final Unity raw-disabled wire payload; TypeScript and scoped ESLint passed. Initial preset ground-ray assertion hit the arriving player capsule; corrected the diagnostic to use prepared floor Y while independently checking the box approach XZ. Final Editor outside Play, clean saved CSI scene, compilationFailed=false, zero error-CS console entries, no pending diagnostics/XR restore, Standalone and Android XR startup enabled. Report docs/validation/custody-pass-2026-10-17.md, manifest docs/evidence/custody-pass/summary.json, preview box-contents.png. Quest comfort and live Supabase remain manual. Client/dashboard allowlist changes need coordinated release. No commit, push or deploy.
### Connected-headset Play Mode recovery (2026-10-17)

User reported Play crash with connected headset. Initial Editor still alive/outside Play; manual attempt logged D3D11 Failed to create RenderTexture (1824x1872 fmt19 aa1), HRESULT0x8007000e. Marshal confirms OutOfMemoryException. System commit only~295MiB available of32472MiB; Editor~13.4GB private, hardware8GB RAM/4GB RTX2050. UnloadUnusedAssets/GC did not recover allocations. Also found persistent WallPenetrationTunneling TunnelingEffect _meshFilter=null and no mesh child, while SmoothMovementTunneling had a valid child. Restored mesh/renderer child using sibling's existing setup/material, assigned serialized reference, saved CSI scene. No package-cache edits or C# gameplay changes.

Requested normal Editor window close/restart after saving; old process emitted native crash during shutdown, preserved tail in docs/evidence/play-mode-recovery/shutdown-stack.txt. No force termination. Reopened clean saved CSI via Unity6000.5.6f1 PID53296, Logs/PlayModeRecovery.log. Fresh private memory~4.2GB and free system commit~16.5GB. Diagnostic bridge reconnected. Retried manual Play with Standalone XR enabled and CustodyPass=11. Runtime: Play=true, paused=false, compilationFailed=false, preset ready/no setup failure, six inventory entries, EvidenceInventoryReview phase, wall mesh assigned. New log checks:0 RenderTexture allocation errors,0 missing-mesh assertions,0 C# errors;0 new native crashes since fresh Editor loading.

IMPORTANT: Headset rendering NOT yet verified. OpenXR initialized, headset detected, but userPresent=false/inputFocus=false, display.running=false and eye dimensions0. Asked user to wear headset/enter Quest Link and report image. Play left active at custody preset awaiting this external check. Existing haptics/unsupported-feature/audio/bridge warnings remain. Do not claim full active-headset success. No XR quality or Android startup changes, scoring/custody/upload changes, commit, push or deploy. Report docs/validation/play-mode-recovery-2026-10-17.md; runtime proof/log counts in docs/evidence/play-mode-recovery/.
### Latest completed task — ViCSI dashboard audit and redesign (2026-10-17)

Completed the current-code and read-only Supabase audit, then redesigned the sibling dashboard at C:/Users/Roduf Eleu/Desktop/vicsi-dashboard. Confirmed the active 1.7.0 deduction formula remains Reasoning Checkpoints 60%, Evidence Relationships 25%, Final Conclusion 15%, and that the physical board is disabled in favor of the confirmed Evidence Relationships Review. Added a shared collapsible ViCSI shell, compact assessment summaries, student attempt selection, chronological reasoning, neutral relationship headings and interpretations, final conclusion outcomes, filtered instructor tables/analysis and student/session drilldowns. Training Analytics is separate from current scoring and reflects actual body transport, custody, IOC/SOCO and closeout events. Historical versions remain distinct; mixed-version averages and test-session cohort mixing are prevented. No deduction values or upload contract changes; CSV filtering now matches the UI, read adapters paginate/batch, and optional findings can fall back to the frozen snapshot.

Validation: production Next.js build/TypeScript passed; full lint has zero errors and seven pre-existing warnings; 42 dashboard tests, 52 browser checks and 36 authentication/token/crypto tests passed. Standard scoring suite: 212 passed, zero failed, seven conditional skips. Extended Unity-linked run: 216 passed, two stale historical truth/hash comparisons failed, one skipped; these older references were preserved rather than overwritten. Current review/custody wire captures passed direct dashboard tests, and actual page components rendered with a current captured Unity upload plus 20 anonymized live historical attempts using GET only. Production still has no current 1.7 sessions and lacks optional findings/insights tables; migration 017 and scenario-registry updates were proposed only. Authenticated deployed current-session end-to-end acceptance remains pending; local component/interaction checks are not represented as that acceptance.

Reports in the dashboard repository: docs/audits/2026-10-17-current-system.md and docs/validation/dashboard-redesign-2026-10-17.md; test logs/screenshots: docs/evidence/dashboard-redesign/. No Unity Editor wiring or Unity runtime changes for this dashboard task. No production writes, migrations, commits, pushes or deployment; the preceding connected-headset rendering acceptance remains unresolved and was not retested by this work.

### Latest completed task - Session record simplification (2026-10-18)

Supersedes the preceding dashboard redesign handoff. Implemented locally in Unity and C:/Users/Roduf Eleu/Desktop/vicsi-dashboard: completed-procedure timeline, frozen text/options/weights/selections for all 13 scored questions, stored verified component results and final score, and simplified student/instructor lists and session details. Existing 1.7.0 scoring remains 60% reasoning, 25% relationships and 15% conclusion. HYP-01/HYP-03 remain unscored; HYP-02 integrates RC-03. Current uploads omit redundant legacy analytics while preserving verification facts, identity protections and historical compatibility. Existing JSONB event storage is reused; no database migration is required for this record change.

Continuation checked saved evidence logs, without rerunning suites: Unity record 70 assertions and 576 real scoring vectors; Unity scoring 41; upload service 33; record/API 17; responsive browser 21; dashboard regression 42; scoring regression 217 with two optional fixture skips; authorization 27. Recorded runs have zero failures; production build and TypeScript are documented as passing. Unity/server parity is within float precision with matching two-decimal display, not bit-identical arithmetic. Full report and exact limits: docs/validation/session-record-simplification-2026-10-18.md. Evidence: docs/evidence/session-simplification/.

Fresh Unity requests passed isolated transport and the actual API handler using an in-memory database substitute. Authenticated live staging upload/database-policy execution and active headset rendering remain unverified. The report records Editor memory exhaustion and successful isolated headless validation; do not infer a currently healthy graphical Editor or headset from those runs. No commit, push, deployment, seed, migration execution or production write. This continuation only synchronized the handoff and task log with the existing completed report.

### Latest completed task - Evidence display numbering (2026-10-18)

Added shared versioned display mapping: BODY-A -> Body A – John (Body A in procedural prose), EVD-014 -> Evidence 1 – Kitchen Knife, EVD-015 -> Evidence 2 – Blood Spatter, EVD-018 -> Evidence 3 – Empty Liquor Bottle. Unity EvidenceDisplay/PlayerDisplayName and the dashboard evidenceDisplay/resolveEvidenceName format prompts, notes, inventory and samples, procedures, Recorder, measurement/sketch labels, reasoning/review prose, completion/custody/closeout, student/instructor session details, historical timelines and CSV label columns. Physical tents now reserve 1/2/3 for the three items regardless of placement order; body uses A, trace/spare tents use 4–6. Prepared development starts seed the same item numbers.

All internal IDs, sample-source references, relationships, statements, prerequisites, grading and database records remain stable. SHA256 audit against this task's starting state confirms all 130 existing serialized scene/data/scoring assets unchanged. Uploaded identifiers and wire schema are unchanged; historical display resolves at read time. CSV retains technical ID columns alongside added human labels. Developer fields, logs, test artifacts and payloads intentionally retain IDs.

Accepted: 672 Unity assertions across 15 suites, zero failures; fresh session/API/display21, dashboard42, scoring217 plus optional actual bottle4/custody3 wire suites, responsive browser21, TypeScript, scoped lint and production Next build pass. Browser DOM checks reject raw active IDs. Full-case Unity fixture exports 576 scoring vectors; fresh API/session parity checks pass. Earlier interrupted/stale-fixture/compile-error attempts retained separately; tests now honor the current bottle final-photo/on-scene fingerprint and minimal upload requirements. Offline custody observer uses the real receiving bounds without changing production gates. Graphical Editor crashed during normal close after a clean saved scene; fresh isolated runs completed and all Editors exited. Zero C# errors in accepted logs, no pending test request, XR startup settings enabled. No headset/live Supabase acceptance claimed.

Report: docs/validation/evidence-display-numbering-2026-10-18.md. Accepted manifest: docs/evidence/evidence-display/summary.json. New captures and screenshots are in that evidence folder; existing diagnostic harnesses also refreshed shared local fixture outputs. No commit, push, deployment, seed, migration or production-data write.


### Completed reasoning display cleanup (2026-10-18)

Added shared QuestionDisplayNames_1/questionDisplay.v1 mappings derived from the six current definitions, three legacy checkpoints and authored relationship names. Game headings, confirmation hints, notifications, history and completion text plus student/instructor question captions, timelines, review prose and historical answers now resolve HYP/RC/REL IDs readably. HYP-02-hosted current fingerprint uses Reasoning Question 3; standalone historical HYP-02 retains Question 2. IDs, scoring rules, prerequisite logic, weights, source snapshots and upload format remain unchanged; technical CSV ID columns remain, with readable columns appended.

Accepted 313 Unity assertions in seven suites with zero C# errors, 52 dashboard tests, 18 session/upload tests, 21 browser checks, all 219 scoring checks across general plus optional capture runs, TypeScript, scoped lint and production build. Direct upload regression preserves HYP-03 in local mocked storage while both roles show readable labels and the original grade. Earlier failed encoding/casing/stale-text expectations are documented separately; no production code scoring/upload change was needed. Report: docs/validation/question-display-cleanup-2026-10-18.md; usage inventories and accepted manifest: docs/evidence/question-display/. No commit, push, deployment, migration, seed or production-data write.


### Final consolidation pass (2026-10-18)

Inspected current Unity, upload/SQL contract and both primary dashboard paths before changing presentation. Most requested simplification was already implemented. Unity prose now uses full Body A / John naming, shared display JSON covers historical phone/glove names, numeric unknown evidence IDs use a readable fallback, duplicate dashboard name constants were removed, and accessible session View labels use scenario/date. Internal IDs, frozen questions, choices, weights, scoring, prerequisites, upload schema and database records stay unchanged. Added a full captured-upload-to-both-detail-routes regression checking all 13 questions / 50 actual options, weight associations, selections, earned/max values, timeline and 60/25/15 components.

Passed 342 Unity checks in six suites with zero C# errors; 52 dashboard, 19 session/upload, 219 scoring and 21 browser checks, TypeScript, scoped lint and production build. Read-only scene audit finds both enabled build scenes free of missing scripts/broken references/local fileIDs; new house active, old house inactive, rear-kitchen evidence and new-house-v3 measurements/sketch align. Geometry audit passes 61 checks and retains one unresolved exterior photography standing-room overlap with Sidewalk surface. No environment changes; 147 existing serialized scene/data files hash-identical, only presentation JSON changed. Old migration comparison baselines predate later bottle/scene additions and remain reported separately. Report: docs/validation/final-consolidation-2026-10-18.md; manifest: docs/evidence/final-consolidation/summary.json. No commit, push, deployment, seed, migration or production-data write.


### Exterior photography clearance resolved (2026-10-18)

Raised only ScenePhotography/ScenePhotoPoint_10_scene-exterior-establishing from (16.9, 0, -0.8) to (16.9, 0.03, -0.8), matching the sidewalk top. This resolves the final-consolidation standing-room finding while preserving angle, interaction settings, scene references, geometry and all unrelated transforms; 739 other pre-existing source/data files remain hash-identical.

All 62 environment checks and 40 focused photography checks pass with zero C# compile errors and zero broken references. The wider procedure suite has 44 pass / 3 marker assertion failures, identical with the original station height in a control run; those unrelated tests/logic were left untouched. The fixed scene was restored exactly. Report: docs/validation/exterior-photo-clearance-2026-10-18.md; evidence: docs/evidence/exterior-photo-clearance/summary.json. No commit, push, deployment or production-data write.


### Menu wheel comfort update (2026-10-18)

Inventoried the non-tool radial UI: UtilityMenuController in CSI_Environment and Tutorial_ToolTest, including phase variants and runtime Settings. Added a menu-only MenuWheelComfortProfile: ring diameter 700 to 660 units, label radius 504 to 250, bold labels 20 to 24, central release hint, readable inactive contrast and stable color selection. Canvas scale and 0.65 m viewing distance remain unchanged. Tool Wheel source/template, all five scenes, shared wall solver/input/locomotion code and other fixed UI remain hash-identical.

Passed 251 focused menu and 57 shared UI-comfort assertions with zero C# errors. Settings has 28 pass / 7 height adjustment/persistence failures, identical with the original menu source in a control run; height logic/tests were left untouched and the exact accepted compact source restored. UIComfortInputTest has an existing ALL PASS header/runner exit mismatch, documented without modifying either source. Quest 3S optical/stereo/eye-head effort acceptance remains pending; ADB found no device. Report: docs/validation/menu-wheel-comfort-2026-10-18.md; manifest: docs/evidence/menu-wheel-comfort/summary.json. No commit, push, deployment or production-data write.


### IOC assessment direction guide (2026-10-18)

Implemented the approved guide for IOC assessment only: Investigate here at the existing survey position, Survey the scene inside it, and Examine this / Move closer for required observations. It reuses the unchanged photography arrow/pin graphic, shader and projection, displays centralized evidence labels, hides for dialogue/checklist/modal UI and after assessment, and preserves out-of-order observations and the existing two-of-three possible-evidence quota. The floor highlight, original photography wording/behavior, all scenes, menu/Tool Wheels, controls, prerequisites, scoring and uploads remain unchanged; the only existing runtime edit auto-adds the guide once.

Passed 35 focused IOC guide, 19 original IOC/SOCO foundation and 26 original photography-guide assertions (80 total, zero failures) with zero C# compile errors. Added an Editor-runner exclusion to keep the IOC-specific test at its real starting phase; 1,337 other scoped pre-existing source/data files remain hash-identical. Initial fixture failures (a tooltip used as spoken dialogue and an occluded bloodstain viewpoint) are preserved separately and corrected without relaxing gameplay gates. No headset was connected; Quest visual acceptance remains manual. Report: docs/validation/ioc-direction-guide-2026-10-18.md; manifest: docs/evidence/ioc-direction-guide/summary.json. No commit, push, deployment or production-data write.


### First Responder and IOC NPC approach guides (2026-10-18)

Added NPCApproachGuide automatically to the existing First Responder and IOC NPC presentations, using their authored role label anchors and the unchanged photography arrow/pin shader/projection. The guide displays Approach plus role/distance, changes the FR instruction to Speak with them inside the original range, yields to dialogue/modal UI and the IOC briefing panel, and disappears after each NPC handoff. Vendor and late-game closeout remain excluded; the original assessment and photography guides take over in their own phases.

Passed 33 NPC approach, 19 original phase foundation, 35 IOC assessment-guide and 26 original photography-guide assertions (113 total, zero failures) with zero C# compile errors. Existing NPC visibility/pose methods, all interaction controllers, scene/actor/evidence/marker transforms, menus/Tool Wheel, scoring and uploads remain unchanged; 1,341 other existing scoped source/data files are hash-identical. No headset was connected, so visual acceptance remains manual. Report: docs/validation/npc-approach-guide-2026-10-18.md; manifest: docs/evidence/npc-approach-guide/summary.json. No commit, push, deployment or production-data write.


### Evidence discovery dots (2026-10-18)

Added small cyan/white circular hints to still-undiscovered physical evidence after all scene-photography requirements finish. Dots appear together on the completion event and hide individually on Found; they have no labels/arrows, input, raycasters or colliders, retain a 5 cm halo independent of prop scale, face the headset above visible bounds and respect opaque geometry. Hidden fingerprint/external-trigger targets and blank held tool props stay excluded. The current proximity discovery timing is preserved, so evidence found during photography never relights afterward; no answer was received to the optional discovery-delay question.

Passed 45 focused dot, 10 original discovery and 26 original photography-guide assertions (81 total, zero failures) with zero C# or shader compile errors. Actual Direct3D pixels verify visibility and wall occlusion; album rendering excludes all dots with nested-safe restoration. Existing runtime edits only attach the dot and hide/restore visuals during capture; original discovery logic, all scenes/data/transforms, other guides, menu/Tool Wheels, scoring and uploads remain unchanged, with 1,383 other scoped source/data files hash-identical. No headset was connected; Quest acceptance remains manual. Report: docs/validation/evidence-discovery-dots-2026-10-18.md; manifest: docs/evidence/evidence-discovery-dots/summary.json. No commit, push, deployment or production-data write.


### Photography standing-circle visibility (2026-10-18)

The active blue circle now hides inside its original standing zone, returns on exit before a successful photo (including rejected attempts), and stays hidden after completion. Only runtime change is ScenePhotoPoint visual refreshing; authored positions/radii, camera targets/angle rules, route, guide arrows, NPC/IOC guides, evidence dots, menus/Tool Wheel, input, scoring and uploads are unchanged. All seven visual objects are separate children and continue tracking while hidden.

Passed 65 new circle checks, 26 original guide, 14 shutter ownership, 19 phase foundation and 44 First Responder turnover checks: 168 total, zero failures, all processes exit 0 and zero C# compile errors. Existing phase/turnover tests only changed visibility expectations to account for standing inside a circle. 1,390 other scoped source/data files hash-identical. Physical Quest verification remains manual. Report: docs/validation/photography-standing-circle-2026-10-18.md; manifest: docs/evidence/photo-zone-visibility/summary.json. No commit, push, deployment or production-data write.


### Vendor ground-arrow trail (2026-10-18)

Added cyan outlined ground chevrons to the physical vendor, gated by the original confirmed knife-print finding plus confirmed RC-03-FINGERPRINT response (integrated HYP-02). Identification/eligibility/selection alone do not unlock it, and any confirmed answer weight is accepted by the original gate. The authored current-house route passes through the existing exit and steps, hides for modal/spoken UI and vendor arrival, returns if leaving before the interview, retires after statement discovery and resets on reload. No input, movement or scoring ownership. A nested-safe photo capture scope excludes arrows from albums.

New VendorPathGuide/VendorPathRoute classes, scene-specific Resources/VendorPathRoute_CSI.asset and focused test; only existing VendorWitnessInteraction attachment and PhotoCaptureListener capture scope changed. All 1,393 other scoped existing source/data files, including scene/prefab transforms, Tool Wheel and previous guides/dots/circles, are hash-identical. Native Direct3D pixels validate visible arrows (8,482 changed cyan pixels). All eight traversal legs pass with the actual Meta capsule settings. Fixed test-only horizontal range and duplicate-player-capsule issues; prior failed diagnostics preserved. Final 41 guide + 17 original vendor assertions pass (58 total, zero failures); both processes exit 0; zero C# or shader compile errors. Physical Quest verification remains manual. Report: docs/validation/vendor-path-guide-2026-10-18.md; manifest and native preview: docs/evidence/vendor-path-guide/. No commit, push, deployment or production-data write.


### SOCO procedure research audit (2026-10-18)

Compared current Unity scripts/assets with public PNP Operational Procedures 2021 section 6.1 and supplementary NIJ/NIST forensic guidance. Verified the official announcement of the 2025 Forensic Group AOM but did not retrieve its full text; current-unit review remains necessary for latest-manual conformance. Main findings: instant identity on print-card storage; generic paper-bag knife packaging; missing explicit swab drying/approved transport; no developed-print photo gate; PPE/clean-tool teaching gaps; partial custody/access documentation; photographic scale/coverage and closeout-versus-formal-release terminology. Blanket chalk requirements need local validation; older Philippine body-position marking guidance prevents a blanket conclusion that all chalk should be removed. Existing handover, documentation, evidence/source separation, body transport and print-interpretation limits are retained strengths.

Read-only research: no scripts, scenes, prefabs, gameplay, score weights, uploads, dashboard or production records changed; no tests run for documentation alone. Full report with code references and primary sources: docs/research/soco-procedure-audit-2026-10-18.md. Downloaded public manual/text are cached outside the project in the OS temporary research folder. No commit, push or deployment.


### Chronological step 1: required arrival gloves (2026-10-19)

User clarified chronological audit order and approved initial gloves before entry. Implemented the PPE part of step 1, not the lab-result recommendation. Existing medical/emergency-response facts remain unchanged. Wider order: arrival/PPE, scene security/access records, IOC survey/contamination, photography, marking/measurement, biological sampling, developed-print documentation, packaging, simulated lab return, custody, final survey/IOC handback. Later improvements are not implemented here.

Six-line FR briefing retains four case lines and adds PPE teaching. Outside-cordon clean-glove station uses A pickup, separate A wear, B return; range/modal/held-button safeguards. Gloves tint existing animated hands using runtime material copies, stay independent of tools/inventory, log PPEEquipped once locally, and reset on reload. Other PPE is explicitly assumed; later glove-change mechanics remain planned. No scoring/wire/schema/dashboard changes.

Briefing plus gloves gate head/capsule entry and IOC observations. SDK-aware boundary return prevents deferred teleport bypass and clears accumulated origin motion. IOC guide waits for gloves; cordon remains closed/visible. Saved rig/hotspot stand outside tape, with entry sign and approach bounds. Preservation audit: four changed existing records, 15 added sign records, no removals; only rig/hotspot local transforms changed. Runtime station faces arrival; no manual wiring.

Accepted: 340 PASS/0 FAIL, seven Unity processes exit0, zero C# compile errors. run6: PPE40, entry26, FR44, phase19, IOC guide35, NPC guide33. run5: SceneTestStart143, all12 presets. Earlier failures preserved: inside-tape start/near-tape bounds; batch SDK end-of-frame origin sync (isolated Editor-only preset adapter now invokes SDK LastUpdate); camera fixtures now bind/restore SDK eyes. No package-cache edits. Preview inspected. Final no Editor owner/pending request, Standalone/Android XR startup enabled. Quest acceptance pending. Report docs/validation/arrival-ppe-2026-10-18.md; manifest docs/evidence/arrival-ppe/summary.json. No build/install, live upload, commit, push or deployment. Next chronological planning topic: scene security/access registration.


### Scene security/access register implemented (2026-10-19)

Implemented the approved chronological step 2. Flow: seven-line FR briefing (including security explanation), gloves, deliberate A-review/A-confirm check-in, then entry. Clipboard is beside the FR outside the cordon. B, walking away, held opening A, missing prerequisites and unrelated modal UI cannot sign. Uses the frozen session owner identity, simulated role and purpose; no extra personal information. Entry and IOC observations require all three prerequisites. Cordon stays closed and visible.

Authorization is distinct from physical visits. Head/capsule perimeter observations use the union of the existing approach and protected footprint with margin/settling, so denied attempts, jitter and the yard/house join create no visits. Genuine exits/re-entries including vendor travel are recorded. One student identity spans IOC/SOCO; scripted arrival reposition and role handover are labelled simulation transitions, without invented exits or NPC history. End/unload labels an open visit Present at scenario end with no fabricated exit time.

Local atomic .access.json sidecar beside the session log; non-scored, no dashboard/transport/schema change. SceneLogSigned records check-in, five append-only access event types preserve old ordinals/SceneEntered load semantics. BeforeSessionEnd hook finalizes before logger freeze. Bounded shared review UI has plain-text long names and visit scrolling. Development presets prepare authorization only in the isolated sandbox. Narrative restart fixture now explicitly prepares gloves and authorization.

Accepted: 452 PASS / 0 FAIL in 11 fresh Unity processes, all exit0, zero C# compile errors or null/missing-reference exceptions in accepted logs. Focused run2: register40. Regression run3: PPE42, entry26, FR44, phase19, IOC guide35, NPC guide33, vendor17, presets143, narrative41, persistence12. Initial inactive-TMP-font failure and timeout preserved in run1; fixed with explicit font initialization. Existing SDK/MRUK teardown diagnostics remain. Native long-name/scroll preview inspected. Scene byte-identical to step-2 pre-change snapshot (SHA256 91d5c8c35425cdc32fec5ecb6bbc01ebfb07c368e227d62da5e10fdf12786005). XR startup Standalone/Android enabled; no pending runner requests.

Report: docs/validation/scene-security-2026-10-19.md. Manifest: docs/evidence/scene-security/summary.json. Plan: docs/plans/crime-scene-security-step-2.md. Physical Quest interaction/readability/boundary comfort pending. No build/install, live upload, commit, push or deployment. Next chronological topic is step 3: IOC survey/contamination; not implemented here.

### IOC survey/contamination step 3 planning (2026-10-19)

Continued the chronological audit after completed scene security. Inspected the actual IOC definition, interaction, guide ordering, dialogue and phase/processing guards, then wrote docs/plans/ioc-survey-contamination-step-3.md with proposed visual-survey/contamination teaching, three concrete dialogue revisions and implementation acceptance checks. Existing survey-first guide preference is distinguished from the currently flexible observation order; the victim-plus-two-items threshold remains proposed unchanged. Glove changes and clean collection tools remain later processing topics.

Documentation only; gameplay implementation is pending. No Unity tests were rerun and no claim is made that prior step-2 results validate step 3. No build/install, live upload, commit, push or deployment.

### IOC survey and contamination guidance implemented (2026-10-19)

Implemented the approved step-3 plan in exactly three existing dialogue entries: IOC visual-only guidance, survey contamination/glove-limit teaching and bottle leave-in-place guidance. Preserved all IDs, entry/observation/processing gates, victim-plus-two-item threshold, observation-order flexibility, scoring, uploads and scene geometry. Updated the stale foundation diagnostic's survey-text expectation; no new gameplay tests. Final opening/survey/bottle lengths are 23/29/29 words, with 14/18/14-second reading times.

Accepted 485 PASS / 0 FAIL across ten fresh Unity suite processes, all exit0: initial eight suites406, final catalog60 plus phase19 after shortening. Zero C# errors or null/missing-reference exceptions in accepted suite logs; existing SDK teardown diagnostics remain. Native Direct3D previews exposed initial tooltip overflow and upper-panel countdown overlap; earlier previews preserved, final copy shortened without changing fonts/UI geometry. All three final previews fit and were inspected; upper prompt3 lines, survey6, bottle5. Preview source retained in Tools/IOCSurveyPreview.cs; temporary Editor copy/meta removed. No Editor owner or pending runner request; XR settings unchanged. Scene SHA256 remains 91d5c8c35425cdc32fec5ecb6bbc01ebfb07c368e227d62da5e10fdf12786005.

Report docs/validation/ioc-survey-contamination-2026-10-19.md; manifest docs/evidence/ioc-survey/summary.json; plan docs/plans/ioc-survey-contamination-step-3.md. Physical Quest readability remains pending. No build/install, live upload, commit, push or deployment. Next chronological topic is photography; user prefers planning before implementation, and that next step is not implemented here.

### Camera photographic scale accessory implemented (2026-10-19)

Implemented the approved ruler as a camera accessory, outside the Tool Wheel/inventory. For knife, bloodstain and bottle: overall scene photos, original unscaled item photo, existing marking prerequisites, then equipped/raised camera and deliberate right A to place the assisted 30 cm ruler. Frame the entire item and printed ruler for the final shot; A removes it. Flat floor rulers and upright bottle ruler use authored metric offsets. Measurement tape remains separate. Body/developed-print workflows and the wider photo-coverage redesign remain unchanged.

Saved scaled images use 960x720 at default 4:3; other images retain 480x360. Checks use saved aspect, visible-mesh bounds, readable face/size, complete frame and clear ruler sightlines. Missing/wrong/clipped/occluded/reversed scales refuse without photo/state credit; visible scales cannot enter originals. A respects modal/raised/range/held-button guards. Holster, source departure/movement, disable and reload clear the accessory. Three append-only local events add no score/upload/dashboard changes; no new evidence state. Runtime attachment needs no scene wiring.

Accepted 485 PASS / 0 FAIL in nine fresh Unity processes, all exit0. Focused run6:61; shutter14, scene procedure47, guide26, measurement36 from run5; sampling59 from run6; chalk39, presets143, catalog60 from run7. Zero C#/shader errors or null/missing-reference exceptions in accepted logs; existing SDK teardown diagnostics remain. Actual photos and dark-panel prompts inspected. Earlier failures retained, including test input type/native shutdown failure, stale tent preview/old-house heights/sample punctuation/bottle order; fixture fixes preserve production procedure rules. Corrected bloodstain framing to full visible mesh and tested clipped wings.

Catalog audit: nine added IDs, all old entries unchanged. Scene SHA256 remains 91d5c8c35425cdc32fec5ecb6bbc01ebfb07c368e227d62da5e10fdf12786005. No Unity owner/pending runner requests; XR startup settings unchanged and enabled. Report docs/validation/photo-scale-accessory-2026-10-19.md; manifest docs/evidence/photo-scale/summary.json; approved plan docs/plans/photo-scale-accessory-step-4.md. Physical Quest input comfort, stereo readability and Android performance remain manual. No build/install, live upload, commit, push or deployment. User prefers planning before further feature work.

### Outdoor glove-change table planning (2026-10-19)

User requested a table outside the house with a glove box or similar, plus a waypoint, and explicitly requested planning first. Inspected the existing ArrivalPPEStation/SceneCordon, sampling paths and NPC/vendor navigation patterns. Proposed expanding the existing outside arrival stand into one clean supply table, with dispenser/pair, separate disposal, deliberate replacement presses, session-local clean/needs-change state and a waypoint with an authored doorway route.

Plan: docs/plans/glove-change-station-step-6.md. Proposed enforcement is fresh gloves before another biological sample, while allowing the current sample to be contained/sealed/stored. Preserve access authorization and real exit/re-entry visits; do not conflate glove changes with a future fresh-swab exercise. Documentation only, no gameplay/assets changed or Unity tests run. Implementation awaits user review; no commit, push, build or deployment.

### Outdoor glove-change supply table implemented (2026-10-19)

Implemented the user's approved plan in docs/plans/glove-change-station-step-6.md. Expanded the existing outside arrival station at (17.92, 0, -0.50) into one physical table with a labelled glove dispenser, clean pair and separate used-glove receptacle. Shared NotWorn/Clean/NeedsChange state; successful sampling marks gloves used without revoking worn-glove entry access. Finish containing/sealing/storing the current sample, then change before another biological sample. Separate A presses dispose, pick up and wear; B returns a pending pair. Range, free-hand, modal, held-button, unfinished-sample, exterior-position and observed-exit guards apply. Initial PPE is logged once; replacement logs use three append-only local non-scored event types.

Added location pin/off-screen destination and cyan route through the kitchen doorway, steps and controlled entrance. Initial waypoint waits for briefing; replacement waypoint waits for stored sample; both retire on fresh wear. Physical sign takes over at table range. UI/capture scopes hide navigation appropriately, and required gloves take priority over optional vendor arrows. Real exit/re-entry records and authorization remain intact. Fresh-swab pickup/disposal is still a separate future exercise; no collision-based contamination or wider fingerprint/packaging gates.

Accepted 523 PASS / 0 FAIL across nine fresh Unity suite processes, all exit0: focused51 run4; PPE42 and sampling60 run5; entry26, register40, ruler61, vendor40, presets143 run2; catalog60 run6. Final native table/waypoint images inspected. Nine Meta capsule traversal segments reach the table; separate A/B, sampling/storage, cancellation, boundary visits and reload verified. Earlier failed input/camera/catalog and boundary-fixture attempts retained. Sampling coroutine now waits for exterior boundary settling before using the real station. Catalog fixture suspends the station while using its tiny substitute catalog.

Zero C#/shader errors or null/missing-reference exceptions and zero new missing-dialogue warnings in accepted logs. Catalog retains the exact old narrative/photo-callback and deliberate missing-ID warning baseline; SDK teardown diagnostics remain. Nine existing files changed in the 772-file feature-start scope, other763 hash-identical. Catalog adds8 IDs, no old entries changed. Saved scene SHA256 remains 91d5c8c35425cdc32fec5ecb6bbc01ebfb07c368e227d62da5e10fdf12786005; original scene/prefab geometry, prior ruler code, scoring, transport and XR settings unchanged. No Unity owner/pending runner request; XR startup enabled. Report docs/validation/glove-change-station-2026-10-19.md; manifest docs/evidence/glove-change/summary.json. Physical Quest controller comfort, stereo legibility and traversal remain manual. No build/install, live upload, commit, push or deployment. User prefers planning first for further work.

### Fresh swab supply and used Tool Wheel state planned (2026-10-19)

User agreed to fresh-swab work and requested greying the Swab tool after use. Inspected SwabTool's automatic clean-tip reset, ToolWheelController's existence-only dimming, and coordinator/registry equip mutation order. Wrote docs/plans/fresh-swab-supply-step-6.md: fresh packaged swabs at the existing outside table, independent glove/swab requirements, one shared waypoint, grey exhausted slot with a reason, and loaded-kit recovery before full disable after storage.

Planning only; no gameplay/assets changed or Unity tests run. The plan preserves containing/sealing/storing the current sample, guards direct equip before held-item changes, and separates fresh replenishment from holstering or glove changes. Implementation awaits review. No build/install, upload, commit, push or deployment.

### Fresh swab supply and grey used slot implemented (2026-10-19)

Implemented docs/plans/fresh-swab-supply-step-6.md. Packaged swabs share the outside glove table: with clean gloves and empty hands, separate A presses pick up and open/load a pack; B, walking away and modal interruption return it without credit. Normal sessions start without a fresh swab. Both sampling paths consume it on success, preserve source attribution and retain the blood-bearing swab with its sample. Sealing/storage no longer restores a clean tip. Glove changes alone do not replenish it. Four append-only local supply events; no new tool role, score/upload/dashboard change.

Tool Wheel keeps the used Swab icon grey on hover without lift and shows the reason. Missing/spent selection refuses before changing held tools/evidence; registry/coordinator/direct tool/grab paths share availability. Grey blood-loaded kit remains recoverable for containment. Held containers retain sealing/B storage, with finish-current-sample guidance. A confirmed fresh pack restores normal equip/appearance. One shared destination covers gloves/swabs, prioritizes gloves and waits for stored samples. Initial swab navigation is requested by a missing-slot/sample attempt, preserving earlier photography; optional table pickup remains available during investigation. Existing doorway route and photo-hide scope unchanged. Isolated presets explicitly prepare a fresh swab; legacy supply fixtures use the real station.

Accepted 604 PASS / 0 FAIL across eleven fresh Unity suite processes, all exit0. Focused51 run4; glove52, sampling60, inventory29, PPE42, entry26, register40, ruler61, vendor40, presets143 and catalog60 run2. Actual Y/thumbstick/A/B, grey/fresh/loaded/contained wheel states, held-item atomicity, sample recovery, supplies and reload verified. Native views inspected. Generic sampling test uses a reversible runtime-only cloned definition, without changing the real bottle case. Run3 fixture failure retained: helper cleared tooltip before assertion; corrected capture-before-cleanup, with no gameplay bypass.

Zero C#/shader errors, null/missing-reference exceptions or new missing-dialogue warnings in accepted logs. Existing catalog baseline callbacks/negative tests and SDK teardown diagnostics remain. Feature baseline776: twelve existing files changed, other764 hash-identical; eight catalog IDs added with old entries unchanged. Scene SHA256 remains 91d5c8c35425cdc32fec5ecb6bbc01ebfb07c368e227d62da5e10fdf12786005. Saved scene/prefab geometry, evidence/measurement data, ruler/capture code, scoring/transport and XR settings unchanged. No Unity owner/pending runner requests; XR startup enabled. Report docs/validation/fresh-swab-supply-2026-10-19.md; manifest docs/evidence/fresh-swab/summary.json. Physical Quest legibility/controller comfort/table reach remain manual. No build/install, live upload, commit, push or deployment. User prefers planning first for further work.


### Protected sample preparation and circular drying implemented (2026-10-19)

User approved the drying/individual-packaging flow and requested a filling circle. Implemented a separate protected cabinet beside the outside supplies, parented to the supply station so SceneCordon final positioning is inherited. Both cabinet and standing approach are outside the security footprint. Existing waypoint/doorway route prioritizes preparation after containment; no new Tool Wheel slot. The existing container is a temporary carrier, not sealed storage.

A parks the same source-specific sample in the cabinet and displays the blood-bearing swab in its drying holder. A texture-free clockwise ring fills over five seconds, explicitly labelled "Drying time simulated; real drying takes longer." Range loss, modal UI and spoken guidance pause progress without losing source or custody; cabinet yields its UI token to dialogue so countdowns cannot deadlock. Separate A confirms the displayed source ID and retrieves an individual paper swab package. Trigger sealing/B storage refuse until prepared; gloves/fresh-swab supplies remain independent. Package wrapper/source persist on the inventory-owned visual through re-equipping. Three append-only local preparation events; eight appended catalog IDs, no previous catalog changes.

Accepted 528 PASS / 0 FAIL across nine isolated fresh Unity processes, each exit0: preparation46, glove53, fresh-swab52 in run7; biological63, inventory29, PPE42, register40, presets143 and catalog60 in run10. Actual XR A/full real-time ring/pixel visibility, nine Meta capsule route segments, standing boundary clearance, held-button protection, pause/resume (including actual spoken guidance finishing), two source identities, packaging/storage, supply independence and reload verified. Native station, wheel and paper-package renders inspected. Legacy synchronous fixtures use the real station with sandbox-only time advancement; glove fixture observes a real preparation boundary trip. Biological fixture drains queued teaching dialogue before B assertions and captures held packages above the offline controller floor. Earlier failed renderer/placement/boundary/fixture attempts and one invalid suite-list invocation retained.

Audit: 11 existing files changed of 486 baseline files; other475 hash-identical. Saved scene SHA256 remains 91d5c8c35425cdc32fec5ecb6bbc01ebfb07c368e227d62da5e10fdf12786005. Evidence/measurement definitions, saved geometry, scoring, transport and ruler/capture implementation unchanged. Legacy preview files restored byte-for-byte. Accepted logs have no C#/shader/null/missing-reference errors or new missing-dialogue warnings; exact prior catalog fixture baseline and SDK/license/teardown diagnostics remain. No Unity owner/pending request; XR startup enabled and scoped XR git status clean.

Report: docs/validation/sample-preparation-2026-10-19.md. Plan: docs/plans/sample-preparation-step-7.md. Manifest: docs/evidence/sample-preparation/summary.json. Procedure reference is NIJ Packaging & Storage (linked in report). Physical Quest reach, stereo legibility and performance remain manual. No build/install, upload, commit, push or deployment. User prefers plan first for subsequent features.


### Fingerprint comparison tutorial station implemented (2026-10-19)

User approved the unlimited-retry manual-comparison exercise and an outside desk near evidence submission. Added a runtime desk at (20.2,0,-1.2), monitor, labelled packaged-lift tray and training sign, spawned by the evidence box at Start independently of its nonuniform scale. Standing approach and monitor collision clearance validated. Shared GloveStationGuide gains a comparison destination and an exterior detour around supplies/drying; preparation and supplies retain priority.

Practice reads explicitly stored, sealed fingerprint lift cards from existing inventory. A opens with empty hands. Three neutral references, independent zoom/rotation, optional highlighted ridge ending/split, immediate explanatory wrong feedback, unlimited retries, no timer or score penalty, explicit specialist verification guidance on success. Stored samples can be switched and replayed; the next-sample control disables for a single sample. Illustrative native ridge diagrams (PracticeFingerprintGraphic), not real biometric data or a current PNP UI. Case findings, including bottle multi-identity results, remain unchanged. Tutorial completion/attempts are separate per source. No extra Tool Wheel slot, inventory transfer or unpackaging. XR A/B/stick and controller-ray trigger controls; guard held opening input; close safely on range/modal/disable/reload and release input/locomotion. Two append-only local events; completion scored=false. Seventeen catalog IDs appended without altering old bytes.

Accepted 396 PASS / 0 FAIL across nine fresh isolated Unity processes, all exit0: focused53 run4; fingerprint storage33, bottle40, drying46, glove53, inventory29, PPE42, register40, catalog60 run5. Actual XR inputs, 30 additional wrong guesses, two real dust/lift/stored sources, independent completion, case/custody/score preservation, sample switch/replay, closure/reset, twelve Meta capsule route segments, boundary/solid-geometry clearance, native text fit and an unobscured-print pixel check verified. Native station/hint/completion renders inspected. Earlier label/placement/bottle-measurement/pointer-guard attempts retained. FingerprintStorageTest success marker normalized from ALL PASS to PASS for the batch runner; assertions unchanged. Monitor sorting raised to the existing review-panel convention to remove overlay occlusion.

Audit: six changed existing files of491 baseline, other485 hash-identical. Scene SHA256 remains 91d5c8c35425cdc32fec5ecb6bbc01ebfb07c368e227d62da5e10fdf12786005. Evidence/measurement data, state/inventory managers, scoring, transport behavior, tool registry and ruler/capture code unchanged. Legacy test previews and bottle wire example restored byte-for-byte. Accepted logs have no C#/shader/null/missing-reference errors or new missing-dialogue warnings; exact prior catalog fixture baseline and SDK/license/teardown diagnostics remain. No Unity owner/pending request; XR startup enabled and scoped XR status clean.

Plan docs/plans/fingerprint-comparison-step-8.md; report docs/validation/fingerprint-comparison-2026-10-19.md; manifest docs/evidence/fingerprint-comparison/summary.json. Physical Quest reach/stereo readability/performance remain manual. No build/install, upload, commit, push or deployment. User prefers planning first for subsequent features.


### Adaptive dialogue layout implemented (2026-10-19)

User requested flexible, readable dialogue based on text. Shared DialoguePanelLayout is now an instance capturing the authored maximum width. Both central presenters use it: compact short messages, wrapped long lines, full preferred height, >=24 font with no auto-shrink, real padding, and shrink on replacement. The facing edge remains fixed. Actual authored background scale/offsets are normalized to the measured panel; countdown remains below text. Legacy maxPanelHeight stays serialized/hidden but no longer clips full text. No pagination/scrolling; very long external passages may make tall panels. Queue, timing, input/history semantics unchanged.

Accepted focused suites total 123 PASS / 0 FAIL, all exit0: AdaptiveDialogueLayoutTest26 run2 (341 catalog bounds plus real backgrounds/countdown and transitions), DialogueCatalogTest60 run1, DialogueHistoryTest24 run1, independent DialoguePlacementTest13 run5 (both real presenters, long/short/wall, visible safe slots and >=5cm gap). Native short/long preview inspected. Broader UIComfortInputTest run2/run3 aborted at an obsolete Evidence Notes component assumption; an exploratory prefix run4 reported36 PASS/18 FAIL. These broader failures remain unresolved and are NOT counted as passing or established baseline failures. Temporary old-test changes were removed byte-for-byte. New placement suite isolates current presenters from unsolicited guidance/input/modal fixtures.

Baseline367 files (365 existing C# scripts + scene/catalog): exactly four production files changed, other363 identical. Saved scene hash remains91d5c8c35425cdc32fec5ecb6bbc01ebfb07c368e227d62da5e10fdf12786005. Accepted logs have no C#/shader/null/missing-reference errors; exact known catalog warning baseline retained. XR flags all1, scoped XR clean, no live Unity owner/pending request. No build/install/upload/commit/deployment. Quest stereo readability/comfort/performance pending. Report docs/validation/adaptive-dialogue-2026-10-19.md; evidence docs/evidence/adaptive-dialogue/summary.json; runner Tools/RunAdaptiveDialogueChecks.ps1; audit Tools/Audit/adaptive-dialogue-results.py.


### House entrance cordon and front-yard staging implemented (2026-10-19)

User requested the cordon at the house entrance, with tables inside the fence instead of on the sidewalk. Saved scene now has tape at foot of steps z=2.7, short controlled entry strip2.7?3.0, protected house area from z3.0. The yard remains outside the inner access footprint. Supply anchor(18.2,.03,1.6), child drying table(19.75,.03,1.6), independent comparison anchor(21.35,.03,1.6), evidence box on left(14.4,.55,1.5). FR moved to(18.15,.03,2.4); player/hotspot begin at yard walkway z.65, preserving height offset and gaze toward FR/entry. SceneCordon serialized anchors control supply/comparison placement; fallback for legacy scenes retained. Guide ends in front-yard aisle z.75, no street detour; first seven house waypoints preserved.

Accepted345 PASS /0 FAIL across eight fresh processes, all exit0: staging32 run2; arrival29, PPE43, register40, glove53, fresh-swab52, drying46 run3; fingerprint50 run4. Actual station footprints inside fence, sixteen Meta capsule approach/return/house-exit segments, unauthorized entry correction, real briefing/PPE/check-in and visit logging, supply/drying/sample custody, fingerprint XR/ray controls/unlimited retries/case-score preservation/reload verified. Native yard/entry/supply/preparation/comparison previews inspected. Earlier authoring-floor, trigger-only box fixture, autoauthorized arrival fixture and old comparison-distance attempts retained; final accepted checks pass. ArrivalEntryTest excluded from automatic batch authorization, and fixtures now visit relocated desks. Only layout/range assumptions changed; no case assertion was removed.

Audit383 baseline files, ten existing files changed, other373 identical. Scene diff exactly six old blocks (rig, cordon component/root, FR, hotspot, box), plus four blocks for two new anchors, no removals; all other serialized house/evidence blocks unchanged. New intended accepted scene SHA256 e441ee08004b03baf5be2ce53598e8542c4318bbb45ff690cd29429d78cadd3d supersedes the earlier scene hash. Resource assets/catalog unchanged. Three runtime scripts changed: SceneCordon, GloveStationGuide, EvidenceBoxDepositZone (only desk-spawn placement). Four diagnostic fixtures and one batch-runner exclusion updated. New authoring script, focused staging suite/metas, runner/audit/plan/report/evidence.

Legacy PPE/register previews/example JSON archived under entrance-staging/run3/legacy-previews and restored byte-for-byte. Accepted logs have no C#/shader/null/missing-reference errors or missing dialogue IDs; existing SDK/licensing/MCP/teardown diagnostics remain. No live Unity owner/pending request, XR flags all1 and scoped XR clean. No build/install/upload/commit/deployment. Physical Quest reach/stereo comfort/performance pending. Report docs/validation/house-entrance-staging-2026-10-19.md; summary docs/evidence/entrance-staging/summary.json; plan docs/plans/house-entrance-staging.md; runner Tools/RunEntranceStagingChecks.ps1; audit Tools/Audit/entrance-staging-results.py. Authoring entrypoint HouseEntranceStagingSetup.Apply.

2026-10-20: Fixed entrance register identity dead end for explicitly unassigned direct scenario starts. Permission is scoped to the unlocked controller/session/scene; no student owner is invented. Runtime compilation: zero errors; 7 compiled-code permission checks passed. Added 5 SceneAccessRegisterTest assertions; Play/Quest interaction not run with user Editor open. See docs/validation/scene-entry-identity-2026-10-20.md. Saved scenario unchanged (e441ee08004b03baf5be2ce53598e8542c4318bbb45ff690cd29429d78cadd3d).

### Scene Test Start menu refreshed (2026-10-20)

Updated the Unity manual test menu to 16 grouped presets while preserving all existing enum values and saved selections. Added knife/bloodstain/bottle ruler-photo starts and fingerprint comparison practice, equipped the scene-route camera, included both fingerprint cards in completed-procedure fixtures, and refreshed instructions for drying, supplies and custody. SceneTestStartTest passed 207 checks with zero failures, including real ruler/shutter captures and preset restarts; the old 12-preset baseline passed 143 checks before edits. Guide: docs/DEVELOPMENT_TEST_STARTS.md; report: docs/validation/scene-test-start-refresh-2026-10-20.md. Editor is outside Play mode with the updated window open; saved scenario unchanged and Quest validation pending.

### Investigation UI refresh implemented (2026-10-20)

Implemented the approved UI review: current ruler instructions, separate containment/drying/paper-packaging/sealing/B-storage checklist stages, explicit bag storage, and meaningful still-camera context. Album photos now carry subject/stage/time metadata, successful route shots are saved with their view names, images retain their aspect, and the album provides labelled paging, empty/control states, 1x-4x zoom and bounded left-stick pan. Repaired and migrated UI fixtures to actual preparation/storage and current panel/display contracts.

Accepted 331 PASS / 0 FAIL across seven fresh Unity processes, all exit0: workflow24, ruler74, shutter15, map69, panel46, cabinet46 and broad comfort/input57. Native album and camera previews inspected; initial caption clipping corrected. Failed fixture/import attempts remain in evidence. Scene SHA256 unchanged e441ee08004b03baf5be2ce53598e8542c4318bbb45ff690cd29429d78cadd3d; no evidence definitions, grading or upload changes. Report docs/validation/ui-refresh-2026-10-20.md; controls docs/INVESTIGATION_UI_CONTROLS.md; evidence docs/evidence/ui-refresh-2026-10-20/summary.json; runner Tools/RunUIRefreshChecks.ps1. Physical Quest readability/comfort/performance remain manual. No build/install, upload, commit, push or deployment.

Camera frame correction (2026-10-20): restored original CameraFrameOverlay image in ViewfinderFrameMask at user request; removed replacement native corners/header. Original mask sizing and green confirmation tint retained.

Logging table (2026-10-20): replaced runtime clipboard pedestal/upright sign in SceneAccessRegisterUI with a small four-legged table, ruled paper register and blue pen. Instructions lie on the paper; existing check-in/review inputs and persistence preserved. SceneAccessRegisterTest: 45 passed / 0 failed. Native preview: docs/evidence/logging-table-2026-10-20/table-preview.png. No scene asset edits.

Swab label correction (2026-10-20): PaperSwabPackage and SamplePreparationStation now render source via EvidenceDisplay.Label, removing EVD-014/EVD-015 from visible source labels. Existing label assertions migrated to readable labels plus no-EVD checks. 109 PASS / 0 FAIL across biological/preparation suites. Evidence: docs/evidence/swab-label-fix-2026-10-20/.

Dashboard workflow alignment (2026-10-20): mirrored completed procedure records added to ProcedureTimeline_1.json, release authorization/completion copy corrected. New ProcedureTimelineUploadTest: 14 PASS. Dashboard typecheck passes, 75 dashboard tests and 21 browser checks pass (110 combined checks). Full details: docs/validation/dashboard-procedure-upload-2026-10-20.md. Fixed tent mapping retained; no grading or scene asset change. No deployment.

Evidence holder table (2026-10-20): saved table beneath entrance-side EvidenceHoldingCrate, raised holder center Y to .864 (base/tabletop .74), broadened horizontal custody trigger for clear standing access. Added wood/metal material assets. CustodyPassTest 50 PASS / 0 FAIL. Scene hash intentionally changed to 7EDEE789FE6AEDBF8B724AC0B42ADC43D082FBCC66F147397633EE4CB9C7FB0F. See docs/validation/evidence-holder-table-2026-10-20.md.

Evidence-box waypoint (2026-10-20): runtime destination pin/distance with off-screen arrow added via EvidenceBoxWaypoint. Active only when all evidence is ready for the custody pass; hides near box for A prompt, behind UI/dialogue, during captures and after transfer. Independent canvas avoids crate scale. CustodyPassTest 62 PASS / 0 FAIL. See docs/validation/evidence-box-waypoint-2026-10-20.md and native preview. No scene/grading changes.

Cordon layout correction (2026-10-20): extended tape at Z=2.7 to side fences X=8/25, preserving central entry X=16.2-17.6 and exterior staging. Security bounds now follow the enclosed property through rear Z=19.5. Persistent CordonFenceLayoutSetup Editor menu discovers fence colliders and saves changes with backup/Undo. Accepted ArrivalEntryTest 46 + SceneAccessRegisterTest 48 = 94 PASS / 0 FAIL; initial gaze failure retained alongside passing repeat (17.8 degrees, unchanged arrival transforms/threshold). Native previews inspected; scene clean outside Play mode. Scene SHA256 now 0EE4453F0761012F9948E36F4B211C48C441ABDAD1542AE1639E29C61BDD1FAE. Report docs/validation/cordon-layout-fix-2026-10-20.md. Headset traversal remains manual.

House inner / yard outer cordons (2026-10-20): SUPERSEDES earlier cordon layouts. Latest user instruction treats the entire house as hot zone and yard as warm zone. Inner tape/sign is at front door, side door has seals, bounds cover both storeys and projecting front wing; separate outer tape/sign joins front fence ends at property gate Z=.15. Front-yard register/supply/preparation/comparison/evidence-box tables stay outside inner bounds. Added registerStationAnchor and additionalProtectedAreas to SceneCordon; removed kitchen-only tape. Persistent authoring menu: Tools > ViCSI > Scene Setup > Place House Inner And Yard Outer Cordons (existing CordonFenceLayoutSetup class). Accepted ArrivalEntryTest 42 + SceneAccessRegisterTest 48 = 90 PASS / 0 FAIL. Native previews inspected; editor idle and scene clean. Scene SHA256 F3D1753960C82B0B74FD5B431266A19F79A4D4CC1C253D84B686EEA5644CB6A6. Report docs/validation/house-inner-yard-outer-cordons-2026-10-20.md. Headset walkthrough remains manual.

Police tape lettering (2026-10-20): saved bold black POLICE LINE DO NOT CROSS on yellow across all 10 current tape strips, including inner entrance, side door and outer cordon. Both broad faces read correctly; long spans repeat the phrase. Assets: Assets/_Project/Art/Textures/PoliceTape. Generator Tools/GeneratePoliceTapeTexture.ps1; persistent PoliceCordonLetteringSetup editor menu is also called by CordonFenceLayoutSetup. Four native renders inspected; before/after Transform, BoxCollider, CapsuleCollider and MonoBehaviour records identical. No behavioral suites rerun for this visual edit. Saved scene SHA256 F595FA0F19DAC9FBDD695C2393C7A5CB5D717A9C5C845508CF3E21181C2368B0. Subsequent editor dirty state preserved during previews. Report docs/validation/police-line-lettering-2026-10-20.md.

2026-10-20: Materialized runtime-created props, panels and guidance into CSI_Environment: 627 bound scene GameObjects across 36 groups. Added Tools > ViCSI > Editable Scene Objects browser, repeatable Make Runtime Visuals Editable setup, persistent visual assets, editable camera ruler and paper-swab package prefab. Runtime builders reconnect callbacks while retaining authored visual overrides. Supply route and worn-glove tint are Inspector settings. Original camera frame and current cordon layout preserved. Accepted 510 PASS / 0 FAIL across ten fresh Unity processes (all exit 0); code whitespace checks pass. Native edit-mode previews inspected. Unity reopened outside Play mode with browser available; dirty editor scene state preserved. Initial failed migrations and import/reload crash evidence retained; saved scene and backup preserved. See docs/EDITABLE_SCENE_OBJECTS.md, docs/validation/editable-scene-visuals-2026-10-20.md and docs/evidence/editable-scene-visuals-2026-10-20/summary.json. Saved scene SHA256 A5850BBA3A16AF8086C46A1B9BB51FD638113938032371B86756ECE553FC3B7C. Physical headset checks remain manual. No commit, push or deployment.
2026-10-21: Fixed tool-wheel appearance regression caused by editable-visual asset persistence. Unnamed sprite/texture shared an output path and overwrote the texture; extensionless sprite assets also reimported as DefaultAssets. Restored three wheel masks and persistent textures with original GUID/fileID references, retaining authored layout and saved scene hash. PersistAssets now saves dependencies before choosing the unique parent path and supplies type names for unnamed objects. Added persistent ToolWheelVisualRepair editor repair/native-preview check. Five checks pass including texture persistence after forced reimport and alpha masks; isolated native preview inspected. Original editor dirty state preserved. Earlier 510 gameplay checks missed these missing masks; see docs/validation/tool-wheel-textures-2026-10-21.md. No physical headset test, commit or push.
2026-10-21: Fingerprint case identity/conclusions now require a correct comparison at the desk. Removed automatic MarkMatched from sample storage and legacy station processing. Lift/storage leave SentToLab with no identity; wrong references cannot unlock the knife identity finding or Paul-handled-knife conclusion; correct confirmation triggers identification once. Removed practice/training/illustrative/simulated-lab player wording, updated saved labels and comparison preset to pending identities. Legacy internal event/dialogue identifiers retained for compatibility. Accepted 308 PASS / 0 FAIL (comparison 58, bottle 42, scene presets 208); native desk previews inspected. Initial failed diagnostics retained; label fit fixed and a missing SDK wall comfort-mask child/reference restored from its existing prefab to repair scene reload. See docs/validation/fingerprint-comparison-gate-2026-10-21.md and evidence summary.json. Saved scene SHA256 59BA4926A00A44E044DF7BA960F4D820096226FEA982B3972D3C5194C35C2C6C. Editor outside Play mode, scene clean, no queued test. No physical headset test, external upload, commit or push.
2026-10-21: Updated blood drying/preparation wording to casework language per user. Station title BLOOD EVIDENCE DRYING, active air-drying instructions, source verification and individual paper packaging prompts; removed simulated/practice and generic sample wording in this flow. Paper wrapper now BLOOD EVIDENCE. Existing drying timing, source/custody and packaging/seal/storage gates retained. Saved labels updated through persistent BloodEvidenceCaseworkSetup helper; editable prefab/runtime labels updated. SamplePreparationTest 47 PASS / 0 FAIL; native drying/packaging renders inspected, text-height check passes. Existing foliage partly overlaps sign in preview; layout retained. See docs/validation/blood-drying-wording-2026-10-21.md and evidence summary.json. No physical headset test, commit, push or external upload.
2026-10-21: Recorder Documentation Accuracy implemented for BODY-A/EVD-014/EVD-015/EVD-018 only. Versioned six-choice catalog, exactly three selections, three supported at 2 points and three unsupported at 0, separate 6/subject and 24 total. Confirmation freezes selections; no active-investigation correctness/score feedback. User approved existing body-wound/knife-consistency/spatter findings at recording completion independently of chosen answers. Deduction stays 60/25/15. Single ReportLinesSelected event retains all six options/selection flags/text/weights/points in minimal uploads; dashboard validates snapshots and shared student/professor section shows separate Recorder subtotals/total. Historical events/table-only report rows remain readable and unscored. Old Greybox full-case fingerprint fixture now uses actual comparison desk instead of expecting storage to identify the lift. Accepted Unity suites: 338 checks / 0 failed; dashboard 83/0, scoring 219/0, responsive browser 21/0; TypeScript/scoped lint pass; zero C# compiler errors. See docs/validation/recorder-documentation-accuracy-2026-10-21.md (audit, all 24 exact choices, weights, implementation, compatibility, exclusions, results) and docs/evidence/recorder-documentation-2026-10-21/summary.json. Earlier failed attempts retained. No physical headset playthrough, production read/write, commit, push or deployment. Existing dirty working trees preserved.
2026-10-21: Added Walking Speed to existing utility-wheel Settings, matching height minus/plus/reset controls. Headset-local preference, 10% steps, 50-200%, default/reset 100%; persisted across scenario restart, Main lobby and another session. PlayerWalkingSpeed captures authored walking/running/crouching factors; PhotographTool composes aim slowdown with the chosen multiplier and restores current preference when lowered. Saved editable CSI_Environment controls/component; targeted authoring menu Tools > ViCSI > Scene Setup > Refresh Walking Speed Settings preserves existing controls. Explicit row ordering and live text/hint refresh prevent saved-template ordering/stale-label issues. SettingsPauseMenuTest final run3: 49 PASS / 0 FAIL; Unity zero C# compiler errors; native 130% preview inspected. Earlier failed attempts retained. Report docs/validation/walking-speed-settings-2026-10-21.md; evidence docs/evidence/walking-speed-settings-2026-10-21/summary.json. Test restores original user preferences; no physical headset test, production write, commit, push or deployment. Existing unrelated changes preserved.
2026-10-22: Downloaded and wired 22 no-attribution interaction SFX per user; explicitly excluded zipper, doors and cabinets. Kenney CC0 interface/impact/9 surface-step variants, 3 Freesound CC0 publicly offered HQ previews (original login downloads not used), Mixkit original WAV paper/writing/tape/switch/closure recordings. Mono trimmed/faded PCM assets in Assets/_Project/Audio/InteractionSfx; editable Resources/InteractionSfxLibrary.asset, 8 bounded spatial voices and 2D UI source saved in CSI_Environment. Existing tool source/clip fields assigned. Neutral UI A/trigger/B Recorder/tool+utility selection; writing/register/sketch, album page, tape measuring/lift/paper seal, packaging, object/tent/body-letter placement and evidence-box custody sounds; distance-based footsteps reset for menus/height/teleports and retain camera creep. Chalk audio now obeys actual guide tolerance and stops for menus/holster. No scoring/rule changes. Stale ChalkProcedureTest supply fixture now visits the real station; FingerprintStorageTest uses real comparison desk before expecting identity/RC-03. Accepted 280 PASS / 0 FAIL across SFX39, Recorder30, measuring41, chalk39, fingerprint35, preparation47, settings49. Compile passed; source whitespace and all 22 original/converted hashes verified. License/item snapshots, originals and manifest: docs/licenses/interaction-sfx-2026-10-22. Report docs/validation/interaction-sfx-2026-10-22.md; evidence summary.json. Physical headset listening remains outstanding; no production writes, commit, push or deploy. User requested save; all files and scene saved locally.
## 2026-10-22 - House entrance outdoor-photography barrier
- Saved editable invisible front-door wall in CSI_Environment; HousePhotographyEntryGate controls its collider.
- No new IOC obstruction. Activates at SOCO investigation until all required Exterior AND Entrance route points complete, then opens before interior photos.
- SceneCordon.CanEnter includes this gate, retaining existing room-scale/teleport return safeguards and an outdoor-photography reminder.
- HousePhotographyEntryGateSetup editor menu installs/repositions the saved wall. No runtime-only geometry.
- 151 checks passed: HousePhotographyEntryGateTest 27, IOCAssessmentGuideTest 35, ArrivalEntryTest 42, ScenePhotographyProcedureTest 47. Zero C# compile errors; no headset playtest.
- Report: docs/validation/house-photography-entry-2026-10-22.md. Evidence: docs/evidence/house-photography-entry-2026-10-22/run1.
- No commits, push, deployment or production data changes.
## 2026-10-22 - False wall-penetration view mask
- Reproduced old SDK false wall detection for triggers, default-layer world UI and its 22 cm ahead-of-head ray extension.
- Saved WallPenetrationViewGuard on the existing wall-effect object; original broad SDK driver disabled. Real-wall SDK effect/curve retained; smooth-movement tunneling unchanged.
- Filter excludes triggers, Canvas/RectTransform UI, player hierarchy, Ignore Raycast/UI layers and HousePhotographyEntryGate; tests only the actual logical-to-tracked head segment and selects nearest surface.
- Mask renderer starts hidden and clears explicitly on startup, disable and no obstruction. Editor menu: Tools/ViCSI/Scene Setup/Fix Wall Penetration View Filter.
- Restored unrelated UI RectTransform serialization changes against the pre-task snapshot.
- 96 checks passed: WallPenetrationViewTest 20, HousePhotographyEntryGateTest 27, SettingsPauseMenuTest 49. Compilation succeeded after correcting a missing namespace import; no headset playtest.
- Report: docs/validation/wall-penetration-view-2026-10-22.md. Evidence: docs/evidence/wall-penetration-view-2026-10-22/run1.
- No commits/push/deploy/production-data changes.
## 2026-10-22 - Keep evidence points until Recorder confirmation
- EvidenceDiscoveryDot no longer hides at Found. It remains through documentation/processing until recorderDocumentation exists or the record reaches Logged or later via the authoritative sequence.
- Existing capture hiding, role/photo prerequisites, physical visibility and depth occlusion retained; no scoring or evidence progression changes.
- Extended EvidenceDiscoveryDotTest to exercise all four real Recorder confirmations, partial selection, cancellation, other subjects staying visible and legacy Logged/later states.
- Updated an obsolete photo-capture fixture to use public CaptureScenePhoto rather than an evidence callback on a listener without a PhotographTool. Initial timeout report retained; accepted rerun passes.
- 125 accepted checks: EvidenceDiscoveryDotTest run2 95, RecorderReportLimitTest run1 30. Unity compilation succeeded; no headset playtest.
- Report: docs/validation/evidence-dots-until-recorded-2026-10-22.md. Evidence: docs/evidence/evidence-dots-until-recorded-2026-10-22/.
- No commits/push/deployment/production changes.
## 2026-10-22 - Implemented CC0 scene ambience
- Saved Scene ambience - CC0 house and yard in CSI_Environment with SceneAmbience, three stereo AudioSources and two exterior low-pass filters. All editable in Inspector; install menu Tools/ViCSI/Scene Setup/Install CC0 Scene Ambience.
- Approved recordings: mzui/Freesound135074 residential birds, visionear/Freesound565535 room tone, Joseph SARDIN/BigSoundBank0904 tree rustle. Stereo loops approx 73.33s / 20.31s / 58s. Public MP3 acquisitions retained; derived WAVs are not original lossless recordings.
- Existing house footprint controls smooth indoor/outdoor mix; indoor exterior leakage/muffling and dialogue/menu ducking. No new colliders, UI, procedure/scoring events, lobby bootstrap or persistent voices.
- Clips stream as Vorbis; scene-owned controller stops/restarts its three sources on disable/enable. Existing interaction audio unchanged.
- Licenses/originals/hashes/edit manifest: docs/licenses/ambient-audio-2026-10-22/. Import helper: Tools/ImportAmbientAudio.py; temporary Python deps outside Unity Temp.
- 134 checks passed: SceneAmbienceTest 28, InteractionSfxTest 39, SettingsPauseMenuTest 49, media/provenance checks 18. Final compile zero C# errors. Physical headset listening/loudness balancing remains outstanding.
- Restored 9 unrelated editor-generated UI RectTransform blocks against pre-task disk snapshot; final scene diff is only ambience additions.
- Report docs/validation/ambient-audio-2026-10-22.md; evidence docs/evidence/ambient-audio-2026-10-22/summary.json.
- No commits, push, deploy or production-data changes.
## 2026-10-22 - In-game ambience and SFX volume controls
- Existing Settings now has Ambience Volume and SFX Volume +/- rows (10% steps, 0-100%, 0% mute) plus Reset Sound Volumes. Existing responsive scrolling/input preserved; starts at first control.
- GameAudioSettings saves headset-local preferences, applies separate exposed bus gains immediately and restores them on scene loads. GameAudioMixer.mixer contains Ambience and SFX groups; default 100% preserves authored source mix and ambience ducking.
- Saved routes for current sources, plus runtime routing for shared UI/spatial SFX, feedback sources and ambience. No repeated multiplication of source gains.
- AudioSettingsSetup installs native mixer + editable rows, recovers partial template migrations, and preserves original navigation controls. Final BuildSettings group has 48 populated objects, no null slots or duplicated navigation templates.
- 138 accepted checks: AudioSettingsTest run2 22, SettingsPauseMenuTest run1 49, SceneAmbienceTest run1 28, InteractionSfxTest run1 39. Final compile succeeded; rendered settings-preview.png reviewed. Headset loudness testing remains outstanding.
- Initial editor/template recovery and untracked-camera pointer-test failures retained; accepted pointer rerun uses existing wall-clear tracked-pose fixture without bypassing input/placement gates.
- Restored 9 unrelated layout blocks outside Settings from pre-task disk snapshot.
- Report docs/validation/audio-settings-2026-10-22.md; evidence docs/evidence/audio-settings-2026-10-22/summary.json.
- No commit/push/deploy/production changes.
## 2026-10-22 IOC to SOCO comfort fade
Replaced direct rig reposition with 0.5s fade-out, fully opaque rendered frame, synchronized SDK capsule/origin teleport to authored grounded entrance, 0.4s settle, 0.5s fade-in. Head tracking retained. Temporary UI/tool/locomotion ownership and canvas suspension; disable restores everything. Saved editable camera fade quad/material/stereo overlay shader and independent arrival floor anchor. SOCOWorkflowInteraction Inspector exposes timings/references. Form 2, briefing, access simulation transition and scoring preserved. 148 accepted checks passed (Narrative47, Foundation19, Access49, NPC33), zero C# or shader errors since import. Updated legacy tests for grounded capsule and current outdoor photo prerequisites. Report docs/validation/soco-arrival-fade-2026-10-22.md; evidence summary.json in matching docs/evidence folder. Restored 9 unrelated auto-layout RectTransforms after verification; reloaded saved scene. Headset comfort review pending. No commits/push/deploy/production writes.

## 2026-10-22 Procedure navigation and mandatory follow-up (all seven requested steps complete)
Audit/report: docs/validation/procedure-guidance-2026-10-22.md; evidence/accepted 14-suite summary: docs/evidence/procedure-guidance-2026-10-22/summary.json. 821 accepted checks passed, zero C# errors. All 16 Scene Test Start presets passed 208 checks. No commits/push/deploy/production writes. Headset review remains manual.
New ProcedureDestinationGuide is saved as an editable scene root/canvas/material; handles entry register, nearest unfinished body/evidence, matched-card storage cue and final survey; existing NPC/photo/glove/preparation/comparison/vendor/box guides cover other destinations. 5 Hz resolver, view follows every frame, capture/modal suppression. ProcedureGuidanceSetup polls Temp/setup-procedure-guidance.flag and saves guide plus current comparison monitor labels. Its catalogue cache can be stale before Play; reapplying after a fresh Play reload saves the new Idle wording correctly (verified file contains Keep the recovered card in hand).
ProcedureInterlock blocks unrelated forensic/tools/measurement/handling/interview/deduction work after blood use until current blood evidence is secured and gloves disposed/replaced. It owns no modal or locomotion locks. Disposing alone remains gated. ToolInputLock.IsLockedFor(role) allows Swab.Update to seal a held container even when registry CurrentRole=None; B storage also explicitly allows the current blood sample. Used-glove paths/prompt/cancellation tested with actual XR input.
Fingerprint card flow is lift -> carry held card to comparison desk -> confirm match -> close UI -> B store. Comparison opens with held card; opening records completed fingerprint recovery, identity remains pending until correct reference. Wrong/cancelled comparison cannot store or bypass; matched card must be stored before unrelated procedures. Legacy already-stored unmatched cards still examinable; completed cards excluded from available/action UI. Inventory.PendingFingerprintItemId avoids sorting allocations. Fresh-swab-request guidance is suppressed while a fingerprint card is pending, preserving mandatory comparison priority. Comparison preset now seeds a held unmatched knife card; other card is complete.
Evidence box is ready in SOCOInvestigation once all physical procedures/storage are complete, with exact missing-step feedback. Early custody does not advance conclusion/final survey or change 60/25/15. Stations (gloves/swab supply/drying/comparison) remain usable during SOCOCloseout. Drying action canvas is gated by eligible sample/range/context; A unavailable while timer runs. Cache the Canvas component, not Graphic.canvas (null when disabled). Fingerprint idle/action UI is similarly eligibility/range gated. Updated empty-hands and pre-match storage wording in catalogue and saved monitor.
Extended BiologicalSamplingTest completes actual documentation, both blood samples, both held-card comparisons, body removal and six-item box transfer: 69 passes. Other final coverage: Guide18, Map75, FPComparison61, BodyRemoval49, Glove59, Prep51, FPStorage41, Bottle43, EarlyBox9, Custody62, UIWorkflow25, Closeout51, Presets208. Initial failed reports retained (outdated fixture assumptions and initial Canvas visibility null). Guide observer must be enabled to become Camera.main. BodyRemovalTest waits for full black AND authoritative removal event, rather than one guessed coroutine frame. Relevant Greybox/map/biological/UI/preset fixtures updated for new required order.
Scene cleanup restored nine unrelated auto-layout RectTransforms from pre-task disk snapshot. Only intentional existing scene changes are SceneRoots plus comparison Idle text, and 18 new guide blocks. Reloaded saved scene afterward to resolve Unity external modification modal. No test/setup request left pending.
Final editor housekeeping: after saving the new Idle wording, used Hierarchy scene context > Discard changes > OK to reload the verified on-disk scene and discard only unsaved automatic layout churn. The ordinary Ctrl+R shortcut did not reload an already-open dirty scene with unchanged disk hash. Saved task content was verified before discarding.
