// Assets/_Project/Scripts/UI/NotificationUI.cs
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Generic world-space toast popup with two ways to use it:
///  - Show(message) queues a transient line that fades in, holds, fades out.
///    Back-to-back events each get their own full, uninterrupted display instead
///    of stepping on each other, unlike STCSNotificationUI (one line, restarts on
///    interruption).
///  - ShowPrompt(message) / HidePrompt() is for a message that should stay up for
///    as long as something is true (e.g. "[X] Pick Up Evidence Tent" while the
///    player is in range) - it takes over the display immediately, pausing the
///    toast queue, and resumes the queue once hidden.
/// Knows nothing about what triggered a message; see NotificationManager for how
/// other systems reach this.
/// </summary>
public class NotificationUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text label;
    [SerializeField] private float defaultDisplaySeconds = 3f;
    [SerializeField] private float fadeSeconds = 0.2f;

    private readonly Queue<(string message, float duration)> pending = new();
    private Coroutine playRoutine;
    private Coroutine promptRoutine;
    private bool promptActive;

    private void Awake()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
    }

    public void Show(string message, float? durationSeconds = null)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        pending.Enqueue((message, durationSeconds ?? defaultDisplaySeconds));

        if (!promptActive && playRoutine == null)
        {
            playRoutine = StartCoroutine(PlayQueue());
        }
    }

    /// <summary>Shows a message that stays up until HidePrompt() is called - takes over the display from the toast queue.</summary>
    public void ShowPrompt(string message)
    {
        if (promptActive && label != null && label.text == message)
        {
            return;
        }

        promptActive = true;

        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        if (label != null)
        {
            label.text = message;
        }

        if (promptRoutine != null)
        {
            StopCoroutine(promptRoutine);
        }
        promptRoutine = StartCoroutine(Fade(CurrentAlpha(), 1f, fadeSeconds));
    }

    /// <summary>Hides the current prompt (no-op if none is showing) and resumes any queued toasts.</summary>
    public void HidePrompt()
    {
        if (!promptActive)
        {
            return;
        }

        promptActive = false;

        if (promptRoutine != null)
        {
            StopCoroutine(promptRoutine);
        }
        promptRoutine = StartCoroutine(HidePromptThenResumeQueue());
    }

    private IEnumerator HidePromptThenResumeQueue()
    {
        yield return Fade(CurrentAlpha(), 0f, fadeSeconds);
        promptRoutine = null;

        if (pending.Count > 0 && playRoutine == null)
        {
            playRoutine = StartCoroutine(PlayQueue());
        }
    }

    private IEnumerator PlayQueue()
    {
        while (pending.Count > 0)
        {
            var (message, duration) = pending.Dequeue();

            if (label != null)
            {
                label.text = message;
            }

            yield return Fade(0f, 1f, fadeSeconds);
            yield return new WaitForSeconds(duration);
            yield return Fade(1f, 0f, fadeSeconds);
        }

        playRoutine = null;
    }

    private float CurrentAlpha() => canvasGroup != null ? canvasGroup.alpha : 0f;

    private IEnumerator Fade(float from, float to, float seconds)
    {
        if (canvasGroup == null)
        {
            yield break;
        }

        if (seconds <= 0f)
        {
            canvasGroup.alpha = to;
            yield break;
        }

        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / seconds));
            yield return null;
        }
        canvasGroup.alpha = to;
    }
}
