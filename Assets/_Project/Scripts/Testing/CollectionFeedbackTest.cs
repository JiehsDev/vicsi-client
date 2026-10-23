// Assets/_Project/Scripts/Testing/CollectionFeedbackTest.cs
using System.Collections;
using System.Text;
using System.Linq;
using UnityEngine.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using System.Collections.Generic;

/// <summary>
/// Regression for "I can't grab the knife and nothing tells me why": walks the kitchen knife
/// (EVD-014) through its procedure one step at a time and checks that, at every stage before
/// it's ready, a grip on it names the ONE step still missing (EvidenceGrabGate.ShowNotReadyReason
/// / EvidenceStateManager.GetCollectionBlockReason) - including the last one, recording it with
/// the Recorder - and that once it's ready the grab is on and nothing is shown. State is driven
/// with the same EvidenceStateManager calls GreyboxFlowTest uses; the grip-near-item detection
/// itself needs a headset and is not exercised here.
///
/// Run from the editor via:
///     FindFirstObjectByType&lt;CollectionFeedbackTest&gt;().RunAndLog()
/// </summary>
public class CollectionFeedbackTest : MonoBehaviour
{
    private const string Knife = "EVD-014";

    public bool IsDone { get; private set; }
    public string Report { get; private set; }

    private StringBuilder report;
    private int failures;

    public void RunAndLog()
    {
        AutomatedTestSandbox.Activate(nameof(CollectionFeedbackTest));
        DontDestroyOnLoad(gameObject);
        StartCoroutine(Run());
    }

    private void Check(string label, bool ok)
    {
        report.AppendLine($"  {(ok ? "PASS" : "FAIL")}: {label}");
        if (!ok) failures++;
    }

    private IEnumerator Run()
    {
        IsDone = false;
        failures = 0;
        report = new StringBuilder("=== CollectionFeedbackTest ===\n");
        yield return null;

        var esm = EvidenceStateManager.Instance;
        EvidenceGrabGate gate = null;
        foreach (var g in FindObjectsByType<EvidenceGrabGate>(FindObjectsSortMode.None))
        {
            if (g.GetComponent<EvidenceProp>().evidenceId == Knife) gate = g;
        }
        if (esm == null || gate == null)
        {
            Finish("FAIL: EvidenceStateManager or the knife's EvidenceGrabGate is missing.");
            yield break;
        }
        if (SceneTurnover.IsPending) SceneTurnover.Controller.CompleteWithoutDialogue();
        Check("scene photography route completed (precondition)", ScenePhotographyTestUtil.CompleteRoute());

        var record = esm.GetRecord(Knife);
        Expect(gate, "NotFound", GameDialogue.Text("evidence-step-find-first"));

        esm.MarkFound(Knife, ToolType.None);
        Expect(gate, "Found", GameDialogue.Text("evidence-step-photograph-first"));

        esm.MarkPhotographedRaw(Knife, ToolType.Photographer);
        Expect(gate, "PhotographedRaw", record.definition.requiresChalk
            ? GameDialogue.Text("evidence-step-chalk-first")
            : GameDialogue.Text("evidence-step-marker-first"));

        if (record.definition.requiresChalk)
        {
            esm.MarkChalkMarked(Knife, ToolType.ChalkMark);
            Expect(gate, "ChalkMarked", GameDialogue.Text("evidence-step-marker-first"));
        }

        esm.MarkTented(Knife, ToolType.EvidenceMarker, 1);
        Expect(gate, "Marked, blood trace not tented", GameDialogue.Text("evidence-trace-marker-first"));

        TraceMarkerTestUtil.MarkTraceIfRequired(Knife);
        Expect(gate, "Marked, both tents down", GameDialogue.Text("evidence-step-final-photo-first"));
        esm.MarkPhotographedFinal(Knife, ToolType.Photographer);
        Check($"knife reached PhotographedFinal [{record.status}]", record.status == EvidenceStatus.PhotographedFinal);
        Expect(gate, "PhotographedFinal, not swabbed", GameDialogue.Text("evidence-blood-sample-before-record"));

        // Sampling can itself be gated on measurements: take them only if the swab is refused.
        bool measuredEarly = false;
        if (esm.MarkSwabbed(Knife, ToolType.Swab) == TransitionResult.Violation)
        {
            MeasurementTestUtil.CompleteDiscoveredMeasurements();
            MeasurementTestUtil.CompleteSketchFor(Knife);
            measuredEarly = true;
            esm.MarkSwabbed(Knife, ToolType.Swab);
        }
        if (!record.swabSampleSealed) esm.MarkSwabSampleSealed(Knife, ToolType.Swab);
        Check($"knife swabbed [{record.swabbingDone}]", record.swabbingDone);
        Expect(gate, "swabbed, print not lifted", GameDialogue.Text("evidence-fingerprint-before-record"));

        esm.MarkFingerprinted(Knife, ToolType.FingerprintKit);
        Check($"knife fingerprinted on scene [{record.fingerprintingDone}]", record.fingerprintingDone);

        if (!measuredEarly)
        {
            Expect(gate, "measurements incomplete", GameDialogue.Text("step-measurements-before-records"));
            MeasurementTestUtil.CompleteDiscoveredMeasurements();
        }

        // The reported case: everything done except recording it.
        string recorderLine = GameDialogue.Text("evidence-step-documentation-first");
        Check($"the Logged step's wording names the Recorder ['{recorderLine}']", recorderLine.Contains("Recorder"));
        Expect(gate, "not recorded yet", recorderLine);
        Check("grab still off before recording", !gate.GrabEnabled);

        esm.MarkLogged(Knife, ToolType.Recorder);
        yield return null;
        Check($"recorded -> ReadyForCollection [{record.status}]", record.status == EvidenceStatus.ReadyForCollection);
        Check("grab switched on once ready", gate.GrabEnabled);
        Check("nothing to explain once ready", esm.GetCollectionBlockReason(Knife) == null && ResetAndShow(gate) == null);

        yield return CheckBagAndStore(Knife);

        // Same authored bottle prerequisites, using the existing procedure API.
        const string bottleId = "EVD-018";
        esm.MarkFound(bottleId, ToolType.None);
        esm.MarkPhotographedRaw(bottleId, ToolType.Photographer);
        if (esm.GetRecord(bottleId).definition.requiresChalk) esm.MarkChalkMarked(bottleId, ToolType.ChalkMark);
        esm.MarkTented(bottleId, ToolType.EvidenceMarker, 2);
        MeasurementTestUtil.CompleteDiscoveredMeasurements();
        esm.MarkPhotographedFinal(bottleId, ToolType.Photographer);
        MeasurementTestUtil.CompleteSketchFor(bottleId);
        esm.MarkFingerprinted(bottleId,ToolType.FingerprintKit); // This suite isolates bagging; real bottle sampling has its own suite.
        esm.MarkMatched(bottleId);
        esm.MarkLogged(bottleId, ToolType.Recorder);
        Check("bottle reaches collection after on-scene fingerprint processing",
            esm.GetRecord(bottleId).status == EvidenceStatus.ReadyForCollection && esm.GetRecord(bottleId).fingerprintingDone);
        yield return CheckBagAndStore(bottleId);
        Check("knife and bottle remain separate stored IDs/names", EvidenceInventory.Instance.IsInInventory(Knife)
            && EvidenceInventory.Instance.IsInInventory(bottleId)
            && EvidenceInventory.Instance.GetDefinition(Knife).displayName != EvidenceInventory.Instance.GetDefinition(bottleId).displayName);

        yield return SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().path);
        yield return null;
        Check("reset clears bags/custody and restores both original evidence props", !EvidenceInventory.Instance.AllEntryIds.Any()
            && EvidenceInventory.Instance.CurrentHeldEvidenceId == null
            && new[] { Knife, bottleId }.All(id => EvidenceProp.FindById(id) != null
                && EvidenceStateManager.Instance.GetRecord(id).custodyLocation == EvidenceCustodyLocation.Scene
                && EvidenceStateManager.Instance.GetRecord(id).status == EvidenceStatus.NotFound));
        Check("reset leaves no pending bag or completed held visual", EvidenceBagTool.Instance.TrySeal() == TransitionResult.Violation
            && EvidenceInventory.Instance.HeldVisual == null);

        Finish(failures == 0 ? "=== PASS, 0 failures ===" : $"=== FAIL: {failures} failure(s) ===");
    }

    private IEnumerator CheckBagAndStore(string id)
    {
        var inventory = EvidenceInventory.Instance;
        var bag = EvidenceBagTool.Instance;
        var coordinator = HeldItemCoordinator.Instance;
        var rig = FindFirstObjectByType<OVRCameraRig>();
        var prompt = FindAnyObjectByType<HeldEvidenceStorePrompt>();
        STCSNotificationUI.Instance.HideImmediately();
        Check($"[{id}] equip open bag via coordinator", coordinator.ToggleEquipTool(ToolType.EvidenceCollector, rig.leftHandAnchor));
        Check($"[{id}] before insertion: open visual, no completed bag or B prompt", bag.IsVirtuallyEquipped
            && bag.GetComponentsInChildren<Renderer>().Any(r => r.enabled)
            && inventory.HeldVisual == null && !prompt.IsPromptShown && !inventory.IsInInventory(id));
        var prop = EvidenceProp.FindById(id);
        InputSystem.RegisterLayout("{\"name\":\"ViCSIBagController\",\"extend\":\"XRController\",\"controls\":[{\"name\":\"triggerPressed\",\"layout\":\"Button\",\"format\":\"FLT\",\"usages\":[\"TriggerButton\"]},{\"name\":\"gripPressed\",\"layout\":\"Button\",\"format\":\"FLT\",\"usages\":[\"GripButton\"]}]}");
        var left = InputSystem.AddDevice("ViCSIBagController");
        var right = InputSystem.AddDevice("ViCSIBagController");
        InputSystem.SetDeviceUsage(left, CommonUsages.LeftHand);
        InputSystem.SetDeviceUsage(right, CommonUsages.RightHand);
        InputSystem.QueueDeltaStateEvent((ButtonControl)left["triggerPressed"], 1f);
        InputSystem.QueueDeltaStateEvent((ButtonControl)right["gripPressed"], 1f);
        yield return null; yield return null;
        Check($"[{id}] left trigger opens equipped bag", bag.IsOpen);
        var zone = (Collider)typeof(EvidenceBagTool).GetField("receivingZoneCollider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(bag);
        // Approach the visible paper bag, never the invisible detector's own center:
        // a test aimed at that center passed while the detector missed the model.
        var visual = bag.GetComponentsInChildren<Renderer>().First(r => r.enabled).bounds;
        var opening = new Vector3(visual.center.x, visual.max.y, visual.center.z);
        Check($"[{id}] receiving zone contains the visible bag opening", zone.bounds.Contains(opening));
        var body = prop.GetComponent<Rigidbody>();
        body.isKinematic = true;
        var oldRight = rig.rightHandAnchor.position;
        // The SDK grabs at its rigidbody point, which need not coincide with the
        // tracked controller anchor. Reproduce a held item reaching the bag while
        // that anchor is outside the old 15 cm insertion test.
        var grab = rig.GetComponentsInChildren<GrabInteractor>(true).First(g =>
            g.GetComponentInParent<IController>()?.Handedness == Handedness.Right);
        var forced = new List<GameObject>();
        for (var t = grab.transform; t != null; t = t.parent)
            if (!t.gameObject.activeSelf) { t.gameObject.SetActive(true); forced.Add(t.gameObject); }
        var point = grab.Rigidbody.transform;
        rig.rightHandAnchor.position = opening + Vector3.up * .5f;
        var oldPoint = point.localPosition;
        point.position = opening;
        prop.transform.position = opening;
        Physics.SyncTransforms();
        Check($"[{id}] grab point touches evidence while controller anchor is outside bagging reach",
            Vector3.Distance(rig.rightHandAnchor.position, opening) > .15f
            && prop.GetComponent<RightHandOnlyFilter>().DistanceToSurface(point.position) <= .15f);
        for (int i = 0; i < 8; i++) yield return new WaitForFixedUpdate();
        Check($"[{id}] physical receiver overlap collects without prematurely sealing/closing",
            EvidenceStateManager.Instance.GetRecord(id).status == EvidenceStatus.Collected
            && inventory.HeldVisual == null && prop.GetComponentsInChildren<Renderer>(true).All(r => !r.enabled));
        point.localPosition = oldPoint;
        rig.rightHandAnchor.position = oldRight;
        foreach (var go in forced) go.SetActive(false);
        InputSystem.QueueDeltaStateEvent((ButtonControl)left["triggerPressed"], 0f);
        for (int i = 0; i < 4; i++) yield return null;
        InputSystem.RemoveDevice(left); InputSystem.RemoveDevice(right); InputSystem.RemoveLayout("ViCSIBagController");
        Check($"[{id}] releasing left trigger seals and holds closed bag without automatically storing",
            EvidenceStateManager.Instance.IsAtOrPastStatus(id, EvidenceStatus.Sealed)
            && coordinator.CurrentEvidenceId == id && coordinator.CurrentCategory == HeldItemCoordinator.HeldCategory.Evidence
            && inventory.GetCustody(id) == EvidenceCustodyLocation.Held && !inventory.IsInInventory(id)
            && inventory.IsBaggedEvidence(id) && !bag.IsVirtuallyEquipped && !prop.gameObject.activeSelf);
#if UNITY_EDITOR
        var model = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Environments/MODELS/NEW/EVIDENCE_PAPER_BAG_CLOSED.glb");
        var meshes = model.GetComponentsInChildren<MeshFilter>(true).Select(m => m.sharedMesh).ToArray();
        Check($"[{id}] held bag uses supplied closed-paper-bag meshes", inventory.HeldVisual != null
            && inventory.HeldVisual.GetComponentsInChildren<MeshFilter>(true).All(m => meshes.Contains(m.sharedMesh)));
        CaptureBag(inventory.HeldVisual, id);
#endif
        Check($"[{id}] another tool cannot implicitly store the bag", !coordinator.ToggleEquipTool(ToolType.Photographer, rig.rightHandAnchor)
            && inventory.CurrentHeldEvidenceId == id && !inventory.IsInInventory(id));
        PlayerUIGate.Enter(this);
        yield return null;
        Check($"[{id}] modal suppresses B storage and prompt", !prompt.TryStore() && !prompt.IsPromptShown);
        PlayerUIGate.Exit(this);
        STCSNotificationUI.Instance.HideImmediately();
        yield return null;
        yield return null;
        Check($"[{id}] completed bag shows exact B prompt", prompt.IsPromptShown
            && GameDialogue.Text("prompt-store-bagged-evidence") == "Press B to store in inventory");
        Check($"[{id}] B handler stores same ID, clears prompt and frees hand", prompt.TryStore() && inventory.IsInInventory(id)
            && inventory.GetCustody(id) == EvidenceCustodyLocation.Inventory && !prompt.IsPromptShown
            && coordinator.CurrentCategory == HeldItemCoordinator.HeldCategory.None && inventory.HeldVisual == null);
        Check($"[{id}] stored item can be re-equipped and stored again", coordinator.EquipEvidence(id, rig.rightHandAnchor)
            && inventory.IsHeld(id) && coordinator.TryStoreHeldEvidence());
    }

    private static void CaptureBag(GameObject model, string id)
    {
        if (model == null) return;
        var renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        var camera = new GameObject("BagVisualCheck").AddComponent<Camera>();
        camera.nearClipPlane = .001f; camera.fieldOfView = 35f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f, .14f, .16f);
        camera.transform.position = bounds.center + new Vector3(1f, .3f, 1f).normalized * bounds.size.magnitude * 2f;
        camera.transform.LookAt(bounds.center);
        var light = new GameObject("BagCheckLight").AddComponent<Light>();
        light.type = LightType.Point; light.intensity = 10f; light.range = 3f; light.transform.position = camera.transform.position;
        var target = new RenderTexture(700, 700, 24); camera.targetTexture = target; camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = target;
        var image = new Texture2D(700, 700, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 700, 700), 0, 0); image.Apply();
        System.IO.Directory.CreateDirectory("docs/evidence/bagged-evidence-inventory");
        System.IO.File.WriteAllBytes("docs/evidence/bagged-evidence-inventory/" + id + "-bag.png", image.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null;
        Destroy(target); Destroy(image); Destroy(light.gameObject); Destroy(camera.gameObject);
    }

    // One stage: the resolver names exactly this step, and a grip shows it wrapped in the
    // "not ready for collection" line (never a generic fallback).
    private void Expect(EvidenceGrabGate gate, string stage, string expectedReason)
    {
        string reason = EvidenceStateManager.Instance.GetCollectionBlockReason(Knife);
        string shown = ResetAndShow(gate);
        string expectedLine = GameDialogue.Text("collect-not-ready", EvidenceStateManager.Instance.GetDisplayName(Knife), expectedReason);
        Check($"[{stage}] grip names the missing step: '{shown}'",
            reason == expectedReason && shown == expectedLine && !gate.GrabEnabled);
    }

    // Clears the per-item cooldown so each stage's grip is shown.
    private static string ResetAndShow(EvidenceGrabGate gate)
    {
        typeof(EvidenceGrabGate).GetField("lastRefusalTime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(gate, -999f);
        return gate.ShowNotReadyReason();
    }

    private void Finish(string last)
    {
        report.AppendLine(last);
        Report = report.ToString();
        Debug.Log(Report);
        IsDone = true;
    }
}
