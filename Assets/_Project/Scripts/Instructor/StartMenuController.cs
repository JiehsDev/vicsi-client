// Assets/_Project/Scripts/Instructor/StartMenuController.cs
using TMPro;
using UnityEngine;

/// <summary>
/// Start menu (Main.unity) additions for Phase 5: the "Instructor / Headset Management"
/// entry and a compact status line (Online/Offline, sessions waiting to upload, student
/// directory sync). "Start Investigation" stays the existing UIManager.OnStartPressed,
/// which loads CSI_Environment where student identification runs (Phase 3).
///
/// The startup background work itself is done by the existing services on this scene's
/// _Managers - StudentDirectoryService (load cache, then one non-blocking sync) and
/// AssessmentUploadService (interrupted-upload recovery, 30-day cleanup, resume due
/// uploads) - this component only adds the reachability probe and shows the results.
/// Nothing here waits; the menu is usable immediately.
/// </summary>
public class StartMenuController : MonoBehaviour
{
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private InstructorManagementUI instructorUI;
    [SerializeField] private TMP_Text statusLine;
    [SerializeField] private AssessmentUploadConfiguration configuration;

    [SerializeField] private RectTransform statusTable;

    private bool dirty = true;

    private void OnEnable()
    {
        AssessmentUploadService.OnQueueEntryChanged += MarkDirty;
        AssessmentUploadService.OnQueueEntryRemoved += MarkDirtyId;
        StudentDirectoryService.OnSyncFinished += HandleSync;
        ServerReachabilityProbe.OnChanged += HandleReach;
        if (instructorUI != null) instructorUI.OnClosed += HandleInstructorClosed;
    }

    private void OnDisable()
    {
        AssessmentUploadService.OnQueueEntryChanged -= MarkDirty;
        AssessmentUploadService.OnQueueEntryRemoved -= MarkDirtyId;
        StudentDirectoryService.OnSyncFinished -= HandleSync;
        ServerReachabilityProbe.OnChanged -= HandleReach;
        if (instructorUI != null) instructorUI.OnClosed -= HandleInstructorClosed;
    }

    private void Start()
    {
        _ = ServerReachabilityProbe.CheckAsync(configuration);   // non-blocking
        dirty = true;
    }

    private void Update()
    {
        if (!dirty) return;
        dirty = false;
        RefreshStatus();
    }

    /// <summary>Button: Instructor / Headset Management.</summary>
    public void OpenInstructorManagement()
    {
        if (instructorUI == null) return;
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        instructorUI.Open();
    }

    private void HandleInstructorClosed()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        dirty = true;
    }

    public void RefreshStatus()
    {
        if (statusLine == null) return;
        var s = HeadsetStatusSnapshot.Build(
            StudentDirectoryService.Instance != null ? StudentDirectoryService.Instance.Cache : null,
            AssessmentUploadService.Instance != null ? AssessmentUploadService.Instance.GetAllEntries() : null,
            StudentDirectoryService.Instance != null && !string.IsNullOrEmpty(StudentDirectoryService.Instance.DeviceKey),
            configuration, ServerReachabilityProbe.Last, 0);
        int waiting = s.Queue.NotYetUploaded;
        string sync = string.IsNullOrEmpty(s.LastSuccessfulSyncUtc) ? "Students not synced yet" : $"Students synced {HeadsetStatusSnapshot.FormatUtc(s.LastSuccessfulSyncUtc)}";
        if (statusTable != null)
        {
            statusLine.alpha = 0;
            LobbyTableLayout.Clear(statusTable);
            LobbyTableLayout.Row(statusTable, "Connection", s.ConnectionLabel, statusLine.font);
            LobbyTableLayout.Row(statusTable, "Upload queue", $"{waiting} session{(waiting == 1 ? "" : "s")} waiting to upload", statusLine.font);
            LobbyTableLayout.Row(statusTable, "Student directory", sync, statusLine.font);
        }
        statusLine.text = $"{s.ConnectionLabel}   ·   {waiting} session{(waiting == 1 ? "" : "s")} waiting to upload   ·   {sync}";
    }

    private void MarkDirty(AssessmentUploadQueueEntry _) => dirty = true;
    private void MarkDirtyId(string _) => dirty = true;
    private void HandleReach(ServerReachability _) => dirty = true;
    private void HandleSync(StudentDirectorySyncStatus status)
    {
        if (status == StudentDirectorySyncStatus.Succeeded) ServerReachabilityProbe.Note(ServerReachability.Online);
        dirty = true;
    }
}
