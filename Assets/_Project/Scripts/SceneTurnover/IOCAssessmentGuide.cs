using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>IOC-only, read-only recommendations. Observation validation and progress stay in their existing owners.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(21000)]
public sealed class IOCAssessmentGuide : MonoBehaviour
{
    [SerializeField, Min(.8f)] private float displayDistance = 1.6f;
    private IOCAssessmentInteraction assessment;
    private readonly Dictionary<string, EvidenceProp> targets = new();
    private Canvas canvas;
    private RectTransform marker;
    private PhotographyGuideGraphic icon;
    private Text label;
    private Material overlayMaterial;
    private string lastObservation;
    private string lastInstruction;
    private float nextLabelTime;
    private bool wasPin, lastSurveyZone;

    public bool IsVisible => canvas != null && canvas.enabled && canvas.gameObject.activeInHierarchy;
    public bool IsPin { get; private set; }
    public bool IsReady { get; private set; }
    public string TargetObservationId { get; private set; }
    public Vector3 TargetPosition { get; private set; }
    public Vector2 MarkerViewport { get; private set; }
    public string GuidanceText => label != null ? label.text : string.Empty;
    public Canvas GuideCanvas => canvas;

    [EditableSceneBuilder]
    private void Awake()
    {
        using (EditableSceneVisuals.Begin(this,"Awake"))
        {
        assessment = GetComponent<IOCAssessmentInteraction>();
        var shader = Resources.Load<Shader>("PhotographyGuideOverlay");
        if (shader == null) { enabled = false; Debug.LogError("IOC guide shader is missing.", this); return; }
        overlayMaterial = new Material(shader) { name = "IOC direction guide overlay", hideFlags = HideFlags.HideAndDontSave };
        var go = EditableSceneVisuals.Create("IOC direction guide", typeof(RectTransform), typeof(Canvas));
        go.transform.SetParent(transform, false);
        canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 90; canvas.enabled = false;
        ((RectTransform)go.transform).sizeDelta = new Vector2(420, 180);
        go.transform.localScale = Vector3.one * .0015f;
        var glyph = EditableSceneVisuals.Create("Arrow or location pin", typeof(RectTransform), typeof(PhotographyGuideGraphic));
        marker = (RectTransform)glyph.transform; marker.SetParent(go.transform, false); marker.sizeDelta = new Vector2(64, 64);
        icon = glyph.GetComponent<PhotographyGuideGraphic>(); icon.material = overlayMaterial; icon.raycastTarget = false;
        var backing = EditableSceneVisuals.Create("Guidance label background", typeof(RectTransform), typeof(Image));
        backing.transform.SetParent(go.transform, false);
        var backgroundRect = (RectTransform)backing.transform;
        backgroundRect.sizeDelta = new Vector2(420, 84); backgroundRect.anchoredPosition = new Vector2(0, -65);
        var image = backing.GetComponent<Image>(); image.material = overlayMaterial;
        image.color = new Color(.025f, .05f, .075f, .88f); image.raycastTarget = false;
        var text = EditableSceneVisuals.Create("IOC guidance", typeof(RectTransform), typeof(Text));
        text.transform.SetParent(go.transform, false);
        var rect = (RectTransform)text.transform;
        rect.sizeDelta = new Vector2(400, 76); rect.anchoredPosition = new Vector2(0, -65);
        label = text.GetComponent<Text>(); label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.material = overlayMaterial; label.fontSize = 24; label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white; label.raycastTarget = false; label.supportRichText = false;
        var shadow = EditableSceneVisuals.Component<Shadow>(text); shadow.effectColor = new Color(0, 0, 0, .95f); shadow.effectDistance = new Vector2(1.5f, -1.5f);
            }
    }

    private void LateUpdate() => Refresh();

    /// <summary>Never observes, logs, consumes input, moves scene objects or completes a phase.</summary>
    public void Refresh()
    {
        if (canvas == null) return;
        var cam = Camera.main;
        bool show = isActiveAndEnabled && assessment != null && assessment.isActiveAndEnabled
            && assessment.Definition != null && cam != null && SceneTurnover.IsGated
            && SceneTurnover.Phase == ScenarioPhase.IOCAssessment && SceneTurnover.EntryAccessReady && !assessment.IsChecklistVisible
            && !PlayerUIGate.IsBlocked
            && (STCSNotificationUI.Instance == null || (!STCSNotificationUI.Instance.IsShowing && STCSNotificationUI.Instance.PendingCount == 0));
        var observation = show ? NextObservation() : null;
        canvas.enabled = observation != null;
        if (!canvas.enabled) { ResetTarget(); return; }

        bool survey = observation.kind == IOCObservationKind.SceneSurvey;
        bool inSurveyZone = survey && InSurveyZone(cam.transform.position);
        bool changed = lastObservation != observation.observationId || lastSurveyZone != inSurveyZone;
        if (changed) wasPin = false;
        lastObservation = TargetObservationId = observation.observationId; lastSurveyZone = inSurveyZone;
        string instruction, name;
        if (survey)
        {
            var point = assessment.ObservePoint;
            Vector3 forward = point != null ? Vector3.ProjectOnPlane(point.forward, Vector3.up).normalized : Vector3.zero;
            TargetPosition = inSurveyZone
                ? (point != null ? cam.transform.position + forward * 3f : assessment.Definition.surveyLookAt)
                : (point != null ? point.position + Vector3.up * .12f : assessment.Definition.surveyArea.center);
            instruction = inSurveyZone ? "Survey the scene" : "Investigate here";
            name = "Initial scene survey";
        }
        else
        {
            var prop = EvidenceTarget(observation.evidenceId);
            if (prop == null || !prop.gameObject.activeInHierarchy) { canvas.enabled = false; ResetTarget(); return; }
            TargetPosition = prop.transform.position + observation.worldOffset;
            instruction = Vector3.Distance(cam.transform.position, TargetPosition) > assessment.Definition.observationDistance
                ? "Move closer" : "Examine this";
            name = EvidenceDisplay.Label(observation.evidenceId);
        }
        IsReady = assessment.ObservationAtGaze() == observation.observationId;
        var view = cam.WorldToViewportPoint(TargetPosition);
        MarkerViewport = ScenePhotographyGuide.Project(view, cam.transform.InverseTransformPoint(TargetPosition), wasPin, out bool pin, out var direction);
        IsPin = wasPin = pin;
        canvas.transform.SetPositionAndRotation(cam.ViewportToWorldPoint(new Vector3(MarkerViewport.x, MarkerViewport.y, displayDistance)), cam.transform.rotation);
        icon.SetState(pin, IsReady);
        marker.localRotation = Quaternion.Euler(0, 0, pin ? 0 : Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
        Vector3 distance = cam.transform.position - TargetPosition;
        if (survey && !inSurveyZone) distance.y = 0;
        // While surveying, the forward point is an orientation cue, not a destination distance.
        if (changed || lastInstruction != instruction || Time.unscaledTime >= nextLabelTime)
        {
            nextLabelTime = Time.unscaledTime + .15f;
            lastInstruction = instruction;
            label.text = instruction + (survey && inSurveyZone ? "" : " \u00b7 " + distance.magnitude.ToString("0.0") + " m") + "\n" + name;
        }
    }

    private IOCObservationDefinition NextObservation()
    {
        var definitions = assessment.Definition.observations;
        var done = SceneTurnover.IOCObservations;
        foreach (var observation in definitions)
            if (observation.kind == IOCObservationKind.SceneSurvey && !Contains(done, observation.observationId)) return observation;
        foreach (var observation in definitions)
            if (observation.kind == IOCObservationKind.Victim && !Contains(done, observation.observationId) && EvidenceTarget(observation.evidenceId) != null) return observation;
        if (SceneTurnover.PossibleEvidenceObserved < assessment.Definition.minimumPossibleEvidence)
            foreach (var observation in definitions)
                if (observation.kind == IOCObservationKind.PossibleEvidence && !Contains(done, observation.observationId) && EvidenceTarget(observation.evidenceId) != null) return observation;
        return null;
    }
    private static bool Contains(IReadOnlyCollection<string> observations, string id)
    {
        foreach (var observation in observations) if (observation == id) return true;
        return false;
    }
    private EvidenceProp EvidenceTarget(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (targets.TryGetValue(id, out var prop) && prop != null) return prop;
        foreach (var candidate in FindObjectsByType<EvidenceProp>())
            if (!string.IsNullOrEmpty(candidate.evidenceId)) targets[candidate.evidenceId] = candidate;
        return targets.TryGetValue(id, out prop) ? prop : null;
    }
    private bool InSurveyZone(Vector3 eye)
    {
        if (assessment.ObservePoint == null) return assessment.Definition.surveyArea.Contains(eye);
        Vector3 offset = eye - assessment.ObservePoint.position; offset.y = 0;
        return offset.sqrMagnitude <= assessment.ObserveRadius * assessment.ObserveRadius;
    }
    private void ResetTarget() { wasPin = false; lastObservation = TargetObservationId = lastInstruction = null; IsPin = IsReady = false; }
    private void OnDisable() { if (canvas != null) canvas.enabled = false; ResetTarget(); }
    private void OnDestroy() { if (overlayMaterial != null) Destroy(overlayMaterial); }
}
