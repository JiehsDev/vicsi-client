// Assets/_Project/Scripts/Sketch/SketchEvidenceAnchor.cs
using UnityEngine;

/// <summary>
/// An invisible drop zone on a CrimeSceneSketchTemplate: where an evidence element belongs.
/// Drawn only as an editor gizmo (circle + id) - at runtime it has no visuals at all, so
/// the sketch never shows where evidence goes (see project evidence-leak rule). Move it in
/// the Scene view; snapRadius is in board units.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SketchEvidenceAnchor : MonoBehaviour
{
    public string anchorId;

    [Min(5f)] public float snapRadius = 50f;

    public RectTransform RectTransform => (RectTransform)transform;

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        var template = GetComponentInParent<CrimeSceneSketchTemplate>();
        if (template == null) return;
        Vector3 centre = transform.position;
        float worldRadius = snapRadius * template.transform.lossyScale.x;
        UnityEditor.Handles.color = new Color(1f, 0.55f, 0.1f, 0.9f);
        UnityEditor.Handles.DrawWireDisc(centre, template.transform.forward, worldRadius);
        UnityEditor.Handles.Label(centre + template.transform.up * worldRadius, anchorId);
    }
#endif
}
