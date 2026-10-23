using UnityEngine;
using UnityEngine.UI;

/// <summary>Small circular cue: soft cyan halo, dark outline, cyan face and white center. No labels or arrows.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class EvidenceDiscoveryDotGraphic : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        float size = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height);
        Disc(vh, size * .5f, new Color32(90, 222, 255, 80), new Color32(90, 222, 255, 0));
        Disc(vh, size * .31f, new Color32(12, 24, 34, 245), new Color32(12, 24, 34, 245));
        Disc(vh, size * .26f, new Color32(90, 222, 255, 255), new Color32(90, 222, 255, 255));
        Disc(vh, size * .12f, new Color32(245, 255, 255, 255), new Color32(245, 255, 255, 255));
    }
    private static void Disc(VertexHelper vh, float radius, Color32 center, Color32 edge)
    {
        const int segments = 32;
        int first = vh.currentVertCount;
        vh.AddVert(Vector3.zero, center, Vector2.zero);
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2 / segments;
            vh.AddVert(new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius, edge, Vector2.zero);
        }
        for (int i = 0; i < segments; i++) vh.AddTriangle(first, first + 1 + i, first + 1 + (i + 1) % segments);
    }
}
