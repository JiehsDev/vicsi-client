using System;
using System.Collections.Generic;
using UnityEngine;

public enum ReasoningTrigger { Form1Received, InitialSceneObserved, FindingConfirmed, VendorStatementDiscovered }

[Serializable]
public class ReasoningResponse
{
    public string id;
    [TextArea] public string text;
    [Range(1, 4)] public int weight;
}

[Serializable]
public class ReasoningCheckpointDefinition
{
    public string id;
    public ReasoningTrigger trigger;
    public string findingId;
    public string integratedHypothesisId;
    public string category;
    [TextArea] public string prompt;
    public string contextCatalogId;
    public List<ReasoningResponse> responses = new();
    // Catalog explanations withheld until this snapshot is recorded.
    public List<string> deferredDialogueIds = new();
    public List<string> deferredTrainingIds = new();
    public ReasoningResponse FindResponse(string id) => responses.Find(r => r.id == id);
}

[CreateAssetMenu(menuName = "VR-CSI/Contextual Reasoning Checkpoints")]
public class ReasoningCheckpointSet : ScriptableObject
{
    public string contentVersion = "1.0.0";
    public List<ReasoningCheckpointDefinition> checkpoints = new();
    public bool IsValid(out string problem)
    {
        var ids = new HashSet<string>();
        var responses = new HashSet<string>();
        foreach (var c in checkpoints)
        {
            if (c == null || string.IsNullOrWhiteSpace(c.id) || !ids.Add(c.id)
                || string.IsNullOrWhiteSpace(c.prompt) || string.IsNullOrWhiteSpace(c.category)
                || c.responses == null || c.responses.Count != 4)
            { problem = "Missing/duplicate checkpoint metadata or response count."; return false; }
            foreach (var r in c.responses)
                if (r == null || string.IsNullOrWhiteSpace(r.id) || !responses.Add(r.id)
                    || string.IsNullOrWhiteSpace(r.text) || r.weight < 1 || r.weight > 4)
                { problem = "Missing/duplicate response ID, text or invalid weight."; return false; }
        }
        problem = null; return checkpoints.Count > 0;
    }
}

/// <summary>Immutable public snapshot; numerical metadata is never passed to the display.</summary>
public sealed class ReasoningCheckpointRecord
{
    public string CheckpointId { get; }
    public string ResponseId { get; }
    public string Category { get; }
    public int Weight { get; }
    public int MaximumWeight => 4;
    public int Order { get; }
    public double EligibleSeconds { get; }
    public double AnsweredSeconds { get; }
    public ReasoningCheckpointRecord(string checkpoint, string response, string category, int weight, int order, double eligible, double answered)
    { CheckpointId = checkpoint; ResponseId = response; Category = category; Weight = weight; Order = order; EligibleSeconds = eligible; AnsweredSeconds = answered; }
}
