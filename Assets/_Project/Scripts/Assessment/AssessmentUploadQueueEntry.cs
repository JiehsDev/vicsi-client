// Assets/_Project/Scripts/Assessment/AssessmentUploadQueueEntry.cs
using System;
using System.Collections.Generic;

/// <summary>
/// Lifecycle of one queued upload attempt. Explicit ints, appended-only forever after -
/// same discipline as every other persisted enum in this project (EvidenceStatus,
/// RelationshipState, ObjectiveState, ...), load-bearing here specifically because this
/// value is written to a JSON file on disk that must remain readable across app updates.
/// </summary>
public enum AssessmentUploadStatus
{
    Pending = 0,
    Uploading = 1,
    Uploaded = 2,
    RetryWaiting = 3,
    Rejected = 4,
    FailedPermanently = 5,

    /// <summary>
    /// Queued, but not currently attempted because the device isn't paired as the
    /// SAME identity that was paired when this entry was queued (see
    /// AssessmentUploadQueueEntry.pairedStudentDisplayName/pairedClassDisplayName and
    /// AssessmentUploadService's own "restart authentication" design - Option B: never
    /// persist the assessment token, require re-pairing after a restart, and never
    /// upload one student's queued session using a different student's token). Entries
    /// in this state sit safely on disk, attempted again automatically the moment
    /// PairingIdentityService reports the MATCHING student paired (see
    /// AssessmentUploadService.HandlePaired) - never sent under a mismatched identity.
    /// </summary>
    AwaitingPairing = 6
}

/// <summary>
/// Coarse category for the last error a queue entry hit - drives retry-vs-give-up
/// policy (see AssessmentUploadService) and is shown, human-readable, on the completion
/// screen. Deliberately coarser than an HTTP status code: the retry decision only ever
/// depends on which bucket a failure falls into, never the exact code.
/// </summary>
public enum AssessmentUploadErrorCategory
{
    None = 0,
    NoNetwork = 1,
    Timeout = 2,
    ValidationRejected = 3,     // HTTP 400 - permanent, never retried
    AuthenticationFailed = 4,   // HTTP 401/403 - permanent until a new login happens
    PayloadTooLarge = 5,        // HTTP 413 - permanent, retrying the same payload cannot succeed
    RateLimited = 6,            // HTTP 429 - retryable, honors Retry-After if present
    ServerError = 7,            // HTTP 5xx - retryable
    InvalidServerResponse = 8,  // malformed JSON etc - retryable, may indicate a server-side bug
    Unknown = 9,
    // VR path only: the server has no assignment for this scenario in any of the owner's
    // classes (NO_ASSIGNMENT). Recoverable business-rule state - the session waits,
    // retryable, until an instructor creates the assignment. Never uploaded elsewhere.
    NoAssignment = 10,
    // The app closed while this entry was Uploading, so the server's answer is unknown.
    // Retried (idempotent by sessionId + payload hash) - see AssessmentUploadQueueStore.RecoverInterruptedUploads.
    Interrupted = 11
}

/// <summary>
/// One row of the local pending-upload queue's index. Everything here is metadata about
/// an upload attempt; the actual payload JSON lives in its own file at
/// localPayloadPath - see AssessmentUploadQueueStore's own class comment for why the
/// index and the payload bodies are deliberately separate files.
/// </summary>
[Serializable]
public class AssessmentUploadQueueEntry
{
    public string sessionId;
    public int payloadVersion;
    public string localPayloadPath;

    public string createdAtUtc;
    public string lastAttemptedAtUtc;
    public string nextAttemptAtUtc;
    public int attemptCount;

    public AssessmentUploadStatus status = AssessmentUploadStatus.Pending;
    public AssessmentUploadErrorCategory lastErrorCategory = AssessmentUploadErrorCategory.None;
    public string lastErrorMessage;
    public string serverReceiptId;

    // Informational only, for the queue list / Editor tool / completion-screen status
    // to display without re-opening the payload file - same non-authoritative role as
    // AssessmentSessionUploadDto.studentId/classId, and subject to the identical rule:
    // never trusted as identity by anything that enforces access control.
    public string studentIdForDisplay;
    public string scenarioIdForDisplay;

    // SECURITY BOUNDARY, load-bearing (Option B restart-authentication design): captured
    // from PairingIdentityService at the moment this entry was queued - the identity that
    // was actually paired when this session was PLAYED. Before any real upload attempt,
    // AssessmentUploadService requires the CURRENTLY paired identity to match these two
    // fields exactly; a mismatch (including "not currently paired at all") means the
    // entry sits at AwaitingPairing and is never sent. This is what stops one student's
    // queued session from being uploaded under a different student's token after a
    // shared-headset restart - see AssessmentUploadService.IdentityMatchesQueuedEntry.
    public string pairedStudentDisplayName;
    public string pairedClassDisplayName;

    // VR-CODE OWNERSHIP (Phase 3) - copied from the session's SessionOwnerRecord when
    // queued. When ownerStudentId is set, this entry uploads through the device-
    // authenticated bridge: the headset requests a fresh, short-lived upload token for
    // THIS student and THIS session right before each attempt, so whoever is identified
    // on the headset later is irrelevant. Empty = a legacy pairing-token entry, handled
    // exactly as before. No token is ever stored here.
    public string ownerStudentId;
    public string ownerDisplayNameSnapshot;
    public string ownerClassIdHint;
    public bool isTestSession;
    public string sessionStartedAtUtc;

    public bool UsesVrOwnership => !string.IsNullOrEmpty(ownerStudentId);

    // LIFECYCLE (Phase 4). All ISO-8601 UTC; empty/null = not applicable yet.
    /// <summary>When the session finished on the headset (payload completedAtUtc, else queue time). Display only.</summary>
    public string sessionCompletedAtUtc;
    /// <summary>The SERVER's receipt time for this session (serverReceivedAtUtc from the upload response - the original receipt time even for a duplicate). Set only on a server-confirmed upload; the 30-day local retention counts from here.</summary>
    public string uploadConfirmedAtUtc;
    /// <summary>The headset's own clock at the moment the confirmation arrived - a second, independent guard against a wrong device or server clock (see UploadedSessionRetention).</summary>
    public string uploadConfirmedLocalAtUtc;
    /// <summary>uploadConfirmedAtUtc + 30 days: when the local copy becomes eligible for cleanup. Display value; eligibility is always re-checked.</summary>
    public string localExpiresAtUtc;
}

/// <summary>JsonUtility-serializable root wrapper - JsonUtility.ToJson cannot serialize a bare List/array at the document root.</summary>
[Serializable]
public class AssessmentUploadQueueIndex
{
    public int indexVersion = 1;
    public List<AssessmentUploadQueueEntry> entries = new();

    /// <summary>Latest SERVER time this headset has seen (from upload receipts). Cleanup never trusts the device clock beyond this plus a small grace - see UploadedSessionRetention.</summary>
    public string lastKnownServerTimeUtc;
}
