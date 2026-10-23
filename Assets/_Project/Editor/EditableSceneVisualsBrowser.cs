#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Locate authored props and expose normally hidden panels for visual editing.</summary>
public sealed class EditableSceneVisualsBrowser : EditorWindow
{
    Vector2 scroll;
    [MenuItem("Tools/ViCSI/Editable Scene Objects")]
    static void Open()=>GetWindow<EditableSceneVisualsBrowser>("Editable Scene Objects");
    void OnGUI()
    {
        EditorGUILayout.HelpBox("Edit the real scene objects below. Scripts reuse their transforms, materials, fonts and colours. Live data, visibility and camera-follow poses still update during play.",MessageType.Info);
        using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if(GUILayout.Button("Materialize missing scene objects"))EditableSceneVisualsSetup.Materialize();
        }
        scroll=EditorGUILayout.BeginScrollView(scroll);
        foreach(var storage in UnityEngine.Object.FindObjectsByType<EditableSceneVisuals>(FindObjectsInactive.Include,FindObjectsSortMode.None).OrderBy(s=>s.name))
        {
            EditorGUILayout.LabelField(storage.name,EditorStyles.boldLabel);
            foreach(var group in storage.Groups)
            {
                using(new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(group.owner.GetType().Name+" / "+group.builder+" ("+group.objects.Count+")");
                    if(GUILayout.Button("Select",GUILayout.Width(60)))
                    {
                        Selection.objects=group.objects.Where(g=>g!=null).Cast<Object>().ToArray();
                        SceneView.lastActiveSceneView?.FrameSelected();
                    }
                    using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                        if(GUILayout.Button("Show UI",GUILayout.Width(70)))
                        {
                            if(group.builder=="BuildSettings"||group.builder.StartsWith("Confirmation-"))
                                foreach(var other in storage.Groups.Where(g=>g.owner==group.owner&&(g.builder=="BuildSettings"||g.builder.StartsWith("Confirmation-"))))
                                    foreach(var item in other.objects.Where(g=>g!=null)){Undo.RecordObject(item,"Switch UI authoring preview");item.SetActive(false);}
                            foreach(var go in group.objects.Where(g=>g!=null))
                            {
                                for(var t=go.transform;t!=null&&t!=storage.transform;t=t.parent)
                                {
                                    var canvas=t.GetComponent<Canvas>();if(canvas!=null){Undo.RecordObject(canvas,"Preview authored UI");canvas.enabled=true;}
                                    Undo.RecordObject(t.gameObject,"Preview authored UI");t.gameObject.SetActive(true);
                                }
                            }
                        }
                }
            }
        }
        EditorGUILayout.EndScrollView();
        if(GUILayout.Button("Edit paper swab package prefab"))AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/EditablePaperSwabPackage.prefab"));
    }
}
#endif
