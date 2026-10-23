using System.Linq;

/// <summary>Answer earned review questions through the same locked-response API as the UI.</summary>
public static class RelationshipReviewTestUtil
{
    // Full-case fixtures exercise earned state/ending paths. Presentation and XR
    // timing are independently covered by ReasoningCheckpointTest.
    public static void BeginCaseFixture()
    {
        ReasoningCheckpointManager.Instance.enabled = false;
        HypothesisCheckpointManager.Instance.enabled = false;
        ProceduralTrainingGuidance.Instance.enabled = false;
        var ui = UnityEngine.Object.FindFirstObjectByType<HypothesisCheckpointUI>();
        if (ui != null && ui.IsShowing) { ui.enabled = false; ui.enabled = true; }
        STCSNotificationUI.Instance?.HideImmediately();
        NotificationManager.Instance?.UI.ClearTooltip();
    }
    public static void RecordEarnedReasoning()
    {
        var manager = ReasoningCheckpointManager.Instance;
        manager.Evaluate();
        foreach (var question in manager.Definitions.checkpoints)
            if (manager.IsEligible(question.id) && !manager.IsAnswered(question.id))
                manager.RecordResponse(question.id, question.responses.OrderByDescending(c => c.weight).First().id);
        STCSNotificationUI.Instance?.HideImmediately();
        NotificationManager.Instance?.UI.ClearTooltip();
    }
    // Called with the real conclusion picker holding input, immediately before its
    // final selection, so the frozen assessment retains genuine system readiness.
    public static void EnableAssessmentCapture()
    {
        RecordEarnedReasoning();
        ReasoningCheckpointManager.Instance.enabled = true;
        HypothesisCheckpointManager.Instance.enabled = true;
    }
    public static void ConfirmAvailable()
    {
        var review = EvidenceRelationshipsReview.Instance;
        foreach (var question in review.EligibleQuestions().ToArray())
            if (!review.HasResponse(question.id)) review.ConfirmResponse(question.id, question.correctInterpretationId);
    }
}
