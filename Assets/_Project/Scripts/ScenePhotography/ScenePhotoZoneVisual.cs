// Assets/_Project/Scripts/ScenePhotography/ScenePhotoZoneVisual.cs
using UnityEngine;

/// <summary>
/// Flat glowing ring on the floor marking a ScenePhotoPoint's standing zone. The ring
/// mesh is generated once (a few dozen triangles); the look comes entirely from the
/// assigned material, and the pulse is a MaterialPropertyBlock alpha change, so no
/// material instances and no custom shader are created.
///
/// A soft translucent column rises from the ring and fades out upward, so the active
/// spot can still be found across a room when tables or chairs hide the floor ring
/// (a flat ring several metres away is nearly edge-on to the player's eye).
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ScenePhotoZoneVisual : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    [Tooltip("Ring outer radius (m). Usually matches the point's standingRadius.")]
    [Min(0.1f)] public float outerRadius = 0.75f;

    [Tooltip("Ring band width (m).")]
    [Min(0.01f)] public float bandWidth = 0.08f;

    [Tooltip("Faint fill inside the ring so the whole standing area reads, not just its edge. 0 = ring only.")]
    [Range(0f, 1f)] public float fillAlpha = 0.12f;

    [Tooltip("Height (m) of the light column rising from the ring. 0 = no column.")]
    [Min(0f)] public float columnHeight = 1f;

    [Tooltip("Column opacity at the floor; it fades to 0 at the top.")]
    [Range(0f, 1f)] public float columnAlpha = 0.3f;

    public Color color = new Color(0.25f, 0.85f, 1f, 0.75f);

    [Min(0f)] public float pulseSpeed = 2f;
    [Range(0f, 1f)] public float pulseAmount = 0.35f;

    [Range(8, 128)] public int segments = 48;

    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock block;

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        block = new MaterialPropertyBlock();
        GetComponent<MeshFilter>().sharedMesh = BuildMesh();
    }

    private void Update()
    {
        float k = 1f - pulseAmount * 0.5f * (1f + Mathf.Sin(Time.time * pulseSpeed));
        var c = color;
        c.a *= k;
        block.SetColor(BaseColorId, c);
        meshRenderer.SetPropertyBlock(block);
    }

    private Mesh BuildMesh()
    {
        float inner = Mathf.Max(0f, outerRadius - bandWidth);
        bool fill = fillAlpha > 0f;
        int ringVerts = (segments + 1) * 2;
        var vertices = new Vector3[ringVerts + (fill ? segments + 2 : 0)];
        var colors = new Color[vertices.Length];
        var triangles = new System.Collections.Generic.List<int>();

        for (int i = 0; i <= segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            vertices[i * 2] = dir * inner;
            vertices[i * 2 + 1] = dir * outerRadius;
            colors[i * 2] = Color.white;
            colors[i * 2 + 1] = Color.white;
            if (i < segments)
            {
                int v = i * 2;
                triangles.AddRange(new[] { v, v + 1, v + 3, v, v + 3, v + 2 });
            }
        }

        if (fill)
        {
            int center = ringVerts;
            vertices[center] = Vector3.zero;
            colors[center] = new Color(1f, 1f, 1f, fillAlpha);
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                vertices[center + 1 + i] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * inner;
                colors[center + 1 + i] = new Color(1f, 1f, 1f, fillAlpha);
                if (i < segments)
                {
                    triangles.AddRange(new[] { center, center + 2 + i, center + 1 + i });
                }
            }
        }

        // Ring triangles are wound for the upward face; the band is reversed here so it
        // also faces up (the fill above already does).
        for (int t = 0; t < segments * 6; t += 3)
        {
            (triangles[t + 1], triangles[t + 2]) = (triangles[t + 2], triangles[t + 1]);
        }

        var allVertices = new System.Collections.Generic.List<Vector3>(vertices);
        var allColors = new System.Collections.Generic.List<Color>(colors);
        if (columnHeight > 0f && columnAlpha > 0f)
        {
            // Open cylinder at the ring's outer edge, alpha columnAlpha at the floor fading
            // to 0 at the top. Both windings, so it reads from inside and outside the zone.
            int baseIndex = allVertices.Count;
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * outerRadius;
                allVertices.Add(dir);
                allVertices.Add(dir + Vector3.up * columnHeight);
                allColors.Add(new Color(1f, 1f, 1f, columnAlpha));
                allColors.Add(new Color(1f, 1f, 1f, 0f));
                if (i < segments)
                {
                    int v = baseIndex + i * 2;
                    triangles.AddRange(new[] { v, v + 1, v + 3, v, v + 3, v + 2 });
                    triangles.AddRange(new[] { v, v + 3, v + 1, v, v + 2, v + 3 });
                }
            }
        }

        var mesh = new Mesh { name = "ScenePhotoZoneRing" };
        mesh.SetVertices(allVertices);
        mesh.SetColors(allColors);
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
