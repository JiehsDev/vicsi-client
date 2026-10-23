// Assets/_Project/Scripts/Assessment/SessionOwnership.cs
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>Who a student was identified as on the pre-session screen - by code (online or offline) or Continue Without Code. In memory only.</summary>
public class IdentifiedStudent
{
    public string StudentId;          // stable server id (profiles.id) - the ONLY ownership key
    public string DisplayName;
    public List<CachedStudentClass> Classes = new();
    public bool IsTestSession;        // Continue Without Code -> the existing test student
    public string Source;             // "online-code" | "offline-code" | "test-mode" (diagnostic only)

    public CachedStudentClass PrimaryClass => Classes.Count > 0 ? Classes[0] : null;

    public string ClassDisplay
    {
        get
        {
            var c = PrimaryClass;
            if (c == null) return string.Empty;
            return string.IsNullOrEmpty(c.classSection) ? c.className : $"{c.className} - {c.classSection}";
        }
    }
}

/// <summary>
/// The permanent owner of one session, written once when the session starts.
/// Uploads read the owner from HERE - never from whoever is identified on the headset
/// at upload time - so a session played offline by Juan still uploads as Juan after
/// Maria has logged in. Holds no credential of any kind.
/// </summary>
[Serializable]
public class SessionOwnerRecord
{
    public string sessionId;
    public string studentId;
    public string displayNameSnapshot;   // display only; never sent as identity
    public string classIdHint;           // the class shown at login; the server may still pick another enrolled class
    public string classDisplaySnapshot;
    public bool isTestSession;
    public string sessionStartedAtUtc;   // when ownership was bound (= the session became playable)
}

[Serializable]
public class SessionOwnerFile
{
    public int schemaVersion = 1;
    public List<SessionOwnerRecord> sessions = new();
}

/// <summary>
/// File-backed, write-once store of session owners at
/// &lt;persistentDataPath&gt;/vicsi_session_owners.json (same crash-safe write as the
/// upload queue). TryBind never replaces an existing record: once a session id has an
/// owner, no later login, code entry, directory sync or bind call can change it.
/// </summary>
public class SessionOwnershipStore
{
    public const string FileName = "vicsi_session_owners.json";

    public string FilePath { get; }

    public SessionOwnershipStore(string rootDirectory = null)
    {
        string root = string.IsNullOrEmpty(rootDirectory) ? Application.persistentDataPath : rootDirectory;
        FilePath = Path.Combine(root, FileName);
    }

    public SessionOwnerRecord Find(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId)) return null;
        return Load().sessions.Find(r => string.Equals(r.sessionId, sessionId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Binds sessionId to the student. Returns the record now on file: the new one, or - if this session already had an owner - the ORIGINAL one, unchanged.</summary>
    public SessionOwnerRecord TryBind(string sessionId, IdentifiedStudent student, DateTime startedAtUtc, out bool created)
    {
        created = false;
        if (string.IsNullOrEmpty(sessionId) || student == null || string.IsNullOrEmpty(student.StudentId))
        {
            return null;
        }

        var file = Load();
        var existing = file.sessions.Find(r => string.Equals(r.sessionId, sessionId, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            return existing;
        }

        var record = new SessionOwnerRecord
        {
            sessionId = sessionId,
            studentId = student.StudentId,
            displayNameSnapshot = student.DisplayName,
            classIdHint = student.PrimaryClass?.classId,
            classDisplaySnapshot = student.ClassDisplay,
            isTestSession = student.IsTestSession,
            sessionStartedAtUtc = startedAtUtc.ToUniversalTime().ToString("o"),
        };
        file.sessions.Add(record);
        AssessmentUploadQueueStore.WriteAtomic(FilePath, JsonUtility.ToJson(file, prettyPrint: true));
        created = true;
        return record;
    }

    /// <summary>
    /// Removes the owner record for one session. ONLY for a session the device no longer
    /// holds at all - UploadedSessionRetention calls it after removing an uploaded
    /// session's local copy at the end of its 30-day retention, and only when no queue
    /// entry with that sessionId remains. Never call it for a session that is still
    /// queued, retrying, uploading, or inside retention. Returns false if absent.
    /// </summary>
    public bool Remove(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId)) return false;
        var file = Load();
        int removed = file.sessions.RemoveAll(r => string.Equals(r.sessionId, sessionId, StringComparison.OrdinalIgnoreCase));
        if (removed > 0)
        {
            AssessmentUploadQueueStore.WriteAtomic(FilePath, JsonUtility.ToJson(file, prettyPrint: true));
        }
        return removed > 0;
    }

    /// <summary>Session ids currently on file - diagnostics and tests.</summary>
    public List<string> SessionIds() => Load().sessions.ConvertAll(r => r.sessionId);

    private SessionOwnerFile Load()
    {
        if (!File.Exists(FilePath))
        {
            return new SessionOwnerFile();
        }
        try
        {
            var file = JsonUtility.FromJson<SessionOwnerFile>(File.ReadAllText(FilePath));
            if (file == null) throw new InvalidDataException("empty");
            file.sessions ??= new();
            return file;
        }
        catch (Exception ex)
        {
            // Never silently drop owners: refuse to continue with a damaged file rather
            // than start a fresh one that would let a session be re-bound to someone else.
            throw new InvalidDataException($"Session owner file is unreadable ({ex.GetType().Name}) at {FilePath}; refusing to bind or upload until it is inspected.", ex);
        }
    }
}

/// <summary>Scene-free entry point: the default (persistentDataPath) store, swappable for tests.</summary>
public static class SessionOwnership
{
    private static SessionOwnershipStore store;

    public static SessionOwnershipStore Store
    {
        get => store ??= new SessionOwnershipStore();
        set => store = value;
    }

    /// <summary>Binds the CURRENT SessionLogger session to the identified student. Call once, when the student confirms identity and before play begins.</summary>
    public static SessionOwnerRecord BindCurrentSession(IdentifiedStudent student)
    {
        string sessionId = SessionLogger.Instance != null ? SessionLogger.Instance.SessionId : null;
        if (string.IsNullOrEmpty(sessionId))
        {
            Debug.LogError("[SessionOwnership] No running SessionLogger session to bind.");
            return null;
        }
        var record = Store.TryBind(sessionId, student, DateTime.UtcNow, out bool created);
        if (!created && record != null && record.studentId != student.StudentId)
        {
            Debug.LogWarning($"[SessionOwnership] Session {sessionId} already belongs to another student; ownership unchanged.");
        }
        return record;
    }
}
