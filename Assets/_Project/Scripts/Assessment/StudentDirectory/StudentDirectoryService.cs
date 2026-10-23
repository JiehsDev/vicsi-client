// Assets/_Project/Scripts/Assessment/StudentDirectory/StudentDirectoryService.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Startup owner of the headset's offline student directory (StudentDirectoryCache).
/// Lives on _Managers in Main (the start menu, build index 0) and in CSI_Environment,
/// so the directory is loaded and refreshed whichever scene the app starts in:
///
///   Awake  - loads the cached directory from disk, synchronously, before anything else
///            can ask for it. Offline, this is all there is, and it is enough: every
///            previously synced code works.
///   Start  - kicks off ONE server sync in the background and returns immediately.
///            Startup never waits on it. The sync is abandoned after
///            AssessmentUploadConfiguration.studentDirectorySyncTimeoutSeconds even if
///            the request itself never returns. A failed, timed-out, refused or
///            malformed sync leaves the cached directory exactly as it was.
///
/// Reuses the upload stack's configuration asset (same apiBaseUrl, same HTTPS/
/// localhost refusal via IsValidForRuntimeUse) - no second HTTP/config stack.
///
/// DEVICE KEY: the directory endpoint requires the dashboard's VR_DEVICE_SYNC_KEY as a
/// bearer token. It is NOT stored in any asset or scene (this project keeps no standing
/// credential in source control - see AssessmentUploadService's class comment). Each
/// headset reads it from &lt;persistentDataPath&gt;/vicsi_vr_device_sync_key.txt
/// (provisioned per device, e.g. `adb push`). In the Editor only, editorDeviceKeyOverride
/// can supply one for local testing - never commit a real value there. No key = status
/// NotConfigured, cache used as-is.
///
/// Status for the future Instructor Management screen: Cache.SyncMetadata (last attempt,
/// last success, last status/error), Cache.ActiveStudentCount, IsSyncing, OnSyncFinished.
/// </summary>
public class StudentDirectoryService : MonoBehaviour
{
    public const string DeviceKeyFileName = VrDeviceKey.FileName;

    public static StudentDirectoryService Instance { get; private set; }

    /// <summary>Raised on the main thread whenever a sync attempt ends, successfully or not.</summary>
    public static event Action<StudentDirectorySyncStatus> OnSyncFinished;

    [SerializeField] private AssessmentUploadConfiguration configuration;
    [Tooltip("Run one directory sync when this component starts. Leave on; off only for tests.")]
    [SerializeField] private bool syncOnStart = true;
    [Tooltip("EDITOR ONLY - used when no device key file exists. Ignored in builds. Never commit a real key.")]
    [SerializeField] private string editorDeviceKeyOverride;

    private IStudentDirectoryTransport transport;
    private StudentDirectoryStore store;
    private string deviceKeyOverride;
    private bool deviceKeyOverrideSet;
    private CancellationTokenSource lifetimeCts;

    public StudentDirectoryCache Cache { get; private set; }
    public bool IsSyncing { get; private set; }
    public Task<StudentDirectorySyncStatus> CurrentSync { get; private set; }

    private void Awake()
    {
        Instance = this;
        lifetimeCts = new CancellationTokenSource();
        transport ??= new UnityWebRequestStudentDirectoryTransport();
        store ??= new StudentDirectoryStore();
        Cache ??= new StudentDirectoryCache(store);
    }

    private void Start()
    {
        if (syncOnStart)
        {
            SyncNow();
        }
    }

    private void OnDestroy()
    {
        lifetimeCts?.Cancel();
        lifetimeCts?.Dispose();
        lifetimeCts = null;
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>Starts a sync unless one is already running; returns the running one either way. Never throws.</summary>
    public Task<StudentDirectorySyncStatus> SyncNow()
    {
        if (IsSyncing && CurrentSync != null)
        {
            return CurrentSync;
        }
        CurrentSync = RunSync();
        return CurrentSync;
    }

    /// <summary>Local-only: empties this headset's cached directory. See StudentDirectoryCache.Clear - nothing on the server changes.</summary>
    public void ClearLocalDirectory()
    {
        Cache.Clear(DateTime.UtcNow);
        OnSyncFinished?.Invoke(StudentDirectorySyncStatus.Cleared);
    }

    private async Task<StudentDirectorySyncStatus> RunSync()
    {
        IsSyncing = true;
        StudentDirectorySyncStatus status;
        try
        {
            status = await SyncCore();
        }
        catch (Exception ex)
        {
            status = StudentDirectorySyncStatus.ServerError;
            Cache.RecordFailure(status, "Unexpected sync error (" + ex.GetType().Name + ").", DateTime.UtcNow);
        }
        finally
        {
            IsSyncing = false;
        }

        if (configuration != null && configuration.debugLoggingEnabled)
        {
            Debug.Log($"[StudentDirectoryService] Sync finished: {status}. Active cached students: {Cache.ActiveStudentCount}.");
        }
        OnSyncFinished?.Invoke(status);
        return status;
    }

    private async Task<StudentDirectorySyncStatus> SyncCore()
    {
        DateTime started = DateTime.UtcNow;
        Cache.RecordAttemptStarted(started);
        if (configuration == null || !configuration.IsValidForRuntimeUse(out _) || string.IsNullOrEmpty(configuration.StudentDirectoryUrl))
        {
            Cache.RecordFailure(StudentDirectorySyncStatus.NotConfigured, "No valid server configuration on this headset.", started);
            return StudentDirectorySyncStatus.NotConfigured;
        }
        string deviceKey = ResolveDeviceKey();
        if (string.IsNullOrEmpty(deviceKey))
        {
            Cache.RecordFailure(StudentDirectorySyncStatus.NotConfigured, "This headset has no device sync key.", started);
            return StudentDirectorySyncStatus.NotConfigured;
        }

        float timeout = Mathf.Max(1f, configuration.studentDirectorySyncTimeoutSeconds);

        using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(lifetimeCts?.Token ?? CancellationToken.None);
        Task<StudentDirectoryFetchResult> fetch = transport.FetchAsync(configuration.StudentDirectoryUrl, deviceKey, timeout, attemptCts.Token);
        // Hard ceiling independent of the transport honoring its own timeout or the token.
        Task deadline = Task.Delay(TimeSpan.FromSeconds(timeout + 1f));
        if (await Task.WhenAny(fetch, deadline) != fetch)
        {
            attemptCts.Cancel();
            Cache.RecordFailure(StudentDirectorySyncStatus.TimedOut, $"No answer from the server within {timeout:0} s.", DateTime.UtcNow);
            return StudentDirectorySyncStatus.TimedOut;
        }

        StudentDirectoryFetchResult result = await fetch;
        if (!result.Succeeded)
        {
            Cache.RecordFailure(result.FailureStatus, FailureMessage(result), DateTime.UtcNow);
            return result.FailureStatus;
        }
        return Cache.ApplyServerResponse(result.Body, DateTime.UtcNow);
    }

    private static string FailureMessage(StudentDirectoryFetchResult result) => result.FailureStatus switch
    {
        StudentDirectorySyncStatus.NetworkUnavailable => "Server unreachable (offline?). Using the cached directory.",
        StudentDirectorySyncStatus.TimedOut => "Server did not answer in time. Using the cached directory.",
        StudentDirectorySyncStatus.Unauthorized => "Server refused this headset's device key.",
        StudentDirectorySyncStatus.ServerError => $"Server error (HTTP {result.HttpStatusCode}). Using the cached directory.",
        _ => $"Unexpected server response (HTTP {result.HttpStatusCode}). Using the cached directory.",
    };

    private string ResolveDeviceKey()
    {
        if (deviceKeyOverrideSet)
        {
            return deviceKeyOverride;
        }
        return VrDeviceKey.Resolve(editorDeviceKeyOverride);
    }

    /// <summary>The device key every device-authenticated VR call uses (directory sync, code validation, upload authorization). Never log it.</summary>
    public string DeviceKey => ResolveDeviceKey();

    public AssessmentUploadConfiguration Configuration => configuration;

    /// <summary>Test seam: swap the network, the storage folder and the key before the first sync. Rebuilds the cache from the given store.</summary>
    public void ConfigureForTesting(AssessmentUploadConfiguration testConfiguration, IStudentDirectoryTransport testTransport, StudentDirectoryStore testStore, string deviceKey)
    {
        configuration = testConfiguration;
        transport = testTransport;
        store = testStore;
        deviceKeyOverride = deviceKey;
        deviceKeyOverrideSet = true;
        syncOnStart = false;
        Cache = new StudentDirectoryCache(store);
    }
}
