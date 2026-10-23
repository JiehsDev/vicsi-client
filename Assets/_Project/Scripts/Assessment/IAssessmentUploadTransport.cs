// Assets/_Project/Scripts/Assessment/IAssessmentUploadTransport.cs
using System.Threading;
using System.Threading.Tasks;

/// <summary>Outcome of one transport-level attempt - independent of AssessmentUploadStatus, which tracks the QUEUE entry's lifecycle rather than one HTTP attempt.</summary>
public class AssessmentUploadAttemptResult
{
    public bool Success;
    public AssessmentUploadErrorCategory ErrorCategory = AssessmentUploadErrorCategory.None;
    public int HttpStatusCode;                      // 0 if the request never reached the server (no network/DNS/timeout)
    public AssessmentSubmissionResponseDto Response;  // set only when Success
    public ApiErrorResponseDto ServerError;          // set when the server returned a parseable structured error
    public string RawErrorMessage;                   // dev-facing only (Debug.Log) - never shown to the player verbatim
    public float RetryAfterSeconds = -1f;             // >= 0 if the server sent Retry-After (HTTP 429)

    public static AssessmentUploadAttemptResult Ok(AssessmentSubmissionResponseDto response, int statusCode) =>
        new() { Success = true, Response = response, HttpStatusCode = statusCode, ErrorCategory = AssessmentUploadErrorCategory.None };

    public static AssessmentUploadAttemptResult Fail(AssessmentUploadErrorCategory category, int statusCode, string rawMessage, ApiErrorResponseDto serverError = null, float retryAfterSeconds = -1f) =>
        new() { Success = false, ErrorCategory = category, HttpStatusCode = statusCode, RawErrorMessage = rawMessage, ServerError = serverError, RetryAfterSeconds = retryAfterSeconds };
}

/// <summary>
/// Seam between AssessmentUploadService's retry/queue state machine and the actual
/// network call - swappable so the service can be exercised end-to-end against
/// MockAssessmentUploadTransport (every required failure mode, on demand, with no
/// network and no real server) before the real Node.js endpoint exists, and against
/// UnityWebRequestUploadTransport once it does. Matches this project's established
/// "interface is the seam" pattern (ISessionLogWriter/LocalJsonLogWriter).
/// </summary>
public interface IAssessmentUploadTransport
{
    Task<AssessmentUploadAttemptResult> UploadAsync(string submissionUrl, string payloadJson, string bearerToken, float timeoutSeconds, CancellationToken cancellationToken);
}
