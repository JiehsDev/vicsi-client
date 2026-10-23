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

    private void OnEnable()
    {
        EvidenceStateManager.OnFingerprintLabStatusChanged += HandleFingerprintLabStatusChanged;
    }

    private void OnDisable()
    {
        EvidenceStateManager.OnFingerprintLabStatusChanged -= HandleFingerprintLabStatusChanged;
    }

    /// <summary>
    /// Generic reveal hook for the decoupled fingerprint lab side-track (see
    /// FingerprintLabStatus) - exactly the extensibility point that side-track's own
    /// event was built for (see EvidenceStateManager.OnFingerprintLabStatusChanged's
    /// class comment). Fires "evidence_&lt;digits&gt;_matched" the moment any item's
    /// print resolves to Matched - generic by evidenceId, same as every other
    /// FireEvidenceSTCSTrigger call in this project (an item with no matching
    /// STCSTrigger entry just no-ops with a harmless "no untriggered trigger found"
    /// warning).
    /// </summary>
    private void HandleFingerprintLabStatusChanged(string evidenceId, FingerprintLabStatus newStatus)
    {
        if (newStatus == FingerprintLabStatus.Matched)
        {
            PlayerTool.FireEvidenceSTCSTrigger(evidenceId, "matched");
        }
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
        if (SceneTestStart.IsPreparing) return;
        StartCoroutine(PlayTrigger(trigger));
    }

    private IEnumerator PlayTrigger(STCSTrigger trigger)
    {
        if (trigger.lineIds != null && trigger.lineIds.Length > 0)
        {
            // Catalog content (ViCSIDialogueCatalog): text, timing and priority come from there.
            if (!trigger.playAllLinesInSequence)
            {
                GameDialogue.SayLine(trigger.lineIds[Random.Range(0, trigger.lineIds.Length)], "Team", trigger.triggerId, notificationUI);
                ApplyEvidenceSideEffect(trigger.triggerId, 0);
                yield break;
            }
            for (int i = 0; i < trigger.lineIds.Length; i++)
            {
                GameDialogue.SayLine(trigger.lineIds[i], "Team", trigger.triggerId, notificationUI);
                ApplyEvidenceSideEffect(trigger.triggerId, i);
                if (i < trigger.lineIds.Length - 1)
                {
                    yield return new WaitForSeconds(lineDisplaySeconds);
                }
            }
            yield break;
        }

        if (trigger.pool == null || trigger.pool.lines == null || trigger.pool.lines.Length == 0)
        {
            yield break;
        }

        if (!trigger.playAllLinesInSequence)
        {
            ShowLine(trigger.pool.GetRandomLine(), trigger.triggerId);
            ApplyEvidenceSideEffect(trigger.triggerId, 0);
            yield break;
        }

        for (int i = 0; i < trigger.pool.lines.Length; i++)
        {
            ShowLine(trigger.pool.lines[i], trigger.triggerId);
            ApplyEvidenceSideEffect(trigger.triggerId, i);

            if (i < trigger.pool.lines.Length - 1)
            {
                yield return new WaitForSeconds(lineDisplaySeconds);
            }
        }
    }

    private void ShowLine(string line, string triggerId)
    {
        if (string.IsNullOrEmpty(line))
        {
            return;
        }

        if (notificationUI != null)
        {
            // The display extracts legacy inline speaker labels into history metadata.
            notificationUI.ShowLine(line, null, "Team", triggerId);
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

        // Was "evidence_014_photographed", retargeted to "evidence_014_logged" - see
        // the trigger definition in the scene's STCSManager.triggers list and the
        // dialogue itself (DialoguePool_Evidence014Photographed): line 1 is the
        // Recorder saying "Kitchen knife is entered into the evidence log," which
        // describes MarkLogged, not the photograph. Firing this off "photographed"
        // (now just the FIRST of five steps before Logged, post documentation-flow
        // restructure - see EvidenceStatus's insertion log) made both the timing
        // wrong and lineIndex 1's own MarkLogged call procedurally unreachable at
        // fire time. RecorderTool already fires this exact suffix for real
        // (ReportEvidence(..., "logged")) the moment any item is genuinely logged, so
        // retargeting needed no new call site - just pointing this trigger at a
        // suffix that already exists and already means what the line says.
        //
        // No side effect needed here anymore: PlayerTool.ReportEvidence calls
        // markMethod (MarkLogged) BEFORE firing the STCS trigger, so by the time this
        // runs the real MarkLogged this line narrates has already applied. Calling it
        // again would only ever hit the harmless Duplicate path - removed rather than
        // left as a no-op call.
        //
        // lineIndex 0 used to call MarkSketched here, simulating a teammate doing the
        // sketch immediately after the photograph. Sketched is retired by the
        // documentation-flow restructure with no equivalent step to auto-fire in its
        // place - ChalkMarked, Marked, and PhotographedFinal are all real player
        // actions now, not something a flavor-text line should silently skip past.
        // Removed rather than left calling a method that no longer exists; not
        // replaced with a guess at which new step (if any) a teammate should be
        // simulated performing.
    }
}
