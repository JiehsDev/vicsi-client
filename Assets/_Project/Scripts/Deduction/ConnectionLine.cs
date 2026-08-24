// Assets/_Project/Scripts/Deduction/ConnectionLine.cs
using UnityEngine;

/// <summary>
/// Draws a LineRenderer between two connected EvidenceNodes. Tracks the
/// RectTransforms' world positions every frame so the line stays attached
/// while either card is being dragged.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class ConnectionLine : MonoBehaviour
{
    [SerializeField] private LineRenderer lineRenderer;

    private RectTransform pointA;
    private RectTransform pointB;

    private void Awake()
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
        }

        lineRenderer.positionCount = 2;
        lineRenderer.useWorldSpace = true;
    }

    public void Bind(RectTransform a, RectTransform b)
    {
        pointA = a;
        pointB = b;
    }

    private void LateUpdate()
    {
        if (pointA == null || pointB == null)
        {
            return;
        }

        lineRenderer.SetPosition(0, pointA.position);
        lineRenderer.SetPosition(1, pointB.position);
    }
}
