// Assets/_Project/Scripts/Requirements/RequirementNode.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One node in a requirement tree: either a concrete check (Leaf) or a logical
/// combination of child nodes (Group, ALL/ANY/NOT - see RequirementLogicalOperator).
/// This is the generalized successor to SelectionPrerequisite's flat, AND-only list -
/// see SelectionPrerequisite's own comment and RequirementEvaluator's class comment for
/// how the two coexist. SelectionPrerequisite itself is NOT restructured; existing
/// ReportLineOption/ReasoningOption assets keep using it completely unchanged. This class
/// is for NEW authoring that needs ANY/NOT/nesting, and for wrapping old data via
/// FromLegacyList when a consumer wants to run everything through one evaluator.
///
/// A single union class rather than a Leaf/Group class pair: this project has no existing
/// precedent for polymorphic serialized fields (SerializeReference), and every comparable
/// structure (SelectionPrerequisite, HypothesisCheckpoint, ReasoningOption) is a plain flat
/// [Serializable] class. A union also lets one recursive PropertyDrawer render the whole
/// tree without a type-switch on the drawer side.
///
/// Deliberately carries no CSI_Environment-specific data anywhere in this file - every
/// field here is generic (an evidenceId string, a status enum, a report-line id string).
/// Case-specific content is what gets TYPED INTO these fields by whoever authors a
/// scenario's requirement trees, never something this class or RequirementEvaluator
/// hardcodes.
/// </summary>
[System.Serializable]
public class RequirementNode
{
    // Defaults to an empty ALL group (Group/All, with an empty children list below),
    // NOT Leaf - a bare `new RequirementNode()` must be VACUOUSLY TRUE, matching
    // SelectionPrerequisite.AllSatisfied's own null/empty-list convention, because this
    // is exactly the shape every "optional extra requirement" field defaults to
    // (ReasoningOption.extraRequirement, ReportLineOption.extraRequirement, ...) for
    // every option authored before that field existed. Defaulting to Leaf here was a
    // real, previously-shipped bug: a fresh Leaf defaults requirementType to
    // EvidenceStatusAtOrAbove with evidenceId null, which RequirementEvaluator fails
    // closed on (MissingEvidenceId) - meaning every "no extra requirement configured"
    // option would have silently, always failed its extra gate. Caught live when it
    // blocked GreyboxFlowTest's report-line selection outright (a hard-gated step);
    // it had already been silently suppressing HypothesisCheckpointManager's
    // CountThreshold checkpoint eligibility without any visible failure, since nothing
    // there hard-asserts a checkpoint must actually open.
    public RequirementNodeKind kind = RequirementNodeKind.Group;

    // --- Group fields (kind == Group) ---
    public RequirementLogicalOperator operatorType = RequirementLogicalOperator.All;

    // [SerializeReference], not a plain nested list: RequirementNode is self-referential
    // (children : List<RequirementNode>), and Unity's default serializer statically
    // unrolls nested [Serializable] value types to a fixed depth cap (10) - confirmed
    // live during this pass ("Serialization depth limit 10 exceeded at
    // 'RequirementNode.kind'"), which would silently drop any tree nested deeper than
    // that with only a console warning, never a build error. SerializeReference resolves
    // children through Unity's managed-reference table instead of inlining the type
    // graph, which breaks the static recursion entirely - the console warning itself
    // suggested this exact fix. Root-level embedding fields elsewhere (RequirementDefinition.
    // root, ReasoningOption.extraRequirement) don't need this: a single non-list
    // RequirementNode field isn't itself the recursive edge, only this list is.
    [SerializeReference]
    public List<RequirementNode> children = new();

    // --- Leaf fields (kind == Leaf) - only the ones relevant to requirementType are meaningful ---
    public RequirementType requirementType = RequirementType.EvidenceStatusAtOrAbove;

    [Tooltip("Used by every leaf type except LegacySelectionPrerequisite and RequirementAssetReference.")]
    public string evidenceId;

    [Tooltip("Authoring convenience only - evaluation never reads this. Drag an EvidenceDefinition here in the Inspector to auto-fill evidenceId above from its evidenceId field, instead of typing the id by hand. Safe to leave empty; evidenceId is always the field that actually matters at runtime.")]
    public EvidenceDefinition evidenceReference;

    [Tooltip("EvidenceStatusAtOrAbove only: the status this item must have reached (or passed), per EvidenceStateManager.IsAtOrPastStatus.")]
    public EvidenceStatus requiredStatus;

    [Tooltip("FingerprintLabStatusAtOrAbove only: the lab stage this item's print must have reached (or passed).")]
    public FingerprintLabStatus requiredLabStatus;

    [Tooltip("ReportLineSelected only: the report-line id that must have been selected on evidenceId's report.")]
    public string reportLineId;

    [Tooltip("LegacySelectionPrerequisite only: the original prerequisite, evaluated verbatim via SelectionPrerequisite.EvaluateOne - see FromLegacyList.")]
    public SelectionPrerequisite legacyPrerequisite;

    [Tooltip("RequirementAssetReference only: a shared, reusable requirement tree authored as its own asset. Evaluated by recursing into its root node, guarded against circular references.")]
    public RequirementDefinition requirementAsset;

    [Tooltip("NarrativeEventCompleted only: the eventId of a NarrativeEvent (authored in a NarrativeEventSet) that must have fired at least once. Plain string, not an object reference - NarrativeEvent is a plain class inside a set's list, not its own asset, the same reportLineId-is-a-string reasoning as ReportLineSelected above.")]
    public string narrativeEventId;

    [Tooltip("ObjectiveCompleted only: the objectiveId of an Objective (authored in an ObjectiveSet) that must have reached Completed. Also how \"depends on an earlier objective\" is expressed - see Objective's own class comment.")]
    public string objectiveId;

    [Tooltip("RelationshipCompleted only: the relationshipId of a RelationshipDefinition that must have reached RelationshipState.Completed.")]
    public string relationshipId;

    [Tooltip("FindingConfirmed only: the findingId of a FindingDefinition that must have been confirmed - see FindingStateManager.IsConfirmed.")]
    public string findingId;

    [Tooltip("MeasurementCompleted only: the requirementId of a MeasurementRequirement (in the scene's MeasurementRequirementSet) that must be complete.")]
    public string measurementRequirementId;
    public ScenarioPhase scenarioPhase;

    [TextArea]
    [Tooltip("Optional free-text label, shown in the Inspector summary and in dev-facing failure messages - purely for the author's own clarity, never read by evaluation logic.")]
    public string authoringNote;

    /// <summary>
    /// Wraps an existing flat SelectionPrerequisite list as an ALL-group of
    /// LegacySelectionPrerequisite leaves, one per entry, in the same order. This is the
    /// backward-compatibility bridge: evaluating the result through RequirementEvaluator
    /// produces exactly the same pass/fail as SelectionPrerequisite.AllSatisfied(legacy)
    /// would, because each leaf calls SelectionPrerequisite.EvaluateOne on the same
    /// unmodified entry rather than re-deriving the check. Null or empty input yields an
    /// empty ALL-group, which evaluates to true - matching AllSatisfied's own vacuous-true
    /// behavior for a null/empty list.
    /// </summary>
    public static RequirementNode FromLegacyList(List<SelectionPrerequisite> legacy)
    {
        var group = new RequirementNode { kind = RequirementNodeKind.Group, operatorType = RequirementLogicalOperator.All };

        if (legacy == null)
        {
            return group;
        }

        foreach (var entry in legacy)
        {
            group.children.Add(new RequirementNode
            {
                kind = RequirementNodeKind.Leaf,
                requirementType = RequirementType.LegacySelectionPrerequisite,
                legacyPrerequisite = entry
            });
        }

        return group;
    }

    /// <summary>
    /// A human-readable one-line (or nested, indented) description of this node, for the
    /// Inspector's summary foldout and for building dev-facing failure messages. Never
    /// used by evaluation itself - purely presentational.
    /// </summary>
    public string BuildSummary()
    {
        var sb = new System.Text.StringBuilder();
        AppendSummary(sb, 0);
        return sb.ToString();
    }

    private void AppendSummary(System.Text.StringBuilder sb, int depth)
    {
        string indent = depth > 0 ? new string(' ', depth * 2) : string.Empty;

        if (kind == RequirementNodeKind.Group)
        {
            if (children == null || children.Count == 0)
            {
                sb.Append(indent).Append(operatorType).Append(" of: (empty)");
                return;
            }

            sb.Append(indent).Append(operatorType).Append(" of:");
            foreach (var child in children)
            {
                sb.Append('\n');
                if (child == null)
                {
                    sb.Append(indent).Append("  (missing child)");
                    continue;
                }
                child.AppendSummary(sb, depth + 1);
            }
            return;
        }

        sb.Append(indent).Append(DescribeLeaf());
    }

    private string DescribeLeaf()
    {
        string note = string.IsNullOrEmpty(authoringNote) ? string.Empty : $" ({authoringNote})";

        switch (requirementType)
        {
            case RequirementType.EvidenceStatusAtOrAbove:
                return $"[{Id()} reached {requiredStatus}]{note}";
            case RequirementType.ReportLineSelected:
                return $"[{Id()} report line '{reportLineId}' selected]{note}";
            case RequirementType.FingerprintLabStatusAtOrAbove:
                return $"[{Id()} fingerprint lab reached {requiredLabStatus}]{note}";
            case RequirementType.EvidenceSwabbed:
                return $"[{Id()} swabbed]{note}";
            case RequirementType.EvidenceFingerprinted:
                return $"[{Id()} fingerprinted]{note}";
            case RequirementType.EvidenceFlipped:
                return $"[{Id()} flipped]{note}";
            case RequirementType.EvidenceDiscovered:
                return $"[{Id()} discovered]{note}";
            case RequirementType.WitnessStatementDiscovered:
                return $"[witness statement '{evidenceId}' discovered]{note}";
            case RequirementType.LegacySelectionPrerequisite:
                return legacyPrerequisite == null
                    ? $"[legacy prerequisite: missing]{note}"
                    : $"[legacy: {legacyPrerequisite.type} {legacyPrerequisite.evidenceId}/{legacyPrerequisite.lineId}]{note}";
            case RequirementType.RequirementAssetReference:
                return requirementAsset == null
                    ? $"[requirement asset: missing]{note}"
                    : $"[requirement asset: {requirementAsset.name}]{note}";
            case RequirementType.NarrativeEventCompleted:
                return $"[narrative event '{narrativeEventId}' completed]{note}";
            case RequirementType.ObjectiveCompleted:
                return $"[objective '{objectiveId}' completed]{note}";
            case RequirementType.RelationshipCompleted:
                return $"[relationship '{relationshipId}' completed]{note}";
            case RequirementType.FindingConfirmed:
                return $"[finding '{findingId}' confirmed]{note}";
            case RequirementType.SceneMeasurementsComplete:
                return $"[scene measurements complete]{note}";
            case RequirementType.EvidencePairMeasurementsComplete:
                return $"[{Id()} evidence distances complete]{note}";
            case RequirementType.DiscoveredSceneMeasurementsComplete:
                return $"[discovered scene measurements complete]{note}";
            case RequirementType.IOCAssessmentCompleted:
                return $"[IOC initial assessment completed]{note}";
            case RequirementType.ScenarioPhaseReached:
                return $"[procedural phase {scenarioPhase} reached]{note}";
            case RequirementType.SceneTurnoverCompleted:
                return $"[scene turned over by the First Responder]{note}";
            default:
                return $"[unrecognized leaf]{note}";
        }
    }

    private string Id() => string.IsNullOrEmpty(evidenceId) ? "(no evidenceId)" : evidenceId;

    /// <summary>
    /// Author-time validation, walked recursively. Appends one human-readable warning per
    /// issue found; does not throw and does not mutate the tree. Mirrors
    /// HypothesisCheckpointSet.OnValidate's own warn-don't-block idiom - callers (the
    /// custom PropertyDrawer, RequirementDefinition.OnValidate) decide how to surface
    /// these, this method only finds them.
    /// </summary>
    public void Validate(List<string> warnings, string path = "root")
    {
        if (warnings == null)
        {
            return;
        }

        if (kind == RequirementNodeKind.Group)
        {
            if (children == null || children.Count == 0)
            {
                if (operatorType == RequirementLogicalOperator.Any)
                {
                    warnings.Add($"{path}: empty ANY group - this will always evaluate to false.");
                }
                // Empty ALL is intentionally not warned about - it is the documented,
                // legitimate "no requirement" vacuous-true default (e.g. ReasoningOption.
                // extraRequirement's own default state).
                return;
            }

            if (operatorType == RequirementLogicalOperator.Not && children.Count != 1)
            {
                warnings.Add($"{path}: NOT group must have exactly one child (has {children.Count}). It will always fail closed until fixed.");
            }

            var seenSummaries = new HashSet<string>();
            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                string childPath = $"{path}/{operatorType}[{i}]";

                if (child == null)
                {
                    warnings.Add($"{childPath}: missing child node.");
                    continue;
                }

                string summary = child.BuildSummary();
                if (!seenSummaries.Add(summary))
                {
                    warnings.Add($"{childPath}: duplicate of an earlier sibling condition ({summary.Replace("\n", " ")}).");
                }

                child.Validate(warnings, childPath);
            }
            return;
        }

        // Leaf.
        switch (requirementType)
        {
            case RequirementType.EvidenceStatusAtOrAbove:
            case RequirementType.EvidenceSwabbed:
            case RequirementType.EvidenceFingerprinted:
            case RequirementType.EvidenceFlipped:
            case RequirementType.EvidenceDiscovered:
            case RequirementType.WitnessStatementDiscovered:
                if (string.IsNullOrEmpty(evidenceId))
                {
                    warnings.Add($"{path}: {requirementType} leaf has no evidenceId - will always fail.");
                }
                break;

            case RequirementType.ReportLineSelected:
                if (string.IsNullOrEmpty(evidenceId) || string.IsNullOrEmpty(reportLineId))
                {
                    warnings.Add($"{path}: ReportLineSelected leaf is missing evidenceId or reportLineId - will always fail.");
                }
                break;

            case RequirementType.FingerprintLabStatusAtOrAbove:
                if (string.IsNullOrEmpty(evidenceId))
                {
                    warnings.Add($"{path}: FingerprintLabStatusAtOrAbove leaf has no evidenceId - will always fail.");
                }
                break;

            case RequirementType.LegacySelectionPrerequisite:
                if (legacyPrerequisite == null || string.IsNullOrEmpty(legacyPrerequisite.evidenceId))
                {
                    warnings.Add($"{path}: LegacySelectionPrerequisite leaf is missing its wrapped prerequisite or its evidenceId - will always fail.");
                }
                break;

            case RequirementType.RequirementAssetReference:
                if (requirementAsset == null)
                {
                    warnings.Add($"{path}: RequirementAssetReference leaf has no asset assigned - will always fail.");
                }
                break;

            case RequirementType.NarrativeEventCompleted:
                if (string.IsNullOrEmpty(narrativeEventId))
                {
                    warnings.Add($"{path}: NarrativeEventCompleted leaf has no narrativeEventId - will always fail.");
                }
                break;

            case RequirementType.ObjectiveCompleted:
                if (string.IsNullOrEmpty(objectiveId))
                {
                    warnings.Add($"{path}: ObjectiveCompleted leaf has no objectiveId - will always fail.");
                }
                break;

            case RequirementType.RelationshipCompleted:
                if (string.IsNullOrEmpty(relationshipId))
                {
                    warnings.Add($"{path}: RelationshipCompleted leaf has no relationshipId - will always fail.");
                }
                break;

            case RequirementType.FindingConfirmed:
                if (string.IsNullOrEmpty(findingId))
                {
                    warnings.Add($"{path}: FindingConfirmed leaf has no findingId - will always fail.");
                }
                break;

            case RequirementType.EvidencePairMeasurementsComplete:
                if (string.IsNullOrEmpty(evidenceId))
                {
                    warnings.Add($"{path}: EvidencePairMeasurementsComplete leaf has no evidenceId - will always fail.");
                }
                break;
        }
    }
}
