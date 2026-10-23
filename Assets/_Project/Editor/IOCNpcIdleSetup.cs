using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Author the supplied IOC idle as ordinary animation/controller assets.</summary>
public static class IOCNpcIdleSetup
{
    private const string Folder = "Assets/_Project/Art/Environments/MODELS/NPC/IOC/";

    [MenuItem("ViCSI/Scene Setup/Connect IOC Idle")]
    public static void ConnectIdle()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            throw new InvalidOperationException("Connect the IOC idle outside Play mode after compilation.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/_Project/Scenes/Scenarios/CSI_Environment.unity" || scene.isDirty)
            throw new InvalidOperationException("Open the saved CSI_Environment scene before connecting the idle.");
        var root = GameObject.Find("NPC_IOC");
        var visual = root != null ? root.transform.Find("FacingPivot/Visual/IOC_NPC") : null;
        var animator = visual != null ? visual.GetComponent<Animator>() : null;
        var source = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "IOC_NPC_Unity.glb");
        if (animator == null || source == null)
            throw new InvalidOperationException("The linked IOC model or supplied animation is missing.");

        // The export's static pose and animation use different hip heights.
        // Align the copied hip curve to the static feet without moving the NPC root.
        float heightOffset;
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "IOC_NPC_Unity.glb"), preview);
            model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            model.transform.localScale = Vector3.one;
            var renderer = model.GetComponentInChildren<SkinnedMeshRenderer>();
            float staticFeet = FeetHeight(renderer);
            source.SampleAnimation(model, 0f);
            heightOffset = FeetHeight(renderer) - staticFeet;
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }

        var copy = UnityEngine.Object.Instantiate(source);
        copy.name = "IOC_Idle";
        bool aligned = false;
        foreach (var binding in AnimationUtility.GetCurveBindings(copy))
        {
            if (binding.path != "FBX_Root/mixamorig_Hips" || binding.propertyName != "m_LocalPosition.y")
                continue;
            var curve = AnimationUtility.GetEditorCurve(copy, binding);
            var keys = curve.keys;
            for (int i = 0; i < keys.Length; i++) keys[i].value -= heightOffset;
            curve.keys = keys;
            AnimationUtility.SetEditorCurve(copy, binding, curve);
            aligned = true;
        }
        if (!aligned)
        {
            UnityEngine.Object.DestroyImmediate(copy);
            throw new InvalidOperationException("The supplied idle has no expected hip-height curve.");
        }
        var settings = AnimationUtility.GetAnimationClipSettings(copy);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(copy, settings);
        var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "IOC_Idle.anim");
        if (idle == null) { idle = copy; AssetDatabase.CreateAsset(idle, Folder + "IOC_Idle.anim"); }
        else { EditorUtility.CopySerialized(copy, idle); UnityEngine.Object.DestroyImmediate(copy); }
        EditorUtility.SetDirty(idle);

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder + "IOC_Idle.controller");
        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(Folder + "IOC_Idle.controller");
        var machine = controller.layers[0].stateMachine;
        var state = machine.defaultState != null ? machine.defaultState : machine.AddState("Idle");
        state.motion = idle;
        state.speed = 1f;
        machine.defaultState = state;
        EditorUtility.SetDirty(state);
        EditorUtility.SetDirty(machine);
        EditorUtility.SetDirty(controller);
        Undo.RecordObject(animator, "Connect IOC idle");
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = visual.gameObject;
        Debug.Log("IOC idle connected; static-pose hip alignment = " + heightOffset + " model units.");
    }

    private static float FeetHeight(SkinnedMeshRenderer renderer)
    {
        var mesh = new Mesh();
        try { renderer.BakeMesh(mesh); return mesh.bounds.min.y; }
        finally { UnityEngine.Object.DestroyImmediate(mesh); }
    }
}
