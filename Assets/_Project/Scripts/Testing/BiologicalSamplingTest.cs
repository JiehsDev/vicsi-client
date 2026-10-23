// Assets/_Project/Scripts/Testing/BiologicalSamplingTest.cs
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;

/// <summary>
/// Play-mode check of biological sampling order. Real tools throughout: PhotographTool,
/// ChalkMarkTool, EvidenceTentTool / LetterTentTool, MeasurementTool, SwabTool
/// (TrySampleRequiredSwab -> TryContain -> TrySeal), FingerprintKitTool (TryLoadDust /
/// TryDust), RecorderTool.TryOpenReport; the rest of each procedure via GreyboxFlowTest
/// resumed from where the item stands.
///   Knife:   fingerprint before swab is refused; swab -> container -> fingerprint works.
///   Spatter: Recorder before the sample (and before its seal) is refused; then works.
///   Bottle / body: unchanged paths, no swab requirement.
/// Run once per fresh Play session: RunAllTests(), then poll IsDone / Report.
/// </summary>
public class BiologicalSamplingTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }

    private EvidenceStateManager esm;
    private OVRCameraRig rig;
    private PhotographTool camera;
    private MeasurementTool tape;
    private EvidenceTentTool tents;
    private LetterTentTool letterTent;
    private GreyboxFlowTest flow;
    private List<string> failures;
    private StringBuilder report;
    private int nextTent = 1;

    public void RunAllTests()
    {
        AutomatedTestSandbox.Activate(nameof(BiologicalSamplingTest));
        DontDestroyOnLoad(gameObject);
        IsDone = false;
        Application.logMessageReceived += OnLog;
        StartCoroutine(Run());
    }
    private void OnDestroy() => Application.logMessageReceived -= OnLog;
    private void OnLog(string message,string stack,LogType type)
    {
        if(type!=LogType.Exception || IsDone)return;
        report.AppendLine("FAIL: "+message);report.AppendLine("=== FAIL ===");Report=report.ToString();IsDone=true;
    }

    private IEnumerator Run()
    {
        failures = new List<string>();
        report = new StringBuilder("=== BiologicalSamplingTest ===\n");
        esm = EvidenceStateManager.Instance;
        rig = FindFirstObjectByType<OVRCameraRig>();
        camera = FindFirstObjectByType<PhotographTool>(FindObjectsInactive.Include);
        tape = FindFirstObjectByType<MeasurementTool>(FindObjectsInactive.Include);
        tents = FindFirstObjectByType<EvidenceTentTool>(FindObjectsInactive.Include);
        letterTent = FindFirstObjectByType<LetterTentTool>(FindObjectsInactive.Include);
        flow = FindFirstObjectByType<GreyboxFlowTest>(FindObjectsInactive.Include);
        if (esm == null || rig == null || camera == null || tape == null || tents == null || letterTent == null || flow == null
            || SwabTool.Instance == null || FingerprintKitTool.Instance == null || RecorderTool.Instance == null)
        {
            Report = "FAIL: required objects missing.";
            IsDone = true;
            yield break;
        }
        yield return null;
        Check("[training] team-role concept scheduled at SOCO investigation start", ProceduralTrainingGuidance.Instance != null
            && ProceduralTrainingGuidance.Instance.HasScheduled("TRAIN-SOCO-TEAM"));
        Check("[container] clean kit refuses insertion and has no held completed container",
            !SwabTool.Instance.HasSample && SwabTool.Instance.TryContain() == TransitionResult.Violation
            && EvidenceInventory.Instance.HeldVisual == null);

        Check("[data] knife: swab before fingerprinting", Def("EVD-014").swabBeforeFingerprinting && Def("EVD-014").requiresSwabbing);
        Check("[data] spatter: on-scene sample that is its collection", Def("EVD-015").requiresSwabbing && Def("EVD-015").collectedBySwab);
        Check("[data] bottle and body: no swab requirement",
            !Def("EVD-018").requiresSwabbing && !Def("EVD-018").swabBeforeFingerprinting && !Def("BODY-A").requiresSwabbing);

        foreach (var id in new[] { "BODY-A", "EVD-014", "EVD-015", "EVD-018" })
        {
            esm.MarkFound(id, ToolType.None);
        }
        yield return DismissCheckpoints();
        Check("[scene] overall scene photography completed before evidence work", ScenePhotographyTestUtil.CompleteRoute());
        Check("[training] completed photo route schedules photography context", ProceduralTrainingGuidance.Instance.HasScheduled("TRAIN-PHOTOGRAPHY"));
        yield return null;

        // ---------------- Knife ----------------
        yield return DocumentToFinalPhoto("EVD-014");
        Check("[training] knife documentation schedules marker, trace source and measurement concepts",
            new[] { "TRAIN-MARKER", "TRAIN-TRACE-SOURCE", "TRAIN-MEASUREMENT" }.All(ProceduralTrainingGuidance.Instance.HasScheduled));
        var knife = esm.GetRecord("EVD-014");
        var printMarker = EvidenceFingerprintMarker.FindByEvidenceId("EVD-014");
        int blockedBefore = Count("\"eventType\":\"EvidenceTransitionBlocked\"");

        FingerprintKitTool.Instance.TryLoadDust();
        bool dusted = FingerprintKitTool.Instance.TryDust(printMarker);
        FingerprintKitTool.Instance.TryDust(printMarker); // still held on the print, next frame
        Check("[knife wrong order] powder refused before the blood sample: print not dusted, not fingerprinted",
            !dusted && !printMarker.IsDusted && !knife.fingerprintingDone && knife.fingerprintLabStatus == FingerprintLabStatus.NotSent);
        Check("[knife wrong order] reason 'Collect the blood sample before applying fingerprint powder.'",
            esm.GetFingerprintBlockReason("EVD-014") == "Collect the blood sample before applying fingerprint powder.");
        Check("[knife wrong order] logged once as a blocked action (not per frame)", Count("\"eventType\":\"EvidenceTransitionBlocked\"") - blockedBefore == 1);
        Check("[knife wrong order] the authoritative fingerprint step refuses too", esm.MarkFingerprinted("EVD-014", ToolType.FingerprintKit) == TransitionResult.Violation && !knife.fingerprintingDone);
        Check("[knife wrong order] no ProcedureOutOfOrder added for a blocked action", Count("\"eventType\":\"ProcedureOutOfOrder\"") == 0);

        Equip(ToolType.Swab);
        Check("[knife] swab the blade trace", SwabTool.Instance.TrySampleRequiredSwab(EvidenceSwabRequirement.FindByEvidenceId("EVD-014")));
        CheckUsedSwab("EVD-014");
        Check("[training] touching blood alone does not schedule containment lesson", !ProceduralTrainingGuidance.Instance.HasScheduled("TRAIN-BIOLOGICAL-SAMPLE"));
        Check("[knife] sample into the container", SwabTool.Instance.TryContain() == TransitionResult.Applied && knife.swabbingDone);
        Check("[training] successful source-specific containment schedules biological sampling lesson", ProceduralTrainingGuidance.Instance.HasScheduled("TRAIN-BIOLOGICAL-SAMPLE"));
        string knifeSample = SwabTool.Instance.PendingContainerId;
        Check("[knife] insertion holds completed model, hides loose swab, retains source and does not store/seal automatically",
            knifeSample != null && EvidenceInventory.Instance.GetSampleSource(knifeSample) == "EVD-014"
            && EvidenceInventory.Instance.HeldVisual != null && PlayerToolRegistry.CurrentRole == ToolType.None
            && !SwabTool.Instance.IsVirtuallyEquipped && !knife.swabSampleSealed && !EvidenceInventory.Instance.IsInInventory(knifeSample)
            && knife.status == EvidenceStatus.PhotographedFinal && EvidenceProp.FindById("EVD-014") != null);
        yield return VerifyAndStoreSample(knifeSample, "knife");
        Check("[training] explicit B storage schedules custody lesson", ProceduralTrainingGuidance.Instance.HasScheduled("TRAIN-CUSTODY"));
        yield return GloveChangeTestUtil.VisitAndReplace();
        Check("[knife] fingerprint processing now allowed", esm.GetFingerprintBlockReason("EVD-014") == null);
        yield return FinishWithFlow("EVD-014");
        Check("[knife] print dusted, lifted and matched to Paul",
            knife.fingerprintingDone && knife.fingerprintLabStatus == FingerprintLabStatus.Matched && knife.fingerprintLabMatchResult == "Paul");
        Check("[training] complete fingerprint lifting and physical packaging schedule their concepts",
            ProceduralTrainingGuidance.Instance.HasScheduled("TRAIN-FINGERPRINT") && ProceduralTrainingGuidance.Instance.HasScheduled("TRAIN-PACKAGING"));

        // ---------------- Spatter ----------------
        yield return DocumentToFinalPhoto("EVD-015");
        var spatter = esm.GetRecord("EVD-015");
        var spatterProp = EvidenceProp.FindById("EVD-015");
        Check("[spatter wrong order] Recorder before the sample: refused, not logged",
            !RecorderTool.Instance.TryOpenReport(spatterProp) && spatter.status == EvidenceStatus.PhotographedFinal
            && ProceduralGateValidator.Instance.GetBlockReason("EVD-015", EvidenceStatus.Logged) == "Collect the blood sample before completing the evidence record.");
        CloseRecorderIfOpen();

        yield return GloveChangeTestUtil.VisitAndReplace();
        Equip(ToolType.Swab);
        Check("[gloves] real station replacement before a different source ("+GloveChangeTestUtil.LastFailure+")", ArrivalPPEStation.Instance.HasCleanGloves);
        Check("[spatter] swab the pattern", SwabTool.Instance.TrySampleRequiredSwab(EvidenceSwabRequirement.FindByEvidenceId("EVD-015")));
        CheckUsedSwab("EVD-015");
        int pendingBeforeSecondSample = ProceduralTrainingGuidance.Instance.PendingCount;
        Check("[spatter] sample into the container", SwabTool.Instance.TryContain() == TransitionResult.Applied && spatter.swabbingDone);
        Check("[training] second sample does not queue another biological lesson", ProceduralTrainingGuidance.Instance.PendingCount == pendingBeforeSecondSample);
        Check("[spatter] Recorder still refused until the container is sealed",
            !ProceduralGateValidator.Instance.CanTransition("EVD-015", EvidenceStatus.Logged));
        Check("[spatter] sample dried and individually packaged",SamplePreparationTestUtil.PrepareCurrent());
        Check("[spatter] container sealed", SwabTool.Instance.TrySeal() == TransitionResult.Applied && spatter.swabSampleSealed);
        Check("[spatter] trigger sealing does not automatically store and both sample IDs remain independent",
            EvidenceInventory.Instance.CurrentHeldEvidenceId == "EVD-015" && !EvidenceInventory.Instance.IsInInventory("EVD-015")
            && EvidenceInventory.Instance.GetSampleSource("EVD-015") == "EVD-015"
            && EvidenceInventory.Instance.GetSampleSource(knifeSample) == "EVD-014" && EvidenceInventory.Instance.IsInInventory(knifeSample));
        yield return VerifyAndStoreSample("EVD-015", "spatter");
        yield return GloveChangeTestUtil.VisitAndReplace();
        Check("[spatter] Recorder now allowed", ProceduralGateValidator.Instance.CanTransition("EVD-015", EvidenceStatus.Logged));
        Check("[spatter] the old swab-to-collect path is not offered (no second swab)", !SwabTool.Instance.TrySwab(spatterProp));
        yield return FinishWithFlow("EVD-015");
        Check("[spatter] sealed sample carried custody: Processed and in the inventory, one Collected / one Sealed event",
            spatter.status == EvidenceStatus.Processed && EvidenceInventory.Instance.IsInInventory("EVD-015")
            && CountStatus("EVD-015", "Collected") == 1 && CountStatus("EVD-015", "Sealed") == 1);

        // ---------------- Bottle ----------------
        yield return DocumentToFinalPhoto("EVD-018");
        Check("[bottle] no fingerprint gate from sampling", esm.GetFingerprintBlockReason("EVD-018") == null);
        yield return FinishWithFlow("EVD-018");
        var bottle = esm.GetRecord("EVD-018");
        Check("[bottle] Recorder -> collect -> bag -> seal -> lab print matched (Paul)",
            bottle.status == EvidenceStatus.Processed && !bottle.swabbingDone && bottle.fingerprintLabMatchResult == "Paul");

        // ---------------- Body ----------------
        yield return DocumentToFinalPhoto("BODY-A");
        // Evidence records wait for the discovered evidence's measurements (fixed references + pairs among discovered items).
        MeasurementTestUtil.CompleteDiscoveredMeasurements();
        esm.RecordReportLinesSelected("BODY-A", new[] { "body-a-wound-sharp-object" });
        Check("[body] Recorder with no sample requirement; examined in place",
            esm.MarkLogged("BODY-A", ToolType.Recorder) == TransitionResult.Duplicate
            && esm.GetRecord("BODY-A").status == EvidenceStatus.Logged && esm.GetRecord("BODY-A").custodyLocation == EvidenceCustodyLocation.Scene);

        Check("[log] no ProcedureOutOfOrder in a correctly ordered session", Count("\"eventType\":\"ProcedureOutOfOrder\"") == 0);

        // Continue the same real-tool session through body removal and all six custody items.
        RelationshipReviewTestUtil.BeginCaseFixture();
        HeldItemCoordinator.Instance.HolsterCurrentTool();
        rig.enabled=false;Camera.main.tag="Untagged";
        var completionEye=new GameObject("Completed procedure standing observer",typeof(Camera));completionEye.tag="MainCamera";
        var bodyProp=EvidenceProp.FindById("BODY-A");
        completionEye.transform.position=bodyProp.transform.position+Vector3.forward*.8f+Vector3.up*1.6f;
        var removal=bodyProp.GetComponent<BodyRemovalInteraction>();
        STCSNotificationUI.Instance.HideImmediately();NotificationManager.Instance.UI.ClearTooltip();
        report.AppendLine("INFO: body removal remaining requirement="+esm.GetBodyRemovalBlockReason("BODY-A"));
        Check("[completion] actual body preparation can begin after documentation",removal.RequestPreparation());
        yield return new WaitForSecondsRealtime(.3f);
        Check("[completion] body preparation requires a separate confirmation",removal.ConfirmPreparation());
        double removalDeadline=Time.realtimeSinceStartupAsDouble+12;
        while(removal.IsTransitioning&&Time.realtimeSinceStartupAsDouble<removalDeadline)yield return null;
        Check("[completion] documented body is removed and transition ownership is released",removal.VisualRemoved&&!removal.IsTransitioning&&!PlayerUIGate.IsBlocked);
        var custody=EvidenceInventory.Instance;var box=EvidenceBoxDepositZone.Instance;
        Check("[completion] actual procedures leave no unfinished physical job",SOCOCloseout.Instance.AllAccountedFor&&box.ReadyForTransfer&&custody.AllTrackedEntryIds.Count==6);
        completionEye.transform.position=box.GetComponent<Collider>().bounds.center+Vector3.up*1.6f;
        STCSNotificationUI.Instance.HideImmediately();NotificationManager.Instance.UI.ClearTooltip();
        Check("[completion] evidence-box review opens after the complete procedure",box.RequestCustodyPass());
        yield return new WaitForSecondsRealtime(.3f);
        Check("[completion] all six real packages transfer to the box",box.ConfirmCustodyPass()&&box.PassComplete&&custody.AllCustodyDeposited&&box.Contents.ItemCount==6);

        HeldItemCoordinator.Instance.HolsterCurrentTool();
        yield return SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().path);
        yield return null;
        Check("[reset] fresh scene clears both samples, loose swab and source flags; original evidence remains",
            !EvidenceInventory.Instance.AllEntryIds.Any() && EvidenceInventory.Instance.HeldVisual == null
            && !SwabTool.Instance.HasSample && SwabTool.Instance.PendingContainerId == null
            && new[] { "EVD-014", "EVD-015" }.All(id => !EvidenceStateManager.Instance.GetRecord(id).swabbingDone
                && !EvidenceStateManager.Instance.GetRecord(id).swabSampleSealed && EvidenceProp.FindById(id) != null));
        Check("[training reset] fresh scene owns an empty milestone history", ProceduralTrainingGuidance.Instance != null
            && !ProceduralTrainingGuidance.Instance.HasScheduled("TRAIN-BIOLOGICAL-SAMPLE")
            && !ProceduralTrainingGuidance.Instance.HasScheduled("TRAIN-PHOTOGRAPHY"));
        report.AppendLine(failures.Count == 0 ? "=== PASS, 0 failures ===" : $"=== FAIL: {failures.Count} failure(s) ===");
        Report = report.ToString();
        IsDone = true;
    }

    private IEnumerator VerifyAndStoreSample(string itemId, string label)
    {
        var prompt = FindAnyObjectByType<HeldEvidenceStorePrompt>();
        var inventory = EvidenceInventory.Instance;
        Check($"[{label}] protected preparation completed",SamplePreparationTestUtil.PrepareCurrent());
        PlayerUIGate.Enter(this);
        yield return null;
        Check($"[{label}] modal suppresses prompt and B storage", !prompt.IsPromptShown && !prompt.TryStore());
        PlayerUIGate.Exit(this);
        // The preparation visit clears timed tooltips, allowing queued teaching lines
        // to play. Dismiss those fixture lines before asserting the normal B prompt.
        for(int frame=0;frame<20;frame++)
        {
            STCSNotificationUI.Instance.HideImmediately();yield return null;
            if(prompt.IsPromptShown&&!STCSNotificationUI.Instance.IsShowing&&STCSNotificationUI.Instance.PendingCount==0)break;
        }
        Check($"[{label}] completed container offers contextual B storage", prompt.IsPromptShown
            && GameDialogue.Text("prompt-store-blood-sample") == "Press B to store in inventory");
#if UNITY_EDITOR
        var model = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Environments/MODELS/Low poly v2/CONTAINER_WITH_SWAB.glb");
        var authoredMeshes = model.GetComponentsInChildren<MeshFilter>(true).Select(m => m.sharedMesh).ToArray();
        Check($"[{label}] package retains the original sample meshes", inventory.HeldVisual != null
            && inventory.HeldVisual.GetComponentsInChildren<MeshFilter>(true).Any(m => authoredMeshes.Contains(m.sharedMesh)));
        Check($"[{label}] sample has an individual paper wrapper and source label",
            inventory.HeldVisual != null && inventory.HeldVisual.GetComponent<PaperSwabPackage>() != null
            && inventory.HeldVisual.GetComponentInChildren<UnityEngine.UI.Text>().text.Contains(EvidenceDisplay.Label(inventory.GetSampleSource(itemId)))
            && !inventory.HeldVisual.GetComponentInChildren<UnityEngine.UI.Text>().text.Contains("EVD-"));
        CaptureContainer(inventory.HeldVisual, label);
#endif
        Check($"[{label}] B handler seals/stores sample, removes prompt and frees the hand", prompt.TryStore()
            && inventory.IsInInventory(itemId) && inventory.IsSampleSealed(itemId)
            && inventory.CurrentHeldEvidenceId == null && HeldItemCoordinator.Instance.CurrentCategory == HeldItemCoordinator.HeldCategory.None
            && !prompt.IsPromptShown && inventory.HeldVisual == null);
        Check($"[{label}] inventory has a source-specific sample name", inventory.GetDefinition(itemId) != null && inventory.GetDefinition(itemId).displayName.StartsWith("Blood Sample")
            && inventory.GetDefinition(itemId).displayName.Contains(EvidenceStateManager.Instance.GetDisplayName(inventory.GetSampleSource(itemId))));
    }

    private void CheckUsedSwab(string sourceId)
    {
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var swab = SwabTool.Instance;
        var tip = typeof(SwabTool).GetField("tipRenderer", flags).GetValue(swab) as Renderer;
        var sampledMesh = typeof(SwabTool).GetField("sampledTipMesh", flags).GetValue(swab) as Mesh;
        var source = typeof(SwabTool).GetField("sampledRequirement", flags).GetValue(swab) as EvidenceSwabRequirement;
        Check($"[{sourceId}] loose swab carries this source and displays its blood-bearing tip", swab.HasSample
            && source != null && source.evidenceId == sourceId && tip != null && tip.enabled && sampledMesh != null
            && tip.GetComponent<MeshFilter>().sharedMesh == sampledMesh);
    }

    private void CaptureContainer(GameObject model, string label)
    {
        if(model==null)return;
        var position=model.transform.position;Camera capture=null;RenderTexture target=null;Texture2D image=null;
        var previous=RenderTexture.active;
        try
        {
            // Offline controller anchors can be below the floor without tracking.
            // Raise only this held visual for a synchronous render, then restore it.
            model.transform.position+=Vector3.up*5;
            var renderers=model.GetComponentsInChildren<Renderer>().Where(r=>r.enabled&&r.gameObject.activeInHierarchy).ToArray();
            if(renderers.Length==0)return;
            var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            capture=new GameObject("SampleVisualCheck").AddComponent<Camera>();capture.nearClipPlane=.001f;capture.fieldOfView=35;
            capture.clearFlags=CameraClearFlags.SolidColor;capture.backgroundColor=new Color(.05f,.05f,.05f);
            capture.transform.position=bounds.center+new Vector3(.3f,.9f,.5f).normalized*Mathf.Max(.1f,bounds.size.magnitude*2);
            capture.transform.LookAt(bounds.center);
            target=new RenderTexture(700,700,24);capture.targetTexture=target;Canvas.ForceUpdateCanvases();capture.Render();RenderTexture.active=target;
            image=new Texture2D(700,700,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,700,700),0,0);image.Apply();
            System.IO.Directory.CreateDirectory("docs/evidence/blood-sample-inventory");
            System.IO.File.WriteAllBytes("docs/evidence/blood-sample-inventory/"+label+"-container.png",image.EncodeToPNG());
        }
        finally
        {
            model.transform.position=position;RenderTexture.active=previous;
            if(capture!=null){capture.targetTexture=null;Destroy(capture.gameObject);}
            if(target!=null){target.Release();Destroy(target);}if(image!=null)Destroy(image);
        }
    }

    // Body and bottle take their final photo before measuring; knife and stain measure first.
    private IEnumerator DocumentToFinalPhoto(string id)
    {
        Photo(id);
        if (Def(id).requiresChalk)
        {
            var guide = EvidenceChalkGuide.FindActive(id);
            for (int p = 0; guide != null && p < guide.PointCount && !guide.IsComplete; p++)
            {
                ChalkMarkTool.Instance.TryTraceAt(guide.GetPointPosition(p));
            }
        }
        if (Def(id).examinedInPlace)
        {
            Equip(ToolType.LetterTentMarker);
            AimOrigin(letterTent, id);
            letterTent.TryPlaceLetterTent();
        }
        else
        {
            Equip(ToolType.EvidenceMarker);
            AimOrigin(tents, id);
            typeof(EvidenceTentTool).GetMethod("PlaceTent", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(tents, null);
            nextTent++;
        }
        bool finalFirst = Def(id).requiresBodyRemoval || Def(id).measurementAfterFinalPhoto;
        if (finalFirst) { Photo(id); yield return null; }
        Equip(ToolType.Measurement);
        MeasureSide(id, MeasurementTestUtil.Ref(id, 1));
        MeasureSide(id, MeasurementTestUtil.Ref(id, 2));
        MeasurementTestUtil.RecordAvailablePairsFor(id); // evidence distances to the items already discovered
        Check("["+id+"] measured item placed in Scene Sketch",MeasurementTestUtil.CompleteSketchFor(id));
        if (Def(id).requiresBodyRemoval)
        {
            esm.RecordReportLinesSelected(id, new[] { "body-a-location", "body-a-position", "body-a-wound-sharp-object" });
            esm.MarkLogged(id, ToolType.Recorder);
        }
        if (!finalFirst) Photo(id);
        yield return null;
        Check($"[{id}] documentation follows the item-specific photograph and measurement order",
            esm.GetRecord(id).status == (Def(id).requiresBodyRemoval ? EvidenceStatus.Logged : EvidenceStatus.PhotographedFinal)
            && MeasurementStateManager.Instance.IsMeasurementComplete("MEAS-" + id));
    }

    private IEnumerator FinishWithFlow(string id)
    {
        flow.SetEvidenceIdsForTest(new[] { id }, 20 + nextTent++, true, resume: true);
        string flowReport = flow.RunFullFlow();
        Check($"[{id}] rest of the procedure through the real tools", flowReport.Contains("=== PASS"));
        if (!flowReport.Contains("=== PASS")) report.AppendLine(flowReport);
        flow.SetEvidenceIdsForTest(new[] { "EVD-014", "EVD-015", "EVD-018" });
        yield return DismissCheckpoints();
    }

    // --- Real tools -------------------------------------------------------

    private EvidenceDefinition Def(string id) => esm.GetRecord(id).definition;

    private void Equip(ToolType role)
    {
        if (PlayerToolRegistry.VirtuallyEquippedRole != role)
        {
            HeldItemCoordinator.Instance.ToggleEquipTool(role, rig.rightHandAnchor);
        }
    }

    private void Photo(string id)
    {
        TraceMarkerTestUtil.MarkTraceIfRequired(id); // the knife's second tent (blood on the blade) before its final photo
        Equip(ToolType.Photographer);
        var head = Camera.main.transform;
        Vector3 target = EvidenceProp.FindById(id).transform.position;
        Vector3 eye = target + new Vector3(0.5f, 0.9f, 0.1f);
        head.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye));
        var bf = BindingFlags.NonPublic | BindingFlags.Instance;
        typeof(PhotographTool).GetMethod("UpdateAimTarget", bf).Invoke(camera, null);
        PhotoScaleTestUtil.PrepareForFinalPhoto(camera, id);
        typeof(PhotographTool).GetMethod("TakePhoto", bf).Invoke(camera, null);
        PhotoScaleTestUtil.FinishPhoto(camera);
    }

    private static void AimOrigin(Component tool, string id)
    {
        var origin = tool.GetType().GetField("placementOrigin", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(tool) as Transform;
        Vector3 target = EvidenceProp.FindById(id).transform.position;
        Vector3 from = target + Vector3.up * 0.6f;
        var t = tool.transform;
        t.rotation = Quaternion.FromToRotation(origin.forward, Vector3.down) * t.rotation;
        t.position += from - origin.position;
    }

    private void MeasureSide(string id, string referenceId)
    {
        MeasurementGeometry.TryGetEvidenceAnchor(id, out var anchor);
        if (tape.CurrentPhase == MeasurementTool.Phase.SelectTarget || tape.SelectedRequirementId != "MEAS-" + id)
        {
            PlaceTip(anchor + Vector3.up + Vector3.forward * 0.05f, anchor);
            tape.HandleTrigger();
        }
        // Inside this item's own reference zone (per-evidence fixed references).
        Vector3 hook = MeasurementTestUtil.HookPoint(referenceId);
        PlaceTip(hook, hook + Vector3.back);
        tape.HandleTrigger();
        Vector3 end = new Vector3(anchor.x + 0.02f, 1.0f, anchor.z);
        PlaceTip(end, end + Vector3.down);
        tape.HandleTrigger();
    }

    private void PlaceTip(Vector3 tipWorld, Vector3 lookAt)
    {
        var t = tape.transform;
        t.rotation = Quaternion.LookRotation(lookAt - tipWorld);
        t.position += tipWorld - tape.TapeTip.position;
    }

    private static void CloseRecorderIfOpen()
    {
        if (RecorderReportUI.Instance != null && RecorderReportUI.Instance.IsShowing)
        {
            RecorderReportUI.Instance.Hide();
        }
    }

    private IEnumerator DismissCheckpoints()
    {
        for (int i = 0; i < 5; i++)
        {
            var checkpoint = FindFirstObjectByType<HypothesisCheckpointUI>();
            if (checkpoint != null && checkpoint.IsShowing)
            {
                checkpoint.AnswerForTest();
            }
            yield return null;
        }
    }

    // --- Log -------------------------------------------------------------------

    private static string ReadSessionLog()
    {
        SessionLogger.Instance?.Flush();
        string path = SessionLogger.Instance != null ? SessionLogger.Instance.ActiveLogFilePath : null;
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return string.Empty;
        using var fs = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite);
        using var reader = new System.IO.StreamReader(fs);
        return reader.ReadToEnd();
    }

    private static int Count(string token)
    {
        string text = ReadSessionLog();
        int n = 0, i = 0;
        while ((i = text.IndexOf(token, i, System.StringComparison.Ordinal)) >= 0)
        {
            n++;
            i += token.Length;
        }
        return n;
    }

    private static int CountStatus(string evidenceId, string status)
    {
        int n = 0;
        foreach (var line in ReadSessionLog().Split('\n'))
        {
            if (line.Contains("\"eventType\":\"EvidenceStatusChanged\"") && line.Contains("\"targetId\":\"" + evidenceId + "\"")
                && line.Contains("\"value\":\"" + status + "\""))
            {
                n++;
            }
        }
        return n;
    }

    private void Check(string label, bool passed)
    {
        report.AppendLine($"  {(passed ? "PASS" : "FAIL")}: {label}");
        if (!passed) { failures.Add(label); Debug.LogError("[BiologicalSamplingTest] "+label); }
    }
}
