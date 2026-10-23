// Assets/_Project/Scripts/Measurement/MeasurementTargetAnchor.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The exact point of an evidence item that position measurements are taken to, when the
/// item's own transform origin isn't the intended reconstruction point (e.g. the body).
/// Optional: without one, MeasurementGeometry uses the EvidenceProp's transform. Put it
/// on an empty child of the prop - it carries no collider and changes no physics.
/// </summary>
public class MeasurementTargetAnchor : MonoBehaviour
{
    private static readonly List<MeasurementTargetAnchor> Registered = new();

    [Tooltip("The evidenceId this anchor belongs to.")]
    public string evidenceId;

    private void OnEnable() => Registered.Add(this);

    private void OnDisable() => Registered.Remove(this);

    /// <summary>The active anchor for evidenceId, or null. Searches the scene outside Play mode.</summary>
    public static MeasurementTargetAnchor Find(string evidenceId)
    {
        if (string.IsNullOrEmpty(evidenceId))
        {
            return null;
        }

        foreach (var anchor in Registered)
        {
            if (anchor != null && anchor.evidenceId == evidenceId)
            {
                return anchor;
            }
        }

        if (!Application.isPlaying)
        {
            foreach (var anchor in FindObjectsByType<MeasurementTargetAnchor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (anchor.evidenceId == evidenceId)
                {
                    return anchor;
                }
            }
        }
        return null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.7f, 1f);
        Gizmos.DrawWireSphere(transform.position, 0.06f);
    }
#endif
}
