// Assets/_Project/Scripts/UI/EvidenceInventoryEntryView.cs
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One row in the evidence inventory panel (see EvidenceInventoryUI). Marks a
/// scene-authored template's editable pieces - the same "marker component exposing
/// child references, only ever repositioned/re-texted by its controller" pattern
/// ToolWheelWedgeTemplate already uses for the tool wheel's own per-wedge template.
/// </summary>
public class EvidenceInventoryEntryView : MonoBehaviour
{
    [SerializeField] private Image icon;
    [Tooltip("Shown in the icon's own slot in place of a real icon sprite, for any item with no EvidenceDefinition.icon assigned yet - a readable word instead of a blank/placeholder square. Switches back to the real icon automatically the moment one is assigned; nothing else needs to change when real art arrives.")]
    [SerializeField] private TMP_Text iconWordLabel;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text tentLabel;
    [SerializeField] private TMP_Text statusLabel;
    [SerializeField] private GameObject equippedIndicator;
    [SerializeField] private GameObject highlightIndicator;
    [SerializeField] private TMP_Text lockedReasonLabel;

    public string EvidenceId { get; private set; }
    public bool CanEquip { get; private set; }
    public string LockedReason { get; private set; }

    private float layoutWidth = -1;
    private int layoutSignature;

    /// <summary>Fit all visible details at their readable font sizes, with one owner of row height.</summary>
    public float RefreshLayout(float width)
    {
        var labels = new[] { nameLabel, tentLabel, statusLabel, lockedReasonLabel };
        int signature = equippedIndicator != null && equippedIndicator.activeSelf ? 1 : 0;
        foreach (var label in labels)
            if (label != null) signature = unchecked(signature * 31 + label.text.GetHashCode()
                + label.fontSize.GetHashCode() + (label.gameObject.activeSelf ? 1 : 0));
        var element = GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();
        if (layoutWidth == width && layoutSignature == signature) return element.preferredHeight;
        layoutWidth = width; layoutSignature = signature;
        var horizontal = GetComponent<HorizontalLayoutGroup>(); if (horizontal != null) horizontal.enabled = false;
        var column = nameLabel != null ? nameLabel.transform.parent as RectTransform : null;
        if (column != null)
        {
            var vertical = column.GetComponent<VerticalLayoutGroup>(); if (vertical != null) vertical.enabled = false;
            column.anchorMin = Vector2.zero; column.anchorMax = Vector2.one;
            column.offsetMin = new Vector2(20, 16); column.offsetMax = new Vector2(-20, -16);
        }
        // The authored placeholder repeats the full item name in a narrow icon slot.
        // Give that name the full text column instead; retain the serialized icon references.
        if (iconWordLabel != null) iconWordLabel.gameObject.SetActive(false);
        bool hasIcon = icon != null && icon.enabled;
        if (hasIcon)
        {
            Place(icon.rectTransform, 20, 16, 64, 64);
            if (column != null) column.offsetMin = new Vector2(100, 16);
        }
        float textWidth = Mathf.Max(1, width - 40 - (hasIcon ? 80 : 0)
            - (equippedIndicator != null && equippedIndicator.activeSelf ? 28 : 0));
        float y = 0;
        foreach (var label in labels)
        {
            if (label == null || !label.gameObject.activeSelf) continue;
            label.enableAutoSizing = false; label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Masking; label.margin = Vector4.one * 2;
            label.alignment = TextAlignmentOptions.TopLeft;
            var fixedHeight = label.GetComponent<LayoutElement>(); if (fixedHeight != null) fixedHeight.ignoreLayout = true;
            float height = Mathf.Ceil(label.GetPreferredValues(label.text, Mathf.Max(1, textWidth - 4), Mathf.Infinity).y) + 4;
            Place(label.rectTransform, 0, y, textWidth, height); y += height + 8;
        }
        float required = Mathf.Max(100, y - 8 + 32, hasIcon ? 96 : 0);
        element.minHeight = element.preferredHeight = required; element.flexibleHeight = 0;
        ((RectTransform)transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, required);
        if (highlightIndicator != null)
        {
            var highlight = (RectTransform)highlightIndicator.transform;
            highlight.anchorMin = Vector2.zero; highlight.anchorMax = new Vector2(0, 1);
            highlight.offsetMin = new Vector2(6, 8); highlight.offsetMax = new Vector2(12, -8);
        }
        if (equippedIndicator != null)
        {
            var badge = (RectTransform)equippedIndicator.transform;
            badge.anchorMin = badge.anchorMax = new Vector2(1, 1); badge.pivot = new Vector2(1, 1);
            badge.anchoredPosition = new Vector2(-20, -16); badge.sizeDelta = new Vector2(18, 18);
        }
        return required;
    }

    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
    }

    public void Bind(string evidenceId, EvidenceDefinition definition, bool isHeld, bool canEquip, string lockedReason)
    {
        EvidenceId = evidenceId;
        CanEquip = canEquip;
        LockedReason = lockedReason;
        layoutWidth = -1;
        if (nameLabel != null) nameLabel.fontSize = 28;
        if (tentLabel != null) tentLabel.fontSize = 24;
        if (statusLabel != null) statusLabel.fontSize = 24;
        if (lockedReasonLabel != null) lockedReasonLabel.fontSize = 24;

        // TEMPORARY: every item's EvidenceDefinition.icon right now is just a flat
        // placeholder colour swatch, not a real representative visual, so it's worse
        // than no icon at all - a word reads better than an arbitrary colour square.
        // Forced false for now; switch this back to `definition.icon != null` the
        // moment real per-item icon art exists and this should resolve itself with no
        // other changes needed anywhere in this file.
        bool hasRealIcon = false;

        if (icon != null)
        {
            icon.sprite = definition.icon;
            icon.enabled = hasRealIcon;
        }

        if (iconWordLabel != null)
        {
            iconWordLabel.gameObject.SetActive(!hasRealIcon);
            iconWordLabel.text = definition.PlayerDisplayName;
        }

        if (nameLabel != null)
        {
            nameLabel.text = definition.PlayerDisplayName;
        }

        var record = EvidenceStateManager.Instance?.GetRecord(evidenceId);

        if (tentLabel != null)
        {
            string tent = record != null
                ? (record.tentNumber.HasValue ? record.tentNumber.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : (!string.IsNullOrEmpty(record.tentLetter) ? record.tentLetter : string.Empty))
                : string.Empty;
            tentLabel.text = string.IsNullOrEmpty(tent) ? string.Empty : "Marker " + tent;
            tentLabel.gameObject.SetActive(!string.IsNullOrEmpty(tent));
        }

        if (statusLabel != null)
        {
            var custody = EvidenceInventory.Instance != null ? EvidenceInventory.Instance.GetCustody(evidenceId) : EvidenceCustodyLocation.Inventory;
            statusLabel.text = custody switch
            {
                EvidenceCustodyLocation.Held => "In hand",
                EvidenceCustodyLocation.EvidenceBox => "Deposited",
                _ => record != null && record.status == EvidenceStatus.Processed ? "Sealed - Processed" : "Sealed"
            };
            if (EvidenceInventory.Instance?.IsFingerprintSample(evidenceId) == true)
            {
                statusLabel.text = isHeld ? "In hand - Lifted fingerprint" : "Stored - Fingerprint sample";
                var source = EvidenceStateManager.Instance?.GetRecord(EvidenceInventory.Instance.GetSampleSource(evidenceId));
                if (!isHeld && source?.fingerprintLabStatus == FingerprintLabStatus.Matched)
                    statusLabel.text = "Stored - " + string.Join(", ",source.fingerprintLabMatchIdentities);
            }
            else if (EvidenceInventory.Instance?.GetSampleSource(evidenceId) != null)
                statusLabel.text = (isHeld ? "In hand - " : "Stored - ") + (EvidenceInventory.Instance.IsSampleSealed(evidenceId) ? "Sealed sample" : "Contained sample");
        }

        if (equippedIndicator != null)
        {
            equippedIndicator.SetActive(isHeld);
        }

        if (highlightIndicator != null)
        {
            highlightIndicator.SetActive(false);
        }

        if (lockedReasonLabel != null)
        {
            lockedReasonLabel.gameObject.SetActive(!canEquip && !isHeld && !string.IsNullOrEmpty(lockedReason));
            lockedReasonLabel.text = lockedReason ?? string.Empty;
        }
    }

    public void SetHighlighted(bool highlighted)
    {
        if (highlightIndicator != null)
        {
            highlightIndicator.SetActive(highlighted);
        }
    }
}
