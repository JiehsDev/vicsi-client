// Assets/_Project/Scripts/Assessment/StudentDirectory/StudentDirectoryTransport.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>Outcome of one directory GET at the transport level. Body is only kept for 2xx.</summary>
public class StudentDirectoryFetchResult
{
    public StudentDirectorySyncStatus FailureStatus = StudentDirectorySyncStatus.Succeeded;
    public int HttpStatusCode;
    public string Body;
    public bool Succeeded => FailureStatus == StudentDirectorySyncStatus.Succeeded;

    public static StudentDirectoryFetchResult Ok(int status, string body) => new() { HttpStatusCode = status, Body = body };
    public static StudentDirectoryFetchResult Fail(StudentDirectorySyncStatus status, int httpStatus = 0) => new() { FailureStatus = status, HttpStatusCode = httpStatus };
}

/// <summary>Network seam for the directory sync - same "interface is the seam" pattern as IAssessmentUploadTransport, so tests drive every failure mode without a server.</summary>
public interface IStudentDirectoryTransport
{
    Task<StudentDirectoryFetchResult> FetchAsync(string url, string deviceKey, float timeoutSeconds, CancellationToken cancellationToken);
}

/// <summary>UnityWebRequest implementation. Never logs the device key or the response body.</summary>
public class UnityWebRequestStudentDirectoryTransport : IStudentDirectoryTransport
{
    public async Task<StudentDirectoryFetchResult> FetchAsync(string url, string deviceKey, float timeoutSeconds, CancellationToken cancellationToken)
    {
        using var request = UnityWebRequest.Get(url);
        request.SetRequestHeader("Authorization", "Bearer " + deviceKey);
        request.SetRequestHeader("Accept", "application/json");
        request.timeout = Mathf.Max(1, Mathf.CeilToInt(timeoutSeconds));

        var operation = request.SendWebRequest();
        try
        {
            while (!operation.isDone)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    request.Abort();
                    return StudentDirectoryFetchResult.Fail(StudentDirectorySyncStatus.TimedOut);
                }
                await Task.Yield();
            }
        }
        catch (Exception)
        {
            return StudentDirectoryFetchResult.Fail(StudentDirectorySyncStatus.NetworkUnavailable);
        }

        int status = (int)request.responseCode;
        if (request.result == UnityWebRequest.Result.ConnectionError)
        {
            bool timedOut = !string.IsNullOrEmpty(request.error) && request.error.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0;
            return StudentDirectoryFetchResult.Fail(timedOut ? StudentDirectorySyncStatus.TimedOut : StudentDirectorySyncStatus.NetworkUnavailable);
        }
        if (status == 401 || status == 403)
        {
            return StudentDirectoryFetchResult.Fail(StudentDirectorySyncStatus.Unauthorized, status);
        }
        if (status >= 200 && status < 300 && request.result == UnityWebRequest.Result.Success)
        {
            return StudentDirectoryFetchResult.Ok(status, request.downloadHandler?.text);
        }
        if (status == 0)
        {
            return StudentDirectoryFetchResult.Fail(StudentDirectorySyncStatus.NetworkUnavailable);
        }
        return StudentDirectoryFetchResult.Fail(status >= 500 || status == 429 ? StudentDirectorySyncStatus.ServerError : StudentDirectorySyncStatus.InvalidResponse, status);
    }
}
