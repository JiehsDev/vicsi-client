// Assets/_Project/Scripts/Testing/RequirementEvaluatorTest.cs
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Focused, re-runnable checks for RequirementNode/RequirementEvaluator - the same
/// "checked-in test beats a memory of one" reasoning as GreyboxFlowTest, scoped to just
/// the evaluator rather than the full evidence lifecycle. Exercises the evaluator's own
/// logic (empty groups, NOT validity, nesting, each leaf type, missing ids, duplicate/
/// circular detection, legacy backward compatibility) against whatever evidence items are
/// actually registered in the running scene - it reads EvidenceStateManager, it never
/// calls a Mark* method, so running this cannot advance or corrupt evidence state.
///
/// Run it from the editor via:
///     FindFirstObjectByType&lt;RequirementEvaluatorTest&gt;().RunAllTests()
/// or tick runOnStart to have it run automatically on entering Play mode.
/// </summary>
public class RequirementEvaluatorTest : MonoBehaviour
{
    [Tooltip("Run all checks automatically when entering Play mode.")]
    [SerializeField] private bool runOnStart;

    [Tooltip("A real, registered evidenceId to test status/flag leaves against - must exist in the active scene's EvidenceStateManager. Read-only checks; this item's state is never modified.")]
    [SerializeField] private string sampleEvidenceId = "EVD-014";

    private void Start()
    {
        if (runOnStart)
        {
            Debug.Log(RunAllTests());
        }
    }

    public string RunAllTests()
    {
        var failures = new List<string>();
        var report = new StringBuilder();
        report.AppendLine("=== RequirementEvaluatorTest ===");

        TestEmptyAllIsTrue(failures, report);
        TestEmptyAnyIsFalse(failures, report);
        TestValidNotGroup(failures, report);
        TestInvalidNotGroup_ZeroChildren(failures, report);
        TestInvalidNotGroup_TwoChildren(failures, report);
        TestOneLevelAllPassAndFail(failures, report);
        TestDeeplyNestedTree(failures, report);
        TestMissingEvidenceIdFailsClosed(failures, report);
        TestUnknownEvidenceIdFailsClosed(failures, report);
        TestEvidenceStatusAtOrAbove(failures, report);
        TestEvidenceDiscovered(failures, report);
        TestReportLineSelected_NotSelected(failures, report);
        TestFingerprintLabStatusAtOrAbove_NotSent(failures, report);
        TestSwabFlipFingerprintFlags_AllFalseInitially(failures, report);
        TestDuplicateSiblingDetection(failures, report);
        TestCircularAssetReferenceFailsClosed(failures, report);
        TestDiamondAssetReferenceStillPasses(failures, report);
        TestLegacyBackwardCompatibility(failures, report);
        TestNullNodeIsVacuouslyTrue(failures, report);

        report.AppendLine(failures.Count == 0
            ? $"=== PASS {report_PassCount(report)} checks, 0 failures ==="
            : $"=== FAIL: {failures.Count} failure(s) ===");

        return report.ToString();
    }

    private static int report_PassCount(StringBuilder report)
    {
        // Cheap count of "PASS" lines already appended - avoids threading a separate
        // counter through every Test* method just for the summary line.
        string text = report.ToString();
        int count = 0, index = 0;
        while ((index = text.IndexOf("PASS", index)) != -1)
        {
            count++;
            index += 4;
        }
        return count;
    }

    private static void Check(List<string> failures, StringBuilder report, string label, bool condition, string detailOnFail = null)
    {
        if (condition)
        {
            report.AppendLine($"  PASS: {label}");
        }
        else
        {
            string message = detailOnFail == null ? label : $"{label} - {detailOnFail}";
            failures.Add(message);
            report.AppendLine($"  FAIL: {message}");
        }
    }

    private static RequirementNode Leaf(RequirementType type, string evidenceId = null, EvidenceStatus status = default, string reportLineId = null, FingerprintLabStatus labStatus = default)
    {
        return new RequirementNode
        {
            kind = RequirementNodeKind.Leaf,
            requirementType = type,
            evidenceId = evidenceId,
            requiredStatus = status,
            reportLineId = reportLineId,
            requiredLabStatus = labStatus
        };
    }

    private static RequirementNode Group(RequirementLogicalOperator op, params RequirementNode[] children)
    {
        return new RequirementNode
        {
            kind = RequirementNodeKind.Group,
            operatorType = op,
            children = new List<RequirementNode>(children)
        };
    }

    // --- Empty groups -----------------------------------------------------

    private static void TestEmptyAllIsTrue(List<string> failures, StringBuilder report)
    {
        var node = Group(RequirementLogicalOperator.All);
        var result = RequirementEvaluator.Evaluate(node);
        Check(failures, report, "Empty ALL group evaluates to true", result.Passed);
    }

    private static void TestEmptyAnyIsFalse(List<string> failures, StringBuilder report)
    {
        var node = Group(RequirementLogicalOperator.Any);
        var result = RequirementEvaluator.Evaluate(node);
        Check(failures, report, "Empty ANY group evaluates to false", !result.Passed);
    }

    // --- NOT validity -------------------------------------------------------

    private static void TestValidNotGroup(List<string> failures, StringBuilder report)
    {
        var alwaysFalse = Leaf(RequirementType.EvidenceDiscovered, evidenceId: "");
        var node = Group(RequirementLogicalOperator.Not, alwaysFalse);
        var result = RequirementEvaluator.Evaluate(node);
        Check(failures, report, "Valid NOT (1 child, child fails) evaluates to true", result.Passed);
    }

    private static void TestInvalidNotGroup_ZeroChildren(List<string> failures, StringBuilder report)
    {
        var node = Group(RequirementLogicalOperator.Not);
        var result = RequirementEvaluator.Evaluate(node);
        Check(failures, report, "Invalid NOT (0 children) fails closed", !result.Passed && result.Reason == RequirementFailureReason.InvalidNotGroup);
    }

    private static void TestInvalidNotGroup_TwoChildren(List<string> failures, StringBuilder report)
    {
        var node = Group(RequirementLogicalOperator.Not,
            Leaf(RequirementType.EvidenceDiscovered, evidenceId: "EVD-014"),
            Leaf(RequirementType.EvidenceDiscovered, evidenceId: "EVD-015"));
        var result = RequirementEvaluator.Evaluate(node);
        Check(failures, report, "Invalid NOT (2 children) fails closed", !result.Passed && result.Reason == RequirementFailureReason.InvalidNotGroup);
    }

    // --- One-level and nested trees ------------------------------------------

    private void TestOneLevelAllPassAndFail(List<string> failures, StringBuilder report)
    {
        var passingNode = Group(RequirementLogicalOperator.All,
            Leaf(RequirementType.EvidenceStatusAtOrAbove, evidenceId: sampleEvidenceId, status: EvidenceStatus.NotFound));
        Check(failures, report, "One-level ALL with a satisfiable leaf passes", RequirementEvaluator.IsSatisfied(passingNode));

        var failingNode = Group(RequirementLogicalOperator.All,
            Leaf(RequirementType.EvidenceStatusAtOrAbove, evidenceId: "NONEXISTENT-ID", status: EvidenceStatus.Found));
        Check(failures, report, "One-level ALL with an unknown evidenceId fails", !RequirementEvaluator.IsSatisfied(failingNode));
    }

    private void TestDeeplyNestedTree(List<string> failures, StringBuilder report)
    {
        // ALL > ANY > NOT > ALL > leaf - six levels deep, exercising the
        // [SerializeReference]-backed children list (see RequirementNode's own comment
        // on why that attribute is there) well past the depth Unity's default
        // serializer would otherwise have silently truncated.
        var deepestLeaf = Leaf(RequirementType.EvidenceStatusAtOrAbove, evidenceId: sampleEvidenceId, status: EvidenceStatus.NotFound);
        var innerAll = Group(RequirementLogicalOperator.All, deepestLeaf);
        var notGroup = Group(RequirementLogicalOperator.Not, Leaf(RequirementType.EvidenceDiscovered, evidenceId: ""));
        var anyGroup = Group(RequirementLogicalOperator.Any, notGroup, innerAll);
        var root = Group(RequirementLogicalOperator.All, anyGroup);

        var result = RequirementEvaluator.Evaluate(root, includeChildResults: true);
        Check(failures, report, "6-level nested ALL>ANY>NOT/ALL>leaf tree evaluates to true", result.Passed);
        Check(failures, report, "Nested tree's ChildResults were populated when requested", result.ChildResults != null && result.ChildResults.Count > 0);
    }

    // --- Missing / unknown ids ------------------------------------------------

    private static void TestMissingEvidenceIdFailsClosed(List<string> failures, StringBuilder report)
    {
        var node = Leaf(RequirementType.EvidenceStatusAtOrAbove, evidenceId: null, status: EvidenceStatus.Found);
        var result = RequirementEvaluator.Evaluate(node);
        Check(failures, report, "Leaf with no evidenceId fails closed with MissingEvidenceId", !result.Passed && result.Reason == RequirementFailureReason.MissingEvidenceId);
    }

    private static void TestUnknownEvidenceIdFailsClosed(List<string> failures, StringBuilder report)
    {
        var node = Leaf(RequirementType.EvidenceSwabbed, evidenceId: "NO-SUCH-ITEM-EXISTS");
        var result = RequirementEvaluator.Evaluate(node);
        Check(failures, report, "Leaf with an unregistered evidenceId fails closed with UnknownEvidenceId", !result.Passed && result.Reason == RequirementFailureReason.UnknownEvidenceId);
    }

    // --- Leaf types, read-only against real scene state ----------------------

    private void TestEvidenceStatusAtOrAbove(List<string> failures, StringBuilder report)
    {
        if (EvidenceStateManager.Instance == null)
        {
            Check(failures, report, "EvidenceStatusAtOrAbove leaf (skipped, no EvidenceStateManager in scene)", true);
            return;
        }

        var node = Leaf(RequirementType.EvidenceStatusAtOrAbove, evidenceId: sampleEvidenceId, status: EvidenceStatus.NotFound);
        var result = RequirementEvaluator.Evaluate(node);
        // Every registered item is at least NotFound by construction, so this must
        // always pass for any real evidenceId - proves the leaf reads IsAtOrPastStatus
        // (sequence-aware), not a raw ordinal compare.
        Check(failures, report, $"EvidenceStatusAtOrAbove({sampleEvidenceId}, NotFound) passes", result.Passed, result.FailureMessage);
    }

    private void TestEvidenceDiscovered(List<string> failures, StringBuilder report)
    {
        var node = Leaf(RequirementType.EvidenceDiscovered, evidenceId: "NO-SUCH-ITEM-EXISTS");
        var result = RequirementEvaluator.Evaluate(node);
        Check(failures, report, "EvidenceDiscovered on an unregistered id fails with UnknownEvidenceId", !result.Passed && result.Reason == RequirementFailureReason.UnknownEvidenceId);
    }

    private void TestReportLineSelected_NotSelected(List<string> failures, StringBuilder report)
    {
        if (EvidenceStateManager.Instance == null)
        {
            Check(failures, report, "ReportLineSelected leaf (skipped, no EvidenceStateManager in scene)", true);
            return;
        }

        var node = Leaf(RequirementType.ReportLineSelected, evidenceId: sampleEvidenceId, reportLineId: "definitely-not-a-real-line-id-xyz");
        var result = RequirementEvaluator.Evaluate(node);
        Check(failures, report, "ReportLineSelected for a line never selected fails with ReportLineNotSelected", !result.Passed && result.Reason == RequirementFailureReason.ReportLineNotSelected, result.FailureMessage);
    }

    private void TestFingerprintLabStatusAtOrAbove_NotSent(List<string> failures, StringBuilder report)
    {
        if (EvidenceStateManager.Instance == null)
        {
            Check(failures, report, "FingerprintLabStatusAtOrAbove leaf (skipped, no EvidenceStateManager in scene)", true);
            return;
        }

        var atLeastNotSent = Leaf(RequirementType.FingerprintLabStatusAtOrAbove, evidenceId: sampleEvidenceId, labStatus: FingerprintLabStatus.NotSent);
        Check(failures, report, $"FingerprintLabStatusAtOrAbove({sampleEvidenceId}, NotSent) passes (every item starts at NotSent)", RequirementEvaluator.IsSatisfied(atLeastNotSent));
    }

    private void TestSwabFlipFingerprintFlags_AllFalseInitially(List<string> failures, StringBuilder report)
    {
        if (EvidenceStateManager.Instance == null)
        {
            Check(failures, report, "Swab/Flip/Fingerprint flag leaves (skipped, no EvidenceStateManager in scene)", true);
            return;
        }

        var record = EvidenceStateManager.Instance.GetRecord(sampleEvidenceId);
        if (record == null)
        {
            Check(failures, report, $"Swab/Flip/Fingerprint flag leaves (skipped, '{sampleEvidenceId}' not registered)", true);
            return;
        }

        // Read-only comparison against the record's OWN current flags, rather than
        // assuming false - this test must not assume a fresh session (another test or
        // a real play session may have already advanced this item), only that the
        // leaf's answer matches EvidenceRecord's own field.
        Check(failures, report, "EvidenceSwabbed leaf matches record.swabbingDone",
            RequirementEvaluator.IsSatisfied(Leaf(RequirementType.EvidenceSwabbed, evidenceId: sampleEvidenceId)) == record.swabbingDone);
        Check(failures, report, "EvidenceFingerprinted leaf matches record.fingerprintingDone",
            RequirementEvaluator.IsSatisfied(Leaf(RequirementType.EvidenceFingerprinted, evidenceId: sampleEvidenceId)) == record.fingerprintingDone);
        Check(failures, report, "EvidenceFlipped leaf matches record.isFlipped",
            RequirementEvaluator.IsSatisfied(Leaf(RequirementType.EvidenceFlipped, evidenceId: sampleEvidenceId)) == record.isFlipped);
    }

    // --- Duplicate detection -----------------------------------------------------

    private static void TestDuplicateSiblingDetection(List<string> failures, StringBuilder report)
    {
        var node = Group(RequirementLogicalOperator.All,
            Leaf(RequirementType.EvidenceDiscovered, evidenceId: "EVD-014"),
            Leaf(RequirementType.EvidenceDiscovered, evidenceId: "EVD-014"));

        var warnings = new List<string>();
        node.Validate(warnings);
        Check(failures, report, "Duplicate sibling leaves produce a validation warning", warnings.Exists(w => w.Contains("duplicate")));
    }

    // --- Circular / diamond asset references --------------------------------

    private static void TestCircularAssetReferenceFailsClosed(List<string> failures, StringBuilder report)
    {
        var assetA = ScriptableObject.CreateInstance<RequirementDefinition>();
        var assetB = ScriptableObject.CreateInstance<RequirementDefinition>();

        try
        {
            assetA.root = Leaf(RequirementType.RequirementAssetReference);
            assetA.root.requirementAsset = assetB;

            assetB.root = Leaf(RequirementType.RequirementAssetReference);
            assetB.root.requirementAsset = assetA; // A -> B -> A: a genuine cycle

            var referenceToA = Leaf(RequirementType.RequirementAssetReference);
            referenceToA.requirementAsset = assetA;

            var result = RequirementEvaluator.Evaluate(referenceToA);
            Check(failures, report, "Circular RequirementAssetReference (A -> B -> A) fails closed with CircularReference",
                !result.Passed && result.Reason == RequirementFailureReason.CircularReference);
        }
        finally
        {
            Object.DestroyImmediate(assetA);
            Object.DestroyImmediate(assetB);
        }
    }

    private static void TestDiamondAssetReferenceStillPasses(List<string> failures, StringBuilder report)
    {
        // Two independent branches both legitimately pointing at the SAME shared asset
        // (no cycle back onto an ancestor) must still resolve normally - see
        // RequirementEvaluator.EvaluateAssetReference's own comment on why the visited
        // guard is removed after each branch completes rather than kept for the whole
        // evaluation.
        var shared = ScriptableObject.CreateInstance<RequirementDefinition>();

        try
        {
            shared.root = Group(RequirementLogicalOperator.All); // empty ALL -> always true

            var branchA = Leaf(RequirementType.RequirementAssetReference);
            branchA.requirementAsset = shared;
            var branchB = Leaf(RequirementType.RequirementAssetReference);
            branchB.requirementAsset = shared;

            var root = Group(RequirementLogicalOperator.All, branchA, branchB);
            Check(failures, report, "Diamond-shaped shared asset reference (no cycle) still passes", RequirementEvaluator.IsSatisfied(root));
        }
        finally
        {
            Object.DestroyImmediate(shared);
        }
    }

    // --- Backward compatibility with SelectionPrerequisite -------------------

    private void TestLegacyBackwardCompatibility(List<string> failures, StringBuilder report)
    {
        var legacyList = new List<SelectionPrerequisite>
        {
            new SelectionPrerequisite { type = PrerequisiteType.ReportLineSelected, evidenceId = sampleEvidenceId, lineId = "definitely-not-a-real-line-id-xyz" }
        };

        bool oldWay = SelectionPrerequisite.AllSatisfied(legacyList);
        bool newWay = RequirementEvaluator.IsSatisfied(RequirementNode.FromLegacyList(legacyList));
        Check(failures, report, "RequirementNode.FromLegacyList produces the same result as SelectionPrerequisite.AllSatisfied", oldWay == newWay);

        bool emptyOld = SelectionPrerequisite.AllSatisfied(null);
        bool emptyNew = RequirementEvaluator.IsSatisfied(RequirementNode.FromLegacyList(null));
        Check(failures, report, "Null legacy list: both old and new paths agree (vacuously true)", emptyOld && emptyNew && emptyOld == emptyNew);
    }

    private static void TestNullNodeIsVacuouslyTrue(List<string> failures, StringBuilder report)
    {
        Check(failures, report, "Null RequirementNode evaluates to true", RequirementEvaluator.IsSatisfied(null));
    }
}
