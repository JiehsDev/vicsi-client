// Assets/_Project/Scripts/Assessment/StudentDirectory/VrServerClients.cs
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

// ===========================================================================
// Session-upload authorization - POST /api/v1/vr/session-upload-token
// ===========================================================================

[Serializable]
public class VrUploadTokenRequestDto
{
    public string studentId;
    public string sessionId;
    public string scenarioId;
    public string classIdHint;
    public bool isTestSession;
    public string sessionStartedAtUtc;
}

[Serializable]
public class VrUploadTokenResponseDto
{
    public bool success;
    public VrUploadTokenDataDto data;
}

[Serializable]
public class VrUploadTokenDataDto
{
    public string uploadToken;
    public string expiresAtUtc;
    public string sessionId;
    public string studentId;
    public string classId;
    public string assignmentId;
    public string scenarioId;
    public bool isTestSession;
}

public enum VrUploadAuthorizationFailure
{
    None,
    NotConfigured,       // no server URL or no device key on this headset
    NoNetwork,
    Timeout,
    DeviceUnauthorized,  // the server refused the device key
    Refused,             // the server permanently refused THIS session (owner mismatch, unknown/disabled student...)
    RetryLater,          // server error, rate limit, or a fixable state (no assignment yet)
    InvalidResponse,
}

public class VrUploadAuthorizationResult
{
    public VrUploadAuthorizationFailure Failure;
    public string UploadToken;      // memory only - never persisted, never logged
    public DateTime ExpiresAtUtc;
    public string ServerErrorCode;
    public int HttpStatusCode;
    public bool Success => Failure == VrUploadAuthorizationFailure.None;
}

/// <summary>Seam for tests; the real one is UnityWebRequestVrUploadAuthorizationClient.</summary>
public interface IVrUploadAuthorizationClient
{
    Task<VrUploadAuthorizationResult> RequestAsync(string url, string deviceKey, VrUploadTokenRequestDto request, float timeoutSeconds, CancellationToken cancellationToken);
}

public class UnityWebRequestVrUploadAuthorizationClient : IVrUploadAuthorizationClient
{
    // Server refusals that no retry can fix for this session.
    private static readonly HashSet<string> PermanentCodes = new()
    {
        "STUDENT_NOT_FOUND", "STUDENT_NOT_PERMITTED", "STUDENT_DISABLED", "SESSION_OWNER_MISMATCH", "VALIDATION_ERROR",
    };

    public async Task<VrUploadAuthorizationResult> RequestAsync(string url, string deviceKey, VrUploadTokenRequestDto request, float timeoutSeconds, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(deviceKey))
        {
            return new VrUploadAuthorizationResult { Failure = VrUploadAuthorizationFailure.NotConfigured };
        }

        var (status, body, transportFailure) = await VrHttp.PostJsonAsync(url, deviceKey, JsonUtility.ToJson(request), timeoutSeconds, cancellationToken);
        if (transportFailure != VrUploadAuthorizationFailure.None)
        {
            return new VrUploadAuthorizationResult { Failure = transportFailure, HttpStatusCode = status };
        }

        if (status >= 200 && status < 300)
        {
            var parsed = VrHttp.TryParse<VrUploadTokenResponseDto>(body);
            if (parsed == null || !parsed.success || parsed.data == null || string.IsNullOrEmpty(parsed.data.uploadToken)
                || !DateTime.TryParse(parsed.data.expiresAtUtc, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var expires))
            {
                return new VrUploadAuthorizationResult { Failure = VrUploadAuthorizationFailure.InvalidResponse, HttpStatusCode = status };
            }
            return new VrUploadAuthorizationResult { UploadToken = parsed.data.uploadToken, ExpiresAtUtc = expires, HttpStatusCode = status };
        }

        string code = VrHttp.TryParse<ApiErrorResponseDto>(body)?.error?.code;
        VrUploadAuthorizationFailure failure;
        if (status == 401 && (code == null || code == "UNAUTHORIZED"))
        {
            failure = VrUploadAuthorizationFailure.DeviceUnauthorized;
        }
        else if (code != null && PermanentCodes.Contains(code))
        {
            failure = VrUploadAuthorizationFailure.Refused;
        }
        else
        {
            failure = VrUploadAuthorizationFailure.RetryLater;   // 5xx, 429, NO_ASSIGNMENT, anything unexpected
        }
        return new VrUploadAuthorizationResult { Failure = failure, ServerErrorCode = code, HttpStatusCode = status };
    }
}

// ===========================================================================
// Online code validation - POST /api/v1/vr/validate-code
// ===========================================================================

[Serializable]
public class VrValidateCodeRequestDto
{
    public string accessCode;
    public string deviceInstallationId;
}

[Serializable]
public class VrValidateCodeResponseDto
{
    public bool success;
    public VrValidatedStudentDto data;
}

[Serializable]
public class VrValidatedStudentDto
{
    public string studentId;
    public string displayName;
    public int codeVersion;
    public List<StudentDirectoryClassDto> classes;
}

public enum VrCodeValidationOutcome
{
    Valid,
    Invalid,       // the server says this code is not valid (unknown or disabled - it doesn't say which)
    Unreachable,   // offline, timed out, not configured, or a server problem -> use the cached directory
}

public class VrCodeValidationResult
{
    public VrCodeValidationOutcome Outcome;
    public VrValidatedStudentDto Student;
}

public interface IVrCodeValidator
{
    Task<VrCodeValidationResult> ValidateAsync(string canonicalCode, float timeoutSeconds, CancellationToken cancellationToken);
}

public class UnityWebRequestVrCodeValidator : IVrCodeValidator
{
    private readonly Func<AssessmentUploadConfiguration> configuration;
    private readonly Func<string> deviceKey;

    public UnityWebRequestVrCodeValidator(Func<AssessmentUploadConfiguration> configuration, Func<string> deviceKey)
    {
        this.configuration = configuration;
        this.deviceKey = deviceKey;
    }

    public async Task<VrCodeValidationResult> ValidateAsync(string canonicalCode, float timeoutSeconds, CancellationToken cancellationToken)
    {
        var config = configuration();
        string key = deviceKey();
        if (config == null || !config.IsValidForRuntimeUse(out _) || string.IsNullOrEmpty(key))
        {
            return new VrCodeValidationResult { Outcome = VrCodeValidationOutcome.Unreachable };
        }

        var request = new VrValidateCodeRequestDto { accessCode = canonicalCode, deviceInstallationId = DeviceInstallationId() };
        var (status, body, failure) = await VrHttp.PostJsonAsync(config.VrValidateCodeUrl, key, JsonUtility.ToJson(request), timeoutSeconds, cancellationToken);
        if (failure != VrUploadAuthorizationFailure.None)
        {
            return new VrCodeValidationResult { Outcome = VrCodeValidationOutcome.Unreachable };
        }
        if (status >= 200 && status < 300)
        {
            var parsed = VrHttp.TryParse<VrValidateCodeResponseDto>(body);
            if (parsed?.data != null && parsed.success && Guid.TryParse(parsed.data.studentId, out _) && !string.IsNullOrWhiteSpace(parsed.data.displayName))
            {
                return new VrCodeValidationResult { Outcome = VrCodeValidationOutcome.Valid, Student = parsed.data };
            }
            return new VrCodeValidationResult { Outcome = VrCodeValidationOutcome.Unreachable };
        }
        string code = VrHttp.TryParse<ApiErrorResponseDto>(body)?.error?.code;
        return new VrCodeValidationResult { Outcome = code == "VR_CODE_INVALID" ? VrCodeValidationOutcome.Invalid : VrCodeValidationOutcome.Unreachable };
    }

    // Same pseudonymous per-install id PairingIdentityService already sends for rate limiting.
    private static string DeviceInstallationId()
    {
        const string key = "vicsi_device_installation_id";
        string id = PlayerPrefs.GetString(key, null);
        if (string.IsNullOrEmpty(id))
        {
            id = Guid.NewGuid().ToString();
            PlayerPrefs.SetString(key, id);
            PlayerPrefs.Save();
        }
        return id;
    }
}

// ===========================================================================
// Shared HTTP helper for the device-authenticated VR endpoints
// ===========================================================================

internal static class VrHttp
{
    /// <summary>POST JSON with the device key as bearer. Never logs the key, the body or the response.</summary>
    public static async Task<(int status, string body, VrUploadAuthorizationFailure failure)> PostJsonAsync(
        string url, string deviceKey, string json, float timeoutSeconds, CancellationToken cancellationToken)
    {
        using var request = new UnityWebRequest(url, "POST");
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", "Bearer " + deviceKey);
        request.timeout = Mathf.Max(1, Mathf.CeilToInt(timeoutSeconds));

        var operation = request.SendWebRequest();
        try
        {
            while (!operation.isDone)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    request.Abort();
                    return (0, null, VrUploadAuthorizationFailure.Timeout);
                }
                await Task.Yield();
            }
        }
        catch (Exception)
        {
            return (0, null, VrUploadAuthorizationFailure.NoNetwork);
        }

        if (request.result == UnityWebRequest.Result.ConnectionError)
        {
            bool timedOut = !string.IsNullOrEmpty(request.error) && request.error.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0;
            return (0, null, timedOut ? VrUploadAuthorizationFailure.Timeout : VrUploadAuthorizationFailure.NoNetwork);
        }
        return ((int)request.responseCode, request.downloadHandler?.text, VrUploadAuthorizationFailure.None);
    }

    public static T TryParse<T>(string json) where T : class
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            return JsonUtility.FromJson<T>(json);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
