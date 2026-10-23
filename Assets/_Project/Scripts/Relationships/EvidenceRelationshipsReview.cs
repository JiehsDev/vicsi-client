using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

[Serializable] public class RelationshipInterpretationResponse
{
    public string relationshipId, responseId;
    public int responseOrder;
    public double answeredSeconds;
}
[Serializable] public class RelationshipInterpretationChoice { public string id, text; }

/// <summary>Locked relationship interpretations, separate from contextual checkpoints.
/// Reuses their accessible question renderer, without recording checkpoint answers.</summary>
public sealed class EvidenceRelationshipsReview : MonoBehaviour
{
    public static EvidenceRelationshipsReview Instance => RelationshipStateManager.Instance != null
        ? RelationshipStateManager.Instance.GetComponent<EvidenceRelationshipsReview>() : null;
    private readonly List<RelationshipInterpretationResponse> responses = new();
    public IReadOnlyList<RelationshipInterpretationResponse> Responses => Snapshot().AsReadOnly();
    private HypothesisCheckpointUI ui;
    private InputAction close;
    private int openedFrame;
    public bool IsShowing => ui != null && ui.IsShowing;
    public HypothesisCheckpointUI QuestionUI => ui;
    public string ActiveRelationshipId { get; private set; }
    public bool HasResponse(string id) => responses.Any(r => r.relationshipId == id);
    public List<DeductionRelationshipRule> EligibleQuestions()
    {
        var facts = FinalDeductionAssessment.CaptureLive();
        return FinalDeductionAssessment.Rules.relationships.Where(r => FinalDeductionAssessment.RelationshipSupported(r, facts)
            && RelationshipStateManager.Instance?.GetRecord(r.id) != null).ToList();
    }
    public bool IsReadyForConclusion => responses.Count > 0 && EligibleQuestions().All(r => HasResponse(r.id));
    private void Awake()
    {
        close = new InputAction("RelationshipReview_Close", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");
        close.Enable();
    }
    private void Update() { if (IsShowing && Time.frameCount > openedFrame && close.WasPressedThisFrame()) Hide(); }
    private void OnDisable() => Hide();
    private void OnDestroy() { close?.Dispose(); }
    public void Show()
    {
        if (IsShowing || SceneTurnover.IsDeductionBlocked || PlayerUIGate.IsBlocked) return;
        var next = EligibleQuestions().FirstOrDefault(r => !HasResponse(r.id));
        if (next == null)
        {
            NotificationManager.Notify(responses.Count == 0 ? "Develop investigation findings before opening Evidence Relationships Review."
                : "Available relationship responses are recorded. Submit your final conclusion when ready.");
            if (IsReadyForConclusion) EvidenceBoardController.RecordReviewCompletion();
            return;
        }
        if (next.interpretations == null || next.interpretations.Count < 3
            || !next.interpretations.Any(c => c.id == next.correctInterpretationId))
        {
            Debug.LogError("Relationship review content unavailable: " + next.id);
            NotificationManager.Notify("Evidence Relationships Review is temporarily unavailable.");
            return;
        }
        if (ui == null)
        {
            var host = EditableSceneVisuals.Create("Evidence Relationships Review"); host.transform.SetParent(transform, false);
            ui = host.AddComponent<HypothesisCheckpointUI>();
        }
        ui.enabled = true; ActiveRelationshipId = next.id; openedFrame = Time.frameCount;
        ui.ShowRelationshipReview(new ReasoningCheckpointDefinition { id = next.id,
            prompt = next.reviewPrompt,
            responses = next.interpretations.Select(c => new ReasoningResponse { id = c.id, text = c.text }).ToList()
        }, answer => { bool recorded = ConfirmResponse(next.id, answer); ActiveRelationshipId = null; if (recorded) Show(); });
    }
    public void Hide() { if (ui != null) ui.enabled = false; ActiveRelationshipId = null; }
    public bool ConfirmResponse(string relationshipId, string responseId)
    {
        if (SceneTurnover.IsDeductionBlocked || HasResponse(relationshipId)) return false;
        var question = EligibleQuestions().Find(r => r.id == relationshipId);
        if (question == null || !question.interpretations.Any(c => c.id == responseId)) return false;
        var response = new RelationshipInterpretationResponse { relationshipId = relationshipId, responseId = responseId,
            responseOrder = responses.Count + 1, answeredSeconds = SessionLogger.Instance?.ElapsedSeconds ?? 0 };
        responses.Add(response);
        RelationshipStateManager.Instance.RecordInterpretation(relationshipId, responseId == question.correctInterpretationId);
        SessionLogger.Instance?.LogEvent(SessionEventType.RelationshipInterpretationSubmitted, relationshipId,
            new Dictionary<string,string> { {"responseId", responseId}, {"responseOrder", response.responseOrder.ToString()},
                {"reviewVersion", "1"}, {"answeredSeconds", response.answeredSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)} });
        SessionLogger.Instance?.Flush();
        if (IsReadyForConclusion) EvidenceBoardController.RecordReviewCompletion();
        return true;
    }
    public List<RelationshipInterpretationResponse> Snapshot() => responses.Select(r => new RelationshipInterpretationResponse {
        relationshipId=r.relationshipId,responseId=r.responseId,responseOrder=r.responseOrder,answeredSeconds=r.answeredSeconds}).ToList();
}
