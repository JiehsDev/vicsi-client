// Assets/_Project/Scripts/Assessment/AssessmentUploadConfiguration.cs
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Everything about session upload that must be changeable WITHOUT editing C# -
/// endpoint, timeouts, retry policy, and the scenario/version identifiers the DTO
/// stamps onto every upload. Deliberately holds no secret - no Supabase key, no
/// database password, nothing bearer-token-shaped lives here; see the class comment on
/// AssessmentUploadService for why (the server session token is runtime-acquired per
/// player, never an asset-embedded credential).
///
/// One asset per deployment environment (Development/Staging/Production), selected by
/// whichever one is assigned to AssessmentUploadService's own [SerializeField] - not a
/// single asset with a dropdown, so a build simply ships with the correct environment's
/// asset referenced and there is no runtime branch that could pick the wrong one.
/// </summary>
[CreateAssetMenu(fileName = "AssessmentUploadConfig_", menuName = "VR-CSI/Assessment/Upload Configuration")]
public class AssessmentUploadConfiguration : ScriptableObject
{
    [Header("Environment")]
    [Tooltip("Human-readable label for this configuration, e.g. \"Development\", \"Staging\", \"Production\". Shown in the Editor tool and in dev logging only - never uploaded.")]
    public string environmentName = "Development";

    [Tooltip("Marks this as a production environment - gates the HTTPS validation warning below and is surfaced in the Editor tool so a developer can tell at a glance which asset they're looking at.")]
    public bool isProductionEnvironment;

    [Tooltip("Only the Editor-only local-dev config should ever enable this. When false (the default - required for Quest/Staging/Production configs), apiBaseUrl containing \"localhost\" or \"127.0.0.1\" is a hard validation failure both at author-time (OnValidate) and at runtime (IsValidForRuntimeUse) - a Quest build can never silently point at a desktop dev machine that will not exist on the headset's network.")]
    public bool allowLocalhost;

    /// <summary>Runtime (not just Editor-time) gate - checked by AssessmentUploadService/
    /// PairingIdentityService before ever using this config's URLs, so a misconfigured
    /// asset fails closed (no upload attempted, not "uploads to the wrong/unreachable
    /// host") rather than only warning in a place nobody reads on a real device build.</summary>
    public bool IsValidForRuntimeUse(out string reason)
    {
        if (string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            reason = "apiBaseUrl is empty.";
            return false;
        }
        if (!allowLocalhost && ContainsLocalhost(apiBaseUrl))
        {
            reason = "apiBaseUrl points at localhost/127.0.0.1 but allowLocalhost is false - refusing to use it. Assign a real HTTPS-accessible development or production URL instead.";
            return false;
        }
        if ((isProductionEnvironment || !allowLocalhost) && !apiBaseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            reason = "apiBaseUrl must use https:// for any non-purely-local-dev environment (this includes Quest development configs, not just Production) - certificate validation is never disabled to work around this.";
            return false;
        }
        reason = null;
        return true;
    }

    private static bool ContainsLocalhost(string url) =>
        url.IndexOf("localhost", StringComparison.OrdinalIgnoreCase) >= 0 || url.Contains("127.0.0.1");

    [Header("Endpoint")]
    [Tooltip("Base URL of the Node.js API, no trailing slash, e.g. \"https://vicsi-dashboard.example.edu\".")]
    public string apiBaseUrl;

    [Tooltip("Path appended to apiBaseUrl for the submission endpoint, e.g. \"/api/v1/assessment-sessions\".")]
    public string submissionEndpointPath = "/api/v1/assessment-sessions";

    [Tooltip("Path appended to apiBaseUrl for the pairing-code exchange endpoint - Unity POSTs the code the student typed here and receives a short-lived assessment-session token back. NOT a login endpoint: Unity never sends or holds a password, PIN, or student-supplied identity - see PairingIdentityService's own class comment.")]
    public string pairingCodeExchangeEndpointPath = "/api/v1/device-pairings/exchange";

    [Tooltip("Path appended to apiBaseUrl for the VR student directory (vicsi-dashboard migration 018) - the roster the headset caches for offline student-code identification. See StudentDirectoryService.")]
    public string studentDirectoryEndpointPath = "/api/v1/vr/student-directory";

    [Tooltip("Seconds the startup student-directory sync may take before it is abandoned and the cached directory is used as-is. Kept short: startup never waits on it, but a hung request shouldn't keep the status at Syncing for long.")]
    [Min(1f)]
    public float studentDirectorySyncTimeoutSeconds = 8f;

    public string SubmissionUrl => CombineUrl(apiBaseUrl, submissionEndpointPath);
    public string PairingCodeExchangeUrl => CombineUrl(apiBaseUrl, pairingCodeExchangeEndpointPath);
    public string StudentDirectoryUrl => CombineUrl(apiBaseUrl, studentDirectoryEndpointPath);

    [Tooltip("Path for online student-code validation (device-authenticated). Used by the identification screen when the server is reachable; the cached directory is used otherwise.")]
    public string vrValidateCodeEndpointPath = "/api/v1/vr/validate-code";

    [Tooltip("Path for device-authenticated, short-lived, single-session upload authorization (vicsi-dashboard migration 019). Requested right before uploading a code-identified session; the token is used once and never stored.")]
    public string vrSessionUploadTokenEndpointPath = "/api/v1/vr/session-upload-token";

    [Tooltip("Seconds the identification screen waits for online code validation before falling back to the cached directory.")]
    [Min(1f)]
    public float vrCodeValidationTimeoutSeconds = 5f;

    public string VrValidateCodeUrl => CombineUrl(apiBaseUrl, vrValidateCodeEndpointPath);
    public string VrSessionUploadTokenUrl => CombineUrl(apiBaseUrl, vrSessionUploadTokenEndpointPath);

    private static string CombineUrl(string baseUrl, string path)
    {
        if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }
        return baseUrl.TrimEnd('/') + "/" + path.TrimStart('/');
    }

    [Header("Timing")]
    [Tooltip("Seconds to wait for a single upload attempt before treating it as a timeout failure.")]
    [Min(1f)]
    public float requestTimeoutSeconds = 20f;

    [Tooltip("Maximum number of upload attempts for one queued session before it is marked FailedPermanently (retryable failures only - a validation rejection fails permanently after just one attempt regardless of this value).")]
    [Min(1)]
    public int maxRetryAttempts = 8;

    [Tooltip("Delay before the FIRST retry, in seconds. Doubles on each subsequent retry (exponential backoff) up to maxRetryDelaySeconds.")]
    [Min(0.1f)]
    public float initialRetryDelaySeconds = 5f;

    [Tooltip("Upper bound on the exponential backoff delay between retries, in seconds, regardless of how many attempts have already failed.")]
    [Min(1f)]
    public float maxRetryDelaySeconds = 300f;

    [Header("Behavior")]
    [Tooltip("If true, a completed session is queued for upload automatically. If false, only a manual retry (Editor tool or the completion-screen retry button) ever attempts one.")]
    public bool automaticUploadEnabled = true;

    [Tooltip("If true, the full ordered raw event log is embedded in the payload (AssessmentSessionUploadDto.orderedEvents). If false, only the derived summaries (evidenceResults, relationshipResults, ...) are sent - smaller payload, no per-event audit trail server-side.")]
    public bool uploadRawEventLogEnabled = true;

    [Tooltip("Hard cap on how many raw events one upload request may embed, independent of uploadRawEventLogEnabled - protects against HTTP 413 on an unusually long session. Excess events are dropped from the TAIL (kept: earliest events) and includesRawEventLog is still true; the local JSONL file itself is never truncated, only what's embedded in this one request.")]
    [Min(1)]
    public int maxEventsPerRequest = 5000;

    [Header("Diagnostics")]
    [Tooltip("Verbose upload logging via Debug.Log - request lifecycle, retry scheduling, queue transitions. Never logs secret values or a full payload; see AssessmentUploadService's own class comment.")]
    public bool debugLoggingEnabled;

#if UNITY_EDITOR
    /// <summary>Author-time validation - same warn-don't-block idiom as every other *Set/*Configuration.OnValidate in this project.</summary>
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            Debug.LogWarning($"[{name}] apiBaseUrl is empty - uploads will always fail closed until this is set.", this);
        }
        else if (!IsValidForRuntimeUse(out string invalidReason))
        {
            Debug.LogWarning($"[{name}] {invalidReason}", this);
        }

        if (requestTimeoutSeconds <= 0f)
        {
            Debug.LogWarning($"[{name}] requestTimeoutSeconds must be positive.", this);
        }

        if (maxRetryAttempts <= 0)
        {
            Debug.LogWarning($"[{name}] maxRetryAttempts must be at least 1.", this);
        }

        if (initialRetryDelaySeconds <= 0f || maxRetryDelaySeconds <= 0f)
        {
            Debug.LogWarning($"[{name}] Retry delays must be positive.", this);
        }
        else if (maxRetryDelaySeconds < initialRetryDelaySeconds)
        {
            Debug.LogWarning($"[{name}] maxRetryDelaySeconds is smaller than initialRetryDelaySeconds - every retry will be clamped to the same (too-short) delay.", this);
        }

        // Duplicate-environment-name detection across every AssessmentUploadConfiguration
        // asset in the project - two assets both named "Production" is exactly the kind
        // of mistake that would otherwise only surface when someone ships the wrong one.
        var guids = UnityEditor.AssetDatabase.FindAssets("t:AssessmentUploadConfiguration");
        foreach (var guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var other = UnityEditor.AssetDatabase.LoadAssetAtPath<AssessmentUploadConfiguration>(path);
            if (other != null && other != this && !string.IsNullOrEmpty(environmentName)
                && string.Equals(other.environmentName, environmentName, StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning($"[{name}] environmentName \"{environmentName}\" duplicates {path} - the Editor tool and any environment-name-keyed logic can't tell these apart.", this);
                break;
            }
        }
    }
#endif
}
