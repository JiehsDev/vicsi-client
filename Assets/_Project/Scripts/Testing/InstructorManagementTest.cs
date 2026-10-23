// Assets/_Project/Scripts/Testing/InstructorManagementTest.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Phase 5 checks through the REAL InstructorManagementUI (PIN keypad, tabs, confirm
/// dialog, PIN re-check, details) wired to isolated test services: InstructorPinStore,
/// StudentDirectoryService and AssessmentUploadService on temp folders with a fake server.
/// Covers Tests A, B, C, I, K, L, M, N, O, P plus the Students / Sessions / Status tabs.
/// Never touches the real PIN file, directory, queue, owner file, raw logs or network.
///
/// Run in Play mode (alone - shares static instances with the other upload suites):
///     var t = new GameObject("IMT").AddComponent&lt;InstructorManagementTest&gt;(); t.RunAllTests();
/// </summary>
public class InstructorManagementTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }

    private readonly List<string> failures = new();
    private readonly StringBuilder report = new();

    private const string Juan = "11111111-1111-4111-8111-111111111111";
    private const string Maria = "22222222-2222-4222-8222-222222222222";
    private const string JohnDoe = "99999999-9999-4999-8999-999999999999";
    private const string CodeJuan = "K7M4PQ2D";
    private const string CodeMaria = "ABCD2345";
    private const string DeviceKey = "device-key-for-tests";

    public void RunAllTests()
    {
        IsDone = false;
        StartCoroutine(Run());
    }

    private void Check(string label, bool ok)
    {
        report.AppendLine((ok ? "  PASS: " : "  FAIL: ") + label);
        if (!ok) failures.Add(label);
    }

    // ===================================================================== fake server

    private class FakeServer
    {
        public bool Online = true;
        public readonly HashSet<string> NoAssignment = new();
        public readonly Dictionary<string, string> Tokens = new();
        public readonly Dictionary<string, string> Sessions = new();
        public readonly List<(string id, string name, string code, bool enabled)> Students = new()
        {
            (Juan, "Juan Dela Cruz", CodeJuan, true),
            (Maria, "Maria Santos", CodeMaria, true),
        };
    }

    private class FakeAuth : IVrUploadAuthorizationClient
    {
        private readonly FakeServer s;
        public FakeAuth(FakeServer server) => s = server;
        public Task<VrUploadAuthorizationResult> RequestAsync(string url, string key, VrUploadTokenRequestDto r, float t, CancellationToken ct)
        {
            if (!s.Online) return Task.FromResult(new VrUploadAuthorizationResult { Failure = VrUploadAuthorizationFailure.NoNetwork });
            if (key != DeviceKey) return Task.FromResult(new VrUploadAuthorizationResult { Failure = VrUploadAuthorizationFailure.DeviceUnauthorized });
            if (s.NoAssignment.Contains(r.studentId)) return Task.FromResult(new VrUploadAuthorizationResult { Failure = VrUploadAuthorizationFailure.RetryLater, ServerErrorCode = "NO_ASSIGNMENT" });
            string tok = "tok-" + Guid.NewGuid().ToString("N");
            s.Tokens[tok] = r.studentId;
            return Task.FromResult(new VrUploadAuthorizationResult { UploadToken = tok, ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10) });
        }
    }

    private class FakeUpload : IAssessmentUploadTransport
    {
        private readonly FakeServer s;
        public FakeUpload(FakeServer server) => s = server;
        public Task<AssessmentUploadAttemptResult> UploadAsync(string url, string payload, string token, float t, CancellationToken ct)
        {
            if (!s.Online) return Task.FromResult(AssessmentUploadAttemptResult.Fail(AssessmentUploadErrorCategory.NoNetwork, 0, "offline"));
            if (token == null || !s.Tokens.TryGetValue(token, out var student)) return Task.FromResult(AssessmentUploadAttemptResult.Fail(AssessmentUploadErrorCategory.AuthenticationFailed, 401, "bad"));
            var dto = JsonUtility.FromJson<AssessmentSessionUploadDto>(payload);
            bool dup = s.Sessions.ContainsKey(dto.sessionId);
            if (!dup) s.Sessions[dto.sessionId] = student;
            return Task.FromResult(AssessmentUploadAttemptResult.Ok(new AssessmentSubmissionResponseDto
            {
                success = true,
                data = new AssessmentSubmissionDataDto { receiptId = Guid.NewGuid().ToString(), sessionId = dto.sessionId, duplicate = dup, serverReceivedAtUtc = DateTime.UtcNow.ToString("O") },
            }, 200));
        }
    }

    private class FakeDirectory : IStudentDirectoryTransport
    {
        private readonly FakeServer s;
        public int Calls;
        public FakeDirectory(FakeServer server) => s = server;
        public Task<StudentDirectoryFetchResult> FetchAsync(string url, string key, float t, CancellationToken ct)
        {
            Calls++;
            if (!s.Online) return Task.FromResult(StudentDirectoryFetchResult.Fail(StudentDirectorySyncStatus.NetworkUnavailable));
            string salt = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
            var data = new StudentDirectoryDataDto
            {
                generatedAtUtc = DateTime.UtcNow.ToString("o"),
                codeLookup = new StudentDirectoryCodeLookupDto { algorithm = "PBKDF2-HMAC-SHA256", iterations = 2000, keyBytes = 32, saltBase64 = salt, input = "canonical-code-utf8", output = "hex" },
                students = new List<StudentDirectoryStudentDto>(),
                testStudent = new StudentDirectoryTestStudentDto { studentId = JohnDoe, displayName = "John Doe", classes = new List<StudentDirectoryClassDto>() },
            };
            foreach (var st in s.Students)
            {
                data.students.Add(new StudentDirectoryStudentDto
                {
                    studentId = st.id, displayName = st.name, codeVersion = 1, enabled = st.enabled,
                    codeHash = st.enabled ? StudentAccessCodes.DeriveLookupHash(st.code, salt, 2000, 32) : null,
                    updatedAtUtc = DateTime.UtcNow.ToString("o"),
                    classes = new List<StudentDirectoryClassDto> { new() { classId = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa", className = "CRIM 101", classSection = "A" } },
                });
            }
            return Task.FromResult(StudentDirectoryFetchResult.Ok(200, JsonUtility.ToJson(new StudentDirectoryResponseDto { success = true, data = data })));
        }
    }

    // ===================================================================== helpers

    private string root, queueRoot, logRoot;
    private SessionOwnershipStore owners;

    private static IEnumerator WaitFor(Func<bool> c, float limit = 5f)
    {
        float t = 0f;
        while (!c() && t < limit) { t += Time.unscaledDeltaTime; yield return null; }
    }

    private static IEnumerator Frames(int n)
    {
        for (int i = 0; i < n; i++) yield return null;
    }

    private static void SetStatic(Type type, object value) => type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.SetValue(null, value);

    private static void EnterPin(InstructorManagementUI ui, string pin)
    {
        ui.ClearPin();
        foreach (char c in pin) ui.PressDigit(c - '0');
        ui.SubmitPin();
    }

    private string RawLog(string sessionId, string contentSessionId = null)
    {
        string path = Path.Combine(logRoot, $"session_{sessionId}_20260901-120000.jsonl");
        File.WriteAllText(path, JsonUtility.ToJson(new SessionEvent { sessionId = contentSessionId ?? sessionId, sequenceNumber = 0, eventType = "SessionStarted" }) + "\n");
        return path;
    }

    private string SessionLogForQueue(string sessionId)
    {
        string path = Path.Combine(root, "built_" + sessionId + ".jsonl");
        File.WriteAllText(path,
            JsonUtility.ToJson(new SessionEvent { sessionId = sessionId, sequenceNumber = 0, eventType = "SessionStarted", payload = new List<PayloadEntry> { new() { key = "startedAtUtc", value = DateTime.UtcNow.ToString("O") } } }) + "\n" +
            JsonUtility.ToJson(new SessionEvent { sessionId = sessionId, sequenceNumber = 1, timestampMs = 500, eventType = "SessionEnded" }) + "\n");
        return path;
    }

    private string Bind(string studentId, string name, bool test = false)
    {
        string sid = Guid.NewGuid().ToString();
        owners.TryBind(sid, new IdentifiedStudent { StudentId = studentId, DisplayName = name, IsTestSession = test, Classes = new List<CachedStudentClass>() }, DateTime.UtcNow, out _);
        return sid;
    }

    private string Crafted(AssessmentUploadQueueStore q, string name, AssessmentUploadStatus status, double uploadedDaysAgo, bool hasServerTime, AssessmentUploadErrorCategory cat = AssessmentUploadErrorCategory.None)
    {
        string sid = Guid.NewGuid().ToString();
        owners.TryBind(sid, new IdentifiedStudent { StudentId = Juan, DisplayName = name }, DateTime.UtcNow.AddDays(-61), out _);
        var e = new AssessmentUploadQueueEntry
        {
            sessionId = sid, payloadVersion = 1, status = status, lastErrorCategory = cat, ownerStudentId = Juan, ownerDisplayNameSnapshot = name,
            scenarioIdForDisplay = "CSI-ENVIRONMENT-001",
            // A real (minimal) payload: a resumed upload of this fixture must behave like a real session.
            localPayloadPath = q.WritePayload(sid, JsonUtility.ToJson(new AssessmentSessionUploadDto { payloadVersion = 1, sessionId = sid, scenarioId = "CSI-ENVIRONMENT-001" })),
            createdAtUtc = DateTime.UtcNow.AddDays(-60).ToString("O"), sessionCompletedAtUtc = DateTime.UtcNow.AddDays(-60).ToString("O"),
        };
        if (status == AssessmentUploadStatus.Uploaded && hasServerTime)
        {
            e.uploadConfirmedAtUtc = DateTime.UtcNow.AddDays(-uploadedDaysAgo).ToString("O");
            e.uploadConfirmedLocalAtUtc = e.uploadConfirmedAtUtc;
        }
        q.TryAddEntry(e);
        return sid;
    }

    // ===================================================================== run

    private IEnumerator Run()
    {
        report.AppendLine("=== InstructorManagementTest ===");
        root = Path.Combine(Application.temporaryCachePath, "instructor_test_" + Guid.NewGuid().ToString("N"));
        queueRoot = Path.Combine(root, "queue");
        logRoot = Path.Combine(root, "logs");
        Directory.CreateDirectory(queueRoot);
        Directory.CreateDirectory(logRoot);
        Directory.CreateDirectory(Path.Combine(root, "dir"));
        owners = new SessionOwnershipStore(root);

        var prevLogger = SessionLogger.Instance;
        var prevPairing = PairingIdentityService.Instance;
        var prevUpload = AssessmentUploadService.Instance;
        var prevDirectory = StudentDirectoryService.Instance;
        bool realUploaderWasEnabled = prevUpload != null && prevUpload.enabled;
        if (realUploaderWasEnabled) prevUpload.enabled = false;
        SetStatic(typeof(SessionLogger), null);
        SetStatic(typeof(PairingIdentityService), null);
        var prevReach = ServerReachabilityProbe.Last;
        var spawned = new List<GameObject>();

        try
        {
            var server = new FakeServer();
            var config = ScriptableObject.CreateInstance<AssessmentUploadConfiguration>();
            config.apiBaseUrl = "https://instructor-test.invalid";
            config.environmentName = "Instructor Test";
            config.automaticUploadEnabled = true;
            config.maxRetryAttempts = 3;
            config.initialRetryDelaySeconds = 60f;
            config.maxRetryDelaySeconds = 60f;
            config.maxEventsPerRequest = 5000;
            var scenario = ScriptableObject.CreateInstance<ScenarioDefinition>();
            scenario.scenarioId = "CSI-ENVIRONMENT-001";
            scenario.scenarioVersion = "1.0.0";
            scenario.scoringRulesVersion = "1.0.0";

            // ---- directory service (test instance)
            var dirGo = new GameObject("Directory (instructor test)");
            dirGo.SetActive(false);
            spawned.Add(dirGo);
            var dir = dirGo.AddComponent<StudentDirectoryService>();
            var dirTransport = new FakeDirectory(server);
            string currentKey = DeviceKey;
            dir.ConfigureForTesting(config, dirTransport, new StudentDirectoryStore(Path.Combine(root, "dir")), DeviceKey);
            dirGo.SetActive(true);

            // ---- upload service (test instance; seams before activation)
            var upGo = new GameObject("Uploads (instructor test)");
            upGo.SetActive(false);
            spawned.Add(upGo);
            var uploads = upGo.AddComponent<AssessmentUploadService>();
            typeof(AssessmentUploadService).GetField("configuration", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(uploads, config);
            typeof(AssessmentUploadService).GetField("scenario", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(uploads, scenario);
            uploads.SetQueueRootForTesting(queueRoot);
            uploads.SetTransportForTesting(new FakeUpload(server));
            uploads.SetVrOwnershipSeamsForTesting(new FakeAuth(server), () => currentKey, owners);
            uploads.SetRawLogDirectoryForTesting(logRoot);
            upGo.SetActive(true);
            var store = (AssessmentUploadQueueStore)typeof(AssessmentUploadService).GetField("store", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(uploads);
            if (!Path.GetFullPath(store.PayloadDirectory).StartsWith(Path.GetFullPath(queueRoot)))
                throw new InvalidOperationException("Upload service is not on the test queue - aborting before touching real data.");

            // ---- UI under a temporary canvas
            var canvasGo = new GameObject("Instructor test canvas", typeof(RectTransform), typeof(Canvas));
            spawned.Add(canvasGo);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvasGo.transform).sizeDelta = new Vector2(600, 800);
            var uiGo = new GameObject("InstructorManagementUI (test)");
            uiGo.SetActive(false);
            uiGo.transform.SetParent(canvasGo.transform, false);
            var ui = uiGo.AddComponent<InstructorManagementUI>();
            string pinRoot = Path.Combine(root, "pin");
            Directory.CreateDirectory(pinRoot);
            ui.PinStore = new InstructorPinStore(pinRoot, 5, 2f);
            ui.Directory = () => dir;
            ui.Uploads = () => uploads;
            typeof(InstructorManagementUI).GetField("configuration", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(ui, config);
            uiGo.SetActive(true);
            ui.Build((RectTransform)canvasGo.transform);

            // ============================================== TEST A - default PIN
            ui.Open();
            Check("[A] Instructor Management opens locked at the PIN screen", ui.IsOpen && ui.CurrentView == InstructorManagementUI.View.Pin && !ui.Unlocked && ui.PinPromptText == "Instructor Access");
            ui.PressDigit(1); ui.PressDigit(2);
            Check("[A] PIN slots are masked (no digits shown)", ui.PinSlotsText.Count(c => c == '●') == 2 && !ui.PinSlotsText.Contains("1") && !ui.PinSlotsText.Contains("2"));
            ui.SubmitPin();
            Check("[A] an incomplete/incorrect entry is refused", !ui.Unlocked && ui.FeedbackText.Length > 0);
            EnterPin(ui, "1234");
            Check("[A] wrong PIN -> 'Incorrect PIN.', configured PIN never revealed", !ui.Unlocked && ui.FeedbackText == "Incorrect PIN." && !ui.FeedbackText.Contains("0000"));
            EnterPin(ui, "0000");
            Check("[A] default 0000 unlocks (lands on Headset Status)", ui.Unlocked && ui.CurrentView == InstructorManagementUI.View.Status);

            // ============================================== TEST B - change PIN, restart
            ui.ShowTab(InstructorManagementUI.View.Settings);
            Check("[B] Settings warns while the default PIN is in use", ui.SummaryText.Contains("default"));
            ui.BeginChangePin();
            EnterPin(ui, "0000");
            Check("[B] change flow asks for the new PIN", ui.PinPromptText == "New PIN");
            EnterPin(ui, "4821");
            Check("[B] then for confirmation", ui.PinPromptText == "Confirm New PIN");
            EnterPin(ui, "4821");
            Check("[B] PIN changed, inputs cleared", ui.FeedbackText == "Instructor PIN changed." && ui.CurrentView == InstructorManagementUI.View.Settings);
            var restarted = new InstructorPinStore(pinRoot, 5, 2f);   // a fresh instance reads the persisted file, as after an app restart
            Check("[B] after restart: 0000 rejected, 4821 accepted",
                restarted.Verify("0000").Outcome == InstructorPinStore.VerifyOutcome.Rejected && restarted.Verify("4821").Outcome == InstructorPinStore.VerifyOutcome.Accepted && !restarted.IsDefaultPin);
            string pinFile = File.ReadAllText(restarted.FilePath);
            Check("[B] the PIN file holds a salted hash, not the PIN", !pinFile.Contains("4821") && pinFile.Contains("hashBase64"));
            ui.BeginChangePin(); EnterPin(ui, "4821"); EnterPin(ui, "5555"); EnterPin(ui, "5556");
            Check("[B] a mismatched confirmation leaves the PIN unchanged", ui.FeedbackText.Contains("didn't match") && restarted.Verify("4821").Outcome == InstructorPinStore.VerifyOutcome.Accepted);

            // ============================================== TEST C - cooldown
            ui.Close();
            ui.Open();
            for (int i = 0; i < 5; i++) EnterPin(ui, "9999");
            Check("[C] 5 wrong PINs -> temporary cooldown", ui.FeedbackText.StartsWith("Too many incorrect attempts") && ui.PinStore.CooldownSecondsRemaining > 0);
            EnterPin(ui, "4821");
            Check("[C] even the correct PIN waits during the cooldown", !ui.Unlocked);
            Check("[C] a restart doesn't skip the cooldown", new InstructorPinStore(pinRoot, 5, 2f).CooldownSecondsRemaining > 0);
            yield return new WaitForSecondsRealtime(2.2f);
            EnterPin(ui, "4821");
            Check("[C] after the cooldown the correct PIN works (no permanent lockout)", ui.Unlocked);
            Check("[C] success resets the failure count", ui.PinStore.Verify("1111").AttemptsBeforeCooldown == 4 && ui.PinStore.Verify("4821").Outcome == InstructorPinStore.VerifyOutcome.Accepted);

            // ============================================== Students tab + manual sync
            ui.ShowTab(InstructorManagementUI.View.Students);
            Check("[students] before any sync: empty directory explained", ui.SummaryText.Contains("Active students: 0"));
            ui.SyncDirectory();
            yield return WaitFor(() => dir.Cache.ActiveStudentCount == 2 && !dir.IsSyncing);
            yield return Frames(3);
            Check("[sync] manual Sync Student Directory runs the existing sync and refreshes counts/status live",
                ui.SummaryText.Contains("Active students: 2") && ui.SummaryText.Contains("Succeeded") && ui.FeedbackText == "Student directory synced.");
            var rows = ui.RowTexts;
            Check("[students] rows: name, class, Active - no internal UUID, no code", rows.Count == 2 && rows.Any(r => r.Contains("Juan Dela Cruz") && r.Contains("CRIM 101 - A") && r.Contains("Active"))
                && rows.All(r => !r.Contains(Juan) && !r.Contains(CodeJuan)));
            ui.SetStudentSearch("maria");
            Check("[students] search by name", ui.RowTexts.Count == 1 && ui.RowTexts[0].Contains("Maria Santos"));
            ui.SetStudentSearch("k7m4-pq2d");
            Check("[students] search by access code (hash lookup)", ui.RowTexts.Count == 1 && ui.RowTexts[0].Contains("Juan Dela Cruz"));
            ui.SetStudentSearch("");
            server.Online = false;
            ui.SyncDirectory();
            yield return WaitFor(() => !dir.IsSyncing);
            yield return Frames(3);
            Check("[sync] offline sync shows feedback and keeps the cache", ui.FeedbackText.Contains("Couldn't reach the server") && dir.Cache.ActiveStudentCount == 2);
            server.Online = true;

            // ============================================== sessions fixtures
            var juanPending = Bind(Juan, "Juan Dela Cruz");
            server.Online = false;
            uploads.QueueSessionForUpload(SessionLogForQueue(juanPending), null, null);
            yield return WaitFor(() => uploads.GetAllEntries().Exists(e => e.sessionId == juanPending && e.status == AssessmentUploadStatus.RetryWaiting));
            server.Online = true;
            string juanPendingLog = RawLog(juanPending);
            var doeSession = Bind(JohnDoe, "John Doe", test: true);
            server.NoAssignment.Add(JohnDoe);
            uploads.QueueSessionForUpload(SessionLogForQueue(doeSession), null, null);
            yield return WaitFor(() => uploads.GetAllEntries().Exists(e => e.sessionId == doeSession && e.lastErrorCategory == AssessmentUploadErrorCategory.NoAssignment));
            string doeLog = RawLog(doeSession);
            var legacy = Crafted(store, "Legacy Student", AssessmentUploadStatus.Uploaded, 0, hasServerTime: false);
            string legacyLog = RawLog(legacy);
            string legacyImpostorLog = Path.Combine(logRoot, $"session_{legacy}_20260902-090000.jsonl");
            File.WriteAllText(legacyImpostorLog, JsonUtility.ToJson(new SessionEvent { sessionId = Guid.NewGuid().ToString(), eventType = "SessionStarted" }) + "\n");
            var expired = Crafted(store, "Expired Student", AssessmentUploadStatus.Uploaded, 40, hasServerTime: true);
            string expiredLog = RawLog(expired);
            var recent = Crafted(store, "Recent Student", AssessmentUploadStatus.Uploaded, 29, hasServerTime: true);
            string recentLog = RawLog(recent);
            var oldPending = Crafted(store, "Old Pending Student", AssessmentUploadStatus.Pending, 0, false);
            string oldPendingLog = RawLog(oldPending);
            store.RecordServerTime(DateTime.UtcNow);

            // ============================================== Sessions tab, live refresh (P), NO_ASSIGNMENT (K), TEST (I)
            ui.ShowTab(InstructorManagementUI.View.Sessions);
            yield return Frames(2);
            string Row(string name) => ui.RowTexts.FirstOrDefault(r => r.Contains(name));
            Check("[K] NO_ASSIGNMENT shows as 'Waiting for scenario assignment'", Row("John Doe")?.Contains("Waiting for scenario assignment") == true);
            Check("[I] John Doe Test Mode session is marked TEST and owned by John Doe", Row("John Doe")?.Contains("TEST") == true && owners.Find(doeSession).studentId == JohnDoe);
            ui.OpenSessionDetails(doeSession);
            Check("[K] details explain the session is safe and retryable after assignment",
                ui.DetailsText.Contains("safely stored on this headset") && ui.DetailsText.Contains("Assign the scenario") && ui.DetailsRetryAvailable);
            Check("[security] session details show no token or device key", !ui.DetailsText.Contains(DeviceKey) && !ui.DetailsText.Contains("tok-"));
            ui.CloseDetails();
            Check("[sessions] legacy, recent and expired uploads labelled correctly",
                Row("Legacy Student")?.Contains("Legacy Uploaded") == true && Row("Recent Student")?.Contains("Uploaded") == true && Row("Old Pending Student")?.Contains("Pending") == true);
            ui.OpenSessionDetails(legacy);
            Check("[M] legacy details: 'Retention date unavailable', no days number, no retry", ui.DetailsText.Contains("Retention date unavailable") && !ui.DetailsRetryAvailable);
            ui.OpenSessionDetails(recent);
            Check("[L] recent upload details: 'Local backup expires in 1 day'", ui.DetailsText.Contains("Local backup expires in 1 day"));
            ui.OpenSessionDetails(expired);
            Check("[L] expired upload details: 'Local backup eligible for cleanup'", ui.DetailsText.Contains("Local backup eligible for cleanup"));
            ui.CloseDetails();

            // P: live update on retry without reopening; K: same owner after assignment
            server.NoAssignment.Remove(JohnDoe);
            ui.RetrySession(doeSession);
            yield return WaitFor(() => uploads.GetAllEntries().Exists(e => e.sessionId == doeSession && e.status == AssessmentUploadStatus.Uploaded));
            yield return Frames(3);
            Check("[P] the open Sessions list updated live (OnQueueEntryChanged) - no reopen",
                Row("John Doe")?.Contains("Uploaded") == true && ui.CurrentView == InstructorManagementUI.View.Sessions);
            Check("[K] after the assignment exists, the same session uploads under the same owner (John Doe)", server.Sessions.TryGetValue(doeSession, out var doeOwner) && doeOwner == JohnDoe);
            ui.RetrySession(juanPending);
            yield return WaitFor(() => uploads.GetAllEntries().Exists(e => e.sessionId == juanPending && e.status == AssessmentUploadStatus.Uploaded));
            Check("[retry] Retry Upload uses RetryNow - Juan's pending session uploads to Juan", server.Sessions.TryGetValue(juanPending, out var jo) && jo == Juan);

            // ============================================== L - automatic cleanup excludes legacy; Clean Expired via UI
            // The automatic cleanup already ran after those successful uploads (Phase 4); run it
            // once more explicitly and judge the outcome, not which pass removed what.
            uploads.RunRetentionCleanup();
            Check("[M] automatic cleanup never removes a legacy uploaded entry", uploads.GetAllEntries().Exists(e => e.sessionId == legacy) && File.Exists(legacyLog));
            Check("[L] automatic cleanup removed only the expired upload (+ its proven raw log), kept the recent upload and the pending session",
                !uploads.GetAllEntries().Exists(e => e.sessionId == expired) && !File.Exists(expiredLog) && owners.Find(expired) == null
                && uploads.GetAllEntries().Exists(e => e.sessionId == recent) && File.Exists(recentLog)
                && uploads.GetAllEntries().Exists(e => e.sessionId == oldPending) && File.Exists(oldPendingLog));
            ui.RequestCleanExpired();
            Check("[L] nothing else eligible afterwards", ui.FeedbackText.StartsWith("Nothing to clean"));

            // ============================================== M - legacy cleanup: confirm + PIN re-check
            ui.ShowTab(InstructorManagementUI.View.Sessions);
            ui.RequestClearLegacy();
            Check("[M] legacy cleanup shows the count and the server-safe warning", ui.ConfirmText != null && ui.ConfirmText.Contains("1 session(s)") && ui.ConfirmText.Contains("Server records are not deleted"));
            ui.ConfirmYes();
            Check("[M] confirmation then requires PIN re-entry", ui.CurrentView == InstructorManagementUI.View.Pin && ui.PinPromptText.Contains("Re-enter"));
            EnterPin(ui, "0000");
            Check("[M] wrong PIN at re-check does nothing", ui.CurrentView == InstructorManagementUI.View.Pin && uploads.GetAllEntries().Exists(e => e.sessionId == legacy));
            EnterPin(ui, "4821");
            Check("[M] correct PIN removes ONLY the legacy uploaded local copy",
                !uploads.GetAllEntries().Exists(e => e.sessionId == legacy) && uploads.GetAllEntries().Exists(e => e.sessionId == oldPending)
                && uploads.GetAllEntries().Exists(e => e.sessionId == recent) && owners.Find(legacy) == null && owners.Find(oldPending) != null);
            Check("[raw] legacy raw log deleted only where proven; a same-name file with another session's content is kept",
                !File.Exists(legacyLog) && File.Exists(legacyImpostorLog) && ui.FeedbackText.Contains("1 raw log file(s) kept"));
            Check("[raw] pending / recent sessions' raw logs untouched", File.Exists(oldPendingLog) && File.Exists(recentLog) && File.Exists(juanPendingLog));

            // ============================================== N - clear local directory
            int queueBefore = uploads.GetAllEntries().Count;
            var ownersBefore = owners.SessionIds();
            int serverStudentsBefore = server.Students.Count;
            ui.ShowTab(InstructorManagementUI.View.Students);
            ui.RequestClearLocalDirectory();
            Check("[N] strong confirmation states it is local only", ui.ConfirmText.Contains("does not delete students or codes from the server"));
            ui.ConfirmYes();
            EnterPin(ui, "4821");
            Check("[N] after PIN re-check the local directory is empty", dir.Cache.IsEmpty && dir.Cache.ActiveStudentCount == 0);
            Check("[N] queue, uploads, ownership, raw logs and server students all untouched",
                uploads.GetAllEntries().Count == queueBefore && owners.SessionIds().SequenceEqual(ownersBefore) && server.Students.Count == serverStudentsBefore
                && File.Exists(oldPendingLog) && File.Exists(recentLog));
            ui.SyncDirectory();
            yield return WaitFor(() => dir.Cache.ActiveStudentCount == 2 && !dir.IsSyncing);
            Check("[N] the next successful sync repopulates the directory", dir.Cache.ActiveStudentCount == 2);

            // ============================================== Status tab + O (missing device key)
            ServerReachabilityProbe.Note(ServerReachability.Online);
            ui.ShowTab(InstructorManagementUI.View.Status);
            yield return Frames(2);
            var snap = ui.BuildSnapshot();
            Check($"[status] readiness with directory synced, credential present, online: {snap.ReadinessLabel} ({snap.ConnectionLabel}; {string.Join(" | ", snap.ActionsNeeded)})", snap.Ready && ui.SummaryText.Contains("READY FOR TRAINING"));
            Check("[status] shows credential state but never the key", ui.RowTexts.Any(r => r.Contains("Device credential available")) && ui.RowTexts.All(r => !r.Contains(DeviceKey)));

            currentKey = null;
            dir.ConfigureForTesting(config, dirTransport, new StudentDirectoryStore(Path.Combine(root, "dir")), null);   // credential missing
            ui.ShowTab(InstructorManagementUI.View.Status);
            yield return Frames(2);
            snap = ui.BuildSnapshot();
            Check("[O] missing credential -> ACTION NEEDED + 'Device credential missing', no secret shown",
                !snap.Ready && ui.RowTexts.Any(r => r.Contains("Device credential missing")) && snap.ActionsNeeded.Any(a => a.Contains("Device credential missing")));
            var offlineFlow = new StudentIdentificationFlow(null, () => dir.Cache, () => 1f);
            var cachedLogin = offlineFlow.IdentifyByCodeAsync(CodeMaria);
            yield return WaitFor(() => cachedLogin.IsCompleted);
            Check("[O] offline cached login still works without the credential", cachedLogin.Result.Outcome == IdentificationOutcome.Identified && cachedLogin.Result.Student.StudentId == Maria);
            var keyless = Bind(Maria, "Maria Santos");
            uploads.QueueSessionForUpload(SessionLogForQueue(keyless), null, null);
            yield return WaitFor(() => uploads.GetAllEntries().Exists(e => e.sessionId == keyless && e.status == AssessmentUploadStatus.RetryWaiting));
            Check("[O] uploads needing the device credential stay local (RetryWaiting), nothing lost",
                uploads.GetAllEntries().Exists(e => e.sessionId == keyless && e.status == AssessmentUploadStatus.RetryWaiting) && !server.Sessions.ContainsKey(keyless));
            ui.SyncDirectory();
            Check("[O] manual sync explains the missing credential and keeps the cache", ui.FeedbackText.Contains("no device credential") && dir.Cache.ActiveStudentCount == 2);

            // ============================================== no generic delete
            var allButtons = ui.GetComponentsInChildren<UnityEngine.UI.Button>(true).Select(b => b.GetComponentInChildren<TMPro.TMP_Text>(true)?.text ?? "").ToList();
            Check("[sessions] there is no generic 'Delete Session' control", allButtons.All(t => t.IndexOf("Delete", StringComparison.OrdinalIgnoreCase) < 0));

            ui.Close();
            Check("[close] Close returns to locked state", !ui.IsOpen && !ui.Unlocked);
        }
        finally
        {
            foreach (var go in spawned) if (go != null) DestroyImmediate(go);
            SetStatic(typeof(AssessmentUploadService), prevUpload);
            SetStatic(typeof(StudentDirectoryService), prevDirectory);
            SetStatic(typeof(SessionLogger), prevLogger);
            SetStatic(typeof(PairingIdentityService), prevPairing);
            if (realUploaderWasEnabled && prevUpload != null) prevUpload.enabled = true;
            ServerReachabilityProbe.Note(prevReach);
        }

        report.AppendLine(failures.Count == 0 ? "=== PASS, 0 failures ===" : $"=== FAIL: {failures.Count} failure(s) ===");
        Report = report.ToString();
        IsDone = true;
    }
}
