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
