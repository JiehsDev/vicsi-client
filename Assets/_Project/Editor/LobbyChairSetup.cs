using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Places an office chair beneath the lobby starting view, facing the menu.</summary>
public static class LobbyChairSetup
{
    const string RootName = "Player lobby chair";
    const string PrefabPath = "Assets/Furniture Mega Pack/Prefabs/Chairs/Chair31.prefab";

    [MenuItem("ViCSI/Scene Setup/Add Player Lobby Chair")]
    public static void Apply()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/_Project/Scenes/Main.unity")
            throw new InvalidOperationException("Open Main outside Play Mode before placing the player's chair.");
        Directory.CreateDirectory("Temp");
        EditorSceneManager.SaveScene(scene, "Temp/lobby-before-player-chair.unity", true);
        CreateOrReplace(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the player chair.");
    }

    public static void CreateOrReplace(Scene scene)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) throw new InvalidOperationException("Office chair prefab is missing.");
        foreach (var previous in scene.GetRootGameObjects().Where(g => g.name == RootName))
            Undo.DestroyObjectImmediate(previous);
        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Add lobby player chair");
        SceneManager.MoveGameObjectToScene(root, scene);
        // The player's existing spawn is x=-1.84, z=8.64. Leave headset tracking and menu height intact.
        root.transform.position = new Vector3(-1.84f,.015f,8.64f);
        var chair = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        chair.name = "Padded office chair";
        chair.transform.SetParent(root.transform, false);
        chair.transform.localPosition = Vector3.zero;
        chair.transform.localRotation = Quaternion.identity;
        var renderers = chair.GetComponentsInChildren<Renderer>();
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        chair.transform.localScale *= .72f / bounds.size.x;
        bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        chair.transform.position += root.transform.position - new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
        // The model's seated direction is local -Z, toward the lobby menu and reception desk.
        // The player starts inside the seat; colliders would displace the locomotor or block hand rays.
        foreach (var collider in chair.GetComponentsInChildren<Collider>(true))
            UnityEngine.Object.DestroyImmediate(collider);
        foreach (var body in chair.GetComponentsInChildren<Rigidbody>(true))
            UnityEngine.Object.DestroyImmediate(body);
        var seat = new GameObject("Lobby seat center - facing menu");
        seat.transform.SetParent(root.transform, false);
        seat.transform.localPosition = new Vector3(0,.455f,0);
        seat.transform.localRotation = Quaternion.Euler(0,180,0);
        root.AddComponent<LobbySeatInteraction>().Configure(seat.transform);
    }
}
