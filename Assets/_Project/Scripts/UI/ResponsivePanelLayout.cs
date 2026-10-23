using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Bounded text panels with independently scrolling header/body/footer and stable vertical option layout.</summary>
[DefaultExecutionOrder(100)]
public sealed class ResponsivePanelLayout : MonoBehaviour
{
    public sealed class Area
    {
        public RectTransform Root, Content, Viewport;
        public ScrollRect Scroll;
        private GameObject up, down;
        private readonly float spacing;
        public float PreferredHeight { get; private set; }
        public int Signature { get; private set; }
        public bool CanScroll => PreferredHeight > Viewport.rect.height + .5f;
        public Area(RectTransform panel, string name, RectTransform existingContent, float gap)
        {
            spacing = gap;
            Root = Rect(panel, name + "Scroll");
            Scroll = EditableSceneVisuals.Component<ScrollRect>(Root.gameObject);
            Scroll.horizontal = false; Scroll.vertical = true; Scroll.inertia = false;
            Scroll.movementType = ScrollRect.MovementType.Clamped;
            Viewport = Rect(Root, "Viewport");
            Viewport.anchorMin = Vector2.zero; Viewport.anchorMax = Vector2.one;
            Viewport.offsetMin = Viewport.offsetMax = Vector2.zero;
            EditableSceneVisuals.Component<RectMask2D>(Viewport.gameObject);
            Content = existingContent != null ? existingContent : Rect(Viewport, "Content");
            Content.SetParent(Viewport, false);
            Content.anchorMin = new Vector2(0, 1); Content.anchorMax = Vector2.one; Content.pivot = new Vector2(.5f, 1);
            Content.anchoredPosition = Vector2.zero; Content.sizeDelta = Vector2.zero;
            var fitter = Content.GetComponent<ContentSizeFitter>(); if (fitter != null) fitter.enabled = false;
            var layout = Content.GetComponent<VerticalLayoutGroup>() ?? EditableSceneVisuals.Component<VerticalLayoutGroup>(Content.gameObject);
            layout.spacing = gap; layout.childAlignment = TextAnchor.UpperCenter;
            layout.padding = new RectOffset();
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            Scroll.viewport = Viewport; Scroll.content = Content;
        }
        public void RegisterControls(WorldPanelPointer pointer, string prefix)
        {
            up = ScrollButton("Scroll up", 1, true, pointer, prefix + "up");
            down = ScrollButton("Scroll down", -1, false, pointer, prefix + "down");
        }
        private GameObject ScrollButton(string name, int direction, bool atTop, WorldPanelPointer pointer, string id)
        {
            var image = ForensicUIStyle.Rect(Root, name, ForensicUIStyle.SelectedFill);
            var rect = image.rectTransform; rect.anchorMin = rect.anchorMax = new Vector2(1, atTop ? 1 : 0);
            rect.pivot = new Vector2(0, atTop ? 1 : 0); rect.sizeDelta = new Vector2(28, 32); rect.anchoredPosition = new Vector2(4, 0);
            // Geometric chevron, independent of font glyph support.
            for (int i = 0; i < 2; i++)
            {
                var bar = ForensicUIStyle.Rect(rect, "Arrow", ForensicUIStyle.TextPrimary).rectTransform;
                bar.sizeDelta = new Vector2(12, 3); bar.anchoredPosition = new Vector2(i == 0 ? -4 : 4, 0);
                bar.localRotation = Quaternion.Euler(0, 0, (i == 0 ? 45 : -45) * direction);
            }
            pointer.Register(id, rect, Vector2.zero, () => ScrollBy(-direction * 90));
            image.gameObject.SetActive(false); return image.gameObject;
        }
        public float Measure(float width)
        {
            PreferredHeight = 0; int count = 0; Signature = 17;
            for (int i = 0; i < Content.childCount; i++)
            {
                var child = Content.GetChild(i) as RectTransform;
                if (child == null || !child.gameObject.activeSelf) continue;
                var responsive = child.GetComponent<ResponsiveTextOption>();
                float height = responsive != null ? responsive.Refresh(width) : LayoutUtility.GetPreferredHeight(child);
                var element = child.GetComponent<LayoutElement>(); if (element != null && element.ignoreLayout) continue;
                if (height <= 0) height = child.rect.height;
                if (count++ > 0) PreferredHeight += spacing;
                PreferredHeight += height;
                Signature = unchecked(Signature * 31 + child.GetEntityId().GetHashCode() + height.GetHashCode());
            }
            Content.sizeDelta = new Vector2(0, PreferredHeight);
            return PreferredHeight;
        }
        public void Place(float width, float height, float top)
        {
            Root.anchorMin = Root.anchorMax = new Vector2(.5f, 1); Root.pivot = new Vector2(.5f, 1);
            Root.sizeDelta = new Vector2(width, height); Root.anchoredPosition = new Vector2(0, -top);
            LayoutRows(width);
        }
        public void RefreshFixed()
        {
            Measure(Root.rect.width); LayoutRows(Root.rect.width);
        }
        private void LayoutRows(float width)
        {
            // Deterministic initial geometry also while the parent is still inactive.
            float y = 0;
            for (int i = 0; i < Content.childCount; i++)
            {
                var row = Content.GetChild(i) as RectTransform;
                if (row == null || !row.gameObject.activeSelf) continue;
                var element = row.GetComponent<LayoutElement>(); if (element != null && element.ignoreLayout) continue;
                float rowHeight = element != null && element.preferredHeight >= 0 ? element.preferredHeight : row.rect.height;
                row.anchorMin = row.anchorMax = new Vector2(.5f, 1); row.pivot = new Vector2(.5f, 1);
                row.sizeDelta = new Vector2(width, rowHeight); row.anchoredPosition = new Vector2(0, -y); y += rowHeight + spacing;
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(Content);
            if (!CanScroll) Content.anchoredPosition = Vector2.zero;
            else Scroll.verticalNormalizedPosition = Mathf.Clamp01(Scroll.verticalNormalizedPosition);
            if (up != null) up.SetActive(CanScroll); if (down != null) down.SetActive(CanScroll);
        }
        public static Area WrapFixed(RectTransform content, string name, float spacing)
        {
            var parent = (RectTransform)content.parent;
            var aMin = content.anchorMin; var aMax = content.anchorMax; var pivot = content.pivot;
            var position = content.anchoredPosition; var size = content.sizeDelta;
            var background = content.GetComponent<Image>();
            var area = new Area(parent, name, content, spacing);
            area.Root.anchorMin = aMin; area.Root.anchorMax = aMax; area.Root.pivot = pivot;
            area.Root.sizeDelta = size; area.Root.anchoredPosition = position;
            if (background != null)
            {
                var copy = ForensicUIStyle.Rect(area.Root, "Background", background.color).rectTransform;
                copy.SetAsFirstSibling(); copy.anchorMin = Vector2.zero; copy.anchorMax = Vector2.one;
                copy.offsetMin = copy.offsetMax = Vector2.zero; background.enabled = false;
            }
            return area;
        }
        public void ScrollBy(float units)
        {
            if (!CanScroll) return;
            Scroll.StopMovement();
            var position = Content.anchoredPosition;
            position.y = Mathf.Clamp(position.y + units, 0, PreferredHeight - Viewport.rect.height);
            Content.anchoredPosition = position;
        }
        public void EnsureVisible(RectTransform row)
        {
            if (row == null || !row.IsChildOf(Content) || !CanScroll) return;
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(Viewport, row);
            if (bounds.size.y > Viewport.rect.height || bounds.max.y > Viewport.rect.yMax)
                ScrollBy(Viewport.rect.yMax - bounds.max.y);
            else if (bounds.min.y < Viewport.rect.yMin) ScrollBy(Viewport.rect.yMin - bounds.min.y);
        }
    }
    private RectTransform panel;
    [SerializeField] private float minimum, maximum, horizontalPadding;
    private float edge = 24, gap = 12;
    [SerializeField] private float headerMaximum = 280, footerMaximum = 180;
    private int lastSignature;
    private float lastWidth = -1, lastHeight;
    public Area Header { get; private set; }
    public Area Body { get; private set; }
    public Area Footer { get; private set; }
    public float MaximumHeight => maximum;
    public static ResponsivePanelLayout Create(RectTransform panel, TMP_Text header, RectTransform content, TMP_Text footer,
        float minHeight, float maxHeight, float paddingX = 40, float spacing = 12)
    {
        var layout = EditableSceneVisuals.Component<ResponsivePanelLayout>(panel.gameObject);
        layout.panel = panel; layout.minimum = minHeight; layout.maximum = maxHeight; layout.horizontalPadding = paddingX;
        layout.Header = new Area(panel, "Header", null, 0);
        layout.Body = new Area(panel, "Options", content, spacing);
        layout.Footer = new Area(panel, "Footer", null, 0);
        if (header != null) layout.AddText(header, layout.Header, 48);
        if (footer != null) layout.AddText(footer, layout.Footer, 40, 12);
        return layout;
    }
    public RectTransform AddText(TMP_Text text, Area area, float minimumHeight = 48, float paddingY = 16)
    {
        var row = Rect(area.Content, text.name + "Container"); text.transform.SetParent(row, false);
        ResponsiveTextOption.Bind(row, text, minimumHeight, 20, paddingY); return row;
    }
    public void AddOption(RectTransform row, TMP_Text text, float minimumHeight = 64)
    {
        row.SetParent(Body.Content, false);
        ResponsiveTextOption.Bind(row, text, minimumHeight);
    }
    public void AddFixedRow(RectTransform row) { row.SetParent(Body.Content, false); }
    public void RegisterControls(WorldPanelPointer pointer, string prefix)
    {
        Header.RegisterControls(pointer, prefix + "header-"); Body.RegisterControls(pointer, prefix + "body-"); Footer.RegisterControls(pointer, prefix + "footer-");
    }
    public void Refresh(bool force = true)
    {
        if (panel == null) return;
        float width = panel.rect.width - horizontalPadding * 2;
        float header = Header.Measure(width), body = Body.Measure(width), footer = Footer.Measure(width);
        int signature = unchecked(Header.Signature * 31 + Body.Signature * 37 + Footer.Signature * 41);
        if (!force && signature == lastSignature && width == lastWidth && panel.rect.height == lastHeight) return;
        float hh = Mathf.Min(header, headerMaximum), fh = Mathf.Min(footer, footerMaximum);
        float gaps = (header > 0 ? gap : 0) + (footer > 0 ? gap : 0);
        float height = Mathf.Clamp(edge * 2 + hh + body + fh + gaps, minimum, maximum);
        panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        Header.Place(width, hh, edge);
        Body.Place(width, Mathf.Max(1, height - edge * 2 - hh - fh - gaps), edge + hh + (header > 0 ? gap : 0));
        Footer.Place(width, fh, height - edge - fh);
        lastSignature = signature; lastWidth = width; lastHeight = height;
    }
    public void EnsureVisible(RectTransform row) => Body.EnsureVisible(row);
    public void ScrollOptions(float units) => Body.ScrollBy(units);
    private void LateUpdate() => Refresh(false);
    public static RectTransform Rect(Transform parent, string name)
    {
        var rect = EditableSceneVisuals.Create(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false); return rect;
    }
}
