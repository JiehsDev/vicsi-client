// Assets/_Project/Scripts/Testing/EvidenceInventoryTest.cs
using System;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>
/// Isolated regressions for the evidence inventory / held-item / deposit feature - see
/// EvidenceCustodyLocation, EvidenceInventory, HeldItemCoordinator, EvidenceBoxDepositZone.
/// Same isolated-rig discipline as EvidenceBoundaryTest: builds its own
/// EvidenceStateManager/EvidenceInventory/HeldItemCoordinator on a deactivated rig
/// GameObject, runs entirely against synthetic evidence ids, and restores every static
/// Instance/PlayerToolRegistry entry it touched in a finally block. Run in Play mode;
/// never advances real scene evidence.
/// </summary>
public class EvidenceInventoryTest : MonoBehaviour
{
    /// <summary>Minimal concrete PlayerTool for exercising the real ToggleEquipTool/HolsterCurrent path - no gameplay behavior of its own beyond a ToolRole.</summary>
    private class FakeTool : PlayerTool
    {
        public ToolType role = ToolType.Photographer;
        public override ToolType ToolRole => role;
    }

    public string RunAllTests()
    {
        var report = new StringBuilder("=== EvidenceInventoryTest ===\n");
        int passed = 0, failed = 0;
        Action<string, Func<bool>> check = (name, test) =>
        {
            try { if (test()) { passed++; report.AppendLine("PASS: " + name); }
                else { failed++; report.AppendLine("FAIL: " + name); } }
            catch (Exception e) { failed++; report.AppendLine("FAIL: " + name + " (" + e.GetType().Name + ": " + e.Message + ")"); }
        };

        var previousStateManager = EvidenceStateManager.Instance;
        var previousInventory = EvidenceInventory.Instance;
        var previousCoordinator = HeldItemCoordinator.Instance;

        // PlayerUIGate is also process-wide static state, shared with whatever the
        // REAL scene's own UI currently holds when this runs inside a real Play Mode
        // session (e.g. a HypothesisCheckpointUI popup that opened on its own at scene
        // start - a real, unrelated gameplay lock, not a bug this test should report).
        // Saved and temporarily cleared here so this test's own equip/deposit checks
        // aren't blocked by scene state that has nothing to do with the feature under
        // test, then fully restored in the finally block below - this test's own
        // explicit PlayerUIGate-blocks-interaction checks near the end use their own
        // separate token and are unaffected.
        var blockersField = typeof(PlayerUIGate).GetField("blockers", BindingFlags.NonPublic | BindingFlags.Static);
        var blockersSet = (System.Collections.Generic.HashSet<object>)blockersField.GetValue(null);
        var savedBlockers = new System.Collections.Generic.HashSet<object>(blockersSet);
        blockersSet.Clear();

        var rig = new GameObject("EvidenceInventoryTestRig");
        rig.SetActive(false);
        var manager = rig.AddComponent<EvidenceStateManager>();
        var inventory = rig.AddComponent<EvidenceInventory>();
        var coordinator = rig.AddComponent<HeldItemCoordinator>();

        var assets = new System.Collections.Generic.List<EvidenceDefinition>();
        var sceneProps = new System.Collections.Generic.List<EvidenceProp>();
        FakeTool tool = null;
        PlayerTool evictedRealTool = null;
        bool evictedPhotographerSlot = false;
        var dummyAnchor = new GameObject("DummyHandAnchor").transform;

        var placeholderHeld = new GameObject("PlaceholderHeldVisual_TEST");
        placeholderHeld.SetActive(false);

        try
        {
            // --- Fixture: two sealed-eligible evidence definitions, one with a held
            // prefab configured, one deliberately without (for the missing-prefab
            // fail-safe check). ---
            EvidenceDefinition MakeDef(string id, bool withHeldPrefab)
            {
                var def = ScriptableObject.CreateInstance<EvidenceDefinition>();
                def.evidenceId = id;
                def.displayName = id;
                if (withHeldPrefab)
                {
                    def.heldPrefab = placeholderHeld;
                }
                return def;
            }

            var defA = MakeDef("test-inv-A", withHeldPrefab: true);
            var defB = MakeDef("test-inv-B", withHeldPrefab: true);
            var defNoPrefab = MakeDef("test-inv-noprefab", withHeldPrefab: false);
            assets.Add(defA);
            assets.Add(defB);
            assets.Add(defNoPrefab);

            typeof(EvidenceStateManager).GetField("sceneEvidenceDefinitions", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(manager, assets);

            rig.SetActive(true);

            // A loose scene EvidenceProp for defA, so we can verify it only hides
            // once inventory admission genuinely succeeds - not before, not on failure.
            // Built INACTIVE first: EvidenceProp.OnEnable() (which does the registry
            // add) fires the instant AddComponent runs on an active GameObject, before
            // evidenceId would ever be assigned - registering with a blank id, which
            // silently never matches EvidenceProp.FindById later. Set the id while
            // inactive, then activate, so OnEnable() sees the real id.
            var propGO = new GameObject("SceneProp_test-inv-A");
            propGO.SetActive(false);
            var prop = propGO.AddComponent<EvidenceProp>();
            prop.evidenceId = "test-inv-A";
            propGO.SetActive(true);
            sceneProps.Add(prop);

            void Advance(string id)
            {
                manager.MarkFound(id, ToolType.None);
                manager.MarkPhotographedRaw(id, ToolType.None);
                manager.MarkChalkMarked(id, ToolType.None);
                manager.MarkTented(id, ToolType.None, id == "test-inv-A" ? 1 : id == "test-inv-B" ? 2 : 3);
                manager.MarkPhotographedFinal(id, ToolType.None);
                manager.MarkLogged(id, ToolType.None);
                manager.MarkCollected(id, ToolType.None);
            }

            Advance("test-inv-A");
            Advance("test-inv-B");
            Advance("test-inv-noprefab");

            check("pre-seal: item not yet Sealed cannot be admitted", () => !inventory.Add("test-inv-A"));

            manager.MarkSealed("test-inv-A", ToolType.None);
            manager.MarkSealed("test-inv-B", ToolType.None);
            manager.MarkSealed("test-inv-noprefab", ToolType.None);

            check("scene prop still visible before admission", () => prop.gameObject.activeSelf);

            check("Add admits a sealed item to inventory", () => inventory.Add("test-inv-A")
                && manager.GetRecord("test-inv-A").custodyLocation == EvidenceCustodyLocation.Inventory);

            check("scene representation hidden only after successful admission", () => !prop.gameObject.activeSelf);

            check("duplicate Add is rejected", () => !inventory.Add("test-inv-A"));

            check("Add for a second, distinct item succeeds independently", () => inventory.Add("test-inv-B"));
            check("Add for the missing-heldPrefab item still succeeds (admission and equip are separate concerns)", () => inventory.Add("test-inv-noprefab"));

            check("missing evidenceId Add fails safely, no exception", () => !inventory.Add(null) && !inventory.Add(""));
            check("unregistered evidenceId Add fails safely", () => !inventory.Add("not-a-real-id"));

            // --- Equip / unequip ---
            check("EquipHeld with no heldPrefab fails safely (never falls back to raw model)", () => !inventory.EquipHeld("test-inv-noprefab", dummyAnchor)
                && manager.GetRecord("test-inv-noprefab").custodyLocation == EvidenceCustodyLocation.Inventory);

            check("EquipHeld spawns the held visual and moves custody to Held", () => inventory.EquipHeld("test-inv-A", dummyAnchor)
                && manager.GetRecord("test-inv-A").custodyLocation == EvidenceCustodyLocation.Held
                && inventory.IsHeld("test-inv-A"));

            check("only one held object at a time: equipping B auto-unequips A", () => inventory.EquipHeld("test-inv-B", dummyAnchor)
                && inventory.IsHeld("test-inv-B")
                && !inventory.IsHeld("test-inv-A")
                && manager.GetRecord("test-inv-A").custodyLocation == EvidenceCustodyLocation.Inventory
                && manager.GetRecord("test-inv-B").custodyLocation == EvidenceCustodyLocation.Held);

            check("UnequipHeld returns the item to Inventory", () => { inventory.UnequipHeld(); return !inventory.IsHeld("test-inv-B") && manager.GetRecord("test-inv-B").custodyLocation == EvidenceCustodyLocation.Inventory; });

            check("UnequipHeld with nothing held is a safe no-op", () => { inventory.UnequipHeld(); return inventory.CurrentHeldEvidenceId == null; });

            // --- HeldItemCoordinator mutual exclusion (tool <-> evidence) ---
            // PlayerToolRegistry is process-wide static state (by design), and this
            // test runs inside the REAL scene's Play Mode session, which already has a
            // real PlayerTool registered for every ToolType - including Photographer
            // (PhotographTool). PlayerToolRegistry.Register refuses a second tool for an
            // already-claimed role, so FakeTool would silently never actually register
            // without evicting the real one first. Evicted here, restored in the
            // finally block below - this never touches the real scene's own gameplay,
            // only this test's own brief window.
            var toolsField = typeof(PlayerToolRegistry).GetField("tools", BindingFlags.NonPublic | BindingFlags.Static);
            var toolsDict = (System.Collections.Generic.Dictionary<ToolType, PlayerTool>)toolsField.GetValue(null);
            toolsDict.TryGetValue(ToolType.Photographer, out evictedRealTool);
            toolsDict.Remove(ToolType.Photographer);
            evictedPhotographerSlot = true;

            tool = rig.AddComponent<FakeTool>();
            tool.role = ToolType.Photographer;

            inventory.EquipHeld("test-inv-A", dummyAnchor);
            check("setup: A is held before tool-equip test", () => inventory.IsHeld("test-inv-A"));

            // Re-cleared here, not just once at the top of the method: a real
            // HypothesisCheckpointManager trigger can legitimately fire mid-test (e.g.
            // once enough of this test's own MarkFound/MarkCollected/MarkSealed calls
            // above satisfy a real scenario's checkpoint condition) and re-enter
            // PlayerUIGate through the real scene's own HypothesisCheckpointUI - a
            // genuine gameplay lock, not a bug, but one this test must not let leak into
            // its own EquipEvidence/ToggleEquipTool checks below, which all gate on
            // PlayerUIGate.IsBlocked. See this test's own explicit
            // PlayerUIGate-blocks-interaction checks near the end for the one place a
            // blocker is intentionally left in place.
            blockersSet.Clear();
            bool toolEquipped = coordinator.ToggleEquipTool(ToolType.Photographer, dummyAnchor);
            check("equipping a tool unequips held evidence", () => toolEquipped
                && !inventory.IsHeld("test-inv-A")
                && manager.GetRecord("test-inv-A").custodyLocation == EvidenceCustodyLocation.Inventory
                && coordinator.CurrentCategory == HeldItemCoordinator.HeldCategory.Tool
                && coordinator.CurrentToolRole == ToolType.Photographer);

            blockersSet.Clear();
            bool evidenceEquipped = coordinator.EquipEvidence("test-inv-A", dummyAnchor);
            check("equipping evidence holsters the current tool", () => evidenceEquipped
                && inventory.IsHeld("test-inv-A")
                && PlayerToolRegistry.CurrentRole == ToolType.None
                && coordinator.CurrentCategory == HeldItemCoordinator.HeldCategory.Evidence
                && coordinator.CurrentEvidenceId == "test-inv-A");

            check("CurrentCategory reflects exactly one held object, never both", () =>
            {
                var category = coordinator.CurrentCategory;
                bool toolHeld = PlayerToolRegistry.CurrentRole != ToolType.None;
                bool evidenceHeld = inventory.CurrentHeldEvidenceId != null;
                return !(toolHeld && evidenceHeld) && category == (evidenceHeld ? HeldItemCoordinator.HeldCategory.Evidence : toolHeld ? HeldItemCoordinator.HeldCategory.Tool : HeldItemCoordinator.HeldCategory.None);
            });

            // --- Deposit ---
            blockersSet.Clear();
            check("cannot deposit a tool (Deposit requires the item to be the currently HELD EVIDENCE)", () =>
            {
                coordinator.ToggleEquipTool(ToolType.Photographer, dummyAnchor); // holsters evidence, equips tool
                bool depositResult = inventory.Deposit("test-inv-A", "TEST-BOX"); // A is no longer held (it's in Inventory)
                return !depositResult;
            });

            blockersSet.Clear();
            coordinator.EquipEvidence("test-inv-A", dummyAnchor);
            check("cannot deposit an in-inventory item that is not currently held", () => !inventory.Deposit("test-inv-noprefab", "TEST-BOX"));
            check("cannot deposit a different in-inventory item while another is held", () => !inventory.Deposit("test-inv-B", "TEST-BOX"));

            check("Deposit succeeds for the currently held, sealed item", () => inventory.Deposit("test-inv-A", "TEST-BOX")
                && manager.GetRecord("test-inv-A").custodyLocation == EvidenceCustodyLocation.EvidenceBox);

            check("deposited evidence leaves the live inventory", () => !inventory.IsInInventory("test-inv-A"));
            check("deposited evidence is marked deposited", () => inventory.IsDeposited("test-inv-A"));
            check("deposited evidence cannot be re-equipped", () => !inventory.EquipHeld("test-inv-A", dummyAnchor));
            check("one deposit removes it from CurrentHeldEvidenceId (one press deposits once)", () => inventory.CurrentHeldEvidenceId != "test-inv-A");
            check("a second Deposit call for the same id fails (no double-deposit)", () => !inventory.Deposit("test-inv-A", "TEST-BOX"));

            // --- PlayerUIGate ---
            var gateToken = new object();
            PlayerUIGate.Enter(gateToken);
            try
            {
                check("PlayerUIGate blocks EquipEvidence", () => !coordinator.EquipEvidence("test-inv-B", dummyAnchor));
                check("PlayerUIGate blocks ToggleEquipTool", () => !coordinator.ToggleEquipTool(ToolType.Photographer, dummyAnchor));
            }
            finally
            {
                PlayerUIGate.Exit(gateToken);
            }
        }
        finally
        {
            // Unregister(tool) already clears CurrentTool/CurrentRole/VirtuallyEquippedRole
            // if they were pointing at our FakeTool (see PlayerToolRegistry.Unregister's
            // own "if (CurrentTool == tool)" branch) - nothing further needed for that.
            if (tool != null)
            {
                PlayerToolRegistry.Unregister(tool);
            }

            // Restore exactly the one slot this test evicted - PlayerToolRegistry is
            // process-wide static state shared with the REAL scene's own live tools
            // when this runs inside a real Play Mode session, so a blanket reset
            // (tools.Clear(), as PlayerToolRegistry.ResetOnPlaySessionStart does) would
            // silently wipe every real tool's registration, not just this test's own.
            if (evictedPhotographerSlot)
            {
                var toolsField = typeof(PlayerToolRegistry).GetField("tools", BindingFlags.NonPublic | BindingFlags.Static);
                var toolsDict = (System.Collections.Generic.Dictionary<ToolType, PlayerTool>)toolsField.GetValue(null);
                if (evictedRealTool != null)
                {
                    toolsDict[ToolType.Photographer] = evictedRealTool;
                }
                else
                {
                    toolsDict.Remove(ToolType.Photographer);
                }
            }

            DestroyImmediate(rig);
            foreach (var asset in assets) DestroyImmediate(asset);
            foreach (var p in sceneProps) if (p != null) DestroyImmediate(p.gameObject);
            DestroyImmediate(dummyAnchor.gameObject);
            DestroyImmediate(placeholderHeld);

            typeof(EvidenceStateManager).GetProperty("Instance").SetValue(null, previousStateManager);
            typeof(EvidenceInventory).GetProperty("Instance").SetValue(null, previousInventory);
            typeof(HeldItemCoordinator).GetProperty("Instance").SetValue(null, previousCoordinator);

            // Restore whatever real scene blockers were present before this test ran -
            // this test's own gate token (gateToken above) was already exited by its
            // own inner finally, so restoring the pre-test snapshot here can't
            // reintroduce a stale lock.
            blockersSet.Clear();
            foreach (var b in savedBlockers) blockersSet.Add(b);
        }

        report.AppendLine($"=== { (failed == 0 ? "PASS" : "FAIL") }: {passed} passed, {failed} failed ===");
        return report.ToString();
    }
}
