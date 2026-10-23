#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Restore a missing SDK comfort-mask child from its existing source prefab.</summary>
public static class ComfortMaskReferenceRepair
{
    [MenuItem("Tools/ViCSI/Scene Setup/Repair Missing Comfort Mask Reference")]
    public static void Repair()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.InvalidOperationException("Use Edit mode.");
        foreach(var effect in Object.FindObjectsByType<Oculus.Interaction.TunnelingEffect>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            var data=new SerializedObject(effect);var field=data.FindProperty("_meshFilter");
            if(field.objectReferenceValue!=null)continue;
            var source=PrefabUtility.GetCorrespondingObjectFromOriginalSource(effect);
            if(source==null)throw new System.InvalidOperationException("Missing source comfort prefab.");
            var sourceMesh=(MeshFilter)new SerializedObject(source).FindProperty("_meshFilter").objectReferenceValue;
            var existing=effect.GetComponentInChildren<MeshFilter>(true);
            if(existing==null)
            {
                var child=Object.Instantiate(sourceMesh.gameObject,effect.transform,false);child.name=sourceMesh.name;
                Undo.RegisterCreatedObjectUndo(child,"Restore missing comfort mask");existing=child.GetComponent<MeshFilter>();
            }
            Undo.RecordObject(effect,"Restore comfort mask reference");field.objectReferenceValue=existing;data.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(effect);
        }
    }
}
#endif
