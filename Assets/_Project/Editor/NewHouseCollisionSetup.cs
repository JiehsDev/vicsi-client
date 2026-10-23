using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using PlayerController = Oculus.Interaction.Locomotion.CharacterController;

/// <summary>Scene-local static collisions; retains openings and the authored stair treads.</summary>
public static class NewHouseCollisionSetup
{
    const string Evidence = "docs/evidence/new-house-collisions";
    static Transform House => SceneManager.GetActiveScene().GetRootGameObjects().Single(g => g.name == "Vicsi_House").transform;

    [MenuItem("ViCSI/Scene Setup/Add New House Collisions")]
    public static void Apply()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/_Project/Scenes/Scenarios/CSI_Environment.unity")
            throw new InvalidOperationException("Open CSI_Environment outside Play mode.");
        Directory.CreateDirectory(Evidence);
        if (!File.Exists("Temp/new-house-before-collisions.unity") && !EditorSceneManager.SaveScene(scene, "Temp/new-house-before-collisions.unity", true))
            throw new IOException("Could not snapshot the live scene.");
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Add new house collisions");
        var house = House;
        var log = new StringBuilder();
        foreach (var filter in house.GetComponentsInChildren<MeshFilter>(true))
        {
            var t = filter.transform;
            // Small decorations, curtains and light fittings do not obstruct the player.
            if (Under(t, house, "HomeDecor") || Under(t, house, "Light") || Under(t, house, "Curtain")) continue;
            if (t.name.StartsWith("Handle") && t.name != "Handle_Back.016") continue;
            if (filter.sharedMesh == null) continue;
            // Cabinet/sofa colliders already cover their attached drawers and fittings.
            bool covered = false;
            for (var p = t; p != null && p != house; p = p.parent)
                if (p.GetComponents<Collider>().Any(c => c.enabled && !c.isTrigger)) { covered = true; break; }
            if (covered) continue;
            if (t.GetComponentInParent<Rigidbody>() != null)
                throw new InvalidOperationException("Static house collision has a Rigidbody: " + t.name);
            var collider = Undo.AddComponent<MeshCollider>(t.gameObject);
            collider.sharedMesh = filter.sharedMesh;
            collider.convex = false;
            collider.isTrigger = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
            log.AppendLine(t.name + " mesh=" + filter.sharedMesh.name + " layer=" + t.gameObject.layer);
        }
        AddStairRamps();
        EnsureStairHeadroom();
        Undo.CollapseUndoOperations(group);
        Physics.SyncTransforms();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save house collisions.");
        File.WriteAllText(Evidence + "/added-colliders.txt", log.ToString());
    }

    public static void EnsureStairHeadroom()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Requires Edit mode.");
        // Extend the existing stairwell by 0.633 m so a 2 m player can descend beneath its lip.
        // Cut the visible floor and ceiling together; collision remains consistent with the opening.
        foreach (string name in new[] { "Plane.012", "InteriorCeilingLining" })
        {
            var t = House.Find(name);
            var filter = t.GetComponent<MeshFilter>();
            if (filter.sharedMesh.name.StartsWith("StairClearance_")) continue;
            var source = filter.sharedMesh;
            var positions = source.vertices; var normals = source.normals; var uv = source.uv;
            var output = new List<Vertex>();
            var submeshes = new List<int[]>();
            for (int sub = 0; sub < source.subMeshCount; sub++)
            {
                var indices = new List<int>(); var triangles = source.GetTriangles(sub);
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    var inside = new List<Vertex>();
                    for (int j = 0; j < 3; j++)
                    {
                        int index = triangles[i+j];
                        inside.Add(new Vertex { p = t.TransformPoint(positions[index]), n = normals.Length == positions.Length ? normals[index] : Vector3.up, uv = uv.Length == positions.Length ? uv[index] : Vector2.zero });
                    }
                    // Remove only the rectangular extension of the existing stair opening.
                    foreach (var plane in new[] { new Vector4(1,0,0,-10.6f),new Vector4(-1,0,0,14.55f),new Vector4(0,0,1,-9.402f),new Vector4(0,0,-1,10.32f) })
                    {
                        var outside = Clip(inside, -plane);
                        Append(outside);
                        inside = Clip(inside, plane);
                        if (inside.Count == 0) break;
                    }
                }
                submeshes.Add(indices.ToArray());
                void Append(List<Vertex> polygon)
                {
                    if (polygon.Count < 3) return;
                    int start = output.Count; output.AddRange(polygon);
                    for (int k = 1; k < polygon.Count - 1; k++) { indices.Add(start); indices.Add(start+k); indices.Add(start+k+1); }
                }
            }
            var mesh = new Mesh { name = "StairClearance_" + name, vertices = output.Select(v => t.InverseTransformPoint(v.p)).ToArray(), normals = output.Select(v => v.n).ToArray(), uv = output.Select(v => v.uv).ToArray(), subMeshCount = source.subMeshCount };
            for (int i = 0; i < submeshes.Count; i++) mesh.SetTriangles(submeshes[i], i);
            mesh.RecalculateBounds();
            const string folder = "Assets/_Project/Art/Environments/MODELS/House/Collision";
            Directory.CreateDirectory(folder);
            AssetDatabase.CreateAsset(mesh, folder + "/" + mesh.name + ".asset");
            Undo.RecordObject(filter, "Clear stairwell headroom"); filter.sharedMesh = mesh;
            var collider = t.GetComponent<MeshCollider>(); Undo.RecordObject(collider, "Clear stair collision headroom"); collider.sharedMesh = mesh;
            PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
            PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
        }
        AssetDatabase.SaveAssets(); Physics.SyncTransforms();
    }
    struct Vertex { public Vector3 p, n; public Vector2 uv; }
    static List<Vertex> Clip(List<Vertex> input, Vector4 plane)
    {
        var result = new List<Vertex>();
        if (input.Count == 0) return result;
        var previous = input[input.Count - 1]; float a = Distance(previous.p);
        foreach (var current in input)
        {
            float b = Distance(current.p);
            if ((a >= 0) != (b >= 0))
            {
                float f = a / (a - b);
                result.Add(new Vertex { p = Vector3.Lerp(previous.p,current.p,f), n = Vector3.Lerp(previous.n,current.n,f).normalized, uv = Vector2.Lerp(previous.uv,current.uv,f) });
            }
            if (b >= 0) result.Add(current);
            previous = current; a = b;
        }
        return result;
        float Distance(Vector3 p) => Vector3.Dot(new Vector3(plane.x,plane.y,plane.z),p)+plane.w;
    }

    public static void AddStairRamps()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Requires Edit mode.");
        var house = House;
        if (house.Find("StairCollisionRamps") != null) return;
        const string folder = "Assets/_Project/Art/Environments/MODELS/House/Collision";
        Directory.CreateDirectory(folder);
        var root = new GameObject("StairCollisionRamps");
        Undo.RegisterCreatedObjectUndo(root, "Add smooth stair collisions");
        root.transform.SetParent(house, false);
        // Top surfaces follow the stair nosings. Thin undersides retain the space beneath the stairs.
        Ramp("Lower flight", new Vector3(15.027f, 1.206f, 9.402f), new Vector3(12.027f, 2.971f, 9.402f), .9f);
        Ramp("Return flight", new Vector3(11.691f, 2.971f, 10.502f), new Vector3(13.791f, 4.206f, 10.502f), .9f);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        void Ramp(string name, Vector3 low, Vector3 high, float width)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Add stair ramp");
            go.transform.SetParent(root.transform, false);
            Vector3 side = Vector3.forward * width, down = Vector3.down * .04f;
            var vertices = new[] { low, high, high + side, low + side, low + down, high + down, high + side + down, low + side + down };
            for (int i = 0; i < vertices.Length; i++) vertices[i] = go.transform.InverseTransformPoint(vertices[i]);
            int[] triangles = { 0,1,2, 0,2,3, 4,6,5, 4,7,6, 0,4,5, 0,5,1, 3,2,6, 3,6,7, 0,3,7, 0,7,4, 1,5,6, 1,6,2 };
            // The lower flight runs in the opposite X direction.
            if (Vector3.Cross(high - low, side).y < 0) for (int i = 0; i < triangles.Length; i += 3) { int swap = triangles[i+1]; triangles[i+1] = triangles[i+2]; triangles[i+2] = swap; }
            var mesh = new Mesh { name = name + " collision", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            string path = folder + "/" + name.Replace(" ", "_") + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) AssetDatabase.CreateAsset(mesh, path);
            else { UnityEngine.Object.DestroyImmediate(mesh); mesh = existing; }
            var collider = Undo.AddComponent<MeshCollider>(go);
            collider.sharedMesh = mesh;
        }
    }

    static bool Under(Transform t, Transform root, string name)
    {
        for (var p = t; p != null && p != root; p = p.parent) if (p.name == name) return true;
        return false;
    }

    [MenuItem("ViCSI/Scene Setup/Validate New House Collisions")]
    public static void Validate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Requires Edit mode.");
        Directory.CreateDirectory(Evidence);
        var log = new StringBuilder();
        int failures = 0;
        Action<bool, string> check = (ok, label) => { log.AppendLine((ok ? "PASS " : "FAIL ") + label); if (!ok) failures++; };
        var source = UnityEngine.Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Single();
        var house = House;
        foreach (var name in new[] { "Plane", "Plane.001", "Plane.002", "Plane.005", "Plane.006", "Plane.007", "Plane.008", "Plane.009", "Plane.010", "Plane.011", "Plane.012", "Plane.013", "Plane.014", "Plane.015", "Plane.017", "Stairs.003", "walls", "walls.001", "walls.003" })
        {
            var c = house.Find(name).GetComponent<MeshCollider>();
            check(c != null && c.enabled && !c.isTrigger && !c.convex && (source.LayerMask.value & (1 << c.gameObject.layer)) != 0, name + " solid and visible to player collision mask");
        }
        // Use the actual Meta controller algorithm and scene collision mask, without moving the live player.
        var probe = new GameObject("House collision validation capsule") { hideFlags = HideFlags.HideAndDontSave, layer = 2 };
        try
        {
            var capsule = probe.AddComponent<CapsuleCollider>();
            capsule.radius = source.Radius;
            var cc = probe.AddComponent<PlayerController>();
            cc.InjectCapsule(capsule);
            cc.LayerMask = source.LayerMask.value & ~(1 << probe.layer);
            cc.SkinWidth = source.SkinWidth;
            cc.MaxStep = source.MaxStep;
            cc.MaxSlopeAngle = source.MaxSlopeAngle;
            cc.MaxReboundSteps = source.MaxReboundSteps;
            foreach (float height in new[] { 1.4f, 1.7f, 2.0f })
            {
                capsule.height = height;
                Place(cc, new Vector3(14.99f, 1.2f, 9.85f));
                Walk(cc, new Vector2(11.35f, 9.85f));
                check(NearFeet(cc, new Vector3(11.35f, 2.965f, 9.85f)), "height " + height + " ascend lower flight " + Feet(cc).ToString("F3"));
                Walk(cc, new Vector2(11.35f, 10.95f));
                Walk(cc, new Vector2(14.35f, 10.95f));
                check(NearFeet(cc, new Vector3(14.35f, 4.205f, 10.95f)), "height " + height + " ascend return flight and reach upper landing " + Feet(cc).ToString("F3"));
                Walk(cc, new Vector2(11.35f, 10.95f));
                Walk(cc, new Vector2(11.35f, 9.85f));
                Walk(cc, new Vector2(14.99f, 9.85f));
                check(NearFeet(cc, new Vector3(14.99f, 1.2f, 9.85f)), "height " + height + " descend both flights " + Feet(cc).ToString("F3"));
                Place(cc, new Vector3(16.9f, 0f, 2.6f));
                Walk(cc, new Vector2(16.9f, 5.25f));
                check(NearFeet(cc, new Vector3(16.9f, 1.2f, 5.25f)), "height " + height + " climb exterior entrance steps " + Feet(cc).ToString("F3"));
                Walk(cc, new Vector2(16.9f, 2.6f));
                check(NearFeet(cc, new Vector3(16.9f, 0f, 2.6f)), "height " + height + " descend exterior steps " + Feet(cc).ToString("F3"));
            }
            capsule.height = 1.7f;
            foreach (var feet in new[] {new Vector3(13,1.2f,7),new Vector3(18,1.2f,9.5f),new Vector3(17,1.2f,13),new Vector3(13,4.205f,7),new Vector3(18,4.205f,10),new Vector3(17,4.205f,13)})
            {
                Place(cc, feet);
                for (int i = 0; i < 180; i++) { cc.Move(Vector3.down * .02f); Physics.SyncTransforms(); }
                check(Mathf.Abs(Feet(cc).y - feet.y) < .04f, "floor supports player at " + feet.ToString("F2"));
            }
            Place(cc, new Vector3(18,1.2f,9.5f));
            Walk(cc, new Vector2(20.5f,9.5f));
            check(Feet(cc).x < 19.5f && Feet(cc).x > 19, "east wall blocks movement " + Feet(cc).ToString("F3"));
            Place(cc, new Vector3(11.5f,1.2f,7));
            Walk(cc, new Vector2(9.5f,7));
            check(Feet(cc).x > 10.8f && Feet(cc).x < 11.3f, "west wall blocks movement " + Feet(cc).ToString("F3"));
        }
        finally { UnityEngine.Object.DestroyImmediate(probe); Physics.SyncTransforms(); }
        log.AppendLine("failures=" + failures + " compilationFailed=" + EditorUtility.scriptCompilationFailed + " playing=" + EditorApplication.isPlaying + " dirty=" + SceneManager.GetActiveScene().isDirty);
        File.WriteAllText(Evidence + "/validation.txt", log.ToString());
    }
    static Vector3 Feet(PlayerController cc) => cc.Pose.position - Vector3.up * cc.Height * .5f;
    static void Place(PlayerController cc, Vector3 feet)
    {
        cc.SetPosition(feet + Vector3.up * (cc.Height * .5f + .01f));
        Physics.SyncTransforms();
        cc.TryGround(.05f);
    }
    static bool NearFeet(PlayerController cc, Vector3 expected) => Vector3.Distance(Feet(cc), expected) < .09f;
    static void Walk(PlayerController cc, Vector2 target)
    {
        // 90 Hz movement with downward contact/gravity; no teleporting between flight endpoints.
        for (int i = 0; i < 1000; i++)
        {
            var feet = Feet(cc);
            var delta = new Vector3(target.x - feet.x, 0, target.y - feet.z);
            if (delta.magnitude < .012f) { for (int j = 0; j < 40; j++) { cc.Move(Vector3.down * .02f); Physics.SyncTransforms(); } return; }
            cc.Move(Vector3.ClampMagnitude(delta, 1.5f / 90f) + Vector3.down * .02f);
            Physics.SyncTransforms();
        }
    }
}
