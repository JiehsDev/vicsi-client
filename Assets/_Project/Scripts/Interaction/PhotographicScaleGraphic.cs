using UnityEngine;
using UnityEngine.Rendering;

/// <summary>A metric 30 cm photo ruler: one printed surface, one draw call, no collider.</summary>
public sealed class PhotographicScaleGraphic : MonoBehaviour
{
    public const float Length=.30f;
    public const float HalfWidth=.022f;
    private const int TextureWidth=2048,TextureHeight=282;
    private Mesh mesh;
    private Material material;
    private Texture2D texture;
    private Color32[] pixels;
    private static readonly int[] digits={0x3f,0x06,0x5b,0x4f,0x66,0x6d,0x7d,0x07,0x7f,0x6f};

    private void Awake()
    {
        // A scene-authored ruler keeps its editable mesh, print and material.
        var authoredFilter=GetComponent<MeshFilter>();var authoredRenderer=GetComponent<MeshRenderer>();
        if(authoredFilter!=null && authoredRenderer!=null && authoredFilter.sharedMesh!=null && authoredRenderer.sharedMaterial!=null)return;
        // Printing on one texture avoids z-fighting between near-coplanar ink and backing.
        pixels=new Color32[TextureWidth*TextureHeight];
        for(int i=0;i<pixels.Length;i++) pixels[i]=new Color32(255,255,255,255);
        for(int mm=0;mm<=300;mm++)
        {
            float height=mm%50==0?.012f:mm%10==0?.009f:.0045f;
            Rect(-.15f+mm*.001f-.0003f,.017f-height,.0006f,height);
        }
        for(int cm=0;cm<=30;cm+=5)
        {
            string value=cm.ToString();float start=-.15f+cm*.01f-(value.Length*.008f-.003f)/2;
            for(int i=0;i<value.Length;i++) Digit(value[i]-'0',start+i*.008f,-.006f);
        }
        Segment(new Vector2(-.005f,-.011f),new Vector2(-.010f,-.011f));
        Segment(new Vector2(-.010f,-.011f),new Vector2(-.010f,-.017f));
        Segment(new Vector2(-.010f,-.017f),new Vector2(-.005f,-.017f));
        Segment(new Vector2(-.002f,-.017f),new Vector2(-.002f,-.011f));
        Segment(new Vector2(-.002f,-.011f),new Vector2(.002f,-.014f));
        Segment(new Vector2(.002f,-.014f),new Vector2(.006f,-.011f));
        Segment(new Vector2(.006f,-.011f),new Vector2(.006f,-.017f));
        texture=new Texture2D(TextureWidth,TextureHeight,TextureFormat.RGBA32,true)
            {name="30 cm photographic ruler print",filterMode=FilterMode.Trilinear,wrapMode=TextureWrapMode.Clamp,anisoLevel=8};
        texture.SetPixels32(pixels);texture.Apply(true,Application.isPlaying);pixels=null;
        mesh=new Mesh {name="Metric photographic ruler"};
        mesh.vertices=new[]{new Vector3(-.16f,-.022f,0),new Vector3(.16f,-.022f,0),new Vector3(.16f,.022f,0),new Vector3(-.16f,.022f,0)};
        mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateBounds();
        material=new Material(Resources.Load<Shader>("PhotographicScale")){name="Photo ruler printed surface"};
        material.SetColor("_BaseColor",Color.white);material.SetTexture("_BaseMap",texture);
        gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
        renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
    }
    private int X(float value)=>Mathf.Clamp(Mathf.RoundToInt((value+.16f)/.32f*(TextureWidth-1)),0,TextureWidth-1);
    private int Y(float value)=>Mathf.Clamp(Mathf.RoundToInt((value+.022f)/.044f*(TextureHeight-1)),0,TextureHeight-1);
    private void Rect(float x,float y,float width,float height)
    {
        int left=X(x),right=X(x+width),bottom=Y(y),top=Y(y+height);
        for(int row=bottom;row<=top;row++) for(int col=left;col<=right;col++) pixels[row*TextureWidth+col]=new Color32(0,0,0,255);
    }
    private void Segment(Vector2 a,Vector2 b)
    {
        var ab=b-a;float length=ab.sqrMagnitude;const float half=.0006f;
        for(int row=Y(Mathf.Min(a.y,b.y)-half);row<=Y(Mathf.Max(a.y,b.y)+half);row++)
            for(int col=X(Mathf.Min(a.x,b.x)-half);col<=X(Mathf.Max(a.x,b.x)+half);col++)
            {
                var point=new Vector2((float)col/(TextureWidth-1)*.32f-.16f,(float)row/(TextureHeight-1)*.044f-.022f);
                var nearest=a+ab*Mathf.Clamp01(Vector2.Dot(point-a,ab)/length);
                if((point-nearest).sqrMagnitude<=half*half) pixels[row*TextureWidth+col]=new Color32(0,0,0,255);
            }
    }
    private void Digit(int digit,float x,float y)
    {
        Vector2 P(float a,float b)=>new Vector2(x+a*.005f,y+b*.010f);
        var ends=new[]{P(0,1),P(1,1),P(1,1),P(1,.5f),P(1,.5f),P(1,0),P(0,0),P(1,0),
            P(0,.5f),P(0,0),P(0,1),P(0,.5f),P(0,.5f),P(1,.5f)};
        for(int bit=0;bit<7;bit++) if((digits[digit]&(1<<bit))!=0) Segment(ends[bit*2],ends[bit*2+1]);
    }
    private void OnDestroy()
    {
        if(mesh!=null) Destroy(mesh);if(material!=null) Destroy(material);if(texture!=null) Destroy(texture);
    }
}
