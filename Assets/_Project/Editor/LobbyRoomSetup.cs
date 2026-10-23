using System;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class LobbyRoomSetup
{
    const string MaterialFolder = "Assets/_Project/Art/Materials/Lobby";
    static Transform design;
    static Material wall, navy, oak, brass, stone, fabric, green, dark, glow, rug;
    static double next;
    static LobbyRoomSetup() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (EditorApplication.timeSinceStartup < next || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        next = EditorApplication.timeSinceStartup + 1;
        const string request = "Temp/lobby-room-request.txt";
        if (!File.Exists(request)) return;
        string command = File.ReadAllText(request).Trim(); File.Delete(request);
        try
        {
            if (command == "inspect") Inspect();
            else if (command == "apply") Apply();
            else if (command == "capture") Capture();
        }
        catch (Exception ex) { File.WriteAllText("Temp/lobby-room-error.txt", ex.ToString()); }
    }
    [MenuItem("ViCSI/Scene Setup/Restyle Lobby Room")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != "Assets/_Project/Scenes/Main.unity")
            throw new InvalidOperationException("Open Main outside Play mode.");
        if (!File.Exists("Temp/lobby-room-before.unity")) EditorSceneManager.SaveScene(scene,"Temp/lobby-room-before.unity",true);
        Undo.IncrementCurrentGroup(); Undo.SetCurrentGroupName("Restyle lobby room");
        var previous = scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="LobbyInteriorDesign");
        if (previous != null) Undo.DestroyObjectImmediate(previous);
        var root = new GameObject("LobbyInteriorDesign"); Undo.RegisterCreatedObjectUndo(root,"Lobby interior"); design=root.transform;
        Directory.CreateDirectory(MaterialFolder);
        wall=Mat("Warm plaster",new Color(.78f,.77f,.72f),.16f);
        navy=Mat("Midnight blue",new Color(.075f,.13f,.18f),.28f);
        oak=Mat("Natural oak",new Color(.47f,.30f,.16f),.25f);
        brass=Mat("Brushed brass",new Color(.62f,.46f,.23f),.38f,.55f);
        stone=Mat("Limestone",new Color(.48f,.49f,.46f),.27f);
        fabric=Mat("Sage upholstery",new Color(.23f,.34f,.30f),.07f);
        green=Mat("Plant foliage",new Color(.10f,.25f,.14f),.15f);
        dark=Mat("Charcoal metal",new Color(.055f,.068f,.073f),.4f,.4f);
        rug=Mat("Woven sand",new Color(.43f,.39f,.32f),.04f);
        glow=Mat("Warm luminous",new Color(.91f,.80f,.62f),.2f);
        glow.EnableKeyword("_EMISSION"); glow.SetColor("_EmissionColor",new Color(1f,.87f,.68f)*2f); EditorUtility.SetDirty(glow);
        var existing=UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude,FindObjectsSortMode.None);
        foreach(var r in existing)
        {
            var b=r.bounds; bool inRoom=b.center.x>-6.1f && b.center.x<2.6f && b.center.z>1.2f && b.center.z<10.1f;
            if(inRoom && (r.name.StartsWith("Wall_") || r.name.StartsWith("Pillar_")))
            { Undo.RecordObject(r,"Lobby wall finish"); r.sharedMaterial=wall; }
            else if(r.name=="Floor" && inRoom && b.center.y<.1f)
            { Undo.RecordObject(r,"Lobby floor finish"); r.sharedMaterial=stone; }
            else if(r.name=="Floor (1)" && b.center.y>3.9f && b.size.x>15)
            { Undo.RecordObject(r,"Lobby ceiling finish"); r.sharedMaterial=wall; }
        }
        foreach(var g in scene.GetRootGameObjects().Where(g=>g.name.StartsWith("Ceiling Light")))
        { Undo.RecordObject(g,"Replace fluorescent lights"); g.SetActive(false); }
        // Room footprint remains approximately 8 x 8 metres, with its original four-metre ceiling.
        Box("Front feature wall",new Vector3(-1.84f,2.05f,1.78f),new Vector3(5.6f,3.2f,.08f),navy);
        for(int i=0;i<14;i++)
        {
            Box("Oak fluting left",new Vector3(-5.35f+i*.10f,1.85f,1.78f),new Vector3(.05f,3.5f,.07f),oak);
            Box("Oak fluting right",new Vector3(.45f+i*.10f,1.85f,1.78f),new Vector3(.05f,3.5f,.07f),oak);
        }
        // The menu supplies the branding; keep its background quiet and readable.
        foreach(float x in new[]{-5.59f,1.90f})
        {
            Box("Side wall lower panel",new Vector3(x,.48f,5.72f),new Vector3(.08f,.9f,7.70f),navy);
            Box("Oak panel cap",new Vector3(x,.95f,5.72f),new Vector3(.10f,.05f,7.70f),oak);
            Box("Brass skirting",new Vector3(x,.10f,5.72f),new Vector3(.10f,.065f,7.70f),brass);
        }
        Box("Rear wall lower panel",new Vector3(-1.84f,.48f,9.72f),new Vector3(7.4f,.9f,.07f),navy);
        Box("Rear oak cap",new Vector3(-1.84f,.95f,9.72f),new Vector3(7.4f,.05f,.09f),oak);
        // Clear centre circulation and a desk that stays entirely below the 1.1-metre UI bottom.
        Box("Reception body",new Vector3(-1.84f,.45f,6.82f),new Vector3(2.4f,.86f,.62f),navy,true);
        Box("Reception oak top",new Vector3(-1.84f,.91f,6.82f),new Vector3(2.48f,.07f,.70f),oak,true);
        Box("Reception brass reveal",new Vector3(-1.84f,.77f,7.14f),new Vector3(2.3f,.025f,.016f),brass);
        for(int i=0;i<18;i++) Box("Reception oak batten",new Vector3(-2.96f+i*.13f,.46f,7.15f),new Vector3(.035f,.54f,.022f),oak);
        Box("Reception toe kick",new Vector3(-1.84f,.045f,6.82f),new Vector3(2.15f,.07f,.48f),dark);
        Box("Desk organiser",new Vector3(-2.75f,.97f,6.85f),new Vector3(.25f,.045f,.19f),dark);
        Furniture("Assets/Furniture Mega Pack/Prefabs/Chairs/Chair01.prefab","Reception chair",new Vector3(-1.1f,0,6.05f),180,.52f,navy);
        Box("Lounge rug",new Vector3(-1.85f,.015f,4.50f),new Vector3(4.65f,.022f,3.45f),rug);
        Box("Rug border front",new Vector3(-1.85f,.028f,2.86f),new Vector3(4.5f,.002f,.06f),oak);
        Box("Rug border rear",new Vector3(-1.85f,.028f,6.14f),new Vector3(4.5f,.002f,.06f),oak);
        Furniture("Assets/Furniture Mega Pack/Prefabs/Sofas/Sofa01.prefab","West lounge sofa",new Vector3(-4.76f,0,4.35f),90,2.3f,fabric);
        Furniture("Assets/Furniture Mega Pack/Prefabs/Sofas/Sofa01.prefab","East lounge sofa",new Vector3(.95f,0,4.35f),-90,2.3f,fabric);
        Cylinder("Round coffee table top",new Vector3(-1.84f,.46f,4.30f),.63f,.065f,oak);
        Cylinder("Coffee table pedestal",new Vector3(-1.84f,.23f,4.30f),.16f,.40f,dark);
        Cylinder("Coffee table base",new Vector3(-1.84f,.035f,4.30f),.37f,.06f,dark);
        Box("Book navy",new Vector3(-2.02f,.51f,4.30f),new Vector3(.28f,.035f,.20f),navy);
        Box("Book cream",new Vector3(-2.00f,.545f,4.30f),new Vector3(.26f,.025f,.18f),wall);
        Cylinder("Table vase",new Vector3(-1.56f,.60f,4.27f),.075f,.20f,wall);
        for(int i=0;i<5;i++) Leaf("Vase leaf",new Vector3(-1.56f+(i-2)*.028f,.78f,4.27f),new Vector3(.045f,.18f,.04f),Quaternion.Euler(0,i*55,20+i*6));
        foreach(var p in new[]{new Vector3(-4.95f,0,2.4f),new Vector3(1.15f,0,2.4f),new Vector3(-5.02f,0,8.7f),new Vector3(1.13f,0,8.7f)}) Plant(p);
        foreach(float x in new[]{-5.51f,1.83f})
        {
            bool left=x<0;
            for(int i=0;i<2;i++) Art(new Vector3(x,2.05f,3.4f+i*2.05f),left?90:-90,i==0?"OBSERVATION":"EVIDENCE");
        }
        // A recessed ceiling frame and two pendant fixtures replace the oversized fluorescent panels.
        foreach(float x in new[]{-5.25f,1.55f})
        {
            Box("Ceiling oak perimeter",new Vector3(x,3.88f,5.70f),new Vector3(.15f,.10f,7.35f),oak);
            Box("Ceiling warm strip",new Vector3(x,3.82f,5.70f),new Vector3(.035f,.018f,7.1f),glow);
        }
        foreach(float z in new[]{2.05f,9.35f})
        {
            Box("Ceiling oak crosspiece",new Vector3(-1.85f,3.88f,z),new Vector3(6.95f,.10f,.15f),oak);
            Box("Ceiling warm strip",new Vector3(-1.85f,3.82f,z),new Vector3(6.8f,.018f,.035f),glow);
        }
        foreach(float x in new[]{-3.35f,-.35f})
        {
            Cylinder("Pendant cable",new Vector3(x,3.58f,4.45f),.014f,.68f,dark);
            Cylinder("Pendant shade",new Vector3(x,3.24f,4.45f),.34f,.13f,navy);
            Cylinder("Pendant diffuser",new Vector3(x,3.17f,4.45f),.31f,.018f,glow);
            LightAt("Warm lounge light",new Vector3(x,3.08f,4.45f),new Color(1,.87f,.72f),1.8f,5.8f);
        }
        LightAt("Reception fill",new Vector3(-1.85f,3.2f,7.65f),new Color(.87f,.93f,1),2.2f,5.5f);
        LightAt("Feature wall wash",new Vector3(-1.85f,3.2f,2.7f),new Color(1,.89f,.75f),1.5f,4.5f);
        RenderSettings.ambientMode=AmbientMode.Flat;
        RenderSettings.ambientLight=new Color(.44f,.47f,.49f);
        RenderSettings.reflectionIntensity=.45f;
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Inspect(); Capture();
        File.WriteAllText("Temp/lobby-room-applied.txt",$"Room restyled. Added meshes={root.GetComponentsInChildren<MeshRenderer>().Length}, lights={root.GetComponentsInChildren<Light>().Length}. Existing footprint and UI retained. sceneDirty={scene.isDirty}");
    }
    static Material Mat(string name,Color color,float smooth,float metal=0)
    {
        string path=$"{MaterialFolder}/{name}.mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null) {m=new Material(Shader.Find("Universal Render Pipeline/Lit")); m.name=name; AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",color); m.SetFloat("_Smoothness",smooth); m.SetFloat("_Metallic",metal); m.enableInstancing=true; EditorUtility.SetDirty(m); return m;
    }
    static GameObject Box(string name,Vector3 position,Vector3 size,Material material,bool solid=false)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube); g.name=name; g.transform.SetParent(design);g.transform.position=position;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=material;
        if(!solid) UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }
    static void Cylinder(string name,Vector3 position,float radius,float height,Material material)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=name;g.transform.SetParent(design);g.transform.position=position;g.transform.localScale=new Vector3(radius*2,height/2,radius*2);g.GetComponent<Renderer>().sharedMaterial=material;UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
    }
    static void Leaf(string name,Vector3 p,Vector3 scale,Quaternion rotation)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Sphere);g.name=name;g.transform.SetParent(design);g.transform.SetPositionAndRotation(p,rotation);g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=green;UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
    }
    static void Plant(Vector3 p)
    {
        Cylinder("Planter brass foot",p+Vector3.up*.04f,.22f,.07f,brass);
        Cylinder("Ivory planter",p+Vector3.up*.29f,.235f,.49f,wall);
        Cylinder("Planter soil",p+Vector3.up*.54f,.22f,.025f,dark);
        for(int i=0;i<14;i++)
        {
            float angle=i*2.399963f; float h=.80f+(i%5)*.15f;
            Vector3 tip=p+new Vector3(Mathf.Cos(angle)*.16f,h,Mathf.Sin(angle)*.16f);
            Cylinder("Plant stem",p+new Vector3(Mathf.Cos(angle)*.055f,(h+.5f)/2,Mathf.Sin(angle)*.055f),.009f,h-.5f,green);
            Leaf("Plant leaf",tip,new Vector3(.16f,.39f,.075f),Quaternion.Euler(28,angle*Mathf.Rad2Deg,32));
        }
    }
    static void Furniture(string path,string name,Vector3 feet,float yaw,float width,Material material)
    {
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path); if(asset==null) throw new InvalidOperationException(path);
        var g=(GameObject)PrefabUtility.InstantiatePrefab(asset);g.name=name;g.transform.SetParent(design);g.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
        var renderers=g.GetComponentsInChildren<Renderer>(); var bounds=renderers[0].bounds;foreach(var r in renderers) bounds.Encapsulate(r.bounds);
        float scale=width/Mathf.Max(bounds.size.x,.01f);g.transform.localScale*=scale;g.transform.rotation=Quaternion.Euler(0,yaw,0);
        bounds=renderers[0].bounds;foreach(var r in renderers) {bounds.Encapsulate(r.bounds);r.sharedMaterials=r.sharedMaterials.Select(_=>material).ToArray();}
        g.transform.position+=feet-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
        foreach(var c in g.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(c);
        foreach(var b in g.GetComponentsInChildren<Behaviour>()) if(!(b is Animator)) b.enabled=false;
    }
    static void Text(string name,string value,Vector3 position,float fontSize,Color color,Vector2 size)
    {
        var g=new GameObject(name);g.transform.SetParent(design);g.transform.SetPositionAndRotation(position,Quaternion.Euler(0,180,0));
        var t=g.AddComponent<TextMeshPro>();t.text=value;t.fontSize=fontSize;t.color=color;t.alignment=TextAlignmentOptions.Center;t.rectTransform.sizeDelta=size;t.raycastTarget=false;
    }
    static void Art(Vector3 p,float yaw,string title)
    {
        var g=Box("Framed forensic print",p,new Vector3(.95f,1.3f,.055f),oak);g.transform.rotation=Quaternion.Euler(0,yaw,0);
        Vector3 f=g.transform.forward;
        var paper=Box("Print paper",p+f*.04f,new Vector3(.86f,1.2f,.012f),wall);paper.transform.rotation=g.transform.rotation;
        for(int i=0;i<5;i++)
        {
            var line=Box("Abstract evidence line",p+f*.05f+Vector3.up*(i-2)*.13f,new Vector3(.5f-i*.055f,.028f,.009f),i%2==0?navy:brass);line.transform.rotation=g.transform.rotation;
        }
    }
    static void LightAt(string name,Vector3 p,Color color,float intensity,float range)
    {
        var g=new GameObject(name);g.transform.SetParent(design);g.transform.position=p;var l=g.AddComponent<Light>();l.type=LightType.Point;l.color=color;l.intensity=intensity;l.range=range;l.shadows=LightShadows.None;
    }
    static void Inspect()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var s=new StringBuilder();s.AppendLine($"scene={scene.path} dirty={scene.isDirty} playing={EditorApplication.isPlaying} compilationFailed={EditorUtility.scriptCompilationFailed}");
        foreach(var root in scene.GetRootGameObjects())s.AppendLine($"ROOT {root.name} active={root.activeSelf} pos={root.transform.position:F2}");
        foreach(var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude,FindObjectsSortMode.None))s.AppendLine($"MESH {r.name} centre={r.bounds.center:F2} size={r.bounds.size:F2} materials={string.Join(",",r.sharedMaterials.Select(m=>m!=null?m.name:"null"))}");
        File.WriteAllText("Temp/lobby-room-inspect.txt",s.ToString());
    }
    static void Capture()
    {
        SaveView("Temp/lobby-room-main.png",new Vector3(-1.84f,1.60f,8.64f),new Vector3(-1.84f,1.60f,2),false);
        SaveView("Temp/lobby-room-wide.png",new Vector3(.7f,2.1f,8.7f),new Vector3(-2.15f,1.4f,4.3f),true);
    }
    static void SaveView(string path,Vector3 eye,Vector3 target,bool hideUI)
    {
        var canvas=UnityEngine.Object.FindAnyObjectByType<Canvas>();bool was=canvas!=null&&canvas.enabled;
        var g=new GameObject("Temporary Lobby Preview"){hideFlags=HideFlags.HideAndDontSave};var c=g.AddComponent<Camera>();c.CopyFrom(Camera.main);c.enabled=false;c.stereoTargetEye=StereoTargetEyeMask.None;c.fieldOfView=65;c.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye));
        var rt=RenderTexture.GetTemporary(1400,1000,24);var old=RenderTexture.active;
        try {if(hideUI&&canvas!=null)canvas.enabled=false;c.targetTexture=rt;c.Render();RenderTexture.active=rt;var t=new Texture2D(1400,1000,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,1400,1000),0,0);t.Apply();File.WriteAllBytes(path,t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);}
        finally {if(canvas!=null)canvas.enabled=was;RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(g);}
    }
}
