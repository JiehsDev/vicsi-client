using UnityEngine;
using UnityEngine.UI;

/// <summary>Small native UI mesh; arrow points up, location pin's tip is at its anchor.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class PhotographyGuideGraphic : MaskableGraphic
{
    private bool pin, ready;
    public void SetState(bool locationPin, bool aligned)
    {
        if(pin==locationPin&&ready==aligned)return;
        pin=locationPin;ready=aligned;SetVerticesDirty();
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Draw(vh,new Color32(12,24,34,245),1.18f);
        Draw(vh,ready?new Color32(92,246,175,255):new Color32(90,222,255,255),1f);
    }
    private void Draw(VertexHelper vh,Color32 tint,float scale)
    {
        if(pin)
        {
            Triangle(vh,new Vector2(-16,17)*scale,new Vector2(16,17)*scale,Vector2.zero,tint);
            const int segments=28;
            for(int i=0;i<segments;i++)
            {
                float a=i*Mathf.PI*2/segments,b=(i+1)*Mathf.PI*2/segments;
                var center=new Vector2(0,27)*scale;
                Triangle(vh,center,center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*18*scale,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*18*scale,tint);
            }
            if(scale==1f)
                for(int i=0;i<segments;i++)
                {
                    float a=i*Mathf.PI*2/segments,b=(i+1)*Mathf.PI*2/segments;
                    var c=new Vector2(0,27);
                    Triangle(vh,c,c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*7,c+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*7,new Color32(12,24,34,255));
                }
        }
        else
        {
            Triangle(vh,new Vector2(-22,-5)*scale,new Vector2(0,26)*scale,new Vector2(22,-5)*scale,tint);
            Triangle(vh,new Vector2(-8,-4)*scale,new Vector2(8,-4)*scale,new Vector2(8,-24)*scale,tint);
            Triangle(vh,new Vector2(-8,-4)*scale,new Vector2(8,-24)*scale,new Vector2(-8,-24)*scale,tint);
        }
    }
    private static void Triangle(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Color32 color)
    {
        int i=vh.currentVertCount;
        vh.AddVert(a,color,Vector2.zero);vh.AddVert(b,color,Vector2.zero);vh.AddVert(c,color,Vector2.zero);
        vh.AddTriangle(i,i+1,i+2);
    }
}
