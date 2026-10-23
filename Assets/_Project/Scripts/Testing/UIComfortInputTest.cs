using System.Collections;
using System.Linq;
using System.Text;
using Oculus.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>Geometry fixtures and actual raw XR presses through the rig's Meta jump event.</summary>
public sealed class UIComfortInputTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    private readonly StringBuilder report = new("=== UIComfortInputTest ===\n");
    private int failures;
    private InputDevice device;
    private InputAction a;
    private ActiveStateUnityEventWrapper jumpSource;
    private UIConfirmJumpRelay jump;
    private GameObject fixture;
    private Camera originalCamera;
    private GameObject testCamera;
    public void RunAllTests() => StartCoroutine(Run());
    private void Check(string name, bool ok) { if (!ok) failures++; report.AppendLine((ok ? "PASS: " : "FAIL: ") + name); }
    private IEnumerator Press(bool held)
    {
        InputSystem.QueueDeltaStateEvent((ButtonControl)device["primaryButton"], held ? 1f : 0f);
        yield return null; yield return null;
    }
    private static void Clear()
    { STCSNotificationUI.Instance?.HideImmediately(); NotificationManager.Instance?.UI.ClearTooltip(); NotificationManager.Instance?.UI.HidePrompt(); }
    private HudFollowCamera Panel(string name, Vector2 size, float distance, int priority, bool companion = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas)); go.transform.SetParent(fixture.transform);
        go.transform.localScale = Vector3.one;
        ((RectTransform)go.transform).sizeDelta = size;
        go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var follow = go.AddComponent<HudFollowCamera>(); follow.Distance = distance;
        HudFollowCamera.ConfigureShared(go, null, priority, companion);
        follow.MinimumDistance = .82f; follow.SmoothTime = 0;
        return follow;
    }
    private IEnumerator Run()
    {
        AutomatedTestSandbox.Activate(nameof(UIComfortInputTest));
        yield return null;
        var preSession = FindAnyObjectByType<PreSessionFlowController>();
        if (preSession != null)
        {
            preSession.enabled = false;
            var token = typeof(PreSessionFlowController).GetField("uiGateToken", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(preSession);
            if (token != null) PlayerUIGate.Exit(token);
        }
        FindAnyObjectByType<StudentIdentificationPanel>(FindObjectsInactive.Include)?.Hide();
        // Disable only unsolicited presentation producers; all assertions use normal runtime owners.
        if (ReasoningCheckpointManager.Instance != null) ReasoningCheckpointManager.Instance.enabled = false;
        if (HypothesisCheckpointManager.Instance != null) HypothesisCheckpointManager.Instance.enabled = false;
        if (ProceduralTrainingGuidance.Instance != null) ProceduralTrainingGuidance.Instance.enabled = false;
        if (NarrativeEventManager.Instance != null) { NarrativeEventManager.Instance.enabled = false; NarrativeEventManager.Instance.StopAllCoroutines(); }
        // Delayed photography instructions must not replace the timed message in the geometry fixture.
        var scenePhotography = FindAnyObjectByType<ScenePhotographyManager>();
        if (scenePhotography != null)
        { scenePhotography.CancelInvoke("ActivateFirstPoint"); scenePhotography.enabled = false; scenePhotography.StopAllCoroutines(); }
        FindAnyObjectByType<HypothesisCheckpointUI>()?.AnswerForTest(0,0);
        Clear();
        jump = FindObjectsByType<UIConfirmJumpRelay>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
        Check("Meta Jump event automatically routed, no manual scene wiring", jump != null);
        if (jump == null) { Finish(); yield break; }
        jumpSource = jump.GetComponent<ActiveStateUnityEventWrapper>();
        Check("Only the persistent Jump listener is disabled", Enumerable.Range(0,jumpSource.WhenActivated.GetPersistentEventCount())
            .Any(i => jumpSource.WhenActivated.GetPersistentMethodName(i)=="Jump" && jumpSource.WhenActivated.GetPersistentListenerState(i)==UnityEngine.Events.UnityEventCallState.Off));
        fixture = new GameObject("UIComfortFixture");
        var high = new GameObject("ModalProbe").AddComponent<UIComfortInputProbe>(); high.transform.SetParent(fixture.transform); high.Initialize(600);
        var low = new GameObject("InformationProbe").AddComponent<UIComfortInputProbe>(); low.transform.SetParent(fixture.transform); low.Initialize(300);
        // Desktop Play has no active tracked-controller branch. Exercise the same Meta
        // broadcaster/wrapper with an injected IActiveState driven by the raw XR device.
        var jumpGo = new GameObject("MetaJumpFixture"); jumpGo.transform.SetParent(fixture.transform);
        var broadcaster = jumpGo.AddComponent<Oculus.Interaction.Locomotion.LocomotionActionsBroadcaster>();
        jumpSource = jumpGo.AddComponent<ActiveStateUnityEventWrapper>();
        jumpSource.InjectActiveState(high);
        jumpSource.InjectOptionalWhenActivated(new UnityEngine.Events.UnityEvent());
        jumpSource.InjectOptionalWhenDeactivated(new UnityEngine.Events.UnityEvent());
#if UNITY_EDITOR
        UnityEditor.Events.UnityEventTools.AddPersistentListener(jumpSource.WhenActivated, broadcaster.Jump);
#endif
        UIConfirmJumpRelay.Install(jumpSource); jump = jumpGo.GetComponent<UIConfirmJumpRelay>();
        InputSystem.RegisterLayout("{\"name\":\"ViCSIComfortController\",\"extend\":\"XRController\",\"controls\":[{\"name\":\"primaryButton\",\"layout\":\"Button\",\"format\":\"FLT\"},{\"name\":\"secondaryButton\",\"layout\":\"Button\",\"format\":\"FLT\"},{\"name\":\"triggerPressed\",\"layout\":\"Button\",\"format\":\"FLT\"}]}");
        device = InputSystem.AddDevice("ViCSIComfortController"); InputSystem.SetDeviceUsage(device, CommonUsages.RightHand);
        a = new InputAction("TestMetaA", InputActionType.Button, "<XRController>{RightHand}/primaryButton"); a.Enable();
        yield return null;
        Check("Fixture starts without modal locks (owners="+string.Join(",",PlayerUIGate.LockOwnerNames)+", jump="+jump.isActiveAndEnabled+")", !PlayerUIGate.IsBlocked && jump.isActiveAndEnabled);
        int jumps = jump.ForwardedCount;
        yield return Press(true);
        Check("Unowned A forwards normal gameplay jump", jump.ForwardedCount == jumps + 1);
        yield return Press(false);
        NotificationManager.Notify("Noninteractive test notification", DialogueKind.Confirmation);
        jumps = jump.ForwardedCount; yield return Press(true);
        Check("Noninteractive notification does not own A or block jump", !PlayerUIGate.IsUIConsumingConfirm && jump.ForwardedCount == jumps + 1);
        yield return Press(false); Clear();
        high.Available = low.Available = true;
        PlayerUIGate.Enter(high); PlayerUIGate.Enter(low); yield return null;
        jumps = jump.ForwardedCount; yield return Press(true);
        Check("Highest-priority UI alone receives actual A", high.Count == 1 && low.Count == 0);
        Check("Same A never reaches gameplay jump", jump.ForwardedCount == jumps && jump.SuppressedCount > 0);
        yield return Press(false);
        high.OnConfirm = () => { high.Available = false; PlayerUIGate.Exit(high); low.Available = false; PlayerUIGate.Exit(low); };
        yield return Press(true);
        Check("Confirm closes UI without same-frame jump", high.Count == 2 && !PlayerUIGate.IsBlocked && jump.ForwardedCount == jumps);
        jumpSource.WhenActivated.Invoke(); yield return null;
        Check("Held A after close stays consumed", jump.ForwardedCount == jumps && PlayerUIGate.SuppressGameplayJump);
        yield return Press(false);
        Check("Release clears ownership without synthesizing jump", jump.ForwardedCount == jumps && !PlayerUIGate.IsUIConsumingConfirm);
        yield return Press(true); Check("Next fresh A jumps normally", jump.ForwardedCount == jumps + 1); yield return Press(false);
        high.OnConfirm = null;
        low.Available = true;
        low.OnConfirm = () => { low.Available = false; high.Available = true; PlayerUIGate.Enter(high); };
        jumps = jump.ForwardedCount; yield return Press(true);
        Check("A opens modal without also selecting it or jumping", low.Count == 1 && high.Count == 2 && jump.ForwardedCount == jumps);
        yield return Press(false); yield return Press(true);
        Check("Modal accepts fresh press after activation", high.Count == 3 && jump.ForwardedCount == jumps);
        yield return Press(false); high.Available = false; PlayerUIGate.Exit(high);
        // No tracked HMD is present in the Editor runner: give reading UI a real eye
        // pose in empty space instead of testing input with its canvas below the floor.
        originalCamera = Camera.main; originalCamera.tag = "Untagged";
        var cameraGo = new GameObject("UIComfortCamera", typeof(Camera)); testCamera = cameraGo;
        cameraGo.tag = "MainCamera"; cameraGo.transform.position = new Vector3(1000,1000,1000);
        var cam = cameraGo.GetComponent<Camera>();
        var notification = NotificationManager.Instance.UI;
        var noticeFollow = notification.GetComponent<HudFollowCamera>();
        var noticeCanvas = notification.GetComponent<Canvas>();
        typeof(HudFollowCamera).GetField("targetCamera", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(noticeFollow, cam);
        var state = EvidenceStateManager.Instance;
        state.MarkFound("EVD-018", ToolType.None);
        Check("Real procedure completion reaches the converted notification channel", state.MarkPhotographedRaw("EVD-018", ToolType.Photographer) == TransitionResult.Applied
            && !string.IsNullOrEmpty(notification.CurrentTooltip) && noticeFollow.SharedLayout && noticeFollow.ObstacleAwarePlacement);
        notification.ShowTooltip(notification.CurrentTooltip, 60);
        yield return new WaitForSecondsRealtime(.25f); yield return null;
        Check("Procedure feedback appears normally in open space with the lightweight profile", !noticeFollow.IsLayoutSuppressed && noticeCanvas.enabled
            && Vector3.Distance(notification.transform.position, cam.transform.position + new Vector3(0, -.18f, .90f)) < .01f
            && Mathf.Abs(noticeFollow.MinimumDistance - .60f) < .001f && Mathf.Abs(noticeFollow.WallClearance - .12f) < .001f
            && Mathf.Abs(notification.transform.lossyScale.x - .0011f) < .00001f);
        Check("Timed feedback has lower placement priority and no input locks", noticeFollow.PlacementPriority == PlayerUIGate.ProceduralFeedbackPriority
            && !PlayerUIGate.IsBlocked && !ToolInputLock.HasUIOwners && !PlayerUIGate.IsUIConsumingConfirm);
        jumps = jump.ForwardedCount; yield return Press(true);
        Check("Visible wall-aware procedure feedback still permits normal A jump", jump.ForwardedCount == jumps + 1 && !PlayerUIGate.IsUIConsumingConfirm);
        yield return Press(false);
        var noticeWall = GameObject.CreatePrimitive(PrimitiveType.Cube); noticeWall.name = "NotificationWall"; noticeWall.transform.SetParent(fixture.transform);
        noticeWall.transform.position = cam.transform.position + new Vector3(0, 0, .90f); noticeWall.transform.localScale = new Vector3(10, 3, .08f);
        Physics.SyncTransforms();
        state.MarkFound("EVD-014", ToolType.None); state.MarkPhotographedRaw("EVD-014", ToolType.Photographer);
        notification.ShowTooltip(notification.CurrentTooltip, 60); yield return null; yield return null;
        Check("Completion facing a wall stays visible on its player side", !noticeFollow.IsLayoutSuppressed && noticeCanvas.enabled
            && noticeFollow.PanelFits(cam.transform.position, notification.transform.position, notification.transform.rotation)
            && notification.transform.position.z < noticeWall.GetComponent<Collider>().bounds.min.z);
        noticeWall.transform.position = cam.transform.position + new Vector3(0, 0, .30f); noticeWall.transform.localScale = new Vector3(.35f, 3, .08f);
        Physics.SyncTransforms(); state.MarkFound("BODY-A", ToolType.None); state.MarkPhotographedRaw("BODY-A", ToolType.Photographer);
        notification.ShowTooltip(notification.CurrentTooltip, 60); yield return null; yield return null;
        int noticeSide = noticeFollow.FallbackSide;
        Check("Very close wall uses a readable, safe notification fallback", !noticeFollow.IsLayoutSuppressed && noticeSide != 0
            && Vector3.Distance(cam.transform.position, notification.transform.position) >= .599f
            && noticeFollow.PanelFits(cam.transform.position, notification.transform.position, notification.transform.rotation));
        bool stableNotice = true;
        for (int i=0;i<12;i++) { cameraGo.transform.position += new Vector3(i%2==0?.002f:-.002f,0,0); yield return null;
            stableNotice &= noticeFollow.FallbackSide == noticeSide && !noticeFollow.IsLayoutSuppressed
                && noticeFollow.PanelFits(cam.transform.position, notification.transform.position, notification.transform.rotation); }
        Check("Notification fallback remains safe and does not flip under head jitter", stableNotice);
        noticeWall.transform.position = cam.transform.position + new Vector3(0, 0, .30f); noticeWall.transform.localScale = new Vector3(10, 3, .08f);
        Physics.SyncTransforms(); yield return null; yield return null;
        Check("No readable slot withholds feedback instead of placing it through a wall", noticeFollow.IsLayoutSuppressed && !noticeCanvas.enabled);
        float hiddenRemaining = notification.TooltipRemaining; string hiddenText = notification.CurrentTooltip;
        yield return new WaitForSecondsRealtime(.2f);
        report.AppendLine("INFO: deferred timer before=" + hiddenRemaining + "; after=" + notification.TooltipRemaining
            + "; textUnchanged=" + (hiddenText == notification.CurrentTooltip) + "; suppressed=" + noticeFollow.IsLayoutSuppressed);
        Check("Spatially deferred feedback preserves remaining display time", Mathf.Abs(notification.TooltipRemaining-hiddenRemaining)<.001f);
        Destroy(noticeWall); yield return null; yield return null;
        Check("Feedback resumes when a readable slot becomes available", !noticeFollow.IsLayoutSuppressed && noticeCanvas.enabled && notification.TooltipRemaining > 0);
        notification.ClearTooltip(); notification.ShowPrompt("Ambient placement fixture"); yield return null;
        Check("Ambient notices use the same solver at the lowest placement priority", noticeFollow.SharedLayout && noticeFollow.PlacementPriority == PlayerUIGate.AmbientPriority);
        notification.HidePrompt(); yield return new WaitForSecondsRealtime(.2f);
        // Real checkpoint raw selection and lock path.
        var ui = FindAnyObjectByType<HypothesisCheckpointUI>();
        var privateInstance = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var definition = ReasoningCheckpointManager.Instance.Definitions.checkpoints.First(d => !ReasoningCheckpointManager.Instance.IsAnswered(d.id));
        int submitted = 0;
        ui.ShowReasoning(definition, id => submitted++); yield return null;
        Check("Reasoning owns A and locks tools as well as its existing locomotion", PlayerUIGate.ConfirmOwnerToken == ui && ToolInputLock.HasUIOwners);
        var reasoningWall = GameObject.CreatePrimitive(PrimitiveType.Cube); reasoningWall.transform.SetParent(fixture.transform);
        reasoningWall.transform.position = cam.transform.position + new Vector3(0, 0, .65f);
        reasoningWall.transform.localScale = new Vector3(.4f, 3, .08f); Physics.SyncTransforms();
        yield return null; yield return null;
        var reasoningFollow = (GameObject)typeof(HypothesisCheckpointUI).GetField("panelRoot", privateInstance).GetValue(ui);
        var reasoningPlacement = reasoningFollow.GetComponent<HudFollowCamera>();
        Check("Real reasoning rectangle avoids wall", !reasoningPlacement.IsLayoutSuppressed
            && reasoningPlacement.PanelFits(cam.transform.position, reasoningFollow.transform.position, reasoningFollow.transform.rotation));
        notification.ShowTooltip("Procedure complete during reasoning", 1f); yield return null; yield return null;
        float modalRemaining = notification.TooltipRemaining;
        yield return new WaitForSecondsRealtime(.2f);
        Check("Reasoning defers feedback without overlapping the primary or consuming its timer", noticeFollow.IsLayoutSuppressed && !noticeCanvas.enabled
            && notification.IsPlacementDeferred && Mathf.Abs(notification.TooltipRemaining-modalRemaining)<.001f
            && !reasoningPlacement.IsLayoutSuppressed && PlayerUIGate.ConfirmOwnerToken == ui);
        notification.ClearTooltip();
        Destroy(reasoningWall); yield return null;
        var toolWheel = FindAnyObjectByType<ToolWheelController>();
        var utilityWheel = FindAnyObjectByType<UtilityMenuController>();
        typeof(ToolWheelController).GetMethod("OpenWheel",privateInstance).Invoke(toolWheel,null);
        typeof(UtilityMenuController).GetMethod("OpenMenu",privateInstance).Invoke(utilityWheel,null);
        Check("Reasoning modal prevents both radial wheels from opening", !toolWheel.IsCanvasActive && !utilityWheel.IsCanvasActive);
        yield return Press(true); Check("First reasoning A selects without locking answer", ui.IsShowing && submitted == 0);
        yield return Press(false); yield return Press(true);
        Check("Second fresh reasoning A confirms once", !ui.IsShowing && submitted == 1);
        yield return Press(false); Clear();
        var grabGate = FindObjectsByType<EvidenceGrabGate>().First();
        var setGrab = typeof(EvidenceGrabGate).GetMethod("SetGrabEnabled",privateInstance);
        bool originalGrab = (bool)typeof(EvidenceGrabGate).GetField("grabEnabled",privateInstance).GetValue(grabGate);
        setGrab.Invoke(grabGate,new object[]{true});
        ToolInputLock.Enter(high); yield return null;
        Check("Modal locks physical evidence grab interactables without changing evidence status", !grabGate.GetComponent<Oculus.Interaction.GrabInteractable>().enabled);
        InputSystem.QueueDeltaStateEvent((ButtonControl)device["triggerPressed"],1f); yield return null; yield return null;
        ToolInputLock.Exit(high);
        Check("Held UI trigger remains blocked after modal closes", ToolInputLock.HasUIOwners);
        InputSystem.QueueDeltaStateEvent((ButtonControl)device["triggerPressed"],0f); yield return null; yield return null; yield return null;
        Check("Releasing UI trigger restores normal tool and grab input", !ToolInputLock.HasUIOwners && grabGate.GetComponent<Oculus.Interaction.GrabInteractable>().enabled);
        setGrab.Invoke(grabGate,new object[]{originalGrab});
        // Geometry uses a separate main camera in empty space so scene props cannot bias results.
        var primary = Panel("Primary", new Vector2(.66f,.54f),1.2f,600); primary.LocalOffset = new Vector3(0,-.12f,0);
        yield return null; yield return null;
        Check("Open-space preferred distance and offset unchanged", Vector3.Distance(primary.transform.position,cam.transform.position+new Vector3(0,-.12f,1.2f))<.005f);
        var companion = Panel("Companion",new Vector2(.52f,.18f),1.05f,400,true);
        yield return null; yield return null;
        Vector3 relative = Quaternion.Inverse(primary.transform.rotation)*(companion.transform.position-primary.transform.position);
        Check("Simultaneous panels share rotation and measured nonoverlapping vertical slot", Quaternion.Angle(primary.transform.rotation,companion.transform.rotation)<.01f && Mathf.Abs(relative.x)<.001f && Mathf.Abs(relative.z)<.001f && relative.y>.36f);
        primary.Distance = .65f; primary.MinimumDistance = .55f; companion.MinimumDistance = .95f;
        // Force a replan as changing serialized profile values in the Inspector would.
        cameraGo.transform.position += Vector3.right * .1f; yield return null; yield return null;
        Check("Group respects secondary minimum readability without an unnecessary side fallback in open space",
            Vector3.Distance(cam.transform.position,companion.transform.position)>=.949f && primary.FallbackSide==0);
        primary.Distance = 1.2f; primary.MinimumDistance = .82f; companion.MinimumDistance = .82f;
        cameraGo.transform.position -= Vector3.right * .1f; yield return null; yield return null;
        var lower = Panel("LowerInteractive",new Vector2(.6f,.4f),.95f,300);
        yield return null; yield return null;
        Check("Modal keeps primary placement; lower interactive canvas yields", lower.IsLayoutSuppressed && !primary.IsLayoutSuppressed && !companion.IsLayoutSuppressed);
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name="ComfortWall";wall.transform.SetParent(fixture.transform);
        wall.transform.position=cam.transform.position+new Vector3(0,0,.65f);wall.transform.localScale=new Vector3(.4f,3,.08f);
        Physics.SyncTransforms(); yield return null; yield return null;
        Check("Wall fixture obstructs the preferred primary pose", !primary.PanelFits(cam.transform.position,cam.transform.position+new Vector3(0,-.12f,1.2f),Quaternion.identity));
        Check("Viewer inside solid geometry remains rejected", !primary.PanelFits(wall.transform.position,
            cam.transform.position + new Vector3(1.5f, 0, 1.2f), Quaternion.identity));
        // Same non-convex collision shape as the new house. This overlaps the broad
        // sweep at its origin while leaving the actual eye and left-side view clear.
        var primitiveWall = wall.GetComponent<BoxCollider>(); primitiveWall.enabled = false;
        var meshWall = wall.AddComponent<MeshCollider>(); meshWall.sharedMesh = wall.GetComponent<MeshFilter>().sharedMesh; meshWall.convex = false;
        wall.transform.position = cam.transform.position + new Vector3(.4f,0,.05f); Physics.SyncTransforms();
        int unsupportedMeshErrors = 0;
        Application.LogCallback meshError = (message, trace, type) => { if(message.Contains("ClosestPoint")) unsupportedMeshErrors++; };
        Application.logMessageReceived += meshError;
        bool meshBlocks, meshSideClear;
        try
        {
            meshBlocks = !primary.PanelFits(cam.transform.position,cam.transform.position+new Vector3(1.2f,0,.05f),Quaternion.identity);
            meshSideClear = primary.PanelFits(cam.transform.position,cam.transform.position+new Vector3(-1.4f,0,.05f),Quaternion.identity);
        }
        finally { Application.logMessageReceived -= meshError; }
        Check("Non-convex house wall blocks a panel through it", meshBlocks);
        Check("Non-convex wall permits a clear side without unsupported ClosestPoint calls", meshSideClear && unsupportedMeshErrors == 0);
        meshWall.enabled = false; Destroy(meshWall); primitiveWall.enabled = true;
        wall.transform.position = cam.transform.position + new Vector3(0,0,.65f); Physics.SyncTransforms();
        yield return null; yield return null;
        int side=primary.FallbackSide;
        Check("Wall fallback clears primary rectangle", !primary.IsLayoutSuppressed && primary.PanelFits(cam.transform.position,primary.transform.position,primary.transform.rotation));
        Check("Secondary also clears wall with same fallback and rotation", !companion.IsLayoutSuppressed && companion.FallbackSide==side && companion.PanelFits(cam.transform.position,companion.transform.position,companion.transform.rotation));
        Check("Fallback chose a stable side", side!=0);
        Vector3 slot=companion.transform.position-primary.transform.position;
        for(int i=0;i<12;i++){ cameraGo.transform.position += new Vector3(i%2==0?.004f:-.004f,0,0);yield return null; }
        Check("Head jitter does not flip side or reorder panels", primary.FallbackSide==side && companion.FallbackSide==side && Vector3.Distance(slot,companion.transform.position-primary.transform.position)<.01f);
        // A wall touching only the companion must still influence the whole layout.
        wall.transform.position=cam.transform.position+new Vector3(0,.3f,1.2f);wall.transform.localScale=new Vector3(10,.16f,.1f);Physics.SyncTransforms();
        yield return null;yield return null;
        Check("Secondary-only wall is detected", companion.IsLayoutSuppressed || companion.PanelFits(cam.transform.position,companion.transform.position,companion.transform.rotation));
        Check("Primary remains wall-safe while group adjusts", !primary.IsLayoutSuppressed && primary.PanelFits(cam.transform.position,primary.transform.position,primary.transform.rotation));
        Check("Group actually moves or defers secondary for secondary-only obstruction", companion.IsLayoutSuppressed || primary.transform.position.z < cam.transform.position.z + 1.1f);
        Destroy(wall); primary.gameObject.SetActive(false);companion.gameObject.SetActive(false);lower.gameObject.SetActive(false);
        // Real dialogue uses the same geometry solver.
        var dialogue=STCSNotificationUI.Instance;
        typeof(HudFollowCamera).GetField("targetCamera",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(dialogue.GetComponent<HudFollowCamera>(),cam);
        var dialogueWall=GameObject.CreatePrimitive(PrimitiveType.Cube);dialogueWall.transform.SetParent(fixture.transform);
        dialogueWall.transform.position=cam.transform.position+new Vector3(0,0,.65f);dialogueWall.transform.localScale=new Vector3(.4f,3,.08f);Physics.SyncTransforms();
        GameDialogue.SayLine("statement-vendor-paul-3am","Witness","comfort-dialogue");yield return null;yield return null;
        var dialogueFollow=dialogue.GetComponent<HudFollowCamera>();
        Check("Real dialogue opts into shared wall avoidance", dialogueFollow.SharedLayout && dialogueFollow.ObstacleAwarePlacement);
        Check("Real dialogue rectangle avoids wall", !dialogueFollow.IsLayoutSuppressed && dialogueFollow.PanelFits(cam.transform.position,dialogue.transform.position,dialogue.transform.rotation));
        notification.ShowTooltip("Procedure complete during dialogue", 60); yield return null; yield return null;
        Vector3 noticeSlot = Quaternion.Inverse(dialogue.transform.rotation) * (notification.transform.position - dialogue.transform.position);
        var dialogueRect = (RectTransform)dialogue.transform; var noticeRect = (RectTransform)notification.transform;
        float dialogueBottom = dialogueRect.rect.yMin * dialogue.transform.lossyScale.y;
        float noticeTop = noticeSlot.y + noticeRect.rect.yMax * notification.transform.lossyScale.y;
        Check("Temporary feedback uses a measured nonoverlapping lower dialogue slot", !noticeFollow.IsLayoutSuppressed && !dialogueFollow.IsLayoutSuppressed
            && noticeTop <= dialogueBottom - .049f && Quaternion.Angle(notification.transform.rotation,dialogue.transform.rotation)<.01f
            && noticeFollow.FallbackSide == dialogueFollow.FallbackSide);
        Check("Both real dialogue and temporary rectangles remain wall-safe together", noticeFollow.PanelFits(cam.transform.position,notification.transform.position,notification.transform.rotation)
            && dialogueFollow.PanelFits(cam.transform.position,dialogue.transform.position,dialogue.transform.rotation));
        notification.ClearTooltip();
        Check("Dialogue progression still accepts existing skip", dialogue.IsShowing);
        yield return new WaitForSecondsRealtime(.4f); Check("Dialogue skip accepted after protection",dialogue.Skip());yield return null;yield return null;
        Clear(); Destroy(dialogueWall); yield return null;
        var readingWall = GameObject.CreatePrimitive(PrimitiveType.Cube); readingWall.transform.SetParent(fixture.transform);
        readingWall.transform.position = cam.transform.position + new Vector3(0, 0, .65f);
        readingWall.transform.localScale = new Vector3(.4f, 3, .08f); Physics.SyncTransforms();
        var notes = FindAnyObjectByType<EvidenceProcedureUI>(); notes.Show(); yield return null; yield return null;
        var notesRoot = (GameObject)typeof(EvidenceProcedureUI).GetField("panelRoot", privateInstance).GetValue(notes);
        var notesPlacement = notesRoot.GetComponent<HudFollowCamera>();
        report.AppendLine("INFO: notes showing=" + notes.IsShowing + "; placement="+(notesPlacement!=null)
            + "; owners=" + string.Join(",", PlayerUIGate.LockOwnerNames));
        Check("Real Evidence Notes rectangle uses its anchored wall-safe placement", notes.IsShowing && notesPlacement!=null
            && notesPlacement.AnchoredPlacement && !notesPlacement.IsLayoutSuppressed
            && notesPlacement.PanelFits(cam.transform.position, notesRoot.transform.position, notesRoot.transform.rotation));
        notes.Hide(); yield return null;
        var inventory = FindAnyObjectByType<EvidenceInventoryUI>(FindObjectsInactive.Include); inventory.Show();
        var inventoryRoot = (GameObject)typeof(EvidenceInventoryUI).GetField("panelRoot", privateInstance).GetValue(inventory);
        var inventoryPlacement = inventoryRoot.GetComponent<HudFollowCamera>();
        typeof(HudFollowCamera).GetField("targetCamera", privateInstance).SetValue(inventoryPlacement, cam);
        yield return null; yield return null;
        Check("Real inventory rectangle uses shared wall-safe placement", inventoryRoot.activeSelf && inventoryPlacement.SharedLayout
            && !inventoryPlacement.IsLayoutSuppressed && inventoryPlacement.PanelFits(cam.transform.position, inventoryRoot.transform.position, inventoryRoot.transform.rotation));
        inventory.Hide(); Destroy(readingWall); yield return null;
        var anchored=Panel("AnchoredNotes",new Vector2(.5f,.4f),1.35f,300);
        WorldPanelPlacement.PlaceInFront(anchored.transform);yield return null;yield return null;
        Vector3 anchor=anchored.transform.position;cameraGo.transform.rotation=Quaternion.Euler(0,25,0);yield return null;yield return null;
        Check("World-anchored reading panel keeps its pose when head turns",anchored.AnchoredPlacement && Vector3.Distance(anchor,anchored.transform.position)<.001f);
        Finish();
    }
    private void Finish()
    {
        report.AppendLine(failures==0?"=== PASS ===":"=== FAIL: "+failures+" FAILURE(S) ===");
        Report=report.ToString();IsDone=true;Debug.Log(Report);Cleanup();
    }
    private void OnDisable() => Cleanup();
    private void Cleanup()
    {
        a?.Dispose();a=null;
        if(device!=null){InputSystem.RemoveDevice(device);device=null;InputSystem.RemoveLayout("ViCSIComfortController");}
        if(originalCamera!=null) originalCamera.tag="MainCamera";
        if(fixture!=null)Destroy(fixture);
        if(testCamera!=null)Destroy(testCamera);
    }
}
