// Assets/_Project/Scripts/Sketch/CrimeSceneSketchManager.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public enum SketchState
{
    NotStarted = 0,
    InProgress = 1,
    ReadyForSubmission = 2,
    Submitted = 3
}

public enum SketchPlacementResult
{
    Placed,
    Incorrect,
    AlreadyPlaced,
    NotAvailable,
    ReadOnly,
    UnknownTarget
}

/// <summary>
/// One recorded distance offered to the student as a draggable card. Identity is
/// (requirementId, referenceId) - CardId - never the displayed number, which two cards
/// may share.
/// </summary>
public readonly struct SketchMeasurementCard
{
    public readonly string evidenceId;
    public readonly string requirementId;
    public readonly string referenceId;
    public readonly float value;

    public SketchMeasurementCard(string evidenceId, string requirementId, string referenceId, float value)
    {
        this.evidenceId = evidenceId;
        this.requirementId = requirementId;
        this.referenceId = referenceId;
        this.value = value;
    }

    public string CardId => MakeCardId(requirementId, referenceId);

    /// <summary>Same precision as the tape measure's own readout ("0.00 m").</summary>
    public string DisplayValue => FormatDistance(value);

    public static string MakeCardId(string requirementId, string referenceId) => requirementId + "|" + referenceId;

    public static string FormatDistance(float metres) => $"{metres:0.00} m";
}

/// <summary>One rejected drop, kept for a later Sketch assessment. Not scored.</summary>
public class SketchIncorrectAttempt
{
    public bool isMeasurement;
    public string evidenceId;
    public string itemId;          // measurement: card id; evidence: evidence id
    public string attemptedTargetId; // anchor or slot id; "none" = open board
    public int order;
    public float sessionTimeSeconds;
}

/// <summary>
/// Scene-level Crime Scene Sketch state - performed once per investigation, deliberately
/// NOT an EvidenceStatus and never touching the evidence lifecycle. Tracks which evidence
/// elements and recorded-distance cards are available, what is placed where, rejected
/// drops, and submission. CrimeSceneSketchUI is only a view over this.
///
/// All state is per-instance and rebuilt when the scene loads, which is how a new
/// investigation starts in this project (CompletionScreenUI / UIManager reload the
/// scene); ResetSession() clears it in place for tests and any future in-scene restart.
///
/// EVIDENCE-LEAK RULE: an element unlocks only once its item is at or past
/// PhotographedRaw - the same rule as the Evidence Procedure Map
/// (EvidenceProcedureUI.IsEligible). Found is reached by proximity alone, so unlocking
/// on Found would list which nearby objects are evidence. Measurement cards follow their
/// item's unlock too. Do not loosen either.
/// </summary>
public class CrimeSceneSketchManager : MonoBehaviour
{
    public static CrimeSceneSketchManager Instance { get; private set; }

    /// <summary>Fired after any change to sketch state (placement, rejection, submission, reset).</summary>
    public static event Action OnSketchChanged;

    [SerializeField] private CrimeSceneSketchDefinition definition;

    public CrimeSceneSketchDefinition Definition => definition;
    public string SketchId => definition != null ? definition.sketchId : string.Empty;

    private readonly HashSet<string> placedEvidence = new();
    private readonly List<string> placementOrder = new();
    private readonly Dictionary<string, string> filledSlots = new(); // slotId -> cardId
    private readonly List<SketchIncorrectAttempt> incorrectAttempts = new();
    private bool submitted;
    private int actionOrder;
    private bool announcedSketch;
    private readonly HashSet<string> announcedReady = new();

    public int OpenCount { get; private set; }

    public IReadOnlyList<SketchIncorrectAttempt> IncorrectAttempts => incorrectAttempts;
    public IReadOnlyList<string> PlacedEvidenceInOrder => placementOrder;

    public int IncorrectEvidenceAttempts
    {
        get
        {
            int n = 0;
            foreach (var a in incorrectAttempts) if (!a.isMeasurement) n++;
            return n;
        }
    }

    public int IncorrectMeasurementAttempts => incorrectAttempts.Count - IncorrectEvidenceAttempts;

    private void Awake()
    {
        Instance = this;
        ResetState();
    }

    private void OnEnable()
    {
        MeasurementStateManager.OnMeasurementProgress += HandleMeasurementProgress;
        EvidenceStateManager.OnEvidenceStatusChanged += HandleEvidenceStatusChanged;
        AttachRecordedMeasurements();
    }

    private void OnDisable()
    {
        MeasurementStateManager.OnMeasurementProgress -= HandleMeasurementProgress;
        EvidenceStateManager.OnEvidenceStatusChanged -= HandleEvidenceStatusChanged;
    }

    private void HandleEvidenceStatusChanged(string id, EvidenceStatus status)
    {
        if (announcedSketch || definition == null || definition.FindElement(id) == null || status != EvidenceStatus.PhotographedRaw) return;
        announcedSketch = true;
        GameDialogue.Say("sketch-tool-available");
    }

    private void HandleMeasurementProgress(string requirementId)
    {
        AttachRecordedMeasurements();
        if (definition == null || submitted) return;
        foreach (var element in definition.evidenceElements)
        {
            if (element == null || IsEvidencePlaced(element.evidenceId) || !CanSketchEvidence(element.evidenceId) || !announcedReady.Add(element.evidenceId)) continue;
            GameDialogue.Say("sketch-item-ready", EvidenceDisplay.Label(element.evidenceId));
        }
        Changed();
    }

    public bool CanSketchEvidence(string evidenceId) => IsEvidenceUnlocked(evidenceId)
        && MeasurementStateManager.Instance != null
        && MeasurementStateManager.Instance.GetEvidenceMeasurementStatus(evidenceId).IsComplete;

    /// <summary>Per-item processing gate; final submission remains a separate closeout step.</summary>
    public static string GetProcessingBlockReason(string evidenceId)
    {
        var sketch = Instance;
        if (sketch == null || sketch.definition == null || sketch.definition.FindElement(evidenceId) == null) return null;
        if (sketch.IsEvidencePlaced(evidenceId)) return null;
        return sketch.CanSketchEvidence(evidenceId)
            ? GameDialogue.Text("sketch-before-processing", EvidenceDisplay.Label(evidenceId))
            : GameDialogue.Text("sketch-measure-first", EvidenceDisplay.Label(evidenceId));
    }

    /// <summary>Attach actual recorded distances to correctly placed evidence, including later measurements.</summary>
    private void AttachRecordedMeasurements()
    {
        if (submitted || definition == null) return;
        var cards = GetRecordedCards();
        foreach (var slot in definition.measurementSlots)
        {
            if (slot == null || !IsSlotVisible(slot.slotId) || IsSlotFilled(slot.slotId)) continue;
            foreach (var card in cards)
            {
                if (card.evidenceId != slot.evidenceId || card.requirementId != slot.requirementId || card.referenceId != slot.referenceId) continue;
                PlaceMeasurement(card.CardId, slot.slotId);
                break;
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // --- State -------------------------------------------------------------------------

    public SketchState State
    {
        get
        {
            if (submitted) return SketchState.Submitted;
            if (AllRequiredPlaced()) return SketchState.ReadyForSubmission;
            return placedEvidence.Count > 0 || filledSlots.Count > 0 ? SketchState.InProgress : SketchState.NotStarted;
        }
    }

    public bool IsSubmitted => submitted;
    public static string CompletionBlockReason => Instance != null && Instance.Definition != null && !Instance.IsSubmitted
        ? GameDialogue.Text("sketch-submit-before-completion") : null;
    public bool IsReadyForSubmission => State == SketchState.ReadyForSubmission;

    private bool AllRequiredPlaced()
    {
        if (definition == null) return false;
        bool anyRequired = false;
        foreach (var e in definition.evidenceElements)
        {
            if (e == null || !e.required) continue;
            anyRequired = true;
            if (!placedEvidence.Contains(e.evidenceId)) return false;
        }
        foreach (var s in definition.measurementSlots)
        {
            if (s == null || !s.required) continue;
            anyRequired = true;
            if (!filledSlots.ContainsKey(s.slotId)) return false;
        }
        return anyRequired && OrientationPresent;
    }

    /// <summary>Required orientation information (north arrow) is present in the authored template.</summary>
    public bool OrientationPresent
    {
        get
        {
            if (definition == null || !definition.requireNorthArrow) return true;
            var template = Template;
            return template != null && template.HasFixedElement(SketchFixedElementKind.NorthArrow);
        }
    }

    /// <summary>The scene's template for this definition (the board the UI adopts).</summary>
    public CrimeSceneSketchTemplate Template
    {
        get
        {
            if (cachedTemplate != null) return cachedTemplate;
            foreach (var t in FindObjectsByType<CrimeSceneSketchTemplate>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.definition == definition)
                {
                    cachedTemplate = t;
                    break;
                }
            }
            return cachedTemplate;
        }
    }

    private CrimeSceneSketchTemplate cachedTemplate;

    // --- Availability -------------------------------------------------------------------

    /// <summary>Discovered under the evidence-leak rule above (at or past PhotographedRaw).</summary>
    public static bool IsEvidenceUnlocked(string evidenceId)
    {
        var esm = EvidenceStateManager.Instance;
        return esm != null && !string.IsNullOrEmpty(evidenceId) && esm.GetRecord(evidenceId) != null
            && esm.IsAtOrPastStatus(evidenceId, EvidenceStatus.PhotographedRaw);
    }

    public bool IsEvidencePlaced(string evidenceId) => placedEvidence.Contains(evidenceId);

    /// <summary>Unlocked elements not yet placed, in definition order (the "Available Elements" palette).</summary>
    public List<SketchEvidenceElementDefinition> GetAvailableElements()
    {
        var list = new List<SketchEvidenceElementDefinition>();
        if (definition == null) return list;
        foreach (var e in definition.evidenceElements)
        {
            if (e != null && !placedEvidence.Contains(e.evidenceId) && IsEvidenceUnlocked(e.evidenceId))
            {
                list.Add(e);
            }
        }
        return list;
    }

    /// <summary>
    /// Every distance the student has actually recorded this session for an unlocked item in
    /// this sketch, as the tape measure recorded it (never recalculated), in definition order.
    /// </summary>
    public List<SketchMeasurementCard> GetRecordedCards()
    {
        var list = new List<SketchMeasurementCard>();
        var msm = MeasurementStateManager.Instance;
        if (definition == null || msm == null) return list;
        foreach (var element in definition.evidenceElements)
        {
            if (element == null || !IsEvidenceUnlocked(element.evidenceId)) continue;
            foreach (var record in msm.AllRecords)
            {
                var r = record.requirement;
                if (r == null || r.targetEvidenceId != element.evidenceId) continue;
                if (record.hasA) list.Add(new SketchMeasurementCard(element.evidenceId, r.requirementId, r.referenceAId, record.measuredA));
                if (record.hasB) list.Add(new SketchMeasurementCard(element.evidenceId, r.requirementId, r.referenceBId, record.measuredB));
            }
        }
        return list;
    }

    /// <summary>Recorded cards not yet filed in a slot (the "Available Measurements" palette).</summary>
    public List<SketchMeasurementCard> GetAvailableMeasurementCards()
    {
        var list = GetRecordedCards();
        list.RemoveAll(c => filledSlots.ContainsValue(c.CardId));
        return list;
    }

    public bool TryGetCard(string cardId, out SketchMeasurementCard card)
    {
        foreach (var c in GetRecordedCards())
        {
            if (c.CardId == cardId)
            {
                card = c;
                return true;
            }
        }
        card = default;
        return false;
    }

    public bool IsSlotFilled(string slotId) => filledSlots.ContainsKey(slotId);

    /// <summary>The card filed in slotId, if any.</summary>
    public bool TryGetSlotCard(string slotId, out SketchMeasurementCard card)
    {
        card = default;
        return filledSlots.TryGetValue(slotId, out var cardId) && TryGetCard(cardId, out card);
    }

    /// <summary>A slot is a drop target only once its evidence element is placed - before that it isn't shown at all.</summary>
    public bool IsSlotVisible(string slotId)
    {
        var slot = definition != null ? definition.FindSlot(slotId) : null;
        return slot != null && placedEvidence.Contains(slot.evidenceId);
    }

    /// <summary>The marker the student already sees on the item in the world ("A", "1"), or empty before it is marked.</summary>
    public static string MarkerLabel(string evidenceId)
    {
        var record = EvidenceStateManager.Instance != null ? EvidenceStateManager.Instance.GetRecord(evidenceId) : null;
        if (record == null) return string.Empty;
        if (!string.IsNullOrEmpty(record.tentLetter)) return record.tentLetter;
        return record.tentNumber.HasValue ? record.tentNumber.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
    }

    // --- Actions ------------------------------------------------------------------------

    private readonly HashSet<string> hintedEvidence = new();

    /// <summary>Wrong drops of this one evidence element so far.</summary>
    public int IncorrectEvidenceAttemptsFor(string evidenceId)
    {
        int n = 0;
        foreach (var a in incorrectAttempts)
        {
            if (!a.isMeasurement && a.evidenceId == evidenceId) n++;
        }
        return n;
    }

    /// <summary>Elements whose placement area has been highlighted as a hint.</summary>
    public int HintsShown => hintedEvidence.Count;

    public bool WasHinted(string evidenceId) => hintedEvidence.Contains(evidenceId);

    /// <summary>
    /// The UI highlighted evidenceId's placement area after repeated wrong drops. Logged once
    /// per element (SketchPlacementHintShown) so the assessment can tell an assisted placement
    /// from an unaided one; changes nothing else.
    /// </summary>
    public void NoteHintShown(string evidenceId)
    {
        if (submitted || string.IsNullOrEmpty(evidenceId) || !hintedEvidence.Add(evidenceId)) return;
        Log(SessionEventType.SketchPlacementHintShown, evidenceId, actionOrder, new Dictionary<string, string>
        {
            { "evidenceId", evidenceId },
            { "incorrectAttemptsBeforeHint", IncorrectEvidenceAttemptsFor(evidenceId).ToString(CultureInfo.InvariantCulture) }
        });
    }

    /// <summary>
    /// The student dropped evidenceId's element on the board, landing on attemptedAnchorId
    /// (null/empty = open board). Correct anchor: placed and locked. Anything else: rejected
    /// and logged once (SketchPlacementIncorrect); the UI keeps the element held for retry.
    /// </summary>
    public SketchPlacementResult PlaceEvidence(string evidenceId, string attemptedAnchorId)
    {
        if (submitted) return SketchPlacementResult.ReadOnly;
        var element = definition != null ? definition.FindElement(evidenceId) : null;
        if (element == null || !IsEvidenceUnlocked(evidenceId)) return SketchPlacementResult.NotAvailable;
        if (placedEvidence.Contains(evidenceId)) return SketchPlacementResult.AlreadyPlaced;
        if (ProcedureInterlock.BlocksUnrelatedActions) return SketchPlacementResult.NotAvailable;
        if (!CanSketchEvidence(evidenceId)) return SketchPlacementResult.NotAvailable;

        int order = ++actionOrder;
        string attempted = string.IsNullOrEmpty(attemptedAnchorId) ? "none" : attemptedAnchorId;
        if (attempted != element.correctAnchorId)
        {
            incorrectAttempts.Add(new SketchIncorrectAttempt
            {
                isMeasurement = false,
                evidenceId = evidenceId,
                itemId = evidenceId,
                attemptedTargetId = attempted,
                order = order,
                sessionTimeSeconds = SessionTime()
            });
            Log(SessionEventType.SketchPlacementIncorrect, evidenceId, order, new Dictionary<string, string>
            {
                { "evidenceId", evidenceId },
                { "attemptedAnchorId", attempted }
            });
            Changed();
            return SketchPlacementResult.Incorrect;
        }

        placedEvidence.Add(evidenceId);
        placementOrder.Add(evidenceId);
        Log(SessionEventType.SketchEvidencePlaced, evidenceId, order, new Dictionary<string, string>
        {
            { "evidenceId", evidenceId },
            { "anchorId", element.correctAnchorId }
        });
        AttachRecordedMeasurements();
        Changed();
        return SketchPlacementResult.Placed;
    }

    /// <summary>
    /// The student dropped a measurement card on slotId. Correct = the slot takes exactly this
    /// card's (requirementId, referenceId) - identity, not the number shown. Wrong visible slot:
    /// rejected and logged (SketchMeasurementIncorrect). A slot that isn't shown yet is not a
    /// target (UnknownTarget, not logged).
    /// </summary>
    public SketchPlacementResult PlaceMeasurement(string cardId, string slotId)
    {
        if (submitted) return SketchPlacementResult.ReadOnly;
        if (!TryGetCard(cardId, out var card)) return SketchPlacementResult.NotAvailable;
        if (filledSlots.ContainsValue(cardId)) return SketchPlacementResult.AlreadyPlaced;
        var slot = definition.FindSlot(slotId);
        if (slot == null || !IsSlotVisible(slotId)) return SketchPlacementResult.UnknownTarget;
        if (filledSlots.ContainsKey(slotId)) return SketchPlacementResult.AlreadyPlaced;

        int order = ++actionOrder;
        if (slot.requirementId != card.requirementId || slot.referenceId != card.referenceId)
        {
            incorrectAttempts.Add(new SketchIncorrectAttempt
            {
                isMeasurement = true,
                evidenceId = card.evidenceId,
                itemId = cardId,
                attemptedTargetId = slotId,
                order = order,
                sessionTimeSeconds = SessionTime()
            });
            Log(SessionEventType.SketchMeasurementIncorrect, card.evidenceId, order, new Dictionary<string, string>
            {
                { "evidenceId", card.evidenceId },
                { "referenceId", card.referenceId },
                { "requirementId", card.requirementId },
                { "attemptedSlotId", slotId }
            });
            Changed();
            return SketchPlacementResult.Incorrect;
        }

        filledSlots[slotId] = cardId;
        Log(SessionEventType.SketchMeasurementPlaced, card.evidenceId, order, new Dictionary<string, string>
        {
            { "evidenceId", card.evidenceId },
            { "referenceId", card.referenceId },
            { "requirementId", card.requirementId },
            { "slotId", slotId },
            { "measuredDistance", card.value.ToString("0.000", CultureInfo.InvariantCulture) }
        });
        Changed();
        return SketchPlacementResult.Placed;
    }

    /// <summary>Submits once, only when every required element and measurement is placed. Read-only afterwards.</summary>
    public bool Submit()
    {
        if (submitted || !IsReadyForSubmission) return false;
        submitted = true;
        InteractionSfx.Play(InteractionSound.Writing,Camera.main!=null ? Camera.main.transform.position : transform.position);
        Log(SessionEventType.SketchSubmitted, SketchId, -1, new Dictionary<string, string>
        {
            { "evidencePlaced", placedEvidence.Count.ToString(CultureInfo.InvariantCulture) },
            { "measurementsPlaced", filledSlots.Count.ToString(CultureInfo.InvariantCulture) },
            { "incorrectEvidenceAttempts", IncorrectEvidenceAttempts.ToString(CultureInfo.InvariantCulture) },
            { "incorrectMeasurementAttempts", IncorrectMeasurementAttempts.ToString(CultureInfo.InvariantCulture) },
            { "placementHintsShown", hintedEvidence.Count.ToString(CultureInfo.InvariantCulture) }
        });
        Changed();
        return true;
    }

    /// <summary>Called by the UI each time the board is opened.</summary>
    public void NotifyOpened()
    {
        OpenCount++;
        Log(SessionEventType.SketchOpened, SketchId, -1, new Dictionary<string, string>
        {
            { "state", State.ToString() },
            { "openCount", OpenCount.ToString(CultureInfo.InvariantCulture) }
        });
    }

    /// <summary>Clears every placement, rejection and the submission - a fresh investigation.</summary>
    public void ResetSession()
    {
        ResetState();
        Changed();
    }

    private void ResetState()
    {
        placedEvidence.Clear();
        placementOrder.Clear();
        filledSlots.Clear();
        incorrectAttempts.Clear();
        submitted = false;
        actionOrder = 0;
        OpenCount = 0;
        cachedTemplate = null;
        announcedSketch = false;
        announcedReady.Clear();
    }

    private static void Changed() => OnSketchChanged?.Invoke();

    private static float SessionTime() => SessionLogger.Instance != null ? SessionLogger.Instance.ElapsedSeconds : Time.time;

    private void Log(SessionEventType type, string targetId, int order, Dictionary<string, string> payload)
    {
        if (SessionLogger.Instance == null) return;
        payload["sketchId"] = SketchId;
        payload["sessionTimeSeconds"] = SessionTime().ToString("0.000", CultureInfo.InvariantCulture);
        if (order > 0) payload["order"] = order.ToString(CultureInfo.InvariantCulture);
        SessionLogger.Instance.LogEvent(type, targetId, payload);
    }
}
