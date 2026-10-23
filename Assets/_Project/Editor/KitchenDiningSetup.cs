using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class KitchenDiningSetup
{
    static double next;
    static KitchenDiningSetup() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (EditorApplication.timeSinceStartup < next || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        next = EditorApplication.timeSinceStartup + 1;
        const string request = "Temp/kitchen-dining-request.txt";
        if (!File.Exists(request)) return;
        string command = File.ReadAllText(request).Trim(); File.Delete(request);
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Requires Edit mode.");
            if (command == "inspect") Inspect();
            if (command == "preview") Preview();
            if (command == "apply") Apply();
            if (command == "capture") Capture("Temp/kitchen-after.png",new Vector3(14.1f,3.25f,5.25f),new Vector3(12.7f,1.8f,7.4f));
            if (command == "face-table")
            {
                var scene=SceneManager.GetActiveScene();
                var set=scene.GetRootGameObjects().Single(g=>g.name=="Vicsi_House").transform.Find("KitchenDiningSet");
                foreach(Transform chair in set)
                {
                    if(!chair.name.Contains("Chair"))continue;
                    Undo.RecordObject(chair,"Face kitchen chairs toward table");
                    var b=BoundsOf(chair.gameObject); var center=b.center; var floor=b.min.y;
                    chair.rotation=Quaternion.Euler(0,chair.name.EndsWith("West")?270:90,0);
                    b=BoundsOf(chair.gameObject); chair.position+=new Vector3(center.x-b.center.x,floor-b.min.y,center.z-b.center.z);
                }
                EditorSceneManager.MarkSceneDirty(scene); if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");
                File.AppendAllText("Temp/kitchen-dining-result.txt","Both chairs face inward toward table. Scene saved clean.\n");
                Capture("Temp/kitchen-after.png",new Vector3(14.1f,3.25f,5.25f),new Vector3(12.7f,1.8f,7.4f));
            }
        }
        catch (Exception e) { File.WriteAllText("Temp/kitchen-dining-error.txt", e.ToString()); }
    }
    static string PathOf(Transform t) { return t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name; }
    static Bounds BoundsOf(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        var b = new Bounds(go.transform.position, Vector3.zero);
        if (renderers.Length > 0) { b = renderers[0].bounds; foreach (var r in renderers) b.Encapsulate(r.bounds); }
        return b;
    }
    static void Inspect()
    {
        var scene = SceneManager.GetActiveScene();
        var s = new StringBuilder($"scene={scene.path} dirty={scene.isDirty} compilationFailed={EditorUtility.scriptCompilationFailed}\n");
        foreach (var root in scene.GetRootGameObjects()) s.AppendLine($"ROOT {root.name} active={root.activeSelf} p={root.transform.position:F3}");
        foreach (var t in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)))
            if (PathOf(t).Contains("Vicsi_House") || t.name.IndexOf("kitchen", StringComparison.OrdinalIgnoreCase)>=0)
            { var b = BoundsOf(t.gameObject); s.AppendLine($"{PathOf(t)} p={t.position:F3} rot={t.eulerAngles:F1} min={b.min:F3} max={b.max:F3}"); }
        File.WriteAllText("Temp/kitchen-dining-inspect.txt",s.ToString());
    }
    static void Preview()
    {
        Capture("Temp/kitchen-before.png", new Vector3(15.5f,3.7f,5.0f), new Vector3(12.6f,1.9f,7.5f));
        string[] paths = { "Tables/Table01", "Tables/Table02", "Tables/Table03", "Tables/Table04", "Tables/Table05", "Tables/Table06", "Tables/Table19", "Tables/Table20", "Chairs/Chair01", "Chairs/Chair02", "Chairs/Chair03", "Chairs/Chair04", "Chairs/Chair05", "Chairs/Chair06", "Chairs/Chair07", "Chairs/Chair08" };
        var atlas = new Texture2D(1024,1024,TextureFormat.RGB24,false);
        var report = new StringBuilder();
        for (int i=0;i<paths.Length;i++)
        {
            var util = new PreviewRenderUtility();
            GameObject instance = null;
            try
            {
                instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Furniture Mega Pack/Prefabs/"+paths[i]+".prefab"));
                util.AddSingleGO(instance);
                var b = BoundsOf(instance); report.AppendLine($"{i}: {paths[i]} size={b.size:F3}");
                util.camera.transform.position = b.center + new Vector3(1.4f,1.0f,1.6f)*b.size.magnitude;
                util.camera.transform.LookAt(b.center); util.camera.fieldOfView=30;
                util.camera.nearClipPlane=.01f; util.camera.farClipPlane=100;
                util.camera.backgroundColor = new Color(.24f,.26f,.29f); util.camera.clearFlags=CameraClearFlags.SolidColor;
                util.lights[0].intensity=1.3f; util.lights[0].transform.rotation=Quaternion.Euler(40,30,0);
                util.lights[1].intensity=.8f; util.ambientColor=new Color(.5f,.5f,.5f);
                util.BeginStaticPreview(new Rect(0,0,256,256)); util.Render(true);
                var tex=util.EndStaticPreview(); atlas.SetPixels((i%4)*256,(3-i/4)*256,256,256,tex.GetPixels());
                UnityEngine.Object.DestroyImmediate(tex);
            }
            finally { util.Cleanup(); }
        }
        atlas.Apply(); File.WriteAllBytes("Temp/kitchen-candidates.png",atlas.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(atlas);
        File.WriteAllText("Temp/kitchen-candidates.txt",report.ToString());
    }
    [MenuItem("ViCSI/Scene Setup/Furnish New House Kitchen")]
    public static void Apply()
    {
        var scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlayingOrWillChangePlaymode || scene.path!="Assets/_Project/Scenes/Scenarios/CSI_Environment.unity")
            throw new InvalidOperationException("Open CSI_Environment outside Play mode.");
        var house=scene.GetRootGameObjects().Single(g=>g.name=="Vicsi_House");
        if(house.transform.Find("KitchenDiningSet")!=null) throw new InvalidOperationException("Kitchen set already exists.");
        if(!EditorSceneManager.SaveScene(scene,"Temp/kitchen-before-furnishing.unity",true)) throw new IOException("Snapshot failed.");
        Undo.IncrementCurrentGroup(); Undo.SetCurrentGroupName("Furnish new house kitchen");
        var group=new GameObject("KitchenDiningSet"); Undo.RegisterCreatedObjectUndo(group,"Kitchen dining set");
        group.transform.SetParent(house.transform,false); group.transform.SetPositionAndRotation(new Vector3(13.05f,1.2f,7.05f),Quaternion.identity);
        group.transform.localScale=Vector3.one;
        Place("Tables/Table19","Kitchen Dining Table",group.transform,new Vector3(13.05f,1.2f,7.05f),0,.475f);
        Place("Chairs/Chair02","Kitchen Dining Chair West",group.transform,new Vector3(12.20f,1.2f,7.05f),270,.50f);
        Place("Chairs/Chair02","Kitchen Dining Chair East",group.transform,new Vector3(13.90f,1.2f,7.05f),90,.50f);
        var report=new StringBuilder();
        foreach(Transform t in group.transform)
        { var b=BoundsOf(t.gameObject); if(Mathf.Abs(b.min.y-1.2f)>.002f)throw new InvalidOperationException("Furniture not grounded.");
          report.AppendLine($"{t.name} source={PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject)} min={b.min:F3} max={b.max:F3} size={b.size:F3}"); }
        report.AppendLine($"Compilation failed={EditorUtility.scriptCompilationFailed}; playing={EditorApplication.isPlaying}");
        EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");
        report.AppendLine($"Saved scene={scene.path}; dirty={scene.isDirty}");
        File.WriteAllText("Temp/kitchen-dining-result.txt",report.ToString());
        Selection.activeGameObject=group;
        SceneView.lastActiveSceneView?.Frame(BoundsOf(group),false);
        Capture("Temp/kitchen-after.png",new Vector3(14.1f,3.25f,5.25f),new Vector3(12.7f,1.8f,7.4f));
    }
    static GameObject Place(string asset,string name,Transform parent,Vector3 center,float yaw,float scale)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Furniture Mega Pack/Prefabs/"+asset+".prefab");
        if(prefab==null)throw new InvalidOperationException("Missing furniture: "+asset);
        var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent.gameObject.scene);
        Undo.RegisterCreatedObjectUndo(go,"Add kitchen furniture"); go.name=name;
        go.transform.SetParent(parent,false); go.transform.SetPositionAndRotation(center,Quaternion.Euler(0,yaw,0));
        go.transform.localScale=Vector3.one*scale;
        var b=BoundsOf(go); go.transform.position+=new Vector3(center.x-b.center.x,center.y-b.min.y,center.z-b.center.z);
        return go;
    }
    static void Capture(string path,Vector3 from,Vector3 target)
    {
        var scene=SceneManager.GetActiveScene(); bool wasDirty=scene.isDirty;
        var go=new GameObject("Kitchen diagnostic camera"); go.hideFlags=HideFlags.HideAndDontSave;
        var camera=go.AddComponent<Camera>(); camera.transform.position=from; camera.transform.LookAt(target);
        camera.fieldOfView=65; camera.nearClipPlane=.05f; camera.farClipPlane=80;
        var rt=RenderTexture.GetTemporary(1200,900,24); var previous=RenderTexture.active;
        try { camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt;
            var tex=new Texture2D(1200,900,TextureFormat.RGB24,false); tex.ReadPixels(new Rect(0,0,1200,900),0,0); tex.Apply();
            File.WriteAllBytes(path,tex.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(tex); }
        finally { camera.targetTexture=null; RenderTexture.active=previous; RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.DestroyImmediate(go);
            if(!wasDirty && scene.isDirty)EditorSceneManager.SaveScene(scene); }
    }
}
