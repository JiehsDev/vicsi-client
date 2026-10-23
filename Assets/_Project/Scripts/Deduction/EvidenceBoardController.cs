using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Serialized compatibility adapter. The old canvas/node/line data remains
/// dormant; all player interaction uses the relationship interpretation review.</summary>
public class EvidenceBoardController : MonoBehaviour
{
    public bool IsShowing => EvidenceRelationshipsReview.Instance?.IsShowing == true;
    public static bool HasEverSubmittedTheory { get; private set; }
    public static DeductionScoreResult? LastScoreResult { get; private set; }
    public static event Action OnTheorySubmitted;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { HasEverSubmittedTheory = false; LastScoreResult = null; OnTheorySubmitted = null; }
    internal static void ResetForSession() { HasEverSubmittedTheory = false; LastScoreResult = null; }
    private void Awake() => gameObject.SetActive(false);
    public void Toggle() { if (IsShowing) Hide(); else Show(); }
    public void Show() => EvidenceRelationshipsReview.Instance?.Show();
    public void Hide() => EvidenceRelationshipsReview.Instance?.Hide();
    public void TryConnect(EvidenceNode a, EvidenceNode b) { }
    public bool IsConnected(string a, string b) => false;
    public List<DeductionBoardPair> GetAssessmentConnections() => new();
    public void SubmitTheory()
    {
        if (SceneTurnover.IsDeductionBlocked) return;
        if (EvidenceRelationshipsReview.Instance?.IsReadyForConclusion != true) { Show(); return; }
        RecordReviewCompletion(); Hide();
    }
    public static void RecordReviewCompletion()
    {
        if (SceneTurnover.IsDeductionBlocked || EvidenceRelationshipsReview.Instance?.IsReadyForConclusion != true) return;
        if (!HasEverSubmittedTheory) { HasEverSubmittedTheory = true; OnTheorySubmitted?.Invoke(); }
        var score = FinalDeductionAssessment.Score(FinalDeductionAssessment.CaptureLive(), false, FinalDeductionAssessment.Rules);
        LastScoreResult = new DeductionScoreResult {requiredLinksScore=score.deductionRelationships,total=score.deductionRelationships};
    }
}
