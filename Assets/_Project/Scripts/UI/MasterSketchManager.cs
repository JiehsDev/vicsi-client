// Assets/_Project/Scripts/UI/MasterSketchManager.cs
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One entry stamped onto the master sketch: which item, what tent number to label it
/// with, and where it sits on the sketch as a 0-1 normalized position within the
/// scene's footprint (see MasterSketchManager.WorldToNormalized). Position, not the
/// evidenceId, is what the UI needs to place the mark - the id stays attached so a
/// re-annotation of the same item (defensive; the procedural gate already prevents the
/// player from triggering this twice) replaces its own dot instead of adding a second one.
/// </summary>
public readonly struct SketchAnnotation
{
    public readonly string evidenceId;
    public readonly int tentNumber;
    public readonly Vector2 normalizedPosition;

    public SketchAnnotation(string evidenceId, int tentNumber, Vector2 normalizedPosition)
    {
        this.evidenceId = evidenceId;
        this.tentNumber = tentNumber;
        this.normalizedPosition = normalizedPosition;
    }
}

/// <summary>
/// Scene-wide store for the ONE shared crime-scene sketch, the same Instance-singleton
/// shape as PhotoAlbumManager/EvidenceStateManager elsewhere in this project. Exists
/// because real scene sketching produces a single spatial document with every item's
/// numbered marker plotted onto it, not a separate drawing per item - unlike
/// photography, which genuinely is per-item. SketchTool (and, for the scripted
/// walkthrough, GreyboxFlowTest) calls RecordAnnotation once per item; MasterSketchUI
/// listens for OnAnnotationAdded and draws the accumulated result. Doesn't know
/// anything about how the annotation action is triggered or how it's displayed - same
/// separation PhotoAlbumManager keeps from PhotoCaptureListener/PhotoAlbumUI.
///
/// Does NOT gate-check anything itself. The caller (SketchTool.TrySketch,
/// GreyboxFlowTest.Step) is expected to have already confirmed
/// ProceduralGateValidator.CanTransition(evidenceId, EvidenceStatus.Sketched) before
/// calling RecordAnnotation, exactly as every other MarkX call site does for its own
/// status. Keeping the gate check at the call site (not here) is what lets
/// GreyboxFlowTest exercise this exact method instead of a parallel test-only path.
/// </summary>
public class MasterSketchManager : MonoBehaviour
{
    public static MasterSketchManager Instance { get; private set; }

    [Tooltip("Metres of empty margin added around the evidence items' bounding box, so a marker near the edge of the room doesn't sit flush against the sketch's border.")]
    [SerializeField] private float boundsPadding = 1.5f;

    [Tooltip("Minimum sketch footprint on either axis (metres), so a scene with items clustered along one line (or a single item) still produces a sensibly proportioned rectangle instead of a sliver.")]
    [SerializeField] private float minExtent = 2f;

    private readonly List<SketchAnnotation> annotations = new();

    /// <summary>Fired right after a new (or replaced) annotation is recorded.</summary>
    public event Action<SketchAnnotation> OnAnnotationAdded;

    /// <summary>Every annotation stamped onto the sketch so far. Read-only view; never mutated by callers.</summary>
    public IReadOnlyList<SketchAnnotation> Annotations => annotations;

    // World-space XZ bounds the sketch represents, computed once from every registered
    // EvidenceProp's position the first time a normalization is needed (by then every
    // prop in the scene has already registered in OnEnable, well before the player can
    // have reached the Sketched step on anything). Deliberately not scene-tuned data:
    // a hand-authored rectangle would silently go stale the moment an item's position
    // changed, the same class of drift the interactionRadius work this session already
    // eliminated once for tent attribution.
    private bool boundsReady;
    private Vector2 worldMin;
    private Vector2 worldMax;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Stamps evidence's tent number onto the sketch at its projected position, and
    /// replaces any earlier annotation for the same evidenceId rather than duplicating
    /// it. Caller's responsibility to have already gate-checked the Sketched
    /// transition - see the class comment.
    /// </summary>
    public void RecordAnnotation(EvidenceProp evidence)
    {
        if (evidence == null || string.IsNullOrEmpty(evidence.evidenceId))
        {
            return;
        }

        int tentNumber = EvidenceStateManager.Instance?.GetRecord(evidence.evidenceId)?.tentNumber ?? 0;
        Vector2 normalized = WorldToNormalized(evidence.transform.position);

        annotations.RemoveAll(a => a.evidenceId == evidence.evidenceId);
        var annotation = new SketchAnnotation(evidence.evidenceId, tentNumber, normalized);
        annotations.Add(annotation);

        OnAnnotationAdded?.Invoke(annotation);
    }

    /// <summary>Projects a world position into the sketch's 0-1 XZ space. (0,0) is the near/left corner of the computed bounds, (1,1) the far/right corner.</summary>
    public Vector2 WorldToNormalized(Vector3 worldPosition)
    {
        EnsureBounds();
        if (!boundsReady)
        {
            return new Vector2(0.5f, 0.5f);
        }

        float u = Mathf.InverseLerp(worldMin.x, worldMax.x, worldPosition.x);
        float v = Mathf.InverseLerp(worldMin.y, worldMax.y, worldPosition.z);
        return new Vector2(u, v);
    }

    private void EnsureBounds()
    {
        if (boundsReady || EvidenceProp.All.Count == 0)
        {
            return;
        }

        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;

        foreach (var prop in EvidenceProp.All)
        {
            if (prop == null)
            {
                continue;
            }

            Vector3 p = prop.transform.position;
            minX = Mathf.Min(minX, p.x);
            maxX = Mathf.Max(maxX, p.x);
            minZ = Mathf.Min(minZ, p.z);
            maxZ = Mathf.Max(maxZ, p.z);
        }

        minX -= boundsPadding;
        maxX += boundsPadding;
        minZ -= boundsPadding;
        maxZ += boundsPadding;

        if (maxX - minX < minExtent)
        {
            float cx = (minX + maxX) * 0.5f;
            minX = cx - minExtent * 0.5f;
            maxX = cx + minExtent * 0.5f;
        }

        if (maxZ - minZ < minExtent)
        {
            float cz = (minZ + maxZ) * 0.5f;
            minZ = cz - minExtent * 0.5f;
            maxZ = cz + minExtent * 0.5f;
        }

        worldMin = new Vector2(minX, minZ);
        worldMax = new Vector2(maxX, maxZ);
        boundsReady = true;
    }
}
