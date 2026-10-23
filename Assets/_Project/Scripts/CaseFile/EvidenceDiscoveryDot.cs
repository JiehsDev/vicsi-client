using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Visual cue for physical evidence awaiting Recorder confirmation after scene photography. Never advances evidence.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(21000)]
public sealed class EvidenceDiscoveryDot : MonoBehaviour
{
    [SerializeField, Min(.01f)] private float diameter = .05f;
    [SerializeField, Min(.01f)] private float surfaceClearance = .06f;
    private EvidenceProp prop;
    private Renderer[] surfaces;
    private ScenePhotographyManager photos;
    private Canvas canvas;
    private Material material;
    private static readonly List<EvidenceDiscoveryDot> active = new();
    private static int captureDepth;

    public bool IsVisible => canvas != null && canvas.enabled && canvas.gameObject.activeInHierarchy;
    public Canvas DotCanvas => canvas;
    public Vector3 WorldPosition => canvas != null ? canvas.transform.position : transform.position;
    public float Diameter => diameter;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { active.Clear(); captureDepth = 0; }

    private void Awake()
    {
        prop = GetComponent<EvidenceProp>();
        surfaces = GetComponentsInChildren<Renderer>(true);
        var shader = Resources.Load<Shader>("EvidenceDiscoveryDot");
        if (shader == null) { enabled = false; Debug.LogError("Evidence discovery dot shader is missing.", this); return; }
        material = new Material(shader) { name = "Evidence discovery dot", hideFlags = HideFlags.HideAndDontSave };
        // World-sized root avoids inheriting the bottle's imported .15 scale or a flat decal's rotation.
        var go = new GameObject("Evidence discovery dot", typeof(RectTransform), typeof(Canvas), typeof(EvidenceDiscoveryDotGraphic));
        go.hideFlags = HideFlags.DontSave; go.layer = 2; // Ignore Raycast; no colliders or raycaster.
        canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.enabled = false;
        ((RectTransform)go.transform).sizeDelta = Vector2.one * diameter;
        var graphic = go.GetComponent<EvidenceDiscoveryDotGraphic>(); graphic.material = material; graphic.raycastTarget = false;
    }
    private void OnEnable()
    {
        if (!active.Contains(this)) active.Add(this);
        EvidenceStateManager.OnEvidenceStatusChanged += StatusChanged;
        BindPhotography();
    }
    private void OnDisable()
    {
        active.Remove(this);
        EvidenceStateManager.OnEvidenceStatusChanged -= StatusChanged;
        if (photos != null) photos.OnScenePhotographyCompleted -= Refresh;
        photos = null;
        if (canvas != null) canvas.enabled = false;
    }
    private void OnDestroy()
    {
        if (canvas != null) Destroy(canvas.gameObject);
        if (material != null) Destroy(material);
    }
    private void BindPhotography()
    {
        if (photos == ScenePhotographyManager.Instance) return;
        if (photos != null) photos.OnScenePhotographyCompleted -= Refresh;
        photos = ScenePhotographyManager.Instance;
        if (photos != null) photos.OnScenePhotographyCompleted += Refresh;
    }
    private void StatusChanged(string id, EvidenceStatus status) { if (prop != null && id == prop.evidenceId) Refresh(); }
    private void LateUpdate() => Refresh();

    /// <summary>Read-only refresh; all discovery/procedure input remains on the existing evidence controllers.</summary>
    public void Refresh()
    {
        if (canvas == null) return;
        BindPhotography();
        var record = prop != null && !string.IsNullOrEmpty(prop.evidenceId) && EvidenceStateManager.Instance != null
            ? EvidenceStateManager.Instance.GetRecord(prop.evidenceId) : null;
        var camera = Camera.main;
        bool show = isActiveAndEnabled && prop != null && prop.isActiveAndEnabled && !prop.RequiresExternalDiscovery
            && record?.definition != null && record.recorderDocumentation == null
            && !EvidenceStateManager.Instance.IsAtOrPastStatus(prop.evidenceId, EvidenceStatus.Logged)
            && photos != null && photos.isActiveAndEnabled && photos.IsScenePhotographyComplete
            && !SceneTurnover.IsInvestigationBlocked && camera != null && captureDepth == 0;
        canvas.enabled = show;
        if (!show) return;
        bool foundSurface = false; Bounds bounds = default;
        foreach (var surface in surfaces)
        {
            if (surface == null || !surface.enabled || !surface.gameObject.activeInHierarchy
                || !(surface is MeshRenderer || surface is SkinnedMeshRenderer)
                || surface.GetComponentInParent<EvidenceProp>() != prop) continue;
            if (!foundSurface) { bounds = surface.bounds; foundSurface = true; }
            else bounds.Encapsulate(surface.bounds);
        }
        Vector3 position = foundSurface ? new Vector3(bounds.center.x, bounds.max.y, bounds.center.z) : prop.transform.position;
        canvas.transform.SetPositionAndRotation(position + Vector3.up * surfaceClearance, camera.transform.rotation);
    }

    /// <summary>Keep the visual hints out of actual evidence photographs; supports nested capture scopes.</summary>
    public static IDisposable HideForCapture() => new CaptureScope();
    private sealed class CaptureScope : IDisposable
    {
        private bool disposed;
        public CaptureScope() { captureDepth++; RefreshActive(); }
        public void Dispose() { if (disposed) return; disposed = true; captureDepth = Mathf.Max(0, captureDepth - 1); RefreshActive(); }
    }
    private static void RefreshActive()
    {
        for (int i = active.Count - 1; i >= 0; i--) if (active[i] != null) active[i].Refresh();
    }
}
