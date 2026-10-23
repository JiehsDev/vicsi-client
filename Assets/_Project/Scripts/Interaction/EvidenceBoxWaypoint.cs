using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Read-only destination cue for a ready custody pass; owns no input or evidence state.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(21000)]
public sealed class EvidenceBoxWaypoint : MonoBehaviour
{
    private EvidenceBoxDepositZone box;
    private Canvas canvas;
    private RectTransform icon;
    private PhotographyGuideGraphic graphic;
    private Text label;
    private Material material;
    private static readonly List<EvidenceBoxWaypoint> active = new();
    private static int captureDepth;
    public bool IsVisible => canvas != null && canvas.enabled && canvas.gameObject.activeInHierarchy && isActiveAndEnabled;
    public bool IsPin { get; private set; }
    public Vector2 MarkerViewport { get; private set; }
    public Canvas WaypointCanvas => canvas;
    public bool DestinationActive => isActiveAndEnabled && box != null && box.isActiveAndEnabled && box.ReadyForTransfer
        && (SceneTurnover.Phase != ScenarioPhase.SOCOInvestigation || EndingManager.Instance?.FeedbackAccepted == true);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { active.Clear(); captureDepth = 0; }
    [EditableSceneBuilder]
    private void Awake()
    {
        using (EditableSceneVisuals.Begin(this,"Awake"))
        {
        box = GetComponent<EvidenceBoxDepositZone>();
        var shader = Resources.Load<Shader>("PhotographyGuideOverlay");
        if (box == null || shader == null) { enabled = false; return; }
        material = new Material(shader) { name = "Evidence box waypoint", hideFlags = HideFlags.HideAndDontSave };
        var root = EditableSceneVisuals.Create("Evidence box destination", typeof(RectTransform), typeof(Canvas));
        // A world-space root avoids shear when the imported crate has nonuniform scale.
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, gameObject.scene);
        root.hideFlags = HideFlags.DontSave;
        root.transform.localScale = Vector3.one * .0015f;
        ((RectTransform)root.transform).sizeDelta = new Vector2(440, 190);
        canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 91; canvas.enabled = false;
        var glyph = EditableSceneVisuals.Create("Location pin or edge arrow", typeof(RectTransform), typeof(PhotographyGuideGraphic));
        icon = (RectTransform)glyph.transform; icon.SetParent(root.transform, false); icon.sizeDelta = new Vector2(64, 64);
        graphic = glyph.GetComponent<PhotographyGuideGraphic>(); graphic.material = material; graphic.raycastTarget = false;
        var background = EditableSceneVisuals.Create("Waypoint label", typeof(RectTransform), typeof(Image)); background.transform.SetParent(root.transform, false);
        ((RectTransform)background.transform).sizeDelta = new Vector2(440, 105);
        ((RectTransform)background.transform).anchoredPosition = new Vector2(0, -75);
        var image = background.GetComponent<Image>(); image.material = material; image.color = new Color(.025f, .05f, .075f, .94f); image.raycastTarget = false;
        var text = EditableSceneVisuals.Create("Evidence box guidance", typeof(RectTransform), typeof(Text)); text.transform.SetParent(background.transform, false);
        ((RectTransform)text.transform).sizeDelta = new Vector2(420, 95);
        label = text.GetComponent<Text>(); label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.material = material;
        label.fontSize = 24; label.color = Color.white; label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false;
            }
    }
    private void OnEnable() { if (!active.Contains(this)) active.Add(this); }
    private void OnDisable() { active.Remove(this); if (canvas != null) canvas.enabled = false; }
    private void OnDestroy() { if (canvas != null) Destroy(canvas.gameObject); if (material != null) Destroy(material); }
    private void LateUpdate() => Refresh();
    public void Refresh()
    {
        if (canvas == null) return;
        var head = Camera.main;
        var dialogue = STCSNotificationUI.Instance;
        canvas.enabled = DestinationActive && head != null && !box.PlayerInRange && captureDepth == 0
            && !PlayerUIGate.IsBlocked && !ToolInputLock.HasUIOwners
            && (dialogue == null || (!dialogue.IsShowing && dialogue.PendingCount == 0));
        if (!canvas.enabled) { IsPin = false; return; }
        var target = box.transform.position + Vector3.up * .48f;
        MarkerViewport = ScenePhotographyGuide.Project(head.WorldToViewportPoint(target), head.transform.InverseTransformPoint(target), IsPin, out var pin, out var direction);
        IsPin = pin;
        canvas.transform.SetPositionAndRotation(head.ViewportToWorldPoint(new Vector3(MarkerViewport.x, MarkerViewport.y, 1.6f)), head.transform.rotation);
        icon.localRotation = Quaternion.Euler(0, 0, pin ? 0 : Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
        graphic.SetState(pin, false);
        var distance = Vector2.Distance(new Vector2(head.transform.position.x, head.transform.position.z), new Vector2(target.x, target.z));
        label.text = "Go to evidence box\nTransfer stored evidence · " + distance.ToString("0") + " m";
    }
    public static IDisposable HideForCapture() => new CaptureScope();
    private sealed class CaptureScope : IDisposable
    {
        private bool disposed;
        public CaptureScope() { captureDepth++; foreach (var guide in active) if (guide != null && guide.canvas != null) guide.canvas.enabled = false; }
        public void Dispose()
        {
            if (disposed) return; disposed = true; captureDepth = Mathf.Max(0, captureDepth - 1);
            foreach (var guide in active) if (guide != null) guide.Refresh();
        }
    }
}
