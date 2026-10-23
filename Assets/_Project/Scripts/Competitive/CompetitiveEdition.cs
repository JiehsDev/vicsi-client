using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Competitive branch only. No course identity, directory sync or research upload.</summary>
public sealed class CompetitiveEdition : MonoBehaviour
{
    public static bool Enabled => true;
    public static CompetitiveEdition Instance { get; private set; }
    public static string SaveRoot => Environment.GetCommandLineArgs().Contains("-competitiveSmoke")
        ? Path.Combine(Application.temporaryCachePath, "competitive-smoke", smokeSession)
        : Path.Combine(Application.persistentDataPath, "competitive-v1");
    static readonly string smokeSession = Guid.NewGuid().ToString("N");
    public static string RulesKey => string.Join("|", FinalDeductionAssessment.Rules.scenarioId,
        FinalDeductionAssessment.Rules.scenarioVersion, FinalDeductionAssessment.Rules.contentVersion,
        FinalDeductionAssessment.Rules.scoringRulesVersion, "guided-v1");
    public CompetitiveRunHistory History { get; private set; } = new();
    public CompetitiveRunRecord LastRun { get; private set; }
    public string Username { get; private set; }
    public double ElapsedSeconds => LastRun?.seconds ?? elapsed;
    public string StorageMessage { get; private set; }
    public bool ShowDemoRankings => PlayerPrefs.GetInt("competitive.demoRankings", 1) != 0;
    public System.Collections.Generic.List<CompetitiveRunRecord> Leaderboard(string rules) =>
        CompetitiveRanking.Leaderboard(History, rules, ShowDemoRankings);
    public void SetDemoRankings(bool enabled)
    {
        PlayerPrefs.SetInt("competitive.demoRankings", enabled ? 1 : 0);
        PlayerPrefs.Save();
    }
    string playerId, runId;
    double elapsed;
    bool running, focused = true, applicationPaused;
    SettingsPauseMenu settings;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => Instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        if (Instance == null) new GameObject("Competitive edition").AddComponent<CompetitiveEdition>();
    }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this; DontDestroyOnLoad(gameObject);
        Load(); SceneManager.sceneLoaded += SceneLoaded;
    }
    void OnDestroy() { SceneManager.sceneLoaded -= SceneLoaded; if (Instance == this) Instance = null; }

    void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        running = false; LastRun = null; settings = null;
        if (scene.name == "Main") gameObject.AddComponent<CompetitiveMenuUI>();
        else if (scene.name == "CSI_Environment")
        {
            if (!CompetitiveRanking.ValidUsername(Username))
            { SceneManager.LoadScene("Main"); return; }
            runId = Guid.NewGuid().ToString(); elapsed = 0; running = !SceneTestStart.IsActive;
        }
    }

    void Update()
    {
        if (!running) return;
        if (settings == null) settings = FindAnyObjectByType<SettingsPauseMenu>();
        if (focused && !applicationPaused && (settings == null || !settings.IsOpen)) elapsed += Time.unscaledDeltaTime;
        if (SceneTurnover.Phase == ScenarioPhase.InvestigationComplete) CompleteRun();
    }
    void OnApplicationFocus(bool value) => focused = value;
    void OnApplicationPause(bool value) => applicationPaused = value;

    public bool SetUsername(string value)
    {
        value = value?.Trim();
        if (!CompetitiveRanking.ValidUsername(value)) return false;
        Username = value;
        // Names identify local profiles only; no identity or authentication is claimed.
        string key = "competitive.profile." + value.ToLowerInvariant();
        playerId = PlayerPrefs.GetString(key, "");
        if (!Guid.TryParse(playerId, out _)) { playerId = Guid.NewGuid().ToString(); PlayerPrefs.SetString(key, playerId); }
        PlayerPrefs.SetString("competitive.username", value); PlayerPrefs.Save();
        return true;
    }

    public CompetitiveRunRecord CompleteRun()
    {
        if (LastRun != null) return LastRun;
        if (!running || SceneTurnover.Phase != ScenarioPhase.InvestigationComplete) return null;
        running = false;
        var result = FinalDeductionAssessment.FinalResult(true);
        if (!result.hasFinalScore) { StorageMessage = "This run has no verified final score and cannot rank."; return null; }
        LastRun = new CompetitiveRunRecord { runId = runId, playerId = playerId, username = Username,
            completedUtc = DateTime.UtcNow.ToString("O"), rulesVersion = RulesKey,
            score = result.totalScore, seconds = elapsed,
            recorderPoints = EvidenceStateManager.Instance.AllRecords.Sum(r => r.recorderDocumentation?.earnedPoints ?? 0) };
        if (CompetitiveRanking.Add(History, LastRun)) Save();
        return LastRun;
    }

    public string ResultsPage()
    {
        var run = CompleteRun();
        if (run == null) return "Run results\n\n" + (StorageMessage ?? "Finish the scene closeout to record this run.");
        var board = Leaderboard(run.rulesVersion);
        var best = board.Find(r => r.playerId == run.playerId);
        return $"Run results\n\n{run.username}\nInvestigation score: {run.score:0.##} / 100\nTime: {CompetitiveRanking.FormatTime(run.seconds)}"
            + $"\nRecorder notes: {run.recorderPoints:0} / 24\n\nLocal rank: {board.FindIndex(r => r.playerId == run.playerId) + 1}"
            + (best != null ? $"\nPersonal best: {best.score:0.##} / 100 · {CompetitiveRanking.FormatTime(best.seconds)}" : "")
            + "\n\nHigher scores rank first. Time breaks equal scores.\n" + StorageMessage;
    }

    void Load()
    {
        try
        {
            string path = Path.Combine(SaveRoot, "runs.json");
            if (File.Exists(path))
            {
                History = JsonUtility.FromJson<CompetitiveRunHistory>(File.ReadAllText(path));
                if (History == null || History.version != 1 || History.runs == null || History.runs.Any(r => !CompetitiveRanking.ValidRun(r)))
                    throw new InvalidDataException("Invalid run history");
            }
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
        {
            // Preserve unreadable data; never overwrite it with an empty history.
            History = new(); StorageMessage = "Saved history could not be read. New results will remain available for this launch.";
            saveBlocked = true;
        }
        Username = PlayerPrefs.GetString("competitive.username", "");
        if (CompetitiveRanking.ValidUsername(Username)) SetUsername(Username);
    }
    bool saveBlocked;
    void Save()
    {
        if (saveBlocked) return;
        try
        {
            Directory.CreateDirectory(SaveRoot);
            string path = Path.Combine(SaveRoot, "runs.json"), temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(History, true));
            if (File.Exists(path)) File.Replace(temp, path, path + ".bak"); else File.Move(temp, path);
            StorageMessage = "Saved on this device.";
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        { StorageMessage = "Could not save to disk. This result is available for this launch."; }
    }
}
