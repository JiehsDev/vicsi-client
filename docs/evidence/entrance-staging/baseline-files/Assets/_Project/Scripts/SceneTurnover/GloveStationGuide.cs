using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Glove-table destination and authored exit trail; reads state, owns no input.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(21000)]
public sealed class GloveStationGuide : MonoBehaviour
{
    private ArrivalPPEStation station;
    private RouteData route;
    [Serializable] public class RouteData { public string sceneName; public Vector3[] waypoints; public float arrowSpacing=.8f, arrowWidth=.28f, arrowLength=.36f; }
    private Canvas pinCanvas;
    private RectTransform pin;
    private PhotographyGuideGraphic pinIcon;
    private UnityEngine.UI.Text pinLabel;
    private Material pinMaterial;
    private bool wasPin;
    public bool IsPinVisible => pinCanvas != null && pinCanvas.enabled;
    public Canvas WaypointCanvas => pinCanvas;
    private SamplePreparationStation Preparation => SamplePreparationStation.Instance;
    private bool Preparing => Preparation != null && Preparation.GuidanceNeeded;
    private FingerprintComparisonStation Comparison => FingerprintComparisonStation.Instance;
    private bool Comparing => !Preparing && !station.GuidanceNeeded && Comparison!=null && Comparison.GuidanceNeeded;
    private Transform Destination => Preparing ? Preparation.transform : Comparing?Comparison.transform:station.transform;
    private bool InDestinationRange => Preparing ? Preparation.IsInRange : Comparing?Comparison.IsInRange:station.IsInRange;
    public bool DestinationActive => station != null && (Preparing || station.GuidanceNeeded || Comparing) && isActiveAndEnabled;
    private Mesh mesh;
    private MeshRenderer visual;
    private Material material;
    private int worldMask;
    private float nextRefresh;
    private readonly List<Vector3> vertices = new();
    private readonly List<Color32> colors = new();
    private readonly List<int> triangles = new();
    private readonly List<Vector3> arrowPositions = new();
    private readonly List<Vector3> arrowDirections = new();
    private static readonly List<GloveStationGuide> active = new();
    private static int captureDepth;
    private static readonly Vector2[] glyph = { new(-1,-1), new(0,-.2f), new(1,-1), new(1,0), new(0,1), new(-1,0) };
    private static readonly int[] glyphTriangles = {0,1,5,1,4,5,1,3,4,1,2,3};
    public bool IsVisible => visual != null && visual.enabled && visual.gameObject.activeInHierarchy;
    public IReadOnlyList<Vector3> ArrowPositions => arrowPositions;
    public IReadOnlyList<Vector3> ArrowDirections => arrowDirections;
    public RouteData Route => route;
    public GameObject VisualObject => visual != null ? visual.gameObject : null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { active.Clear(); captureDepth = 0; }
    private void Awake()
    {
        station = GetComponent<ArrivalPPEStation>();
        var json=Resources.Load<TextAsset>("GloveStationRoute_CSI");
        route=json!=null?JsonUtility.FromJson<RouteData>(json.text):null;
        var shader = Resources.Load<Shader>("EvidenceDiscoveryDot"); // Existing depth-tested, stereo-safe vertex-color cue shader.
        if (route == null || shader == null || station == null || gameObject.scene.name != route.sceneName)
        { enabled = false; return; }
        var controller = FindFirstObjectByType<Oculus.Interaction.Locomotion.CharacterController>();
        worldMask = controller != null ? controller.LayerMask.value : Physics.DefaultRaycastLayers;
        worldMask &= ~(1 << 2);
        material = new Material(shader) { name = "Glove table path arrows", hideFlags = HideFlags.HideAndDontSave };
        mesh = new Mesh { name = "Glove table breadcrumb trail", hideFlags = HideFlags.HideAndDontSave };
        mesh.MarkDynamic();
        var go = new GameObject("Glove table ground-arrow trail", typeof(MeshFilter), typeof(MeshRenderer));
        go.hideFlags = HideFlags.DontSave; go.layer = 2;
        // World-space root avoids inheriting an NPC model's rotation or imported scale.
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        visual = go.GetComponent<MeshRenderer>(); visual.sharedMaterial = material; visual.enabled = false;
        visual.shadowCastingMode = ShadowCastingMode.Off; visual.receiveShadows = false;
        visual.lightProbeUsage = LightProbeUsage.Off; visual.reflectionProbeUsage = ReflectionProbeUsage.Off;
        BuildPin();
    }
    private void OnEnable() { if (!active.Contains(this)) active.Add(this); }
    private void OnDisable() { active.Remove(this); if (visual != null) visual.enabled = false; if(pinCanvas!=null) pinCanvas.enabled=false; }
    private void OnDestroy()
    {
        if(pinMaterial!=null) Destroy(pinMaterial);
        if (visual != null) Destroy(visual.gameObject);
        if (material != null) Destroy(material);
        if (mesh != null) Destroy(mesh);
    }
    private void LateUpdate()
    {
        // Visibility gates are checked every frame. Geometry is refreshed at most five times/second.
        Refresh(Time.unscaledTime >= nextRefresh);
    }
    public void Refresh() => Refresh(true);
    private void Refresh(bool rebuild)
    {
        if (visual == null) return;
        var head = Camera.main;
        bool show = DestinationActive && head != null && !PlayerUIGate.IsBlocked && captureDepth == 0
            && !ToolInputLock.HasUIOwners
            && (STCSNotificationUI.Instance == null || (!STCSNotificationUI.Instance.IsShowing && STCSNotificationUI.Instance.PendingCount == 0));
        // The physical sign and local action prompt take over at the table.
        if(pinCanvas!=null) pinCanvas.enabled=show && !InDestinationRange;
        if (!show) { visual.enabled = false; return; }
        UpdatePin(head);
        if(InDestinationRange) { visual.enabled=false; return; }
        if (rebuild || !visual.enabled)
        {
            BuildTrail(head.transform.position);
            nextRefresh = Time.unscaledTime + .2f;
        }
        visual.enabled = arrowPositions.Count > 0;
    }
    private void BuildTrail(Vector3 head)
    {
        vertices.Clear(); colors.Clear(); triangles.Clear(); arrowPositions.Clear(); arrowDirections.Clear();
        var points = new Vector3[route.waypoints.Length+(Comparing?4:1)];
        Array.Copy(route.waypoints,points,route.waypoints.Length);
        if(Comparing)
        {
            // Walk around the outside supplies and drying cabinet, never through
            // their colliders or the controlled entrance approach.
            points[route.waypoints.Length]=new Vector3(route.waypoints[route.waypoints.Length-1].x,Destination.position.y,-3.15f);
            points[route.waypoints.Length+1]=new Vector3(Destination.position.x,Destination.position.y,-3.15f);
            points[route.waypoints.Length+2]=Destination.position-Destination.forward*1.35f;
        }
        points[points.Length-1]=Destination.position-Destination.forward*.85f;
        float best = float.PositiveInfinity;
        int first = -1; Vector3 entry = default;
        // Join only an unobstructed nearby section of the authored exit route. Never draw a direct shortcut through walls.
        for (int i = 0; points != null && i + 1 < points.Length; i++)
        {
            Vector3 delta = points[i + 1] - points[i];
            Vector3 flat = delta; flat.y = 0;
            Vector3 from = head - points[i]; from.y = 0;
            float t = flat.sqrMagnitude > .0001f ? Mathf.Clamp01(Vector3.Dot(from, flat) / flat.sqrMagnitude) : 0;
            Vector3 at = points[i] + delta * t;
            float height = head.y - at.y;
            if (height < -.3f || height > 2.7f) continue;
            Vector3 source = new Vector3(head.x, at.y + .9f, head.z);
            Vector3 target = at + Vector3.up * .9f;
            Vector3 direction = target - source;
            float distance = direction.magnitude;
            if (distance > 4f || (distance > .01f && Physics.SphereCast(source, .24f, direction / distance,
                out _, distance, worldMask, QueryTriggerInteraction.Ignore))) continue;
            float cost = distance + Mathf.Abs(height - 1.6f) * .25f;
            if (cost >= best) continue;
            best = cost; first = i; entry = at;
        }
        if (first >= 0)
        {
            Vector3 start = new Vector3(head.x, entry.y, head.z);
            float untilNext = .6f;
            AddLeg(start, entry, ref untilNext);
            start = entry;
            for (int i = first + 1; i < points.Length; i++)
            { AddLeg(start, points[i], ref untilNext); start = points[i]; }
        }
        mesh.Clear(); mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
    }
    private void AddLeg(Vector3 start, Vector3 end, ref float untilNext)
    {
        Vector3 delta = end - start;
        float length = delta.magnitude;
        if (length < .001f) return;
        Vector3 direction = delta / length;
        for (; untilNext <= length; untilNext += Mathf.Max(.4f, route.arrowSpacing))
        {
            Vector3 at = start + direction * untilNext;
            Vector3 tableOffset = at - Destination.position; tableOffset.y = 0;
            if (tableOffset.magnitude <= .8f) continue;
            if (!Physics.Raycast(at + Vector3.up * .5f, Vector3.down, out var ground, 1f, worldMask, QueryTriggerInteraction.Ignore)
                || ground.normal.y < .9f) continue;
            at.y = ground.point.y + .035f;
            if (Physics.CheckSphere(at + Vector3.up * .2f, .13f, worldMask, QueryTriggerInteraction.Ignore)) continue;
            Vector3 forward = direction; forward.y = 0; forward.Normalize();
            arrowPositions.Add(at); arrowDirections.Add(forward);
            Arrow(at, forward, 1.18f, new Color32(8, 23, 34, 245));
            Arrow(at + Vector3.up * .002f, forward, 1f, new Color32(90, 222, 255, 255));
        }
        untilNext -= length;
    }
    private void Arrow(Vector3 center, Vector3 forward, float scale, Color32 color)
    {
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        float w = route.arrowWidth * .5f * scale, h = route.arrowLength * .5f * scale;
        int offset = vertices.Count;
        // Concave chevron, split into four triangles; no texture, pulse, input or collider.
        foreach (var p in glyph)
        { vertices.Add(center + right * (p.x * w) + forward * (p.y * h)); colors.Add(color); }
        foreach (int i in glyphTriangles) triangles.Add(offset + i);
    }
    private void BuildPin()
    {
        pinMaterial=new Material(Resources.Load<Shader>("PhotographyGuideOverlay")) { name="Glove table waypoint" };
        var go=new GameObject("Clean gloves outside waypoint",typeof(RectTransform),typeof(Canvas));
        go.transform.SetParent(transform,false);go.transform.localScale=Vector3.one*.0015f;
        pinCanvas=go.GetComponent<Canvas>();pinCanvas.renderMode=RenderMode.WorldSpace;pinCanvas.sortingOrder=91;pinCanvas.enabled=false;
        ((RectTransform)go.transform).sizeDelta=new Vector2(440,190);
        var glyph=new GameObject("Location pin or edge arrow",typeof(RectTransform),typeof(PhotographyGuideGraphic));
        pin=(RectTransform)glyph.transform;pin.SetParent(go.transform,false);pin.sizeDelta=new Vector2(64,64);
        pinIcon=glyph.GetComponent<PhotographyGuideGraphic>();pinIcon.material=pinMaterial;pinIcon.raycastTarget=false;
        var bg=new GameObject("Label background",typeof(RectTransform),typeof(UnityEngine.UI.Image));bg.transform.SetParent(go.transform,false);
        ((RectTransform)bg.transform).sizeDelta=new Vector2(440,105);((RectTransform)bg.transform).anchoredPosition=new Vector2(0,-75);
        var image=bg.GetComponent<UnityEngine.UI.Image>();image.material=pinMaterial;image.color=new Color(.025f,.05f,.075f,.94f);image.raycastTarget=false;
        var label=new GameObject("Guidance",typeof(RectTransform),typeof(UnityEngine.UI.Text));label.transform.SetParent(bg.transform,false);
        ((RectTransform)label.transform).sizeDelta=new Vector2(420,95);pinLabel=label.GetComponent<UnityEngine.UI.Text>();
        pinLabel.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");pinLabel.material=pinMaterial;pinLabel.fontSize=24;
        pinLabel.color=Color.white;pinLabel.alignment=TextAnchor.MiddleCenter;pinLabel.raycastTarget=false;
    }
    private void UpdatePin(Camera head)
    {
        var target=Destination.position+Vector3.up*1.1f;
        var position=ScenePhotographyGuide.Project(head.WorldToViewportPoint(target),head.transform.InverseTransformPoint(target),wasPin,out bool isPin,out var direction);
        wasPin=isPin;pinCanvas.transform.SetPositionAndRotation(head.ViewportToWorldPoint(new Vector3(position.x,position.y,1.6f)),head.transform.rotation);
        pin.localRotation=Quaternion.Euler(0,0,isPin?0:Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg-90f);
        pinIcon.SetState(isPin,InDestinationRange);
        bool occupied=HeldItemCoordinator.Instance!=null&&HeldItemCoordinator.Instance.CurrentCategory!=HeldItemCoordinator.HeldCategory.None;
        pinLabel.text=(Preparing?GameDialogue.Text("sample-destination"):Comparing?GameDialogue.Text("fp-practice-destination"):station.DestinationText)+"\n"
            +(InDestinationRange?(Preparing?Preparation.Instruction:Comparing?GameDialogue.Text("fp-practice-open"):occupied?GameDialogue.Text("gloves-holster"):station.Instruction):GameDialogue.Text("gloves-follow-route"));
    }
    public static IDisposable HideForCapture() => new CaptureScope();
    private sealed class CaptureScope : IDisposable
    {
        private bool disposed;
        public CaptureScope() { captureDepth++; foreach (var guide in active) if (guide != null) { if(guide.visual!=null) guide.visual.enabled=false; if(guide.pinCanvas!=null) guide.pinCanvas.enabled=false; } }
        public void Dispose()
        {
            if (disposed) return; disposed = true; captureDepth = Mathf.Max(0, captureDepth - 1);
            foreach (var guide in active) if (guide != null) guide.Refresh();
        }
    }
}
