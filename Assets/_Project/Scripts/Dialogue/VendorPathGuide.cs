using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Read-only ground breadcrumbs for the existing, confirmed knife-print vendor lead.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(21000)]
public sealed class VendorPathGuide : MonoBehaviour
{
    private VendorWitnessInteraction witness;
    private VendorPathRoute route;
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
    private static readonly List<VendorPathGuide> active = new();
    private static int captureDepth;
    private static readonly Vector2[] glyph = { new(-1,-1), new(0,-.2f), new(1,-1), new(1,0), new(0,1), new(-1,0) };
    private static readonly int[] glyphTriangles = {0,1,5,1,4,5,1,3,4,1,2,3};
    public bool IsVisible => visual != null && visual.enabled && visual.gameObject.activeInHierarchy;
    public IReadOnlyList<Vector3> ArrowPositions => arrowPositions;
    public IReadOnlyList<Vector3> ArrowDirections => arrowDirections;
    public VendorPathRoute Route => route;
    public GameObject VisualObject => visual != null ? visual.gameObject : null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { active.Clear(); captureDepth = 0; }
    [EditableSceneBuilder]
    private void Awake()
    {
        using (EditableSceneVisuals.Begin(this,"Awake"))
        {
        witness = GetComponent<VendorWitnessInteraction>();
        route = Resources.Load<VendorPathRoute>("VendorPathRoute_CSI");
        var shader = Resources.Load<Shader>("EvidenceDiscoveryDot"); // Existing depth-tested, stereo-safe vertex-color cue shader.
        if (route == null || shader == null || witness == null || gameObject.scene.name != route.sceneName)
        { enabled = false; return; }
        var controller = FindFirstObjectByType<Oculus.Interaction.Locomotion.CharacterController>();
        worldMask = controller != null ? controller.LayerMask.value : Physics.DefaultRaycastLayers;
        worldMask &= ~(1 << 2);
        material = new Material(shader) { name = "Vendor path arrows", hideFlags = HideFlags.HideAndDontSave };
        mesh = new Mesh { name = "Vendor breadcrumb trail", hideFlags = HideFlags.HideAndDontSave };
        mesh.MarkDynamic();
        var go = EditableSceneVisuals.Create("Vendor ground-arrow trail", typeof(MeshFilter), typeof(MeshRenderer));
        go.hideFlags = HideFlags.DontSave; go.layer = 2;
        // World-space root avoids inheriting an NPC model's rotation or imported scale.
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        visual = go.GetComponent<MeshRenderer>(); visual.sharedMaterial = material; visual.enabled = false;
        visual.shadowCastingMode = ShadowCastingMode.Off; visual.receiveShadows = false;
        visual.lightProbeUsage = LightProbeUsage.Off; visual.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
    }
    private void OnEnable() { if (!active.Contains(this)) active.Add(this); }
    private void OnDisable() { active.Remove(this); if (visual != null) visual.enabled = false; }
    private void OnDestroy()
    {
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
        var interview = witness != null ? witness.Interview : null;
        bool show = isActiveAndEnabled && witness != null && witness.isActiveAndEnabled && head != null
            && !ProcedureInterlock.BlocksUnrelatedActions
            && SceneTurnover.Phase == ScenarioPhase.SOCOInvestigation
            && interview != null && interview.isActiveAndEnabled && interview.FollowupUnlocked
            && !interview.IsDiscovered && !interview.IsShowing && !interview.IsTalking
            && !PlayerUIGate.IsBlocked && captureDepth == 0
            && !(ArrivalPPEStation.Instance != null && ArrivalPPEStation.Instance.GuidanceNeeded)
            && (STCSNotificationUI.Instance == null || (!STCSNotificationUI.Instance.IsShowing && STCSNotificationUI.Instance.PendingCount == 0));
        if (show)
        {
            Vector3 offset = head.transform.position - witness.InteractionPosition; offset.y = 0;
            show = offset.sqrMagnitude > witness.InteractionRadius * witness.InteractionRadius;
        }
        if (!show) { visual.enabled = false; return; }
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
        var points = route.waypoints;
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
            Vector3 vendorOffset = at - witness.InteractionPosition; vendorOffset.y = 0;
            if (vendorOffset.magnitude <= witness.InteractionRadius + .15f) continue;
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
    public static IDisposable HideForCapture() => new CaptureScope();
    private sealed class CaptureScope : IDisposable
    {
        private bool disposed;
        public CaptureScope() { captureDepth++; foreach (var guide in active) if (guide != null && guide.visual != null) guide.visual.enabled = false; }
        public void Dispose()
        {
            if (disposed) return; disposed = true; captureDepth = Mathf.Max(0, captureDepth - 1);
            foreach (var guide in active) if (guide != null) guide.Refresh();
        }
    }
}
