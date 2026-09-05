// Assets/_Project/Scripts/UI/NotificationUI.cs
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Generic world-space toast popup. One label, one canvas group, and two classes of
/// message competing for them:
///
///  - Show(message) posts a STATE-DRIVEN message: something happened or was refused.
///    Gate-block reasons ("Not yet Marked."), confirmations ("Evidence tent 1
///    placed."), status changes. Each gets its own full, uninterrupted display.
///
///  - ShowPrompt(message) / HidePrompt() posts an AMBIENT message: a standing
///    affordance hint that is true for as long as some context holds, e.g.
///    "[B] Pick Up Evidence Tent" while the player is in range of a tent.
///
/// PRIORITY RULE - AMBIENT PROMPTS ARE ALWAYS PREEMPTABLE, STATE-DRIVEN MESSAGES
/// NEVER ARE. A state-driven message always takes the display, immediately,
/// interrupting any ambient prompt; the prompt resumes on its own once the queue
/// drains, provided the context that asked for it is still true. Do not invert this,
/// and do not add a case that lets some particular prompt outrank a state message.
///
/// This is deliberately expressed as two classes of message rather than as checks on
/// particular strings: every message posted through Show() outranks every message
/// posted through ShowPrompt(), whatever either says, including ones added later.
///
/// Why it matters: an ambient prompt is a restatement of something already true and
/// still visible in the world - the player can walk two steps and see it again. A
/// state-driven message is the only report of an event that has already happened,
/// and if it is not shown at the moment it fires it is not merely delayed, it is
/// lost. The previous version had this exactly backwards: ShowPrompt() paused the
/// toast queue, so standing near a tent silently swallowed every refusal the player
/// triggered. Pulling a trigger and getting no response at all reads as a broken
/// tool, not as a refused action - which is precisely how it was reported.
///
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

    // What the world currently WANTS the ambient prompt to say, which is not the same
    // as what is on screen: it stays set while a state-driven message preempts it, so
    // the prompt can come back afterwards without the caller re-posting it.
    private string ambientPromptMessage;

    private Coroutine driveRoutine;

    /// <summary>True while an ambient prompt is the thing currently requested (whether or not it is the thing currently displayed).</summary>
    public bool HasAmbientPrompt => ambientPromptMessage != null;

    private void Awake()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
    }

    /// <summary>Posts a state-driven message. Always displayed in full; preempts any ambient prompt.</summary>
    public void Show(string message, float? durationSeconds = null)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        pending.Enqueue((message, durationSeconds ?? defaultDisplaySeconds));
        EnsureDriving();
    }

    /// <summary>
    /// Posts the ambient prompt - a standing hint shown while some context holds.
    /// Displayed only while no state-driven message is waiting or playing; it is
    /// recorded either way and appears (or reappears) as soon as the display is free.
    /// </summary>
    public void ShowPrompt(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        ambientPromptMessage = message;
        EnsureDriving();
    }

    /// <summary>Withdraws the ambient prompt. No-op if none is requested. Never affects queued state-driven messages.</summary>
    public void HidePrompt()
    {
        ambientPromptMessage = null;
        EnsureDriving();
    }

    private void EnsureDriving()
    {
        if (driveRoutine == null && isActiveAndEnabled)
        {
            driveRoutine = StartCoroutine(Drive());
        }
    }

    private void OnDisable()
    {
        driveRoutine = null;
        pending.Clear();
        ambientPromptMessage = null;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
    }

    /// <summary>
    /// The single owner of the label. Everything that wants to display something adds
    /// to state and lets this decide, so the two message classes can never each hold a
    /// half-finished coroutine writing over the other - which is what made the old
    /// version's ordering depend on which call happened to arrive first.
    /// </summary>
    private IEnumerator Drive()
    {
        while (true)
        {
            // State-driven always wins, and drains completely before anything ambient
            // is considered.
            if (pending.Count > 0)
            {
                var (message, duration) = pending.Dequeue();
                SetLabel(message);
                yield return Fade(CurrentAlpha(), 1f, fadeSeconds);
                yield return new WaitForSeconds(duration);
                yield return Fade(CurrentAlpha(), 0f, fadeSeconds);
                continue;
            }

            // Nothing state-driven waiting, so the ambient prompt may have the display
            // back - but only until the next state-driven message arrives.
            if (ambientPromptMessage != null)
            {
                string showing = ambientPromptMessage;
                SetLabel(showing);
                yield return Fade(CurrentAlpha(), 1f, fadeSeconds);

                while (ambientPromptMessage == showing && pending.Count == 0)
                {
                    yield return null;
                }

                // Preempted by a state-driven message: clear the display for it. If the
                // prompt merely changed or was withdrawn, loop round and re-evaluate
                // without a redundant fade.
                if (pending.Count > 0)
                {
                    yield return Fade(CurrentAlpha(), 0f, fadeSeconds);
                }
                continue;
            }

            // Nothing to show at all.
            if (CurrentAlpha() > 0f)
            {
                yield return Fade(CurrentAlpha(), 0f, fadeSeconds);
            }

            // Re-check rather than exiting blind: a message may have been posted during
            // that fade, and dropping it here would reintroduce the lost-message bug
            // this class exists to prevent.
            if (pending.Count == 0 && ambientPromptMessage == null)
            {
                driveRoutine = null;
                yield break;
            }
        }
    }

    private void SetLabel(string message)
    {
        if (label != null)
        {
            label.text = message;
        }
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
