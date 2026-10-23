// Assets/_Project/Scripts/Assessment/MockAssessmentUploadTransport.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Scriptable stand-in for IAssessmentUploadTransport - lets AssessmentUploadService be
/// exercised end-to-end (queue -> attempt -> retry/backoff -> terminal state) against
/// every required failure mode on demand, entirely offline, before a real Node.js
/// endpoint exists. See Implementation order step 4 ("Unity upload service using a mock
/// server") and the required-tests list (successful upload, duplicate, timeout, no
/// network, 400/401/409/413/429/5xx, invalid JSON, ...).
///
/// Scripted by session id: queue a sequence of canned results for a given sessionId via
/// EnqueueResult, and each call to UploadAsync for that session pops the next one (the
/// last one repeats once exhausted, so a test doesn't have to pre-compute exactly how
/// many attempts a retry sequence will take). A sessionId with nothing scripted
/// defaults to immediate success, so tests that don't care about failure behavior don't
/// need to script anything.
/// </summary>
public class MockAssessmentUploadTransport : IAssessmentUploadTransport
{
    private readonly Dictionary<string, Queue<AssessmentUploadAttemptResult>> scriptedResults = new();
    private readonly Dictionary<string, AssessmentUploadAttemptResult> lastResult = new();

    public List<(string sessionId, string url, string payloadJson)> Calls { get; } = new();

    public void EnqueueResult(string sessionId, AssessmentUploadAttemptResult result)
    {
        if (!scriptedResults.TryGetValue(sessionId, out var queue))
        {
            queue = new Queue<AssessmentUploadAttemptResult>();
            scriptedResults[sessionId] = queue;
        }
        queue.Enqueue(result);
    }

    public void Reset()
    {
        scriptedResults.Clear();
        lastResult.Clear();
        Calls.Clear();
    }

    public Task<AssessmentUploadAttemptResult> UploadAsync(string submissionUrl, string payloadJson, string bearerToken, float timeoutSeconds, CancellationToken cancellationToken)
    {
        string sessionId = ExtractSessionId(payloadJson);
        Calls.Add((sessionId, submissionUrl, payloadJson));

        AssessmentUploadAttemptResult result;
        if (scriptedResults.TryGetValue(sessionId, out var queue) && queue.Count > 0)
        {
            result = queue.Count > 1 ? queue.Dequeue() : queue.Peek();
        }
        else if (lastResult.TryGetValue(sessionId, out var previous))
        {
            result = previous;
        }
        else
        {
            result = AssessmentUploadAttemptResult.Ok(new AssessmentSubmissionResponseDto
            {
                success = true,
                data = new AssessmentSubmissionDataDto
                {
                    sessionId = sessionId,
                    receiptId = "MOCK-" + Guid.NewGuid().ToString("N")[..12],
                    serverReceivedAtUtc = DateTime.UtcNow.ToString("O"),
                    uploadStatus = "accepted",
                    duplicate = false,
                    verificationStatus = "pending_verification",
                    payloadVersion = 1
                }
            }, 200);
        }

        lastResult[sessionId] = result;
        return Task.FromResult(result);
    }

    // Cheap, dependency-free sessionId extraction from the payload JSON (used only for
    // keying scripted results, never trusted for anything security-relevant) - avoids
    // requiring a full JsonUtility.FromJson round-trip just to route a mock response.
    private static string ExtractSessionId(string payloadJson)
    {
        const string marker = "\"sessionId\":\"";
        int start = payloadJson.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0) return string.Empty;
        start += marker.Length;
        int end = payloadJson.IndexOf('"', start);
        return end < 0 ? string.Empty : payloadJson[start..end];
    }
}
