// Assets/_Project/Scripts/Sketch/SketchFixedElement.cs
using UnityEngine;

public enum SketchFixedElementKind
{
    Outline,
    Wall,
    Door,
    Window,
    Stairs,
    Counter,
    Fixture,
    Label,
    ReferenceLabel,
    NorthArrow,
    ScaleBar,
    KeyPlan
}

/// <summary>
/// Tags a permanent, architectural piece of a CrimeSceneSketchTemplate (wall, counter,
/// north arrow, reference-structure label...). The visuals are ordinary authored UI
/// (Image / TMP) on this object or its children - move them in the Scene view like any
/// RectTransform. Fixed elements never say where evidence belongs.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SketchFixedElement : MonoBehaviour
{
    public string elementId;
    public SketchFixedElementKind kind = SketchFixedElementKind.Wall;

    [Tooltip("ReferenceLabel only: SceneReferencePoint.referenceId. At runtime the label text is taken from that reference's display name, so the sketch and the tape measure always use the same words.")]
    public string referenceId;

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        var template = GetComponentInParent<CrimeSceneSketchTemplate>();
        if (template == null || !template.showEditorLabels || string.IsNullOrEmpty(elementId)) return;
        UnityEditor.Handles.color = new Color(0.6f, 0.6f, 0.6f, 0.8f);
        UnityEditor.Handles.Label(transform.position, elementId);
    }
#endif
}
