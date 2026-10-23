using UnityEngine;
using UnityEngine.UI;

/// <summary>Authored illustrative ridge diagrams, never real biometric evidence or an identification algorithm.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class PracticeFingerprintGraphic : MaskableGraphic
{
    public int Variant { get; private set; }
    public bool Recovered { get; private set; }
    public float Zoom { get; private set; } = 1;
    public float Rotation { get; private set; }
    public bool ShowFeatures { get; private set; }
    public void Configure(int variant,bool recovered,float zoom,float rotation,bool features)
    {
        Variant=variant;Recovered=recovered;Zoom=zoom;Rotation=rotation;ShowFeatures=features;SetVerticesDirty();
    }
    private Vector2 Ridge(int index,float angle)
    {
        float radius=.14f+index*.046f;
        return new Vector2(Mathf.Sin(angle)*radius*(Variant==1?.78f:.72f),Mathf.Cos(angle)*radius);
    }
    private Vector2 Project(Vector2 point)
    {
        float radians=Rotation*Mathf.Deg2Rad,c=Mathf.Cos(radians),s=Mathf.Sin(radians);
        return new Vector2(point.x*c-point.y*s,point.x*s+point.y*c)*Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)*.47f*Zoom;
    }
    private int EndingRidge => Variant==2?5:8;
    private float EndingAngle => Variant==1?-.65f:.25f;
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();var ink=new Color32(24,29,33,255);
        float extent=Recovered?2.05f:2.8f;
        for(int ridge=0;ridge<17;ridge++)
        {
            for(int segment=0;segment<120;segment++)
            {
                float a=Mathf.Lerp(-extent,extent,segment/120f),b=Mathf.Lerp(-extent,extent,(segment+1)/120f);
                if(ridge==EndingRidge && a>=EndingAngle && a<EndingAngle+.30f)continue;
                Line(vh,Project(Ridge(ridge,a)),Project(Ridge(ridge,b)),1.7f,ink);
            }
        }
        int branchRidge=Variant==2?10:11;float branchAngle=Variant==1?.8f:-.5f;
        var junction=Ridge(branchRidge,branchAngle);
        Line(vh,Project(junction),Project(junction+new Vector2(-.065f,-.055f)),1.7f,ink);
        Line(vh,Project(junction+new Vector2(-.065f,-.055f)),Project(junction+new Vector2(-.12f,-.14f)),1.7f,ink);
        if(ShowFeatures)
        {
            Ring(vh,Project(Ridge(EndingRidge,EndingAngle)),11,new Color32(186,100,0,255));
            Ring(vh,Project(junction),11,new Color32(10,110,170,255));
        }
    }
    private static void Ring(VertexHelper vh,Vector2 center,float radius,Color32 color)
    {
        for(int i=0;i<36;i++)
        {
            float a=i*Mathf.PI*2/36,b=(i+1)*Mathf.PI*2/36;
            Line(vh,center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,2.5f,color);
        }
    }
    private static void Line(VertexHelper vh,Vector2 a,Vector2 b,float width,Color32 color)
    {
        Vector2 delta=b-a;if(delta.sqrMagnitude<.000001f)return;
        var normal=new Vector2(-delta.y,delta.x).normalized*width*.5f;int offset=vh.currentVertCount;
        vh.AddVert(a-normal,color,Vector2.zero);vh.AddVert(a+normal,color,Vector2.zero);
        vh.AddVert(b+normal,color,Vector2.zero);vh.AddVert(b-normal,color,Vector2.zero);
        vh.AddTriangle(offset,offset+1,offset+2);vh.AddTriangle(offset,offset+2,offset+3);
    }
}
