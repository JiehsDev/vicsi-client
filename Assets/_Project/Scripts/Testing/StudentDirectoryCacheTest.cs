// Assets/_Project/Scripts/Testing/StudentDirectoryCacheTest.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Checks for the headset's offline student directory (StudentDirectoryCache /
/// StudentDirectoryService) against a fake server, in a throwaway folder under
/// temporaryCachePath - never the real cached directory, never the network.
///
/// Run in Play mode:
///     var t = new GameObject("SDT").AddComponent&lt;StudentDirectoryCacheTest&gt;(); t.RunAllTests();
/// then read t.Report once t.IsDone.
/// </summary>
public class StudentDirectoryCacheTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }

    private readonly List<string> failures = new();
    private readonly StringBuilder report = new();

    private const string IdA = "11111111-1111-4111-8111-111111111111";
    private const string IdB = "22222222-2222-4222-8222-222222222222";
    private const string IdC = "33333333-3333-4333-8333-333333333333";
    private const string ClassX = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
    private const string ClassY = "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb";
    private const string CodeA1 = "K7M4PQ2D";
    private const string CodeA2 = "QRST2346";
    private const string CodeB = "ABCD2345";
    private const string CodeC = "HJKM6789";

    private string root;

    public void RunAllTests()
    {
        IsDone = false;
        StartCoroutine(Run());
    }

    private void Check(string label, bool ok)
    {
        report.AppendLine((ok ? "  PASS: " : "  FAIL: ") + label);
        if (!ok)
        {
            failures.Add(label);
        }
    }

    // ---------------------------------------------------------------- fake server

    private class FakeTransport : IStudentDirectoryTransport
    {
        public Func<StudentDirectoryFetchResult> Next;
        public bool Hang;
        public int Calls;
        public string LastKey;

        public Task<StudentDirectoryFetchResult> FetchAsync(string url, string deviceKey, float timeoutSeconds, CancellationToken cancellationToken)
        {
            Calls++;
            LastKey = deviceKey;
            if (Hang)
            {
                return new TaskCompletionSource<StudentDirectoryFetchResult>().Task; // never completes, ignores the token
            }
            return Task.FromResult(Next());
        }
    }

    private class S
    {
        public string id, name, code;
        public int version = 1;
        public bool enabled = true;
        public string[] classes = { ClassX };
    }

    private static string Body(params S[] students)
    {
        string salt = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        var data = new StudentDirectoryDataDto
        {
            generatedAtUtc = DateTime.UtcNow.ToString("o"),
            codeLookup = new StudentDirectoryCodeLookupDto { algorithm = "PBKDF2-HMAC-SHA256", iterations = 2000, keyBytes = 32, saltBase64 = salt, input = "canonical-code-utf8", output = "hex" },
            students = new List<StudentDirectoryStudentDto>(),
        };
        foreach (var s in students)
        {
            var dto = new StudentDirectoryStudentDto
            {
                studentId = s.id,
                displayName = s.name,
                codeVersion = s.version,
                enabled = s.enabled,
                codeHash = s.enabled ? StudentAccessCodes.DeriveLookupHash(s.code, salt, 2000, 32) : null,
                updatedAtUtc = DateTime.UtcNow.ToString("o"),
                classes = new List<StudentDirectoryClassDto>(),
            };
            foreach (var c in s.classes)
            {
                dto.classes.Add(new StudentDirectoryClassDto { classId = c, className = c == ClassX ? "CRIM 101" : "CRIM 202", classSection = "A" });
            }
            data.students.Add(dto);
        }
        return JsonUtility.ToJson(new StudentDirectoryResponseDto { success = true, data = data });
    }

    private StudentDirectoryService NewService(FakeTransport transport, string folder, string key = "device-key", float timeout = 2f)
    {
        var config = ScriptableObject.CreateInstance<AssessmentUploadConfiguration>();
        config.apiBaseUrl = "https://directory-test.invalid";
        config.studentDirectorySyncTimeoutSeconds = timeout;
        // Built inactive so Awake (which would load the REAL cached directory) runs only
        // after the test store/transport are in place.
        var go = new GameObject("StudentDirectoryService (test)");
        go.SetActive(false);
        go.transform.SetParent(transform);
        var service = go.AddComponent<StudentDirectoryService>();
        service.ConfigureForTesting(config, transport, new StudentDirectoryStore(folder), key);
        go.SetActive(true);
        return service;
    }

    private static IEnumerator Await<T>(Task<T> task, float limitSeconds = 10f)
    {
        float t = 0f;
        while (!task.IsCompleted && t < limitSeconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private string Folder(string name)
    {
        string path = Path.Combine(root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static string FileText(string folder) =>
        File.Exists(Path.Combine(folder, StudentDirectoryStore.FileName)) ? File.ReadAllText(Path.Combine(folder, StudentDirectoryStore.FileName)) : null;

    // ---------------------------------------------------------------- tests

    private IEnumerator Run()
    {
        report.AppendLine("=== StudentDirectoryCacheTest ===");
        root = Path.Combine(Application.temporaryCachePath, "student_directory_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        // --- contract with the server ---
        Check("[contract] PBKDF2 matches the server's Node test vector byte for byte",
            StudentAccessCodes.DeriveLookupHash("K7M4PQ2D", "MDEyMzQ1Njc4OWFiY2RlZg==", 20000, 32) == "e1a98c69a63dc6278b1fb223099bb9245ccc93084276eafe9d98d17504857b4c");
        Check("[contract] normalization: case/space/dash-insensitive, lookalikes rejected",
            StudentAccessCodes.Normalize(" k7m4-pq2d ") == "K7M4PQ2D" && StudentAccessCodes.Normalize("K7M4PQ0D") == null
            && StudentAccessCodes.Normalize("K7M4PQ2") == null && StudentAccessCodes.Normalize("K7M4PQ2DD") == null);

        string main = Folder("main");
        var server = new FakeTransport();
        var service = NewService(server, main);

        // --- 1. no cache + online sync ---
        Check("[no cache] starts empty", service.Cache.IsEmpty && service.Cache.Lookup(CodeA1).DirectoryEmpty);
        server.Next = () => StudentDirectoryFetchResult.Ok(200, Body(new S { id = IdA, name = "Ana Reyes", code = CodeA1 }, new S { id = IdB, name = "Ben Cruz", code = CodeB }));
        var sync = service.SyncNow();
        yield return Await(sync);
        var a = service.Cache.Lookup("k7m4-pq2d");
        Check("[no cache + online] sync succeeds, 2 active students, typed code resolves to the stable id",
            sync.Result == StudentDirectorySyncStatus.Succeeded && service.Cache.ActiveStudentCount == 2
            && a.Outcome == StudentCodeLookupOutcome.Valid && a.Student.studentId == IdA && a.Student.classes.Count == 1);
        Check("[metadata] last attempt + last success + status recorded, device key sent",
            !string.IsNullOrEmpty(service.Cache.SyncMetadata.lastSuccessfulSyncUtc) && !string.IsNullOrEmpty(service.Cache.SyncMetadata.lastAttemptUtc)
            && service.Cache.SyncMetadata.LastStatus == StudentDirectorySyncStatus.Succeeded && server.LastKey == "device-key");
        string onDisk = FileText(main);
        Check("[storage] directory persisted, and the file holds no plaintext code",
            onDisk != null && !onDisk.Contains(CodeA1) && !onDisk.Contains(CodeB) && !onDisk.Contains("K7M4-PQ2D"));

        // --- 2. local persistence reload ---
        var reloaded = new StudentDirectoryCache(new StudentDirectoryStore(main));
        var ra = reloaded.Lookup(CodeA1);
        Check("[reload] a fresh cache from disk (as after a restart) resolves the same code offline",
            ra.Outcome == StudentCodeLookupOutcome.Valid && ra.Student.studentId == IdA && reloaded.ActiveStudentCount == 2);

        // --- 3/4. existing cache + online update; new student appears ---
        server.Next = () => StudentDirectoryFetchResult.Ok(200, Body(
            new S { id = IdA, name = "Ana R. Santos", code = CodeA1, classes = new[] { ClassX, ClassY } },
            new S { id = IdB, name = "Ben Cruz", code = CodeB },
            new S { id = IdC, name = "Cara Lim", code = CodeC }));
        sync = service.SyncNow();
        yield return Await(sync);
        a = service.Cache.Lookup(CodeA1);
        Check("[update] changed name and classes replace the cached ones",
            sync.Result == StudentDirectorySyncStatus.Succeeded && a.Outcome == StudentCodeLookupOutcome.Valid
            && a.Student.displayName == "Ana R. Santos" && a.Student.classes.Count == 2);
        var c = service.Cache.Lookup(CodeC);
        Check("[new student] a student added on the server is recognized after sync", c.Outcome == StudentCodeLookupOutcome.Valid && c.Student.studentId == IdC);

        // --- 5. regenerated code replaces old cached code ---
        server.Next = () => StudentDirectoryFetchResult.Ok(200, Body(
            new S { id = IdA, name = "Ana R. Santos", code = CodeA2, version = 2 },
            new S { id = IdB, name = "Ben Cruz", code = CodeB },
            new S { id = IdC, name = "Cara Lim", code = CodeC }));
        sync = service.SyncNow();
        yield return Await(sync);
        var oldA = service.Cache.Lookup(CodeA1);
        var newA = service.Cache.Lookup(CodeA2);
        Check("[regenerated] old code no longer recognized; new code resolves to the SAME student id at version 2",
            oldA.Outcome == StudentCodeLookupOutcome.Unknown && newA.Outcome == StudentCodeLookupOutcome.Valid
            && newA.Student.studentId == IdA && newA.Student.codeVersion == 2);

        // --- 6. disabled code becomes invalid; 7. removed student drops out ---
        server.Next = () => StudentDirectoryFetchResult.Ok(200, Body(
            new S { id = IdA, name = "Ana R. Santos", code = CodeA2, version = 2 },
            new S { id = IdB, name = "Ben Cruz", code = CodeB, enabled = false }));
        sync = service.SyncNow();
        yield return Await(sync);
        var b = service.Cache.Lookup(CodeB);
        Check("[disabled] a server-disabled code is refused and reported as Disabled (not Unknown)",
            b.Outcome == StudentCodeLookupOutcome.Disabled && b.Student.studentId == IdB && service.Cache.ActiveStudentCount == 1);
        Check("[removed] a student missing from the server snapshot is no longer recognized",
            service.Cache.Lookup(CodeC).Outcome == StudentCodeLookupOutcome.Unknown);
        server.Next = () => StudentDirectoryFetchResult.Ok(200, Body(
            new S { id = IdA, name = "Ana R. Santos", code = CodeA2, version = 2 },
            new S { id = IdB, name = "Ben Cruz", code = CodeB }));
        sync = service.SyncNow();
        yield return Await(sync);
        Check("[re-enabled] re-enabling on the server makes the same code valid again",
            service.Cache.Lookup(CodeB).Outcome == StudentCodeLookupOutcome.Valid);
        Check("[unknown] a never-synced code stays Unknown - the headset can't invent students",
            service.Cache.Lookup("ZZZZ9999").Outcome == StudentCodeLookupOutcome.Unknown && service.Cache.Lookup("nope").Outcome == StudentCodeLookupOutcome.InvalidFormat);

        // --- 8. server unavailable with valid cache ---
        string lastGood = service.Cache.SyncMetadata.lastSuccessfulSyncUtc;
        string before = FileText(main);
        server.Next = () => StudentDirectoryFetchResult.Fail(StudentDirectorySyncStatus.NetworkUnavailable);
        sync = service.SyncNow();
        yield return Await(sync);
        Check("[offline + cache] status NetworkUnavailable, error recorded, cache still answers, last success unchanged",
            sync.Result == StudentDirectorySyncStatus.NetworkUnavailable && !string.IsNullOrEmpty(service.Cache.SyncMetadata.lastError)
            && service.Cache.Lookup(CodeA2).Outcome == StudentCodeLookupOutcome.Valid && service.Cache.SyncMetadata.lastSuccessfulSyncUtc == lastGood
            && SameStudents(before, FileText(main)));

        // --- refused key / server error leave the cache too ---
        foreach (var failure in new[] { StudentDirectorySyncStatus.Unauthorized, StudentDirectorySyncStatus.ServerError })
        {
            server.Next = () => StudentDirectoryFetchResult.Fail(failure, failure == StudentDirectorySyncStatus.Unauthorized ? 401 : 503);
            sync = service.SyncNow();
            yield return Await(sync);
            Check($"[{failure}] reported, cache untouched", sync.Result == failure && service.Cache.ActiveStudentCount == 2 && SameStudents(before, FileText(main)));
        }

        // --- 10. malformed responses never destroy the cache ---
        var malformed = new Dictionary<string, string>
        {
            { "not JSON", "<html>502 Bad Gateway</html>" },
            { "empty body", "" },
            { "success false", "{\"success\":false,\"error\":{\"code\":\"SERVER_ERROR\",\"message\":\"x\",\"retryable\":true}}" },
            { "no codeLookup", Body(new S { id = IdA, name = "A", code = CodeA2 }).Replace("\"codeLookup\"", "\"codeLookupX\"") },
            { "wrong algorithm", Body(new S { id = IdA, name = "A", code = CodeA2 }).Replace("PBKDF2-HMAC-SHA256", "SHA1") },
            { "missing students", "{\"success\":true,\"data\":{\"codeLookup\":{\"algorithm\":\"PBKDF2-HMAC-SHA256\",\"iterations\":2000,\"keyBytes\":32,\"saltBase64\":\"AAAAAAAAAAAAAAAAAAAAAA==\"}}}" },
            { "duplicate student id", Body(new S { id = IdA, name = "A", code = CodeA2 }, new S { id = IdA, name = "A again", code = CodeB }) },
            { "bad student id", Body(new S { id = "not-a-uuid", name = "A", code = CodeA2 }) },
            { "enabled without hash", Body(new S { id = IdA, name = "A", code = CodeA2 }).Replace("\"enabled\":true,\"codeHash\":\"", "\"enabled\":true,\"codeHash\":\"zz") },
        };
        foreach (var kv in malformed)
        {
            string body = kv.Value;
            server.Next = () => StudentDirectoryFetchResult.Ok(200, body);
            sync = service.SyncNow();
            yield return Await(sync);
            Check($"[malformed: {kv.Key}] rejected as InvalidResponse, cached directory unchanged",
                sync.Result == StudentDirectorySyncStatus.InvalidResponse && SameStudents(before, FileText(main))
                && service.Cache.Lookup(CodeA2).Outcome == StudentCodeLookupOutcome.Valid);
        }

        // --- 9. server unavailable with no cache ---
        string empty = Folder("empty");
        var emptyServer = new FakeTransport { Next = () => StudentDirectoryFetchResult.Fail(StudentDirectorySyncStatus.NetworkUnavailable) };
        var emptyService = NewService(emptyServer, empty);
        sync = emptyService.SyncNow();
        yield return Await(sync);
        var none = emptyService.Cache.Lookup(CodeA2);
        Check("[offline + no cache] no crash, status NetworkUnavailable, every code Unknown with DirectoryEmpty",
            sync.Result == StudentDirectorySyncStatus.NetworkUnavailable && emptyService.Cache.IsEmpty
            && none.Outcome == StudentCodeLookupOutcome.Unknown && none.DirectoryEmpty);

        // --- no device key ---
        var keyless = NewService(new FakeTransport { Next = () => StudentDirectoryFetchResult.Ok(200, Body()) }, Folder("keyless"), key: null);
        sync = keyless.SyncNow();
        yield return Await(sync);
        Check("[no device key] NotConfigured, no request made", sync.Result == StudentDirectorySyncStatus.NotConfigured);

        // --- 11. startup not blocked by a hung sync ---
        string hungFolder = Folder("hung");
        File.Copy(Path.Combine(main, StudentDirectoryStore.FileName), Path.Combine(hungFolder, StudentDirectoryStore.FileName));
        var hung = new FakeTransport { Hang = true };
        var hungService = NewService(hung, hungFolder, timeout: 1f);
        float t0 = Time.realtimeSinceStartup;
        sync = hungService.SyncNow();
        float returnedAfter = Time.realtimeSinceStartup - t0;
        bool usableImmediately = hungService.Cache.Lookup(CodeA2).Outcome == StudentCodeLookupOutcome.Valid;
        Check($"[startup] SyncNow returns at once ({returnedAfter * 1000f:0} ms) and the cached directory is usable while the server hangs",
            !sync.IsCompleted && hungService.IsSyncing && usableImmediately && returnedAfter < 0.5f);
        yield return Await(sync, 6f);
        Check("[startup] a hung request is abandoned after the timeout; cache kept",
            sync.IsCompleted && sync.Result == StudentDirectorySyncStatus.TimedOut && !hungService.IsSyncing
            && hungService.Cache.Lookup(CodeA2).Outcome == StudentCodeLookupOutcome.Valid);

        // --- 12. clear affects the local directory only ---
        string sentinelQueue = Path.Combine(main, "assessment_upload_queue_index.json");
        string sentinelLog = Path.Combine(main, "session_log_sentinel.jsonl");
        File.WriteAllText(sentinelQueue, "{\"entries\":[]}");
        File.WriteAllText(sentinelLog, "{\"event\":\"x\"}");
        int callsBefore = server.Calls;
        service.ClearLocalDirectory();
        var cleared = new StudentDirectoryCache(new StudentDirectoryStore(main));
        Check("[clear] directory emptied on disk, last success reset, status Cleared",
            cleared.IsEmpty && string.IsNullOrEmpty(cleared.SyncMetadata.lastSuccessfulSyncUtc) && cleared.SyncMetadata.LastStatus == StudentDirectorySyncStatus.Cleared
            && service.Cache.Lookup(CodeA2).Outcome == StudentCodeLookupOutcome.Unknown);
        Check("[clear] no server request made, other local files (upload queue, session logs) untouched",
            server.Calls == callsBefore && File.ReadAllText(sentinelQueue) == "{\"entries\":[]}" && File.ReadAllText(sentinelLog) == "{\"event\":\"x\"}");
        server.Next = () => StudentDirectoryFetchResult.Ok(200, Body(new S { id = IdA, name = "Ana R. Santos", code = CodeA2, version = 2 }));
        sync = service.SyncNow();
        yield return Await(sync);
        Check("[clear] the next successful sync restores the directory", service.Cache.Lookup(CodeA2).Outcome == StudentCodeLookupOutcome.Valid);

        // --- damaged cache file ---
        string damaged = Folder("damaged");
        File.WriteAllText(Path.Combine(damaged, StudentDirectoryStore.FileName), "{ this is not json");
        var fromDamaged = new StudentDirectoryCache(new StudentDirectoryStore(damaged));
        Check("[damaged file] loads empty instead of throwing, damaged file kept aside for inspection",
            fromDamaged.IsEmpty && Directory.GetFiles(damaged, StudentDirectoryStore.FileName + ".unreadable-*").Length == 1);

        try
        {
            Directory.Delete(root, recursive: true);
        }
        catch (Exception)
        {
            // temp folder; harmless if it lingers
        }

        report.AppendLine(failures.Count == 0 ? "=== PASS, 0 failures ===" : $"=== FAIL: {failures.Count} failure(s) ===");
        Report = report.ToString();
        IsDone = true;
    }

    /// <summary>Compares only the cached students section of two cache files (sync metadata legitimately changes on a failed attempt).</summary>
    private static bool SameStudents(string a, string b)
    {
        if (a == null || b == null)
        {
            return false;
        }
        var fa = JsonUtility.FromJson<StudentDirectoryCacheFile>(a);
        var fb = JsonUtility.FromJson<StudentDirectoryCacheFile>(b);
        return JsonUtility.ToJson(new StudentDirectoryCacheFile { students = fa.students }) == JsonUtility.ToJson(new StudentDirectoryCacheFile { students = fb.students });
    }
}
