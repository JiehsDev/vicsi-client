using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Real museum artwork in the existing lobby frames. Sources and CC0 records live beside the textures.</summary>
public static class LobbyPaintingSetup
{
    const string Folder = "Assets/_Project/Art/Decor/LobbyPaintings";
    static readonly string[] Files = { "VanGogh_SelfPortrait", "VanGogh_Roses", "VanGogh_Cypresses", "Manet_MonetFamilyGarden" };
    static readonly string[] Titles = { "Self-Portrait with a Straw Hat", "Roses", "Cypresses", "The Monet Family in Their Garden at Argenteuil" };

    [MenuItem("ViCSI/Scene Setup/Replace Lobby Paintings")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/_Project/Scenes/Main.unity")
            throw new InvalidOperationException("Open Main outside Play Mode before replacing its paintings.");
        var design = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "LobbyInteriorDesign");
        if (design == null) throw new InvalidOperationException("LobbyInteriorDesign was not found.");
        var frames = design.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Framed forensic print").ToArray();
        if (frames.Length != 4) throw new InvalidOperationException("Expected the four existing lobby frames.");
        // Validate all images before changing the scene.
        for (int i = 0; i < Files.Length; i++) MaterialFor(i);
        Directory.CreateDirectory("Temp");
        EditorSceneManager.SaveScene(scene, "Temp/lobby-before-museum-paintings.unity", true);
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Replace lobby prints with museum paintings");
        foreach (var t in design.GetComponentsInChildren<Transform>(true))
            if (t != null && (t.name == "Abstract evidence line" || t.name.StartsWith("Museum painting - ")))
                Undo.DestroyObjectImmediate(t.gameObject);
        foreach (var frame in frames) CreatePainting(frame);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the updated lobby scene.");
        Debug.Log("[Lobby paintings] Four museum paintings installed in the existing frames.");
    }

    public static void CreatePainting(Transform frame)
    {
        int index = (frame.position.x < 0 ? 0 : 2) + (frame.position.z > 4 ? 1 : 0);
        var material = MaterialFor(index);
        var texture = material.mainTexture;
        float aspect = (float)texture.width / texture.height;
        float width = Mathf.Min(.78f, 1.12f * aspect);
        float height = width / aspect;
        var image = GameObject.CreatePrimitive(PrimitiveType.Quad);
        image.name = "Museum painting - " + Titles[index];
        Undo.RegisterCreatedObjectUndo(image, "Lobby painting");
        image.transform.SetParent(frame.parent, false);
        // Unity's quad faces local -Z; rotate it so the image faces into the room.
        image.transform.SetPositionAndRotation(frame.position + frame.forward * .049f,
            frame.rotation * Quaternion.Euler(0, 180, 0));
        image.transform.localScale = new Vector3(width, height, 1);
        UnityEngine.Object.DestroyImmediate(image.GetComponent<Collider>());
        var renderer = image.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static Material MaterialFor(int index)
    {
        string path = Folder + "/" + Files[index];
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path + ".jpg");
        if (texture == null) throw new InvalidOperationException("Missing museum painting: " + path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path + ".jpg");
        if (importer.maxTextureSize != 1024 || importer.wrapMode != TextureWrapMode.Clamp || !importer.mipmapEnabled || importer.npotScale != TextureImporterNPOTScale.None)
        {
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 1024;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
        }
        var material = AssetDatabase.LoadAssetAtPath<Material>(path + ".mat");
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path + ".mat");
        }
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Smoothness", .08f);
        material.SetFloat("_Metallic", 0);
        EditorUtility.SetDirty(material);
        return material;
    }
}
