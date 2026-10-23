#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Offline UI/scene smoke fixture, not a claimed human or headset playthrough.</summary>
public sealed class CompetitiveFlowSmokeTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public int Failures { get; private set; }
    public string Report => report.ToString();
    readonly StringBuilder report = new();
    void Check(string name, bool ok) { string line = (ok ? "PASS: " : "FAIL: ") + name; report.AppendLine(line); Debug.Log("Competitive smoke: " + line); if (!ok) Failures++; }
    public void Run() => StartCoroutine(GuardedTests());
    IEnumerator GuardedTests()
    {
        var tests = Tests();
        while (true)
        {
            bool next = false, failed = false; object current = null;
            try { next = tests.MoveNext(); if (next) current = tests.Current; }
            catch (System.Exception error) { Check("Smoke fixture exception: " + error, false); failed = true; }
            if (failed) { IsDone = true; yield break; }
            if (!next) yield break;
            yield return current;
        }
    }
    static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    static void Click(CompetitiveMenuUI menu, string text)
    {
        var pointer = Field<WorldPanelPointer>(menu, "pointer");
        pointer.Targets.First(t => t.rect.GetComponentInChildren<TMP_Text>()?.text == text).onClick();
    }
    IEnumerator Tests()
    {
        yield return null; yield return null;
        var edition = CompetitiveEdition.Instance;
        var menu = FindAnyObjectByType<CompetitiveMenuUI>();
        Check("Competitive lobby installed", edition != null && menu != null);
        Check("Course panels hidden", FindObjectsByType<StudentIdentificationPanel>(FindObjectsInactive.Include).All(p => !p.IsShowing));
        Check("Course status hidden", !FindAnyObjectByType<StartMenuController>().enabled);
        Check("Directory sync disabled", !FindAnyObjectByType<StudentDirectoryService>().enabled);
        Check("Competitive save path isolated", CompetitiveEdition.SaveRoot.Contains("competitive-smoke"));
        yield return new WaitForSecondsRealtime(.4f);
        Click(menu, "Leaderboard");
        Check("Leaderboard opens with empty state", Field<string>(menu, "screen") == "board");
        yield return new WaitForSecondsRealtime(.4f); Click(menu, "Back to menu");
        yield return new WaitForSecondsRealtime(.4f); Click(menu, "Start investigation");
        Check("Username keyboard replaces course code", Field<string>(menu, "screen") == "name");
        yield return new WaitForSecondsRealtime(.4f); Click(menu, "Clear");
        yield return new WaitForSecondsRealtime(.4f); Click(menu, "A");
        yield return new WaitForSecondsRealtime(.4f); Click(menu, "A");
        Check("VR keyboard enters username", Field<string>(menu, "username") == "AA");
        yield return new WaitForSecondsRealtime(.4f); Click(menu, "Begin investigation");
        yield return null;
        while (SceneManager.GetActiveScene().name != "CSI_Environment") yield return null;
        yield return null; yield return null;
        Check("Username carried into investigation", edition.Username == "AA");
        edition.SendMessage("OnApplicationFocus", true); // Simulate an active player in a hidden batch editor.
        Check("Investigation unlocks without identity code", FindAnyObjectByType<PreSessionFlowController>().SessionUnlocked);
        Check("Gameplay starts at original FR handover", SceneTurnover.Phase == ScenarioPhase.IOCTurnover);
        Check("Research scoring remains active", FinalDeductionAssessment.ActiveInScene);
        Check("Course upload refuses competitive run", AssessmentUploadService.Instance.QueueSessionForUpload("unused", "", "") == null);
        Check("Raw logs isolated", SessionLogger.Instance.ActiveLogFilePath.StartsWith(CompetitiveEdition.SaveRoot));
        Check("Course panels remain hidden in scenario", FindObjectsByType<StudentIdentificationPanel>(FindObjectsInactive.Include).All(p => !p.IsShowing));
        Check("Incomplete run cannot rank", edition.CompleteRun() == null && edition.History.runs.Count == 0);
        var pause = FindAnyObjectByType<SettingsPauseMenu>();
        STCSNotificationUI.Instance?.HideImmediately();
        pause.Show();
        yield return null;
        double pausedAt = Field<double>(edition, "elapsed");
        yield return new WaitForSecondsRealtime(.25f);
        Check("Explicit pause freezes the run timer", pause.IsOpen && System.Math.Abs(Field<double>(edition, "elapsed") - pausedAt) < .01);
        pause.Close();
        yield return new WaitForSecondsRealtime(.25f);
        Check("Timer resumes after closing settings", Field<double>(edition, "elapsed") > pausedAt);
        edition.SendMessage("OnApplicationFocus", false);
        double focusLostAt = Field<double>(edition, "elapsed");
        yield return new WaitForSecondsRealtime(.25f);
        Check("Losing application focus freezes the timer", System.Math.Abs(Field<double>(edition, "elapsed") - focusLostAt) < .01);
        edition.SendMessage("OnApplicationFocus", true);
        SceneTurnover.Controller.CompleteWithoutDialogue();
        AutomatedTestSandbox.Activate(nameof(CompetitiveFlowSmokeTest));
        ArrivalPPEStation.Instance.PrepareDevelopmentGloves();
        var register = FindAnyObjectByType<SceneCordon>().AccessRegister;
        Check("Named investigator can sign the scene register without course identity", register.Authorize(out _));
        Check("Scene register uses chosen username", register.StudentName == "AA");
        yield return SceneManager.LoadSceneAsync("Main"); yield return null; yield return null;
        Check("Returning to lobby preserves username", edition.Username == "AA");
        Check("Returning unfinished adds no leaderboard record", edition.History.runs.Count == 0);
        Check("Lobby rebuilt on return", FindAnyObjectByType<CompetitiveMenuUI>() != null);
        // A synthetic scored closeout fixture verifies results/persistence independently
        // of the earlier real menu and scene-entry checks; it is not a full case playthrough.
        yield return SceneManager.LoadSceneAsync("CSI_Environment"); yield return null; yield return null;
        var fixture = new GameObject("Score fixture").AddComponent<FinalDeductionAssessmentTest>();
        var input = (DeductionAssessmentInput)typeof(FinalDeductionAssessmentTest).GetMethod("Fixture", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(fixture, new object[] { null });
        typeof(FinalDeductionAssessment).GetField("frozen", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, input);
        typeof(FinalDeductionAssessment).GetField("frozenSessionId", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, SessionLogger.Instance.SessionId);
        typeof(SceneTurnover).GetProperty("Phase").SetValue(null, ScenarioPhase.InvestigationComplete);
        var completed = edition.CompleteRun();
        Check("Scored closeout records username, score and time", completed != null && completed.username == "AA" && completed.score == 100 && completed.seconds >= 0);
        Check("Repeated closeout saves only once", edition.CompleteRun() == completed && edition.History.runs.Count == 1);
        string path = System.IO.Path.Combine(CompetitiveEdition.SaveRoot, "runs.json");
        Check("Completed run saved to competitive history", System.IO.File.Exists(path));
        var restored = JsonUtility.FromJson<CompetitiveRunHistory>(System.IO.File.ReadAllText(path));
        Check("Unity JSON preserves saved score and run identity", restored.runs.Count == 1 && restored.runs[0].runId == completed.runId && restored.runs[0].score == 100);
        string results = edition.ResultsPage();
        Check("Final results include score, time and personal best", results.Contains("100 / 100") && results.Contains("Time:") && results.Contains("Personal best:"));
        report.AppendLine(Failures == 0 ? "=== PASS ===" : "=== FAIL ==="); IsDone = true;
    }
}
#endif
