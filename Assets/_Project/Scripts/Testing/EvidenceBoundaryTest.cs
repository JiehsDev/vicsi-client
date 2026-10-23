using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>Isolated state-boundary regressions. Run in Play mode; never advances scene evidence.</summary>
public class EvidenceBoundaryTest : MonoBehaviour
{
    public string RunAllTests()
    {
        var report = new StringBuilder("=== EvidenceBoundaryTest ===\n");
        int passed = 0, failed = 0;
        Action<string, Func<bool>> check = (name, test) =>
        {
            try { if (test()) { passed++; report.AppendLine("PASS: " + name); }
                else { failed++; report.AppendLine("FAIL: " + name); } }
            catch (Exception e) { failed++; report.AppendLine("FAIL: " + name + " (" + e.GetType().Name + ")"); }
        };
        var previous = EvidenceStateManager.Instance;
        var assets = new List<EvidenceDefinition>();
        var go = new GameObject("EvidenceBoundaryTestRig");
        go.SetActive(false);
        var manager = go.AddComponent<EvidenceStateManager>();
        try
        {
            foreach (var id in new[] { "test-body", "test-object", "test-other", "test-swab", "test-print" })
            {
                var def = ScriptableObject.CreateInstance<EvidenceDefinition>();
                def.evidenceId = id;
                def.examinedInPlace = id == "test-body";
                def.requiresSwabbing = id == "test-swab";
                def.requiresFingerprinting = id == "test-print";
                def.fingerprintBeforeCollection = id == "test-print";
                assets.Add(def);
            }
            typeof(EvidenceStateManager).GetField("sceneEvidenceDefinitions", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(manager, assets);
            go.SetActive(true);
            check("null evidence ID returns no record", () => manager.GetRecord(null) == null);
            check("unknown evidence cannot be collected", () => manager.MarkCollected("unknown", ToolType.None) == TransitionResult.Violation);
            check("null evidence cannot be collected", () => manager.MarkCollected(null, ToolType.None) == TransitionResult.Violation);
            check("unknown lifecycle status cannot advance", () => !manager.CanAdvanceTo("test-object", (EvidenceStatus)999));
            check("skipping photo/notes blocks collection", () => manager.MarkCollected("test-object", ToolType.None) == TransitionResult.Violation);
            foreach (var def in assets)
            {
                manager.MarkFound(def.evidenceId, ToolType.None);
                manager.MarkPhotographedRaw(def.evidenceId, ToolType.Photographer);
                manager.MarkChalkMarked(def.evidenceId, ToolType.None);
            }
            check("numbered body marker rejected without mutation", () => manager.MarkTented("test-body", ToolType.None, 1) == TransitionResult.Violation
                && manager.GetRecord("test-body").status == EvidenceStatus.ChalkMarked);
            check("ordinary object rejects letter marker", () => manager.MarkTentedWithLetter("test-object", ToolType.None, "A") == TransitionResult.Violation
                && manager.GetRecord("test-object").status == EvidenceStatus.ChalkMarked);
            check("blank body marker rejected", () => manager.MarkTentedWithLetter("test-body", ToolType.None, " ") == TransitionResult.Violation);
            check("numeric body letter rejected", () => manager.MarkTentedWithLetter("test-body", ToolType.None, "1") == TransitionResult.Violation);
            check("body accepts A", () => manager.MarkTentedWithLetter("test-body", ToolType.None, "A") == TransitionResult.Applied
                && manager.GetRecord("test-body").tentLetter == "A" && manager.GetRecord("test-body").tentNumber == null);
            check("same body marker is a safe duplicate", () => manager.MarkTentedWithLetter("test-body", ToolType.None, "A") == TransitionResult.Duplicate);
            check("zero numbered marker rejected", () => manager.MarkTented("test-object", ToolType.None, 0) == TransitionResult.Violation);
            check("negative numbered marker rejected", () => manager.MarkTented("test-object", ToolType.None, -1) == TransitionResult.Violation);
            check("object accepts numbered marker", () => manager.MarkTented("test-object", ToolType.None, 1) == TransitionResult.Applied);
            check("same marker is a safe duplicate", () => manager.MarkTented("test-object", ToolType.None, 1) == TransitionResult.Duplicate);
            check("marker cannot be reassigned by duplicate call", () => manager.MarkTented("test-object", ToolType.None, 2) == TransitionResult.Violation
                && manager.GetRecord("test-object").tentNumber == 1);
            check("number cannot mark two objects", () => manager.MarkTented("test-other", ToolType.None, 1) == TransitionResult.Violation);
            manager.MarkPhotographedFinal("test-body", ToolType.None);
            manager.MarkLogged("test-body", ToolType.None);
            check("body terminates at Logged", () => manager.GetRecord("test-body").status == EvidenceStatus.Logged);
            check("body cannot advance to collection readiness", () => !manager.CanAdvanceTo("test-body", EvidenceStatus.ReadyForCollection));
            check("body collection reason is explicit", () => manager.GetItemBlockReason("test-body", EvidenceStatus.Collected)?.Contains("examined in place") == true);
            check("body cannot be collected", () => manager.MarkCollected("test-body", ToolType.None) == TransitionResult.Violation);
            check("body cannot be sealed", () => manager.MarkSealed("test-body", ToolType.None) == TransitionResult.Violation);
            // Exercise the actual existing physical-object chain as a regression;
            // marker/chalk order migration has its own pending acceptance test.
            manager.MarkPhotographedFinal("test-object", ToolType.None);
            manager.MarkLogged("test-object", ToolType.None);
            check("ordinary logged item becomes ready", () => manager.IsReadyForCollection("test-object"));
            check("ordinary evidence can be collected and sealed", () => manager.MarkCollected("test-object", ToolType.None) == TransitionResult.Applied
                && manager.MarkSealed("test-object", ToolType.None) == TransitionResult.Applied
                && manager.GetRecord("test-object").status == EvidenceStatus.Processed);
            check("duplicate sealing preserves completed state", () => manager.MarkSealed("test-object", ToolType.None) == TransitionResult.Duplicate
                && manager.GetRecord("test-object").status == EvidenceStatus.Processed);
            manager.MarkTented("test-swab", ToolType.None, 3);
            manager.MarkPhotographedFinal("test-swab", ToolType.None);
            check("unswabbed trace blocks logging", () => manager.MarkLogged("test-swab", ToolType.None) == TransitionResult.Violation);
            check("swab applies once", () => manager.MarkSwabbed("test-swab", ToolType.None) == TransitionResult.Applied
                && manager.MarkSwabbed("test-swab", ToolType.None) == TransitionResult.Duplicate);
            manager.MarkTented("test-print", ToolType.None, 4);
            manager.MarkPhotographedFinal("test-print", ToolType.None);
            check("required on-scene print blocks logging", () => manager.MarkLogged("test-print", ToolType.None) == TransitionResult.Violation);
        }
        finally
        {
            DestroyImmediate(go);
            foreach (var asset in assets) DestroyImmediate(asset);
            typeof(EvidenceStateManager).GetProperty("Instance").SetValue(null, previous);
        }
        report.AppendLine($"=== { (failed == 0 ? "PASS" : "FAIL") }: {passed} passed, {failed} failed ===");
        return report.ToString();
    }
}
