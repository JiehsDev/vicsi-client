// Assets/_Project/Scripts/Assessment/AssessmentUploadResponseDto.cs
using System;

/// <summary>
/// Real shape of every vicsi-dashboard v1 assessment API response, matching the
/// Route Handlers under vicsi-dashboard/src/app/api/v1/**/route.ts and the shared
/// envelope helper vicsi-dashboard/src/lib/apiResponse.ts. Every response is either
/// {"success":true,"data":{...}} or {"success":false,"error":{...}} - never a bare
/// object - so every endpoint gets its own *ResponseDto wrapping the matching *DataDto,
/// plus the one shared ApiErrorResponseDto for the failure case. JsonUtility parses
/// happily into these nested types as long as the caller checks `success` first and
/// only trusts `data` (or `error`) after that, the same discipline
/// UnityWebRequestUploadTransport/AssessmentUploadService and PairingIdentityService
/// both already follow.
/// </summary>

[Serializable]
public class AssessmentSubmissionDataDto
{
    public string receiptId;
    public string sessionId;
    public string uploadStatus;         // "accepted"
    public bool duplicate;
    public string verificationStatus;   // "pending_verification" | "verified" | "verification_failed" | "requires_review"
    public string serverReceivedAtUtc;
    public int payloadVersion;
}

/// <summary>Successful POST /api/v1/assessment-sessions response envelope.</summary>
[Serializable]
public class AssessmentSubmissionResponseDto
{
    public bool success;
    public AssessmentSubmissionDataDto data;
}

[Serializable]
public class ApiFieldErrorDto
{
    public string field;
    public string message;
}

[Serializable]
public class ApiErrorDto
{
    public string code;     // e.g. "PAIRING_CODE_EXPIRED", "SESSION_PAYLOAD_CONFLICT", "TOKEN_EXPIRED" - see apiResponse.ts's ApiErrorCode union for the full set
    public string message;  // short, human-readable, safe to show on the completion/pairing screen
    public bool retryable;
    public ApiFieldErrorDto[] fieldErrors; // optional, may be null/empty
}

/// <summary>Shape of every non-2xx response across all v1 endpoints - {"success":false,"error":{...}}.</summary>
[Serializable]
public class ApiErrorResponseDto
{
    public bool success;
    public ApiErrorDto error;
}

[Serializable]
public class PairingCodeExchangeDataDto
{
    public string assessmentToken;
    public string tokenExpiresAtUtc;
    public string assessmentAssignmentId;
    public string studentDisplayName;   // safe to show, e.g. full name as stored in profiles
    public string classDisplayName;     // e.g. "CRIM 301 - A", safe to show
    public string scenarioId;
    public string scenarioVersion;
    public string scoringRulesVersion;
}

/// <summary>Successful POST /api/v1/device-pairings/exchange response envelope - see PairingIdentityService's own class comment for the full flow this belongs to.</summary>
[Serializable]
public class PairingCodeExchangeResponseDto
{
    public bool success;
    public PairingCodeExchangeDataDto data;
}
