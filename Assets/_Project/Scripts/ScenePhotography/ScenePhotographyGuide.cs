using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Read-only guidance for the active scene photo: stand here, then face this view.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(21000)]
public sealed class ScenePhotographyGuide : MonoBehaviour
{
    [SerializeField, Min(.8f)] private float displayDistance = 1.6f;
    private ScenePhotographyManager manager;
    private PhotographTool cameraTool;
    private Canvas canvas;
    private RectTransform marker;
    private PhotographyGuideGraphic icon;
    private Text label;
    private Material overlayMaterial;
    private ScenePhotoPoint lastPoint;
    private bool wasPin, wasInZone;
    private float nextLabelTime;

    public bool IsVisible => canvas != null && canvas.enabled && canvas.gameObject.activeInHierarchy;
    public bool IsPin { get; private set; }
    public bool IsFramingTarget { get; private set; }
    public Vector3 TargetPosition { get; private set; }
    public Vector2 MarkerViewport { get; private set; }
    public string TargetPointId { get; private set; }
    public Canvas GuideCanvas => canvas;

    [EditableSceneBuilder]
    private void Awake()
    {
        using (EditableSceneVisuals.Begin(this,"Awake"))
        {
        manager = GetComponent<ScenePhotographyManager>();
        cameraTool = FindFirstObjectByType<PhotographTool>(FindObjectsInactive.Include);
        var shader = Resources.Load<Shader>("PhotographyGuideOverlay");
        if (shader == null) { enabled = false; Debug.LogError("Photography guide shader is missing.",this); return; }
        overlayMaterial = new Material(shader) { name = "Photography guide overlay", hideFlags = HideFlags.HideAndDontSave };
        var go = EditableSceneVisuals.Create("Photography direction guide",typeof(RectTransform),typeof(Canvas));
        go.transform.SetParent(transform,false);
        canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 90; canvas.enabled = false;
        ((RectTransform)go.transform).sizeDelta = new Vector2(420,180);
        go.transform.localScale = Vector3.one * .0015f;
        var glyph = EditableSceneVisuals.Create("Arrow or location pin",typeof(RectTransform),typeof(PhotographyGuideGraphic));
        marker = (RectTransform)glyph.transform; marker.SetParent(go.transform,false); marker.sizeDelta = new Vector2(64,64);
        icon = glyph.GetComponent<PhotographyGuideGraphic>(); icon.material = overlayMaterial; icon.raycastTarget = false;
        var backing = EditableSceneVisuals.Create("Guidance label background",typeof(RectTransform),typeof(Image));
        backing.transform.SetParent(go.transform,false);
        var backingRect = (RectTransform)backing.transform; backingRect.sizeDelta = new Vector2(420,84); backingRect.anchoredPosition = new Vector2(0,-65);
        var backingImage = backing.GetComponent<Image>(); backingImage.material = overlayMaterial;
        backingImage.color = new Color(.025f,.05f,.075f,.88f); backingImage.raycastTarget = false;
        var text = EditableSceneVisuals.Create("Photo guidance",typeof(RectTransform),typeof(Text));
        text.transform.SetParent(go.transform,false);
        var rect = (RectTransform)text.transform; rect.sizeDelta = new Vector2(400,76); rect.anchoredPosition = new Vector2(0,-65);
        label = text.GetComponent<Text>(); label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.material = overlayMaterial; label.fontSize = 24; label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white; label.raycastTarget = false; label.supportRichText = false;
        var shadow = EditableSceneVisuals.Component<Shadow>(text); shadow.effectColor = new Color(0,0,0,.95f); shadow.effectDistance = new Vector2(1.5f,-1.5f);
            }
    }

    private void LateUpdate() => Refresh();

    /// <summary>Also used by focused diagnostics; never changes route or capture eligibility.</summary>
    public void Refresh()
    {
        if (canvas == null) return;
        var point = manager != null && manager.isActiveAndEnabled ? manager.CurrentPoint : null;
        var cam = Camera.main;
        bool show = point != null && point.isActiveAndEnabled && !point.IsCompleted && cam != null
            && !SceneTurnover.IsInvestigationBlocked && !ProcedureInterlock.BlocksUnrelatedActions && !manager.IsScenePhotographyComplete
            && !PlayerUIGate.IsBlockedByOther(cameraTool);
        canvas.enabled = show;
        if (!show) { wasPin = false; TargetPointId = null; return; }
        bool inZone = point.ContainsPlayer(cam.transform.position);
        bool changed = lastPoint != point || wasInZone != inZone;
        if (changed) wasPin = false;
        lastPoint = point; wasInZone = inZone; TargetPointId = point.photoPointId;
        IsFramingTarget = inZone;
        TargetPosition = inZone
            ? (point.cameraTarget != null ? point.cameraTarget.position : cam.transform.position + point.transform.forward * 5f)
            : point.transform.position + Vector3.up * .12f;
        var view = cam.WorldToViewportPoint(TargetPosition);
        var local = cam.transform.InverseTransformPoint(TargetPosition);
        MarkerViewport = Project(view,local,wasPin,out var pin,out var direction);
        IsPin = wasPin = pin;
        canvas.transform.SetPositionAndRotation(cam.ViewportToWorldPoint(new Vector3(MarkerViewport.x,MarkerViewport.y,displayDistance)),cam.transform.rotation);
        icon.SetState(pin, inZone && point.AngleFrom(cam.transform) <= point.angleTolerance);
        marker.localRotation = Quaternion.Euler(0,0,pin ? 0 : Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg-90f);
        if (changed || Time.unscaledTime >= nextLabelTime)
        {
            nextLabelTime = Time.unscaledTime + .15f;
            var name = string.IsNullOrWhiteSpace(point.displayName) ? "Scene photograph" : point.displayName;
            var distance = cam.transform.position-TargetPosition;
            if(!inZone)distance.y=0;
            label.text = (inZone ? "Face this view" : "Stand here") + " · " + distance.magnitude.ToString("0.0") + " m\n" + name;
        }
    }

    /// <summary>Viewport edge clamp with stable left/right directions even behind the head.</summary>
    public static Vector2 Project(Vector3 viewport, Vector3 cameraLocal, bool previousPin, out bool pin, out Vector2 direction)
    {
        var edge = new Vector2(.34f,.28f); // inset from the headset's physical display edges
        var p = new Vector2(viewport.x-.5f,viewport.y-.5f);
        float hysteresis = previousPin ? .018f : -.018f;
        pin = viewport.z > .01f && Mathf.Abs(p.x) <= edge.x+hysteresis && Mathf.Abs(p.y) <= edge.y+hysteresis;
        if (pin) { direction = Vector2.up; return p + Vector2.one*.5f; }
        // Projection flips behind the eye. Use yaw/pitch there instead of the mirrored viewport.
        var unit = cameraLocal.normalized;
        direction = cameraLocal.z > .001f ? p : new Vector2(
            Mathf.Atan2(Mathf.Abs(unit.x)<.01f ? .01f : unit.x,unit.z),
            Mathf.Atan2(cameraLocal.y,new Vector2(cameraLocal.x,cameraLocal.z).magnitude));
        if (direction.sqrMagnitude < .00001f) direction = Vector2.right;
        direction.Normalize();
        float scale = 1f/Mathf.Max(Mathf.Abs(direction.x)/edge.x,Mathf.Abs(direction.y)/edge.y);
        return direction*scale + Vector2.one*.5f;
    }

    /// <summary>Synchronous capture scope; also restores correctly if rendering throws.</summary>
    public IDisposable HideForCapture() => new CaptureScope(canvas);
    private sealed class CaptureScope : IDisposable
    {
        private Canvas target; private readonly bool previous;
        public CaptureScope(Canvas value) { target=value; previous=value!=null&&value.enabled; if(value!=null)value.enabled=false; }
        public void Dispose() { if(target!=null)target.enabled=previous; target=null; }
    }
    private void OnDisable() { if(canvas!=null)canvas.enabled=false; }
    private void OnDestroy() { if(overlayMaterial!=null)Destroy(overlayMaterial); }
}
