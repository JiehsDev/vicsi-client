// Assets/_Project/Scripts/Testing/ObjectiveStateManagerTest.cs
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>
/// Focused, re-runnable checks for Objective/ObjectiveStateManager, including the
/// RequirementType.ObjectiveCompleted/RelationshipCompleted integrations and the
/// narrativeEventOnUnlock/CompletionId side effects - same "checked-in test beats a
/// memory of one" reasoning as this project's other test scripts. Builds its own
/// throwaway managers, never touching any real scene-registered singleton.
///
/// Synchronous (unlike NarrativeEventManagerTest) - Objective transitions have no delay/
/// coroutine involved, so everything can be asserted within one call.
///
/// Run it from the editor via:
///     FindFirstObjectByType&lt;ObjectiveStateManagerTest&gt;().RunAllTests()
/// </summary>
public class ObjectiveStateManagerTest : MonoBehaviour
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
        report.AppendLine("=== ObjectiveStateManagerTest ===");

        var scratchAssets = new List<ScriptableObject>();
        GameObject objGo = null, relGo = null, narGo = null;

        // This test's own throwaway ObjectiveStateManager/RelationshipStateManager/
        // NarrativeEventManager instances would clobber (and, on Destroy, potentially
        // permanently null out) a REAL scene singleton's static Instance if one exists -
        // confirmed as a real, live consequence once EndingManager got a real scene
        // instance, not a hypothetical. Captured and force-restored regardless, so this
        // stays safe once real instances exist for these three too.
        var previousObjectiveInstance = ObjectiveStateManager.Instance;
        var previousRelationshipInstance = RelationshipStateManager.Instance;
        var previousNarrativeInstance = NarrativeEventManager.Instance;

        try
        {
            // --- Relationship + Narrative rigs, for the cross-system leaf checks ---
            var relDef = ScriptableObject.CreateInstance<RelationshipDefinition>();
            relDef.relationshipId = "REL-test";
            relDef.sourceId = "EVD-014";
            relDef.targetId = "conclusion_murder_weapon";
            relDef.isCorrect = true;
            relDef.unlockRequirement = new RequirementNode { kind = RequirementNodeKind.Group, operatorType = RequirementLogicalOperator.All };
            scratchAssets.Add(relDef);

            relGo = new GameObject("TempRelationshipStateManager");
            relGo.SetActive(false);
            var relManager = relGo.AddComponent<RelationshipStateManager>();
            SetPrivateField(relManager, "sceneRelationshipDefinitions", new List<RelationshipDefinition> { relDef });
            relGo.SetActive(true);

            var pool = ScriptableObject.CreateInstance<DialogueLinePool>();
            pool.lines = new[] { "Test line." };
            scratchAssets.Add(pool);

            var manualEvent = new NarrativeEvent
            {
                eventId = "test-narrative-side-effect",
                pool = pool,
                triggerMode = NarrativeTriggerMode.Manual
            };
            // Fired manually mid-test (never touching the real scene EvidenceStateManager
            // singleton) purely to flip a NarrativeEventCompleted-based leaf from false to
            // true on demand - see the failure/completion precedence check below.
            var manualFlipTrigger = new NarrativeEvent
            {
                eventId = "test-flip-trigger",
                pool = pool,
                triggerMode = NarrativeTriggerMode.Manual
            };
            var eventSet = ScriptableObject.CreateInstance<NarrativeEventSet>();
            eventSet.events = new List<NarrativeEvent> { manualEvent, manualFlipTrigger };
            scratchAssets.Add(eventSet);

            narGo = new GameObject("TempNarrativeEventManager");
            narGo.SetActive(false);
            var narManager = narGo.AddComponent<NarrativeEventManager>();
            SetPrivateField(narManager, "eventSet", eventSet);
            narGo.SetActive(true);

            // --- Objectives ---
            var alwaysUnlocks = new Objective
            {
                objectiveId = "obj-always-unlocks",
                unlockRequirement = new RequirementNode { kind = RequirementNodeKind.Group, operatorType = RequirementLogicalOperator.All },
                completionRequirement = new RequirementNode { kind = RequirementNodeKind.Group, operatorType = RequirementLogicalOperator.All }, // default empty -> completes immediately once Active
                narrativeEventOnUnlockId = "test-narrative-side-effect"
            };

            var neverUnlocks = new Objective
            {
                objectiveId = "obj-never-unlocks",
                unlockRequirement = new RequirementNode
                {
                    kind = RequirementNodeKind.Leaf,
                    requirementType = RequirementType.EvidenceStatusAtOrAbove,
                    evidenceId = "NEVER-REGISTERED",
                    requiredStatus = EvidenceStatus.Found
                }
            };

            var realCompletion = new Objective
            {
                objectiveId = "obj-real-completion",
                unlockRequirement = new RequirementNode { kind = RequirementNodeKind.Group, operatorType = RequirementLogicalOperator.All },
                completionRequirement = new RequirementNode
                {
                    kind = RequirementNodeKind.Leaf,
                    requirementType = RequirementType.RelationshipCompleted,
                    relationshipId = "REL-test"
                }
            };

            var withFailure = new Objective
            {
                objectiveId = "obj-with-failure",
                unlockRequirement = new RequirementNode { kind = RequirementNodeKind.Group, operatorType = RequirementLogicalOperator.All },
                completionRequirement = new RequirementNode
                {
                    kind = RequirementNodeKind.Leaf,
                    requirementType = RequirementType.NarrativeEventCompleted,
                    narrativeEventId = "test-flip-trigger"
                },
                failureRequirement = new RequirementNode
                {
                    kind = RequirementNodeKind.Leaf,
                    requirementType = RequirementType.NarrativeEventCompleted,
                    narrativeEventId = "test-flip-trigger"
                }
            };

            var noFailureField = new Objective
            {
                objectiveId = "obj-no-failure-field",
                unlockRequirement = new RequirementNode { kind = RequirementNodeKind.Group, operatorType = RequirementLogicalOperator.All },
                completionRequirement = new RequirementNode
                {
                    kind = RequirementNodeKind.Leaf,
                    requirementType = RequirementType.NarrativeEventCompleted,
                    narrativeEventId = "test-flip-trigger"
                }
                // failureRequirement intentionally left null - the default.
            };

            var chainedDependent = new Objective
            {
                objectiveId = "obj-depends-on-always-unlocks",
                unlockRequirement = new RequirementNode
                {
                    kind = RequirementNodeKind.Leaf,
                    requirementType = RequirementType.ObjectiveCompleted,
                    objectiveId = "obj-always-unlocks"
                },
                completionRequirement = new RequirementNode { kind = RequirementNodeKind.Group, operatorType = RequirementLogicalOperator.All }
            };

            var objectiveSet = ScriptableObject.CreateInstance<ObjectiveSet>();
            objectiveSet.objectives = new List<Objective> { alwaysUnlocks, neverUnlocks, realCompletion, withFailure, noFailureField, chainedDependent };
            scratchAssets.Add(objectiveSet);

            objGo = new GameObject("TempObjectiveStateManager");
            objGo.SetActive(false);
            var manager = objGo.AddComponent<ObjectiveStateManager>();
            SetPrivateField(manager, "objectiveSet", objectiveSet);
            objGo.SetActive(true); // Awake + OnEnable (+ initial RefreshAll) run

            Check(failures, report, "Objective with an always-true unlock AND completion reaches Completed immediately (single RefreshAll pass)",
                manager.GetRecord("obj-always-unlocks")?.state == ObjectiveState.Completed);

            Check(failures, report, "narrativeEventOnUnlockId actually fired the referenced NarrativeEvent",
                narManager.IsCompleted("test-narrative-side-effect"));

            Check(failures, report, "Objective with an unsatisfiable unlock requirement stays Hidden",
                manager.GetRecord("obj-never-unlocks")?.state == ObjectiveState.Hidden);

            Check(failures, report, "Objective gated by RequirementType.RelationshipCompleted stays Active until the relationship actually completes",
                manager.GetRecord("obj-real-completion")?.state == ObjectiveState.Active);

            relManager.MarkSelectedForPair("EVD-014", "conclusion_murder_weapon");
            relManager.CompleteAllSelected();
            manager.RefreshAll(); // relationship-state-changed event already triggers this too; explicit call makes the test's causality clear
            Check(failures, report, "Objective completes once its RelationshipCompleted requirement is satisfied",
                manager.GetRecord("obj-real-completion")?.state == ObjectiveState.Completed);

            Check(failures, report, "RequirementType.ObjectiveCompleted reads IsCompleted correctly (true case)",
                RequirementEvaluator.IsSatisfied(new RequirementNode { kind = RequirementNodeKind.Leaf, requirementType = RequirementType.ObjectiveCompleted, objectiveId = "obj-always-unlocks" }));
            Check(failures, report, "RequirementType.ObjectiveCompleted reads IsCompleted correctly (false case)",
                !RequirementEvaluator.IsSatisfied(new RequirementNode { kind = RequirementNodeKind.Leaf, requirementType = RequirementType.ObjectiveCompleted, objectiveId = "obj-never-unlocks" }));

            Check(failures, report, "Chained same-tick dependency (B unlocks on A.ObjectiveCompleted) resolves within one RefreshAll call",
                manager.GetRecord("obj-depends-on-always-unlocks")?.state == ObjectiveState.Completed);

            // --- Failure precedence: EVD-014 is not discovered here, so neither
            // objective can complete or fail yet.
            Check(failures, report, "Objective with a failure requirement stays Active before either condition is met",
                manager.GetRecord("obj-with-failure")?.state == ObjectiveState.Active);
            Check(failures, report, "Objective with NO failure field (null) stays Active before its completion condition is met",
                manager.GetRecord("obj-no-failure-field")?.state == ObjectiveState.Active);

            // Fire the shared flip trigger - both objectives' completionRequirement AND
            // (for obj-with-failure) failureRequirement become simultaneously satisfiable.
            narManager.Fire("test-flip-trigger");
            manager.RefreshAll();

            Check(failures, report, "When both completion and failure conditions are true at once, failure wins",
                manager.GetRecord("obj-with-failure")?.state == ObjectiveState.Failed);
            Check(failures, report, "An objective with no failure field completes normally under the identical condition",
                manager.GetRecord("obj-no-failure-field")?.state == ObjectiveState.Completed);

            // --- Terminal states ---
            var before = manager.GetRecord("obj-no-failure-field")?.state;
            manager.RefreshAll();
            manager.RefreshAll();
            Check(failures, report, "Completed is terminal - repeated RefreshAll calls do not change it",
                manager.GetRecord("obj-no-failure-field")?.state == before);
        }
        finally
        {
            if (objGo != null) Object.Destroy(objGo);
            if (relGo != null) Object.Destroy(relGo);
            if (narGo != null) Object.Destroy(narGo);

            typeof(ObjectiveStateManager).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.SetValue(null, previousObjectiveInstance);
            typeof(RelationshipStateManager).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.SetValue(null, previousRelationshipInstance);
            typeof(NarrativeEventManager).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.SetValue(null, previousNarrativeInstance);

            foreach (var asset in scratchAssets)
            {
                Object.DestroyImmediate(asset);
            }
        }

        report.AppendLine(failures.Count == 0 ? "=== PASS, 0 failures ===" : $"=== FAIL: {failures.Count} failure(s) ===");
        return report.ToString();
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        field.SetValue(target, value);
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
