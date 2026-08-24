// Assets/_Project/Scripts/STCS/STCSNotificationUI.cs
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Minimal world-space text popup for STCS lines - a floating panel that
/// shows a line, then hides itself after a delay. Style/polish can come later.
/// </summary>
public class STCSNotificationUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text label;
    [SerializeField] private float displaySeconds = 4f;

    private Coroutine hideRoutine;

    private void Awake()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
    }

    public void ShowLine(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return;
        }

        if (label != null)
        {
            label.text = line;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }

        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
        }
        hideRoutine = StartCoroutine(HideAfterDelay());
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(displaySeconds);
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
        hideRoutine = null;
    }
}
