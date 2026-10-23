using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Replace the collection crate's placeholder visual without changing its deposit zone.</summary>
public static class EvidenceTransportBinSetup
{
    private const string ModelPath = "Assets/_Project/Art/Environments/MODELS/NEW/EVIDENCE_TRANSPORT_BIN.glb";

    [MenuItem("ViCSI/Scene Setup/Connect Evidence Transport Bin")]
    public static void Connect()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            throw new InvalidOperationException("Connect the bin outside Play mode after compilation.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/_Project/Scenes/Scenarios/CSI_Environment.unity" || scene.isDirty)
            throw new InvalidOperationException("Open the saved CSI_Environment scene first.");
        var root = GameObject.Find("EvidenceHoldingCrate");
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (root == null || model == null || root.GetComponent<EvidenceBoxDepositZone>() == null)
            throw new InvalidOperationException("The collection bin or transport model is missing.");
        var existing = root.transform.Find("TransportBinVisual");
        if (existing != null)
            throw new InvalidOperationException("The transport bin is already connected.");

        var placeholder = root.GetComponent<MeshRenderer>();
        var target = placeholder.bounds;
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
        visual.name = "TransportBinVisual";
        visual.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        visual.transform.localScale = Vector3.one;
        var renderers = visual.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            UnityEngine.Object.DestroyImmediate(visual);
            throw new InvalidOperationException("The transport model has no renderers.");
        }
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        // Preserve the authored collection footprint and model proportions. The existing
        // root's nonuniform cube scale is compensated on this visual child only.
        float scale = Mathf.Min(target.size.x / bounds.size.x,
            target.size.y / bounds.size.y, target.size.z / bounds.size.z);
        visual.transform.localScale = Vector3.one * scale;
        visual.transform.position = target.center - bounds.center * scale;
        visual.transform.SetParent(root.transform, true);
        placeholder.enabled = false;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = root;
        Debug.Log("Evidence transport bin connected; deposit root, trigger and custody settings preserved.");
    }
}
