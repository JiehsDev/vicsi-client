// Assets/_Project/Scripts/Narrative/NarrativeEvent.cs
using System;
using UnityEngine;

/// <summary>
/// How a NarrativeEvent starts playing. Mirrors HypothesisTriggerType's own two-mode
/// shape (CountThreshold vs SubmissionInitiated) - a reactive, requirement-gated mode
/// and a deliberate, unconditional call-site mode - applied here for the same reason.
/// </summary>
public enum NarrativeTriggerMode
{
    /// <summary>Reactively fires the moment `trigger` becomes satisfied - re-checked on every relevant EvidenceStateManager event, same idiom as RelationshipStateManager.RefreshUnlocks.</summary>
    Automatic = 0,

    /// <summary>
    /// Fires only when something calls NarrativeEventManager.Instance.Fire(eventId) -
    /// `trigger` is NOT checked in this mode. Exists so an existing imperative call
    /// site (the same shape as STCSManager.Fire, which every current PlayerTool.
    /// ReportEvidence call already uses) can be pointed at a NarrativeEvent instead of
    /// an STCSTrigger without gaining an extra, unwanted gate the call site didn't ask
    /// for - the call itself already IS the condition.
    /// </summary>
    Manual = 1
}

/// <summary>
/// One narrative beat: a prompt/dialogue moment (reusing DialogueLinePool, the same
/// asset type STCSTrigger already uses - no new dialogue-line data model), gated either
/// reactively (see NarrativeTriggerMode) or fired on demand, with priority-ordered
/// queuing when several become eligible at once. Generalizes STCSTrigger's shape with
/// requirement-tree gating, priority, repeatability, delay, and a completion signal other
/// systems can unlock against (RequirementType.NarrativeEventCompleted) - see
/// NarrativeEventManager's own class comment for why this coexists with STCSManager
/// rather than replacing it.
///
/// A plain [Serializable] class held in a NarrativeEventSet's list, not its own asset -
/// same "no meaning outside the set it belongs to" reasoning as HypothesisCheckpoint.
///
/// Deliberately carries no CSI_Environment-specific data in this class - every field is
/// generic. Whatever gets typed into an actual authored entry (dialogue lines, a
/// specific evidenceId in `trigger`, ...) is content, not something this file hardcodes.
/// </summary>
[Serializable]
public class NarrativeEvent
{
    [Tooltip("Stable, author-set identifier (e.g. \"narrative-brother-false-summit\"). Used as the logged targetId and as what a RequirementType.NarrativeEventCompleted leaf elsewhere references - must not be derived from list position.")]
    public string eventId;

    [Tooltip("Author-facing label only, shown in editor tooling - never player-facing.")]
    public string title;

    [Tooltip("Optional. Leave blank if the speaker is already baked into the dialogue line text (\"Dispatch: ...\") - the convention every existing DialoguePool asset already uses. Only fill this in if a future display surface wants the speaker as a separate field.")]
    public string speaker;

    [Tooltip("Lines to play, as ViCSIDialogueCatalog ids (text and timing are edited in the catalog). In order when playAllLinesInSequence, else one at random.")]
    public string[] lineIds;

    [Tooltip("Legacy: used only when lineIds is empty. Content now lives in ViCSIDialogueCatalog.")]
    public DialogueLinePool pool;

    [Tooltip("If true, every line in the pool plays in order; if false, one random line plays. Same field STCSTrigger already has.")]
    public bool playAllLinesInSequence;

    [Tooltip("Automatic: reactively gated by `trigger`, re-checked whenever relevant evidence state changes. Manual: fires only via NarrativeEventManager.Fire(eventId); `trigger` is ignored.")]
    public NarrativeTriggerMode triggerMode = NarrativeTriggerMode.Automatic;

    [Tooltip("Automatic mode only: must be satisfied before this event becomes eligible to play. Empty ALL group (the default) means eligible immediately once the scene starts.")]
    public RequirementNode trigger = new();

    [Tooltip("When multiple events are eligible/queued at the same moment, higher priority plays first. Ties keep queue (first-eligible-first-shown) order.")]
    public int priority;

    [Tooltip("If false (default), this event plays at most once per session - matches STCSTrigger.firedOnce's existing default behavior. If true, it may fire again each time it becomes eligible again (Automatic) or is Fire()'d again (Manual).")]
    public bool repeatable;

    [Tooltip("Seconds to wait after this event is queued before it actually plays.")]
    [Min(0f)]
    public float delaySeconds;

    [Tooltip("Optional forward-reference by id to an objective this event should surface as the natural next step. Nothing consumes this yet - the investigation-objective system (a later sub-project) will look it up by this id.")]
    public string followUpObjectiveId;

    [Tooltip("Optional voice-over clip, played once via an AudioSource when this event plays. Leave empty for text-only (the current default for every STCS line in this project).")]
    public AudioClip voiceOverClip;
}
