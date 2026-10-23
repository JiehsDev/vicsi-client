using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Fills navigation gaps between the existing phase-specific guides. Never advances a procedure.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(21010)]
public sealed class ProcedureDestinationGuide : MonoBehaviour
{
    [SerializeField] private Canvas waypointCanvas;
    [SerializeField] private RectTransform glyph;
    [SerializeField] private PhotographyGuideGraphic icon;
    [SerializeField] private Text instruction;
    [SerializeField, Min(.8f)] private float displayDistance = 1.6f;
    private bool wasPin;
    private float nextResolve, nextLabel;
    private static int captureDepth;
    private static readonly List<ProcedureDestinationGuide> active = new();
    public bool IsVisible => waypointCanvas != null && waypointCanvas.enabled;
    public string TargetId { get; private set; }
    public string GuidanceText { get; private set; }
    public Vector3 TargetPosition { get; private set; }
    public Canvas GuideCanvas => waypointCanvas;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { active.Clear(); captureDepth = 0; }
    void OnEnable() { if (!active.Contains(this)) active.Add(this); }
    void OnDisable() { active.Remove(this); if (waypointCanvas != null) waypointCanvas.enabled = false; }
    void LateUpdate() => Refresh(false);
    public void Refresh() => Refresh(true);
    private void Refresh(bool force)
    {
        if (waypointCanvas == null) return;
        if (force || Time.unscaledTime >= nextResolve) { Resolve(); nextResolve = Time.unscaledTime + .2f; }
        var camera = Camera.main;
        bool show = isActiveAndEnabled && TargetId != null && camera != null && captureDepth == 0
            && !PlayerUIGate.IsBlocked && !ToolInputLock.HasUIOwners
            && (STCSNotificationUI.Instance == null || (!STCSNotificationUI.Instance.IsShowing && STCSNotificationUI.Instance.PendingCount == 0));
        waypointCanvas.enabled = show;
        if (!show) { wasPin = false; return; }
        var viewport = ScenePhotographyGuide.Project(camera.WorldToViewportPoint(TargetPosition), camera.transform.InverseTransformPoint(TargetPosition), wasPin, out bool pin, out var direction);
        wasPin = pin;
        waypointCanvas.transform.SetPositionAndRotation(camera.ViewportToWorldPoint(new Vector3(viewport.x, viewport.y, displayDistance)), camera.transform.rotation);
        glyph.localRotation = Quaternion.Euler(0, 0, pin ? 0 : Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90);
        icon.SetState(pin, false);
        Vector3 distance = camera.transform.position - TargetPosition; distance.y = 0;
        if (force || Time.unscaledTime >= nextLabel) { instruction.text = GuidanceText + "\n" + distance.magnitude.ToString("0.0") + " m"; nextLabel=Time.unscaledTime+.15f; }
    }
    void SetTarget(string id, string text, Vector3 position) { TargetId = id; GuidanceText = text; TargetPosition = position; }
    void Resolve()
    {
        TargetId = null; GuidanceText = null;
        if (!SceneTurnover.IsGated) return;
        var cordon = FindAnyObjectByType<SceneCordon>();
        var gloves = ArrivalPPEStation.Instance;
        if (SceneTurnover.Phase == ScenarioPhase.IOCAssessment)
        {
            // Existing glove and IOC observation guides own the adjacent steps.
            if (gloves != null && gloves.HasCleanGloves && cordon != null && cordon.AccessRegister != null && !cordon.AccessRegister.IsAuthorized && cordon.AuthoredRegister != null)
                SetTarget("access-register", "Sign the scene-entry register", cordon.AuthoredRegister.transform.position + Vector3.up);
            return;
        }
        var closeout = SOCOCloseout.Instance;
        if (SceneTurnover.Phase == ScenarioPhase.FinalSceneSurvey && closeout != null)
        { SetTarget("final-survey", "Complete the final scene survey", closeout.FinalSurveyPosition); return; }
        var interview = VendorFollowupInterview.Instance;
        if (SceneTurnover.Phase == ScenarioPhase.SOCOInvestigation && interview != null && interview.RequiresFollowup
            && !interview.IsShowing && !interview.IsTalking && !ProcedureInterlock.BlocksUnrelatedActions
            && (gloves == null || !gloves.GuidanceNeeded))
        {
            var vendor = FindAnyObjectByType<VendorWitnessInteraction>();
            if (vendor != null && vendor.isActiveAndEnabled && vendor.Interview == interview && !vendor.PlayerInRange())
                SetTarget("vendor-followup", GameDialogue.Text("vendor-followup-waypoint"), vendor.InteractionPosition + Vector3.up * 1.4f);
            return;
        }
        // Existing guides own travel to stations, scene photography and the box.
        // Local evidence work uses its interaction prompts, without another waypoint.
    }
    public static IDisposable HideForCapture() => new CaptureScope();
    sealed class CaptureScope : IDisposable
    {
        bool disposed;
        public CaptureScope() { captureDepth++; foreach (var g in active) if (g != null && g.waypointCanvas != null) g.waypointCanvas.enabled = false; }
        public void Dispose() { if (disposed) return; disposed = true; captureDepth = Mathf.Max(0, captureDepth - 1); foreach (var g in active) if (g != null) g.Refresh(); }
    }
}
