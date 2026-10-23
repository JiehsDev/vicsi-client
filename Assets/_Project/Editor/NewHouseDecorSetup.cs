using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Scene-local decoration, with shared low-poly meshes and no gameplay components.</summary>
[InitializeOnLoad]
public static class NewHouseDecorSetup
{
    const string Folder="Assets/_Project/Art/Decor/NewHouse";
    const string Evidence="docs/evidence/new-house-decor";
    static double next;
    static Material clay,cream,soil,green,sage,wood,paper,linen;
    static Transform design;
    static NewHouseDecorSetup(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.timeSinceStartup<next||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        next=EditorApplication.timeSinceStartup+1;
        const string request="Temp/new-house-decor-request.txt";
        if(!File.Exists(request))return;
        string command=File.ReadAllText(request).Trim();File.Delete(request);
        try{if(command=="apply")Apply();if(command=="capture")CaptureAll();if(command=="inspect")Inspect();if(command=="finish")Finish();}
        catch(Exception e){File.WriteAllText("Temp/new-house-decor-error.txt",e.ToString());}
    }
    static Transform House()=>SceneManager.GetActiveScene().GetRootGameObjects().Single(g=>g.name=="Vicsi_House").transform;
    static Bounds BoundsOf(Transform t)
    {var rs=t.GetComponentsInChildren<Renderer>(true);var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;}
    [MenuItem("ViCSI/Scene Setup/Decorate New House")]
    public static void Apply()
    {
        var scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlayingOrWillChangePlaymode||scene.path!="Assets/_Project/Scenes/Scenarios/CSI_Environment.unity")throw new InvalidOperationException("Open CSI_Environment outside Play mode.");
        var house=House();if(house.Find("HomeDecor")!=null)throw new InvalidOperationException("HomeDecor already exists; edit that group or undo before applying again.");
        Directory.CreateDirectory(Evidence);Directory.CreateDirectory(Folder);
        if(!EditorSceneManager.SaveScene(scene,"Temp/new-house-before-decor.unity",true))throw new IOException("Could not snapshot the current relocated kitchen.");
        Undo.IncrementCurrentGroup();Undo.SetCurrentGroupName("Decorate new house");
        var root=new GameObject("HomeDecor");Undo.RegisterCreatedObjectUndo(root,"Add house decorations");root.transform.SetParent(house,false);root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);design=root.transform;
        clay=Mat("Terracotta",new Color(.52f,.25f,.13f));cream=Mat("Warm ceramic",new Color(.80f,.76f,.65f));soil=Mat("Soil",new Color(.065f,.047f,.030f));
        green=Mat("Deep foliage",new Color(.095f,.24f,.115f),true);sage=Mat("Sage",new Color(.26f,.36f,.23f),true);
        wood=Mat("Walnut frame",new Color(.18f,.10f,.048f));paper=Mat("Ivory paper",new Color(.85f,.82f,.71f));linen=Mat("Natural linen",new Color(.55f,.49f,.36f));
        var plant=PlantMesh();
        Instance("Dining window floor plant",plant,new[]{cream,soil,green,sage},new Vector3(11.45f,1.2f,8.6f),1,15);
        Instance("Living window floor plant",plant,new[]{clay,soil,green,sage},new Vector3(18.92f,1.2f,10.65f),.75f,-25);
        Instance("Kitchen corner floor plant",plant,new[]{cream,soil,green,sage},new Vector3(19.05f,1.2f,14.15f),.65f,60);
        Instance("Porch planter",plant,new[]{clay,soil,green,sage},new Vector3(18.98f,1.2f,5.05f),.75f,25);
        var counter=house.Find("CabinetBset/CabinetB01").GetComponent<Renderer>().bounds;
        Instance("Kitchen counter herb",plant,new[]{clay,soil,green,sage},new Vector3(counter.center.x,counter.max.y,counter.center.z+.07f),.24f,20);
        var dining=house.Find("KitchenDiningSet/Kitchen Dining Table");if(dining==null)dining=house.Find("Furniture/Table19");
        var db=BoundsOf(dining);Instance("Dining table greenery",plant,new[]{cream,soil,green,sage},new Vector3(db.center.x,db.max.y,db.center.z),.24f,-15);
        var coffee=BoundsOf(house.Find("Furniture/Table35"));
        Books(new Vector3(coffee.center.x-.27f,coffee.max.y,coffee.center.z-.06f));
        Instance("Coffee table bud plant",plant,new[]{cream,soil,green,sage},new Vector3(coffee.center.x+.35f,coffee.max.y,coffee.center.z+.07f),.18f,80);
        Rug(new Vector3(coffee.center.x,1.204f,9.5f),new Vector2(2.65f,2.10f));
        Art("Living botanical print left",new Vector3(17.04f,3.08f,11.37f),180,false);
        Art("Living botanical print right",new Vector3(18.16f,3.08f,11.37f),180,true);
        Art("Dining botanical print",new Vector3(13.5f,2.95f,9.27f),180,false);
        KitchenLight();
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene))throw new IOException("Decorated scene save failed.");
        CaptureAll();Inspect();Selection.activeGameObject=root;
    }
    static Material Mat(string name,Color color,bool doubleSided=false)
    {
        string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.12f);m.SetFloat("_Cull",doubleSided?0:2);m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
    }
    class Builder
    {
        public List<Vector3> vertices=new List<Vector3>();public List<Vector2> uv=new List<Vector2>();public List<int>[] triangles;
        public Builder(int count){triangles=Enumerable.Range(0,count).Select(_=>new List<int>()).ToArray();}
        public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,int material)
        {int n=vertices.Count;vertices.AddRange(new[]{a,b,c,d});uv.AddRange(new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up});triangles[material].AddRange(new[]{n,n+1,n+2,n,n+2,n+3});}
        public void Box(Vector3 c,Vector3 size,int mat)
        {
            var h=size*.5f;Vector3[] p={c+new Vector3(-h.x,-h.y,-h.z),c+new Vector3(h.x,-h.y,-h.z),c+new Vector3(h.x,h.y,-h.z),c+new Vector3(-h.x,h.y,-h.z),c+new Vector3(-h.x,-h.y,h.z),c+new Vector3(h.x,-h.y,h.z),c+new Vector3(h.x,h.y,h.z),c+new Vector3(-h.x,h.y,h.z)};
            Quad(p[0],p[3],p[2],p[1],mat);Quad(p[4],p[5],p[6],p[7],mat);Quad(p[0],p[4],p[7],p[3],mat);Quad(p[1],p[2],p[6],p[5],mat);Quad(p[3],p[7],p[6],p[2],mat);Quad(p[0],p[1],p[5],p[4],mat);
        }
        public void Tube(Vector3 from,Vector3 to,float radius,int mat,int sides=8)
        {
            var axis=(to-from).normalized;var u=Vector3.Cross(axis,Mathf.Abs(axis.y)>.9f?Vector3.right:Vector3.up).normalized;var v=Vector3.Cross(axis,u);
            for(int j=0;j<sides;j++){float a=j*Mathf.PI*2/sides,b=(j+1)*Mathf.PI*2/sides;var r1=(u*Mathf.Cos(a)+v*Mathf.Sin(a))*radius;var r2=(u*Mathf.Cos(b)+v*Mathf.Sin(b))*radius;Quad(from+r1,from+r2,to+r2,to+r1,mat);}
        }
        public void Lathe(Vector2[] profile,int mat,int sides=24)
        {
            for(int i=0;i<profile.Length-1;i++)for(int j=0;j<sides;j++)
            {float a=j*Mathf.PI*2/sides,b=(j+1)*Mathf.PI*2/sides;Vector3 p=new Vector3(Mathf.Cos(a)*profile[i].x,profile[i].y,Mathf.Sin(a)*profile[i].x),q=new Vector3(Mathf.Cos(b)*profile[i].x,profile[i].y,Mathf.Sin(b)*profile[i].x),r=new Vector3(Mathf.Cos(b)*profile[i+1].x,profile[i+1].y,Mathf.Sin(b)*profile[i+1].x),s=new Vector3(Mathf.Cos(a)*profile[i+1].x,profile[i+1].y,Mathf.Sin(a)*profile[i+1].x);Quad(q,p,s,r,mat);}
        }
        public Mesh Save(string name)
        {
            if(vertices.Any(v=>!float.IsFinite(v.x)||!float.IsFinite(v.y)||!float.IsFinite(v.z)))throw new InvalidOperationException("Non-finite decoration mesh vertex: "+name);
            string path=Folder+"/"+name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);bool create=mesh==null;
            if(create)mesh=new Mesh{name=name};else mesh.Clear();
            mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.subMeshCount=triangles.Length;for(int i=0;i<triangles.Length;i++)mesh.SetTriangles(triangles[i],i);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();mesh.UploadMeshData(false);
            if(create)AssetDatabase.CreateAsset(mesh,path);else EditorUtility.SetDirty(mesh);return mesh;
        }
    }
    static Mesh PlantMesh()
    {
        var b=new Builder(4);
        b.Lathe(new[]{new Vector2(0,0),new Vector2(.125f,0),new Vector2(.15f,.035f),new Vector2(.195f,.32f),new Vector2(.205f,.34f),new Vector2(.182f,.34f),new Vector2(.174f,.30f)},0);
        b.Lathe(new[]{new Vector2(.174f,.307f),new Vector2(0,.307f)},1);
        for(int i=0;i<18;i++)
        {
            float angle=i*2.399963f;var outward=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));var side=Vector3.Cross(Vector3.up,outward);
            float h=.48f+(i%6)*.105f;var start=outward*(.025f+(i%3)*.018f)+Vector3.up*.31f;var tip=outward*(.08f+(i%4)*.025f)+Vector3.up*h;
            b.Tube(start,tip,.006f,2,6);float length=.29f+(i%4)*.035f;
            for(int j=0;j<7;j++)
            {
                float t=j/7f,u=(j+1)/7f;float w=Mathf.Pow(Mathf.Max(0,Mathf.Sin(t*Mathf.PI)),.75f)*.075f,wu=Mathf.Pow(Mathf.Max(0,Mathf.Sin(u*Mathf.PI)),.75f)*.075f;
                var a=tip+outward*(length*t)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*.17f+.08f*t);
                var c=tip+outward*(length*u)+Vector3.up*(Mathf.Sin(u*Mathf.PI)*.17f+.08f*u);
                b.Quad(a+side*w,a-side*w,c-side*wu,c+side*wu,i%3==0?3:2);
            }
        }
        return b.Save("CurvedLeafPlant");
    }
    static GameObject Instance(string name,Mesh mesh,Material[] materials,Vector3 position,float scale=1,float yaw=0)
    {
        var g=new GameObject(name);g.transform.SetParent(design,false);g.transform.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));g.transform.localScale=Vector3.one*scale;
        g.AddComponent<MeshFilter>().sharedMesh=mesh;var r=g.AddComponent<MeshRenderer>();r.sharedMaterials=materials;r.shadowCastingMode=ShadowCastingMode.Off;return g;
    }
    static void Rug(Vector3 p,Vector2 size)
    {
        var b=new Builder(2);float x=size.x*.5f,z=size.y*.5f,edge=.075f;
        b.Quad(new Vector3(-x,0,-z),new Vector3(-x,0,z),new Vector3(x,0,z),new Vector3(x,0,-z),0);
        foreach(float sign in new[]{-1f,1f})
        {b.Box(new Vector3(sign*(x-edge),.003f,0),new Vector3(.045f,.002f,size.y-.1f),1);b.Box(new Vector3(0,.003f,sign*(z-edge)),new Vector3(size.x-.1f,.002f,.045f),1);}
        Instance("Woven living room rug",b.Save("LinenBorderRug"),new[]{linen,sage},p);
    }
    static void Books(Vector3 p)
    {
        var b=new Builder(3);
        b.Box(new Vector3(0,.022f,0),new Vector3(.27f,.035f,.19f),1);b.Box(new Vector3(0,.003f,0),new Vector3(.28f,.006f,.20f),0);b.Box(new Vector3(0,.042f,0),new Vector3(.28f,.006f,.20f),0);b.Box(new Vector3(-.137f,.022f,0),new Vector3(.006f,.04f,.20f),0);
        b.Box(new Vector3(.014f,.061f,.006f),new Vector3(.24f,.028f,.165f),1);b.Box(new Vector3(.014f,.046f,.006f),new Vector3(.25f,.005f,.175f),2);b.Box(new Vector3(.014f,.078f,.006f),new Vector3(.25f,.005f,.175f),2);
        Instance("Coffee table books",b.Save("StackedBooks"),new[]{sage,paper,clay},p,1,-12);
    }
    static void Art(string name,Vector3 p,float yaw,bool alternate)
    {
        if(name.StartsWith("Dining"))p=DiningPrintPosition();
        var b=new Builder(3);float w=.62f,h=.78f;
        b.Box(Vector3.zero,new Vector3(w,h,.025f),0);b.Box(new Vector3(0,0,.016f),new Vector3(w-.055f,h-.055f,.012f),1);
        b.Tube(new Vector3(0,-.27f,.025f),new Vector3(alternate?.08f:-.06f,.25f,.025f),.004f,2,6);
        for(int i=0;i<7;i++)
        {
            float y=-.19f+i*.062f;float sign=i%2==0?1:-1;var a=new Vector3(-.01f,y,.028f);var tip=a+new Vector3(sign*(.13f+(i%2)*.035f),.06f,.002f);var cross=new Vector3(-.01f,.04f,0);
            b.Quad(a,(a+tip)*.5f+cross,tip,(a+tip)*.5f-cross,2);
        }
        // Both sheets stay matte and use geometry rather than extra image textures.
        Instance(name,b.Save(name.Replace(' ','_')),new[]{wood,paper,Ink(alternate)},p,1,yaw);
    }
    static Vector3 DiningPrintPosition()
    {
        var origin=new Vector3(13.5f,2.95f,8.4f);
        var hits=Physics.RaycastAll(origin,Vector3.forward,1.4f,~0,QueryTriggerInteraction.Ignore)
            .Where(h=>h.transform.IsChildOf(House())&&Vector3.Dot(h.normal,Vector3.back)>.5f).OrderBy(h=>h.distance).ToArray();
        return hits.Length>0?hits[0].point+Vector3.back*.03f:new Vector3(13.5f,2.95f,9.12f);
    }
    static Material Ink(bool light)
    {
        string path=Folder+"/"+(light?"Sage print ink":"Green print ink")+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Unlit"));AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",light?new Color(.27f,.36f,.23f):new Color(.12f,.25f,.16f));m.SetFloat("_Cull",0);EditorUtility.SetDirty(m);return m;
    }
    static void KitchenLight()
    {
        if(design.Find("Kitchen ceiling light")!=null)return;
        var b=new Builder(1);b.Lathe(new[]{new Vector2(0,0),new Vector2(.12f,0),new Vector2(.15f,.018f),new Vector2(.13f,.035f),new Vector2(0,.035f)},0);
        var m=Mat("Warm light diffuser",new Color(.84f,.75f,.57f));m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",new Color(1,.78f,.45f)*1.5f);EditorUtility.SetDirty(m);
        var g=Instance("Kitchen ceiling light",b.Save("KitchenLightDiffuser"),new[]{m},new Vector3(17.1f,3.9f,13.15f));
        var lightObject=new GameObject("Warm kitchen light");lightObject.transform.SetParent(g.transform,false);lightObject.transform.localPosition=new Vector3(0,-.4f,0);
        var l=lightObject.AddComponent<Light>();l.type=LightType.Point;l.color=new Color(1,.86f,.65f);l.intensity=1.2f;l.range=4.2f;l.shadows=LightShadows.None;
    }
    static void Finish()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Finish outside Play mode.");
        design=House().Find("HomeDecor");PlantMesh();
        var living=design.Find("Living window floor plant");Undo.RecordObject(living,"Keep foliage clear of walls");living.position=new Vector3(18.92f,1.2f,10.65f);living.localScale=Vector3.one*.75f;
        var kitchen=design.Find("Kitchen corner floor plant");Undo.RecordObject(kitchen,"Keep foliage clear of walls");kitchen.localScale=Vector3.one*.65f;
        var print=design.Find("Dining botanical print");Undo.RecordObject(print,"Seat print in front of wall");print.position=DiningPrintPosition();
        clay=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Terracotta.mat");
        foreach(Transform t in design)
        {
            if(!t.name.Contains("botanical print"))continue;
            var r=t.GetComponent<Renderer>();var materials=r.sharedMaterials;materials[2]=Ink(t.name.EndsWith("right"));Undo.RecordObject(r,"Use matte printed botanical ink");r.sharedMaterials=materials;
        }
        KitchenLight();AssetDatabase.SaveAssets();var scene=SceneManager.GetActiveScene();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);CaptureAll();Inspect();
    }
    static void Inspect()
    {
        var scene=SceneManager.GetActiveScene();var root=House().Find("HomeDecor");var s=new StringBuilder($"scene={scene.path} playing={EditorApplication.isPlaying} dirty={scene.isDirty} compilationFailed={EditorUtility.scriptCompilationFailed}\n");
        if(root!=null){foreach(Transform t in root){var b=BoundsOf(t);s.AppendLine($"{t.name}: min={b.min:F3} max={b.max:F3}");if(b.size.sqrMagnitude<.00001f)throw new InvalidOperationException("Empty decorative mesh: "+t.name);}s.AppendLine($"groups={root.childCount} colliders={root.GetComponentsInChildren<Collider>(true).Length} lights={root.GetComponentsInChildren<Light>(true).Length} triangles={root.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3)}");}
        File.WriteAllText("Temp/new-house-decor-inspect.txt",s.ToString());
    }
    static void CaptureAll()
    {
        Directory.CreateDirectory(Evidence);var scene=SceneManager.GetActiveScene();bool wasDirty=scene.isDirty;
        try{Capture("living",new Vector3(18.8f,2.85f,7f),new Vector3(17.5f,2.25f,10f));Capture("dining",new Vector3(14.1f,2.85f,5.25f),new Vector3(12.7f,2.1f,7.5f));Capture("relocated-kitchen",new Vector3(18.7f,2.85f,12.0f),new Vector3(16.7f,2.0f,14.1f));}
        finally{if(!EditorApplication.isPlaying&&!wasDirty&&scene.isDirty)EditorSceneManager.SaveScene(scene);}
    }
    static void Capture(string name,Vector3 from,Vector3 target)
    {
        var go=new GameObject("Decor diagnostic camera"){hideFlags=HideFlags.HideAndDontSave};var c=go.AddComponent<Camera>();c.transform.SetPositionAndRotation(from,Quaternion.LookRotation(target-from));c.fieldOfView=75;c.nearClipPlane=.1f;c.farClipPlane=100;
        var rt=RenderTexture.GetTemporary(1200,900,24,RenderTextureFormat.Default,RenderTextureReadWrite.Default,4);var resolved=RenderTexture.GetTemporary(1200,900,0);var previous=RenderTexture.active;
        try{c.targetTexture=rt;c.Render();Graphics.Blit(rt,resolved);RenderTexture.active=resolved;var tex=new Texture2D(1200,900,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1200,900),0,0);tex.Apply();File.WriteAllBytes(Evidence+"/"+name+".png",tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);}
        finally{c.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);RenderTexture.ReleaseTemporary(resolved);UnityEngine.Object.DestroyImmediate(go);}
    }
}




