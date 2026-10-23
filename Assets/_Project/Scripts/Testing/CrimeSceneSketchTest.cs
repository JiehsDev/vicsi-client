// Assets/_Project/Scripts/Testing/CrimeSceneSketchTest.cs
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using System.Reflection;

/// <summary>
/// Crime Scene Sketch foundation: unlock rule, evidence and measurement drag/drop through
/// the real CrimeSceneSketchUI (retry / snap), identity-based measurement validation with
/// duplicate numbers, incremental construction across close/reopen, submission gating,
/// read-only after submit, logging, and reset. Fresh play session.
/// </summary>
public class CrimeSceneSketchTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }

    private readonly List<string> failures = new();
    private readonly StringBuilder report = new();

    private const string Knife = "EVD-014";
    private const string Spatter = "EVD-015";
    private const string Bottle = "EVD-018";
    private const string Body = "BODY-A";

    public void RunAllTests()
    {
        IsDone = false;
        StartCoroutine(Run());
    }

    private void Check(string label, bool ok)
    {
        report.AppendLine((ok ? "  PASS: " : "  FAIL: ") + label);
        if (!ok) failures.Add(label);
    }

    private static int Count(SessionEventType type)
    {
        var logger = SessionLogger.Instance;
        return logger != null && logger.EventCounts.TryGetValue(type, out int n) ? n : 0;
    }

    private IEnumerator Run()
    {
        report.AppendLine("=== CrimeSceneSketchTest ===");
        // This suite exercises sketch ownership, not automatic reasoning prompts
        // made eligible by the body's required final photograph.
        if (ReasoningCheckpointManager.Instance != null) ReasoningCheckpointManager.Instance.enabled = false;
        var sketch = CrimeSceneSketchManager.Instance;
        var ui = CrimeSceneSketchUI.Instance;
        var esm = EvidenceStateManager.Instance;
        var msm = MeasurementStateManager.Instance;
        if (sketch == null || ui == null || esm == null || msm == null || sketch.Definition == null)
        {
            Report = "FAIL: sketch manager/UI/definition or evidence/measurement managers missing.";
            IsDone = true;
            yield break;
        }
        var def = sketch.Definition;
        var template = ui.Template;

        // --- Authoring ---
        Check("[authoring] template adopted by the UI", template != null && template.definition == def);
        var problems = def.Validate(template, msm.RequirementSet);
        Check($"[authoring] definition, template and measurement set agree ({problems.Count} problems{(problems.Count > 0 ? ": " + problems[0] : "")})", problems.Count == 0);
        Check("[authoring] 4 required evidence elements, 8 required measurement slots", def.evidenceElements.Count == 4 && def.measurementSlots.Count == 8
            && def.evidenceElements.TrueForAll(e => e.required) && def.measurementSlots.TrueForAll(s => s.required));
        bool anchorsInvisible = true;
        foreach (var a in template.Anchors) anchorsInvisible &= a.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).Length == 0;
        Check("[leak] evidence anchors have no visuals at all", anchorsInvisible);
        Check("[leak] north arrow present (orientation requirement)", sketch.OrientationPresent);
        Check("[template] sheet is not left floating in the scene before opening", !template.gameObject.activeInHierarchy);

        // --- Initial state ---
        Check($"[initial] state NotStarted ({sketch.State})", sketch.State == SketchState.NotStarted);
        Check("[initial] no evidence elements available", sketch.GetAvailableElements().Count == 0);
        Check("[initial] no measurement cards", sketch.GetAvailableMeasurementCards().Count == 0);
        Check("[initial] cannot submit", !sketch.Submit() && !sketch.IsSubmitted);
        Check("[initial] an undiscovered item cannot be placed (nothing logged)",
            sketch.PlaceEvidence(Knife, "SketchAnchor_Knife") == SketchPlacementResult.NotAvailable && sketch.IncorrectAttempts.Count == 0);

        // --- Evidence discovery (leak rule: Found by proximity is not enough) ---
        Check("[setup] scene photography route completed", ScenePhotographyTestUtil.CompleteRoute());
        foreach (var id in new[] { Knife, Spatter, Bottle, Body }) esm.MarkFound(id, ToolType.None);
        yield return null;
        Check("[unlock] Found alone unlocks nothing (proximity would reveal evidence)", sketch.GetAvailableElements().Count == 0);
        esm.MarkPhotographedRaw(Knife, ToolType.Photographer);
        var available = sketch.GetAvailableElements();
        Check($"[unlock] knife photographed -> only the knife is available ({available.Count})", available.Count == 1 && available[0].evidenceId == Knife);

        yield return VerifySketchWheel();

        // --- Open the board ---
        int opened = Count(SessionEventType.SketchOpened);
        yield return DismissCheckpoints();
        ui.Show();
        yield return null;
        Check("[open] board shows", ui.IsShowing && template.gameObject.activeInHierarchy);
        Canvas.ForceUpdateCanvases();
        bool planMeshesVisible = true;
        foreach (string planName in new[] {"RearKitchenDetail","GroundFloorPlan","UpperFloorPlan"})
        {
            var plan = template.transform.Find(planName)?.GetComponent<SketchPlanGraphic>();
            var mesh = plan != null ? plan.canvasRenderer.GetMesh() : null;
            planMeshesVisible &= plan != null && plan.strokes.Count > 0 && mesh != null && mesh.vertexCount > 0;
        }
        Check("[map] kitchen detail and both floor plans render actual architectural linework", planMeshesVisible);
        Check("[open] SketchOpened logged", Count(SessionEventType.SketchOpened) == opened + 1);
        Check("[modal] tools and world interaction locked, UI gate held", ToolInputLock.IsLocked && PlayerUIGate.IsBlocked);
        var palette = ui.PaletteIds(false);
        Check($"[open] palette lists the knife only ({string.Join(",", palette)})", palette.Count == 1 && palette[0] == Knife);
        Check("[open] no symbols or measurement boxes on the sheet yet", ui.PlacedSymbolBoardPosition(Knife) == null && !ui.IsSlotShown("Knife_Ref1_Slot"));

        Check("[order] unmeasured evidence cannot be dragged or placed", !ui.BeginDragEvidence(Knife)
            && sketch.PlaceEvidence(Knife, "SketchAnchor_Knife") == SketchPlacementResult.NotAvailable);
        Check("[order] processing requires measurement and sketch", esm.GetActionBlockReason(Knife, ProcedureAction.BiologicalSampling) != null);
        MeasurementTestUtil.RecordExact(Knife, 1); MeasurementTestUtil.RecordExact(Knife, 2);
        Check("[order] completed measurements still require sketch placement", sketch.CanSketchEvidence(Knife)
            && CrimeSceneSketchManager.GetProcessingBlockReason(Knife) != null);
        ArrivalPPEStation.Instance.MarkUsedForSample(Knife);
        Check("[interlock] required glove follow-up blocks a measured sketch placement", ProcedureInterlock.BlocksUnrelatedActions
            && sketch.PlaceEvidence(Knife, "SketchAnchor_Knife") == SketchPlacementResult.NotAvailable && !ui.BeginDragEvidence(Knife));
        ArrivalPPEStation.Instance.PrepareDevelopmentGloves();
        STCSNotificationUI.Instance?.HideImmediately(); NotificationManager.Instance?.UI.ClearTooltip();
        ui.Rebuild();

        Vector2 knifeAnchor = template.ToBoard(template.FindAnchor("SketchAnchor_Knife").transform);
        Vector2 bottleAnchor = template.ToBoard(template.FindAnchor("SketchAnchor_Bottle").transform);

        // --- Wrong evidence placement: Knife -> Bottle anchor ---
        int wrongEvents = Count(SessionEventType.SketchPlacementIncorrect);
        Check("[wrong] knife picked up", ui.BeginDragEvidence(Knife));
        var result = ui.DropAtBoard(bottleAnchor);
        Check($"[wrong] knife on the bottle's anchor -> {result}", result == SketchPlacementResult.Incorrect && !sketch.IsEvidencePlaced(Knife));
        Check("[wrong] element stays held at the cursor without returning", ui.IsDragging && !ui.IsReturning && ui.DraggedCardPosition == ui.CursorPosition);
        var last = LastAttempt(sketch);
        Check($"[wrong] one SketchPlacementIncorrect logged with the attempted anchor ({last?.attemptedTargetId})",
            Count(SessionEventType.SketchPlacementIncorrect) == wrongEvents + 1 && last != null && !last.isMeasurement
            && last.evidenceId == Knife && last.attemptedTargetId == "SketchAnchor_Bottle" && last.order == 1);
        string wrongAnchorMessage = ui.LastMessage;
        yield return new WaitForSecondsRealtime(0.45f);
        Check("[wrong] knife remains held and unplaced after waiting", ui.IsDragging && !ui.IsReturning && sketch.State == SketchState.NotStarted);
        var movedCursor = ui.CursorPosition + new Vector2(12f, 8f);
        ui.MoveCursorTo(movedCursor);
        Check("[wrong] rejected element continues following the cursor", ui.DraggedCardPosition == ui.CursorPosition);

        // Open board: same response as another item's anchor (never hints where things are).
        ui.BeginDragEvidence(Knife);
        Check("[hint] no placement hint after only one wrong drop", ui.PlacementHint == null);
        int hintEvents = Count(SessionEventType.SketchPlacementHintShown);
        result = ui.DropAtBoard(new Vector2(440f, 440f));
        last = LastAttempt(sketch);
        Check($"[wrong] open board -> {result}, logged as 'none', same message as a wrong anchor",
            result == SketchPlacementResult.Incorrect && last != null && last.attemptedTargetId == "none" && ui.LastMessage == wrongAnchorMessage);
        yield return new WaitForSecondsRealtime(0.45f);
        Check("[hint] second wrong drop reveals a persistent hint without picking the card up again",ui.PlacementHint!=null&&ui.PlacementHint.gameObject.activeInHierarchy);
        Check("[hint] assistance is logged once at the second wrong drop",Count(SessionEventType.SketchPlacementHintShown)==hintEvents+1);
        CapturePlacementHint(ui);

        // --- Correct evidence placement ---
        int placedEvents = Count(SessionEventType.SketchEvidencePlaced);
        ui.BeginDragEvidence(Knife);
        // Two wrong drops of the knife so far: its area is now highlighted while it's dragged.
        var hint = ui.PlacementHint;
        Check($"[hint] after two wrong drops the knife's area is highlighted, centred on its anchor ({(hint != null ? hint.anchoredPosition.ToString() : "none")} vs {knifeAnchor})",
            hint != null && Vector2.Distance(hint.anchoredPosition, knifeAnchor) < 0.5f && hint.gameObject.activeInHierarchy);
        Check("[hint] SketchPlacementHintShown logged once", Count(SessionEventType.SketchPlacementHintShown) == hintEvents + 1 && sketch.WasHinted(Knife) && !sketch.WasHinted(Bottle));
        // Forgiveness: 1.5x the authored radius away still lands (drops are judged at 2x).
        float authored = template.FindAnchor("SketchAnchor_Knife").snapRadius;
        result = ui.DropAtBoard(knifeAnchor + new Vector2(authored * 1.5f, 0f));
        Check($"[hint] highlight removed once the card is dropped", ui.PlacementHint == null);
        yield return null;
        var symbol = ui.PlacedSymbolBoardPosition(Knife);
        Check($"[correct] knife 1.5x its authored radius from the anchor still placed (forgiving drop) -> {result}", result == SketchPlacementResult.Placed && sketch.IsEvidencePlaced(Knife));
        Check($"[correct] snapped exactly onto the anchor ({symbol} vs {knifeAnchor})", symbol.HasValue && Vector2.Distance(symbol.Value, knifeAnchor) < 0.5f);
        Check("[correct] SketchEvidencePlaced logged once", Count(SessionEventType.SketchEvidencePlaced) == placedEvents + 1);
        Check("[correct] placed element leaves the palette and is locked",
            !ui.PaletteIds(false).Contains(Knife) && !ui.BeginDragEvidence(Knife) && sketch.PlaceEvidence(Knife, "SketchAnchor_Knife") == SketchPlacementResult.AlreadyPlaced);
        Check($"[state] InProgress after the first placement ({sketch.State})", sketch.State == SketchState.InProgress);
        Check("[slots] the knife's two measurement boxes appear only now; others stay hidden",
            ui.IsSlotShown("Knife_Ref1_Slot") && ui.IsSlotShown("Knife_Ref2_Slot") && !ui.IsSlotShown("Spatter_Ref1_Slot") && !ui.IsSlotShown("Bottle_Ref1_Slot"));
        Check("[submit] Submit not available while incomplete", !ui.PressSubmit() && !ui.IsConfirmPending);

        var knifeReq = MeasurementTestUtil.RequirementFor(Knife);
        string knifeCard1 = SketchMeasurementCard.MakeCardId(knifeReq.requirementId, knifeReq.referenceAId);
        var record = msm.GetRecord(knifeReq.requirementId);
        Check("[measure] correct placement automatically attaches both recorded distances", sketch.IsSlotFilled("Knife_Ref1_Slot") && sketch.IsSlotFilled("Knife_Ref2_Slot"));
        Check("[measure] attached distance uses the recorded session value and tape precision",
            sketch.TryGetSlotCard("Knife_Ref1_Slot", out var attachedKnife) && Mathf.Approximately(attachedKnife.value, record.measuredA)
            && attachedKnife.DisplayValue == $"{record.measuredA:0.00} m");
        Check("[order] per-item sketch gate clears without submitting whole sketch", CrimeSceneSketchManager.GetProcessingBlockReason(Knife) == null && !sketch.IsSubmitted);
        int autoEvents = Count(SessionEventType.SketchMeasurementPlaced);
        ui.Rebuild(); ui.Rebuild();
        Check("[measure] redraw does not duplicate measurement events", Count(SessionEventType.SketchMeasurementPlaced) == autoEvents);

        // --- Incremental: close, discover spatter, reopen ---
        ui.Hide();
        yield return null;
        Check("[close] locks released on close", !ToolInputLock.IsLocked && !PlayerUIGate.IsBlocked);
        esm.MarkPhotographedRaw(Spatter, ToolType.Photographer);
        yield return DismissCheckpoints();
        ui.Show();
        yield return null;
        Check("[incremental] knife still placed after reopening", sketch.IsEvidencePlaced(Knife) && ui.PlacedSymbolBoardPosition(Knife).HasValue);
        Check("[incremental] knife measurement still filed", sketch.IsSlotFilled("Knife_Ref1_Slot"));
        Check($"[incremental] spatter now available ({string.Join(",", ui.PaletteIds(false))})", ui.PaletteIds(false).Count == 1 && ui.PaletteIds(false)[0] == Spatter);

        // --- Duplicate numeric values: identity, not the number ---
        esm.MarkPhotographedRaw(Body, ToolType.Photographer);
        PlaceCorrect(ui, template, def, Spatter);
        PlaceCorrect(ui, template, def, Body);
        var bodyReq = MeasurementTestUtil.RequirementFor(Body);
        var spatterReq = MeasurementTestUtil.RequirementFor(Spatter);
        MeasurementTestUtil.PrepareBodyForMeasurement();


        ui.Rebuild();
        string bodyCard = SketchMeasurementCard.MakeCardId(bodyReq.requirementId, bodyReq.referenceBId);
        string spatterCard = SketchMeasurementCard.MakeCardId(spatterReq.requirementId, spatterReq.referenceBId);
        sketch.TryGetCard(bodyCard, out var bc);
        sketch.TryGetCard(spatterCard, out var sc);
        Check("[identity] body and spatter retain different reference identities", bodyCard != spatterCard);
        Check("[duplicate] equal values attach by evidence and reference identity",
            sketch.TryGetSlotCard("Spatter_Ref2_Slot", out var attachedSpatter) && attachedSpatter.CardId == spatterCard
            && sketch.TryGetSlotCard("BodyA_Ref2_Slot", out var attachedBody) && attachedBody.CardId == bodyCard);

        // --- Complete everything -> ReadyForSubmission ---
        esm.MarkPhotographedRaw(Bottle, ToolType.Photographer);
        MeasurementTestUtil.PrepareBodyForMeasurement();
        Check("[setup] bottle final photograph precedes its measurement cards", esm.IsAtOrPastStatus(Bottle,EvidenceStatus.PhotographedFinal));
        MeasurementTestUtil.RecordExact(Knife, 2);
        MeasurementTestUtil.RecordExact(Spatter, 1);
        MeasurementTestUtil.RecordExact(Body, 1);
        MeasurementTestUtil.RecordExact(Bottle, 1);
        MeasurementTestUtil.RecordExact(Bottle, 2);
        Check("[measure] recorded bottle distances stay off the board before placement", !sketch.IsSlotFilled("Bottle_Ref1_Slot") && !sketch.IsSlotFilled("Bottle_Ref2_Slot"));
        PlaceCorrect(ui, template, def, Bottle);
        Check("[measure] correct bottle placement immediately attaches both prior recordings", sketch.IsSlotFilled("Bottle_Ref1_Slot") && sketch.IsSlotFilled("Bottle_Ref2_Slot"));
        ui.Rebuild();
        foreach (var slot in def.measurementSlots)
        {
            Check($"[complete] {slot.slotId} automatically attached from its matching recording",
                sketch.TryGetSlotCard(slot.slotId, out var attached) && attached.requirementId == slot.requirementId && attached.referenceId == slot.referenceId && attached.evidenceId == slot.evidenceId);
        }
        Check($"[ready] every required element and distance placed -> {sketch.State}", sketch.State == SketchState.ReadyForSubmission);
        Check("[legend] legend lists the four placed items in placement order", sketch.PlacedEvidenceInOrder.Count == 4 && sketch.PlacedEvidenceInOrder[0] == Knife);

        // --- Submission ---
        int submittedEvents = Count(SessionEventType.SketchSubmitted);
        Check("[submit] first press asks for confirmation", ui.PressSubmit() && ui.IsConfirmPending && !sketch.IsSubmitted);
        ui.CancelSubmit();
        Check("[submit] going back leaves it unsubmitted", !ui.IsConfirmPending && sketch.State == SketchState.ReadyForSubmission);
        ui.PressSubmit();
        Check("[submit] confirm submits", ui.ConfirmSubmit() && sketch.State == SketchState.Submitted);
        yield return null;
        Check("[submit] exactly one SketchSubmitted event", Count(SessionEventType.SketchSubmitted) == submittedEvents + 1);
        Check("[read-only] no resubmission", !sketch.Submit() && !ui.PressSubmit() && Count(SessionEventType.SketchSubmitted) == submittedEvents + 1);
        Check("[read-only] nothing can be moved or placed after submission",
            ui.IsReadOnly && sketch.PlaceEvidence(Knife, "SketchAnchor_Knife") == SketchPlacementResult.ReadOnly
            && sketch.PlaceMeasurement(knifeCard1, "Knife_Ref1_Slot") == SketchPlacementResult.ReadOnly && ui.PaletteIds(false).Count == 0);
        Check("[scope] sketch never touched the evidence lifecycle (knife still PhotographedRaw)", esm.GetRecord(Knife).status == EvidenceStatus.PhotographedRaw);
        ui.Hide();
        yield return null;
        Check("[close] locks released after submission", !ToolInputLock.IsLocked && !PlayerUIGate.IsBlocked);

        // --- Reset ---
        sketch.ResetSession();
        yield return DismissCheckpoints();
        ui.Show();
        yield return null;
        Check("[reset] no placements, no filed distances, no rejected attempts, not submitted",
            sketch.PlacedEvidenceInOrder.Count == 0 && !sketch.IsSlotFilled("Knife_Ref1_Slot") && sketch.IncorrectAttempts.Count == 0 && !sketch.IsSubmitted);
        Check($"[reset] state back to NotStarted ({sketch.State})", sketch.State == SketchState.NotStarted);
        Check("[reset] the sheet is clear again", ui.PlacedSymbolBoardPosition(Knife) == null && !ui.IsSlotShown("Knife_Ref1_Slot"));
        ui.Hide();

        report.AppendLine(failures.Count == 0 ? "=== PASS, 0 failures ===" : $"=== FAIL: {failures.Count} failure(s) ===");
        Report = report.ToString();
        IsDone = true;
    }

    private IEnumerator VerifySketchWheel()
    {
        var wheel = FindAnyObjectByType<ToolWheelController>();
        var ui = CrimeSceneSketchUI.Instance;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        InputSystem.RegisterLayout("{\"name\":\"SketchWheelController\",\"extend\":\"XRController\",\"controls\":[{\"name\":\"secondaryButton\",\"layout\":\"Button\",\"format\":\"FLT\"},{\"name\":\"thumbstick\",\"layout\":\"Stick\"}]}");
        var left = InputSystem.AddDevice("SketchWheelController"); InputSystem.SetDeviceUsage(left, CommonUsages.LeftHand);
        var right = InputSystem.AddDevice("SketchWheelController"); InputSystem.SetDeviceUsage(right, CommonUsages.RightHand);
        try
        {
            STCSNotificationUI.Instance?.HideImmediately(); NotificationManager.Instance?.UI.ClearTooltip();
            var priorRole = PlayerToolRegistry.CurrentRole;
            InputSystem.QueueDeltaStateEvent((ButtonControl)left["secondaryButton"], 1f);
            yield return null; yield return null;
            Check("[wheel] actual Y opens the existing tool wheel", wheel.IsCanvasActive);
            var views = (System.Collections.IList)typeof(ToolWheelController).GetField("segmentViews", flags).GetValue(wheel);
            int index = -1; object sketchView = null;
            for (int i = 0; i < views.Count; i++)
                if ((ToolType)views[i].GetType().GetField("Role").GetValue(views[i]) == ToolType.Sketcher) { index = i; sketchView = views[i]; break; }
            Check("[wheel] Scene Sketch is a functional entry with an authored clipboard icon", sketchView != null
                && (bool)sketchView.GetType().GetField("HasTool").GetValue(sketchView)
                && (string)sketchView.GetType().GetField("DisplayName").GetValue(sketchView) == "Scene Sketch"
                && sketchView.GetType().GetField("IconWhite").GetValue(sketchView) != null);
            if (index < 0) yield break;
            float angle = (index + .5f) * Mathf.PI * 2 / views.Count;
            InputSystem.QueueDeltaStateEvent((Vector2Control)right["thumbstick"], new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)));
            yield return null; yield return null;
            var subtitle = (TMPro.TMP_Text)typeof(ToolWheelController).GetField("centerSubtitle", flags).GetValue(wheel);
            subtitle.ForceMeshUpdate();
            Check("[wheel] sketch instructions fit the existing hub", subtitle.preferredHeight <= subtitle.rectTransform.rect.height);
            InputSystem.QueueDeltaStateEvent((ButtonControl)left["secondaryButton"], 0f);
            yield return null; yield return null;
            Check("[wheel] releasing Y opens the sketch and transfers modal ownership", ui.IsShowing && !wheel.IsCanvasActive && PlayerUIGate.IsBlocked && ToolInputLock.HasUIOwners);
            Check("[wheel] opening the sketch preserves the held tool", PlayerToolRegistry.CurrentRole == priorRole);
            ui.Hide();
            Check("[wheel] closing sketch releases UI and tool locks", !PlayerUIGate.IsBlocked && !ToolInputLock.HasUIOwners);
            ArrivalPPEStation.Instance.MarkUsedForSample(Knife);
            STCSNotificationUI.Instance?.HideImmediately(); NotificationManager.Instance?.UI.ClearTooltip();
            InputSystem.QueueDeltaStateEvent((ButtonControl)left["secondaryButton"], 1f);
            yield return null; yield return null;
            InputSystem.QueueDeltaStateEvent((ButtonControl)left["secondaryButton"], 0f);
            yield return null; yield return null;
            Check("[wheel] sketch entry cannot bypass required glove follow-up", !ui.IsShowing && !wheel.IsCanvasActive && !PlayerUIGate.IsBlocked);
            ArrivalPPEStation.Instance.PrepareDevelopmentGloves();
            STCSNotificationUI.Instance?.HideImmediately(); NotificationManager.Instance?.UI.ClearTooltip();
        }
        finally
        {
            ui.Hide(); InputSystem.RemoveDevice(left); InputSystem.RemoveDevice(right);
        }
    }

    private static SketchIncorrectAttempt LastAttempt(CrimeSceneSketchManager sketch) =>
        sketch.IncorrectAttempts.Count > 0 ? sketch.IncorrectAttempts[sketch.IncorrectAttempts.Count - 1] : null;

    private void CapturePlacementHint(CrimeSceneSketchUI ui)
    {
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        var panel=(GameObject)typeof(CrimeSceneSketchUI).GetField("panelRoot",flags).GetValue(ui);
        var caption=(TMPro.TMP_Text)typeof(CrimeSceneSketchUI).GetField("hintLabel",flags).GetValue(ui);
        var transforms=panel.GetComponentsInChildren<Transform>(true);
        var layers=new int[transforms.Length];for(int i=0;i<layers.Length;i++){layers[i]=transforms[i].gameObject.layer;transforms[i].gameObject.layer=31;}
        var go=new GameObject("Sketch hint preview camera");var cam=go.AddComponent<Camera>();cam.enabled=false;
        var rt=RenderTexture.GetTemporary(1600,1100,24);var previous=RenderTexture.active;
        try
        {
            Canvas.ForceUpdateCanvases();caption.ForceMeshUpdate();
            Check("[hint] readable gold-ring instruction remains visible after the card returns",caption.gameObject.activeInHierarchy
                &&caption.text.Contains("gold ring")&&caption.color.a>.9f&&!caption.isTextOverflowing);
            var rect=(RectTransform)panel.transform;
            cam.transform.SetPositionAndRotation(panel.transform.position-panel.transform.forward*2,Quaternion.LookRotation(panel.transform.forward,panel.transform.up));
            cam.orthographic=true;cam.orthographicSize=rect.rect.height*rect.lossyScale.y*.6f;cam.nearClipPlane=.01f;cam.farClipPlane=5;
            cam.cullingMask=1<<31;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.07f,.08f,.1f);
            cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;
            var image=new Texture2D(1600,1100,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1100),0,0);image.Apply();
            var hint=ui.PlacementHint;var center=cam.WorldToViewportPoint(hint.position);
            var edge=cam.WorldToViewportPoint(hint.TransformPoint(Vector3.right*hint.rect.width*.5f));
            int radius=Mathf.CeilToInt(Mathf.Abs(edge.x-center.x)*1600)+12;
            int cx=Mathf.RoundToInt(center.x*1600),cy=Mathf.RoundToInt(center.y*1100),goldPixels=0;
            for(int y=Mathf.Max(0,cy-radius);y<Mathf.Min(1100,cy+radius);y++)
                for(int x=Mathf.Max(0,cx-radius);x<Mathf.Min(1600,cx+radius);x++)
                {var c=image.GetPixel(x,y);if(c.r>.78f&&c.g>.5f&&c.b<.74f)goldPixels++;}
            Check("[hint] rendered board contains visible gold pixels at the correct target ("+goldPixels+")",goldPixels>30);
            string dir="docs/evidence/playtest-bugfixes/sketch";System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllBytes(dir+"/placement-hint.png",image.EncodeToPNG());Destroy(image);
        }
        finally
        {
            for(int i=0;i<layers.Length;i++)transforms[i].gameObject.layer=layers[i];
            cam.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);Destroy(go);
        }
    }

    // Hypothesis checkpoints are forced screens that can open mid-run and hold the UI gate;
    // answer them (same as EvidenceProcedureMapTest) so the board can open.
    private static IEnumerator DismissCheckpoints()
    {
        yield return null;
        for (int i = 0; i < 5; i++)
        {
            var checkpoint = Object.FindFirstObjectByType<HypothesisCheckpointUI>();
            if (checkpoint == null || !checkpoint.IsShowing)
            {
                yield break;
            }
            checkpoint.AnswerForTest(0, 0);
            yield return null;
        }
    }

    private void PlaceCorrect(CrimeSceneSketchUI ui, CrimeSceneSketchTemplate template, CrimeSceneSketchDefinition def, string evidenceId)
    {
        var anchor = template.FindAnchor(def.FindElement(evidenceId).correctAnchorId);
        MeasurementTestUtil.CompleteDiscoveredMeasurements();
        ui.Rebuild(); // pick up anything unlocked since the last refresh
        bool picked = ui.BeginDragEvidence(evidenceId);
        var r = ui.DropAtBoard(template.ToBoard(anchor.transform));
        Check($"[place] {evidenceId} placed on its anchor -> {r}", picked && r == SketchPlacementResult.Placed);
    }
}
