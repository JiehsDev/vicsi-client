// Assets/_Project/Scripts/Assessment/StudentDirectory/StudentDirectoryCache.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// The headset's offline student directory: loads the persisted copy, merges an
/// authoritative server snapshot into it, answers "which student is this typed code?"
/// without a network, and records sync metadata for the future Instructor
/// Management screen. Plain class (no scene needed); StudentDirectoryService drives it
/// at startup.
///
/// MERGE RULES - the server snapshot is authoritative for everything it covers:
///   - A response is validated COMPLETELY before anything changes. Any malformed part
///     (bad envelope, unknown hash algorithm, bad salt, a student without an id/name,
///     a duplicate id, an enabled student without a well-formed hash) rejects the
///     whole response and leaves the cached directory exactly as it was.
///   - New students are added; names, classes, versions and codes are replaced.
///   - Regenerated code (higher codeVersion): the new code's hash replaces the old one,
///     so the old code becomes Unknown here from that sync on.
///   - Disabled code: the entry stays, enabled=false. If this headset had the hash of
///     the same code version, it is kept so the typed code reports Disabled (not
///     Unknown); a code regenerated while disabled has no usable hash and reads Unknown.
///   - Students missing from the snapshot (archived class, removed enrollment, deleted
///     profile) are dropped - they become Unknown.
///   - A request that never produced a valid response (offline, timeout, 401, 5xx,
///     garbage) changes nothing but the sync metadata.
///
/// ACCEPTED LIMITATION - regenerated codes on an offline headset: if an instructor
/// regenerates or disables a code on the web while this headset is offline, the headset
/// keeps accepting the OLD code until its next successful sync. There is deliberately no
/// online revocation check; the next successful sync is the revocation.
///
/// Codes never expire on the headset. The cache only changes on a successful sync or an
/// explicit Clear().
/// </summary>
public class StudentDirectoryCache
{
    private static readonly Regex HexHash = new("^[0-9a-fA-F]+$");
    private static readonly Regex StudentsKey = new("\"students\"\\s*:\\s*\\[");
    private static readonly Regex CodeLookupKey = new("\"codeLookup\"\\s*:\\s*\\{");

    private readonly StudentDirectoryStore store;
    private StudentDirectoryCacheFile file;

    public StudentDirectoryCache(StudentDirectoryStore store)
    {
        this.store = store;
        file = store.Load();
    }

    public IReadOnlyList<CachedStudentEntry> Students => file.students;
    public StudentDirectorySyncMetadata SyncMetadata => file.sync;
    public int ActiveStudentCount => file.students.FindAll(s => s.enabled && s.HasCodeHash).Count;
    public bool IsEmpty => file.students.Count == 0;
    /// <summary>The existing test student for "Continue Without Code", as last synced. Check IsKnown.</summary>
    public CachedTestStudent TestStudent => file.testStudent ??= new CachedTestStudent();

    /// <summary>Re-reads the persisted file (e.g. to prove a restart sees the same directory).</summary>
    public void Reload() => file = store.Load();

    // ------------------------------------------------------------------ sync bookkeeping

    /// <summary>Stamps the attempt time. The in-progress state itself is NOT persisted (StudentDirectoryService.IsSyncing) - a crash mid-sync must not leave "Syncing" on disk forever.</summary>
    public void RecordAttemptStarted(DateTime nowUtc)
    {
        file.sync.lastAttemptUtc = Iso(nowUtc);
    }

    /// <summary>Records a sync that produced no usable directory. The cached students are untouched.</summary>
    public void RecordFailure(StudentDirectorySyncStatus status, string safeMessage, DateTime nowUtc)
    {
        if (string.IsNullOrEmpty(file.sync.lastAttemptUtc)) file.sync.lastAttemptUtc = Iso(nowUtc);
        file.sync.lastStatus = status.ToString();
        file.sync.lastError = safeMessage;
        TrySave(out _);
    }

    // ------------------------------------------------------------------ merge

    /// <summary>
    /// Validates and merges one server response body. Returns the resulting status:
    /// Succeeded, InvalidResponse (nothing changed), or SaveFailed (merged in memory,
    /// previous file still on disk).
    /// </summary>
    public StudentDirectorySyncStatus ApplyServerResponse(string responseJson, DateTime nowUtc)
    {
        if (!TryParseAndValidate(responseJson, out var data, out string problem))
        {
            RecordFailure(StudentDirectorySyncStatus.InvalidResponse, "Directory response rejected: " + problem, nowUtc);
            return StudentDirectorySyncStatus.InvalidResponse;
        }

        var previous = new Dictionary<string, CachedStudentEntry>();
        foreach (var entry in file.students)
        {
            previous[entry.studentId] = entry;
        }

        string syncedAt = Iso(nowUtc);
        var lookup = data.codeLookup;
        var merged = new List<CachedStudentEntry>(data.students.Count);
        foreach (var s in data.students)
        {
            var entry = new CachedStudentEntry
            {
                studentId = s.studentId,
                displayName = s.displayName.Trim(),
                codeVersion = s.codeVersion,
                enabled = s.enabled,
                serverUpdatedAtUtc = s.updatedAtUtc,
                lastSyncedAtUtc = syncedAt,
            };
            if (s.classes != null)
            {
                foreach (var c in s.classes)
                {
                    if (c != null && !string.IsNullOrEmpty(c.classId))
                    {
                        entry.classes.Add(new CachedStudentClass { classId = c.classId, className = c.className, classSection = string.IsNullOrEmpty(c.classSection) ? null : c.classSection });
                    }
                }
            }

            if (s.enabled)
            {
                entry.codeHash = s.codeHash.ToLowerInvariant();
                entry.lookupSaltBase64 = lookup.saltBase64;
                entry.lookupIterations = lookup.iterations;
                entry.lookupKeyBytes = lookup.keyBytes;
            }
            else if (previous.TryGetValue(s.studentId, out var old) && old.HasCodeHash && old.codeVersion == s.codeVersion)
            {
                // Same code, now disabled: keep its hash so a typed attempt reads Disabled.
                entry.codeHash = old.codeHash;
                entry.lookupSaltBase64 = old.lookupSaltBase64;
                entry.lookupIterations = old.lookupIterations;
                entry.lookupKeyBytes = old.lookupKeyBytes;
            }
            merged.Add(entry);
        }

        file.students = merged;
        file.testStudent = ToCachedTestStudent(data.testStudent);
        if (string.IsNullOrEmpty(file.sync.lastAttemptUtc)) file.sync.lastAttemptUtc = syncedAt;
        file.sync.lastSuccessfulSyncUtc = syncedAt;
        file.sync.lastServerGeneratedAtUtc = data.generatedAtUtc;
        file.sync.lastStatus = nameof(StudentDirectorySyncStatus.Succeeded);
        file.sync.lastError = null;

        if (!TrySave(out string saveError))
        {
            file.sync.lastStatus = nameof(StudentDirectorySyncStatus.SaveFailed);
            file.sync.lastError = "Directory updated for this session but could not be saved: " + saveError;
            return StudentDirectorySyncStatus.SaveFailed;
        }
        return StudentDirectorySyncStatus.Succeeded;
    }

    private static bool TryParseAndValidate(string json, out StudentDirectoryDataDto data, out string problem)
    {
        data = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            problem = "empty body";
            return false;
        }

        StudentDirectoryResponseDto response;
        try
        {
            response = JsonUtility.FromJson<StudentDirectoryResponseDto>(json);
        }
        catch (Exception)
        {
            problem = "not JSON";
            return false;
        }
        if (response == null || !response.success || response.data == null)
        {
            problem = "not a successful directory envelope";
            return false;
        }
        // JsonUtility silently substitutes an empty list / default object for a MISSING
        // key, which would make a truncated or wrong-shaped body look like a valid, empty
        // roster and wipe the cache. Require both keys to actually be present.
        if (!StudentsKey.IsMatch(json) || !CodeLookupKey.IsMatch(json))
        {
            problem = "students or codeLookup missing";
            return false;
        }

        data = response.data;
        var lookup = data.codeLookup;
        if (lookup == null || lookup.algorithm != StudentAccessCodes.LookupAlgorithm
            || lookup.iterations < 1000 || lookup.iterations > 5_000_000
            || lookup.keyBytes < 16 || lookup.keyBytes > 64
            || (lookup.input != null && lookup.input != "canonical-code-utf8")
            || (lookup.output != null && lookup.output != "hex"))
        {
            problem = "unsupported code lookup parameters";
            return false;
        }
        try
        {
            if (Convert.FromBase64String(lookup.saltBase64 ?? string.Empty).Length < 8)
            {
                problem = "salt too short";
                return false;
            }
        }
        catch (FormatException)
        {
            problem = "salt is not base64";
            return false;
        }

        if (data.students == null)
        {
            problem = "missing students list";
            return false;
        }

        var seen = new HashSet<string>();
        foreach (var s in data.students)
        {
            if (s == null || string.IsNullOrWhiteSpace(s.studentId) || !Guid.TryParse(s.studentId, out _))
            {
                problem = "a student has no valid id";
                return false;
            }
            if (!seen.Add(s.studentId))
            {
                problem = "duplicate student id";
                return false;
            }
            if (string.IsNullOrWhiteSpace(s.displayName))
            {
                problem = "a student has no display name";
                return false;
            }
            if (s.codeVersion < 1)
            {
                problem = "a student has an invalid code version";
                return false;
            }
            if (s.enabled && (s.codeHash == null || s.codeHash.Length != lookup.keyBytes * 2 || !HexHash.IsMatch(s.codeHash)))
            {
                problem = "an enabled student has no well-formed code hash";
                return false;
            }
        }

        problem = null;
        return true;
    }

    // ------------------------------------------------------------------ lookup

    /// <summary>
    /// Resolves a typed code against the cached directory, offline. Derives the PBKDF2
    /// hash once per distinct salt in the cache (normally one), so it costs a few tens of
    /// milliseconds on device - call it off the main thread (see LookupAsync).
    /// </summary>
    public StudentCodeLookupResult Lookup(string typedCode)
    {
        var result = new StudentCodeLookupResult { DirectoryEmpty = IsEmpty };
        string canonical = StudentAccessCodes.Normalize(typedCode);
        if (canonical == null)
        {
            result.Outcome = StudentCodeLookupOutcome.InvalidFormat;
            return result;
        }

        var derivedBySalt = new Dictionary<string, string>();
        CachedStudentEntry disabledMatch = null;
        foreach (var entry in file.students.ToArray())
        {
            if (!entry.HasCodeHash)
            {
                continue;
            }
            string key = entry.lookupSaltBase64 + "|" + entry.lookupIterations + "|" + entry.lookupKeyBytes;
            if (!derivedBySalt.TryGetValue(key, out string derived))
            {
                derived = StudentAccessCodes.DeriveLookupHash(canonical, entry.lookupSaltBase64, entry.lookupIterations, entry.lookupKeyBytes);
                derivedBySalt[key] = derived;
            }
            if (!StudentAccessCodes.HashEquals(derived, entry.codeHash))
            {
                continue;
            }
            if (entry.enabled)
            {
                result.Outcome = StudentCodeLookupOutcome.Valid;
                result.Student = entry;
                return result;
            }
            disabledMatch ??= entry;
        }

        result.Outcome = disabledMatch != null ? StudentCodeLookupOutcome.Disabled : StudentCodeLookupOutcome.Unknown;
        result.Student = disabledMatch;
        return result;
    }

    public System.Threading.Tasks.Task<StudentCodeLookupResult> LookupAsync(string typedCode) =>
        System.Threading.Tasks.Task.Run(() => Lookup(typedCode));

    // ------------------------------------------------------------------ clear

    /// <summary>
    /// Empties THIS HEADSET's cached directory and its last-successful-sync time, and
    /// saves. Local only: it never contacts the server, so no student, session, grade or
    /// server code is touched. The headset recognizes no code until its next successful
    /// sync. The local upload queue and session logs are separate files and untouched.
    /// </summary>
    public void Clear(DateTime nowUtc)
    {
        file.students = new List<CachedStudentEntry>();
        file.testStudent = new CachedTestStudent();
        file.sync.lastSuccessfulSyncUtc = null;
        file.sync.lastServerGeneratedAtUtc = null;
        file.sync.lastStatus = nameof(StudentDirectorySyncStatus.Cleared);
        file.sync.lastError = null;
        if (string.IsNullOrEmpty(file.sync.lastAttemptUtc)) file.sync.lastAttemptUtc = Iso(nowUtc);
        TrySave(out _);
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>The server's test student, or an empty (unknown) one if absent or malformed - never invented.</summary>
    private static CachedTestStudent ToCachedTestStudent(StudentDirectoryTestStudentDto dto)
    {
        var cached = new CachedTestStudent();
        if (dto == null || string.IsNullOrWhiteSpace(dto.displayName) || !Guid.TryParse(dto.studentId, out _))
        {
            return cached;
        }
        cached.studentId = dto.studentId;
        cached.displayName = dto.displayName.Trim();
        if (dto.classes != null)
        {
            foreach (var c in dto.classes)
            {
                if (c != null && !string.IsNullOrEmpty(c.classId))
                {
                    cached.classes.Add(new CachedStudentClass { classId = c.classId, className = c.className, classSection = string.IsNullOrEmpty(c.classSection) ? null : c.classSection });
                }
            }
        }
        return cached;
    }

    private bool TrySave(out string error)
    {
        try
        {
            store.Save(file);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.GetType().Name;
            Debug.LogError($"[StudentDirectoryCache] Could not save the student directory ({ex.GetType().Name}).");
            return false;
        }
    }

    private static string Iso(DateTime utc) => utc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
}
