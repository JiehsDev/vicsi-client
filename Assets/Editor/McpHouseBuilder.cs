using UnityEditor;
using UnityEngine;

public static class McpHouseBuilder
{
    public static string InstantiatePrefabAtPath(string prefabPath, float x, float y, float z)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            return $"Prefab not found at path: {prefabPath}";
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.position = new Vector3(x, y, z);
        Undo.RegisterCreatedObjectUndo(instance, "Instantiate " + prefab.name);
        Selection.activeGameObject = instance;
        return $"Instantiated '{instance.name}' at ({x}, {y}, {z})";
    }
}
