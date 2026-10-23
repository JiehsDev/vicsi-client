using UnityEngine;
using UnityEngine.UI;

/// <summary>Texture-free clockwise circular progress, starting at twelve o'clock.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class DryingProgressGraphic : MaskableGraphic
{
    public float Progress { get; private set; }
    public void SetProgress(float value) { value=Mathf.Clamp01(value);if(Mathf.Approximately(value,Progress))return;Progress=value;SetVerticesDirty(); }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();float radius=Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)*.48f;
        Draw(vh,radius,1,new Color32(56,76,89,255));
        Draw(vh,radius,Progress,new Color32(90,222,255,255));
    }
    private static void Draw(VertexHelper vh,float radius,float fraction,Color32 tint)
    {
        int steps=Mathf.CeilToInt(fraction*80);if(steps==0)return;
        for(int i=0;i<steps;i++)
        {
            float a=Mathf.Min(i/80f,fraction)*Mathf.PI*2,b=Mathf.Min((i+1)/80f,fraction)*Mathf.PI*2;
            var p=new Vector2(Mathf.Sin(a),Mathf.Cos(a));var q=new Vector2(Mathf.Sin(b),Mathf.Cos(b));int n=vh.currentVertCount;
            vh.AddVert(p*radius,tint,Vector2.zero);vh.AddVert(q*radius,tint,Vector2.zero);
            vh.AddVert(q*(radius-8),tint,Vector2.zero);vh.AddVert(p*(radius-8),tint,Vector2.zero);
            vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
        }
    }
}
