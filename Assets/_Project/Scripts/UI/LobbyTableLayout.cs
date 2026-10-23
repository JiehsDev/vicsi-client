using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Presentation-only, reusable label/value rows. Owns no student or session state.</summary>
public static class LobbyTableLayout
{
    public static readonly Color LabelColor = new Color(.66f, .67f, .68f, 1);
    public static readonly Color ValueColor = new Color(.93f, .92f, .88f, 1);

    public static RectTransform Create(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var layout = go.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8; layout.childControlWidth = true; layout.childControlHeight = true;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        return rt;
    }

    public static void Clear(RectTransform host)
    {
        for (int i = host.childCount - 1; i >= 0; i--)
        {
            Transform child = host.GetChild(i);
            child.gameObject.SetActive(false);
            if (Application.isPlaying) UnityEngine.Object.Destroy(child.gameObject);
            else UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
    }

    public static void Row(RectTransform host, string label, string value, TMP_FontAsset font, float labelWidth = 180, float textSize = 24)
    {
        var row = new GameObject("Row", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
        row.layer = host.gameObject.layer; row.transform.SetParent(host, false);
        var image = row.GetComponent<Image>(); image.color = new Color(.12f,.13f,.16f,1); image.raycastTarget = false;
        var layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(14,14,10,10); layout.spacing = 16;
        layout.childControlWidth = true; layout.childControlHeight = true;
        layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
        if (!string.IsNullOrEmpty(label))
        {
            var t = Text(row.transform, "Label", label, font, textSize - 2, LabelColor);
            var le = t.gameObject.AddComponent<LayoutElement>();
            le.minWidth = le.preferredWidth = labelWidth; le.flexibleWidth = 0;
        }
        var v = Text(row.transform, "Value", string.IsNullOrWhiteSpace(value) ? "-" : value, font, textSize, ValueColor);
        v.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
    }

    public static TMP_Text Text(Transform parent, string name, string value, TMP_FontAsset font, float size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<TextMeshProUGUI>(); if (font != null) text.font = font;
        text.text = value; text.fontSize = size; text.color = color;
        text.alignment = TextAlignmentOptions.TopLeft; text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }

    // Converts existing authored display copy, not a second data store. Colons split only at
    // the first label boundary; dates, paths and session identifiers in values remain intact.
    public static void Details(RectTransform host, string copy, TMP_FontAsset font)
    {
        Clear(host);
        foreach (string line in (copy ?? "").Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            int split = line.IndexOf(": ", StringComparison.Ordinal);
            Row(host, split > 0 ? line.Substring(0, split) : "", split > 0 ? line.Substring(split + 2) : line, font);
        }
    }
}
