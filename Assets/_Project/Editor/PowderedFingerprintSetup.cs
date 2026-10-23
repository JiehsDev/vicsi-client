using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Wire the supplied powdered-print model while retaining the existing contact zone.</summary>
public static class PowderedFingerprintSetup
{
    private const string ModelPath = "Assets/_Project/Art/Environments/MODELS/NEW/POWDERED_FINGERPRINT (1).glb";

    [MenuItem("ViCSI/Scene Setup/Connect Powdered Fingerprint")]
    public static void Connect()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            throw new InvalidOperationException("Connect the fingerprint outside Play mode after compilation.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/_Project/Scenes/Scenarios/CSI_Environment.unity")
            throw new InvalidOperationException("Open CSI_Environment first.");
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null) throw new InvalidOperationException("The powdered fingerprint model is missing.");
        foreach (var marker in UnityEngine.Object.FindObjectsByType<EvidenceFingerprintMarker>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (marker.gameObject.scene != scene) continue;
            var placeholder = marker.GetComponent<Renderer>();
            if (placeholder == null) continue;
            var existing = marker.transform.Find("PowderedFingerprintVisual");
            GameObject visual;
            if (existing != null) visual = existing.gameObject;
            else
            {
                visual = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
                Undo.RegisterCreatedObjectUndo(visual, "Connect powdered fingerprint");
                visual.name = "PowderedFingerprintVisual";
                visual.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                visual.transform.localScale = Vector3.one;
                var renderers = visual.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) throw new InvalidOperationException("Model has no renderers.");
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                // Fit the original 2 cm print footprint uniformly, preserving the source proportions.
                var size = placeholder.GetComponent<MeshFilter>().sharedMesh.bounds.size;
                var footprint = Vector3.Scale(size, marker.transform.lossyScale);
                float scale = Mathf.Min(Mathf.Abs(footprint.x) / bounds.size.x,
                    Mathf.Abs(footprint.y) / bounds.size.y);
                visual.transform.SetParent(marker.transform, false);
                var parentScale = marker.transform.lossyScale;
                visual.transform.localScale = new Vector3(scale / Mathf.Abs(parentScale.x),
                    scale / Mathf.Abs(parentScale.y), scale / Mathf.Abs(parentScale.z));
                // +Z on the supplied flat mesh points into the surface at the existing X=90 anchor.
                visual.transform.localRotation = Quaternion.Euler(0, 180, 0);
                visual.transform.localPosition = new Vector3(0, 0, -size.z * 0.5f - 0.02f);
            }
            visual.SetActive(false);
            var serialized = new SerializedObject(marker);
            serialized.FindProperty("powderedVisual").objectReferenceValue = visual;
            serialized.FindProperty("latentRenderer").objectReferenceValue = placeholder;
            serialized.ApplyModifiedProperties();
        }
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Scene save failed.");
        Debug.Log("Powdered fingerprint model connected; contact zones and procedural gates preserved.");
    }
}
