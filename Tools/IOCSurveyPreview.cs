// Editor-only diagnostic: copy into Assets/_Project/Editor while Unity is closed,
// run IOCSurveyPreview.Render in batch mode with Direct3D11, then remove the copy.
// Uses disposable scene objects and never saves the scene or changes authored UI.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class IOCSurveyPreview
{
    [Serializable] public class Result
    {
        public string id;
        public float preferredHeight, availableHeight, fontSize;
        public int lines;
        public bool fits;
    }
    [Serializable] public class Summary { public List<Result> messages = new(); }

    public static void Render()
    {
        const string output = "docs/evidence/ioc-survey/preview";
        Directory.CreateDirectory(output);
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Scenarios/CSI_Environment.unity");
        var summary = new Summary();
        foreach (string id in new[] { "ioc-assessment-guidance", "ioc-observation-survey", "ioc-observation-bottle" })
        {
            var entry = ViCSIDialogueCatalog.Current.Find(id);
            MonoBehaviour original = entry.type == DialogueEntryType.Dialogue
                ? Object.FindAnyObjectByType<STCSNotificationUI>(FindObjectsInactive.Include)
                : Object.FindAnyObjectByType<NotificationUI>(FindObjectsInactive.Include);
            var clone = Object.Instantiate(original.gameObject);
            clone.name = "Disposable IOC message preview";
            clone.transform.SetParent(null);
            clone.transform.SetPositionAndRotation(Vector3.one * 1000, Quaternion.identity);
            clone.transform.localScale = original.transform.lossyScale;
            clone.SetActive(true);
            foreach (var group in clone.GetComponentsInChildren<CanvasGroup>(true)) group.alpha = 1;
            var owner = clone.GetComponent(original.GetType());
            var label = (TMP_Text)original.GetType().GetField("label", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
            var panel = (RectTransform)clone.transform;
            label.gameObject.SetActive(true);
            label.text = GameDialogue.Text(id);
            Canvas.ForceUpdateCanvases();
            DialoguePanelLayout.Fit(panel, label, panel.sizeDelta.y, entry.type == DialogueEntryType.Tooltip);
            Canvas.ForceUpdateCanvases();
            label.ForceMeshUpdate(true, true);
            float preferred = label.GetPreferredValues(label.text,
                label.rectTransform.rect.width - label.margin.x - label.margin.z, 0).y;
            float available = label.rectTransform.rect.height - label.margin.y - label.margin.w;
            summary.messages.Add(new Result { id = id, preferredHeight = preferred, availableHeight = available,
                fontSize = label.fontSize, lines = label.textInfo.lineCount,
                fits = preferred <= available + .5f && !label.isTextOverflowing });

            var corners = new Vector3[4]; panel.GetWorldCorners(corners);
            var cameraObject = new GameObject("Disposable preview camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true; camera.aspect = 2;
            camera.orthographicSize = Mathf.Max(Vector3.Distance(corners[0], corners[1]) / 2,
                Vector3.Distance(corners[0], corners[3]) / 4) * 1.12f;
            camera.transform.SetPositionAndRotation((corners[0] + corners[2]) / 2 - Vector3.forward * 2, Quaternion.identity);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .035f, .045f);
            var target = RenderTexture.GetTemporary(1400, 700, 24);
            var previous = RenderTexture.active;
            var texture = new Texture2D(1400, 700, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1400, 700), 0, 0); texture.Apply();
                File.WriteAllBytes(Path.Combine(output, id + ".png"), texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(texture);
                Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(clone);
            }
        }
        File.WriteAllText(Path.Combine(output, "layout.json"), JsonUtility.ToJson(summary, true));
        Debug.Log("IOC survey native panel previews saved; no scene or asset was saved.");
    }
}
#endif
