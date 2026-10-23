// Assets/_Project/Scripts/UI/ForensicUIStyle.cs
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared visual language for ViCSI's world-space review panels ("forensic field software
/// projected into VR"): one set of colour tokens, type sizes and small builders so the
/// Evidence / Procedure panel and the panels that follow it (Evidence Notes, Dialogue
/// History, Sketch, Objectives, Deduction) look like one application.
///
/// Flat panel, separators and alignment instead of nested cards; restrained colour
/// (blue = selected/interactive, gold = the one current action, green = done, amber =
/// warning, red = genuine error). State is never carried by colour alone - every state
/// also has a distinct shape (check, filled dot, hollow ring) or label.
///
/// Sizes are canvas units on a canvas scaled 0.001 (1 unit = 1 mm). They were chosen for
/// reading at ~1.35 m on Quest: body text 30 units is ~1.3 degrees per em.
/// </summary>
public static class ForensicUIStyle
{
    // --- Colour tokens -------------------------------------------------------------
    public static readonly Color PanelBackground = new Color(0.075f, 0.086f, 0.11f, 0.98f);
    public static readonly Color HeaderBackground = new Color(0.10f, 0.115f, 0.145f, 1f);
    public static readonly Color FooterBackground = new Color(0.11f, 0.12f, 0.15f, 1f);
    public static readonly Color Separator = new Color(1f, 1f, 1f, 0.09f);

    public static readonly Color TextPrimary = new Color(0.93f, 0.93f, 0.90f, 1f);
    public static readonly Color TextSecondary = new Color(0.74f, 0.76f, 0.79f, 1f);
    public static readonly Color TextMuted = new Color(0.56f, 0.59f, 0.64f, 1f);
    public static readonly Color TextHeading = new Color(0.62f, 0.67f, 0.74f, 1f);

    public static readonly Color Interactive = new Color(0.43f, 0.60f, 0.80f, 1f);
    public static readonly Color SelectedFill = new Color(0.20f, 0.29f, 0.41f, 1f);
    public static readonly Color HoverFill = new Color(1f, 1f, 1f, 0.06f);

    public static readonly Color Accent = new Color(0.86f, 0.70f, 0.36f, 1f);       // current / important
    public static readonly Color AccentFill = new Color(0.86f, 0.70f, 0.36f, 0.11f);
    public static readonly Color Success = new Color(0.45f, 0.72f, 0.52f, 1f);
    public static readonly Color Warning = new Color(0.93f, 0.66f, 0.30f, 1f);
    public static readonly Color Error = new Color(0.86f, 0.38f, 0.34f, 1f);

    // --- Type scale (canvas units) ----------------------------------------------------
    public const float TitleSize = 36f;
    public const float ItemHeadingSize = 34f;
    public const float SectionHeadingSize = 22f;
    public const float BodySize = 29f;
    public const float MetaSize = 26f;
    public const float ListItemSize = 27f;
    public const float NextActionSize = 31f;
    public const float HintSize = 20f;

    // --- Text --------------------------------------------------------------------------

    public static TMP_Text Text(Transform parent, string name, float size, Color color, FontStyles style = FontStyles.Normal,
        TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
    {
        var go = EditableSceneVisuals.Create(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var text = EditableSceneVisuals.Component<TextMeshProUGUI>(go);
        text.fontSize = size;
        text.color = color;
        text.fontStyle = style;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.alignment = alignment;
        text.richText = true;
        text.raycastTarget = false;
        return text;
    }

    /// <summary>Small letter-spaced section label (the only all-caps text on a panel).</summary>
    public static TMP_Text SectionHeading(Transform parent, string label)
    {
        var text = Text(parent, "Heading_" + label, SectionHeadingSize, TextHeading, FontStyles.UpperCase);
        text.characterSpacing = 6f;
        text.text = label;
        return text;
    }

    // --- Shapes --------------------------------------------------------------------------

    public static Image Rect(Transform parent, string name, Color color)
    {
        var go = EditableSceneVisuals.Create(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>1-unit hairline; horizontal when vertical == false.</summary>
    public static Image SeparatorLine(Transform parent, bool vertical = false)
    {
        var line = Rect(parent, vertical ? "SeparatorV" : "SeparatorH", Separator);
        var r = line.rectTransform;
        if (vertical)
        {
            r.anchorMin = new Vector2(0f, 0f);
            r.anchorMax = new Vector2(0f, 1f);
            r.sizeDelta = new Vector2(2f, 0f);
        }
        else
        {
            r.anchorMin = new Vector2(0f, 0f);
            r.anchorMax = new Vector2(1f, 0f);
            r.sizeDelta = new Vector2(0f, 2f);
        }
        return line;
    }

    private static Sprite discSprite;
    private static Sprite ringSprite;

    /// <summary>Anti-aliased filled circle (generated once - no editor-only built-in sprites).</summary>
    public static Sprite Disc => discSprite != null ? discSprite : discSprite = MakeCircle(false);

    /// <summary>Anti-aliased hollow ring.</summary>
    public static Sprite Ring => ringSprite != null ? ringSprite : ringSprite = MakeCircle(true);

    private static Sprite MakeCircle(bool ring)
    {
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = ring ? "ForensicRing" : "ForensicDisc" };
        var pixels = new Color32[size * size];
        float centre = (size - 1) * 0.5f;
        float outer = size * 0.5f - 1f;
        float inner = outer - size * 0.14f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre));
                float a = Mathf.Clamp01(outer - d + 0.5f);
                if (ring)
                {
                    a = Mathf.Min(a, Mathf.Clamp01(d - inner + 0.5f));
                }
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    public static Image Circle(Transform parent, string name, bool hollow, Color color, float diameter)
    {
        var image = Rect(parent, name, color);
        image.sprite = hollow ? Ring : Disc;
        image.rectTransform.sizeDelta = new Vector2(diameter, diameter);
        return image;
    }

    /// <summary>A check mark drawn from two bars (no reliance on a font having U+2713).</summary>
    public static RectTransform CheckMark(Transform parent, Color color, float size, float thickness = 5f)
    {
        var holder = EditableSceneVisuals.Create("Check", typeof(RectTransform)).GetComponent<RectTransform>();
        holder.SetParent(parent, false);
        holder.sizeDelta = new Vector2(size, size);
        var shortBar = Rect(holder, "Short", color).rectTransform;
        shortBar.sizeDelta = new Vector2(size * 0.42f, thickness);
        shortBar.anchoredPosition = new Vector2(-size * 0.20f, -size * 0.06f);
        shortBar.localRotation = Quaternion.Euler(0f, 0f, -45f);
        var longBar = Rect(holder, "Long", color).rectTransform;
        longBar.sizeDelta = new Vector2(size * 0.78f, thickness);
        longBar.anchoredPosition = new Vector2(size * 0.10f, size * 0.04f);
        longBar.localRotation = Quaternion.Euler(0f, 0f, 50f);
        return holder;
    }

    /// <summary>An X drawn from two bars (close control).</summary>
    public static RectTransform Cross(Transform parent, Color color, float size, float thickness = 4f)
    {
        var holder = EditableSceneVisuals.Create("Cross", typeof(RectTransform)).GetComponent<RectTransform>();
        holder.SetParent(parent, false);
        holder.sizeDelta = new Vector2(size, size);
        foreach (float angle in new[] { 45f, -45f })
        {
            var bar = Rect(holder, "Bar", color).rectTransform;
            bar.sizeDelta = new Vector2(size * 1.25f, thickness);
            bar.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
        return holder;
    }

    // --- Layout helpers ------------------------------------------------------------------

    /// <summary>Places rect by its top-left corner inside its parent (top-left anchored).</summary>
    public static RectTransform PlaceTopLeft(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    public static RectTransform Stretch(RectTransform rect, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        return rect;
    }

    /// <summary>
    /// "Label ........ value" row for a vertical layout (measurements, counts). The value is
    /// right-aligned in its own column so a list of rows reads as an aligned table.
    /// </summary>
    public static RectTransform ValueRow(Transform parent, string name, string label, string value, float height,
        float size = MetaSize, Color? labelColor = null, Color? valueColor = null, float indent = 0f, float valueWidth = 110f)
    {
        var row = Group(parent, name);
        EditableSceneVisuals.Component<LayoutElement>(row.gameObject).preferredHeight = height;
        var l = Text(row, "Label", size, labelColor ?? TextSecondary);
        Stretch(l.rectTransform, indent, 0f, valueWidth + 8f, 0f);
        l.textWrappingMode = TextWrappingModes.NoWrap;
        l.text = label;
        var v = Text(row, "Value", size, valueColor ?? TextPrimary, FontStyles.Normal, TextAlignmentOptions.MidlineRight);
        v.rectTransform.anchorMin = new Vector2(1f, 0f);
        v.rectTransform.anchorMax = new Vector2(1f, 1f);
        v.rectTransform.pivot = new Vector2(1f, 0.5f);
        v.rectTransform.sizeDelta = new Vector2(valueWidth, 0f);
        v.rectTransform.anchoredPosition = Vector2.zero;
        v.textWrappingMode = TextWrappingModes.NoWrap;
        v.text = value;
        return row;
    }

    /// <summary>Vertical stack (top-down, children control their own height via LayoutElement).</summary>
    public static VerticalLayoutGroup Stack(RectTransform rect, float spacing = 0f)
    {
        var layout = EditableSceneVisuals.Component<VerticalLayoutGroup>(rect.gameObject);
        layout.spacing = spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.UpperLeft;
        return layout;
    }

    public static RectTransform Group(Transform parent, string name)
    {
        var rect = EditableSceneVisuals.Create(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }
}
