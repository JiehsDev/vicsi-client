// Assets/_Project/Scripts/Assessment/PairingIdentityService.cs
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Holds the student's current assessment-session identity and performs the pairing-
/// code exchange - the ONLY identity mechanism this feature uses. Unity never asks for
/// (or handles) a student's email, web password, or Student ID/PIN directly:
///
///   1. The student signs into vicsi-dashboard on the web (already real, existing auth).
///   2. They pick their class/assigned assessment there and request a short-lived,
///      single-use pairing code.
///   3. They type that code into this headset's identity screen (see
///      PairingCodeIdentityPanel).
///   4. Unity POSTs the code alone to AssessmentUploadConfiguration.PairingCodeExchangeUrl.
///   5. The server resolves studentId/classId/scenario assignment from the pairing
///      record it already owns and returns a short-lived assessment-session token plus
///      safe-to-show display strings - never the raw studentId/classId themselves.
///
/// SECURITY BOUNDARY: this class stores ONLY the opaque token and display strings, held
/// IN MEMORY for the lifetime of this play session - never written to PlayerPrefs or
/// disk. A shared headset that's restarted or handed to the next student starts
/// unpaired again; re-pairing is the intended behavior, not a bug to fix later (see
/// BUSINESS_RULES.md §2.4's own "shared-headset" concerns for why a cached identity on
/// shared hardware is a correctness risk, not just an inconvenience). The token itself
/// is never logged (see Debug.Log call sites below - only its EXPIRY and presence are
/// logged, never the value).
/// </summary>
public class PairingIdentityService : MonoBehaviour
{
    public static PairingIdentityService Instance { get; private set; }

    [SerializeField] private AssessmentUploadConfiguration configuration;
    [SerializeField] private ScenarioDefinition scenario;

    private const string DeviceInstallationIdPrefKey = "vicsi_device_installation_id";
    private const int PayloadVersion = 1;

    public bool IsPaired { get; private set; }
    public string AssessmentSessionToken { get; private set; }
    public DateTime TokenExpiresAtUtc { get; private set; }
    public string AssignedAssessmentAssignmentId { get; private set; }
    public string StudentDisplayName { get; private set; }
    public string ClassDisplayName { get; private set; }
    public string AssignedScenarioId { get; private set; }
    public string AssignedScenarioVersion { get; private set; }
    public string AssignedScoringRulesVersion { get; private set; }

    public static event Action OnPaired;
    public static event Action OnUnpaired;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public bool TokenIsValidNow => IsPaired && DateTime.UtcNow < TokenExpiresAtUtc;

    /// <summary>Result of one pairing-code exchange attempt - a small, purpose-specific result type rather than reusing AssessmentUploadAttemptResult, since the two calls share nothing but "network request, structured error."</summary>
    public class PairingExchangeResult
    {
        public bool Success;
        public string ErrorMessage;      // safe, human-readable - shown directly on PairingCodeIdentityPanel
        public bool IsRetryable;          // false for an expired/invalid/already-used code; true for network/server trouble
        public string ErrorCode;          // e.g. "PAIRING_CODE_EXPIRED" - lets the panel pick its own Inspector-configured copy for specific cases (expired vs. generic) instead of always showing ErrorMessage verbatim
    }

    /// <summary>Clears the current identity - called when a session ends, or explicitly by a "switch student" action on the identity screen.</summary>
    public void ClearIdentity()
    {
        bool wasPaired = IsPaired;
        IsPaired = false;
        AssessmentSessionToken = null;
        TokenExpiresAtUtc = default;
        AssignedAssessmentAssignmentId = null;
        StudentDisplayName = null;
        ClassDisplayName = null;
        AssignedScenarioId = null;
        AssignedScenarioVersion = null;
        AssignedScoringRulesVersion = null;

        if (wasPaired)
        {
            OnUnpaired?.Invoke();
        }
    }

    /// <summary>Test seam - sets the paired identity directly, bypassing the real network exchange, and fires OnPaired exactly as a real successful ExchangePairingCodeAsync would. Never call this from real gameplay code.
    /// The four assignment/scenario fields are optional and only matter when assessmentToken is a REAL captured token (see PreSessionFlowController's development bypass) - a real backend upload needs a real assignment/scenario id to validate against, not just a token.</summary>
    public void SetIdentityForTesting(string studentDisplayName, string classDisplayName, string assessmentToken, DateTime tokenExpiresAtUtc,
        string assignedAssessmentAssignmentId = null, string assignedScenarioId = null, string assignedScenarioVersion = null, string assignedScoringRulesVersion = null)
    {
        StudentDisplayName = studentDisplayName;
        ClassDisplayName = classDisplayName;
        AssessmentSessionToken = assessmentToken;
        TokenExpiresAtUtc = tokenExpiresAtUtc;
        AssignedAssessmentAssignmentId = assignedAssessmentAssignmentId;
        AssignedScenarioId = assignedScenarioId;
        AssignedScenarioVersion = assignedScenarioVersion;
        AssignedScoringRulesVersion = assignedScoringRulesVersion;
        IsPaired = true;
        OnPaired?.Invoke();
    }

    public async Task<PairingExchangeResult> ExchangePairingCodeAsync(string pairingCode, CancellationToken cancellationToken)
    {
        if (configuration == null || string.IsNullOrEmpty(configuration.PairingCodeExchangeUrl))
        {
            return new PairingExchangeResult { Success = false, IsRetryable = false, ErrorMessage = "Pairing is not configured on this device." };
        }

        // Same fail-closed rule as AssessmentUploadService: a misconfigured asset (e.g.
        // a Quest/Production config still pointing at localhost) must refuse outright,
        // not silently attempt a request against an unreachable/wrong host.
        if (!configuration.IsValidForRuntimeUse(out string invalidReason))
        {
            Debug.LogError($"[PairingIdentityService] Refusing pairing exchange: {invalidReason}");
            return new PairingExchangeResult { Success = false, IsRetryable = false, ErrorMessage = "Device upload configuration is invalid. Contact your instructor." };
        }

        if (string.IsNullOrWhiteSpace(pairingCode))
        {
            return new PairingExchangeResult { Success = false, IsRetryable = false, ErrorMessage = "Enter the pairing code shown on the dashboard." };
        }

        string requestJson = JsonUtility.ToJson(new PairingCodeExchangeRequestDto
        {
            payloadVersion = PayloadVersion,
            pairingCode = pairingCode.Trim(),
            scenarioId = scenario != null ? scenario.scenarioId : null,
            clientVersion = Application.version,
            deviceInstallationId = GetOrCreateDeviceInstallationId()
        });

        using var request = new UnityWebRequest(configuration.PairingCodeExchangeUrl, "POST");
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(requestJson));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.timeout = Mathf.Max(1, Mathf.CeilToInt(configuration.requestTimeoutSeconds));

        var operation = request.SendWebRequest();
        while (!operation.isDone)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                request.Abort();
                return new PairingExchangeResult { Success = false, IsRetryable = true, ErrorMessage = "Cancelled." };
            }
            await Task.Yield();
        }

        if (request.result == UnityWebRequest.Result.ConnectionError)
        {
            LogDebug("Pairing exchange failed: no network/connection.");
            return new PairingExchangeResult { Success = false, IsRetryable = true, ErrorMessage = "Could not reach the server. Check your connection and try again." };
        }

        long status = request.responseCode;
        string body = request.downloadHandler?.text;

        if (status == 200)
        {
            PairingCodeExchangeResponseDto response = null;
            try { response = JsonUtility.FromJson<PairingCodeExchangeResponseDto>(body); }
            catch (ArgumentException) { }

            if (response == null || !response.success || response.data == null || string.IsNullOrEmpty(response.data.assessmentToken))
            {
                LogDebug("Pairing exchange returned 200 but the response body did not parse as a successful envelope.");
                return new PairingExchangeResult { Success = false, IsRetryable = true, ErrorMessage = "The server response was not understood. Try again." };
            }

            var data = response.data;
            AssessmentSessionToken = data.assessmentToken;
            TokenExpiresAtUtc = DateTime.TryParse(data.tokenExpiresAtUtc, out var parsed)
                ? parsed.ToUniversalTime()
                : DateTime.UtcNow.AddHours(4); // conservative fallback if the server omits it - never treat a missing expiry as "forever"
            AssignedAssessmentAssignmentId = data.assessmentAssignmentId;
            StudentDisplayName = data.studentDisplayName;
            ClassDisplayName = data.classDisplayName;
            AssignedScenarioId = data.scenarioId;
            AssignedScenarioVersion = data.scenarioVersion;
            AssignedScoringRulesVersion = data.scoringRulesVersion;
            IsPaired = true;

            LogDebug($"Pairing succeeded. Token expires {TokenExpiresAtUtc:O} (value not logged).");
            OnPaired?.Invoke();
            return new PairingExchangeResult { Success = true };
        }

        ApiErrorResponseDto errorEnvelope = null;
        try { errorEnvelope = JsonUtility.FromJson<ApiErrorResponseDto>(body); }
        catch (ArgumentException) { }
        var error = errorEnvelope?.error;

        // Prefers the documented error code over the raw HTTP status - see
        // apiResponse.ts's ApiErrorCode union for the full set this switches on
        // (PAIRING_CODE_INVALID, PAIRING_CODE_EXPIRED, PAIRING_CODE_USED,
        // SCENARIO_MISMATCH, ASSIGNMENT_INACTIVE, RATE_LIMITED, SERVER_ERROR, ...).
        string message = !string.IsNullOrEmpty(error?.message)
            ? error.message
            : error?.code switch
            {
                "PAIRING_CODE_INVALID" => "That code isn't recognized. Check it and try again.",
                "PAIRING_CODE_USED" => "That code has already been used. Request a new one from the dashboard.",
                "PAIRING_CODE_EXPIRED" => "That code has expired. Request a new one from the dashboard.",
                "SCENARIO_MISMATCH" => "That code was issued for a different scenario.",
                "ASSIGNMENT_INACTIVE" => "This assignment is no longer active.",
                "RATE_LIMITED" => "Too many attempts. Wait a moment and try again.",
                _ => "Something went wrong. Try again."
            };

        bool retryable = error != null ? error.retryable : status is 429 or >= 500;
        LogDebug($"Pairing exchange failed: HTTP {status} ({error?.code ?? "no code"}).");
        return new PairingExchangeResult { Success = false, IsRetryable = retryable, ErrorMessage = message, ErrorCode = error?.code };
    }

    /// <summary>
    /// A random, locally-generated, PERSISTENT identifier for this app installation -
    /// deliberately NOT SystemInfo.deviceUniqueIdentifier or any other hardware-derived
    /// value, per this feature's explicit requirement that deviceInstallationId never
    /// expose a headset's serial number, MAC address, or other hardware ID. Stored in
    /// PlayerPrefs (not a server-visible credential - only used server-side for rate
    /// limiting a single device's pairing attempts) and created once, on first use.
    /// </summary>
    private static string GetOrCreateDeviceInstallationId()
    {
        string existing = PlayerPrefs.GetString(DeviceInstallationIdPrefKey, string.Empty);
        if (!string.IsNullOrEmpty(existing))
        {
            return existing;
        }

        string generated = Guid.NewGuid().ToString("N");
        PlayerPrefs.SetString(DeviceInstallationIdPrefKey, generated);
        PlayerPrefs.Save();
        return generated;
    }

    private void LogDebug(string message)
    {
        if (configuration != null && configuration.debugLoggingEnabled)
        {
            Debug.Log($"[PairingIdentityService] {message}");
        }
    }

    [Serializable]
    private class PairingCodeExchangeRequestDto
    {
        public int payloadVersion;
        public string pairingCode;
        public string scenarioId;
        public string clientVersion;
        public string deviceInstallationId; // optional, pseudonymous - see GetOrCreateDeviceInstallationId()
    }
}
