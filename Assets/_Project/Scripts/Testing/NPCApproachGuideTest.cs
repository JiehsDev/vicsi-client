using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

/// <summary>Real FR/IOC conversation input with read-only, phase-specific NPC guidance.</summary>
public sealed class NPCApproachGuideTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    readonly StringBuilder report = new("=== NPCApproachGuideTest ===\n");
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    int failures;
    InputDevice device;
    void Check(string name, bool ok) { if (!ok) failures++; report.AppendLine((ok ? "PASS: " : "FAIL: ") + name); }
    public void RunAllTests() => StartCoroutine(Run());
    static void Clear() => STCSNotificationUI.Instance?.HideImmediately();
    static int Count(SessionEventType type) => SessionLogger.Instance.EventCounts.TryGetValue(type,out int n)?n:0;
    IEnumerator Press(bool down)
    {
        InputSystem.QueueDeltaStateEvent((ButtonControl)device["primaryButton"],down?1f:0f);
        yield return null;yield return null;
    }
    IEnumerator Drain(Func<bool> speaking)
    {
        double deadline=Time.realtimeSinceStartupAsDouble+35;
        while(speaking()&&Time.realtimeSinceStartupAsDouble<deadline)
        {
            var dialogue=STCSNotificationUI.Instance;
            if(dialogue.IsShowing&&dialogue.Countdown.VisibleSeconds>ViCSIDialogueCatalog.Current.skipProtectionSeconds+.05f)dialogue.Skip();
            yield return null;
        }
    }
    IEnumerator Run()
    {
        yield return null;
        var pre=FindAnyObjectByType<PreSessionFlowController>();
        if(pre!=null){pre.enabled=false;PlayerUIGate.Exit(typeof(PreSessionFlowController).GetField("uiGateToken",Private).GetValue(pre));}
        FindAnyObjectByType<StudentIdentificationPanel>(FindObjectsInactive.Include)?.Hide();
        if(ReasoningCheckpointManager.Instance!=null)ReasoningCheckpointManager.Instance.enabled=false;
        if(HypothesisCheckpointManager.Instance!=null)HypothesisCheckpointManager.Instance.enabled=false;
        if(ProceduralTrainingGuidance.Instance!=null)ProceduralTrainingGuidance.Instance.enabled=false;
        if(NarrativeEventManager.Instance!=null){NarrativeEventManager.Instance.enabled=false;NarrativeEventManager.Instance.StopAllCoroutines();}
        Clear();
        var actors=FindObjectsByType<NPCPresentation>(FindObjectsInactive.Include);
        var fr=actors.Single(a=>a.CharacterRole==NPCPresentation.Role.FirstResponder);
        var ioc=actors.Single(a=>a.CharacterRole==NPCPresentation.Role.IOC);
        var responder=fr.Responder;var flow=ioc.Workflow;
        var frGuide=fr.GetComponent<NPCApproachGuide>();var iocGuide=ioc.GetComponent<NPCApproachGuide>();
        var assessment=FindAnyObjectByType<IOCAssessmentInteraction>();var assessmentGuide=assessment.GetComponent<IOCAssessmentGuide>();
        var props=FindObjectsByType<EvidenceProp>().Where(p=>!string.IsNullOrEmpty(p.evidenceId)).ToArray();
        var positions=props.ToDictionary(p=>p,p=>p.transform.position);
        var statuses=EvidenceStateManager.Instance.AllRecords.ToDictionary(r=>r.definition.evidenceId,r=>r.status);
        var frPosition=fr.transform.position;var iocPosition=ioc.transform.position;
        var marker=GameObject.Find("IOC_Start_Ground_Circle");var markerPosition=marker.transform.position;
        Check("Both NPCs auto-attach exactly one guide",frGuide!=null&&iocGuide!=null&&fr.GetComponents<NPCApproachGuide>().Length==1&&ioc.GetComponents<NPCApproachGuide>().Length==1);
        Check("Vendor presentation remains excluded",actors.Where(a=>a.CharacterRole==NPCPresentation.Role.Vendor).All(a=>a.GetComponent<NPCApproachGuide>()==null));
        if(frGuide==null||iocGuide==null){Finish();yield break;}
        var original=Camera.main;string oldTag=original.tag;original.tag="Untagged";
        var rig=original.GetComponentInParent<OVRCameraRig>();bool rigEnabled=rig.enabled;rig.enabled=false;
        var rigPosition=rig.transform.position;var rigRotation=rig.transform.rotation;
        var cameraObject=new GameObject("NPC guide camera",typeof(Camera));var cam=cameraObject.GetComponent<Camera>();cam.tag="MainCamera";cam.enabled=true;cam.fieldOfView=70;cam.aspect=1.4f;
        var token=new object();
        InputSystem.RegisterLayout("{\"name\":\"ViCSINPCGuideController\",\"extend\":\"XRController\",\"controls\":[{\"name\":\"primaryButton\",\"layout\":\"Button\",\"format\":\"FLT\"}]}");
        device=InputSystem.AddDevice("ViCSINPCGuideController");InputSystem.SetDeviceUsage(device,CommonUsages.RightHand);
        try
        {
            cam.transform.position=fr.transform.position+Vector3.up*1.6f+Vector3.back*(responder.InteractionRadius+3);
            cam.transform.rotation=Quaternion.LookRotation(fr.GuidanceAnchor.position-cam.transform.position);
            frGuide.Refresh();iocGuide.Refresh();assessmentGuide.Refresh();
            Check("Pending handover shows only First Responder guide",SceneTurnover.IsPending&&frGuide.IsVisible&&!iocGuide.IsVisible&&!assessmentGuide.IsVisible);
            Check("FR guide uses authored NPC anchor, readable role and horizontal distance",frGuide.IsPin&&!frGuide.IsReady
                &&frGuide.TargetPosition==fr.GuidanceAnchor.position&&frGuide.GuidanceText.StartsWith("Approach")&&frGuide.GuidanceText.Contains(" m\nFirst Responder"));
            int before=SessionLogger.Instance.EventCounts.Values.Sum();
            for(int i=0;i<20;i++){frGuide.Refresh();iocGuide.Refresh();}
            Check("Guidance alone never logs events or advances handover",SessionLogger.Instance.EventCounts.Values.Sum()==before&&SceneTurnover.IsPending);
            cam.transform.rotation=Quaternion.LookRotation(cam.transform.position-frGuide.TargetPosition);frGuide.Refresh();
            Check("Looking away gives stable inset arrow",!frGuide.IsPin&&frGuide.MarkerViewport.x>=.15f&&frGuide.MarkerViewport.x<=.85f&&frGuide.MarkerViewport.y>=.21f&&frGuide.MarkerViewport.y<=.79f);
            PlayerUIGate.Enter(token);frGuide.Refresh();Check("Modal UI hides NPC guidance",!frGuide.IsVisible);PlayerUIGate.Exit(token);
            STCSNotificationUI.Instance.ShowLine("NPC guide dialogue fixture","System","System","npc-guide-test");frGuide.Refresh();
            Check("Spoken dialogue hides NPC guidance",STCSNotificationUI.Instance.IsShowing&&!frGuide.IsVisible);Clear();frGuide.Refresh();
            Check("Guide returns after dialogue closes",frGuide.IsVisible);
            responder.enabled=false;frGuide.Refresh();Check("Disabled interaction cannot advertise an available NPC",!frGuide.IsVisible);responder.enabled=true;
            frGuide.enabled=false;Check("Disabling guide immediately hides canvas",!frGuide.IsVisible);frGuide.enabled=true;
            foreach(var guide in new[]{frGuide,iocGuide})
                Check("NPC HUD adds no raycaster, collider or hit targets: "+guide.name,guide.GuideCanvas.GetComponent<GraphicRaycaster>()==null
                    &&guide.GuideCanvas.GetComponentsInChildren<Collider>().Length==0&&guide.GuideCanvas.GetComponentsInChildren<Graphic>().All(g=>!g.raycastTarget));
            yield return Press(true);Check("Actual A outside FR range does not start conversation",!responder.IsTalking&&SceneTurnover.IsPending);yield return Press(false);
            cam.transform.position=fr.transform.position+Vector3.up*1.6f+Vector3.back;
            cam.transform.rotation=Quaternion.LookRotation(fr.GuidanceAnchor.position-cam.transform.position);frGuide.Refresh();
            Check("In-range FR pin uses original range check and Speak with them",responder.PlayerInRange()&&frGuide.IsReady&&frGuide.GuidanceText.StartsWith("Speak with them"));
            var text=frGuide.GuideCanvas.GetComponentInChildren<Text>();var generator=new TextGenerator();var settings=text.GetGenerationSettings(text.rectTransform.rect.size);generator.Populate(text.text,settings);
            Check("FR instruction fits two readable lines",generator.lineCount<=2&&generator.GetPreferredHeight(text.text,settings)/text.pixelsPerUnit<=text.rectTransform.rect.height+.1f);
            Canvas.ForceUpdateCanvases();var glyph=frGuide.GuideCanvas.GetComponentInChildren<PhotographyGuideGraphic>();
            Check("Native arrow/pin mesh uses supported existing overlay shader",glyph.material.shader.isSupported&&glyph.canvasRenderer.GetMesh().vertexCount>0);
            yield return Press(true);frGuide.Refresh();
            Check("Existing real A begins FR dialogue once and hides guide",responder.IsTalking&&!frGuide.IsVisible&&Count(SessionEventType.FirstResponderInteractionStarted)==1);
            yield return Press(false);yield return Drain(()=>responder.IsTalking);Clear();frGuide.Refresh();assessmentGuide.Refresh();
            Check("FR dialogue completes original handover and switches to assessment guide",SceneTurnover.Phase==ScenarioPhase.IOCAssessment&&!frGuide.IsVisible&&assessmentGuide.IsVisible&&Count(SessionEventType.CSIForm1Received)==1);
            foreach(var observation in assessment.Definition.observations)SceneTurnover.RecordIOCObservation(observation.observationId);
            Check("Existing assessment completion and SOCO request remain available",SceneTurnover.CompleteIOCAssessment()&&flow.Choose(true));Clear();
            Check("Existing arrival enters IOC briefing phase",flow.AcknowledgeArrival()&&SceneTurnover.Phase==ScenarioPhase.SOCOTurnover);Clear();
            while(flow.IsArriving) yield return null;
            Clear();
            Vector3 outside=markerPosition+Vector3.back*(flow.InteractionRadius+2);
            rig.transform.position=new Vector3(outside.x,rig.transform.position.y,outside.z);
            cam.transform.position=outside+Vector3.up*1.6f;cam.transform.rotation=Quaternion.LookRotation(ioc.GuidanceAnchor.position-cam.transform.position);
            yield return null;iocGuide.Refresh();frGuide.Refresh();assessmentGuide.Refresh();
            Check("Only visible IOC actor gets approach guidance before briefing",ioc.IsVisible&&iocGuide.IsVisible&&!frGuide.IsVisible&&!assessmentGuide.IsVisible);
            Check("IOC pin targets actual NPC label anchor, not the FR host",iocGuide.IsPin&&iocGuide.TargetPosition==ioc.GuidanceAnchor.position&&iocGuide.GuidanceText.EndsWith("\nIOC"));
            Check("Guide does not bypass existing IOC marker radius",!flow.IsWithinIOCInteractionRadius&&!flow.IsPanelVisible&&!flow.BeginBriefing());
            flow.enabled=false;iocGuide.Refresh();Check("Disabled IOC workflow hides guidance",!iocGuide.IsVisible);flow.enabled=true;
            // Keep both views outside while testing modal suppression/restoration.
            PlayerUIGate.Enter(token);iocGuide.Refresh();Check("IOC guide respects modal ownership",!iocGuide.IsVisible);PlayerUIGate.Exit(token);iocGuide.Refresh();
            Check("IOC approach guidance restores after modal closes",iocGuide.IsVisible);
            Vector3 inside=markerPosition+Vector3.back*(flow.InteractionRadius*.65f);
            rig.transform.position=new Vector3(inside.x,rig.transform.position.y,inside.z);
            cam.transform.position=inside+Vector3.up*1.6f;cam.transform.rotation=Quaternion.LookRotation(ioc.GuidanceAnchor.position-cam.transform.position);
            yield return null;yield return null;iocGuide.Refresh();
            Check("Existing briefing panel takes over inside radius without overlay clutter",flow.IsWithinIOCInteractionRadius&&flow.IsPanelVisible&&!iocGuide.IsVisible);
            yield return Press(true);iocGuide.Refresh();
            Check("Actual A still starts original IOC briefing and keeps guide hidden",flow.IsTalking&&!iocGuide.IsVisible);yield return Press(false);
            yield return Drain(()=>flow.IsTalking);Clear();iocGuide.Refresh();frGuide.Refresh();
            Check("Completed briefing retires IOC and leaves both NPC guides hidden",SceneTurnover.Phase==ScenarioPhase.SOCOInvestigation&&!ioc.IsVisible&&!iocGuide.IsVisible&&!frGuide.IsVisible);
            var photos=ScenePhotographyManager.Instance;typeof(ScenePhotographyManager).GetMethod("ActivateFirstPoint",Private).Invoke(photos,null);
            var photoGuide=photos.GetComponent<ScenePhotographyGuide>();photoGuide.Refresh();
            Check("Original photography guide remains active after NPC handoffs",photoGuide.IsVisible);
            Check("NPCs, IOC interaction marker and all evidence positions/states preserved",fr.transform.position==frPosition&&ioc.transform.position==iocPosition&&marker.transform.position==markerPosition
                &&props.All(p=>p.transform.position==positions[p])&&EvidenceStateManager.Instance.AllRecords.All(r=>r.status==statuses[r.definition.evidenceId]));
            Check("Existing Form 1/Form 2 and investigation events remain once-only",Count(SessionEventType.CSIForm1Received)==1&&Count(SessionEventType.CSIForm2Completed)==1&&Count(SessionEventType.SOCOInvestigationStarted)==1);
        }
        finally
        {
            PlayerUIGate.Exit(token);original.tag=oldTag;rig.transform.SetPositionAndRotation(rigPosition,rigRotation);rig.enabled=rigEnabled;Destroy(cameraObject);
            if(device!=null)InputSystem.RemoveDevice(device);InputSystem.RemoveLayout("ViCSINPCGuideController");
        }
        Finish();
    }
    void Finish(){report.AppendLine(failures==0?"=== PASS ===":"=== FAIL: "+failures+" ===");Report=report.ToString();IsDone=true;Debug.Log(Report);}
}
