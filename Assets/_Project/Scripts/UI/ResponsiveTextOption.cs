using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Text preferred height plus padding. One owner of row height; no ContentSizeFitter feedback loop.</summary>
[DisallowMultipleComponent]
public sealed class ResponsiveTextOption : MonoBehaviour
{
    private TMP_Text label;
    private LayoutElement element;
    [SerializeField] private float minimumHeight = 64, horizontalPadding = 20, verticalPadding = 16;
    private string measuredText;
    private float measuredWidth = -1, measuredFontSize, measuredLineSpacing, measuredHeight;
    private FontStyles measuredStyle;
    private TMP_FontAsset measuredFont;
    private Vector4 measuredMargins;
    private float measuredPaddingX, measuredCharacterSpacing, measuredWordSpacing;
    public TMP_Text Label => label;
    public float RequiredHeight { get; private set; }
    public float HorizontalPadding => horizontalPadding;
    public float VerticalPadding => verticalPadding;
    public static ResponsiveTextOption Bind(RectTransform row, TMP_Text text, float minimum = 64, float paddingX = 20, float paddingY = 16)
    {
        var fit = row.GetComponent<ResponsiveTextOption>();
        if (fit == null) fit = row.gameObject.AddComponent<ResponsiveTextOption>();
        fit.label = text; fit.minimumHeight = minimum; fit.horizontalPadding = paddingX; fit.verticalPadding = paddingY;
        fit.element = row.GetComponent<LayoutElement>() ?? row.gameObject.AddComponent<LayoutElement>();
        if (text != null)
        {
            text.enableAutoSizing = false; text.textWrappingMode = TextWrappingModes.Normal;
            // TMP's SDF glyph bounds can overhang the advance/ascender by about one unit.
            // Retain a small inner inset as well as the button's readable outer padding.
            text.overflowMode = TextOverflowModes.Masking; text.margin = Vector4.one * 2;
            var rect = text.rectTransform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(paddingX, paddingY); rect.offsetMax = new Vector2(-paddingX, -paddingY);
        }
        return fit;
    }
    public float Measure(float width)
    {
        if (label == null) return minimumHeight;
        if (measuredText != label.text || measuredWidth != width || measuredFontSize != label.fontSize
            || measuredLineSpacing != label.lineSpacing || measuredStyle != label.fontStyle || measuredFont != label.font
            || measuredMargins != label.margin || measuredPaddingX != horizontalPadding
            || measuredCharacterSpacing != label.characterSpacing || measuredWordSpacing != label.wordSpacing)
        {
            measuredText = label.text; measuredWidth = width; measuredFontSize = label.fontSize;
            measuredLineSpacing = label.lineSpacing; measuredStyle = label.fontStyle; measuredFont = label.font;
            measuredMargins = label.margin; measuredPaddingX = horizontalPadding;
            measuredCharacterSpacing = label.characterSpacing; measuredWordSpacing = label.wordSpacing;
            measuredHeight = Mathf.Ceil(label.GetPreferredValues(label.text, Mathf.Max(1, width - 2 * horizontalPadding - label.margin.x - label.margin.z), Mathf.Infinity).y);
        }
        return Mathf.Max(minimumHeight, measuredHeight + 2 * verticalPadding);
    }
    public float Refresh(float width)
    {
        if (element == null) element = GetComponent<LayoutElement>();
        if (label != null)
        {
            label.rectTransform.offsetMin = new Vector2(horizontalPadding, verticalPadding);
            label.rectTransform.offsetMax = new Vector2(-horizontalPadding, -verticalPadding);
        }
        element.ignoreLayout = label != null && !label.gameObject.activeSelf;
        RequiredHeight = element.ignoreLayout ? 0 : Measure(width);
        element.minHeight = element.preferredHeight = RequiredHeight; element.flexibleHeight = 0;
        ((RectTransform)transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, RequiredHeight);
        return RequiredHeight;
    }
}
