// Assets/_Project/Scripts/Interaction/EvidenceTraceMarkerTarget.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Where an item's SECOND evidence tent goes - on a trace that belongs to the item (the
/// blood on the kitchen knife's blade): one evidence record, two tents, the way the trace
/// is documented in the field. Sits on the trace's own child object (Evidence_BloodOnKnifeBlade).
///
/// A numbered tent set down within radius of this point marks the trace
/// (EvidenceStateManager.MarkTraceTented) - but only once the item itself is already Marked
/// by its first tent and before its final photograph, and only for an item whose definition
/// has requiresTraceMarker. In every other state this target is ignored and the tent
/// resolves to the nearest evidence item as usual, so the first tent near the knife always
/// marks the knife. Placement looks and sounds exactly the same either way (see
/// EvidenceTentTool's "DELIBERATELY IDENTICAL" note); this only decides what the tent means.
/// </summary>
public class EvidenceTraceMarkerTarget : MonoBehaviour
{
    private static readonly List<EvidenceTraceMarkerTarget> Registered = new();

    [Tooltip("evidenceId of the item this trace belongs to (e.g. EVD-014 for the blood on the knife blade).")]
    public string evidenceId;

    [Tooltip("Horizontal distance (m) from this point within which a tent marks the trace. Kept tight - the item's own tent radius is much larger.")]
    [Min(0.05f)] public float radius = 0.3f;

    private void OnEnable() => Registered.Add(this);
    private void OnDisable() => Registered.Remove(this);

    /// <summary>Whether this trace can take its tent now: the item is exactly Marked and the trace isn't tented yet.</summary>
    public bool AcceptsTent
    {
        get
        {
            var esm = EvidenceStateManager.Instance;
            var record = esm != null ? esm.GetRecord(evidenceId) : null;
            return record != null && record.definition != null && record.definition.requiresTraceMarker
                && record.status == EvidenceStatus.Marked && !record.traceTentNumber.HasValue;
        }
    }

    /// <summary>The trace target a tent landing at point would mark, or null (then the tent resolves as usual).</summary>
    public static EvidenceTraceMarkerTarget FindForTent(Vector3 point)
    {
        EvidenceTraceMarkerTarget best = null;
        float bestSqr = float.MaxValue;
        foreach (var t in Registered)
        {
            if (t == null || !t.isActiveAndEnabled || !t.AcceptsTent) continue;
            Vector3 d = point - t.transform.position;
            d.y = 0f;
            float sqr = d.sqrMagnitude;
            if (sqr <= t.radius * t.radius && sqr < bestSqr)
            {
                best = t;
                bestSqr = sqr;
            }
        }
        return best;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        UnityEditor.Handles.color = new Color(0.9f, 0.1f, 0.1f, 0.8f);
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, radius);
    }
#endif
}
