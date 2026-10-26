using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>A framed Philippine flag on the rear lobby wall, behind the player's starting view.</summary>
public static class LobbyFlagSetup
{
    const string Folder = "Assets/_Project/Art/Decor/PhilippineFlag";
    const string RootName = "Framed Philippine flag";

    [MenuItem("ViCSI/Scene Setup/Add Philippine Lobby Flag")]
    public static void Apply()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/_Project/Scenes/Main.unity")
            throw new InvalidOperationException("Open Main outside Play Mode before placing the flag.");
        Directory.CreateDirectory("Temp");
        EditorSceneManager.SaveScene(scene, "Temp/lobby-before-framed-flag.unity", true);
        CreateOrReplace(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the framed flag.");
    }

    public static void CreateOrReplace(Scene scene)
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Philippines.png");
        var oak = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Lobby/Natural oak.mat");
        var paper = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Lobby/Warm plaster.mat");
        if (texture == null || oak == null || paper == null)
            throw new InvalidOperationException("The flag image or lobby frame materials are missing.");
        string path = Folder + "/Framed flag print.mat";
        var print = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (print == null)
        {
            print = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(print, path);
        }
        print.SetTexture("_BaseMap", texture);
        print.SetColor("_BaseColor", Color.white);
        print.SetFloat("_Smoothness", .08f);
        print.SetFloat("_Metallic", 0);
        EditorUtility.SetDirty(print);
        foreach (var previous in scene.GetRootGameObjects().Where(g =>
            g.name == RootName || g.name == "Philippine office flag display"))
            Undo.DestroyObjectImmediate(previous);
        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Frame Philippine flag on rear wall");
        SceneManager.MoveGameObjectToScene(root, scene);
        root.transform.position = new Vector3(-1.84f, 2.15f, 9.70f);
        Box(root.transform, "Oak flag frame", Vector3.zero, new Vector3(2.16f,1.18f,.06f), oak);
        Box(root.transform, "Cream picture mount", new Vector3(0,0,-.04f), new Vector3(2.04f,1.06f,.012f), paper);
        var image = GameObject.CreatePrimitive(PrimitiveType.Quad);
        image.name = "Philippine flag wall print";
        image.transform.SetParent(root.transform, false);
        image.transform.localPosition = new Vector3(0,0,-.05f);
        image.transform.localScale = new Vector3(1.92f,.96f,1);
        image.GetComponent<Renderer>().sharedMaterial = print;
        image.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        UnityEngine.Object.DestroyImmediate(image.GetComponent<Collider>());
    }

    static void Box(Transform parent, string name, Vector3 center, Vector3 size, Material material)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = center;
        g.transform.localScale = size;
        g.GetComponent<Renderer>().sharedMaterial = material;
        UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
    }
}
