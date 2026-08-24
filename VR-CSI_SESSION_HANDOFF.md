# VR-CSI Unity Setup — Session Handoff (continue in new window)

**Purpose:** This session got the Unity project itself into a verified-good state and fixed the Claude Code ↔ Unity MCP connection, but got stuck on a process-restart quirk that only a brand-new conversation can clear. Paste this whole document as your first message in the new session so it has full context without re-deriving anything.

---

## 0. What this project is (background, unchanged from thesis doc)

Rolando's thesis at Partido State University – Lagonoy Campus: a VR-based Crime Scene Investigation training simulator (Quest 3S, Unity) + web instructor dashboard, built around the CAMIL framework. Actual 12-week build is scoped down to **1 scenario** (indoor kitchen homicide), **2 playable roles** (Photographer, IOC/Case Analyst), **5 non-played roles simulated via STCS** (scripted comms, no AI/NPCs). Full 7-role/multiplayer is future work — do not re-expand scope.

**Important correction from an earlier handoff doc reviewed this project:** that doc claimed a Next.js + Supabase dashboard, `docs/DEV_GUIDE.md`, and a `vr-csi-simulator/` monorepo structure were "done." **None of that exists anywhere on this machine** — verified by exhaustive filesystem search. Treat any claim about the dashboard/Supabase as unverified/aspirational unless it's re-confirmed from an actual repo. The Unity-side claims in that doc were largely accurate and have now been independently re-verified in this session.

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
