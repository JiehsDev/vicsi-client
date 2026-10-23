// Assets/_Project/Scripts/Assessment/StudentDirectory/StudentDirectoryModels.cs
using System;
using System.Collections.Generic;

// ---------------------------------------------------------------------------
// Server contract - GET /api/v1/vr/student-directory (vicsi-dashboard,
// src/app/api/v1/vr/student-directory/route.ts + src/lib/vrAccessCodes.ts).
// JsonUtility shapes only; never cached as-is (see StudentDirectoryCache).
// ---------------------------------------------------------------------------

[Serializable]
public class StudentDirectoryResponseDto
{
    public bool success;
    public StudentDirectoryDataDto data;
}

[Serializable]
public class StudentDirectoryDataDto
{
    public string generatedAtUtc;
    public string classId;
    public StudentDirectoryCodeLookupDto codeLookup;
    public List<StudentDirectoryStudentDto> students;
    /// <summary>The EXISTING test student ("Continue Without Code"), resolved server-side; empty studentId = none configured.</summary>
    public StudentDirectoryTestStudentDto testStudent;
}

[Serializable]
public class StudentDirectoryTestStudentDto
{
    public string studentId;
    public string displayName;
    public List<StudentDirectoryClassDto> classes;
}

/// <summary>How the server hashed each code in this response: PBKDF2-HMAC-SHA256 of the canonical code, fresh salt per response.</summary>
[Serializable]
public class StudentDirectoryCodeLookupDto
{
    public string algorithm;
    public int iterations;
    public int keyBytes;
    public string saltBase64;
    public string input;
    public string output;
}

[Serializable]
public class StudentDirectoryStudentDto
{
    public string studentId;
    public string displayName;
    public List<StudentDirectoryClassDto> classes;
    public int codeVersion;
    public bool enabled;
    public string codeHash;      // null/empty when disabled
    public string updatedAtUtc;
}

[Serializable]
public class StudentDirectoryClassDto
{
    public string classId;
    public string className;
    public string classSection;
}

// ---------------------------------------------------------------------------
// Local cache - persisted by StudentDirectoryStore. Holds only what the headset
// needs to recognize a typed code offline: never the code itself, only its
// salted PBKDF2 hash with the parameters it was made with.
// ---------------------------------------------------------------------------

[Serializable]
public class CachedStudentClass
{
    public string classId;
    public string className;
    public string classSection;
}

[Serializable]
public class CachedStudentEntry
{
    /// <summary>Stable server student id (profiles.id). The only identity a session may ever be attributed to.</summary>
    public string studentId;
    public string displayName;
    public List<CachedStudentClass> classes = new();
    public int codeVersion;
    /// <summary>False = the server disabled this code. A disabled entry keeps the hash of the code it had when it was last enabled (same version), so a typed disabled code is reported as Disabled rather than Unknown.</summary>
    public bool enabled;
    /// <summary>Hex PBKDF2 of the canonical code; empty when this headset never saw the code enabled at its current version.</summary>
    public string codeHash;
    public string lookupSaltBase64;
    public int lookupIterations;
    public int lookupKeyBytes;
    public string serverUpdatedAtUtc;
    public string lastSyncedAtUtc;

    public bool HasCodeHash => !string.IsNullOrEmpty(codeHash) && !string.IsNullOrEmpty(lookupSaltBase64) && lookupIterations > 0 && lookupKeyBytes > 0;
}

/// <summary>Why the last sync ended the way it did. Shown later in Instructor Management.</summary>
public enum StudentDirectorySyncStatus
{
    NeverAttempted,
    Syncing,
    Succeeded,
    NotConfigured,      // no valid server URL or no device key on this headset
    NetworkUnavailable,
    TimedOut,
    Unauthorized,       // server refused the device key
    ServerError,
    InvalidResponse,    // reached the server but the body failed validation - cache untouched
    SaveFailed,         // merged in memory but couldn't be written to disk
    Cleared,            // local cache cleared on purpose
}

[Serializable]
public class StudentDirectorySyncMetadata
{
    // Timestamps are ISO-8601 UTC. "Never" is null in memory but "" after a reload
    // (JsonUtility writes null strings as "") - always test with string.IsNullOrEmpty.
    public string lastAttemptUtc;
    public string lastSuccessfulSyncUtc;
    public string lastStatus = nameof(StudentDirectorySyncStatus.NeverAttempted);
    /// <summary>Safe, short, human-readable. Never contains a code, a hash, the device key, or a raw server body.</summary>
    public string lastError;
    public string lastServerGeneratedAtUtc;

    public StudentDirectorySyncStatus LastStatus =>
        Enum.TryParse(lastStatus, out StudentDirectorySyncStatus parsed) ? parsed : StudentDirectorySyncStatus.NeverAttempted;
}

[Serializable]
public class StudentDirectoryCacheFile
{
    public int schemaVersion = StudentDirectoryStore.CurrentSchemaVersion;
    public List<CachedStudentEntry> students = new();
    /// <summary>The test student "Continue Without Code" assigns sessions to. Empty studentId = none known yet.</summary>
    public CachedTestStudent testStudent = new();
    public StudentDirectorySyncMetadata sync = new();
}

[Serializable]
public class CachedTestStudent
{
    public string studentId;
    public string displayName;
    public List<CachedStudentClass> classes = new();
    public bool IsKnown => !string.IsNullOrEmpty(studentId) && !string.IsNullOrEmpty(displayName);
}

/// <summary>What a typed code resolves to against the local cache - the three cases the future login screen must tell apart, plus malformed input.</summary>
public enum StudentCodeLookupOutcome
{
    InvalidFormat,
    Unknown,    // not in this headset's directory (never synced here, removed, or replaced by a regenerated code)
    Disabled,   // matches a cached code the server has disabled
    Valid,
}

public class StudentCodeLookupResult
{
    public StudentCodeLookupOutcome Outcome;
    /// <summary>Set for Valid and Disabled.</summary>
    public CachedStudentEntry Student;
    /// <summary>True when the headset has no directory at all yet (never synced) - lets a UI say "this headset needs to sync" instead of "wrong code".</summary>
    public bool DirectoryEmpty;
}
