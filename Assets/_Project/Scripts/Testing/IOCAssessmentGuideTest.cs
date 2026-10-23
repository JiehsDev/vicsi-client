using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

/// <summary>IOC-only guidance through real observation gates; no new scoring or progress owner.</summary>
public sealed class IOCAssessmentGuideTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    readonly StringBuilder report = new("=== IOCAssessmentGuideTest ===\n");
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    int failures;
    InputDevice device;
    void Check(string name, bool pass) { if (!pass) failures++; report.AppendLine((pass ? "PASS: " : "FAIL: ") + name); }
    public void RunAllTests() => StartCoroutine(Run());
    static void ClearDialogue() => STCSNotificationUI.Instance?.HideImmediately();
    IEnumerator Press(bool down)
    {
        InputSystem.QueueDeltaStateEvent((ButtonControl)device["primaryButton"], down ? 1f : 0f);
        yield return null; yield return null;
    }
    static void Aim(Camera cam, IOCAssessmentInteraction assessment, string id)
    {
        var observation = assessment.Definition.Find(id);
        var prop = FindObjectsByType<EvidenceProp>().First(p => p.evidenceId == observation.evidenceId);
        Vector3 target = prop.transform.position + observation.worldOffset;
        Vector3 eye = new Vector3(target.x, 1.6f, target.z - 1.4f);
        cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye));
        Physics.SyncTransforms();
        if (assessment.ObservationAtGaze() == id) return;
        // Blood can be occluded by the body from the shared straight-on fixture.
        // Find a clear view using the real gaze/range/line-of-sight gate, without moving evidence.
        foreach(float height in new[]{2.8f,2.4f,1.6f})
            foreach(float radius in new[]{1.4f,.8f,2f})
                for(int i=0;i<16;i++)
                {
                    float a=i*Mathf.PI*2/16;
                    eye=new Vector3(target.x+Mathf.Sin(a)*radius,height,target.z+Mathf.Cos(a)*radius);
                    cam.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye));
                    if(assessment.ObservationAtGaze()==id)return;
                }
    }
    IEnumerator Run()
    {
        yield return null;
        var pre = FindAnyObjectByType<PreSessionFlowController>();
        if (pre != null) { pre.enabled = false; PlayerUIGate.Exit(typeof(PreSessionFlowController).GetField("uiGateToken",Private).GetValue(pre)); }
        FindAnyObjectByType<StudentIdentificationPanel>(FindObjectsInactive.Include)?.Hide();
        if (ReasoningCheckpointManager.Instance != null) ReasoningCheckpointManager.Instance.enabled = false;
        if (HypothesisCheckpointManager.Instance != null) HypothesisCheckpointManager.Instance.enabled = false;
        if (ProceduralTrainingGuidance.Instance != null) ProceduralTrainingGuidance.Instance.enabled = false;
        if (NarrativeEventManager.Instance != null) { NarrativeEventManager.Instance.enabled = false; NarrativeEventManager.Instance.StopAllCoroutines(); }
        ClearDialogue();
        var assessment = FindAnyObjectByType<IOCAssessmentInteraction>();
        var guide = assessment.GetComponent<IOCAssessmentGuide>();
        var photos = ScenePhotographyManager.Instance;
        var photoGuide = photos.GetComponent<ScenePhotographyGuide>();
        var props = FindObjectsByType<EvidenceProp>().Where(p=>!string.IsNullOrEmpty(p.evidenceId)).ToArray();
        var positions = props.ToDictionary(p=>p,p=>p.transform.position);
        var statuses = EvidenceStateManager.Instance.AllRecords.ToDictionary(r=>r.definition.evidenceId,r=>r.status);
        Check("Automatically attaches exactly once to IOC assessment",guide!=null && assessment.GetComponents<IOCAssessmentGuide>().Length==1);
        if(guide==null){Finish();yield break;}
        guide.Refresh();
        Check("Hidden before First Responder handover",SceneTurnover.IsPending && !guide.IsVisible);
        var original=Camera.main;string originalTag=original.tag;original.tag="Untagged";
        var cameraGo=new GameObject("IOC guide camera",typeof(Camera));var cam=cameraGo.GetComponent<Camera>();cam.tag="MainCamera";cam.enabled=true;cam.fieldOfView=70;cam.aspect=1.4f;
        var rig=FindFirstObjectByType<OVRCameraRig>();bool rigEnabled=rig!=null&&rig.enabled;if(rig!=null)rig.enabled=false;
        var token=new object();
        InputSystem.RegisterLayout("{\"name\":\"ViCSIIOCGuideController\",\"extend\":\"XRController\",\"controls\":[{\"name\":\"primaryButton\",\"layout\":\"Button\",\"format\":\"FLT\"}]}");
        device=InputSystem.AddDevice("ViCSIIOCGuideController");InputSystem.SetDeviceUsage(device,CommonUsages.RightHand);
        try
        {
            SceneTurnover.Controller.CompleteWithoutDialogue();ClearDialogue();
            var point=assessment.ObservePoint;
            var station=point.position;var rotation=point.rotation;
            cam.transform.position=station+Vector3.up*1.6f-point.forward*(assessment.ObserveRadius+2f);
            cam.transform.rotation=Quaternion.LookRotation(station+Vector3.up*.12f-cam.transform.position);
            guide.Refresh();photoGuide.Refresh();
            Check("IOC phase guides to existing survey standing point",guide.IsVisible && guide.TargetObservationId=="ioc-main-scene-survey" && !photoGuide.IsVisible
                && Vector3.Distance(guide.TargetPosition,station+Vector3.up*.12f)<.001f);
            Check("Outside survey area uses Investigate here, distance and pin",guide.IsPin && guide.GuidanceText.StartsWith("Investigate here") && guide.GuidanceText.Contains(" m\n"));
            cam.transform.rotation=Quaternion.LookRotation(cam.transform.position-guide.TargetPosition);guide.Refresh();
            Check("Offscreen and behind-head target uses an inset arrow",!guide.IsPin && guide.MarkerViewport.x>=.15f && guide.MarkerViewport.x<=.85f
                && guide.MarkerViewport.y>=.21f && guide.MarkerViewport.y<=.79f);
            cam.transform.SetPositionAndRotation(station+Vector3.up*1.6f,Quaternion.LookRotation(-point.forward));guide.Refresh();
            Check("Inside survey area switches to Survey the scene and correct facing direction",guide.GuidanceText.StartsWith("Survey the scene") && !guide.IsReady
                && Vector3.Angle(guide.TargetPosition-cam.transform.position,point.forward)<.01f);
            cam.transform.rotation=Quaternion.LookRotation(point.forward);guide.Refresh();
            Check("Aligned survey is ready without automatically recording it",guide.IsReady && SceneTurnover.IOCObservations.Count==0 && SceneTurnover.Phase==ScenarioPhase.IOCAssessment);
            int eventsBefore=SessionLogger.Instance.EventCounts.TryGetValue(SessionEventType.IOCObservationRecorded,out var n)?n:0;
            for(int i=0;i<20;i++)guide.Refresh();
            Check("Repeated guidance never logs observations or changes progress",SceneTurnover.IOCObservations.Count==0
                && (SessionLogger.Instance.EventCounts.TryGetValue(SessionEventType.IOCObservationRecorded,out n)?n:0)==eventsBefore);
            PlayerUIGate.Enter(token);guide.Refresh();Check("Modal ownership hides guidance",!guide.IsVisible);PlayerUIGate.Exit(token);
            assessment.ShowChecklist();guide.Refresh();Check("IOC checklist hides guidance",assessment.IsChecklistVisible&&!guide.IsVisible);
            assessment.HideChecklist();yield return null;yield return null;
            STCSNotificationUI.Instance.ShowLine("IOC guide dialogue visibility fixture", "IOC", "IOC", "ioc-guide-test");yield return null;guide.Refresh();
            Check("Dialogue hides guidance",STCSNotificationUI.Instance.IsShowing&&!guide.IsVisible);ClearDialogue();guide.Refresh();
            Check("Closing dialogue restores guidance",guide.IsVisible);
            assessment.enabled=false;guide.Refresh();Check("Disabled assessment hides guidance",!guide.IsVisible);assessment.enabled=true;
            guide.enabled=false;Check("Disabled guide leaves no visible canvas",!guide.IsVisible);guide.enabled=true;guide.Refresh();
            Check("Guide has no input raycaster, colliders or hit targets",guide.GuideCanvas.GetComponent<GraphicRaycaster>()==null
                && guide.GuideCanvas.GetComponentsInChildren<Collider>().Length==0 && guide.GuideCanvas.GetComponentsInChildren<Graphic>().All(g=>!g.raycastTarget));
            Canvas.ForceUpdateCanvases();
            var graphic=guide.GuideCanvas.GetComponentInChildren<PhotographyGuideGraphic>();
            Check("Reuses native photography arrow/pin mesh and overlay shader",graphic.material.shader.isSupported && graphic.canvasRenderer.GetMesh().vertexCount>0);
            yield return Press(true);
            Check("Existing real XR A records the survey with jump suppression",SceneTurnover.IOCObservations.Contains("ioc-main-scene-survey")&&PlayerUIGate.SuppressGameplayJump);
            yield return Press(false);ClearDialogue();guide.Refresh();
            Check("After survey, guide recommends victim using readable existing label",guide.TargetObservationId=="ioc-victim" && guide.GuidanceText.Contains(EvidenceDisplay.Label("BODY-A")));
            // The recommendation does not impose an order: bottle is valid before victim/knife.
            Aim(cam,assessment,"ioc-drinking-context");
            Check("Out-of-order bottle observation remains valid",assessment.ObservationAtGaze()=="ioc-drinking-context" && assessment.ObserveAtGaze());
            ClearDialogue();guide.Refresh();Check("Guide continues to unfinished victim",guide.TargetObservationId=="ioc-victim");
            Aim(cam,assessment,"ioc-victim");guide.Refresh();
            Check("Within range shows Examine this and readiness matches authoritative gaze",guide.GuidanceText.StartsWith("Examine this")&&guide.IsReady);
            var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.name="IOC guide sightline fixture";
            blocker.transform.position=Vector3.Lerp(cam.transform.position,guide.TargetPosition,.5f);blocker.transform.localScale=Vector3.one*.2f;Physics.SyncTransforms();guide.Refresh();
            Check("Occlusion prevents ready highlight and existing observation",!guide.IsReady && assessment.ObservationAtGaze()!="ioc-victim");
            blocker.GetComponent<Collider>().enabled=false;Destroy(blocker);Physics.SyncTransforms();guide.Refresh();
            Check("Existing victim observation completes normally",assessment.ObserveAtGaze());ClearDialogue();guide.Refresh();
            Check("After victim, recommends unfinished knife",guide.TargetObservationId=="ioc-possible-weapon");
            cam.transform.position=guide.TargetPosition+Vector3.back*5;cam.transform.rotation=Quaternion.LookRotation(guide.TargetPosition-cam.transform.position);guide.Refresh();
            Check("Out-of-range evidence says Move closer without granting eligibility",guide.GuidanceText.StartsWith("Move closer")&&!guide.IsReady);
            // Inspect all current label strings without recording them or changing the quota.
            foreach(var observation in assessment.Definition.observations.Where(o=>o.kind==IOCObservationKind.PossibleEvidence))
            {
                var text=guide.GuideCanvas.GetComponentInChildren<Text>();
                string old=text.text;text.text="Examine this \u00b7 1.0 m\n"+EvidenceDisplay.Label(observation.evidenceId);
                var generator=new TextGenerator();var settings=text.GetGenerationSettings(text.rectTransform.rect.size);
                generator.Populate(text.text,settings);
                Check("Readable guide label fits two lines: "+observation.evidenceId,generator.lineCount<=2
                    && generator.GetPreferredHeight(text.text,settings)/text.pixelsPerUnit<=text.rectTransform.rect.height+.1f);
                text.text=old;
            }
            Aim(cam,assessment,"ioc-bloodstain-area");
            Check("Alternative evidence can satisfy quota without following knife recommendation",assessment.ObserveAtGaze());ClearDialogue();guide.Refresh();
            Check("Two possible-evidence observations suffice; optional knife is not demanded",SceneTurnover.AssessmentReady && SceneTurnover.PossibleEvidenceObserved==2
                && !SceneTurnover.IOCObservations.Contains("ioc-possible-weapon") && !guide.IsVisible);
            Check("Existing completion action reaches IOC decision",SceneTurnover.CompleteIOCAssessment());ClearDialogue();guide.Refresh();
            Check("IOC guide stays hidden after assessment",SceneTurnover.Phase==ScenarioPhase.IOCSOCODecision&&!guide.IsVisible);
            SceneTurnover.TryAdvance(ScenarioPhase.SOCORequested);SceneTurnover.TryAdvance(ScenarioPhase.SOCOTurnover);SceneTurnover.TryAdvance(ScenarioPhase.SOCOInvestigation);
            ClearDialogue();typeof(ScenePhotographyManager).GetMethod("ActivateFirstPoint",Private).Invoke(photos,null);
            guide.Refresh();photoGuide.Refresh();
            Check("SOCO photography retains its own guide and original wording",SceneTurnover.Phase==ScenarioPhase.SOCOInvestigation&&!guide.IsVisible&&photoGuide.IsVisible
                && photoGuide.GuideCanvas.GetComponentInChildren<Text>().text.Contains("Stand here"));
            Check("Survey anchor, all evidence positions and evidence states unchanged",point.position==station&&point.rotation==rotation
                && props.All(p=>p.transform.position==positions[p]) && EvidenceStateManager.Instance.AllRecords.All(r=>statuses[r.definition.evidenceId]==r.status));
            Check("Only four intentional IOC observations were recorded",SceneTurnover.IOCObservations.Count==4
                && SessionLogger.Instance.EventCounts[SessionEventType.IOCObservationRecorded]==eventsBefore+4);
        }
        finally
        {
            PlayerUIGate.Exit(token);assessment.HideChecklist();original.tag=originalTag;if(rig!=null)rig.enabled=rigEnabled;Destroy(cameraGo);
            if(device!=null)InputSystem.RemoveDevice(device);InputSystem.RemoveLayout("ViCSIIOCGuideController");
        }
        Finish();
    }
    void Finish(){report.AppendLine(failures==0?"=== PASS ===":"=== FAIL: "+failures+" ===");Report=report.ToString();IsDone=true;Debug.Log(Report);}
}
