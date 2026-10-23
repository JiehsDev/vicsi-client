// Assets/_Project/Scripts/Testing/NarrativeEventManagerTest.cs
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>
/// Focused, re-runnable checks for NarrativeEvent/NarrativeEventManager, plus the
/// RequirementType.NarrativeEventCompleted integration - same "checked-in test beats a
/// memory of one" reasoning as GreyboxFlowTest/RequirementEvaluatorTest/
/// RelationshipStateManagerTest. Builds its own throwaway NarrativeEventManager and
/// NarrativeEvent entries, never touching the real scene-registered STCSManager or any
/// real DialoguePool content.
///
/// Coroutine-driven rather than a single synchronous method, unlike this project's other
/// test scripts - NarrativeEventManager's play-out genuinely spans real frames (even with
/// every delay set to 0, a coroutine always yields at least once), so there is no way to
/// observe a fired event's Completed state within one synchronous call. Call RunAllTests()
/// to start, then poll IsDone/Report from a later call - see the class's own two-step
/// usage note below.
///
/// Usage from the editor / MCP execute_code:
///     var go = new GameObject("NarrativeTest");
///     var test = go.AddComponent&lt;NarrativeEventManagerTest&gt;();
///     test.RunAllTests();
///     // ... in a LATER call, after real time has passed ...
///     if (test.IsDone) Debug.Log(test.Report);
/// </summary>
public class NarrativeEventManagerTest : MonoBehaviour
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
        var report = new StringBuilder();
        report.AppendLine("=== NarrativeEventManagerTest ===");

        GameObject managerGo = null;
        var scratchAssets = new List<ScriptableObject>();
        var completionOrder = new List<string>();

        // This test's own throwaway NarrativeEventManager would clobber (and, on
        // Destroy, permanently null out) a REAL scene singleton's static Instance if one
        // ever exists - confirmed as a real, live consequence once EndingManager got a
        // real scene instance, not a hypothetical. Captured and force-restored
        // regardless, so this stays safe once a real instance exists here too.
        var previousInstance = NarrativeEventManager.Instance;

        System.Action<string> onCompleted = id => completionOrder.Add(id);
        NarrativeEventManager.OnNarrativeEventCompleted += onCompleted;

        NarrativeEventManager manager = null;

        try
        {
            var pool = ScriptableObject.CreateInstance<DialogueLinePool>();
            pool.lines = new[] { "Test line." };
            scratchAssets.Add(pool);

            var autoAlwaysEligible = new NarrativeEvent
            {
                eventId = "test-auto-always",
                pool = pool,
                triggerMode = NarrativeTriggerMode.Automatic,
                trigger = new RequirementNode { kind = RequirementNodeKind.Group, operatorType = RequirementLogicalOperator.All },
                priority = 1,
                repeatable = false
            };

            var autoNeverEligible = new NarrativeEvent
            {
                eventId = "test-auto-never",
                pool = pool,
                triggerMode = NarrativeTriggerMode.Automatic,
                trigger = new RequirementNode
                {
                    kind = RequirementNodeKind.Leaf,
                    requirementType = RequirementType.EvidenceStatusAtOrAbove,
                    evidenceId = "NEVER-REGISTERED",
                    requiredStatus = EvidenceStatus.Found
                },
                priority = 5
            };

            var manualOnly = new NarrativeEvent
            {
                eventId = "test-manual",
                pool = pool,
                triggerMode = NarrativeTriggerMode.Manual,
                priority = 10
            };

            var higherPriority = new NarrativeEvent
            {
                eventId = "test-priority-high",
                pool = pool,
                triggerMode = NarrativeTriggerMode.Manual,
                priority = 10
            };
            var lowerPriority = new NarrativeEvent
            {
                eventId = "test-priority-low",
                pool = pool,
                triggerMode = NarrativeTriggerMode.Manual,
                priority = 1
            };
            // Fired first, with a real delay, purely so it is still isPlaying (holding
            // the "only one plays at a time" slot) at the moment low and high are BOTH
            // fired - see the test body's own comment on why priority can only be
            // observed between items simultaneously WAITING, never as preemption of
            // something already mid-play.
            var priorityBlocker = new NarrativeEvent
            {
                eventId = "test-priority-blocker",
                pool = pool,
                triggerMode = NarrativeTriggerMode.Manual,
                delaySeconds = 0.2f
            };

            var repeatableEvent = new NarrativeEvent
            {
                eventId = "test-repeatable",
                pool = pool,
                triggerMode = NarrativeTriggerMode.Manual,
                repeatable = true
            };

            var eventSet = ScriptableObject.CreateInstance<NarrativeEventSet>();
            eventSet.events = new List<NarrativeEvent> { autoAlwaysEligible, autoNeverEligible, manualOnly, higherPriority, lowerPriority, priorityBlocker, repeatableEvent };
            scratchAssets.Add(eventSet);

            managerGo = new GameObject("TempNarrativeEventManager");
            managerGo.SetActive(false);
            manager = managerGo.AddComponent<NarrativeEventManager>();

            var field = typeof(NarrativeEventManager).GetField("eventSet", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(manager, eventSet);

            managerGo.SetActive(true); // Awake + OnEnable (+ initial RefreshEligibility) run

            // Automatic event with an empty (vacuously true) trigger should already be
            // queued from the initial RefreshEligibility fired during OnEnable above -
            // with delaySeconds 0 and a single random line, its PlayRoutine never hits a
            // yield at all, so it completes fully SYNCHRONOUSLY within that OnEnable call,
            // before this coroutine's first line even runs. That is correct, intended
            // behavior (a zero-delay event has nothing to wait for), not a bug - so there
            // is no "not yet completed" moment to observe here, unlike every other check
            // in this file.
            yield return WaitUntilOrTimeout(() => manager.IsCompleted("test-auto-always"), 3f);
            Check(failures, report, "Automatic event with an always-true trigger completes on its own", manager.IsCompleted("test-auto-always"));

            yield return null;
            Check(failures, report, "Automatic event with an unsatisfiable trigger never fires", !manager.IsCompleted("test-auto-never"));

            Check(failures, report, "Manual event does not fire on its own (never queued/completed without Fire())", !manager.IsCompleted("test-manual"));
            manager.Fire("test-manual");
            yield return WaitUntilOrTimeout(() => manager.IsCompleted("test-manual"), 3f);
            Check(failures, report, "Manual event completes after Fire()", manager.IsCompleted("test-manual"));

            // Fire the delayed blocker first so it is still isPlaying (mid WaitForSeconds)
            // when both low and high are fired immediately after in the same frame -
            // only then do they actually coexist in the pending queue together, which is
            // the only situation priority ordering can affect (see priorityBlocker's own
            // comment above).
            manager.Fire("test-priority-blocker");
            manager.Fire("test-priority-low");
            manager.Fire("test-priority-high");
            yield return WaitUntilOrTimeout(() =>
                manager.IsCompleted("test-priority-blocker") && manager.IsCompleted("test-priority-high") && manager.IsCompleted("test-priority-low"), 5f);
            int highIndex = completionOrder.IndexOf("test-priority-high");
            int lowIndex = completionOrder.IndexOf("test-priority-low");
            Check(failures, report, $"Higher-priority event plays before lower-priority when both are queued together (order: {string.Join(",", completionOrder)})",
                highIndex >= 0 && lowIndex >= 0 && highIndex < lowIndex);

            manager.Fire("test-repeatable");
            yield return WaitUntilOrTimeout(() => manager.IsCompleted("test-repeatable"), 3f);
            manager.Fire("test-repeatable");
            yield return WaitUntilOrTimeout(() => completionOrder.FindAll(id => id == "test-repeatable").Count >= 2, 3f);
            Check(failures, report, "Repeatable event fires more than once",
                completionOrder.FindAll(id => id == "test-repeatable").Count >= 2);

            manager.Fire("test-manual"); // not repeatable, already fired once above
            yield return null;
            Check(failures, report, "Non-repeatable event does not fire a second time",
                completionOrder.FindAll(id => id == "test-manual").Count == 1);

            // --- RequirementType.NarrativeEventCompleted integration ---
            var requirementLeaf = new RequirementNode
            {
                kind = RequirementNodeKind.Leaf,
                requirementType = RequirementType.NarrativeEventCompleted,
                narrativeEventId = "test-auto-always"
            };
            Check(failures, report, "RequirementType.NarrativeEventCompleted reads IsCompleted correctly (true case)",
                RequirementEvaluator.IsSatisfied(requirementLeaf));

            var requirementLeafFalse = new RequirementNode
            {
                kind = RequirementNodeKind.Leaf,
                requirementType = RequirementType.NarrativeEventCompleted,
                narrativeEventId = "test-auto-never"
            };
            Check(failures, report, "RequirementType.NarrativeEventCompleted reads IsCompleted correctly (false case)",
                !RequirementEvaluator.IsSatisfied(requirementLeafFalse));

            var requirementLeafMissingId = new RequirementNode
            {
                kind = RequirementNodeKind.Leaf,
                requirementType = RequirementType.NarrativeEventCompleted,
                narrativeEventId = ""
            };
            var missingIdResult = RequirementEvaluator.Evaluate(requirementLeafMissingId);
            Check(failures, report, "RequirementType.NarrativeEventCompleted fails closed with MissingNarrativeEventId when blank",
                !missingIdResult.Passed && missingIdResult.Reason == RequirementFailureReason.MissingNarrativeEventId);
        }
        finally
        {
            NarrativeEventManager.OnNarrativeEventCompleted -= onCompleted;

            if (managerGo != null)
            {
                Object.Destroy(managerGo);
            }

            typeof(NarrativeEventManager).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.SetValue(null, previousInstance);

            foreach (var asset in scratchAssets)
            {
                Object.DestroyImmediate(asset);
            }
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
