// Assets/_Project/Scripts/UI/InsightCardView.cs
using TMPro;
using UnityEngine;

/// <summary>
/// One row in InsightsUI's list - purely a dumb view, no logic of its own. Binds the
/// three fields InsightsUI is allowed to show (displayName/description/suggestedAction)
/// and nothing else - never handed a raw insightId, requirement, or finding reference.
/// </summary>
public class InsightCardView : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text suggestedActionText;

    public RectTransform RectTransform => (RectTransform)transform;

    /// <summary>Wires up runtime-built text components - used only by InsightsUI.BuildRuntimeUI's fallback path.</summary>
    public void AssignRuntimeVisuals(TMP_Text title, TMP_Text description, TMP_Text suggestedAction)
    {
        titleText = title;
        descriptionText = description;
        suggestedActionText = suggestedAction;
    }

    public void Bind(string displayName, string description, string suggestedAction)
    {
        if (titleText != null)
        {
            titleText.text = displayName;
        }

        if (descriptionText != null)
        {
            descriptionText.text = description;
        }

        if (suggestedActionText != null)
        {
            bool hasAction = !string.IsNullOrWhiteSpace(suggestedAction);
            suggestedActionText.gameObject.SetActive(hasAction);
            if (hasAction)
            {
                suggestedActionText.text = "Suggested: " + suggestedAction;
            }
        }
    }
}
