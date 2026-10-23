// Assets/_Project/Scripts/Interaction/EvidenceMarkerAcceptanceZone.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Invisible marker-acceptance volume for an examined-in-place item (today only the
/// victim's body, BODY-A): a letter tent set down anywhere inside this box marks that
/// item, and the placed tent stands at placementPoint (beside the body) rather than
/// where the ray happened to land - on the torso, say.
///
/// Why it exists: tent targeting is otherwise "nearest evidence pivot within its radius"
/// (EvidenceProp.FindNearestWithinRadius). The body is ~2.1 m long, but the spatter's and
/// the knife's pivots lie inside or beside its footprint, so most of the body - the head
/// end, the west side - resolved to them and the letter tent was refused. LetterTentTool
/// checks these zones first. Numbered tents (EvidenceTentTool) never consult them, so the
/// knife, spatter and bottle keep their own targeting.
///
/// Acceptance only: the box is a trigger used purely for Contains(); the item's physics
/// and aim colliders are untouched (every tool raycast ignores triggers). Never shown in
/// gameplay - editor gizmo only.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class EvidenceMarkerAcceptanceZone : MonoBehaviour
{
    private static readonly List<EvidenceMarkerAcceptanceZone> Registered = new();

    public static IReadOnlyList<EvidenceMarkerAcceptanceZone> All => Registered;

    [Tooltip("EvidenceDefinition.evidenceId of the examined-in-place item this zone accepts a letter tent for.")]
    public string evidenceId;

    [Tooltip("Where the accepted tent is stood (beside the item). Empty = where the tent was aimed.")]
    public Transform placementPoint;

    private BoxCollider box;

    public BoxCollider Box
    {
        get
        {
            if (box == null)
            {
                box = GetComponent<BoxCollider>();
            }
            return box;
        }
    }

    private void Reset()
    {
        Box.isTrigger = true;
    }

    private void Awake()
    {
        Box.isTrigger = true;
    }

    private void OnEnable() => Registered.Add(this);

    private void OnDisable() => Registered.Remove(this);

    /// <summary>Whether worldPoint lies inside the box (in its own local space).</summary>
    public bool Contains(Vector3 worldPoint)
    {
        Vector3 local = transform.InverseTransformPoint(worldPoint) - Box.center;
        Vector3 half = Box.size * 0.5f;
        return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
    }

    /// <summary>The active zone containing worldPoint, or null.</summary>
    public static EvidenceMarkerAcceptanceZone FindContaining(Vector3 worldPoint)
    {
        foreach (var zone in Registered)
        {
            if (zone != null && zone.isActiveAndEnabled && zone.Contains(worldPoint))
            {
                return zone;
            }
        }
        return null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.9f, 0.6f, 0.1f, 0.9f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Box.center, Box.size);
        Gizmos.matrix = Matrix4x4.identity;
        if (placementPoint != null)
        {
            Gizmos.DrawWireSphere(placementPoint.position, 0.06f);
            UnityEditor.Handles.Label(placementPoint.position + Vector3.up * 0.1f, $"{evidenceId} marker");
        }
    }
#endif
}
