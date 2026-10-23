#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class LobbyVisibilityProbe
{
    static LobbyVisibilityProbe() { EditorApplication.update += Poll; }
    static double next;
    static void Poll()
    {
        if (EditorApplication.timeSinceStartup < next || EditorApplication.isCompiling) return;
        next = EditorApplication.timeSinceStartup + 1;
        const string request = "Temp/lobby-visibility-request.txt";
        if (!File.Exists(request)) return;
        var command = File.ReadAllText(request).Trim();
        File.Delete(request);
        try
        {
            if (command == "stop") EditorApplication.isPlaying = false;
            else if (command == "play") EditorApplication.isPlaying = true;
            else if (command == "play-lobby")
            {
                const string lobby = "Assets/_Project/Scenes/Main.unity";
                if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before starting from the lobby.");
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(lobby);
                UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(lobby);
                EditorApplication.isPlaying = true;
            }
            else if (command == "check-height-restore")
            {
                var placement = UnityEngine.Object.FindAnyObjectByType<LobbyCanvasPlacement>();
                var rig = UnityEngine.Object.FindAnyObjectByType<OVRCameraRig>();
                var before = rig.trackingSpace.localPosition;
                placement.enabled = false;
                var restored = rig.trackingSpace.localPosition;
                placement.enabled = true;
                File.WriteAllText("Temp/lobby-height-restore.txt", $"preview={before:F4} restored={restored:F4} pass={Mathf.Abs(before.y-restored.y-1.6f)<.001f}");
            }
            else if (command == "face-wall")
            {
                var locomotor = UnityEngine.Object.FindAnyObjectByType<Oculus.Interaction.Locomotion.FirstPersonLocomotor>();
                locomotor.HandleLocomotionEvent(new Oculus.Interaction.Locomotion.LocomotionEvent(0,
                    new Pose(Vector3.zero, Quaternion.identity),
                    Oculus.Interaction.Locomotion.LocomotionEvent.TranslationType.None,
                    Oculus.Interaction.Locomotion.LocomotionEvent.RotationType.Absolute));
            }
            else if (command == "restart-placement")
            {
                var placement = UnityEngine.Object.FindAnyObjectByType<LobbyCanvasPlacement>();
                placement.enabled = false;
                placement.enabled = true;
            }
            else if (command == "raise-authored-canvas")
            {
                if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before saving the canvas height.");
                var placement = UnityEngine.Object.FindAnyObjectByType<LobbyCanvasPlacement>();
                var position = placement.transform.position;
                position.y = LobbyCanvasPlacement.DefaultCenterHeight;
                ((RectTransform)placement.transform).anchoredPosition3D = position;
                var serialized = new SerializedObject(placement);
                serialized.FindProperty("centerHeight").floatValue = LobbyCanvasPlacement.DefaultCenterHeight;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var scene = placement.gameObject.scene;
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            }
            else if (command == "low-eye")
            {
                var placement = UnityEngine.Object.FindAnyObjectByType<LobbyCanvasPlacement>();
                var rig = UnityEngine.Object.FindAnyObjectByType<OVRCameraRig>();
                var position = rig.trackingSpace.localPosition;
                position.y = 0;
                rig.trackingSpace.localPosition = position;
                typeof(LobbyCanvasPlacement).GetField("placedCamera", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(placement, null);
            }
            else Snapshot(command);
        }
        catch (Exception ex) { File.WriteAllText("Temp/lobby-visibility-error.txt", ex.ToString()); }
    }
    static void Snapshot(string label)
    {
        var s = new StringBuilder();
        s.AppendLine($"playing={EditorApplication.isPlaying} paused={EditorApplication.isPaused} frame={Time.frameCount} compiling={EditorApplication.isCompiling} compilationFailed={EditorUtility.scriptCompilationFailed}");
        s.AppendLine($"hmd={OVRManager.isHmdPresent} manager={OVRManager.instance}");
        if (EditorApplication.isPlaying)
            s.AppendLine($"headPositionTracked={OVRPlugin.GetNodePositionTracked(OVRPlugin.Node.EyeCenter)}");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        s.AppendLine($"scene={scene.path} dirty={scene.isDirty}");
        foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            s.AppendLine($"camera={c.name} enabled={c.enabled} active={c.gameObject.activeInHierarchy} pos={c.transform.position:F4} rot={c.transform.eulerAngles:F2} target={c.targetTexture} depth={c.depth} mask={c.cullingMask}");
        var placement = UnityEngine.Object.FindAnyObjectByType<LobbyCanvasPlacement>();
        if (placement != null) s.AppendLine("startupFacingApplied=" + typeof(LobbyCanvasPlacement).GetField("startupFacingApplied", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(placement));
        var rigState = UnityEngine.Object.FindAnyObjectByType<OVRCameraRig>();
        if (rigState != null) s.AppendLine($"playerRoot={rigState.transform.position:F4} yaw={rigState.transform.eulerAngles.y:F2} trackingYaw={rigState.trackingSpace.localEulerAngles.y:F2}");
        var cam = Camera.main;
        s.AppendLine("main=" + (cam == null ? "null" : cam.name));
        foreach (var c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            s.AppendLine($"canvas={c.name} enabled={c.enabled} active={c.gameObject.activeInHierarchy} pos={c.transform.position:F4} rot={c.transform.eulerAngles:F2} scale={c.transform.lossyScale:F4} mode={c.renderMode}");
            var corners = new Vector3[4]; ((RectTransform)c.transform).GetWorldCorners(corners);
            s.AppendLine($"canvasBottom={corners.Min(p=>p.y):F4} canvasTop={corners.Max(p=>p.y):F4}");
            if (cam != null)
            {
                s.AppendLine($"viewport={cam.WorldToViewportPoint(c.transform.position):F4} distance={Vector3.Distance(cam.transform.position,c.transform.position)}");
                foreach (var hit in Physics.RaycastAll(cam.transform.position, c.transform.position-cam.transform.position, Vector3.Distance(cam.transform.position,c.transform.position)).OrderBy(h=>h.distance))
                    s.AppendLine($"occluder={hit.collider.name} distance={hit.distance} trigger={hit.collider.isTrigger}");
            }
        }
        foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t=>t.name=="MainMenuPanel" || t.name=="TrackingSpace"))
            s.AppendLine($"object={t.name} active={t.gameObject.activeInHierarchy} pos={t.position:F4}");
        File.WriteAllText("Temp/lobby-visibility-" + label + ".txt", s.ToString());
        if (EditorApplication.isPlaying && cam != null)
        {
            var rt = RenderTexture.GetTemporary(1000,1000,24);
            var old = cam.targetTexture; var active = RenderTexture.active;
            try {
                cam.targetTexture=rt; cam.Render(); RenderTexture.active=rt;
                var tex=new Texture2D(1000,1000,TextureFormat.RGB24,false);
                tex.ReadPixels(new Rect(0,0,1000,1000),0,0); tex.Apply();
                File.WriteAllBytes("Temp/lobby-visibility-"+label+".png",tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
            } finally {cam.targetTexture=old; RenderTexture.active=active; RenderTexture.ReleaseTemporary(rt);}
        }
    }
}
#endif





