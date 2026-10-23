using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Authoring only: the saved scene contains meshes, LODs and static box colliders.
[InitializeOnLoad]
public static class NeighborhoodSetup
{
    const string Folder = "Assets/_Project/Art/Decor/Neighborhood";
    const string Evidence = "docs/evidence/neighborhood";
    const string RootName = "Neighborhood_Exterior";
    static Material palette;
    static double nextRequestPoll;
    static NeighborhoodSetup(){EditorApplication.update+=PollRequest;}
    static void PollRequest()
    {
        if(EditorApplication.timeSinceStartup<nextRequestPoll||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        nextRequestPoll=EditorApplication.timeSinceStartup+1;
        const string request="Temp/neighborhood-request.txt";
        if(!File.Exists(request))return;
        File.Delete(request);
        try{Apply();File.WriteAllText("Temp/neighborhood-result.txt",Validate());}
        catch(Exception ex){File.WriteAllText("Temp/neighborhood-result.txt",ex.ToString());Debug.LogException(ex);}
    }
    static readonly Color32[] Colors = {
        new Color32(223,209,175,255), new Color32(172,185,164,255),
        new Color32(205,174,143,255), new Color32(192,199,193,255),
        new Color32(116,61,44,255), new Color32(62,74,77,255),
        new Color32(77,58,40,255), new Color32(235,224,193,255),
        new Color32(57,89,104,255), new Color32(140,139,126,255),
        new Color32(62,95,43,255), new Color32(87,120,54,255),
        new Color32(72,53,36,255), new Color32(130,108,75,255),
        new Color32(214,196,143,255), new Color32(124,104,76,255)
    };

    sealed class Geometry
    {
        public readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<int> indices = new List<int>();
        readonly List<Vector2> uvs = new List<Vector2>();
        void Face(int color, params Vector3[] points)
        {
            int start = vertices.Count;
            vertices.AddRange(points);
            foreach (var p in points) uvs.Add(new Vector2((color + .5f) / Colors.Length, .5f));
            for (int i=1;i<points.Length-1;i++) indices.AddRange(new[]{start,start+i,start+i+1});
        }
        public void Box(Vector3 p, Vector3 size, int color)
        {
            var a=p-size*.5f;var b=p+size*.5f;
            Face(color,new Vector3(a.x,a.y,a.z),new Vector3(a.x,b.y,a.z),new Vector3(b.x,b.y,a.z),new Vector3(b.x,a.y,a.z));
            Face(color,new Vector3(b.x,a.y,b.z),new Vector3(b.x,b.y,b.z),new Vector3(a.x,b.y,b.z),new Vector3(a.x,a.y,b.z));
            Face(color,new Vector3(a.x,a.y,b.z),new Vector3(a.x,b.y,b.z),new Vector3(a.x,b.y,a.z),new Vector3(a.x,a.y,a.z));
            Face(color,new Vector3(b.x,a.y,a.z),new Vector3(b.x,b.y,a.z),new Vector3(b.x,b.y,b.z),new Vector3(b.x,a.y,b.z));
            Face(color,new Vector3(a.x,b.y,a.z),new Vector3(a.x,b.y,b.z),new Vector3(b.x,b.y,b.z),new Vector3(b.x,b.y,a.z));
            Face(color,new Vector3(a.x,a.y,b.z),new Vector3(a.x,a.y,a.z),new Vector3(b.x,a.y,a.z),new Vector3(b.x,a.y,b.z));
        }
        public void Roof(Vector3 p,float width,float depth,float rise,int color)
        {
            var a=p+new Vector3(-width*.5f,0,-depth*.5f);var b=p+new Vector3(width*.5f,0,-depth*.5f);
            var c=p+new Vector3(-width*.5f,0,depth*.5f);var d=p+new Vector3(width*.5f,0,depth*.5f);
            var e=p+new Vector3(0,rise,-depth*.5f);var f=p+new Vector3(0,rise,depth*.5f);
            Face(color,a,c,f,e);Face(color,b,e,f,d);Face(color,a,e,b);Face(color,c,d,f);
            Face(color,a,b,d,c);
        }
        public void Crown(Vector3 p,Vector3 size,int color)
        {
            var top=p+Vector3.up*size.y*.5f;var bottom=p-Vector3.up*size.y*.5f;
            for(int i=0;i<6;i++)
            {
                float a=i*Mathf.PI/3,b=(i+1)*Mathf.PI/3;
                var v=p+new Vector3(Mathf.Cos(a)*size.x*.5f,0,Mathf.Sin(a)*size.z*.5f);
                var w=p+new Vector3(Mathf.Cos(b)*size.x*.5f,0,Mathf.Sin(b)*size.z*.5f);
                Face(color,top,w,v);Face(color,bottom,v,w);
            }
        }
        public Mesh Mesh(string name)
        {
            var m=new Mesh {name=name,indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
            m.SetVertices(vertices);m.SetUVs(0,uvs);m.SetTriangles(indices,0);m.RecalculateNormals();m.RecalculateBounds();
            return m;
        }
    }
    static void EnsureAssets()
    {
        Directory.CreateDirectory(Folder);Directory.CreateDirectory(Evidence);
        AssetDatabase.Refresh();
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/NeighborhoodPalette.asset");
        if(texture==null)
        {
            texture=new Texture2D(Colors.Length,1,TextureFormat.RGBA32,false){name="Neighborhood colors",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            texture.SetPixels32(Colors);texture.Apply();AssetDatabase.CreateAsset(texture,Folder+"/NeighborhoodPalette.asset");
        }
        palette=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/NeighborhoodShared.mat");
        if(palette==null)
        {
            palette=new Material(Shader.Find("Universal Render Pipeline/Simple Lit")){name="Neighborhood shared opaque"};
            palette.SetTexture("_BaseMap",texture);palette.SetColor("_BaseColor",Color.white);
            palette.SetFloat("_Smoothness",0);palette.SetFloat("_SpecularHighlights",0);
            palette.DisableKeyword("_SPECGLOSSMAP");palette.DisableKeyword("_SPECULAR_COLOR");
            AssetDatabase.CreateAsset(palette,Folder+"/NeighborhoodShared.mat");
        }
    }
    static MeshRenderer SaveGeometry(Transform parent, string name, Geometry geometry, bool shadow=false)
    {
        var mesh=geometry.Mesh(name);string path=Folder+"/"+name+".asset";
        var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(existing!=null){EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}
        else AssetDatabase.CreateAsset(mesh,path);
        var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
        go.GetComponent<MeshFilter>().sharedMesh=mesh;var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=palette;
        r.shadowCastingMode=shadow?ShadowCastingMode.On:ShadowCastingMode.Off;
        r.receiveShadows=true;r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;
        go.isStatic=true;return r;
    }
    static void Collider(Transform parent,string name,Vector3 center,Vector3 size)
    {
        var g=new GameObject(name);g.transform.SetParent(parent,false);var c=g.AddComponent<BoxCollider>();c.center=center;c.size=size;g.isStatic=true;
    }
    static void Tree(Geometry g,float x,float z,float height=5)
    {
        g.Box(new Vector3(x,height*.25f,z),new Vector3(.24f,height*.5f,.24f),12);
        g.Crown(new Vector3(x,height*.69f,z),new Vector3(height*.65f,height*.65f,height*.6f),10);
        g.Crown(new Vector3(x-.45f,height*.84f,z+.2f),new Vector3(height*.48f,height*.42f,height*.46f),11);
    }
    static void Shrub(Geometry g,float x,float z,float size=1)
    {g.Crown(new Vector3(x,size*.45f,z),new Vector3(size,size*.9f,size*.8f),10);}
    static void Window(Geometry g,float x,float y,float z)
    {
        g.Box(new Vector3(x,y,z),new Vector3(1.48f,1.5f,.13f),7);
        g.Box(new Vector3(x,y,z-.08f),new Vector3(1.22f,1.24f,.06f),8);
        g.Box(new Vector3(x,y,z-.12f),new Vector3(.06f,1.25f,.05f),7);
        g.Box(new Vector3(x,y,z-.12f),new Vector3(1.25f,.06f,.05f),7);
        foreach(float side in new[]{-1,1})g.Box(new Vector3(x+side*.91f,y,z-.05f),new Vector3(.27f,1.5f,.12f),6);
        g.Box(new Vector3(x,y-.79f,z-.1f),new Vector3(1.7f,.13f,.28f),7);
    }
    static void House(Transform root,string name,Vector3 position,float rotation,int wall,int roof,float height)
    {
        var plot=new GameObject(name);plot.transform.SetParent(root,false);plot.transform.position=position;plot.transform.rotation=Quaternion.Euler(0,rotation,0);
        var g=new Geometry();float w=8.4f,d=8;
        g.Box(new Vector3(0,.2f,0),new Vector3(w+.2f,.4f,d+.2f),9);
        g.Box(new Vector3(0,height*.5f+.35f,0),new Vector3(w,height,d),wall);
        g.Roof(new Vector3(0,height+.35f,0),w+.9f,d+.9f,2.1f,roof);
        var simple=new Geometry();simple.Box(new Vector3(0,(height+.35f)*.5f,0),new Vector3(w,height+.35f,d),wall);
        simple.Roof(new Vector3(0,height+.35f,0),w+.9f,d+.9f,2.1f,roof);
        // Timber plinth, corner posts, eaves and a band between storeys.
        foreach(float x in new[]{-w*.5f,w*.5f})foreach(float z in new[]{-d*.5f,d*.5f})g.Box(new Vector3(x,height*.5f+.35f,z),new Vector3(.17f,height,.17f),6);
        foreach(float y in new[]{.55f,height+.3f})g.Box(new Vector3(0,y,0),new Vector3(w+.15f,.17f,d+.15f),6);
        if(height>4)g.Box(new Vector3(0,3.2f,0),new Vector3(w+.15f,.17f,d+.15f),6);
        for(float y=1.95f;y<height;y+=2.7f)
        {
            Window(g,-2.55f,y,-4.05f);Window(g,2.55f,y,-4.05f);
            Window(g,-2.55f,y,4.2f);Window(g,2.55f,y,4.2f);
        }
        g.Box(new Vector3(0,1.49f,-4.08f),new Vector3(1.34f,2.3f,.18f),7);
        g.Box(new Vector3(0,1.45f,-4.2f),new Vector3(1.12f,2.15f,.1f),6);
        g.Box(new Vector3(.36f,1.4f,-4.28f),new Vector3(.08f,.08f,.06f),14);
        g.Box(new Vector3(0,.16f,-5),new Vector3(2.5f,.32f,2),9);
        g.Roof(new Vector3(0,2.9f,-4.85f),3,2.5f,.65f,roof);
        foreach(float x in new[]{-1.22f,1.22f})g.Box(new Vector3(x,1.55f,-5.75f),new Vector3(.12f,2.9f,.12f),7);
        g.Box(new Vector3(0,.015f,-7),new Vector3(1.75f,.03f,2.2f),9);
        for(int i=0;i<5;i++){Shrub(g,-3.8f+i*.85f,-5.2f,.7f);Shrub(g,3.9f,3-i*1.5f,.85f);}
        Tree(g,-6,3,5.5f);Tree(g,6,4,4.5f);
        var detail=SaveGeometry(plot.transform,name+"_Detail",g);
        var distant=SaveGeometry(plot.transform,name+"_Distant",simple);
        var lod=plot.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.065f,new Renderer[]{detail}),new LOD(.008f,new Renderer[]{distant})});lod.RecalculateBounds();
        Collider(plot.transform,"Building boundary",new Vector3(0,height*.5f,0),new Vector3(w,height,d));
    }
    static void Fence(Geometry g,Vector3 a,Vector3 b)
    {
        var delta=b-a;bool alongX=Mathf.Abs(delta.x)>Mathf.Abs(delta.z);float length=delta.magnitude;
        int bays=Mathf.CeilToInt(length/2);var step=delta/bays;
        for(int i=0;i<=bays;i++)
        {
            var p=a+step*i;g.Box(p+Vector3.up*.7f,new Vector3(.16f,1.4f,.16f),7);
            g.Roof(p+Vector3.up*1.4f,.23f,.23f,.1f,6);
        }
        for(int i=0;i<bays;i++)
        {
            var p=a+step*(i+.5f);
            foreach(float y in new[]{.4f,1.03f})g.Box(p+Vector3.up*y,alongX?new Vector3(step.magnitude,.1f,.09f):new Vector3(.09f,.1f,step.magnitude),6);
            int pickets=Mathf.CeilToInt(step.magnitude/.22f);
            for(int j=0;j<pickets;j++)
            {var q=a+step*(i+(j+.5f)/pickets);g.Box(q+Vector3.up*.68f,new Vector3(.095f,1.12f,.095f),7);}
        }
    }
    static void Grounds(Transform root)
    {
        // Two lawn strips leave the entire road clear. Match the original lawn's UV density.
        foreach(bool north in new[]{false,true})
        {
            float start=north?-.09f:-36,end=north?44:-7.59f;
            var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.name=north?"North neighborhood lawn":"South neighborhood lawn";ground.transform.SetParent(root,false);
            ground.transform.position=new Vector3(.4f,-.035f,(start+end)*.5f);ground.transform.localScale=new Vector3(13,1,(end-start)/10);
            var filter=ground.GetComponent<MeshFilter>();var mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);
            mesh.uv=mesh.uv.Select(uv=>new Vector2(uv.x*130/20,uv.y*(end-start)/18.18f)).ToArray();
            var path=Folder+"/"+ground.name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing!=null){EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,path);
            filter.sharedMesh=mesh;
            ground.GetComponent<Renderer>().sharedMaterial=GameObject.Find("Plane (3)").GetComponent<Renderer>().sharedMaterial;
            ground.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            UnityEngine.Object.DestroyImmediate(ground.GetComponent<Collider>());
            Collider(root,"Lawn ground",new Vector3(.4f,-.15f,(start+end)*.5f),new Vector3(130,.23f,end-start));ground.isStatic=true;
        }
        var g=new Geometry();
        // Walkway ends at the original stairs; the six metre gateway also admits all exterior NPCs.
        g.Box(new Vector3(16.9f,.018f,1.45f),new Vector3(2.7f,.036f,3.35f),9);
        foreach(var segment in new[]{new[]{new Vector3(8,0,.15f),new Vector3(14.5f,0,.15f)},new[]{new Vector3(20.8f,0,.15f),new Vector3(25,0,.15f)},new[]{new Vector3(8,0,.15f),new Vector3(8,0,19.5f)},new[]{new Vector3(25,0,.15f),new Vector3(25,0,19.5f)},new[]{new Vector3(8,0,19.5f),new Vector3(25,0,19.5f)}})
        {
            Fence(g,segment[0],segment[1]);var size=segment[1]-segment[0];
            Collider(root,"Property fence boundary",(segment[0]+segment[1])*.5f+Vector3.up*.65f,new Vector3(Mathf.Max(.16f,Mathf.Abs(size.x)),1.3f,Mathf.Max(.16f,Mathf.Abs(size.z))));
        }
        // Low planted beds remain outside the photography/NPC/entrance work areas.
        foreach(float x in new[]{8.8f,24.2f})
        {
            g.Box(new Vector3(x,.06f,10),new Vector3(1.1f,.12f,11),15);
            for(int i=0;i<9;i++)Shrub(g,x,5.5f+i*1.1f,.85f);
        }
        Tree(g,23,17.6f,5.4f);Tree(g,9.5f,17.6f,4.5f);
        foreach(float x in new[]{10.1f,12.1f,22.5f})Shrub(g,x,2.5f,.85f);
        // Mailbox and front garden edging.
        g.Box(new Vector3(22.1f,.57f,.7f),new Vector3(.12f,1.14f,.12f),6);
        g.Box(new Vector3(22.1f,1.2f,.7f),new Vector3(.48f,.3f,.35f),5);
        SaveGeometry(root,"HouseGardenAndFence",g);
    }
    static void Road(Transform root)
    {
        var source=GameObject.Find("Road").transform.GetChild(0);
        // Retain the authored centre street. Add only two strips beyond its exact end bounds.
        foreach(bool east in new[]{false,true})
        {
            var road=new GameObject(east?"East road extension":"West road extension");road.transform.SetParent(root,false);
            var instances=new List<GameObject>();
            for(int i=0;i<6;i++)
            {
                var copy=UnityEngine.Object.Instantiate(source.gameObject,road.transform);
                copy.transform.position=new Vector3(east?18.977f+7.5f*i:-18.161f-7.5f*i,-.058f,-3.84f);
                // Geometry only; no new real time lights, scripts, or mesh colliders.
                foreach(var c in copy.GetComponentsInChildren<Component>(true))
                    if(!(c is Transform)&&!(c is MeshFilter)&&!(c is MeshRenderer))UnityEngine.Object.DestroyImmediate(c);
                // One existing-style lamp per fifteen metres.
                foreach(var r in copy.GetComponentsInChildren<MeshRenderer>())
                {
                    // The source lantern glass contains offset geometry; retain only the metal frame.
                    if(r.name.Contains("Lamp")||r.name=="Glass")r.enabled=false;
                    if(r.name.Contains("Streetlight")&&i%2==1)r.enabled=false;
                }
                instances.Add(copy);
            }
            var filters=road.GetComponentsInChildren<MeshFilter>().Where(f=>f.GetComponent<MeshRenderer>()!=null&&f.GetComponent<MeshRenderer>().enabled).ToArray();
            var byMaterial=new Dictionary<Material,List<CombineInstance>>();
            foreach(var f in filters)
            {
                var materials=f.GetComponent<MeshRenderer>().sharedMaterials;
                for(int i=0;i<f.sharedMesh.subMeshCount;i++)
                {
                    var mat=materials[Mathf.Min(i,materials.Length-1)];
                    if(!byMaterial.ContainsKey(mat))byMaterial[mat]=new List<CombineInstance>();
                    byMaterial[mat].Add(new CombineInstance{mesh=f.sharedMesh,subMeshIndex=i,transform=road.transform.worldToLocalMatrix*f.transform.localToWorldMatrix});
                }
            }
            int index=0;
            foreach(var pair in byMaterial)
            {
                var mesh=new Mesh{name=road.name+index,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray(),true,true);mesh.RecalculateBounds();
                var path=Folder+"/"+mesh.name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(existing!=null){EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,path);
                var g=new GameObject("Combined "+pair.Key.name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(road.transform,false);
                g.GetComponent<MeshFilter>().sharedMesh=mesh;var r=g.GetComponent<MeshRenderer>();
                // The imported pack puts solid asphalt, curbs and iron in the transparent queue.
                // Use opaque copies for our extensions, preserving the original scene materials.
                var materialPath=Folder+"/"+pair.Key.name+"_Opaque.mat";
                var opaque=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if(opaque==null)
                {
                    opaque=new Material(pair.Key){name=pair.Key.name+" Neighborhood Opaque"};
                    opaque.SetFloat("_Surface",0);opaque.SetFloat("_ZWrite",1);
                    opaque.SetFloat("_SrcBlend",(float)BlendMode.One);opaque.SetFloat("_DstBlend",(float)BlendMode.Zero);
                    opaque.SetFloat("_SrcBlendAlpha",(float)BlendMode.One);opaque.SetFloat("_DstBlendAlpha",(float)BlendMode.Zero);
                    opaque.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");opaque.DisableKeyword("_ALPHAPREMULTIPLY_ON");opaque.DisableKeyword("_ALPHAMODULATE_ON");
                    opaque.SetOverrideTag("RenderType","Opaque");opaque.renderQueue=2000;
                    AssetDatabase.CreateAsset(opaque,materialPath);
                }
                r.sharedMaterial=opaque;
                r.shadowCastingMode=ShadowCastingMode.Off;r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;g.isStatic=true;index++;
            }
            foreach(var g in instances)UnityEngine.Object.DestroyImmediate(g);
            Collider(road.transform,"Road surface",new Vector3(east?37.727f:-36.911f,-.15f,-3.84f),new Vector3(45,.18f,5.5f));
            foreach(float z in new[]{-.59f,-7.09f})Collider(road.transform,"Sidewalk surface",new Vector3(east?37.727f:-36.911f,-.06f,z),new Vector3(45,.18f,1));
        }
        var paint=new Geometry();
        for(float x=-55;x<59;x+=9)paint.Box(new Vector3(x,-.052f,-3.84f),new Vector3(3,.009f,.1f),14);
        SaveGeometry(root,"StreetMarkings",paint);
    }
    [MenuItem("ViCSI/Scene Setup/Build Lightweight Neighborhood")]
    public static void Apply()
    {
        var scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlayingOrWillChangePlaymode||scene.path!="Assets/_Project/Scenes/Scenarios/CSI_Environment.unity")throw new InvalidOperationException("Open CSI_Environment outside Play mode.");
        EnsureAssets();
        if(!File.Exists(Evidence+"/before.unity"))EditorSceneManager.SaveScene(scene,Evidence+"/before.unity",true);
        Undo.IncrementCurrentGroup();Undo.SetCurrentGroupName("Design lightweight neighborhood");
        var old=GameObject.Find(RootName);if(old!=null)Undo.DestroyObjectImmediate(old);
        var root=new GameObject(RootName);Undo.RegisterCreatedObjectUndo(root,"Add neighborhood");
        Grounds(root.transform);Road(root.transform);
        House(root.transform,"WestSageHouse",new Vector3(-3,0,9),0,1,5,3.5f);
        House(root.transform,"EastCreamHouse",new Vector3(36,0,9),0,0,4,5.8f);
        House(root.transform,"AcrossTerracottaHouse",new Vector3(1,0,-19),180,2,5,3.5f);
        House(root.transform,"AcrossStoneHouse",new Vector3(26,0,-19),180,3,4,3.5f);
        var planting=new Geometry();foreach(float x in new[]{-24f,-15f,47f,53f}){Tree(planting,x,13,6);Tree(planting,x,-21,5);}
        SaveGeometry(root.transform,"StreetTrees",planting);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Validate();Capture();
    }
    public static string Validate()
    {
        var root=GameObject.Find(RootName);var s=new System.Text.StringBuilder();int failures=0;
        Action<bool,string> check=(ok,label)=>{s.AppendLine((ok?"PASS ":"FAIL ")+label);if(!ok)failures++;};
        var filters=root.GetComponentsInChildren<MeshFilter>();int triangles=filters.Sum(f=>f.sharedMesh.triangles.Length/3);
        var renderers=root.GetComponentsInChildren<MeshRenderer>();
        check(triangles<40000,"Added geometry below 40,000 triangles (including inactive distance LODs): "+triangles);
        check(renderers.Length<=30,"Added mesh renderers <=30 (including alternate LODs): "+renderers.Length);
        check(root.GetComponentsInChildren<Light>(true).Length==0,"No added lights");
        check(root.GetComponentsInChildren<MonoBehaviour>(true).Length==0,"No runtime scripts");
        check(root.GetComponentsInChildren<Rigidbody>(true).Length==0,"No rigidbodies");
        check(root.GetComponentsInChildren<MeshCollider>(true).Length==0,"No mesh colliders");
        check(root.GetComponentsInChildren<LODGroup>().Length==4,"Four houses use detail/distant/cull LODs");
        check(renderers.All(r=>r.shadowCastingMode==ShadowCastingMode.Off),"All added renderers avoid shadow caster passes");
        check(renderers.All(r=>r.sharedMaterials.All(m=>m.renderQueue<2500)),"All added materials use opaque rendering");
        check(filters.All(f=>AssetDatabase.Contains(f.sharedMesh)),"All meshes persist as assets");
        var obstacles=root.GetComponentsInChildren<BoxCollider>();
        foreach(var n in new[]{"NPC_FirstResponder","NPC_IOC","NPC_Vendor_PLACEHOLDER","Teleport Hotspot","EvidenceHoldingCrate"})
        {
            var go=GameObject.Find(n);var p=go.transform.position+Vector3.up*.8f;
            check(!obstacles.Any(c=>c.bounds.Contains(p)),n+" is clear of new obstacles");
        }
        // Sample the full street-to-stairs route with a 0.6m-wide standing capsule AABB.
        bool clear=true;
        for(float z=-.1f;z<3.1f;z+=.15f)
        {var b=new Bounds(new Vector3(16.9f,.95f,z),new Vector3(.6f,1.7f,.6f));if(obstacles.Any(c=>c.bounds.Intersects(b)))clear=false;}
        check(clear,"Entrance corridor clear for 0.6m-wide standing player");
        s.AppendLine("Added colliders: "+obstacles.Length+"; shared materials: "+renderers.SelectMany(r=>r.sharedMaterials).Distinct().Count());
        s.AppendLine("Failures: "+failures+"; compilationFailed: "+EditorUtility.scriptCompilationFailed);
        File.WriteAllText(Evidence+"/validation.txt",s.ToString());return s.ToString();
    }
    public static void Capture()
    {
        CaptureOne("overview",new Vector3(62,43,-57),new Vector3(10,1,0));
        CaptureOne("house-front",new Vector3(29,9,-11),new Vector3(16,3.1f,8));
        CaptureOne("street",new Vector3(46,2.1f,-5),new Vector3(8,2,-1));
        CaptureOne("entrance",new Vector3(16.9f,1.7f,-.2f),new Vector3(16.9f,2.8f,6));
    }
    static void CaptureOne(string name,Vector3 from,Vector3 target)
    {
        var go=new GameObject("Neighborhood preview"){hideFlags=HideFlags.HideAndDontSave};var c=go.AddComponent<Camera>();
        c.transform.position=from;c.transform.LookAt(target);c.fieldOfView=60;c.nearClipPlane=.08f;c.farClipPlane=250;
        var rt=RenderTexture.GetTemporary(1600,1000,24);var previous=RenderTexture.active;
        try
        {
            c.targetTexture=rt;c.Render();RenderTexture.active=rt;
            var tex=new Texture2D(1600,1000,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1600,1000),0,0);tex.Apply();
            File.WriteAllBytes(Evidence+"/"+name+".png",tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);
        }
        finally{c.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(go);}
    }
}
