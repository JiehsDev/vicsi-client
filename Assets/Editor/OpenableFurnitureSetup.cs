// Assets/Editor/OpenableFurnitureSetup.cs
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// One-click setup for OpenableFurniture: scans every Transform in the open
/// scene (including inactive) for names containing "Drawer" or "Door", adds an
/// OpenableFurniture component to any that don't already have one, and sets
/// its openMode to Rotate for doors / Slide for drawers. Everything else
/// (interactionRadius, slideLocalOffset, openLocalEulerOffset, speeds) is left
/// at OpenableFurniture's defaults - test each piece in Play Mode and tune
/// those in the Inspector if the slide direction/distance or hinge angle
/// doesn't match that prop.
/// </summary>
public static class OpenableFurnitureSetup
{
    [MenuItem("Tools/VICSI/Attach Openable Furniture To Scene")]
    public static void AttachToScene()
    {
        var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int added = 0;

        foreach (var t in all)
        {
            bool isDrawer = t.name.Contains("Drawer");
            bool isDoor = t.name.Contains("Door");
            if (!isDrawer && !isDoor)
            {
                continue;
            }

            var go = t.gameObject;
            if (go.GetComponent<OpenableFurniture>() != null)
            {
                continue;
            }

            var comp = Undo.AddComponent<OpenableFurniture>(go);
            var serialized = new SerializedObject(comp);
            serialized.FindProperty("openMode").enumValueIndex = isDoor ? 1 : 0; // Slide = 0, Rotate = 1
            serialized.ApplyModifiedProperties();
            added++;
        }

        if (added > 0)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        Debug.Log($"[OpenableFurnitureSetup] Attached OpenableFurniture to {added} object(s).");
    }
}
