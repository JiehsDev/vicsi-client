#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Persistent printed tape assets: repeated, readable lettering on both broad faces.</summary>
[InitializeOnLoad]
public static class PoliceCordonLetteringSetup
{
    const string AssetsFolder = "Assets/_Project/Art/Textures/PoliceTape";
    static PoliceCordonLetteringSetup() { EditorApplication.update += PollPreview; }
    static void PollPreview()
    {
        const string request = "Temp/police-tape-preview.txt";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(request);
        try { CapturePreview(); }
        catch (Exception e) { File.WriteAllText("docs/evidence/police-line-lettering-2026-10-20/preview-error.txt",e.ToString()); }
    }
    [MenuItem("Tools/ViCSI/Scene Setup/Preview Police Tape Lettering")]
    public static void CapturePreview()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty) throw new InvalidOperationException("Preview requires a saved scene outside Play mode.");
        var temp = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        var go = new GameObject("Printed cordon preview camera");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,temp);
        var camera = go.AddComponent<Camera>(); camera.enabled=false; camera.scene=scene; camera.nearClipPlane=.03f; camera.fieldOfView=62;
        var rt = RenderTexture.GetTemporary(1440,1080,24); var prior=RenderTexture.active;
        var texture = new Texture2D(1440,1080,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture=rt;
            var from=new[]{new Vector3(17.2f,2.8f,4.6f),new Vector3(18.9f,1.35f,-1.25f),new Vector3(18.9f,1.35f,1.55f),new Vector3(20.9f,2.6f,12.2f)};
            var target=new[]{new Vector3(17.1f,2.2f,6.3f),new Vector3(18.9f,.8f,.15f),new Vector3(18.9f,.8f,.15f),new Vector3(19.67f,2,12.08f)};
            var names=new[]{"front-door","outer-front","outer-back","side-door"};
            Directory.CreateDirectory("docs/evidence/police-line-lettering-2026-10-20");
            for(int i=0;i<from.Length;i++)
            {
                camera.transform.position=from[i]; camera.transform.LookAt(target[i]); camera.Render(); RenderTexture.active=rt;
                texture.ReadPixels(new Rect(0,0,1440,1080),0,0); texture.Apply();
                File.WriteAllBytes("docs/evidence/police-line-lettering-2026-10-20/"+names[i]+".png",texture.EncodeToPNG());
            }
        }
        finally
        {
            camera.targetTexture=null; RenderTexture.active=prior; RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene); EditorSceneManager.CloseScene(temp,true);
        }
    }
    [MenuItem("Tools/ViCSI/Scene Setup/Add Police Line Tape Lettering")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || scene.isDirty
            || scene.path != "Assets/_Project/Scenes/Scenarios/CSI_Environment.unity")
            throw new InvalidOperationException("Open the saved CSI_Environment outside Play mode first.");
        const string evidence = "docs/evidence/police-line-lettering-2026-10-20";
        Directory.CreateDirectory(evidence);
        if (!File.Exists(evidence + "/CSI_Environment-before.unity")) File.Copy(scene.path,evidence + "/CSI_Environment-before.unity");
        Undo.IncrementCurrentGroup(); Undo.SetCurrentGroupName("Print police cordon tape");
        int count = ApplyToScene();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save printed tape.");
        File.WriteAllText(evidence + "/lettering.txt",count + " tape strips printed on both broad faces. Phrase: POLICE LINE DO NOT CROSS.\n");
    }
    public static int ApplyToScene()
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetsFolder + "/PoliceLineDoNotCross.png");
        if (texture == null) throw new InvalidOperationException("Run Tools/GeneratePoliceTapeTexture.ps1 first.");
        var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture));
        if (importer.wrapMode != TextureWrapMode.Repeat || importer.filterMode != FilterMode.Trilinear || importer.anisoLevel != 8)
        {
            importer.wrapMode = TextureWrapMode.Repeat; importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8; importer.mipmapEnabled = true; importer.maxTextureSize = 2048; importer.SaveAndReimport();
        }
        var plain = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Placeholders/Cordon_Tape_PLACEHOLDER.mat");
        var cordon = UnityEngine.Object.FindAnyObjectByType<SceneCordon>();
        var outer = GameObject.Find("Outer Cordon - Yard");
        if (cordon == null || outer == null) throw new InvalidOperationException("Expected both existing cordons.");
        var renderers = cordon.GetComponentsInChildren<MeshRenderer>().Concat(outer.GetComponentsInChildren<MeshRenderer>()).ToArray();
        int count = 0;
        foreach (var renderer in renderers)
        {
            // Restrict to the authored tape, never the signs or unrelated scene materials.
            if (!(renderer.name.StartsWith("Tape_") || renderer.name == "Side door seal" || renderer.name.StartsWith("Outer gate "))) continue;
            var scale = renderer.transform.lossyScale;
            bool alongZ = scale.z > scale.x;
            float length = alongZ ? scale.z : scale.x;
            int repeats = Mathf.Max(1,Mathf.RoundToInt(length/.8f));
            string matPath = AssetsFolder + "/PoliceTapeRepeat" + repeats + ".mat";
            var printed = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (printed == null)
            {
                printed = new Material(plain) { name = "Police tape - " + repeats + " repeats" };
                printed.SetColor("_BaseColor",Color.white); printed.SetColor("_Color",Color.white);
                printed.SetTexture("_BaseMap",texture); printed.SetTextureScale("_BaseMap",new Vector2(repeats,1));
                printed.SetTexture("_MainTex",texture); printed.SetTextureScale("_MainTex",new Vector2(repeats,1));
                AssetDatabase.CreateAsset(printed,matPath);
            }
            var filter = renderer.GetComponent<MeshFilter>();
            var mesh = PrintedMesh(filter.sharedMesh,alongZ);
            Undo.RecordObject(filter,"Print tape faces"); Undo.RecordObject(renderer,"Apply printed tape material");
            filter.sharedMesh = mesh; renderer.sharedMaterials = new[] { printed,plain }; count++;
        }
        AssetDatabase.SaveAssets();
        return count;
    }
    static Mesh PrintedMesh(Mesh original,bool alongZ)
    {
        string path = AssetsFolder + (alongZ ? "/TapeAlongZ.asset" : "/TapeAlongX.asset");
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (mesh != null) return mesh;
        mesh = UnityEngine.Object.Instantiate(original); mesh.name = alongZ ? "Printed tape along Z" : "Printed tape along X";
        var vertices = mesh.vertices; var normals = mesh.normals; var uv = mesh.uv;
        for (int i=0;i<vertices.Length;i++)
        {
            var v = vertices[i]; var n = normals[i];
            // Each opposing face receives its own readable print rather than mirrored lettering.
            uv[i] = new Vector2((alongZ ? v.z*(n.x>0?1:-1) : v.x*(n.z<0?1:-1))+.5f,v.y+.5f);
        }
        var print = new List<int>(); var edges = new List<int>(); var triangles = mesh.triangles;
        for (int i=0;i<triangles.Length;i+=3)
        {
            var n = normals[triangles[i]];
            var target = (alongZ ? Mathf.Abs(n.x) : Mathf.Abs(n.z))>.9f ? print : edges;
            target.Add(triangles[i]); target.Add(triangles[i+1]); target.Add(triangles[i+2]);
        }
        mesh.uv=uv; mesh.subMeshCount=2; mesh.SetTriangles(print,0); mesh.SetTriangles(edges,1);
        AssetDatabase.CreateAsset(mesh,path); return mesh;
    }
}
#endif
