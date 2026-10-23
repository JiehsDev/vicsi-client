// Assets/_Project/Scripts/UI/EvidenceNotifier.cs
using UnityEngine;

/// <summary>
/// Bridges EvidenceStateManager's existing OnEvidenceStatusChanged event to
/// NotificationManager, so every evidence status change already flowing through
/// this project - photographed, chalk marked, logged, collected, whichever tool
/// caused it - surfaces a toast without any of those tools (PhotographTool,
/// ChalkMarkTool, EvidenceBagTool, ...) needing to know notifications exist.
/// Sample wiring for the reusable NotificationManager/NotificationUI pair; drop
/// this on any GameObject in a scene that has both an EvidenceStateManager and a
/// NotificationManager to enable it.
///
/// This is the presentation layer and only the presentation layer. Nothing here
/// affects the state machine or the session log: a status that goes unannounced was
/// still recorded, still gates the next step, and still reaches the scorer.
/// </summary>
public class EvidenceNotifier : MonoBehaviour
{
    /// <summary>
    /// Whether reaching EvidenceStatus.Found announces itself to the player.
    ///
    /// PER-SCENARIO POLICY, NOT A GLOBAL RULE. It is a serialized field precisely so
    /// that it cannot become one: a hardcoded "Found is always silent" would break
    /// Tutorial_ToolTest, which is a tool testbed where naming the item you just walked
    /// up to is a legitimate teaching aid rather than a leak.
    ///
    /// Defaults TRUE so that every existing scene keeps the behaviour it already had.
    /// CSI_Environment sets it FALSE, and that scene is the reason this field exists:
    /// it is an assessment-grade, no-hints scenario, and a toast reading "EVD-014
    /// found" fires on real evidence while non-evidence objects produce nothing. A
    /// player can therefore map the entire evidence roster by walking the room and
    /// reading toasts - without searching, without judging anything, and without
    /// taking a single deliberate action.
    ///
    /// That is the same leak class as the marking cue (see the DELIBERATELY IDENTICAL
    /// note in EvidenceTentTool.PlaceTent and DO NOT ADD IT in FeedbackDirector), one
    /// level earlier in the sequence and cheaper still to exploit, because marking at
    /// least required the player to commit to a claim.
    ///
    /// Deliberately a bool rather than an enum. A generic, non-naming presence cue
    /// ("something nearby") is a plausible third option for some future scenario, but
    /// it is NOT what CSI_Environment wants - directional information the player did
    /// not generate themselves is still a hint - so there is no Generic case here to
    /// leave unused and rot.
    /// </summary>
    [Tooltip("Announce evidence discovery (Found) to the player by name. Leave OFF for assessment scenarios: only real evidence raises this toast, so a player can map the whole roster by walking the room and reading it — without searching or judging anything. Same leak class as the marking cue, one step earlier. ON is appropriate for tool testbeds like Tutorial_ToolTest.")]
    [SerializeField] private bool announceDiscovery = true;

    private void OnEnable()
    {
        EvidenceStateManager.OnEvidenceStatusChanged += HandleStatusChanged;
    }

    private void OnDisable()
    {
        EvidenceStateManager.OnEvidenceStatusChanged -= HandleStatusChanged;
    }

    private void HandleStatusChanged(string evidenceId, EvidenceStatus status)
    {
        // Presentation-only, and only for this one status. MarkFound has already run:
        // the record advanced, OnEvidenceStatusChanged fired, SessionLogger wrote its
        // EvidenceStatusChanged entry, and the procedural gate now allows Marked. All
        // that is suppressed here is the sentence on screen.
        //
        // Suppressed ENTIRELY when off - no substitute cue, not even a nameless one.
        // Found is a passive backend signal with no required player-facing
        // consequence, unlike marking or a blocked gate, which the player has to be
        // able to read. Replacing the toast with "something nearby" would still be
        // telling them where to look.
        if (status == EvidenceStatus.Found && !announceDiscovery)
        {
            return;
        }

        // Player-facing name only - internal systems (session log, assessment log,
        // requirement evaluation, relationship data) all keep using evidenceId
        // directly; this substitution never leaves this method.
        string name = EvidenceStateManager.Instance != null ? EvidenceStateManager.Instance.GetDisplayName(evidenceId) : evidenceId;
        bool plural = LooksPlural(name);
        string have = plural ? "have" : "has";
        string is_ = plural ? "are" : "is";

        string sentence = status switch
        {
            EvidenceStatus.Found => GameDialogue.Text("evidence-status-discovered", name, have),
            // Marked is deliberately absent from this list. A toast here would read
            // "{name} marked" when the tent lands on registered evidence and show
            // NOTHING when it lands on anything else - which tells the player, at the
            // instant they place it, whether the thing they just identified was real.
            // EvidenceTentTool raises its own toast instead, worded identically on both
            // branches and naming no evidence id. See the note there before changing this.
            EvidenceStatus.PhotographedRaw => GameDialogue.Text("evidence-status-photographed", name, have),
            EvidenceStatus.ChalkMarked => GameDialogue.Text("evidence-status-outlined", name, have),
            EvidenceStatus.PhotographedFinal => GameDialogue.Text("evidence-status-final-photographed", name, have),
            EvidenceStatus.Logged => GameDialogue.Text("evidence-status-recorded", name, have),
            // Not simply "ready for collection" for every item: reaching this status
            // only means Logged has happened (see EvidenceStateManager.CheckReadyForCollection) -
            // it says nothing about whether the NEXT real action is actually a bag
            // insertion. "ready to be swabbed" instead for an item whose own collection
            // method IS the swab container (EvidenceDefinition.collectedBySwab, e.g.
            // EVD-015 - there is no bagging step for it at all). ReadyForCollectionClause
            // also still checks requiresSwabbing/swabbingDone for a bag-collected item
            // with an unmet swab precondition (e.g. the knife) as defence in depth, but
            // that branch is now unreachable in practice: requiresSwabbing now gates
            // Logged itself (see EvidenceStateManager.IsBlockedBySwabbing), so
            // swabbingDone is always already true by the time ReadyForCollection can
            // ever fire.
            EvidenceStatus.ReadyForCollection => GameDialogue.Text(ReadyForCollectionClause(evidenceId), name, is_),
            EvidenceStatus.Collected => GameDialogue.Text("evidence-status-collected", name, have),
            EvidenceStatus.Sealed => GameDialogue.Text("evidence-status-sealed", name, have),
            EvidenceStatus.Processed => GameDialogue.Text("evidence-status-processed", name, have),
            _ => null,
        };

        if (sentence == null)
        {
            return;
        }

        NotificationManager.Notify(sentence, DialogueKind.Confirmation);
    }

    /// <summary>
    /// "ready to be swabbed." if this item's own collection method is the swab
    /// container (collectedBySwab) or it's bag-collected but still has an unmet
    /// requiresSwabbing precondition; "ready for collection." otherwise. See the call
    /// site's comment for why this can't be a flat string for every item.
    /// </summary>
    private static string ReadyForCollectionClause(string evidenceId)
    {
        var record = EvidenceStateManager.Instance?.GetRecord(evidenceId);
        if (record?.definition == null)
        {
            return "evidence-status-ready-for-collection";
        }

        // Only an item still waiting for its swab is "ready to be swabbed" - a sample taken
        // and sealed on scene before the record (collectedBySwab + requiresSwabbing) is
        // already done.
        bool swabNext = (record.definition.collectedBySwab && !record.definition.requiresSwabbing)
            || (record.definition.requiresSwabbing && !record.swabbingDone);

        return swabNext ? "evidence-status-ready-to-swab" : "evidence-status-ready-for-collection";
    }

    /// <summary>
    /// Crude, generic (never evidence-id-specific) plurality guess from the resolved
    /// display name's last word, so "Disposable gloves have been..." reads correctly
    /// alongside "Kitchen knife has been..." without an authored per-item grammar
    /// flag. Deliberately simple: ends-in-s-but-not-'s on the last word. Wrong for a
    /// genuinely irregular name (e.g. one ending "...glasses" would be fine, but
    /// something like "...premises" would misfire) - acceptable given the actual
    /// authored roster has exactly one plural display name (Disposable gloves) and
    /// this is a minor grammar nicety, never gameplay-affecting.
    /// </summary>
    private static bool LooksPlural(string displayName)
    {
        if (string.IsNullOrEmpty(displayName))
        {
            return false;
        }

        string lastWord = displayName.TrimEnd().Split(' ')[^1];
        return lastWord.EndsWith("s", System.StringComparison.OrdinalIgnoreCase)
            && !lastWord.EndsWith("'s", System.StringComparison.OrdinalIgnoreCase);
    }
}
