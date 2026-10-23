#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Author whole-house inner security, front-door tape and a separate property-gate outer cordon.</summary>
public static class CordonFenceLayoutSetup
{
    [MenuItem("Tools/ViCSI/Scene Setup/Place House Inner And Yard Outer Cordons")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || scene.isDirty
            || scene.path != "Assets/_Project/Scenes/Scenarios/CSI_Environment.unity")
            throw new InvalidOperationException("Open the saved CSI_Environment scene in an idle Editor first.");
        var cordon = UnityEngine.Object.FindAnyObjectByType<SceneCordon>();
        var house = GameObject.Find("Vicsi_House");
        var frame = house != null ? house.transform.Find("DoorFrame") : null;
        if (cordon == null || frame == null || Mathf.Abs(frame.position.y - 1.2f) > .01f)
            throw new InvalidOperationException("Expected house entrance missing.");
        var root = cordon.transform;
        if (Quaternion.Angle(root.rotation, Quaternion.identity) > .01f || Vector3.Distance(root.lossyScale, Vector3.one) > .001f)
            throw new InvalidOperationException("Expected unit-scale cordon root.");
        var left = root.Find("EntrancePost_L"); var right = root.Find("EntrancePost_R");
        var closed = root.Find("EntranceTape_Closed");
        var sign = root.Find("Controlled Entry Point Sign");
        if (left == null || right == null || closed == null || sign == null)
            throw new InvalidOperationException("Cordon doorway parts missing.");
        const string output = "docs/evidence/house-inner-yard-outer-cordons-2026-10-20";
        Directory.CreateDirectory(output);
        if (!File.Exists(output + "/CSI_Environment-before.unity")) File.Copy(scene.path, output + "/CSI_Environment-before.unity");
        Undo.IncrementCurrentGroup(); Undo.SetCurrentGroupName("Place house inner and yard outer cordons");
        // Attach to the front door jambs, leaving the actual passage unchanged.
        var bounds = frame.GetComponent<Renderer>().bounds;
        float minX = bounds.min.x + .06f, maxX = bounds.max.x - .06f;
        float floor = frame.position.y, front = bounds.min.z - .02f;
        Undo.RecordObject(left, "Move left door anchor"); Undo.RecordObject(right, "Move right door anchor");
        left.position = new Vector3(minX, floor + .5f, front);
        right.position = new Vector3(maxX, floor + .5f, front);
        // Door jambs replace the old freestanding posts; anchors remain for existing systems.
        foreach (var anchor in new[] { left, right })
        {
            var renderer = anchor.GetComponent<MeshRenderer>(); if (renderer != null) Undo.DestroyObjectImmediate(renderer);
            var filter = anchor.GetComponent<MeshFilter>(); if (filter != null) Undo.DestroyObjectImmediate(filter);
        }
        foreach (Transform strip in closed)
        {
            Undo.RecordObject(strip, "Fit tape to front door");
            strip.SetPositionAndRotation(new Vector3((minX + maxX) * .5f, floor + (strip.name.Contains("Upper") ? .95f : .65f), front), Quaternion.identity);
            strip.localScale = new Vector3(maxX - minX, .05f, .01f);
        }
        foreach (string old in new[] { "Post_L", "Post_R", "PerimeterTape", "EntranceTape_Open" })
        { var unused = root.Find(old); if (unused != null) Undo.DestroyObjectImmediate(unused.gameObject); }
        Undo.RecordObject(sign, "Move inner cordon sign");
        sign.SetPositionAndRotation(new Vector3(bounds.max.x + .34f, floor + 1.25f, front - .025f), Quaternion.identity);
        var label = sign.GetComponentInChildren<Text>(); Undo.RecordObject(label, "Label house cordon"); label.text = "Inner Cordon\nHouse";
        // Keep the paper/pen table with the First Responder in exterior staging.
        var register = root.Find("Front yard register table anchor");
        if (register == null)
        {
            var go = new GameObject("Front yard register table anchor"); Undo.RegisterCreatedObjectUndo(go, "Preserve register staging");
            register = go.transform; register.SetParent(root, false);
            register.position = new Vector3(15.88f, .03f, 2.05f);
            register.rotation = Quaternion.LookRotation(new Vector3(-1.02f, 0, 1.4f));
        }
        var data = new SerializedObject(cordon);
        data.FindProperty("registerStationAnchor").objectReferenceValue = register;
        // Both storeys and all rooms are hot. A separate front wing follows the house's
        // stepped frontage, keeping the porch and service yard outside the interior volume.
        var main = new Bounds();
        main.SetMinMax(root.InverseTransformPoint(new Vector3(10.63f, .8f, bounds.max.z)),
            root.InverseTransformPoint(new Vector3(19.66f, 7.25f, 14.87f)));
        var wing = new Bounds();
        wing.SetMinMax(root.InverseTransformPoint(new Vector3(10.63f, .8f, 4.35f)),
            root.InverseTransformPoint(new Vector3(16.16f, 7.25f, bounds.max.z)));
        var approach = new Bounds();
        approach.SetMinMax(root.InverseTransformPoint(new Vector3(minX, .8f, front)),
            root.InverseTransformPoint(new Vector3(maxX, 7.25f, bounds.max.z)));
        data.FindProperty("protectedArea").boundsValue = main;
        data.FindProperty("controlledEntryApproach").boundsValue = approach;
        var extras = data.FindProperty("additionalProtectedAreas"); extras.arraySize = 1; extras.GetArrayElementAtIndex(0).boundsValue = wing;
        data.ApplyModifiedProperties();
        BuildOuterCordon(scene, closed.GetComponentInChildren<Renderer>().sharedMaterial, sign);
        // The alternate ground-floor exterior door stays closed and marked as secured.
        var side = root.Find("Side Door Seals"); if (side != null) Undo.DestroyObjectImmediate(side.gameObject);
        var sideRoot = new GameObject("Side Door Seals"); Undo.RegisterCreatedObjectUndo(sideRoot, "Secure alternate exterior door"); sideRoot.transform.SetParent(root, false);
        var sideBounds = house.transform.Find("DoorFrame.005").GetComponent<Renderer>().bounds;
        foreach (float height in new[] { .65f, .95f })
            Strip(sideRoot.transform, "Side door seal", new Vector3(sideBounds.max.x+.02f,floor+height,sideBounds.center.z), new Vector3(.01f,.05f,sideBounds.size.z-.12f),closed.GetComponentInChildren<Renderer>().sharedMaterial);
        PoliceCordonLetteringSetup.ApplyToScene();
        Physics.SyncTransforms(); EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save house cordons.");
        File.WriteAllText(output + "/layout-after.txt", "Front doorway: " + bounds + "\nTape: X=" + minX + ".." + maxX + " Z=" + front
            + "\nProtected local: " + main + "\nFront wing local: " + wing + "\nApproach local: " + approach);
        Selection.activeGameObject = closed.gameObject;
    }
    static void BuildOuterCordon(UnityEngine.SceneManagement.Scene scene, Material material, Transform innerSign)
    {
        var old = GameObject.Find("Outer Cordon - Yard"); if (old != null) Undo.DestroyObjectImmediate(old);
        var go = new GameObject("Outer Cordon - Yard"); Undo.RegisterCreatedObjectUndo(go, "Mark outer cordon");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
        // Existing front/side/rear fences form the outer perimeter. Mark the property-gate
        // opening while leaving a central passage for the authorized player.
        foreach (float height in new[] { .65f, .95f })
        {
            Strip(go.transform,"Outer gate left",new Vector3(15.35f,height,.15f),new Vector3(1.7f,.05f,.01f),material);
            Strip(go.transform,"Outer gate right",new Vector3(19.2f,height,.15f),new Vector3(3.2f,.05f,.01f),material);
        }
        var sign = UnityEngine.Object.Instantiate(innerSign.gameObject,go.transform); Undo.RegisterCreatedObjectUndo(sign,"Label outer cordon");
        sign.name = "Outer Cordon Sign";
        sign.transform.SetPositionAndRotation(new Vector3(17.95f,1.25f,.10f),Quaternion.identity);
        sign.GetComponentInChildren<Text>().text = "Outer Cordon\nPolice Only";
    }
    static void Strip(Transform parent,string name,Vector3 position,Vector3 scale,Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; Undo.RegisterCreatedObjectUndo(go,"Add cordon tape");
        go.transform.SetParent(parent,false); go.transform.position = position; go.transform.localScale = scale;
        Undo.DestroyObjectImmediate(go.GetComponent<Collider>()); go.GetComponent<Renderer>().sharedMaterial = material;
    }
}
#endif
