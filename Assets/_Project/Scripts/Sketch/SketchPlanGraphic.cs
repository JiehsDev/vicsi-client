using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public struct SketchPlanStroke
{
    public Vector2 from, to;
    public Color color;
    public float width;
}

/// <summary>Authored architectural linework, batched into one UI mesh per plan. Contains no evidence positions.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class SketchPlanGraphic : MaskableGraphic
{
    public List<SketchPlanStroke> strokes = new();
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        foreach (var stroke in strokes)
        {
            Vector2 along = stroke.to - stroke.from;
            if (along.sqrMagnitude < .0001f) continue;
            Vector2 side = new Vector2(-along.y, along.x).normalized * (stroke.width * .5f);
            int start = vh.currentVertCount;
            vh.AddVert(stroke.from - side, stroke.color, Vector2.zero);
            vh.AddVert(stroke.from + side, stroke.color, Vector2.zero);
            vh.AddVert(stroke.to + side, stroke.color, Vector2.zero);
            vh.AddVert(stroke.to - side, stroke.color, Vector2.zero);
            vh.AddTriangle(start,start+1,start+2); vh.AddTriangle(start,start+2,start+3);
        }
    }
}
