// Assets/_Project/Scripts/Testing/VrIdentityUploadBridgeTest.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Phase 3 checks: student-code identification (online, offline, Continue Without
/// Code), the write-once session owner, and AssessmentUploadService's VR path - device-
/// authenticated per-session upload tokens for the ORIGINAL owner - against a fake
/// server that behaves like vicsi-dashboard's (tokens, expiry, session binding, owner
/// lock, disabled-code policy). Isolated temp folders; never the real queue, owner file,
/// directory or network.
///
/// Run in Play mode:
///     var t = new GameObject("VIUB").AddComponent&lt;VrIdentityUploadBridgeTest&gt;(); t.RunAllTests();
/// then read t.Report once t.IsDone.
/// </summary>
public class VrIdentityUploadBridgeTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }

    private readonly List<string> failures = new();
    private readonly StringBuilder report = new();

    private const string Juan = "11111111-1111-4111-8111-111111111111";
    private const string Maria = "22222222-2222-4222-8222-222222222222";
    private const string JohnDoe = "99999999-9999-4999-8999-999999999999";
    private const string ClassX = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
    private const string CodeJuan = "K7M4PQ2D";
    private const string CodeJuanNew = "QRST2346";
    private const string CodeMaria = "ABCD2345";
    private const string CodeBen = "HJKM6789";
    private const string Ben = "33333333-3333-4333-8333-333333333333";

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

    // =====================================================================
    // Fake vicsi-dashboard
    // =====================================================================

    private class FakeServer
    {
        public string DeviceKey = "device-key-1";
        public bool Online = true;
        public bool IssueExpiredOnce;
        public readonly Dictionary<string, (string student, string session, DateTime expires, bool test)> Tokens = new();
        public readonly Dictionary<string, (string student, bool test)> Sessions = new();
        public readonly Dictionary<string, DateTime> DisabledAt = new();
        public readonly List<VrUploadTokenRequestDto> TokenRequests = new();
        public readonly List<string> TokensIssued = new();
        public int Uploads;
        private int counter;

        public string Issue(VrUploadTokenRequestDto r, out string refusal)
        {
            refusal = null;
            if (Sessions.TryGetValue(r.sessionId, out var stored) && stored.student != r.studentId)
            {
                refusal = "SESSION_OWNER_MISMATCH";
                return null;
            }
            if (DisabledAt.TryGetValue(r.studentId, out var disabledAt))
            {
                bool startedBefore = DateTime.TryParse(r.sessionStartedAtUtc, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out var started) && started < disabledAt;
                if (!startedBefore)
                {
                    refusal = "STUDENT_DISABLED";
                    return null;
                }
            }
            string token = "tok-" + (++counter) + "-" + Guid.NewGuid().ToString("N");
            DateTime exp = IssueExpiredOnce ? DateTime.UtcNow.AddSeconds(-1) : DateTime.UtcNow.AddMinutes(10);
            IssueExpiredOnce = false;
            Tokens[token] = (r.studentId, r.sessionId, exp, r.isTestSession);
            TokensIssued.Add(token);
            return token;
        }
    }

    private class FakeAuthClient : IVrUploadAuthorizationClient
    {
        private readonly FakeServer server;
        public FakeAuthClient(FakeServer server) => this.server = server;

        public Task<VrUploadAuthorizationResult> RequestAsync(string url, string deviceKey, VrUploadTokenRequestDto request, float timeoutSeconds, CancellationToken cancellationToken)
        {
            server.TokenRequests.Add(request);
            if (!server.Online) return Task.FromResult(new VrUploadAuthorizationResult { Failure = VrUploadAuthorizationFailure.NoNetwork });
            if (deviceKey != server.DeviceKey) return Task.FromResult(new VrUploadAuthorizationResult { Failure = VrUploadAuthorizationFailure.DeviceUnauthorized, HttpStatusCode = 401 });
            string token = server.Issue(request, out string refusal);
            if (token == null) return Task.FromResult(new VrUploadAuthorizationResult { Failure = VrUploadAuthorizationFailure.Refused, ServerErrorCode = refusal, HttpStatusCode = 409 });
            return Task.FromResult(new VrUploadAuthorizationResult { UploadToken = token, ExpiresAtUtc = server.Tokens[token].expires, HttpStatusCode = 200 });
        }
    }

    private class FakeUploadTransport : IAssessmentUploadTransport
    {
        private readonly FakeServer server;
        public FakeUploadTransport(FakeServer server) => this.server = server;

        public Task<AssessmentUploadAttemptResult> UploadAsync(string submissionUrl, string payloadJson, string bearerToken, float timeoutSeconds, CancellationToken cancellationToken)
        {
            server.Uploads++;
            if (!server.Online) return Task.FromResult(AssessmentUploadAttemptResult.Fail(AssessmentUploadErrorCategory.NoNetwork, 0, "offline"));
            if (bearerToken == null || !server.Tokens.TryGetValue(bearerToken, out var t))
                return Task.FromResult(AssessmentUploadAttemptResult.Fail(AssessmentUploadErrorCategory.AuthenticationFailed, 401, "TOKEN_INVALID"));
            if (t.expires < DateTime.UtcNow)
                return Task.FromResult(AssessmentUploadAttemptResult.Fail(AssessmentUploadErrorCategory.AuthenticationFailed, 401, "TOKEN_EXPIRED"));
            var dto = JsonUtility.FromJson<AssessmentSessionUploadDto>(payloadJson);
            if (!string.Equals(dto.sessionId, t.session, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(AssessmentUploadAttemptResult.Fail(AssessmentUploadErrorCategory.AuthenticationFailed, 401, "TOKEN_INVALID (other session)"));
            bool duplicate = server.Sessions.ContainsKey(dto.sessionId);
            if (!duplicate) server.Sessions[dto.sessionId] = (t.student, t.test);   // owner comes from the TOKEN, as on the real server
            return Task.FromResult(AssessmentUploadAttemptResult.Ok(new AssessmentSubmissionResponseDto
            {
                success = true,
                data = new AssessmentSubmissionDataDto { receiptId = Guid.NewGuid().ToString(), sessionId = dto.sessionId, duplicate = duplicate, uploadStatus = "accepted" },
            }, 200));
        }
    }

    private class FakeValidator : IVrCodeValidator
    {
        public VrCodeValidationOutcome Mode = VrCodeValidationOutcome.Unreachable;
        public bool Hang;
        public Dictionary<string, (string id, string name)> Known = new();

        public Task<VrCodeValidationResult> ValidateAsync(string canonicalCode, float timeoutSeconds, CancellationToken cancellationToken)
        {
            if (Hang) return new TaskCompletionSource<VrCodeValidationResult>().Task;
            if (Mode == VrCodeValidationOutcome.Unreachable) return Task.FromResult(new VrCodeValidationResult { Outcome = VrCodeValidationOutcome.Unreachable });
            if (!Known.TryGetValue(canonicalCode, out var s)) return Task.FromResult(new VrCodeValidationResult { Outcome = VrCodeValidationOutcome.Invalid });
            return Task.FromResult(new VrCodeValidationResult
            {
                Outcome = VrCodeValidationOutcome.Valid,
                Student = new VrValidatedStudentDto { studentId = s.id, displayName = s.name, codeVersion = 1, classes = new List<StudentDirectoryClassDto> { new() { classId = ClassX, className = "CRIM 101", classSection = "A" } } },
            });
        }
    }

    // =====================================================================
    // helpers
    // =====================================================================

    private static string DirectoryBody(bool includeTestStudent, params (string id, string name, string code, bool enabled)[] students)
    {
        string salt = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        var data = new StudentDirectoryDataDto
        {
            generatedAtUtc = DateTime.UtcNow.ToString("o"),
            codeLookup = new StudentDirectoryCodeLookupDto { algorithm = "PBKDF2-HMAC-SHA256", iterations = 2000, keyBytes = 32, saltBase64 = salt, input = "canonical-code-utf8", output = "hex" },
            students = new List<StudentDirectoryStudentDto>(),
            testStudent = includeTestStudent
                ? new StudentDirectoryTestStudentDto { studentId = JohnDoe, displayName = "John Doe", classes = new List<StudentDirectoryClassDto> { new() { classId = ClassX, className = "ViCSI Dev Test Class", classSection = "Z" } } }
                : new StudentDirectoryTestStudentDto(),
        };
        foreach (var s in students)
        {
            data.students.Add(new StudentDirectoryStudentDto
            {
                studentId = s.id, displayName = s.name, codeVersion = 1, enabled = s.enabled,
                codeHash = s.enabled ? StudentAccessCodes.DeriveLookupHash(s.code, salt, 2000, 32) : null,
                updatedAtUtc = DateTime.UtcNow.ToString("o"),
                classes = new List<StudentDirectoryClassDto> { new() { classId = ClassX, className = "CRIM 101", classSection = "A" } },
            });
        }
        return JsonUtility.ToJson(new StudentDirectoryResponseDto { success = true, data = data });
    }

    private static string SessionLog(string directory, string sessionId)
    {
        string path = Path.Combine(directory, sessionId + ".jsonl");
        var lines = new List<string>
        {
            JsonUtility.ToJson(new SessionEvent { sessionId = sessionId, sequenceNumber = 0, timestampMs = 0, eventType = "SessionStarted", targetId = "CSI_Environment", payload = new List<PayloadEntry> { new() { key = "startedAtUtc", value = DateTime.UtcNow.ToString("O") } } }),
            JsonUtility.ToJson(new SessionEvent { sessionId = sessionId, sequenceNumber = 1, timestampMs = 500, eventType = "SessionEnded", payload = new List<PayloadEntry> { new() { key = "endedAtUtc", value = DateTime.UtcNow.ToString("O") } } }),
        };
        File.WriteAllText(path, string.Join("\n", lines) + "\n");
        return path;
    }

    private static IdentifiedStudent Student(string id, string name, bool test = false) => new()
    {
        StudentId = id, DisplayName = name, IsTestSession = test,
        Classes = new List<CachedStudentClass> { new() { classId = ClassX, className = "CRIM 101", classSection = "A" } },
    };

    private static IEnumerator Await<T>(Task<T> task, float limit = 10f)
    {
        float t = 0f;
        while (!task.IsCompleted && t < limit) { t += Time.unscaledDeltaTime; yield return null; }
    }

    private static IEnumerator WaitFor(Func<bool> condition, float limit = 5f)
    {
        float t = 0f;
        while (!condition() && t < limit) { t += Time.unscaledDeltaTime; yield return null; }
    }

    private static AssessmentUploadQueueEntry Entry(AssessmentUploadService service, string sessionId) => service.GetAllEntries().Find(e => e.sessionId == sessionId);

    private static void SetStatic(Type type, object value) => type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.SetValue(null, value);

    // =====================================================================
    // tests
    // =====================================================================

    private IEnumerator Run()
    {
        report.AppendLine("=== VrIdentityUploadBridgeTest ===");
        string root = Path.Combine(Application.temporaryCachePath, "vr_bridge_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var previousLogger = SessionLogger.Instance;
        var previousPairing = PairingIdentityService.Instance;
        var previousUploadService = AssessmentUploadService.Instance;   // the test service's Awake replaces it
        SetStatic(typeof(SessionLogger), null);          // builder must use each test log's own session id
        SetStatic(typeof(PairingIdentityService), null); // nothing is "paired": the VR path must not need it
        GameObject serviceGo = null;

        try
        {
            // ---------------------------------------------------- identification
            var cache = new StudentDirectoryCache(new StudentDirectoryStore(Path.Combine(root, "dir")));
            Directory.CreateDirectory(Path.Combine(root, "dir"));
            cache.ApplyServerResponse(DirectoryBody(true, (Juan, "Juan Dela Cruz", CodeJuan, true), (Maria, "Maria Santos", CodeMaria, true), (Ben, "Ben Cruz", CodeBen, false)), DateTime.UtcNow);
            // Ben's code must have been seen enabled first to be reported Disabled offline:
            var benCache = new StudentDirectoryCache(new StudentDirectoryStore(Path.Combine(root, "dir")));
            benCache.ApplyServerResponse(DirectoryBody(true, (Juan, "Juan Dela Cruz", CodeJuan, true), (Maria, "Maria Santos", CodeMaria, true), (Ben, "Ben Cruz", CodeBen, true)), DateTime.UtcNow);
            benCache.ApplyServerResponse(DirectoryBody(true, (Juan, "Juan Dela Cruz", CodeJuan, true), (Maria, "Maria Santos", CodeMaria, true), (Ben, "Ben Cruz", CodeBen, false)), DateTime.UtcNow);
            cache = benCache;

            var validator = new FakeValidator { Mode = VrCodeValidationOutcome.Valid };
            validator.Known[CodeJuan] = (Juan, "Juan Dela Cruz");
            var flow = new StudentIdentificationFlow(validator, () => cache, () => 1f);

            var task = flow.IdentifyByCodeAsync("k7m4-pq2d");
            yield return Await(task);
            Check("[identify online] code -> server -> stable id, name, class", task.Result.Outcome == IdentificationOutcome.Identified
                && task.Result.Student.StudentId == Juan && task.Result.Student.Source == "online-code" && task.Result.Student.ClassDisplay == "CRIM 101 - A");

            task = flow.IdentifyByCodeAsync(CodeMaria);   // cache knows it; server says invalid (e.g. regenerated since last sync)
            yield return Await(task);
            Check("[identify online] server is authoritative over an older cache", task.Result.Outcome == IdentificationOutcome.NotRecognized);

            validator.Mode = VrCodeValidationOutcome.Unreachable;
            task = flow.IdentifyByCodeAsync(CodeMaria);
            yield return Await(task);
            Check("[identify offline] cached code -> stable id", task.Result.Outcome == IdentificationOutcome.Identified && task.Result.Student.StudentId == Maria && task.Result.Student.Source == "offline-code");
            task = flow.IdentifyByCodeAsync(CodeBen);
            yield return Await(task);
            Check("[identify offline] disabled cached code -> Disabled", task.Result.Outcome == IdentificationOutcome.Disabled);
            task = flow.IdentifyByCodeAsync("ZZZZ9999");
            yield return Await(task);
            Check("[identify offline] unknown code -> NotRecognized (headset can't invent students)", task.Result.Outcome == IdentificationOutcome.NotRecognized);
            task = flow.IdentifyByCodeAsync("K7M4-PQ0D");
            yield return Await(task);
            Check("[identify] malformed code -> InvalidFormat", task.Result.Outcome == IdentificationOutcome.InvalidFormat);

            validator.Hang = true;
            float t0 = Time.realtimeSinceStartup;
            task = flow.IdentifyByCodeAsync(CodeJuan);
            yield return Await(task, 6f);
            Check($"[identify] a hung server falls back to the cache ({Time.realtimeSinceStartup - t0:0.0}s)", task.IsCompleted && task.Result.Outcome == IdentificationOutcome.Identified && task.Result.Student.StudentId == Juan);
            validator.Hang = false;

            var emptyFlow = new StudentIdentificationFlow(validator, () => new StudentDirectoryCache(new StudentDirectoryStore(Path.Combine(root, "nodir"))), () => 1f);
            Directory.CreateDirectory(Path.Combine(root, "nodir"));
            task = emptyFlow.IdentifyByCodeAsync(CodeJuan);
            yield return Await(task);
            Check("[identify offline] never-synced headset says so", task.Result.Outcome == IdentificationOutcome.DirectoryNeverSynced);

            var noCode = flow.ContinueWithoutCode();
            Check("[test mode] Continue Without Code -> the EXISTING test student id from the directory, flagged test",
                noCode.Outcome == IdentificationOutcome.Identified && noCode.Student.StudentId == JohnDoe && noCode.Student.DisplayName == "John Doe" && noCode.Student.IsTestSession);
            Check("[test mode] unavailable (not invented) when the headset never synced a test student",
                emptyFlow.ContinueWithoutCode().Outcome == IdentificationOutcome.TestModeUnavailable);

            // ---------------------------------------------------- write-once ownership
            var owners = new SessionOwnershipStore(root);
            string sOwn = Guid.NewGuid().ToString();
            var first = owners.TryBind(sOwn, Student(Juan, "Juan Dela Cruz"), DateTime.UtcNow, out bool createdFirst);
            string fileBefore = File.ReadAllText(owners.FilePath);
            var second = owners.TryBind(sOwn, Student(Maria, "Maria Santos"), DateTime.UtcNow, out bool createdSecond);
            Check("[ownership] a bound session can't be re-bound: the original owner is returned and the file is unchanged",
                createdFirst && !createdSecond && first.studentId == Juan && second.studentId == Juan && File.ReadAllText(owners.FilePath) == fileBefore);

            // ---------------------------------------------------- uploader on the VR path
            var server = new FakeServer();
            var config = ScriptableObject.CreateInstance<AssessmentUploadConfiguration>();
            config.apiBaseUrl = "https://bridge-test.invalid";
            config.requestTimeoutSeconds = 5f;
            config.maxRetryAttempts = 3;
            config.initialRetryDelaySeconds = 30f;   // no automatic retries during the test - each step drives RetryNow itself
            config.maxRetryDelaySeconds = 30f;
            config.automaticUploadEnabled = true;
            config.uploadRawEventLogEnabled = true;
            config.maxEventsPerRequest = 5000;
            var scenario = ScriptableObject.CreateInstance<ScenarioDefinition>();
            scenario.scenarioId = "CSI-ENVIRONMENT-001";
            scenario.scenarioVersion = "1.0.0";
            scenario.scoringRulesVersion = "1.0.0";

            serviceGo = new GameObject("VR bridge upload service (test)");
            serviceGo.SetActive(false);
            var service = serviceGo.AddComponent<AssessmentUploadService>();
            typeof(AssessmentUploadService).GetField("configuration", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(service, config);
            typeof(AssessmentUploadService).GetField("scenario", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(service, scenario);
            // Seams BEFORE activation, so OnEnable's startup work never sees the real queue.
            string queueRoot = Path.Combine(root, "queue");
            Directory.CreateDirectory(queueRoot);
            service.SetQueueRootForTesting(queueRoot);
            service.SetTransportForTesting(new FakeUploadTransport(server));
            string deviceKey = server.DeviceKey;
            service.SetVrOwnershipSeamsForTesting(new FakeAuthClient(server), () => deviceKey, owners);
            serviceGo.SetActive(true);

            // 1. real online student
            string s1 = Guid.NewGuid().ToString();
            owners.TryBind(s1, Student(Juan, "Juan Dela Cruz"), DateTime.UtcNow, out _);
            service.QueueSessionForUpload(SessionLog(root, s1), null, null);
            yield return WaitFor(() => Entry(service, s1)?.status == AssessmentUploadStatus.Uploaded);
            var req1 = server.TokenRequests.Find(r => r.sessionId == s1);
            Check("[online student] code-identified session -> device-authorized token for Juan + this session -> upload -> stored under Juan",
                Entry(service, s1)?.status == AssessmentUploadStatus.Uploaded && req1 != null && req1.studentId == Juan
                && server.Sessions.TryGetValue(s1, out var stored1) && stored1.student == Juan);

            // 2. offline student -> later online
            server.Online = false;
            string s2 = Guid.NewGuid().ToString();
            owners.TryBind(s2, Student(Juan, "Juan Dela Cruz"), DateTime.UtcNow, out _);
            service.QueueSessionForUpload(SessionLog(root, s2), null, null);
            yield return WaitFor(() => Entry(service, s2)?.status == AssessmentUploadStatus.RetryWaiting);
            string payloadPath2 = Entry(service, s2)?.localPayloadPath;
            Check("[offline student] completed offline -> saved, waiting (RetryWaiting), payload kept",
                Entry(service, s2)?.status == AssessmentUploadStatus.RetryWaiting && File.Exists(payloadPath2) && !server.Sessions.ContainsKey(s2));

            // 3. Juan -> Maria: Maria logs in and plays while Juan's session is pending
            string s3 = Guid.NewGuid().ToString();
            owners.TryBind(s3, Student(Maria, "Maria Santos"), DateTime.UtcNow, out _);
            service.QueueSessionForUpload(SessionLog(root, s3), null, null);
            yield return WaitFor(() => Entry(service, s3)?.status == AssessmentUploadStatus.RetryWaiting);
            server.Online = true;
            service.RetryNow(s2);
            service.RetryNow(s3);
            yield return WaitFor(() => Entry(service, s2)?.status == AssessmentUploadStatus.Uploaded && Entry(service, s3)?.status == AssessmentUploadStatus.Uploaded);
            Check("[Juan -> Maria] internet returns: Juan's pending session uploads to JUAN, Maria's to Maria",
                server.Sessions.TryGetValue(s2, out var stored2) && stored2.student == Juan
                && server.Sessions.TryGetValue(s3, out var stored3) && stored3.student == Maria
                && server.TokenRequests.FindLast(r => r.sessionId == s2).studentId == Juan);

            // 4. John Doe test mode - same flow, real stable id, test flag
            string s4 = Guid.NewGuid().ToString();
            owners.TryBind(s4, flow.ContinueWithoutCode().Student, DateTime.UtcNow, out _);
            service.QueueSessionForUpload(SessionLog(root, s4), null, null);
            yield return WaitFor(() => Entry(service, s4)?.status == AssessmentUploadStatus.Uploaded);
            var payload4 = JsonUtility.FromJson<AssessmentSessionUploadDto>(File.ReadAllText(Entry(service, s4).localPayloadPath));
            Check("[John Doe] Continue Without Code session -> per-session token for the existing John Doe id -> stored as John Doe, test flag kept",
                server.Sessions.TryGetValue(s4, out var stored4) && stored4.student == JohnDoe && stored4.test
                && server.TokenRequests.FindLast(r => r.sessionId == s4).isTestSession && payload4.isTestSession);

            // 5. regenerated code: future logins change, existing ownership doesn't
            string s5 = Guid.NewGuid().ToString();
            owners.TryBind(s5, Student(Juan, "Juan Dela Cruz"), DateTime.UtcNow, out _);
            cache.ApplyServerResponse(DirectoryBody(true, (Juan, "Juan Dela Cruz", CodeJuanNew, true), (Maria, "Maria Santos", CodeMaria, true)), DateTime.UtcNow);
            validator.Mode = VrCodeValidationOutcome.Unreachable;
            var oldCode = flow.IdentifyByCodeAsync(CodeJuan);
            var newCode = flow.IdentifyByCodeAsync(CodeJuanNew);
            yield return Await(oldCode);
            yield return Await(newCode);
            service.QueueSessionForUpload(SessionLog(root, s5), null, null);
            yield return WaitFor(() => Entry(service, s5)?.status == AssessmentUploadStatus.Uploaded);
            Check("[regenerated code] old code no longer identifies, new one does; the existing session still belongs to Juan",
                oldCode.Result.Outcome == IdentificationOutcome.NotRecognized && newCode.Result.Student?.StudentId == Juan
                && owners.Find(s5).studentId == Juan && server.Sessions[s5].student == Juan);

            // 6. disabled student: work started BEFORE disabling uploads; later work is refused but kept
            string s6 = Guid.NewGuid().ToString();
            owners.TryBind(s6, Student(Maria, "Maria Santos"), DateTime.UtcNow.AddMinutes(-30), out _);
            server.DisabledAt[Maria] = DateTime.UtcNow.AddMinutes(-10);
            service.QueueSessionForUpload(SessionLog(root, s6), null, null);
            yield return WaitFor(() => Entry(service, s6)?.status == AssessmentUploadStatus.Uploaded);
            string s7 = Guid.NewGuid().ToString();
            owners.TryBind(s7, Student(Maria, "Maria Santos"), DateTime.UtcNow, out _);
            service.QueueSessionForUpload(SessionLog(root, s7), null, null);
            yield return WaitFor(() => Entry(service, s7)?.status == AssessmentUploadStatus.Rejected);
            Check("[disabled] a session completed before the code was disabled still uploads",
                Entry(service, s6)?.status == AssessmentUploadStatus.Uploaded && server.Sessions[s6].student == Maria);
            Check("[disabled] a session started after disabling is refused - kept on the headset (Rejected), never discarded",
                Entry(service, s7)?.status == AssessmentUploadStatus.Rejected && File.Exists(Entry(service, s7).localPayloadPath) && !server.Sessions.ContainsKey(s7));

            // 7. expired short-lived token -> fresh token, retry, no loss
            string s8 = Guid.NewGuid().ToString();
            owners.TryBind(s8, Student(Juan, "Juan Dela Cruz"), DateTime.UtcNow, out _);
            int requestsBefore = server.TokenRequests.Count;
            server.IssueExpiredOnce = true;
            service.QueueSessionForUpload(SessionLog(root, s8), null, null);
            yield return WaitFor(() => Entry(service, s8)?.status == AssessmentUploadStatus.Uploaded);
            Check("[expired token] TOKEN_EXPIRED -> one fresh token -> uploaded under Juan, nothing lost",
                Entry(service, s8)?.status == AssessmentUploadStatus.Uploaded && server.TokenRequests.Count - requestsBefore == 2 && server.Sessions[s8].student == Juan);

            // 8. invalid device key
            deviceKey = "wrong-key";
            string s9 = Guid.NewGuid().ToString();
            owners.TryBind(s9, Student(Juan, "Juan Dela Cruz"), DateTime.UtcNow, out _);
            service.QueueSessionForUpload(SessionLog(root, s9), null, null);
            yield return WaitFor(() => Entry(service, s9)?.status == AssessmentUploadStatus.RetryWaiting);
            Check("[invalid device key] authorization refused -> session stays local at RetryWaiting (not AwaitingPairing), payload kept",
                Entry(service, s9)?.status == AssessmentUploadStatus.RetryWaiting && Entry(service, s9).lastErrorCategory == AssessmentUploadErrorCategory.AuthenticationFailed
                && File.Exists(Entry(service, s9).localPayloadPath) && !server.Sessions.ContainsKey(s9));
            deviceKey = server.DeviceKey;
            service.RetryNow(s9);
            yield return WaitFor(() => Entry(service, s9)?.status == AssessmentUploadStatus.Uploaded);
            Check("[invalid device key] once the key is fixed, the same saved session uploads to its owner", server.Sessions.TryGetValue(s9, out var stored9) && stored9.student == Juan);

            // 9. no device key on the headset
            deviceKey = null;
            string s10 = Guid.NewGuid().ToString();
            owners.TryBind(s10, Student(Juan, "Juan Dela Cruz"), DateTime.UtcNow, out _);
            service.QueueSessionForUpload(SessionLog(root, s10), null, null);
            yield return WaitFor(() => Entry(service, s10)?.status == AssessmentUploadStatus.RetryWaiting);
            Check("[no device key] nothing sent, session saved at RetryWaiting", Entry(service, s10)?.status == AssessmentUploadStatus.RetryWaiting && !server.TokenRequests.Exists(r => r.sessionId == s10));
            deviceKey = server.DeviceKey;

            // 10. server says the session belongs to someone else -> Rejected, kept
            string s11 = Guid.NewGuid().ToString();
            server.Sessions[s11] = (Maria, false);
            owners.TryBind(s11, Student(Juan, "Juan Dela Cruz"), DateTime.UtcNow, out _);
            service.QueueSessionForUpload(SessionLog(root, s11), null, null);
            yield return WaitFor(() => Entry(service, s11)?.status == AssessmentUploadStatus.Rejected);
            Check("[owner mismatch] refused by the server, kept locally as Rejected; ownership never moved",
                Entry(service, s11)?.status == AssessmentUploadStatus.Rejected && server.Sessions[s11].student == Maria && File.Exists(Entry(service, s11).localPayloadPath));

            // 11. legacy (no owner record) entries still need pairing - pairing path unchanged
            string s12 = Guid.NewGuid().ToString();
            service.QueueSessionForUpload(SessionLog(root, s12), null, null);
            yield return WaitFor(() => Entry(service, s12)?.status == AssessmentUploadStatus.AwaitingPairing);
            Check("[legacy] a session with no owner record keeps the pairing behavior (AwaitingPairing, nothing sent)",
                Entry(service, s12)?.status == AssessmentUploadStatus.AwaitingPairing && !server.TokenRequests.Exists(r => r.sessionId == s12));

            // 12. secrets never persisted
            string queueIndex = File.ReadAllText(Path.Combine(queueRoot, "assessment_upload_queue_index.json"));
            string ownerFile = File.ReadAllText(owners.FilePath);
            bool leaked = false;
            foreach (var tok in server.TokensIssued)
            {
                leaked |= queueIndex.Contains(tok) || ownerFile.Contains(tok);
                foreach (var payloadFile in Directory.GetFiles(Path.Combine(queueRoot, "assessment_upload_payloads")))
                {
                    leaked |= File.ReadAllText(payloadFile).Contains(tok);
                }
            }
            leaked |= queueIndex.Contains(server.DeviceKey) || ownerFile.Contains(server.DeviceKey);
            Check($"[secrets] none of the {server.TokensIssued.Count} upload tokens nor the device key is in the queue, payloads or owner file", !leaked);
        }
        finally
        {
            if (serviceGo != null) Destroy(serviceGo);
            SetStatic(typeof(AssessmentUploadService), previousUploadService);
            SetStatic(typeof(SessionLogger), previousLogger);
            SetStatic(typeof(PairingIdentityService), previousPairing);
        }

        report.AppendLine(failures.Count == 0 ? "=== PASS, 0 failures ===" : $"=== FAIL: {failures.Count} failure(s) ===");
        Report = report.ToString();
        IsDone = true;
    }
}
