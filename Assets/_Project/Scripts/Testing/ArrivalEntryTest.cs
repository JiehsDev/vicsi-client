using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Saved arrival layout, guidance and briefing/PPE entry boundary checks.</summary>
public sealed class ArrivalEntryTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    readonly StringBuilder report = new("=== ArrivalEntryTest ===\n");
    int failures;
    void Check(string name, bool ok) { report.AppendLine((ok ? "PASS: " : "FAIL: ") + name); if (!ok) failures++; }
    public void RunAllTests() => StartCoroutine(Run());
    IEnumerator Run()
    {
        yield return null;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var pre = FindAnyObjectByType<PreSessionFlowController>();
        if (pre != null) { pre.enabled = false; PlayerUIGate.Exit(typeof(PreSessionFlowController).GetField("uiGateToken", flags).GetValue(pre)); }
        FindAnyObjectByType<StudentIdentificationPanel>(FindObjectsInactive.Include)?.Hide();
        ReasoningCheckpointManager.Instance.enabled = false; HypothesisCheckpointManager.Instance.enabled = false;
        ProceduralTrainingGuidance.Instance.enabled = false;
        if (NarrativeEventManager.Instance != null) { NarrativeEventManager.Instance.enabled = false; NarrativeEventManager.Instance.StopAllCoroutines(); }
        STCSNotificationUI.Instance?.HideImmediately(); NotificationManager.Instance?.UI.ClearTooltip();
        var cordon = FindAnyObjectByType<SceneCordon>(); var responder = SceneTurnover.Controller;
        var actor = responder.GetComponent<NPCPresentation>(); var guide = responder.GetComponent<NPCApproachGuide>();
        var photos = ScenePhotographyManager.Instance;
        var rig = GameObject.Find("[BuildingBlock] Camera Rig").transform;
        var left = cordon.transform.Find("EntrancePost_L"); var right = cordon.transform.Find("EntrancePost_R");
        var entry = (left.position + right.position) * .5f;
        ValidateCordonBoundary(cordon, entry);
        var inward = Vector3.Cross((right.position-left.position).normalized, Vector3.up);
        Check("Player starts outside the visible entrance with full capsule clearance", Vector3.Dot(rig.position-entry,inward) < -.5f);
        Check("Authored arrival is outside both warning areas", !cordon.RequiresBriefingAt(rig.position+Vector3.up*1.6f));
        Check("Opening objective uses the requested briefing wording", ObjectiveStateManager.Instance.AllRecords.Any(o => o.state == ObjectiveState.Active && o.definition.objectiveId == "obj-talk-first-responder" && o.definition.title == "Receive the First Responder's briefing"));
        Check("Initial phase is IOC turnover with tools still locked", SceneTurnover.IsPending && SceneTurnover.PlayerRole == ScenarioPlayerRole.IOC && ToolInputLock.IsLocked);
        var sign = cordon.transform.Find("Controlled Entry Point Sign");
        var label = sign != null ? sign.GetComponentInChildren<Text>() : null;
        Check("One world-space controlled-entry sign is authored on the existing cordon", sign != null && cordon.GetComponentsInChildren<Canvas>().Count(c => c.name == "Controlled Entry Point Sign") == 1 && sign.GetComponent<Canvas>().renderMode == RenderMode.WorldSpace);
        Check("Sign identifies the house inner cordon", label != null && label.text == "Inner Cordon\nHouse" && label.fontStyle == FontStyle.Bold && label.color == Color.white && label.fontSize >= 30);
        Canvas.ForceUpdateCanvases();
        Check("Two-line label fits without shrinking or clipping", label != null && label.preferredHeight <= label.rectTransform.rect.height && label.preferredWidth <= label.rectTransform.rect.width);
        var signRect = (RectTransform)sign;
        Check("Physical sign remains compact and readable", signRect.rect.width*sign.lossyScale.x >= .55f && signRect.rect.width*sign.lossyScale.x <= .75f && signRect.rect.height*sign.lossyScale.y >= .2f);
        Check("Sign owns no input, raycaster or collider", sign.GetComponent<GraphicRaycaster>() == null && sign.GetComponentsInChildren<Collider>().Length == 0 && sign.GetComponentsInChildren<Graphic>().All(g => !g.raycastTarget));
        Check("Existing cordon is intact and passable", cordon.CordonVisible && !cordon.EntranceIsOpen && cordon.GetComponentsInChildren<Collider>().Length == 0);
        var original = Camera.main; string tag = original.tag; original.tag = "Untagged";
        var ovr = original.GetComponentInParent<OVRCameraRig>(); bool ovrEnabled = ovr.enabled; ovr.enabled = false;
        var fixture = new GameObject("Arrival entry validation camera", typeof(Camera)); var camera = fixture.GetComponent<Camera>(); camera.tag = "MainCamera"; camera.fieldOfView = 70; camera.aspect = 1.4f;
        var home = new Vector3(rig.position.x, 1.63f, rig.position.z);
        camera.transform.SetParent(rig, true); camera.transform.SetPositionAndRotation(home, rig.rotation);
        var locomotor = FindAnyObjectByType<Oculus.Interaction.Locomotion.FirstPersonLocomotor>();
        locomotor.InjectPlayerEyes(camera.transform);
        var requested = new List<string>(); System.Action<DialogueRequest> record = request => requested.Add(request.id);
        GameDialogue.OnRequested += record;
        try
        {
            guide.Refresh();
            float responderAngle = HorizontalAngle(rig.forward,responder.transform.position-home);
            float horizontalHalfView=Camera.VerticalToHorizontalFieldOfView(camera.fieldOfView,camera.aspect)*.5f;
            Check("Responder is within the actual horizontal camera view [angle="+responderAngle.ToString("F1")+"]", responderAngle<horizontalHalfView-2);
            var responderView = camera.WorldToViewportPoint(actor.GuidanceAnchor.position);
            Check("Responder label is visible at arrival", InView(responderView));
            camera.transform.position = new Vector3(entry.x, entry.y+1.1f, entry.z-1.4f);
            camera.transform.LookAt(sign.position);
            Check("Inner cordon sign is visible approaching the house door", InView(camera.WorldToViewportPoint(sign.position)));
            camera.transform.SetPositionAndRotation(home,rig.rotation);
            Check("Existing NPC guide is reused and initial briefing is in interaction range", guide.IsVisible && guide.TargetPosition == actor.GuidanceAnchor.position && responder.PlayerInRange());
            Check("Arrival does not start the conversation or unlock scene processing", !responder.IsTalking && SceneTurnover.IsPending && photos.CurrentPoint == null && EvidenceStateManager.Instance.AllRecords.All(r => r.status == EvidenceStatus.NotFound));
            Check("Prompt asks the player to deliberately receive the briefing", GameDialogue.Text(responder.PromptId) == "[A] Receive briefing");
            if (System.Environment.GetCommandLineArgs().Contains("-arrivalPreview")) Preview(camera,sign.GetComponent<Canvas>());
            var approach = (Bounds)typeof(SceneCordon).GetField("controlledEntryApproach", flags).GetValue(cordon);
            Vector3 early = cordon.transform.TransformPoint(new Vector3(approach.center.x,approach.center.y,Mathf.Lerp(approach.min.z,approach.max.z,.1f)));
            early.y=entry.y+1.1f;
            int warnings = cordon.EarlyEntryWarnings;
            camera.transform.position = early; yield return null; yield return null; guide.Refresh();
            Check("Crossing the visible entrance is detected before the old protected-area boundary", !cordon.Contains(early) && cordon.RequiresBriefingAt(early));
            Check("Early entry requests the exact warning once", cordon.EarlyEntryWarnings==warnings+1 && requested.Count(id=>id=="fr-early-entry-warning")==1
                && GameDialogue.Text("fr-early-entry-warning")=="Receive the First Responder's briefing before entering the scene.");
            Check("After entry return the NPC guide still targets the briefing", guide.IsVisible && guide.TargetPosition==actor.GuidanceAnchor.position);
            Check("Early entry returns outside without opening a modal", !cordon.RequiresBriefingAt(camera.transform.position) && !PlayerUIGate.IsBlocked && !responder.IsTalking);
            Check("Early entry unlocks no observations, photography or evidence discovery", SceneTurnover.IsPending && !SceneTurnover.RecordIOCObservation("ioc-victim") && photos.CurrentPoint==null && ToolInputLock.IsLocked && EvidenceStateManager.Instance.AllRecords.All(r=>r.status==EvidenceStatus.NotFound));
            for(int i=0;i<5;i++)yield return null;
            Check("Remaining inside does not spam warnings",cordon.EarlyEntryWarnings==warnings+1);
            camera.transform.position=home; yield return null; yield return null; guide.Refresh();
            Check("Returning outside restores ordinary NPC approach guidance",guide.IsVisible&&!guide.GuidanceText.StartsWith(GameDialogue.Text("fr-return-for-briefing"))&&SceneTurnover.IsPending);
            camera.transform.position=early;yield return null;yield return null;
            Check("A separate early reentry produces one new warning",cordon.EarlyEntryWarnings==warnings+2);
            camera.transform.position=home;yield return null;yield return null;
            STCSNotificationUI.Instance?.HideImmediately(); NotificationManager.Instance?.UI.ClearTooltip();
            responder.CompleteWithoutDialogue();yield return null;guide.Refresh();
            Check("Original Form 1 handover still enters IOC assessment and hides the FR guide",SceneTurnover.Phase==ScenarioPhase.IOCAssessment&&SceneTurnover.Form1Received&&!guide.IsVisible);
            Check("The arrival change does not prematurely start SOCO processing",ToolInputLock.IsLocked&&photos.CurrentPoint==null&&!SceneTurnover.Form2Accomplished);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            cordon.PPEStation.PrepareDevelopmentGloves();
#endif
            var register=cordon.AccessRegister.UI;
            camera.transform.position=register.transform.position-register.transform.forward*.7f+Vector3.up*1.6f;
            STCSNotificationUI.Instance?.HideImmediately();NotificationManager.Instance?.UI.ClearTooltip();
            Check("Gloves still require scene-access authorization",!cordon.CanEnter);
            Check("Register opens at its new front-yard approach",register.Open());
            yield return new WaitForSecondsRealtime(.3f);
            Check("Confirmed check-in authorizes the entry",register.ConfirmCheckIn()&&cordon.CanEnter);
            STCSNotificationUI.Instance?.HideImmediately();NotificationManager.Instance?.UI.ClearTooltip();
            warnings=cordon.EarlyEntryWarnings;camera.transform.position=early;yield return null;yield return null;
            Check("After handover, gloves and check-in, crossing produces no entry warning",cordon.EarlyEntryWarnings==warnings);
        }
        finally { GameDialogue.OnRequested-=record;locomotor.InjectPlayerEyes(original.transform);original.tag=tag;ovr.enabled=ovrEnabled;Destroy(fixture); }
        report.AppendLine(failures==0?"=== PASS, 0 failures ===":"=== FAIL: "+failures+" failure(s) ===");Report=report.ToString();IsDone=true;
    }
    static float HorizontalAngle(Vector3 a, Vector3 b) { a.y=b.y=0;return Vector3.Angle(a,b); }
    static bool InView(Vector3 p)=>p.z>0&&p.x>.05f&&p.x<.95f&&p.y>.05f&&p.y<.95f;
    void ValidateCordonBoundary(SceneCordon cordon, Vector3 entry)
    {
        var frame=GameObject.Find("Vicsi_House").transform.Find("DoorFrame").GetComponent<Renderer>().bounds;
        Check("Inner cordon is at the actual house doorway",Mathf.Abs(entry.x-frame.center.x)<.01f&&Mathf.Abs(entry.z-frame.min.z+.02f)<.01f);
        Check("Old yard-spanning tape and freestanding end posts are removed",cordon.transform.Find("PerimeterTape")==null&&cordon.transform.Find("Post_L")==null&&cordon.transform.Find("Post_R")==null);
        var a=cordon.transform.Find("EntrancePost_L");var b=cordon.transform.Find("EntrancePost_R");
        foreach(var strip in cordon.transform.Find("EntranceTape_Closed").GetComponentsInChildren<Renderer>())
        {
            var bounds=strip.bounds;
            Check(strip.name+" fits between house door jambs",Mathf.Abs(bounds.min.x-a.position.x)<.01f&&Mathf.Abs(bounds.max.x-b.position.x)<.01f
                &&bounds.min.x>=frame.min.x&&bounds.max.x<=frame.max.x&&Mathf.Abs(bounds.center.z-entry.z)<.02f);
            Check(strip.name+" is raised above the house floor",bounds.center.y>frame.min.y+.5f&&bounds.center.y<frame.min.y+1.1f);
        }
        Check("House is secured immediately behind the doorway",cordon.RequiresBriefingAt(new Vector3(entry.x,2.8f,entry.z+.03f))&&cordon.Contains(new Vector3(17.3f,2.8f,13)));
        Check("Living, dining, front wing and upstairs are all within the hot zone",new[]{new Vector3(17.7f,2.8f,9.6f),new Vector3(13.3f,2.8f,7.6f),new Vector3(13.3f,2.8f,5.4f),new Vector3(17.3f,5.8f,13)}.All(p=>cordon.RequiresBriefingAt(p)&&cordon.IsWithinSecurityFootprint(p)));
        Check("Yard and exterior porch remain outside the house hot zone",new[]{new Vector3(16.9f,1.6f,3),new Vector3(18.2f,2.8f,5.5f),new Vector3(20.5f,1.6f,12)}.All(p=>!cordon.RequiresBriefingAt(p)&&!cordon.IsWithinSecurityFootprint(p)));
        var outer=GameObject.Find("Outer Cordon - Yard");
        var gateStrips=outer!=null?outer.GetComponentsInChildren<Renderer>().Where(r=>r.name.StartsWith("Outer gate ")).ToArray():System.Array.Empty<Renderer>();
        Check("Separate outer cordon is at the property gate",gateStrips.Length>=4&&gateStrips.All(r=>Mathf.Abs(r.bounds.center.z-.15f)<.01f));
        Check("Alternate exterior door is marked secured",cordon.transform.Find("Side Door Seals").GetComponentsInChildren<Renderer>().Length==2);
        var staging=new[]{cordon.PPEStation.transform,SamplePreparationStation.Instance.transform,cordon.AccessRegister.UI.transform,
            EvidenceBoxDepositZone.Instance.transform,cordon.FingerprintStationAnchor};
        Check("Gloves, drying, logging, evidence box and comparison staging stay outside the secured area",staging.All(t=>t!=null&&!cordon.IsWithinSecurityFootprint(t.position)));
    }
    void Preview(Camera camera, Canvas sign)
    {
        var target=RenderTexture.GetTemporary(1120,800,24);var priorTarget=camera.targetTexture;var priorActive=RenderTexture.active;
        var image=new Texture2D(1120,800,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture=target;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
            image.ReadPixels(new Rect(0,0,1120,800),0,0);image.Apply();
            System.IO.File.WriteAllBytes("docs/evidence/arrival-entry/arrival-view.png",image.EncodeToPNG());var visible=image.GetPixels32();
            sign.enabled=false;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1120,800),0,0);image.Apply();var hidden=image.GetPixels32();int changed=0;
            for(int i=0;i<visible.Length;i++){var a=visible[i];var b=hidden[i];if(a.r>180&&a.g>180&&a.b>180&&Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b)>100)changed++;}
            Check("Native camera render shows the sign's white lettering [pixels="+changed+"]",changed>50);
        }
        finally {sign.enabled=true;camera.targetTexture=priorTarget;RenderTexture.active=priorActive;RenderTexture.ReleaseTemporary(target);Destroy(image);}
    }
}
