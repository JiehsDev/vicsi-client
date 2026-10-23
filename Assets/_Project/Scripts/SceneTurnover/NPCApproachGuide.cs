using UnityEngine;
using UnityEngine.UI;

/// <summary>Read-only navigation to the existing FR handover and IOC briefing. Owns no input or progression.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(21000)]
public sealed class NPCApproachGuide : MonoBehaviour
{
    [SerializeField, Min(.8f)] private float displayDistance = 1.6f;
    private NPCPresentation actor;
    private SceneCordon cordon;
    private Canvas canvas;
    private RectTransform marker;
    private PhotographyGuideGraphic icon;
    private Text label;
    private Material overlayMaterial;
    private bool wasPin;
    private string lastInstruction;
    private float nextLabelTime;

    public bool IsVisible => canvas != null && canvas.enabled && canvas.gameObject.activeInHierarchy;
    public bool IsPin { get; private set; }
    public bool IsReady { get; private set; }
    public Vector3 TargetPosition { get; private set; }
    public Vector2 MarkerViewport { get; private set; }
    public string GuidanceText => label != null ? label.text : string.Empty;
    public Canvas GuideCanvas => canvas;

    [EditableSceneBuilder]
    private void Awake()
    {
        using (EditableSceneVisuals.Begin(this,"Awake"))
        {
        actor = GetComponent<NPCPresentation>();
        cordon = FindFirstObjectByType<SceneCordon>();
        var shader = Resources.Load<Shader>("PhotographyGuideOverlay");
        if (shader == null) { enabled = false; Debug.LogError("NPC approach guide shader is missing.", this); return; }
        overlayMaterial = new Material(shader) { name = "NPC approach guide overlay", hideFlags = HideFlags.HideAndDontSave };
        var go = EditableSceneVisuals.Create("NPC approach guide", typeof(RectTransform), typeof(Canvas));
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
        var br = (RectTransform)backing.transform; br.sizeDelta = new Vector2(420, 84); br.anchoredPosition = new Vector2(0, -65);
        var image = backing.GetComponent<Image>(); image.material = overlayMaterial;
        image.color = new Color(.025f, .05f, .075f, .88f); image.raycastTarget = false;
        var text = EditableSceneVisuals.Create("NPC guidance", typeof(RectTransform), typeof(Text));
        text.transform.SetParent(go.transform, false);
        var rect = (RectTransform)text.transform; rect.sizeDelta = new Vector2(400, 76); rect.anchoredPosition = new Vector2(0, -65);
        label = text.GetComponent<Text>(); label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.material = overlayMaterial; label.fontSize = 24; label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white; label.raycastTarget = false; label.supportRichText = false;
        var shadow = EditableSceneVisuals.Component<Shadow>(text); shadow.effectColor = new Color(0, 0, 0, .95f); shadow.effectDistance = new Vector2(1.5f, -1.5f);
            }
    }

    private void LateUpdate() => Refresh();

    /// <summary>Only reads the NPC's existing phase, dialogue and proximity checks.</summary>
    public void Refresh()
    {
        if (canvas == null) return;
        var cam = Camera.main;
        bool show = isActiveAndEnabled && actor != null && actor.isActiveAndEnabled && actor.IsVisible
            && SceneTurnover.IsGated && cam != null && !PlayerUIGate.IsBlocked
            && (STCSNotificationUI.Instance == null || (!STCSNotificationUI.Instance.IsShowing && STCSNotificationUI.Instance.PendingCount == 0));
        bool ready = false;
        bool returningForBriefing = false;
        string name = null;
        if (show && actor.CharacterRole == NPCPresentation.Role.FirstResponder)
        {
            var responder = actor.Responder;
            show = SceneTurnover.IsPending && responder != null && responder.isActiveAndEnabled && !responder.IsTalking;
            ready = show && responder.PlayerInRange();
            name = "First Responder";
            returningForBriefing = show && cordon != null && cordon.RequiresBriefingAt(cam.transform.position);
        }
        else if (show && actor.CharacterRole == NPCPresentation.Role.IOC)
        {
            var workflow = actor.Workflow;
            show = SceneTurnover.Phase == ScenarioPhase.SOCOTurnover && workflow != null && workflow.isActiveAndEnabled
                && !workflow.IsTalking && !workflow.IsPanelVisible;
            ready = show && workflow.IsWithinIOCInteractionRadius;
            name = "IOC";
        }
        else show = false;
        canvas.enabled = show;
        if (!show) { ResetTarget(); return; }
        IsReady = ready;
        var anchor = actor.GuidanceAnchor;
        TargetPosition = anchor.position + (anchor == actor.transform ? Vector3.up * 1.6f : Vector3.zero);
        var view = cam.WorldToViewportPoint(TargetPosition);
        MarkerViewport = ScenePhotographyGuide.Project(view, cam.transform.InverseTransformPoint(TargetPosition), wasPin, out bool pin, out var direction);
        IsPin = wasPin = pin;
        canvas.transform.SetPositionAndRotation(cam.ViewportToWorldPoint(new Vector3(MarkerViewport.x, MarkerViewport.y, displayDistance)), cam.transform.rotation);
        marker.localRotation = Quaternion.Euler(0, 0, pin ? 0 : Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
        icon.SetState(pin, ready);
        string instruction = returningForBriefing ? GameDialogue.Text("fr-return-for-briefing") : ready ? (actor.CharacterRole == NPCPresentation.Role.FirstResponder ? "Speak with them" : "Receive briefing") : "Approach";
        if (instruction != lastInstruction || Time.unscaledTime >= nextLabelTime)
        {
            nextLabelTime = Time.unscaledTime + .15f; lastInstruction = instruction;
            Vector3 distance = cam.transform.position - TargetPosition; distance.y = 0;
            label.text = instruction + " \u00b7 " + distance.magnitude.ToString("0.0") + " m\n" + name;
        }
    }
    private void ResetTarget() { wasPin = false; IsPin = IsReady = false; lastInstruction = null; }
    private void OnDisable() { if (canvas != null) canvas.enabled = false; ResetTarget(); }
    private void OnDestroy() { if (overlayMaterial != null) Destroy(overlayMaterial); }
}
