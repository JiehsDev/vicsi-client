// Assets/_Project/Scripts/Sketch/SketchMeasurementSlot.cs
using UnityEngine;

/// <summary>
/// Where one recorded distance is filed on a CrimeSceneSketchTemplate. The slot's own
/// RectTransform is the drop box (its rect, plus dropMargin); referenceFoot marks the
/// point on the reference structure that the measurement line runs to from the evidence
/// symbol. At runtime the slot only appears once its evidence element is placed, and the
/// line only once the distance is filed - neither can reveal a position early.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SketchMeasurementSlot : MonoBehaviour
{
    public string slotId;

    [Tooltip("The point on the reference structure the measurement line ends at (perpendicular foot for a wall/face).")]
    public RectTransform referenceFoot;

    [Tooltip("Extra forgiveness around the slot rect when dropping a card, in board units.")]
    [Min(0f)] public float dropMargin = 12f;

    public RectTransform RectTransform => (RectTransform)transform;

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        var template = GetComponentInParent<CrimeSceneSketchTemplate>();
        if (template == null) return;
        var rt = RectTransform;
        var corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        UnityEditor.Handles.color = new Color(0.3f, 0.75f, 1f, 0.9f);
        UnityEditor.Handles.DrawPolyLine(corners[0], corners[1], corners[2], corners[3], corners[0]);
        UnityEditor.Handles.Label(corners[1], slotId);
        var anchorPos = template.AnchorWorldPositionForSlot(slotId);
        if (referenceFoot != null)
        {
            UnityEditor.Handles.DrawDottedLine(anchorPos ?? rt.position, referenceFoot.position, 3f);
            UnityEditor.Handles.DrawWireDisc(referenceFoot.position, template.transform.forward, 6f * template.transform.lossyScale.x);
        }
    }
#endif
}
