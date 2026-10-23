using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>Exercises actual JSONL disk persistence using unique temporary session IDs.</summary>
public class SessionPersistenceTest : MonoBehaviour
{
    public string RunAllTests()
    {
        var report = new StringBuilder("=== SessionPersistenceTest ===\n");
        int passed = 0, failed = 0;
        Action<string, Func<bool>> check = (name, test) =>
        {
            try { if (test()) { passed++; report.AppendLine("PASS: " + name); }
                else { failed++; report.AppendLine("FAIL: " + name); } }
            catch (Exception e) { failed++; report.AppendLine("FAIL: " + name + " (" + e.GetType().Name + ")"); }
        };
        var previousLogger = SessionLogger.Instance;
        var go = new GameObject("SessionPersistenceTestRig");
        go.SetActive(false);
        var writer = go.AddComponent<LocalJsonLogWriter>();
        var logger = go.AddComponent<SessionLogger>();
        var failureGo = new GameObject("SessionPersistenceFailureRig");
        var retryWriter = failureGo.AddComponent<LocalJsonLogWriter>();
        string logPath = null, retryPath = null;
        try
        {
            typeof(SessionLogger).GetField("writerSource", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(logger, writer);
            go.SetActive(true);
            logPath = writer.FilePath;
            check("session start immediately persisted", () => File.Exists(logPath) && File.ReadAllLines(logPath).Length == 1);
            check("start UTC timestamp parses", () =>
            {
                var start = JsonUtility.FromJson<SessionEvent>(File.ReadAllLines(logPath)[0]);
                return start.eventType == "SessionStarted" && start.payload.Exists(p => p.key == "startedAtUtc" && DateTimeOffset.TryParse(p.value, out _));
            });
            logger.LogEvent(SessionEventType.PhotoTaken, "test-evidence");
            check("event buffered before pause", () => writer.PendingEventCount == 1);
            logger.SendMessage("OnApplicationPause", true);
            check("pause persists pending event", () => writer.PendingEventCount == 0 && File.ReadAllLines(logPath).Length == 2);
            logger.EndSession();
            logger.EndSession();
            logger.LogEvent(SessionEventType.PhotoTaken, "after-end");
            check("session end is idempotent and rejects later events", () => File.ReadAllLines(logPath).Length == 3);
            check("JSONL sequence and session ID are consistent", () =>
            {
                var lines = File.ReadAllLines(logPath);
                for (int i=0; i<lines.Length; i++)
                {
                    var evt = JsonUtility.FromJson<SessionEvent>(lines[i]);
                    if (evt.sequenceNumber != i || evt.sessionId != logger.SessionId || evt.timestampMs < 0) return false;
                }
                return true;
            });
            check("end UTC timestamp persisted", () => JsonUtility.FromJson<SessionEvent>(File.ReadAllLines(logPath)[2])
                .payload.Exists(p => p.key == "endedAtUtc" && DateTimeOffset.TryParse(p.value, out _)));
            retryWriter.Initialize(Guid.NewGuid().ToString());
            retryPath = retryWriter.FilePath;
            // A directory at the file path causes a real platform IO/access error.
            Directory.CreateDirectory(retryPath);
            retryWriter.WriteEvent(new SessionEvent { sessionId = "fixture", sequenceNumber = 0, eventType = "PhotoTaken" });
            retryWriter.Flush();
            check("storage failure retains buffered event", () => !retryWriter.LastFlushSucceeded && retryWriter.PendingEventCount == 1);
            Directory.Delete(retryPath); // unique empty directory created by this test
            retryWriter.Flush();
            check("retry persists retained event exactly once", () => retryWriter.LastFlushSucceeded && retryWriter.PendingEventCount == 0 && File.ReadAllLines(retryPath).Length == 1);
            retryWriter.Flush();
            check("repeated flush does not duplicate lines", () => File.ReadAllLines(retryPath).Length == 1);
            check("writer cannot switch sessions and lose buffered data", () =>
            {
                try { retryWriter.Initialize(Guid.NewGuid().ToString()); return false; }
                catch (InvalidOperationException) { return true; }
            });
            var invalid = failureGo.AddComponent<LocalJsonLogWriter>();
            check("path traversal session ID rejected", () =>
            {
                try { invalid.Initialize("../outside"); return false; }
                catch (ArgumentException) { return invalid.FilePath == null; }
            });
        }
        finally
        {
            DestroyImmediate(go);
            DestroyImmediate(failureGo);
            typeof(SessionLogger).GetProperty("Instance").SetValue(null, previousLogger);
            // Only exact paths created by this test are removed, never scene logs.
            foreach (var file in new[] { logPath, retryPath })
                if (file != null) { if (File.Exists(file)) File.Delete(file); else if (Directory.Exists(file)) Directory.Delete(file); }
        }
        report.AppendLine($"=== {(failed == 0 ? "PASS" : "FAIL")}: {passed} passed, {failed} failed ===");
        return report.ToString();
    }
}
