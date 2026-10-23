#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class ProcedureGuidanceSetup
{
    const string Output = "docs/evidence/procedure-guidance-2026-10-22";
    static ProcedureGuidanceSetup() { EditorApplication.update += Poll; }
    static void Poll()
    {
        const string flag = "Temp/setup-procedure-guidance.flag";
        if (!File.Exists(flag) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(flag);
        try { Apply(); } catch (Exception e) { Directory.CreateDirectory(Output); File.WriteAllText(Output + "/setup-error.txt", e.ToString()); Debug.LogException(e); }
    }
    [MenuItem("Tools/ViCSI/Scene Setup/Install Procedure Destination Guide")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/_Project/Scenes/Scenarios/CSI_Environment.unity" || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Open CSI_Environment outside Play mode.");
        var guide = UnityEngine.Object.FindAnyObjectByType<ProcedureDestinationGuide>();
        if (guide == null)
        {
            var root = new GameObject("Procedure destination guidance"); Undo.RegisterCreatedObjectUndo(root, "Author procedure guidance");
            guide = Undo.AddComponent<ProcedureDestinationGuide>(root);
            var canvasObject = new GameObject("Required destination waypoint", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(root.transform, false); canvasObject.transform.localScale = Vector3.one * .0015f;
            ((RectTransform)canvasObject.transform).sizeDelta = new Vector2(460, 230);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 90; canvas.enabled = false;
            const string materialPath = "Assets/_Project/Art/Materials/ProcedureWaypoint.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null) { material = new Material(Resources.Load<Shader>("PhotographyGuideOverlay")); AssetDatabase.CreateAsset(material, materialPath); }
            var glyph = new GameObject("Destination pin or edge arrow", typeof(RectTransform), typeof(PhotographyGuideGraphic)); glyph.transform.SetParent(canvasObject.transform, false);
            var rect = (RectTransform)glyph.transform; rect.sizeDelta = new Vector2(64, 64); rect.anchoredPosition = new Vector2(0, 74);
            var graphic = glyph.GetComponent<PhotographyGuideGraphic>(); graphic.material = material; graphic.raycastTarget = false;
            var background = new GameObject("Instruction background", typeof(RectTransform), typeof(Image)); background.transform.SetParent(canvasObject.transform, false);
            rect = (RectTransform)background.transform; rect.sizeDelta = new Vector2(450, 150); rect.anchoredPosition = new Vector2(0, -40);
            var image = background.GetComponent<Image>(); image.material = material; image.color = new Color(.025f, .05f, .075f, .9f); image.raycastTarget = false;
            var label = new GameObject("Required action", typeof(RectTransform), typeof(Text)); label.transform.SetParent(canvasObject.transform, false);
            rect = (RectTransform)label.transform; rect.sizeDelta = new Vector2(430, 140); rect.anchoredPosition = new Vector2(0, -40);
            var text = label.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = 24;
            text.color = Color.white; text.alignment = TextAnchor.MiddleCenter; text.material = material; text.raycastTarget = false; text.supportRichText = false;
            var data = new SerializedObject(guide);
            data.FindProperty("waypointCanvas").objectReferenceValue = canvas;
            data.FindProperty("glyph").objectReferenceValue = glyph.transform;
            data.FindProperty("icon").objectReferenceValue = graphic;
            data.FindProperty("instruction").objectReferenceValue = text;
            data.ApplyModifiedProperties();
        }
        var comparison=UnityEngine.Object.FindAnyObjectByType<FingerprintComparisonStation>();
        if(comparison!=null)
        {
            var labels=comparison.GetComponentsInChildren<TMPro.TMP_Text>(true);
            Undo.RecordObjects(labels,"Update held-card comparison instructions");comparison.RefreshCaseworkLabels();
            foreach(var label in labels)EditorUtility.SetDirty(label);
        }
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Directory.CreateDirectory(Output); File.WriteAllText(Output + "/setup.txt", "Saved editable procedure waypoint canvas, arrow, instruction label and material. Existing specialized guides retained.\n");
    }
}
#endif
