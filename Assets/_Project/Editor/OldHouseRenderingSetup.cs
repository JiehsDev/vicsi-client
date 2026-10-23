using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class OldHouseRenderingSetup
{
    static double next;
    static OldHouseRenderingSetup(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.timeSinceStartup<next || EditorApplication.isCompiling || EditorApplication.isUpdating)return;
        next=EditorApplication.timeSinceStartup+1;
        const string file="Temp/old-house-request.txt";
        if(!File.Exists(file))return;
        var command=File.ReadAllText(file).Trim();File.Delete(file);
        try {if(command=="inspect")Inspect(); if(command=="capture")CaptureAll(); if(command=="apply")Apply(); if(command=="validate")Validate();}
        catch(Exception e){File.WriteAllText("Temp/old-house-error.txt",e.ToString());}
    }
    static string PathOf(Transform t)=>t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
    static void Inspect()
    {
        var scene=SceneManager.GetActiveScene(); var s=new StringBuilder($"{scene.path} dirty={scene.isDirty} playing={EditorApplication.isPlaying} compilationFailed={EditorUtility.scriptCompilationFailed}\n");
        s.AppendLine($"ambient={RenderSettings.ambientMode} sky={RenderSettings.ambientSkyColor} equator={RenderSettings.ambientEquatorColor} ground={RenderSettings.ambientGroundColor} intensity={RenderSettings.ambientIntensity} sun={RenderSettings.sun} skybox={RenderSettings.skybox}");
        var house=scene.GetRootGameObjects().Single(g=>g.name=="Harong");
        foreach(var t in house.GetComponentsInChildren<Transform>(true))
        {
            var r=t.GetComponent<Renderer>(); var mf=t.GetComponent<MeshFilter>();
            s.AppendLine($"{PathOf(t)} active={t.gameObject.activeSelf} p={t.position:F3} rot={t.eulerAngles:F1} scale={t.lossyScale:F3}"+(r==null?"":$" min={r.bounds.min:F3} max={r.bounds.max:F3} enabled={r.enabled} shadow={r.shadowCastingMode} receive={r.receiveShadows} lightmap={r.lightmapIndex} materials="+string.Join(";",r.sharedMaterials.Select(m=>m==null?"NULL":$"{m.name}[{AssetDatabase.GetAssetPath(m)}] shader={m.shader.name}"))));
        }
        foreach(var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            s.AppendLine($"LIGHT {PathOf(l.transform)} active={l.gameObject.activeInHierarchy} enabled={l.enabled} type={l.type} p={l.transform.position:F2} intensity={l.intensity} range={l.range} shadows={l.shadows} bias={l.shadowBias} normalBias={l.shadowNormalBias} mask={l.cullingMask}");
        File.WriteAllText("Temp/old-house-inspect.txt",s.ToString());
    }
    static void CaptureAll()
    {
        Capture("Temp/old-house-kitchen.png",new Vector3(-.5f,1.75f,10.3f),new Vector3(-4.5f,1.3f,13.4f));
        Capture("Temp/old-house-ceiling.png",new Vector3(-1.3f,1.7f,9f),new Vector3(-2f,3.3f,12.5f));
        Capture("Temp/old-house-living.png",new Vector3(1.3f,1.7f,7f),new Vector3(-3f,1.5f,10.7f));
        Capture("Temp/old-house-upstairs.png",new Vector3(-1f,4.2f,10.5f),new Vector3(-4f,4.8f,8f));
        Inspect();
    }
    [MenuItem("ViCSI/Scene Setup/Repair Old House Rendering and Alignment")]
    public static void Apply()
    {
        var scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlayingOrWillChangePlaymode||scene.path!="Assets/_Project/Scenes/Scenarios/CSI_Environment.unity")throw new InvalidOperationException("Open CSI_Environment in Edit mode.");
        var house=scene.GetRootGameObjects().Single(g=>g.name=="Harong").transform;
        if(house.Find("Floor1/Interior_Floor").GetComponent<Renderer>().sharedMaterial.name=="Ceiling Plaster")throw new InvalidOperationException("Repair already applied; undo the repair group before applying again.");
        if(!File.Exists("Temp/old-house-before-repair.unity"))EditorSceneManager.SaveScene(scene,"Temp/old-house-before-repair.unity",true);
        Undo.IncrementCurrentGroup();Undo.SetCurrentGroupName("Repair old house rendering and alignment");
        var anchor=house.Find("Floor1").position;
        foreach(Transform tile in house)
        {
            if(!tile.name.StartsWith("Floor1")&&!tile.name.StartsWith("RoofFloor"))continue;
            Undo.RecordObject(tile,"Align ceiling tile grid");var p=tile.position;
            p.x=anchor.x+Mathf.Round((p.x-anchor.x)/2.5f)*2.5f;
            p.z=anchor.z+Mathf.Round((p.z-anchor.z)/2.5f)*2.5f;tile.position=p;
        }
        const string folder="Assets/_Project/Art/Materials/OldHouse";
        Directory.CreateDirectory(folder);
        string ceilingPath=folder+"/CeilingPlaster.mat";
        var ceiling=AssetDatabase.LoadAssetAtPath<Material>(ceilingPath);
        if(ceiling==null)
        {
            ceiling=new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/ModularHousePack1/Art/Materials/M_Plasterl_001.mat"));
            ceiling.name="Ceiling Plaster";ceiling.SetFloat("_BumpScale",.12f);ceiling.SetFloat("_Smoothness",.08f);
            ceiling.SetFloat("_SmoothnessTextureChannel",0);ceiling.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            ceiling.SetColor("_BaseColor",new Color(.80f,.79f,.75f,1));
            AssetDatabase.CreateAsset(ceiling,ceilingPath);
        }
        foreach(var r in house.GetComponentsInChildren<Renderer>(true))
            if(r.name=="Interior_Floor"&&r.transform.parent.name.StartsWith("Floor1") || r.name=="Interior_FloorToRoof" || r.name=="Stairs1_Ceiling")
            {Undo.RecordObject(r,"Use matte ceiling plaster");r.sharedMaterial=ceiling;}
        foreach(var setName in new[]{"CabinetBset","CabinetBset (1)"})
        {
            var set=house.Find(setName);
            var units=set.Cast<Transform>().Where(t=>t.GetComponent<Renderer>()!=null).ToArray();
            var sink=house.Find("Sink01");var sinkUnit=set.Find("CabinetB_Sink");var oldSink=sinkUnit!=null?sinkUnit.position:Vector3.zero;
            var row=units.Where(t=>Mathf.Abs(Mathf.DeltaAngle(t.eulerAngles.y,setName=="CabinetBset"?180:0))<1).OrderByDescending(t=>t.GetComponent<Renderer>().bounds.max.x).ToArray();
            for(int i=1;i<row.Length;i++)
            {Undo.RecordObject(row[i],"Align adjacent kitchen units");var edge=row[i-1].GetComponent<Renderer>().bounds.min.x;var b=row[i].GetComponent<Renderer>().bounds;
                row[i].position+=new Vector3(edge-b.max.x,0,0);}
            if(sinkUnit!=null&&sink!=null){Undo.RecordObject(sink,"Keep sink aligned with cabinet");sink.position+=sinkUnit.position-oldSink;}
            var corner=set.Find("CabinetBCorner01");
            if(corner!=null)
            {
                float edge=corner.GetComponent<Renderer>().bounds.min.z;
                foreach(var side in units.Where(t=>t!=corner&&Mathf.Abs(Mathf.DeltaAngle(t.eulerAngles.y,90))<1).OrderByDescending(t=>t.GetComponent<Renderer>().bounds.max.z))
                {Undo.RecordObject(side,"Align side counter units");var b=side.GetComponent<Renderer>().bounds;side.position+=new Vector3(corner.position.x-side.position.x,0,edge-b.max.z);edge=side.GetComponent<Renderer>().bounds.min.z;}
            }
            foreach(var unit in units)
            {
                // Imported fronts intersect the carcass at their closed rest pose. Keep their hinges and scripts.
                foreach(Transform front in unit)
                {
                    if(!front.name.Contains("Door")&&!front.name.Contains("Drawer"))continue;
                    Undo.RecordObject(front,"Separate kitchen front surfaces");front.position-=unit.up*.006f;
                }
            }
        }
        var pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        string assetPath=AssetDatabase.GetAssetPath(pipeline);
        if(!File.Exists("Temp/old-house-render-pipeline-before.asset"))File.Copy(assetPath,"Temp/old-house-render-pipeline-before.asset");
        Undo.RecordObject(pipeline,"Keep interior lights consistent across tiles");
        var so=new SerializedObject(pipeline);so.FindProperty("m_AdditionalLightsPerObjectLimit").intValue=8;so.ApplyModifiedProperties();AssetDatabase.SaveAssetIfDirty(pipeline);
        EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");
        CaptureAll();Validate();
    }
    static void Validate()
    {
        var scene=SceneManager.GetActiveScene();var house=scene.GetRootGameObjects().Single(g=>g.name=="Harong").transform;
        var s=new StringBuilder();int failures=0;
        foreach(var prefix in new[]{"Floor1","RoofFloor"})
        {
            var tiles=house.Cast<Transform>().Where(t=>t.name.StartsWith(prefix)).ToArray();
            var anchor=house.Find("Floor1").position;
            foreach(var t in tiles)
            {
                var p=t.position;float x=(p.x-anchor.x)/2.5f,z=(p.z-anchor.z)/2.5f;
                bool pass=Mathf.Abs(x-Mathf.Round(x))<.0001f&&Mathf.Abs(z-Mathf.Round(z))<.0001f;
                if(!pass)failures++;s.AppendLine($"{(pass?"PASS":"FAIL")} aligned {t.name} {p:F4}");
            }
            for(int i=0;i<tiles.Length;i++)for(int j=i+1;j<tiles.Length;j++)
                if(Mathf.Abs(tiles[i].position.x-tiles[j].position.x)<2.499f&&Mathf.Abs(tiles[i].position.z-tiles[j].position.z)<2.499f)
                {failures++;s.AppendLine("FAIL overlapping tiles "+tiles[i].name+" / "+tiles[j].name);}
        }
        s.AppendLine($"failures={failures} playing={EditorApplication.isPlaying} compilationFailed={EditorUtility.scriptCompilationFailed} dirty={scene.isDirty}");
        s.AppendLine("Additional lights limit="+new SerializedObject(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline).FindProperty("m_AdditionalLightsPerObjectLimit").intValue);
        File.WriteAllText("Temp/old-house-validation.txt",s.ToString());
    }
    static void Capture(string path,Vector3 from,Vector3 target)
    {
        var scene=SceneManager.GetActiveScene();bool wasDirty=scene.isDirty;
        var go=new GameObject("Old house diagnostic camera"){hideFlags=HideFlags.HideAndDontSave};
        var c=go.AddComponent<Camera>();c.transform.position=from;c.transform.LookAt(target);c.fieldOfView=75;c.nearClipPlane=File.Exists("Temp/old-house-near.txt")?float.Parse(File.ReadAllText("Temp/old-house-near.txt"),System.Globalization.CultureInfo.InvariantCulture):.1f;c.farClipPlane=100;
        var rt=RenderTexture.GetTemporary(1200,900,24,RenderTextureFormat.Default,RenderTextureReadWrite.Default,4);
        var resolved=RenderTexture.GetTemporary(1200,900,0);var previous=RenderTexture.active;
        try{c.targetTexture=rt;c.Render();Graphics.Blit(rt,resolved);RenderTexture.active=resolved;var tex=new Texture2D(1200,900,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1200,900),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);}
        finally{c.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);RenderTexture.ReleaseTemporary(resolved);UnityEngine.Object.DestroyImmediate(go);if(!EditorApplication.isPlaying&&!wasDirty&&scene.isDirty)EditorSceneManager.SaveScene(scene);}
    }
}






