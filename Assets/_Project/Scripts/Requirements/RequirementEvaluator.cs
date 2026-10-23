// Assets/_Project/Scripts/Requirements/RequirementEvaluator.cs
using System.Collections.Generic;

/// <summary>
/// The single shared prerequisite/requirement evaluator - the source of truth this
/// project's later systems (evidence relationships, narrative events, investigation
/// objectives, conclusion submission, ending resolution) are all meant to call, alongside
/// the existing report-line/hypothesis consumers that already go through
/// SelectionPrerequisite. Stateless and read-only: evaluating a tree never calls a
/// EvidenceStateManager.Mark* method, never blocks, and is safe to call every frame if a
/// caller wanted to (none currently do - see HypothesisCheckpointManager's own
/// event-driven re-evaluation pattern for the established alternative).
///
/// Truth table for empty/degenerate groups, locked in as project policy rather than left
/// to drift per-caller:
///   empty ALL -> true   (vacuous AND - "no requirement" reads as satisfied, matching
///                        SelectionPrerequisite.AllSatisfied's own null/empty behavior)
///   empty ANY -> false  (vacuous OR - nothing offered to satisfy it)
///   NOT with children.Count != 1 -> fails closed (InvalidNotGroup) - a NOT is a unary
///                        "invert this one child" node, not a variadic operator; a
///                        misconfigured one must not silently pass.
///
/// Every EvidenceStateManager query goes through its own explicit, sequence-aware public
/// methods (IsAtOrPastStatus, HasSelectedReportLine, IsFingerprintLabStatusAtOrAbove) or
/// direct per-item flags (swabbingDone/fingerprintingDone/isFlipped) - never a raw
/// EvidenceStatus/FingerprintLabStatus int comparison, so a future reorder of either enum
/// cannot silently change what a requirement means.
/// </summary>
public static class RequirementEvaluator
{
    /// <summary>Convenience wrapper for callers that only need a bool. Null node is vacuously true.</summary>
    public static bool IsSatisfied(RequirementNode node)
    {
        return Evaluate(node).Passed;
    }

    /// <summary>
    /// Evaluates node and returns full detail. includeChildResults populates
    /// ChildResults recursively - left false (and ChildResults left null) for the
    /// overwhelmingly common "just tell me pass/fail plus why" case, so a routine gating
    /// check doesn't build a result tree nothing will read.
    /// </summary>
    public static RequirementEvaluationResult Evaluate(RequirementNode node, bool includeChildResults = false)
    {
        return Evaluate(node, includeChildResults, new HashSet<RequirementDefinition>());
    }

    private static RequirementEvaluationResult Evaluate(RequirementNode node, bool includeChildResults, HashSet<RequirementDefinition> visitedAssets)
    {
        if (node == null)
        {
            // No requirement at all - vacuously true, same as SelectionPrerequisite.AllSatisfied(null).
            return RequirementEvaluationResult.Success(null);
        }

        return node.kind == RequirementNodeKind.Group
            ? EvaluateGroup(node, includeChildResults, visitedAssets)
            : EvaluateLeaf(node, visitedAssets);
    }

    private static RequirementEvaluationResult EvaluateGroup(RequirementNode node, bool includeChildResults, HashSet<RequirementDefinition> visitedAssets)
    {
        var children = node.children;

        // NOT is checked BEFORE the generic empty-children branch below, not after -
        // an empty NOT (0 children) must fail closed as InvalidNotGroup, not fall
        // through to the generic "empty ALL/ANY" handling and read as vacuously true.
        // A prior version of this method got this ordering wrong (empty NOT returned
        // Success because the generic check short-circuited first); caught by
        // RequirementEvaluatorTest.TestInvalidNotGroup_ZeroChildren before it ever
        // shipped as a real bug.
        if (node.operatorType == RequirementLogicalOperator.Not)
        {
            if (children == null || children.Count != 1)
            {
                return RequirementEvaluationResult.Failure(node, RequirementFailureReason.InvalidNotGroup,
                    $"NOT group must have exactly one child (has {(children == null ? 0 : children.Count)}).");
            }

            var childResult = Evaluate(children[0], includeChildResults, visitedAssets);
            var result = childResult.Passed
                ? RequirementEvaluationResult.Failure(node, RequirementFailureReason.None, "NOT: wrapped child passed, so this fails.", childResult.RelevantId)
                : RequirementEvaluationResult.Success(node, childResult.RelevantId);

            if (includeChildResults)
            {
                result.ChildResults = new List<RequirementEvaluationResult> { childResult };
            }
            return result;
        }

        if (children == null || children.Count == 0)
        {
            // Only ALL/ANY reach here - NOT already returned above regardless of count.
            return node.operatorType == RequirementLogicalOperator.Any
                ? RequirementEvaluationResult.Failure(node, RequirementFailureReason.EmptyAnyGroup, "Empty ANY group - nothing to satisfy it.")
                : RequirementEvaluationResult.Success(node); // empty ALL - vacuous AND.
        }

        List<RequirementEvaluationResult> collected = includeChildResults ? new List<RequirementEvaluationResult>() : null;

        if (node.operatorType == RequirementLogicalOperator.All)
        {
            RequirementEvaluationResult firstFailure = null;
            foreach (var child in children)
            {
                var childResult = Evaluate(child, includeChildResults, visitedAssets);
                collected?.Add(childResult);
                if (!childResult.Passed && firstFailure == null)
                {
                    firstFailure = childResult;
                    if (!includeChildResults)
                    {
                        // No caller wants the full tree - short-circuit for a routine gate check.
                        break;
                    }
                }
            }

            var groupResult = firstFailure == null
                ? RequirementEvaluationResult.Success(node)
                : RequirementEvaluationResult.Failure(node, firstFailure.Reason, firstFailure.FailureMessage, firstFailure.RelevantId);
            groupResult.Node = node;
            groupResult.ChildResults = collected;
            return groupResult;
        }

        // Any.
        RequirementEvaluationResult firstPass = null;
        RequirementEvaluationResult lastFailure = null;
        foreach (var child in children)
        {
            var childResult = Evaluate(child, includeChildResults, visitedAssets);
            collected?.Add(childResult);
            if (childResult.Passed && firstPass == null)
            {
                firstPass = childResult;
                if (!includeChildResults)
                {
                    break;
                }
            }
            lastFailure = childResult;
        }

        var anyResult = firstPass != null
            ? RequirementEvaluationResult.Success(node, firstPass.RelevantId)
            : RequirementEvaluationResult.Failure(node, lastFailure?.Reason ?? RequirementFailureReason.Unconfigured,
                lastFailure?.FailureMessage ?? "ANY group: no child passed.", lastFailure?.RelevantId);
        anyResult.Node = node;
        anyResult.ChildResults = collected;
        return anyResult;
    }

    private static RequirementEvaluationResult EvaluateLeaf(RequirementNode node, HashSet<RequirementDefinition> visitedAssets)
    {
        switch (node.requirementType)
        {
            case RequirementType.WitnessStatementDiscovered:
                return VendorFollowupInterview.Instance != null
                    && VendorFollowupInterview.Instance.HasDiscovered(node.evidenceId)
                    ? RequirementEvaluationResult.Success(node, node.evidenceId)
                    : RequirementEvaluationResult.Failure(node, RequirementFailureReason.NotDiscovered,
                        "Witness statement has not been obtained.", node.evidenceId);
            case RequirementType.EvidenceStatusAtOrAbove:
                return EvaluateEvidenceStatusAtOrAbove(node);

            case RequirementType.ReportLineSelected:
                return EvaluateReportLineSelected(node);

            case RequirementType.FingerprintLabStatusAtOrAbove:
                return EvaluateFingerprintLabStatusAtOrAbove(node);

            case RequirementType.EvidenceSwabbed:
                return EvaluateRecordFlag(node, r => r.swabbingDone, RequirementFailureReason.SwabNotDone, "swabbed");

            case RequirementType.EvidenceFingerprinted:
                return EvaluateRecordFlag(node, r => r.fingerprintingDone, RequirementFailureReason.FingerprintNotDone, "fingerprinted");

            case RequirementType.EvidenceFlipped:
                return EvaluateRecordFlag(node, r => r.isFlipped, RequirementFailureReason.NotFlipped, "flipped");

            case RequirementType.EvidenceDiscovered:
                return EvaluateEvidenceDiscovered(node);

            case RequirementType.LegacySelectionPrerequisite:
                return EvaluateLegacy(node);

            case RequirementType.RequirementAssetReference:
                return EvaluateAssetReference(node, visitedAssets);

            case RequirementType.NarrativeEventCompleted:
                return EvaluateNarrativeEventCompleted(node);

            case RequirementType.ObjectiveCompleted:
                return EvaluateObjectiveCompleted(node);

            case RequirementType.RelationshipCompleted:
                return EvaluateRelationshipCompleted(node);

            case RequirementType.FindingConfirmed:
                return EvaluateFindingConfirmed(node);

            case RequirementType.ScenePhotographyCompleted:
                return EvaluateScenePhotographyCompleted(node);

            case RequirementType.MeasurementCompleted:
                return EvaluateMeasurementCompleted(node);
            case RequirementType.SceneMeasurementsComplete:
                return EvaluateSceneMeasurementsComplete(node);
            case RequirementType.EvidencePairMeasurementsComplete:
                return EvaluatePairMeasurementsComplete(node);
            case RequirementType.DiscoveredSceneMeasurementsComplete:
                return EvaluateDiscoveredSceneMeasurementsComplete(node);
            case RequirementType.IOCAssessmentCompleted:
                return SceneTurnover.IOCAssessmentCompleted
                    ? RequirementEvaluationResult.Success(node, null)
                    : RequirementEvaluationResult.Failure(node, RequirementFailureReason.Unconfigured, "IOC initial assessment is not complete.");
            case RequirementType.ScenarioPhaseReached:
                return SceneTurnover.IsGated && SceneTurnover.HasReachedPhase(node.scenarioPhase)
                    ? RequirementEvaluationResult.Success(node, null)
                    : RequirementEvaluationResult.Failure(node, RequirementFailureReason.Unconfigured, "Procedural phase has not been reached.");
            case RequirementType.SceneTurnoverCompleted:
                return !SceneTurnover.IsGated || SceneTurnover.IsComplete
                    ? RequirementEvaluationResult.Success(node, null)
                    : RequirementEvaluationResult.Failure(node, RequirementFailureReason.Unconfigured, "The First Responder has not turned the scene over yet.");

            default:
                return RequirementEvaluationResult.Failure(node, RequirementFailureReason.Unconfigured, $"Unrecognized requirement type: {node.requirementType}.");
        }
    }

    private static RequirementEvaluationResult EvaluateEvidenceStatusAtOrAbove(RequirementNode node)
    {
        if (string.IsNullOrEmpty(node.evidenceId))
        {
            return RequirementEvaluationResult.Failure(node, RequirementFailureReason.MissingEvidenceId, "No evidenceId configured.");
        }

        if (EvidenceStateManager.Instance == null)
        {
            return RequirementEvaluationResult.Failure(node, RequirementFailureReason.MissingRuntimeManager, "EvidenceStateManager not available.", node.evidenceId);
        }

        if (EvidenceStateManager.Instance.GetRecord(node.evidenceId) == null)
        {
            return RequirementEvaluationResult.Failure(node, RequirementFailureReason.UnknownEvidenceId, $"Unknown evidenceId: {node.evidenceId}.", node.evidenceId);
        }

        bool reached = EvidenceStateManager.Instance.IsAtOrPastStatus(node.evidenceId, node.requiredStatus);
        return reached
            ? RequirementEvaluationResult.Success(node, node.evidenceId)
            : RequirementEvaluationResult.Failure(node, RequirementFailureReason.StatusNotReached, $"{node.evidenceId} has not reached {node.requiredStatus} yet.", node.evidenceId);
    }

    private static RequirementEvaluationResult EvaluateEvidenceDiscovered(RequirementNode node)
    {
        if (string.IsNullOrEmpty(node.evidenceId))
        {
            return RequirementEvaluationResult.Failure(node, RequirementFailureReason.MissingEvidenceId, "No evidenceId configured.");
        }

        if (EvidenceStateManager.Instance == null)
        {
            return RequirementEvaluationResult.Failure(node, RequirementFailureReason.MissingRuntimeManager, "EvidenceStateManager not available.", node.evidenceId);
        }

        if (EvidenceStateManager.Instance.GetRecord(node.evidenceId) == null)
        {
            return RequirementEvaluationResult.Failure(node, RequirementFailureReason.UnknownEvidenceId, $"Unknown evidenceId: {node.evidenceId}.", node.evidenceId);
        }

        bool discovered = EvidenceStateManager.Instance.IsAtOrPastStatus(node.evidenceId, EvidenceStatus.Found);
        return discovered
            ? RequirementEvaluationResult.Success(node, node.evidenceId)
            : RequirementEvaluationResult.Failure(node, RequirementFailureReason.NotDiscovered, $"{node.evidenceId} has not been discovered yet.", node.evidenceId);
    }

    private static RequirementEvaluationResult EvaluateReportLineSelected(RequirementNode node)
    {
        if (string.IsNullOrEmpty(node.evidenceId) || string.IsNullOrEmpty(node.reportLineId))
        {
            return RequirementEvaluationResult.Failure(node, RequirementFailureReason.MissingEvidenceId, "Missing evidenceId or reportLineId.");
        }

        if (EvidenceStateManager.Instance == null)
        {
            return RequirementEvaluationResult.Failure(node, RequirementFailureReason.MissingRuntimeManager, "EvidenceStateManager not available.", node.evidenceId);
        }

        if (EvidenceStateManager.Instance.GetRecord(node.evidenceId) == null)
        {
            return RequirementEvaluationResult.Failure(node, RequirementFailureReason.UnknownEvidenceId, $"Unknown evidenceId: {node.evidenceId}.", node.evidenceId);
        }

        bool selected = EvidenceStateManager.Instance.HasSelectedReportLine(node.evidenceId, node.reportLineId);
        return selected
            ? RequirementEvaluationResult.Success(node, node.evidenceId)
            : RequirementEvaluationResult.Failure(node, RequirementFailureReason.ReportLineNotSelected, $"{node.evidenceId}: report line '{node.reportLineId}' not selected.", node.evidenceId);
    }

    private static RequirementEvaluationResult EvaluateFingerprintLabStatusAtOrAbove(RequirementNode node)
    {
        if (string.IsNullOrEmpty(node.evidenceId))
        {
            return RequirementEvaluationResult.Failure(node, RequirementFailureReason.MissingEvidenceId, "No evidenceId configured.");
        }

        if (EvidenceStateManager.Instance == null)
        {
            return RequirementEvaluationResult.Failure(node, RequirementFailureReason.MissingRuntimeManager, "EvidenceStateManager not available.", node.evidenceId);
        }

        if (EvidenceStateManager.Instance.GetRecord(node.evidenceId) == null)
        {
            return RequirementEvaluationResult.Failure(node, RequirementFailureReason.UnknownEvidenceId, $"Unknown evidenceId: {node.evidenceId}.", node.evidenceId);
        }

        bool reached = EvidenceStateManager.Instance.IsFingerprintLabStatusAtOrAbove(node.evidenceId, node.requiredLabStatus);
        return reached
            ? RequirementEvaluationResult.Success(node, node.evidenceId)
            : RequirementEvaluationResult.Failure(node, RequirementFailureReason.LabStatusNotReached, $"{node.evidenceId}: fingerprint lab has not reached {node.requiredLabStatus} yet.", node.evidenceId);
    }

    private static RequirementEvaluationResult EvaluateRecordFlag(RequirementNode node, System.Func<EvidenceRecord, bool> flagGetter, RequirementFailureReason failReason, string verb)
    {
        if (string.IsNullOrEmpty(node.evidenceId))
        {
            return RequirementEvaluationResult.Failure(node, RequirementFailureReason.MissingEvidenceId, "No evidenceId configured.");
        }

        if (EvidenceStateManager.Instance == null)
        {
            return RequirementEvaluationResult.Failure(node, RequirementFailureReason.MissingRuntimeManager, "EvidenceStateManager not available.", node.evidenceId);
        }

        var record = EvidenceStateManager.Instance.GetRecord(node.evidenceId);
        if (record == null)
        {
            return RequirementEvaluationResult.Failure(node, RequirementFailureReason.UnknownEvidenceId, $"Unknown evidenceId: {node.evidenceId}.", node.evidenceId);
        }

        return flagGetter(record)
            ? RequirementEvaluationResult.Success(node, node.evidenceId)
            : RequirementEvaluationResult.Failure(node, failReason, $"{node.evidenceId} has not been {verb} yet.", node.evidenceId);
    }

    private static RequirementEvaluationResult EvaluateLegacy(RequirementNode node)
    {
        if (node.legacyPrerequisite == null || string.IsNullOrEmpty(node.legacyPrerequisite.evidenceId))
        {
            return RequirementEvaluationResult.Failure(node, RequirementFailureReason.MissingEvidenceId, "Legacy prerequisite is missing or has no evidenceId.");
        }

        bool passed = SelectionPrerequisite.EvaluateOne(node.legacyPrerequisite);
        return passed
            ? RequirementEvaluationResult.Success(node, node.legacyPrerequisite.evidenceId)
            : RequirementEvaluationResult.Failure(node, RequirementFailureReason.LegacyPrerequisiteFailed,
                $"Legacy prerequisite not satisfied: {node.legacyPrerequisite.type} {node.legacyPrerequisite.evidenceId}/{node.legacyPrerequisite.lineId}.",
                node.legacyPrerequisite.evidenceId);
    }

    private static RequirementEvaluationResult EvaluateNarrativeEventCompleted(RequirementNode node)
    {
        if (string.IsNullOrEmpty(node.narrativeEventId))
        {
            return RequirementEvaluationResult.Failure(node, RequirementFailureReason.MissingNarrativeEventId, "No narrativeEventId configured.");
        }

        if (NarrativeEventManager.Instance == null)
        {
            return RequirementEvaluationResult.Failure(node, RequirementFailureReason.MissingRuntimeManager, "NarrativeEventManager not available.", node.narrativeEventId);
        }

        bool completed = NarrativeEventManager.Instance.IsCompleted(node.narrativeEventId);
        return completed
            ? RequirementEvaluationResult.Success(node, node.narrativeEventId)
            : RequirementEvaluationResult.Failure(node, RequirementFailureReason.NarrativeEventNotCompleted, $"Narrative event '{node.narrativeEventId}' has not completed yet.", node.narrativeEventId);
    }

    private static RequirementEvaluationResult EvaluateObjectiveCompleted(RequirementNode node)
    {
        if (string.IsNullOrEmpty(node.objectiveId))
        {
            return RequirementEvaluationResult.Failure(node, RequirementFailureReason.MissingObjectiveId, "No objectiveId configured.");
        }

        if (ObjectiveStateManager.Instance == null)
        {
            return RequirementEvaluationResult.Failure(node, RequirementFailureReason.MissingRuntimeManager, "ObjectiveStateManager not available.", node.objectiveId);
        }

        bool completed = ObjectiveStateManager.Instance.IsCompleted(node.objectiveId);
        return completed
            ? RequirementEvaluationResult.Success(node, node.objectiveId)
            : RequirementEvaluationResult.Failure(node, RequirementFailureReason.ObjectiveNotCompleted, $"Objective '{node.objectiveId}' has not completed yet.", node.objectiveId);
    }

    private static RequirementEvaluationResult EvaluateRelationshipCompleted(RequirementNode node)
    {
        if (string.IsNullOrEmpty(node.relationshipId))
        {
            return RequirementEvaluationResult.Failure(node, RequirementFailureReason.MissingRelationshipId, "No relationshipId configured.");
        }

        if (RelationshipStateManager.Instance == null)
        {
            return RequirementEvaluationResult.Failure(node, RequirementFailureReason.MissingRuntimeManager, "RelationshipStateManager not available.", node.relationshipId);
        }

        var record = RelationshipStateManager.Instance.GetRecord(node.relationshipId);
        bool completed = record != null && record.state == RelationshipState.Completed;
        return completed
            ? RequirementEvaluationResult.Success(node, node.relationshipId)
            : RequirementEvaluationResult.Failure(node, RequirementFailureReason.RelationshipNotCompleted, $"Relationship '{node.relationshipId}' has not completed yet.", node.relationshipId);
    }

    private static RequirementEvaluationResult EvaluateFindingConfirmed(RequirementNode node)
    {
        if (string.IsNullOrEmpty(node.findingId))
        {
            return RequirementEvaluationResult.Failure(node, RequirementFailureReason.MissingFindingId, "No findingId configured.");
        }

        if (FindingStateManager.Instance == null)
        {
            return RequirementEvaluationResult.Failure(node, RequirementFailureReason.MissingRuntimeManager, "FindingStateManager not available.", node.findingId);
        }

        bool confirmed = FindingStateManager.Instance.IsConfirmed(node.findingId);
        return confirmed
            ? RequirementEvaluationResult.Success(node, node.findingId)
            : RequirementEvaluationResult.Failure(node, RequirementFailureReason.FindingNotConfirmed, $"Finding '{node.findingId}' has not been confirmed yet.", node.findingId);
    }

    private static RequirementEvaluationResult EvaluateScenePhotographyCompleted(RequirementNode node)
    {
        var manager = ScenePhotographyManager.Instance;
        if (manager == null)
        {
            return RequirementEvaluationResult.Failure(node, RequirementFailureReason.MissingRuntimeManager, "ScenePhotographyManager not available.");
        }

        return manager.IsScenePhotographyComplete
            ? RequirementEvaluationResult.Success(node, null)
            : RequirementEvaluationResult.Failure(node, RequirementFailureReason.ScenePhotographyIncomplete, "Scene photography is not complete yet.");
    }

    private static RequirementEvaluationResult EvaluateMeasurementCompleted(RequirementNode node)
    {
        if (string.IsNullOrEmpty(node.measurementRequirementId))
        {
            return RequirementEvaluationResult.Failure(node, RequirementFailureReason.MissingMeasurementRequirementId, "No measurementRequirementId configured.");
        }

        if (MeasurementStateManager.Instance == null)
        {
            return RequirementEvaluationResult.Failure(node, RequirementFailureReason.MissingRuntimeManager, "MeasurementStateManager not available.", node.measurementRequirementId);
        }

        return MeasurementStateManager.Instance.IsMeasurementComplete(node.measurementRequirementId)
            ? RequirementEvaluationResult.Success(node, node.measurementRequirementId)
            : RequirementEvaluationResult.Failure(node, RequirementFailureReason.MeasurementNotCompleted, $"Measurement '{node.measurementRequirementId}' is not complete yet.", node.measurementRequirementId);
    }

    private static RequirementEvaluationResult EvaluateSceneMeasurementsComplete(RequirementNode node)
    {
        if (MeasurementStateManager.Instance == null)
        {
            return RequirementEvaluationResult.Failure(node, RequirementFailureReason.MissingRuntimeManager, "MeasurementStateManager not available.");
        }

        return MeasurementStateManager.Instance.AreSceneMeasurementsComplete
            ? RequirementEvaluationResult.Success(node, null)
            : RequirementEvaluationResult.Failure(node, RequirementFailureReason.SceneMeasurementsIncomplete, "Scene measurements are not complete yet.");
    }

    private static RequirementEvaluationResult EvaluateDiscoveredSceneMeasurementsComplete(RequirementNode node)
    {
        if (MeasurementStateManager.Instance == null)
        {
            return RequirementEvaluationResult.Failure(node, RequirementFailureReason.MissingRuntimeManager, "MeasurementStateManager not available.");
        }

        return MeasurementStateManager.Instance.AreDiscoveredSceneMeasurementsComplete
            ? RequirementEvaluationResult.Success(node, null)
            : RequirementEvaluationResult.Failure(node, RequirementFailureReason.DiscoveredSceneMeasurementsIncomplete, "Measurements of the discovered evidence are not complete yet.");
    }

    private static RequirementEvaluationResult EvaluatePairMeasurementsComplete(RequirementNode node)
    {
        if (string.IsNullOrEmpty(node.evidenceId))
        {
            return RequirementEvaluationResult.Failure(node, RequirementFailureReason.MissingEvidenceId, "No evidenceId configured.");
        }

        if (MeasurementStateManager.Instance == null)
        {
            return RequirementEvaluationResult.Failure(node, RequirementFailureReason.MissingRuntimeManager, "MeasurementStateManager not available.", node.evidenceId);
        }

        return MeasurementStateManager.Instance.AreAvailablePairMeasurementsComplete(node.evidenceId)
            ? RequirementEvaluationResult.Success(node, node.evidenceId)
            : RequirementEvaluationResult.Failure(node, RequirementFailureReason.PairMeasurementsIncomplete, $"Evidence distances for '{node.evidenceId}' are not complete yet.", node.evidenceId);
    }

    private static RequirementEvaluationResult EvaluateAssetReference(RequirementNode node, HashSet<RequirementDefinition> visitedAssets)
    {
        if (node.requirementAsset == null)
        {
            return RequirementEvaluationResult.Failure(node, RequirementFailureReason.MissingRequirementAsset, "No requirement asset assigned.");
        }

        if (!visitedAssets.Add(node.requirementAsset))
        {
            // Already on the current recursion path - a real cycle, not a hypothetical.
            // Fail closed rather than recurse forever or overflow the stack.
            return RequirementEvaluationResult.Failure(node, RequirementFailureReason.CircularReference,
                $"Circular requirement asset reference detected at '{node.requirementAsset.name}'.", node.requirementAsset.name);
        }

        var inner = Evaluate(node.requirementAsset.root, false, visitedAssets);

        // Only this branch's own visit should be undone - a diamond-shaped reference (two
        // different branches both legitimately pointing at the same shared asset) must
        // still resolve normally, only an actual cycle back onto an ANCESTOR should fail.
        visitedAssets.Remove(node.requirementAsset);

        return inner.Passed
            ? RequirementEvaluationResult.Success(node, node.requirementAsset.name)
            : RequirementEvaluationResult.Failure(node, inner.Reason, inner.FailureMessage, inner.RelevantId ?? node.requirementAsset.name);
    }
}
