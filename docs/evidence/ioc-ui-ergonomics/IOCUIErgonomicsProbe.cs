// Temporary Editor Play-mode probe; retained as evidence, not a shipped diagnostic suite.
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class IOCUIErgonomicsProbe : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    private readonly StringBuilder report = new();
    private int failures;
    private const string Output = "docs/evidence/ioc-ui-ergonomics/";
    public void RunAllTests() => StartCoroutine(Run());
    private void Check(string name, bool ok) { report.AppendLine((ok ? "PASS: " : "FAIL: ") + name); if (!ok) failures++; }
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    private IEnumerator Run()
    {
        var assessment = FindAnyObjectByType<IOCAssessmentInteraction>();
        var menu = FindAnyObjectByType<UtilityMenuController>();
        var dialogue = STCSNotificationUI.Instance;
        var notification = NotificationManager.Instance.UI;
        var initialEvidence = EvidenceStateManager.Instance.AllRecords.ToDictionary(r => r.definition.evidenceId, r => r.status);
        var originalCamera = Camera.main;
        originalCamera.tag = "Untagged";
        var cam = new GameObject("IOCUIProbeCamera", typeof(Camera)).GetComponent<Camera>();
        cam.tag = "MainCamera"; cam.fieldOfView = 60;
        typeof(HudFollowCamera).GetField("targetCamera", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(notification.GetComponent<HudFollowCamera>(), cam);
        // Existing diagnostic handover API; this probe tests UI, not the FR conversation.
        SceneTurnover.Controller.CompleteWithoutDialogue();
        yield return null;
        dialogue.HideImmediately(); notification.ClearTooltip();
        cam.transform.SetPositionAndRotation(new Vector3(-3.8f, 1.6f, 9.5f),
            Quaternion.LookRotation(assessment.Definition.surveyLookAt - new Vector3(-3.8f, 1.6f, 9.5f)));
        yield return null;
        Check("Checklist stays hidden in IOC assessment; seven live checklist rows exist", !assessment.IsChecklistVisible
            && assessment.ChecklistText.Split('\n').Count(l => l.Contains("[x]") || l.Contains("[ ]")) == 7);
        bool validHint = assessment.ObservationAtGaze() == "ioc-main-scene-survey"
            && notification.HasAmbientPrompt && notification.GetComponent<HudFollowCamera>().LocalOffset == new Vector3(0, -.16f, 0);
        yield return new WaitForSeconds(.3f); // let the existing HUD smoothing settle before visual capture
        Capture(cam, "action-hint.png");
        var gaze = cam.transform.rotation;
        cam.transform.rotation = Quaternion.LookRotation(Vector3.up);
        yield return null; yield return null;
        bool noTargetHint = assessment.ObservationAtGaze() == null && !notification.HasAmbientPrompt;
        cam.transform.rotation = gaze;
        GameDialogue.SayLine("ioc-assessment-guidance", "System", "ioc-ui-probe");
        yield return null; yield return null;
        assessment.ShowChecklist();
        bool dialogueSuppressed = !notification.HasAmbientPrompt && !assessment.IsChecklistVisible;
        dialogue.HideImmediately();
        yield return null;
        Check("Valid survey has one lower-center hint; no target or dialogue suppresses it", validHint && noTargetHint && dialogueSuppressed);

        InputSystem.RegisterLayout(@"{""name"":""IOCUIProbeController"",""extend"":""XRController"",""controls"":[
            {""name"":""primaryButton"",""layout"":""Button"",""format"":""FLT""},{""name"":""secondaryButton"",""layout"":""Button"",""format"":""FLT""},
            {""name"":""triggerPressed"",""layout"":""Button"",""format"":""FLT""},{""name"":""thumbstick"",""layout"":""Stick""}]}");
        var left = InputSystem.AddDevice("IOCUIProbeController"); InputSystem.SetDeviceUsage(left, CommonUsages.LeftHand);
        var right = InputSystem.AddDevice("IOCUIProbeController"); InputSystem.SetDeviceUsage(right, CommonUsages.RightHand);
        InputSystem.QueueDeltaStateEvent((ButtonControl)left["primaryButton"], 1f);
        InputSystem.QueueDeltaStateEvent((ButtonControl)right["primaryButton"], 1f);
        InputSystem.QueueDeltaStateEvent((ButtonControl)right["triggerPressed"], 1f);
        yield return null; yield return null;
        Check("X utility opening consumes simultaneous A/trigger without observing; hint suppressed", menu.IsCanvasActive
            && SceneTurnover.IOCObservations.Count == 0 && !notification.HasAmbientPrompt);
        // IOC Checklist is authored first, so the existing wheel's initial highlight selects it.
        InputSystem.QueueDeltaStateEvent((ButtonControl)right["primaryButton"], 0f);
        InputSystem.QueueDeltaStateEvent((ButtonControl)right["triggerPressed"], 0f);
        InputSystem.QueueDeltaStateEvent((ButtonControl)left["primaryButton"], 0f);
        yield return null; yield return null;
        var panel = Field<GameObject>(assessment, "panel");
        var text = Field<TMP_Text>(assessment, "checklist");
        Canvas.ForceUpdateCanvases(); text.ForceMeshUpdate();
        Vector3 delta = panel.transform.position - cam.transform.position;
        Vector3 forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
        Check("Release X opens centered readable world panel at 1.35 m, no head follower or text overflow", assessment.IsChecklistVisible
            && PlayerUIGate.IsBlocked && !notification.HasAmbientPrompt && panel.GetComponent<HudFollowCamera>() == null
            && Mathf.Abs(Vector3.Dot(delta, forward) - 1.35f) < .01f
            && Mathf.Abs(Vector3.Dot(delta, cam.transform.right)) < .01f && !text.isTextOverflowing);
        notification.ClearTooltip(); // discard the existing deposit-zone refusal caused by the synthetic trigger test
        yield return new WaitForSeconds(.3f);
        Capture(cam, "checklist-open.png");
        InputSystem.QueueDeltaStateEvent((ButtonControl)right["primaryButton"], 1f);
        InputSystem.QueueDeltaStateEvent((ButtonControl)right["triggerPressed"], 1f);
        yield return null; yield return null;
        bool inert = SceneTurnover.IOCObservations.Count == 0;
        InputSystem.QueueDeltaStateEvent((ButtonControl)right["primaryButton"], 0f);
        InputSystem.QueueDeltaStateEvent((ButtonControl)right["triggerPressed"], 0f);
        InputSystem.QueueDeltaStateEvent((ButtonControl)right["secondaryButton"], 1f);
        yield return null; yield return null;
        Check("Open checklist blocks world A/trigger; B closes and restores UI gate", inert && !assessment.IsChecklistVisible
            && !PlayerUIGate.IsBlocked && assessment.ObservationAtGaze() == "ioc-main-scene-survey");
        InputSystem.QueueDeltaStateEvent((ButtonControl)right["secondaryButton"], 0f);
        InputSystem.QueueDeltaStateEvent((ButtonControl)right["primaryButton"], 1f);
        yield return null; yield return null;
        dialogue.HideImmediately(); notification.ClearTooltip();
        assessment.ShowChecklist();
        Check("Normal A observation resumes and open checklist shows fresh secured/survey progress", SceneTurnover.IOCObservations.Contains("ioc-main-scene-survey")
            && assessment.IsChecklistVisible && assessment.ChecklistText.Contains("[x] " + GameDialogue.Text("ioc-check-security"))
            && assessment.ChecklistText.Contains("[x] " + GameDialogue.Text("ioc-check-survey")));
        // Exercise a phase change while the panel is still open through the existing procedural API.
        foreach (var item in assessment.Definition.observations) SceneTurnover.RecordIOCObservation(item.observationId);
        bool completed = SceneTurnover.CompleteIOCAssessment();
        var entries = Field<System.Collections.Generic.List<UtilityMenuController.MenuEntry>>(menu, "entries");
        Check("Assessment completion closes panel and removes IOC entry availability", completed
            && SceneTurnover.Phase == ScenarioPhase.IOCSOCODecision && !assessment.IsChecklistVisible
            && !assessment.CanOpenChecklist && !entries.Single(e => e.displayName == "IOC Checklist").IsAvailable);
        Check("Evidence statuses remain unchanged", EvidenceStateManager.Instance.AllRecords.All(r => initialEvidence[r.definition.evidenceId] == r.status));
        // Direct prompt presentation exercises no-target withdrawal and tooltip fallback, independent of decision UI.
        notification.ShowPrompt("[A] Observe", new Vector3(0, -.16f, 0));
        notification.ShowTooltip("Existing tooltip", 1f);
        bool tooltipPosition = notification.GetComponent<HudFollowCamera>().LocalOffset == new Vector3(0, -.5f, 0);
        notification.ClearTooltip(); notification.HidePrompt();
        Check("Hint withdrawal clears ambient channel and restores authored tooltip position", !notification.HasAmbientPrompt
            && tooltipPosition && notification.GetComponent<HudFollowCamera>().LocalOffset == new Vector3(0, -.5f, 0));
        InputSystem.RemoveDevice(left); InputSystem.RemoveDevice(right); InputSystem.RemoveLayout("IOCUIProbeController");
        cam.tag = "Untagged"; originalCamera.tag = "MainCamera"; Destroy(cam.gameObject);
        report.AppendLine(failures == 0 ? "=== PASS: 9 focused IOC UI checks ===" : "=== FAIL: IOC UI ergonomics ===");
        Report = report.ToString(); IsDone = true;
    }
    private static void Capture(Camera cam, string name)
    {
        var target = new RenderTexture(1600, 1000, 24); cam.targetTexture = target; cam.Render();
        RenderTexture.active = target; var texture = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0); texture.Apply();
        File.WriteAllBytes(Output + name, texture.EncodeToPNG()); cam.targetTexture = null; RenderTexture.active = null;
        Destroy(texture); target.Release(); Destroy(target);
    }
}
