// Assets/_Project/Scripts/STCS/STCSManager.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fires STCS (Scripted Team Communication System) trigger moments: simulated
/// non-played teammates commenting on, or completing, scene actions. Other
/// interaction scripts call Fire(triggerId) when the matching condition occurs.
/// </summary>
public class STCSManager : MonoBehaviour
{
    public static STCSManager Instance { get; private set; }

    [SerializeField] private List<STCSTrigger> triggers = new();
    [SerializeField] private STCSNotificationUI notificationUI;
    [SerializeField] private float lineDisplaySeconds = 3f;

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>Fires the first untriggered STCSTrigger matching triggerId, if any.</summary>
    public void Fire(string triggerId)
    {
        var trigger = triggers.Find(t => t.triggerId == triggerId && !t.firedOnce);
        if (trigger == null)
        {
            Debug.LogWarning($"[STCSManager] No untriggered STCSTrigger found for '{triggerId}'.");
            return;
        }

        trigger.firedOnce = true;
        StartCoroutine(PlayTrigger(trigger));
    }

    private IEnumerator PlayTrigger(STCSTrigger trigger)
    {
        if (trigger.pool == null || trigger.pool.lines == null || trigger.pool.lines.Length == 0)
        {
            yield break;
        }

        if (!trigger.playAllLinesInSequence)
        {
            ShowLine(trigger.pool.GetRandomLine());
            ApplyEvidenceSideEffect(trigger.triggerId, 0);
            yield break;
        }

        for (int i = 0; i < trigger.pool.lines.Length; i++)
        {
            ShowLine(trigger.pool.lines[i]);
            ApplyEvidenceSideEffect(trigger.triggerId, i);

            if (i < trigger.pool.lines.Length - 1)
            {
                yield return new WaitForSeconds(lineDisplaySeconds);
            }
        }
    }

    private void ShowLine(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return;
        }

        if (notificationUI != null)
        {
            notificationUI.ShowLine(line);
        }

        Debug.Log($"[STCS] {line}");
    }

    // MVP-only: certain STCS lines represent a simulated teammate completing an
    // evidence-handling step rather than pure flavor text, so firing them also
    // advances evidence state. Extend this table as more STCS-driven evidence
    // actions are added; a data-driven version can replace it once the pattern repeats.
    private static void ApplyEvidenceSideEffect(string triggerId, int lineIndex)
    {
        if (EvidenceStateManager.Instance == null)
        {
            return;
        }

        if (triggerId == "evidence_014_photographed")
        {
            if (lineIndex == 0)
            {
                EvidenceStateManager.Instance.MarkSketched("EVD-014", ToolType.None);
            }
            else if (lineIndex == 1)
            {
                EvidenceStateManager.Instance.MarkLogged("EVD-014", ToolType.None);
            }
        }
    }
}
