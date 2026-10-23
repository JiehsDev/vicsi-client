// Assets/_Project/Scripts/Testing/RelationshipStateManagerTest.cs
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>
/// Focused, re-runnable checks for RelationshipDefinition/RelationshipStateManager/
/// DeductionScorer - same "checked-in test beats a memory of one" reasoning as
/// GreyboxFlowTest and RequirementEvaluatorTest. Builds its own throwaway
/// RelationshipStateManager + RelationshipDefinition instances (never touching any real
/// scene-registered manager) so this is safe to run repeatedly without side effects on
/// EvidenceStateManager or any other singleton.
///
/// Run it from the editor via:
///     FindFirstObjectByType&lt;RelationshipStateManagerTest&gt;().RunAllTests()
/// or tick runOnStart to have it run automatically on entering Play mode.
/// </summary>
public class RelationshipStateManagerTest : MonoBehaviour
{
    [Tooltip("Run all checks automatically when entering Play mode.")]
    [SerializeField] private bool runOnStart;

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
        report.AppendLine("=== RelationshipStateManagerTest ===");

        RelationshipStateManager manager = null;
        GameObject managerGo = null;
        var scratchAssets = new List<ScriptableObject>();

        // If a real scene RelationshipStateManager ever exists, this test's own
        // throwaway instance would clobber its static Instance in Awake and then NULL
        // IT OUT PERMANENTLY on Destroy (OnDestroy's own guard fires because Instance
        // now points at the temp one) - confirmed as a real, live consequence for the
        // sibling EndingManager once it got a real scene instance, not a hypothetical.
        // Captured and force-restored regardless, so this stays safe once a real
        // instance exists.
        var previousInstance = RelationshipStateManager.Instance;

        try
        {
            var relAlwaysUnlocked = MakeDefinition(scratchAssets, "REL-always", "EVD-014", "conclusion_murder_weapon", "means", isCorrect: true, weight: 2f);
            var relGatedCorrect = MakeDefinition(scratchAssets, "REL-gated-correct", "EVD-019", "conclusion_wife", "motive", isCorrect: true, weight: 1f);
            relGatedCorrect.unlockRequirement = new RequirementNode
            {
                kind = RequirementNodeKind.Leaf,
                requirementType = RequirementType.EvidenceStatusAtOrAbove,
                evidenceId = "NEVER-REGISTERED-SO-NEVER-UNLOCKS",
                requiredStatus = EvidenceStatus.Found
            };
            var relIncorrect = MakeDefinition(scratchAssets, "REL-red-herring", "EVD-018", "conclusion_murder_weapon", "means", isCorrect: false, weight: 1f);
            // Second candidate sharing the SAME pair as relIncorrect - proves FindByPair
            // returns multiple candidates and both get marked Selected together.
            var relSamePairAlt = MakeDefinition(scratchAssets, "REL-red-herring-alt", "EVD-018", "conclusion_murder_weapon", "opportunity", isCorrect: false, weight: 1f);

            managerGo = new GameObject("TempRelationshipStateManager");
            managerGo.SetActive(false); // Awake/OnEnable deferred until the field below is set
            manager = managerGo.AddComponent<RelationshipStateManager>();

            var field = typeof(RelationshipStateManager).GetField("sceneRelationshipDefinitions", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(manager, new List<RelationshipDefinition> { relAlwaysUnlocked, relGatedCorrect, relIncorrect, relSamePairAlt });

            managerGo.SetActive(true); // now Awake + OnEnable (+ initial RefreshUnlocks) run

            Check(failures, report, "Always-unlocked definition (empty ALL requirement) reaches Available immediately",
                manager.GetRecord("REL-always")?.state == RelationshipState.Available);

            Check(failures, report, "Gated definition with an unsatisfiable requirement stays Locked",
                manager.GetRecord("REL-gated-correct")?.state == RelationshipState.Locked);

            var pairMatches = manager.FindByPair("EVD-018", "conclusion_murder_weapon");
            Check(failures, report, "FindByPair returns both candidates sharing the same pair", pairMatches.Count == 2);

            var pairMatchesReversed = manager.FindByPair("conclusion_murder_weapon", "EVD-018");
            Check(failures, report, "FindByPair is order-independent", pairMatchesReversed.Count == 2);

            manager.MarkSelectedForPair("EVD-014", "conclusion_murder_weapon");
            Check(failures, report, "MarkSelectedForPair marks a matching definition Selected",
                manager.GetRecord("REL-always")?.state == RelationshipState.Selected);
            Check(failures, report, "MarkSelectedForPair sets wasEverSelected",
                manager.GetRecord("REL-always")?.wasEverSelected == true);

            // Locked relationship: the board has no mechanism to refuse a physically
            // drawn connection - see RelationshipStateManager's own class comment.
            manager.MarkSelectedForPair("EVD-019", "conclusion_wife");
            Check(failures, report, "A Locked relationship still registers as Selected when physically connected (board has no gate)",
                manager.GetRecord("REL-gated-correct")?.state == RelationshipState.Selected);

            manager.MarkSelectedForPair("EVD-018", "conclusion_murder_weapon");
            Check(failures, report, "Both candidates sharing a pair are marked Selected together",
                manager.GetRecord("REL-red-herring")?.state == RelationshipState.Selected
                && manager.GetRecord("REL-red-herring-alt")?.state == RelationshipState.Selected);

            manager.MarkDeselectedForPair("EVD-018", "conclusion_murder_weapon");
            Check(failures, report, "Deselecting reverts state to Available",
                manager.GetRecord("REL-red-herring")?.state == RelationshipState.Available);
            Check(failures, report, "Deselecting does NOT clear wasEverSelected (log history preserved)",
                manager.GetRecord("REL-red-herring")?.wasEverSelected == true);

            manager.CompleteAllSelected();
            Check(failures, report, "CompleteAllSelected advances Selected -> Completed",
                manager.GetRecord("REL-always")?.state == RelationshipState.Completed);
            Check(failures, report, "CompleteAllSelected does not touch an Available (deselected) record",
                manager.GetRecord("REL-red-herring")?.state == RelationshipState.Available);

            // --- DeductionScorer, reading this same manager ---
            var scorerGo = new GameObject("TempDeductionScorer");
            var scorer = scorerGo.AddComponent<DeductionScorer>();
            var result = scorer.Score(null);

            // REL-always (isCorrect, weight 2) Completed; REL-gated-correct (isCorrect,
            // weight 1) Selected but never Completed (CompleteAllSelected already ran
            // before this Selected transition would count) - actually it WAS Selected
            // before CompleteAllSelected ran, so it should also be Completed. Expect
            // full required score (2/2 correct weight achieved... but gated one is
            // weight 1, so total correct weight = 3, achieved = 3 if both completed).
            bool gatedCompleted = manager.GetRecord("REL-gated-correct")?.state == RelationshipState.Completed;
            float expectedRequired = gatedCompleted ? 1f : (2f / 3f);
            Check(failures, report, $"DeductionScorer.requiredLinksScore reflects Completed isCorrect weight ({result.requiredLinksScore:F2})",
                Mathf.Approximately(result.requiredLinksScore, expectedRequired));

            // Both incorrect candidates ended Available (deselected), neither Completed,
            // so contradictionScore should be a full 1 (no incorrect link survived to submission).
            Check(failures, report, $"DeductionScorer.contradictionScore is 1 when no incorrect link reached Completed ({result.contradictionScore:F2})",
                Mathf.Approximately(result.contradictionScore, 1f));

            Object.Destroy(scorerGo);
        }
        finally
        {
            if (managerGo != null)
            {
                Object.Destroy(managerGo);
            }
            foreach (var asset in scratchAssets)
            {
                Object.DestroyImmediate(asset);
            }

            var property = typeof(RelationshipStateManager).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            property?.SetValue(null, previousInstance);
        }

        report.AppendLine(failures.Count == 0 ? "=== PASS, 0 failures ===" : $"=== FAIL: {failures.Count} failure(s) ===");
        return report.ToString();
    }

    private static RelationshipDefinition MakeDefinition(List<ScriptableObject> scratchAssets, string id, string sourceId, string targetId, string type, bool isCorrect, float weight)
    {
        var def = ScriptableObject.CreateInstance<RelationshipDefinition>();
        def.relationshipId = id;
        def.sourceId = sourceId;
        def.targetId = targetId;
        def.relationshipType = type;
        def.isCorrect = isCorrect;
        def.scoreWeight = weight;
        def.unlockRequirement = new RequirementNode { kind = RequirementNodeKind.Group, operatorType = RequirementLogicalOperator.All };
        scratchAssets.Add(def);
        return def;
    }

    private static void Check(List<string> failures, StringBuilder report, string label, bool condition)
    {
        if (condition)
        {
            report.AppendLine($"  PASS: {label}");
        }
        else
        {
            failures.Add(label);
            report.AppendLine($"  FAIL: {label}");
        }
    }
}
