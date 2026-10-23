// Assets/_Project/Scripts/Testing/EndingManagerTest.cs
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>
/// Focused, re-runnable checks for EndingDefinition/EndingManager resolution and the
/// Submit Conclusion gate/picker flow - same "checked-in test beats a memory of one"
/// reasoning as this project's other test scripts. Builds its own throwaway
/// EndingManager/CompletionScreenUI/NarrativeEventManager, never touching the real
/// scene-registered CompletionScreenUI (so this test never actually shows the real
/// completion screen to whoever is watching Play mode).
///
/// ONE real singleton IS touched deliberately: HypothesisCheckpointManager.Instance -
/// TrySubmitConclusion calls FireSubmissionInitiatedCheckpoints() internally as its own
/// real, first step (see EndingManager's own class comment), and that call is designed
/// to be safe and idempotent - it is the exact same call GreyboxFlowTest already makes
/// directly, and checkpoints fire at most once each regardless of how many times it is
/// called.
///
/// Coroutine-driven, same reasoning as NarrativeEventManagerTest: the conclusion-picker
/// sub-flow spans at least one real frame (TrySubmitConclusion starts a coroutine that
/// only resolves the ending after SelectConclusionForTest is called AND the coroutine's
/// wait-loop notices on a later frame), so this cannot all be asserted synchronously.
///
/// Run it from the editor via:
///     var go = new GameObject("t"); var t = go.AddComponent&lt;EndingManagerTest&gt;();
///     t.RunAllTests();
///     // poll t.IsDone / t.Report from a LATER call
/// </summary>
public class EndingManagerTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }

    public void RunAllTests()
    {
        StartCoroutine(RunAllTestsRoutine());
    }

    private IEnumerator RunAllTestsRoutine()
    {
        var failures = new List<string>();
        if (!MeasurementTestUtil.CompleteSceneSketch()) failures.Add("Prior full sketch fixture could not submit.");
        var report = new StringBuilder();
        report.AppendLine("=== EndingManagerTest ===");
        AutomatedTestSandbox.Activate(nameof(EndingManagerTest));   // submits conclusions

        var scratchAssets = new List<ScriptableObject>();
        GameObject managerGo = null, screenGo = null, narGo = null, conclusionGo = null;

        // This test's own throwaway EndingManager/NarrativeEventManager instances set
        // their type's static Instance in Awake, exactly like the real scene singletons
        // do - if a real one already exists (EndingManager does, live-wired this sub-
        // project; NarrativeEventManager doesn't yet, but a future sub-project may add
        // one), creating a temp instance CLOBBERS Instance, and destroying the temp
        // instance afterward NULLS IT OUT PERMANENTLY (its own OnDestroy's "if
        // (Instance == this) Instance = null" guard fires, since Instance now points at
        // the temp one) - breaking the real singleton for the rest of the session, not
        // just for this test. Confirmed as a real, live consequence, not a hypothetical:
        // this exact bug broke the real scene EndingManager.Instance the first time this
        // test ran. Captured here and force-restored in finally, regardless of whether a
        // real instance exists right now.
        var previousEndingManagerInstance = EndingManager.Instance;
        var previousNarrativeEventManagerInstance = NarrativeEventManager.Instance;
        var previousConclusionStateManagerInstance = ConclusionStateManager.Instance;

        try
        {
            // --- Rigs: a temp CompletionScreenUI (never the real scene one) and a temp
            // NarrativeEventManager (for the followUpNarrativeEventId check). ---
            screenGo = new GameObject("TempCompletionScreenUI");
            var completionScreen = screenGo.AddComponent<CompletionScreenUI>();

            var pool = ScriptableObject.CreateInstance<DialogueLinePool>();
            pool.lines = new[] { "Test line." };
            scratchAssets.Add(pool);
            var followUpEvent = new NarrativeEvent { eventId = "test-ending-followup", pool = pool, triggerMode = NarrativeTriggerMode.Manual };
            var eventSet = ScriptableObject.CreateInstance<NarrativeEventSet>();
            eventSet.events = new List<NarrativeEvent> { followUpEvent };
            scratchAssets.Add(eventSet);

            narGo = new GameObject("TempNarrativeEventManager");
            narGo.SetActive(false);
            var narManager = narGo.AddComponent<NarrativeEventManager>();
            SetPrivateField(narManager, "eventSet", eventSet);
            narGo.SetActive(true);

            // --- EndingSet content ---
            // Deliberately given an UNSATISFIABLE resolutionRequirement, not the trivial
            // empty-ALL default - an isFallback ending whose own condition IS trivially
            // true would always win via ordinary priority-match, never actually
            // exercising the true "nothing else matched, use the rescue" Fallback rule
            // path this test means to check. Caught as a real self-authored test mistake
            // before this fix, not a bug in ResolveEnding itself.
            var alwaysEligible = new EndingDefinition
            {
                endingId = "ending-fallback",
                priority = 0,
                resolutionRequirement = new RequirementNode
                {
                    kind = RequirementNodeKind.Leaf,
                    requirementType = RequirementType.NarrativeEventCompleted,
                    narrativeEventId = "test-fallback-never-fires"
                },
                endingText = "Fallback ending text.",
                isFallback = true
            };
            var highPriorityGated = new EndingDefinition
            {
                endingId = "ending-high-priority",
                priority = 10,
                resolutionRequirement = new RequirementNode
                {
                    kind = RequirementNodeKind.Leaf,
                    requirementType = RequirementType.NarrativeEventCompleted,
                    narrativeEventId = "test-ending-followup" // reuse as a controllable on/off switch
                },
                endingText = "High priority ending text.",
                followUpNarrativeEventId = "test-ending-followup"
            };
            var conclusionGated = new EndingDefinition
            {
                endingId = "ending-conclusion-gated",
                priority = 20,
                resolutionRequirement = new RequirementNode { kind = RequirementNodeKind.Group, operatorType = RequirementLogicalOperator.All },
                requiredConclusionId = "conclusion-b",
                endingText = "Conclusion-gated ending text."
            };

            var endingSet = ScriptableObject.CreateInstance<EndingSet>();
            endingSet.submissionRequirement = new RequirementNode { kind = RequirementNodeKind.Group, operatorType = RequirementLogicalOperator.All };
            endingSet.conclusionOptions = new List<ConclusionOption>
            {
                new ConclusionOption { conclusionId = "conclusion-a", text = "Conclusion A" },
                new ConclusionOption { conclusionId = "conclusion-b", text = "Conclusion B" }
            };
            endingSet.endings = new List<EndingDefinition> { alwaysEligible, highPriorityGated, conclusionGated };
            scratchAssets.Add(endingSet);

            managerGo = new GameObject("TempEndingManager");
            var manager = managerGo.AddComponent<EndingManager>();
            SetPrivateField(manager, "endingSet", endingSet);
            SetPrivateField(manager, "completionScreen", completionScreen);

            // The current picker reads available Case conclusions, not legacy EndingSet options.
            var caseConclusion = ScriptableObject.CreateInstance<ConclusionDefinition>();
            caseConclusion.conclusionId = "conclusion-b";
            caseConclusion.displayName = "Conclusion B";
            caseConclusion.level = ConclusionLevel.Case;
            caseConclusion.availabilityRequirement = new RequirementNode { kind = RequirementNodeKind.Group, operatorType = RequirementLogicalOperator.All };
            scratchAssets.Add(caseConclusion);
            conclusionGo = new GameObject("TempConclusionStateManager");
            conclusionGo.SetActive(false);
            var conclusionManager = conclusionGo.AddComponent<ConclusionStateManager>();
            SetPrivateField(conclusionManager, "sceneConclusionDefinitions", new List<ConclusionDefinition> { caseConclusion });
            conclusionGo.SetActive(true);

            // --- Pure resolution logic, no coroutine needed ---
            var r1 = manager.ResolveEnding(null);
            Check(failures, report, "With nothing else satisfied, resolution falls back to the isFallback ending",
                r1.Ending?.endingId == "ending-fallback" && r1.Rule == EndingResolutionRule.Fallback);

            var r2 = manager.ResolveEnding("conclusion-b");
            Check(failures, report, "requiredConclusionId match wins over the fallback when its own priority is higher",
                r2.Ending?.endingId == "ending-conclusion-gated" && r2.Rule == EndingResolutionRule.PriorityMatch);

            var r3 = manager.ResolveEnding("conclusion-a");
            Check(failures, report, "requiredConclusionId that does NOT match excludes that ending, falling back",
                r3.Ending?.endingId == "ending-fallback" && r3.Rule == EndingResolutionRule.Fallback);

            // Fire the shared narrative trigger - unlocks ending-high-priority's own
            // resolutionRequirement (priority 10, still below ending-conclusion-gated's 20).
            narManager.Fire("test-ending-followup");
            var r4 = manager.ResolveEnding(null);
            Check(failures, report, $"Highest satisfied priority wins among multiple eligible endings (got {r4.Ending?.endingId})",
                r4.Ending?.endingId == "ending-high-priority" && r4.Rule == EndingResolutionRule.PriorityMatch);

            var r5 = manager.ResolveEnding("conclusion-b");
            Check(failures, report, "A higher-priority conclusion-gated match still wins over a satisfied-but-lower-priority ending",
                r5.Ending?.endingId == "ending-conclusion-gated");

            // --- Submission gate ---
            SetPrivateField(manager, "endingSet", null);
            Check(failures, report, "TrySubmitConclusion with no EndingSet assigned does not throw", true);
            manager.TrySubmitConclusion();
            SetPrivateField(manager, "endingSet", endingSet);

            manager.TrySubmitConclusion();
            yield return null;
            Check(failures, report, "Submit Conclusion stays blocked before theory submission despite an available Case conclusion",
                conclusionManager.IsAvailable("conclusion-b") && !EvidenceBoardController.HasEverSubmittedTheory
                && manager.GetPickerOptionIdsForTest().Count == 0 && !completionScreen.IsShowing);

            // --- Full flow through the real picker coroutine ---
            RelationshipReviewTestUtil.ConfirmAvailable();
            Object.FindFirstObjectByType<EvidenceBoardController>(FindObjectsInactive.Include)?.SubmitTheory();
            manager.TrySubmitConclusion();
            yield return WaitUntilOrTimeout(() => manager.GetPickerOptionIdsForTest().Count > 0, 3f);
            bool selected = manager.SelectConclusionForTest("conclusion-b");
            Check(failures, report, "SelectConclusionForTest succeeds while the picker is showing", selected);

            yield return WaitUntilOrTimeout(() => completionScreen.IsShowing, 3f);
            Check(failures, report, "Full Submit Conclusion flow (gate -> picker -> resolve -> ShowEnding) shows the completion screen",
                completionScreen.IsShowing);

            completionScreen.DismissForTest();
        }
        finally
        {
            if (managerGo != null) Object.Destroy(managerGo);
            if (screenGo != null) Object.Destroy(screenGo);
            if (narGo != null) Object.Destroy(narGo);
            if (conclusionGo != null) Object.Destroy(conclusionGo);
            foreach (var asset in scratchAssets)
            {
                Object.DestroyImmediate(asset);
            }

            // Force-restore regardless of what Destroy's own OnDestroy guard did -
            // see the comment above previousEndingManagerInstance.
            RestoreStaticInstance(typeof(EndingManager), previousEndingManagerInstance);
            RestoreStaticInstance(typeof(NarrativeEventManager), previousNarrativeEventManagerInstance);
            RestoreStaticInstance(typeof(ConclusionStateManager), previousConclusionStateManagerInstance);
        }

        report.AppendLine(failures.Count == 0 ? "=== PASS, 0 failures ===" : $"=== FAIL: {failures.Count} failure(s) ===");
        Report = report.ToString();
        IsDone = true;
    }

    private static IEnumerator WaitUntilOrTimeout(System.Func<bool> condition, float timeoutSeconds)
    {
        float elapsed = 0f;
        while (!condition() && elapsed < timeoutSeconds)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        field.SetValue(target, value);
    }

    /// <summary>Force-sets a type's static "Instance { get; private set; }" property - see the comment above previousEndingManagerInstance in RunAllTestsRoutine for why this is necessary.</summary>
    private static void RestoreStaticInstance(System.Type type, object value)
    {
        var property = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        property?.SetValue(null, value);
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
