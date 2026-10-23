// Assets/_Project/Scripts/UI/DialogueCountdownBar.cs
using UnityEngine;

/// <summary>
/// The thin line along the bottom of a dialogue panel that shows how long the current
/// message stays up: full when it appears, shrinking smoothly to nothing as it hides.
/// Secondary to the text on purpose - a few pixels high, low contrast, no numbers, no
/// pulsing. Scales the fill's width (left-pivoted) rather than using a Filled Image, so
/// it needs no sprite.
/// </summary>
public class DialogueCountdownBar : MonoBehaviour
{
    [SerializeField] private RectTransform fill;
    [SerializeField] private CanvasGroup group;

    private void Awake()
    {
        SetVisible(false);
    }

    /// <summary>0 = empty, 1 = full.</summary>
    public void SetProgress(float progress)
    {
        if (fill != null)
        {
            fill.localScale = new Vector3(Mathf.Clamp01(progress), 1f, 1f);
        }
    }

    /// <summary>Hidden for messages with no countdown (ambient prompts) and between messages.</summary>
    public void SetVisible(bool visible)
    {
        if (group != null)
        {
            group.alpha = visible ? 1f : 0f;
        }
    }
}
