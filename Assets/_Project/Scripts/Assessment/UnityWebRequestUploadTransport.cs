// Assets/_Project/Scripts/Assessment/UnityWebRequestUploadTransport.cs
using System;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Real network implementation of IAssessmentUploadTransport, using UnityWebRequest
/// (com.unity.modules.unitywebrequest - already a project dependency, no new package
/// needed). Interprets every failure mode this feature is required to handle: no
/// network/DNS/connection failure, timeout, 400/401/403/409/413/429/5xx, and an
/// unparseable response body - see AssessmentUploadErrorCategory's own class comment
/// for why these collapse into a small fixed set of retry-relevant buckets rather than
/// being handled as raw status codes throughout the rest of the system.
///
/// Never logs the bearer token or the full payload body - see AssessmentUploadService's
/// own class comment on the same rule.
/// </summary>
public class UnityWebRequestUploadTransport : IAssessmentUploadTransport
{
    public async Task<AssessmentUploadAttemptResult> UploadAsync(string submissionUrl, string payloadJson, string bearerToken, float timeoutSeconds, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(submissionUrl))
        {
            return AssessmentUploadAttemptResult.Fail(AssessmentUploadErrorCategory.Unknown, 0, "Submission URL is empty.");
        }

        using var request = new UnityWebRequest(submissionUrl, "POST");
        byte[] bodyBytes = Encoding.UTF8.GetBytes(payloadJson);
        request.uploadHandler = new UploadHandlerRaw(bodyBytes);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        if (!string.IsNullOrEmpty(bearerToken))
        {
            request.SetRequestHeader("Authorization", "Bearer " + bearerToken);
        }
        request.timeout = Mathf.Max(1, Mathf.CeilToInt(timeoutSeconds));

        var operation = request.SendWebRequest();

        try
        {
            while (!operation.isDone)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    request.Abort();
                    return AssessmentUploadAttemptResult.Fail(AssessmentUploadErrorCategory.Unknown, 0, "Upload cancelled (application closing or session stopped).");
                }
                await Task.Yield();
            }
        }
        catch (Exception ex)
        {
            return AssessmentUploadAttemptResult.Fail(AssessmentUploadErrorCategory.Unknown, 0, $"Unexpected exception awaiting request: {ex.GetType().Name}: {ex.Message}");
        }

        return Interpret(request);
    }

    private static AssessmentUploadAttemptResult Interpret(UnityWebRequest request)
    {
        long statusCode = request.responseCode;

        if (request.result == UnityWebRequest.Result.ConnectionError)
        {
            return AssessmentUploadAttemptResult.Fail(AssessmentUploadErrorCategory.NoNetwork, 0, request.error);
        }

        if (request.result == UnityWebRequest.Result.DataProcessingError)
        {
            return AssessmentUploadAttemptResult.Fail(AssessmentUploadErrorCategory.InvalidServerResponse, (int)statusCode, request.error);
        }

        // UnityWebRequest surfaces a client-side timeout as ConnectionError above in most
        // versions, but request.error containing "timed out" (rather than "connect" or
        // "resolve") lets us report the more specific category the retry backoff wants
        // to log distinctly, without changing retry behavior (both are retryable).
        if (request.result == UnityWebRequest.Result.ConnectionError
            && !string.IsNullOrEmpty(request.error)
            && request.error.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return AssessmentUploadAttemptResult.Fail(AssessmentUploadErrorCategory.Timeout, 0, request.error);
        }

        string body = request.downloadHandler?.text;

        if (statusCode >= 200 && statusCode < 300)
        {
            if (statusCode == 202 || statusCode == 200)
            {
                // An identical-payload resubmission is ALSO a 200 with success:true -
                // the server signals it purely via data.duplicate, never a different
                // HTTP status. This transport never decides "duplicate == success"
                // itself; it only surfaces what the server's own envelope said. See
                // AssessmentUploadService.HandleResult for the duplicate-sessionId
                // cross-check that happens above this layer.
                var response = TryParse<AssessmentSubmissionResponseDto>(body);
                if (response == null || !response.success || response.data == null)
                {
                    return AssessmentUploadAttemptResult.Fail(AssessmentUploadErrorCategory.InvalidServerResponse, (int)statusCode, "2xx response body did not parse as a successful AssessmentSubmissionResponseDto.");
                }
                return AssessmentUploadAttemptResult.Ok(response, (int)statusCode);
            }
        }

        var serverError = TryParse<ApiErrorResponseDto>(body);

        switch (statusCode)
        {
            case 400:
                return AssessmentUploadAttemptResult.Fail(AssessmentUploadErrorCategory.ValidationRejected, (int)statusCode, request.error, serverError);
            case 401:
            case 403:
                return AssessmentUploadAttemptResult.Fail(AssessmentUploadErrorCategory.AuthenticationFailed, (int)statusCode, request.error, serverError);
            case 409:
                // SESSION_PAYLOAD_CONFLICT - the same sessionId was already submitted
                // with a DIFFERENT payload. Always a genuine, permanent error now
                // (never a disguised success) - resubmitting the same conflicting
                // payload can never resolve itself, so this is bucketed with the
                // other permanent-rejection category rather than retried forever.
                return AssessmentUploadAttemptResult.Fail(AssessmentUploadErrorCategory.ValidationRejected, (int)statusCode, request.error, serverError);
            case 413:
                return AssessmentUploadAttemptResult.Fail(AssessmentUploadErrorCategory.PayloadTooLarge, (int)statusCode, request.error, serverError);
            case 429:
            {
                float retryAfter = -1f;
                string headerValue = request.GetResponseHeader("Retry-After");
                if (!string.IsNullOrEmpty(headerValue) && float.TryParse(headerValue, out var parsed))
                {
                    retryAfter = parsed;
                }
                return AssessmentUploadAttemptResult.Fail(AssessmentUploadErrorCategory.RateLimited, (int)statusCode, request.error, serverError, retryAfter);
            }
            default:
                if (statusCode >= 500)
                {
                    return AssessmentUploadAttemptResult.Fail(AssessmentUploadErrorCategory.ServerError, (int)statusCode, request.error, serverError);
                }
                return AssessmentUploadAttemptResult.Fail(AssessmentUploadErrorCategory.Unknown, (int)statusCode, request.error, serverError);
        }
    }

    private static T TryParse<T>(string json) where T : class
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }
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
