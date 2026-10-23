// Assets/_Project/Scripts/Instructor/InstructorManagementUI.cs
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Instructor / Headset Management (Phase 5): one world-space workspace, built at runtime
/// inside the start menu's existing ray-interactable Canvas (same Meta Interaction SDK
/// PointableCanvas the menu buttons already use - no new input path), like
/// DialogueHistoryUI builds its own panel. Views inside the one panel:
///
///   PIN entry ("Instructor Access", 4 masked slots, keypad, Unlock / Back)
///   Tabs: Students | Sessions | Headset Status | Settings
///   Confirm dialog, PIN re-check, details overlay
///
/// Everything shown comes from the existing systems - StudentDirectoryService.Cache
/// (ActiveStudentCount, SyncMetadata, Students, TestStudent), AssessmentUploadService
/// (GetAllEntries, RetryNow, retention), AssessmentUploadQueueView /
/// UploadedSessionRetention (states, retention labels), InstructorPinStore - and refreshes
/// live on OnQueueEntryChanged / OnQueueEntryRemoved / OnSyncFinished. It never shows a
/// device key, pairing token, upload token, internal UUID (except under Diagnostics) or
/// grade, and offers no generic "delete session".
/// </summary>
public class InstructorManagementUI : MonoBehaviour
{
    public enum View { Closed, Pin, Students, Sessions, Status, Settings }
    private enum PinMode { Unlock, Recheck, ChangeCurrent, ChangeNew, ChangeConfirm }

    [SerializeField] private RectTransform hostCanvas;
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private AssessmentUploadConfiguration configuration;
    [Tooltip("Consecutive wrong PINs before the cooldown.")]
    [SerializeField] private int maxFailedPinAttempts = 5;
    [Tooltip("Cooldown length after too many wrong PINs, in seconds.")]
    [SerializeField] private float pinCooldownSeconds = 30f;

    public event Action OnClosed;

    // ------------------------------------------------------------------ dependencies (swappable for tests)
    public InstructorPinStore PinStore { get; set; }
    public Func<StudentDirectoryService> Directory { get; set; } = () => StudentDirectoryService.Instance;
    public Func<AssessmentUploadService> Uploads { get; set; } = () => AssessmentUploadService.Instance;

    // ------------------------------------------------------------------ state
    public View CurrentView { get; private set; } = View.Closed;
    public bool IsOpen => CurrentView != View.Closed;
    public bool Unlocked { get; private set; }
    public string FeedbackText => feedback != null ? feedback.text : null;
    public string PinPromptText => pinPrompt != null ? pinPrompt.text : null;
    public string PinSlotsText => pinSlots != null ? pinSlots.text : null;
    public string SummaryText => summary != null ? summary.text : null;
    public string DetailsText => detailsOpen ? detailsBody.text : null;
    public string ConfirmText => confirmOpen ? confirmTitle.text + "\n" + confirmBody.text : null;
    public IReadOnlyList<string> RowTexts => rowLabels.Where(l => l.transform.parent.gameObject.activeSelf).Select(l => l.text).ToList();
    public bool DetailsRetryAvailable => detailsOpen && detailsRetry.gameObject.activeSelf;
    public int CurrentPage { get; private set; }
    public string StudentSearch { get; private set; } = string.Empty;

    private PinMode pinMode;
    private string pinBuffer = string.Empty;
    private string changeCurrent, changeNew;
    private Action pendingRecheckAction;
    private Action pendingConfirmAction;
    private bool confirmOpen, detailsOpen;
    private bool dirty;
    private string detailsSessionId;
    private List<object> pageItems = new();

    // ------------------------------------------------------------------ ui refs
    private GameObject root, pinGroup, mainGroup, confirmGroup, detailsGroup;
    private TMP_Text title, pinPrompt, pinSlots, feedback, summary, pageLabel, confirmTitle, confirmBody, detailsBody;
    private readonly List<TMP_Text> rowLabels = new();
    private readonly List<Button> rowButtons = new();
    private readonly List<Button> tabButtons = new();
    private Button footerA, footerB, prevPage, nextPage, detailsRetry, detailsAux;
    private TMP_Text footerALabel, footerBLabel, detailsAuxLabel;
    private TMP_InputField searchField;
    private Action footerAAction, footerBAction, detailsAuxAction;

    private const int RowsPerPage = 7;

    // Restrained forensic palette, consistent with the existing dark panels.
    private static readonly Color PanelColor = new(0.07f, 0.08f, 0.10f, 0.97f);
    private static readonly Color RowColor = new(0.12f, 0.13f, 0.16f, 1f);
    private static readonly Color ButtonColor = new(0.17f, 0.24f, 0.33f, 1f);      // muted blue
    private static readonly Color ActiveTabColor = new(0.29f, 0.45f, 0.62f, 1f);
    private static readonly Color DangerColor = new(0.45f, 0.20f, 0.18f, 1f);
    private static readonly Color TextColor = new(0.93f, 0.92f, 0.88f, 1f);       // off-white
    private static readonly Color MutedText = new(0.66f, 0.67f, 0.68f, 1f);
    private static readonly Color Gold = new(0.82f, 0.68f, 0.36f, 1f);

    // ================================================================== lifecycle

    private void Awake()
    {
        if (CompetitiveEdition.Enabled) { enabled = false; return; }
        PinStore ??= new InstructorPinStore(null, maxFailedPinAttempts, pinCooldownSeconds);
        if (hostCanvas != null) Build(hostCanvas);
    }

    private void OnEnable()
    {
        AssessmentUploadService.OnQueueEntryChanged += HandleQueueChanged;
        AssessmentUploadService.OnQueueEntryRemoved += HandleQueueRemoved;
        StudentDirectoryService.OnSyncFinished += HandleSyncFinished;
        ServerReachabilityProbe.OnChanged += HandleReachability;
    }

    private void OnDisable()
    {
        AssessmentUploadService.OnQueueEntryChanged -= HandleQueueChanged;
        AssessmentUploadService.OnQueueEntryRemoved -= HandleQueueRemoved;
        StudentDirectoryService.OnSyncFinished -= HandleSyncFinished;
        ServerReachabilityProbe.OnChanged -= HandleReachability;
    }

    private void Update()
    {
        if (dirty && IsOpen)
        {
            dirty = false;
            Refresh();
        }
        if (CurrentView == View.Pin && PinStore != null && PinStore.CooldownSecondsRemaining > 0)
        {
            SetFeedback($"Too many incorrect attempts. Try again in {PinStore.CooldownSecondsRemaining} s.");
        }
    }

    private void HandleQueueChanged(AssessmentUploadQueueEntry _) => dirty = true;
    private void HandleQueueRemoved(string _) => dirty = true;
    private void HandleSyncFinished(StudentDirectorySyncStatus status)
    {
        if (status == StudentDirectorySyncStatus.Succeeded) ServerReachabilityProbe.Note(ServerReachability.Online);
        dirty = true;
    }
    private void HandleReachability(ServerReachability _) => dirty = true;

    /// <summary>Builds the panel under a canvas (normally done from the serialized hostCanvas in Awake).</summary>
    public void Build(RectTransform canvas)
    {
        if (root != null) return;
        hostCanvas = canvas;
        BuildUi();
        root.SetActive(false);
    }

    // ================================================================== open / close / pin

    /// <summary>Entry point from the start menu: always starts locked at the PIN screen.</summary>
    public void Open()
    {
        Unlocked = false;
        root.SetActive(true);
        root.transform.SetAsLastSibling();
        StartPin(PinMode.Unlock, "Instructor Access");
    }

    public void Close()
    {
        Unlocked = false;
        pinBuffer = string.Empty;
        changeCurrent = changeNew = null;
        CloseOverlays();
        CurrentView = View.Closed;
        root.SetActive(false);
        OnClosed?.Invoke();
    }

    private void StartPin(PinMode mode, string prompt)
    {
        pinMode = mode;
        pinBuffer = string.Empty;
        CloseOverlays();
        CurrentView = View.Pin;
        pinGroup.SetActive(true);
        mainGroup.SetActive(false);
        title.text = "Instructor / Headset Management";
        pinPrompt.text = prompt;
        UpdateSlots();
        SetFeedback(PinStore.CooldownSecondsRemaining > 0 ? $"Too many incorrect attempts. Try again in {PinStore.CooldownSecondsRemaining} s." : string.Empty);
    }

    public void PressDigit(int digit)
    {
        if (CurrentView != View.Pin || pinBuffer.Length >= 4 || digit < 0 || digit > 9) return;
        pinBuffer += (char)('0' + digit);
        UpdateSlots();
    }

    public void Backspace()
    {
        if (pinBuffer.Length > 0) pinBuffer = pinBuffer.Substring(0, pinBuffer.Length - 1);
        UpdateSlots();
    }

    public void ClearPin()
    {
        pinBuffer = string.Empty;
        UpdateSlots();
    }

    private void UpdateSlots()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < 4; i++) sb.Append(i < pinBuffer.Length ? "● " : "○ ");
        pinSlots.text = sb.ToString().TrimEnd();
    }

    /// <summary>Unlock / OK on the keypad - meaning depends on the mode.</summary>
    public void SubmitPin()
    {
        if (CurrentView != View.Pin) return;
        string entered = pinBuffer;
        pinBuffer = string.Empty;
        UpdateSlots();

        switch (pinMode)
        {
            case PinMode.Unlock:
            case PinMode.Recheck:
            {
                var result = PinStore.Verify(entered);
                if (result.Outcome == InstructorPinStore.VerifyOutcome.Accepted)
                {
                    if (pinMode == PinMode.Unlock)
                    {
                        Unlocked = true;
                        ShowTab(View.Status);
                        _ = ServerReachabilityProbe.CheckAsync(configuration);
                    }
                    else
                    {
                        var action = pendingRecheckAction;
                        pendingRecheckAction = null;
                        ShowTab(previousTab);
                        action?.Invoke();
                    }
                    return;
                }
                SetFeedback(PinFeedback(result));
                return;
            }
            case PinMode.ChangeCurrent:
                changeCurrent = entered;
                StartPin(PinMode.ChangeNew, "New PIN");
                return;
            case PinMode.ChangeNew:
                if (!InstructorPinStore.IsWellFormed(entered))
                {
                    SetFeedback("Enter exactly 4 digits.");
                    return;
                }
                changeNew = entered;
                StartPin(PinMode.ChangeConfirm, "Confirm New PIN");
                return;
            case PinMode.ChangeConfirm:
            {
                var outcome = PinStore.Change(changeCurrent, changeNew, entered);
                changeCurrent = changeNew = null;
                ShowTab(View.Settings);
                SetFeedback(outcome switch
                {
                    InstructorPinStore.ChangeOutcome.Changed => "Instructor PIN changed.",
                    InstructorPinStore.ChangeOutcome.CurrentPinWrong => "Current PIN was incorrect. PIN not changed.",
                    InstructorPinStore.ChangeOutcome.ConfirmationMismatch => "The new PINs didn't match. PIN not changed.",
                    InstructorPinStore.ChangeOutcome.CoolingDown => $"Too many incorrect attempts. Try again in {PinStore.CooldownSecondsRemaining} s.",
                    _ => "The new PIN must be exactly 4 digits. PIN not changed.",
                });
                return;
            }
        }
    }

    /// <summary>Back on the keypad: leaves the PIN screen without unlocking / without running the pending action.</summary>
    public void PinBack()
    {
        if (CurrentView != View.Pin) return;
        if (pinMode == PinMode.Unlock)
        {
            Close();
            return;
        }
        pendingRecheckAction = null;
        changeCurrent = changeNew = null;
        ShowTab(pinMode == PinMode.Recheck ? previousTab : View.Settings);
        SetFeedback("Cancelled.");
    }

    private string PinFeedback(InstructorPinStore.VerifyResult r) => r.Outcome switch
    {
        InstructorPinStore.VerifyOutcome.CoolingDown => $"Too many incorrect attempts. Try again in {r.SecondsRemaining} s.",
        InstructorPinStore.VerifyOutcome.Malformed => "Enter all 4 digits.",
        _ => "Incorrect PIN.",
    };

    private View previousTab = View.Status;

    private void RequirePinRecheck(Action action)
    {
        previousTab = CurrentView == View.Pin ? previousTab : CurrentView;
        pendingRecheckAction = action;
        StartPin(PinMode.Recheck, "Re-enter Instructor PIN to confirm");
    }

    // ================================================================== tabs

    public void ShowTab(View tab)
    {
        if (!Unlocked || tab == View.Closed || tab == View.Pin) return;
        CloseOverlays();
        if (tab != CurrentView) CurrentPage = 0;
        CurrentView = tab;
        previousTab = tab;
        pinGroup.SetActive(false);
        mainGroup.SetActive(true);
        SetFeedback(string.Empty);
        Refresh();
    }

    public void NextPage()
    {
        if ((CurrentPage + 1) * RowsPerPage < pageItems.Count) CurrentPage++;
        Refresh();
    }

    public void PreviousPage()
    {
        if (CurrentPage > 0) CurrentPage--;
        Refresh();
    }

    public void SetStudentSearch(string query)
    {
        StudentSearch = query ?? string.Empty;
        if (searchField != null && searchField.text != StudentSearch) searchField.SetTextWithoutNotify(StudentSearch);
        CurrentPage = 0;
        if (CurrentView == View.Students) Refresh();
    }

    public void Refresh()
    {
        if (!IsOpen || CurrentView == View.Pin) return;
        for (int i = 0; i < tabButtons.Count; i++)
        {
            tabButtons[i].image.color = (int)CurrentView - (int)View.Students == i ? ActiveTabColor : ButtonColor;
        }
        searchField.gameObject.SetActive(CurrentView == View.Students);
        switch (CurrentView)
        {
            case View.Students: RenderStudents(); break;
            case View.Sessions: RenderSessions(); break;
            case View.Status: RenderStatus(); break;
            case View.Settings: RenderSettings(); break;
        }
        if (detailsOpen && detailsSessionId != null) OpenSessionDetails(detailsSessionId);
    }

    // ------------------------------------------------------------------ Students

    private void RenderStudents()
    {
        title.text = "Student Directory";
        var dir = Directory()?.Cache;
        if (dir == null)
        {
            summary.text = "Student directory service not available in this scene.";
            SetRows(new List<object>(), _ => string.Empty);
            SetFooter("Sync Student Directory", SyncDirectory, "Clear Local Student Directory", RequestClearLocalDirectory, danger: true);
            return;
        }
        var meta = dir.SyncMetadata;
        summary.text = $"Active students: {dir.ActiveStudentCount}   Last successful sync: {HeadsetStatusSnapshot.FormatUtc(meta.lastSuccessfulSyncUtc)}\n" +
                       $"Last sync result: {SyncLabel(meta.LastStatus)}{(string.IsNullOrEmpty(meta.lastError) ? "" : " - " + meta.lastError)}" +
                       (Directory()?.IsSyncing == true ? "\nSyncing..." : "");

        IEnumerable<CachedStudentEntry> students = dir.Students;
        string q = StudentSearch.Trim();
        if (q.Length > 0)
        {
            string canonical = StudentAccessCodes.Normalize(q);
            if (canonical != null)
            {
                // Codes are stored only as salted hashes, so a code search is a lookup, not a text match.
                var hit = dir.Lookup(canonical);
                students = hit.Student != null ? new[] { hit.Student } : Array.Empty<CachedStudentEntry>();
            }
            else
            {
                students = students.Where(s => s.displayName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                    || s.classes.Any(c => (c.className ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                                       || (c.classSection ?? "").Equals(q, StringComparison.OrdinalIgnoreCase)));
            }
        }
        var list = students.OrderBy(s => s.displayName, StringComparer.OrdinalIgnoreCase).Cast<object>().ToList();
        SetRows(list, o =>
        {
            var s = (CachedStudentEntry)o;
            return $"{s.displayName}   <color=#{ColorUtility.ToHtmlStringRGB(MutedText)}>{ClassText(s.classes)}</color>   " +
                   (s.enabled ? "<color=#8FB98B>Active</color>" : "<color=#C9897F>Disabled</color>");
        }, o => OpenStudentDetails((CachedStudentEntry)o));
        SetFooter("Sync Student Directory", SyncDirectory, "Clear Local Student Directory", RequestClearLocalDirectory, danger: true);
    }

    private void OpenStudentDetails(CachedStudentEntry s)
    {
        detailsSessionId = null;
        detailsBody.text =
            $"<b>{s.displayName}</b>\n" +
            $"Class: {ClassText(s.classes)}\n" +
            $"Status: {(s.enabled ? "Active" : "Disabled")}\n" +
            $"Code version: {s.codeVersion}\n" +
            $"Last synced: {HeadsetStatusSnapshot.FormatUtc(s.lastSyncedAtUtc)}\n\n" +
            "<size=80%>Access codes are kept on this headset only as protected hashes, so the code itself can't be shown here. " +
            "View, regenerate, enable or disable codes on the ViCSI dashboard; this headset picks up the change at its next sync.</size>";
        detailsRetry.gameObject.SetActive(false);
        detailsAux.gameObject.SetActive(true);
        detailsAuxLabel.text = "Regenerate / Enable / Disable";
        detailsAuxAction = () => SetFeedback(ServerReachabilityProbe.Last == ServerReachability.Online
            ? "Code changes are made on the ViCSI dashboard (instructor sign-in required); not available from the headset."
            : "Internet connection is required to change this student's access code.");
        ShowDetails();
    }

    public void SyncDirectory()
    {
        var service = Directory();
        if (service == null)
        {
            SetFeedback("Student directory service not available.");
            return;
        }
        if (string.IsNullOrEmpty(service.DeviceKey))
        {
            SetFeedback("This headset has no device credential, so it can't sync. The current directory is kept.");
            return;
        }
        SetFeedback("Syncing student directory...");
        var task = service.SyncNow();
        dirty = true;
        task.ContinueWith(t => dirty = true, System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
        task.ContinueWith(t =>
        {
            SetFeedback(t.Result switch
            {
                StudentDirectorySyncStatus.Succeeded => "Student directory synced.",
                StudentDirectorySyncStatus.NetworkUnavailable or StudentDirectorySyncStatus.TimedOut => "Couldn't reach the server. The current directory is kept.",
                StudentDirectorySyncStatus.Unauthorized => "The server refused this headset's device credential. The current directory is kept.",
                StudentDirectorySyncStatus.NotConfigured => "This headset isn't configured to sync. The current directory is kept.",
                _ => "Sync failed. The current directory is kept.",
            });
        }, System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
    }

    public void RequestClearLocalDirectory()
    {
        ShowConfirm("Clear Local Student Directory?",
            "This removes locally cached student codes from this headset. It does not delete students or codes from the server.\n\n" +
            "Sessions, uploads and their owners on this headset are not affected. The directory refills at the next successful sync.",
            "Continue", () => RequirePinRecheck(() =>
            {
                Directory()?.ClearLocalDirectory();
                SetFeedback("Local student directory cleared.");
                dirty = true;
            }));
    }

    // ------------------------------------------------------------------ Sessions

    private void RenderSessions()
    {
        title.text = "Sessions on this Headset";
        var service = Uploads();
        if (service == null)
        {
            summary.text = "Upload service not available in this scene.";
            SetRows(new List<object>(), _ => string.Empty);
            SetFooter(null, null, null, null);
            return;
        }
        var entries = service.GetAllEntries();
        var s = AssessmentUploadQueueView.Summarize(entries);
        summary.text = $"Pending {s.Pending + s.Uploading}   Retry {s.RetryWaiting}   Waiting for assignment {s.NoAssignment}   Review {s.RequiresReview + s.AwaitingPairing}\n" +
                       $"Uploaded backups {s.Uploaded}   Legacy uploaded {s.LegacyUploaded}   Eligible for cleanup {service.CountCleanupEligible()}";

        var list = entries
            .OrderBy(e => AssessmentUploadQueueView.Classify(e) is AssessmentUploadQueueView.DisplayState.Uploaded or AssessmentUploadQueueView.DisplayState.LegacyUploaded ? 1 : 0)
            .ThenByDescending(e => e.sessionCompletedAtUtc ?? e.createdAtUtc)
            .Cast<object>().ToList();
        SetRows(list, o =>
        {
            var e = (AssessmentUploadQueueEntry)o;
            string test = e.isTestSession ? $"<color=#{ColorUtility.ToHtmlStringRGB(Gold)}>TEST</color> " : "";
            return $"{test}{Owner(e)}   <color=#{ColorUtility.ToHtmlStringRGB(MutedText)}>{e.scenarioIdForDisplay}  {HeadsetStatusSnapshot.FormatUtc(e.sessionCompletedAtUtc ?? e.createdAtUtc)}  #{Short(e.sessionId)}</color>\n" +
                   $"<size=85%>{StateColor(e)}{AssessmentUploadQueueView.StateLabel(e)}</color></size>";
        }, o => OpenSessionDetails(((AssessmentUploadQueueEntry)o).sessionId));
        SetFooter("Clean Expired Uploaded Backups", RequestCleanExpired, $"Clear Legacy Uploaded ({s.LegacyUploaded})", RequestClearLegacy, danger: true);
    }

    public void OpenSessionDetails(string sessionId)
    {
        var service = Uploads();
        var e = service?.GetAllEntries().Find(x => x.sessionId == sessionId);
        if (e == null)
        {
            CloseDetails();
            return;
        }
        detailsSessionId = sessionId;
        string retention = AssessmentUploadQueueView.RetentionLabel(e, service.DeviceNow, service.TrustedNowForRetention());
        detailsBody.text =
            $"<b>{Owner(e)}</b>{(e.isTestSession ? "   (Test Mode)" : "")}\n" +
            $"Session: {e.sessionId}\n" +
            $"Scenario: {e.scenarioIdForDisplay}\n" +
            $"Completed: {HeadsetStatusSnapshot.FormatUtc(e.sessionCompletedAtUtc ?? e.createdAtUtc)}\n" +
            $"Upload state: <b>{AssessmentUploadQueueView.StateLabel(e)}</b>\n" +
            $"Last attempt: {HeadsetStatusSnapshot.FormatUtc(e.lastAttemptedAtUtc)}\n" +
            $"Attempts: {e.attemptCount}\n" +
            (string.IsNullOrEmpty(e.lastErrorMessage) ? "" : $"Last result: {e.lastErrorMessage}\n") +
            (string.IsNullOrEmpty(e.uploadConfirmedAtUtc) ? "" : $"Upload confirmed: {HeadsetStatusSnapshot.FormatUtc(e.uploadConfirmedAtUtc)}\n") +
            (retention == null ? "" : $"Local backup: {retention}\n") +
            $"\n<size=85%>{AssessmentUploadQueueView.StateExplanation(e)}</size>";
        detailsRetry.gameObject.SetActive(AssessmentUploadQueueView.CanRetry(e));
        detailsAux.gameObject.SetActive(false);
        ShowDetails();
    }

    /// <summary>Retry Upload - the existing AssessmentUploadService.RetryNow, nothing else.</summary>
    public void RetrySession(string sessionId)
    {
        var service = Uploads();
        var e = service?.GetAllEntries().Find(x => x.sessionId == sessionId);
        if (e == null || !AssessmentUploadQueueView.CanRetry(e)) return;
        service.RetryNow(sessionId);
        SetFeedback("Retrying upload...");
        dirty = true;
    }

    public void RequestCleanExpired()
    {
        var service = Uploads();
        int eligible = service != null ? service.CountCleanupEligible() : 0;
        if (eligible == 0)
        {
            SetFeedback(service?.TrustedNowForRetention() == null
                ? "Nothing to clean: no trusted server time yet, so no backup is treated as expired."
                : "Nothing to clean: no uploaded backup is past its 30-day retention.");
            return;
        }
        ShowConfirm("Clean Expired Uploaded Backups?",
            $"{eligible} uploaded session backup(s) are past their 30-day local retention. Their local copies will be removed. " +
            "Server records are not affected. Pending, failed and waiting sessions are never included.",
            "Clean", () =>
            {
                var result = Uploads()?.RunRetentionCleanup();
                SetFeedback(result == null ? "Upload service not available." : $"Removed {result.RemovedSessionIds.Count} expired local backup(s)." +
                    (result.RetainedRawLogFiles.Count > 0 ? $" {result.RetainedRawLogFiles.Count} raw log file(s) kept (not provably linked)." : ""));
                dirty = true;
            });
    }

    public void RequestClearLegacy()
    {
        var service = Uploads();
        int legacy = service == null ? 0 : service.GetAllEntries().Count(AssessmentUploadQueueView.IsLegacyUploaded);
        if (legacy == 0)
        {
            SetFeedback("There are no legacy uploaded backups.");
            return;
        }
        ShowConfirm("Clear Legacy Uploaded Local Backups?",
            $"{legacy} session(s) are already marked uploaded but do not have a reliable local retention date. " +
            "This removes only their local headset copies. Server records are not deleted.\n\nPending, failed and waiting sessions are never included.",
            "Continue", () => RequirePinRecheck(() =>
            {
                var result = Uploads()?.ClearLegacyUploadedLocalBackups();
                SetFeedback(result == null ? "Upload service not available." : $"Removed {result.RemovedSessionIds.Count} legacy local backup(s)." +
                    (result.RetainedRawLogFiles.Count > 0 ? $" {result.RetainedRawLogFiles.Count} raw log file(s) kept (not provably linked)." : ""));
                dirty = true;
            }));
    }

    // ------------------------------------------------------------------ Status

    public HeadsetStatusSnapshot BuildSnapshot()
    {
        var dirService = Directory();
        var uploads = Uploads();
        return HeadsetStatusSnapshot.Build(
            dirService?.Cache,
            uploads != null ? (IReadOnlyList<AssessmentUploadQueueEntry>)uploads.GetAllEntries() : Array.Empty<AssessmentUploadQueueEntry>(),
            dirService != null && !string.IsNullOrEmpty(dirService.DeviceKey),
            configuration,
            ServerReachabilityProbe.Last,
            uploads != null ? uploads.CountCleanupEligible() : 0);
    }

    // Compact (status/settings) lists: 9 rows of 48 px fit the 448 px list area exactly;
    // more lines page instead of spilling onto the pager/footer.
    private const int CompactRows = 9;

    private static string Owner(AssessmentUploadQueueEntry e)
    {
        string name = AssessmentUploadQueueView.OwnerDisplay(e);
        return string.IsNullOrWhiteSpace(name) ? "Unknown student" : name;
    }

    private void RenderStatus()
    {
        title.text = "Headset Status";
        var s = BuildSnapshot();
        summary.text = $"<size=130%><color=#{(s.Ready ? "8FB98B" : ColorUtility.ToHtmlStringRGB(Gold))}><b>{s.ReadinessLabel}</b></color></size>";
        var lines = new List<object>
        {
            $"Connection   {s.ConnectionLabel} ({s.EnvironmentName})",
            $"Device provisioning   {(s.DeviceCredentialAvailable ? "Device credential available" : "Device credential missing")}",
            $"Student directory   {s.ActiveCachedStudents} active students",
            $"Last sync   {HeadsetStatusSnapshot.FormatUtc(s.LastSuccessfulSyncUtc)}",
            $"Sync result   {SyncLabel(s.LastSyncStatus)}",
            $"Pending uploads   {s.Queue.Pending + s.Queue.Uploading}",
            $"Retry waiting   {s.Queue.RetryWaiting}",
            $"Assignment waiting   {s.Queue.NoAssignment}",
            $"Upload review   {s.Queue.RequiresReview + s.Queue.AwaitingPairing}",
            $"Uploaded backups   {s.Queue.Uploaded}",
            $"Legacy uploaded   {s.Queue.LegacyUploaded}",
            $"Cleanup eligible   {s.CleanupEligible}",
            $"Local records   {s.TotalLocalSessionRecords}",
        };
        foreach (var a in s.ActionsNeeded) lines.Add($"<color=#{ColorUtility.ToHtmlStringRGB(Gold)}>• {a}</color>");
        foreach (var n in s.Notes) lines.Add($"<color=#{ColorUtility.ToHtmlStringRGB(MutedText)}>{n}</color>");
        SetRows(lines, o => (string)o, null, pageSize: CompactRows);
        SetFooter("Check Connection", () => { _ = ServerReachabilityProbe.CheckAsync(configuration); SetFeedback("Checking server..."); }, "Sync Student Directory", SyncDirectory);
    }

    // ------------------------------------------------------------------ Settings

    private void RenderSettings()
    {
        title.text = "Settings";
        summary.text = PinStore.IsDefaultPin ? "<color=#D2AD5C>The Instructor PIN is still the default (0000). Change it before classroom use.</color>" : "Instructor PIN is set.";
        var lines = new List<object>
        {
            $"App version   {Application.version}",
            $"Environment   {(configuration != null ? configuration.environmentName : "(none)")}",
            $"Local data   {ShortPath(Application.persistentDataPath)}",
            $"PIN lock   {PinStore.MaxFailedAttempts} wrong attempts -> {Mathf.RoundToInt(PinStore.CooldownSeconds)} s cooldown",
        };
        SetRows(lines, o => (string)o, null, pageSize: CompactRows);
        SetFooter("Change Instructor PIN", BeginChangePin, null, null);
    }

    public void BeginChangePin()
    {
        previousTab = View.Settings;
        StartPin(PinMode.ChangeCurrent, "Current PIN");
    }

    // ================================================================== overlays

    private void ShowConfirm(string heading, string body, string confirmLabel, Action onConfirm)
    {
        CloseDetails();
        pendingConfirmAction = onConfirm;
        confirmTitle.text = heading;
        confirmBody.text = body;
        confirmGroup.transform.Find("ConfirmButton").GetComponentInChildren<TMP_Text>().text = confirmLabel;
        confirmGroup.SetActive(true);
        confirmGroup.transform.SetAsLastSibling();
        confirmOpen = true;
    }

    public void ConfirmYes()
    {
        var action = pendingConfirmAction;
        pendingConfirmAction = null;
        confirmGroup.SetActive(false);
        confirmOpen = false;
        action?.Invoke();
    }

    public void ConfirmNo()
    {
        pendingConfirmAction = null;
        confirmGroup.SetActive(false);
        confirmOpen = false;
        SetFeedback("Cancelled.");
    }

    private void ShowDetails()
    {
        LobbyTableLayout.Details(detailsTable, detailsBody.text, font);
        detailsGroup.SetActive(true);
        detailsGroup.transform.SetAsLastSibling();
        detailsOpen = true;
    }

    public void CloseDetails()
    {
        detailsSessionId = null;
        if (detailsGroup != null) detailsGroup.SetActive(false);
        detailsOpen = false;
    }

    private void CloseOverlays()
    {
        CloseDetails();
        if (confirmGroup != null) confirmGroup.SetActive(false);
        confirmOpen = false;
    }

    public void PressDetailsRetry()
    {
        if (detailsSessionId != null) RetrySession(detailsSessionId);
    }

    // ================================================================== rows / footer helpers

    private Action<object> rowClick;
    private Func<object, string> rowText;
    private int pageSize = RowsPerPage;

    private void SetRows(List<object> items, Func<object, string> text, Action<object> onClick = null, int pageSize = RowsPerPage)
    {
        bool accounts = CurrentView == View.Students || CurrentView == View.Sessions;
        tableHeader.text = accounts ? "STUDENT" : "DETAIL";
        tableValueHeader.text = CurrentView == View.Students ? "CLASS / STATUS" : CurrentView == View.Sessions ? "SESSION / UPLOAD STATUS" : "VALUE";
        tableValueHeader.rectTransform.anchoredPosition = new Vector2(accounts ? 254 : 234, -242);
        pageItems = items;
        rowText = text;
        rowClick = onClick;
        this.pageSize = Mathf.Min(pageSize, rowLabels.Count);
        int pages = Mathf.Max(1, Mathf.CeilToInt(items.Count / (float)this.pageSize));
        CurrentPage = Mathf.Clamp(CurrentPage, 0, pages - 1);
        for (int i = 0; i < rowLabels.Count; i++)
        {
            int index = CurrentPage * this.pageSize + i;
            bool show = i < this.pageSize && index < items.Count;
            rowButtons[i].gameObject.SetActive(show);
            if (!show) continue;
            rowLabels[i].text = text(items[index]);
            rowButtons[i].interactable = onClick != null;
            rowButtons[i].image.color = onClick != null ? RowColor : new Color(0, 0, 0, 0);
            var rt = (RectTransform)rowButtons[i].transform;
            float h = this.pageSize > RowsPerPage ? 58f : 74f;
            rowLabels[i].alpha = 0;
            LobbyTableLayout.Clear(rowTables[i]);
            string copy = rowLabels[i].text;
            int split = copy.IndexOf("   ", StringComparison.Ordinal);
            // Existing list callbacks continue to own names, statuses, filtering and clicks.
            LobbyTableLayout.Row(rowTables[i], split > 0 ? copy.Substring(0, split) : "",
                split > 0 ? copy.Substring(split + 3) : copy, font,
                CurrentView == View.Students || CurrentView == View.Sessions ? 200 : 180, 22);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, h - 6f);
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -(i * h));
            var cell = rowTables[i].GetChild(rowTables[i].childCount - 1).gameObject;
            cell.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = true;
            var height = cell.AddComponent<LayoutElement>();
            height.minHeight = height.preferredHeight = h - 6;
            foreach (var t in cell.GetComponentsInChildren<TMP_Text>()) t.overflowMode = TextOverflowModes.Ellipsis;
        }
        bool paged = items.Count > this.pageSize;
        prevPage.gameObject.SetActive(paged);
        nextPage.gameObject.SetActive(paged);
        pageLabel.text = items.Count == 0 ? (CurrentView == View.Students ? "No students cached on this headset." : CurrentView == View.Sessions ? "No sessions on this headset." : "")
            : paged ? $"Page {CurrentPage + 1} of {pages}   ({items.Count})" : "";
    }

    private void OnRowPressed(int i)
    {
        int index = CurrentPage * pageSize + i;
        if (rowClick != null && index < pageItems.Count) rowClick(pageItems[index]);
    }

    private void SetFooter(string a, Action aAction, string b, Action bAction, bool danger = false)
    {
        footerA.gameObject.SetActive(a != null);
        footerB.gameObject.SetActive(b != null);
        if (a != null) footerALabel.text = a;
        if (b != null) footerBLabel.text = b;
        footerAAction = aAction;
        footerBAction = bAction;
        footerB.image.color = danger ? DangerColor : ButtonColor;
    }

    private void SetFeedback(string text)
    {
        if (feedback != null) feedback.text = text ?? string.Empty;
    }

    private static string SyncLabel(StudentDirectorySyncStatus s) => s switch
    {
        StudentDirectorySyncStatus.Succeeded => "Succeeded",
        StudentDirectorySyncStatus.NeverAttempted => "Never attempted",
        StudentDirectorySyncStatus.NotConfigured => "Not configured",
        StudentDirectorySyncStatus.NetworkUnavailable => "Offline",
        StudentDirectorySyncStatus.TimedOut => "Timed out",
        StudentDirectorySyncStatus.Unauthorized => "Device credential refused",
        StudentDirectorySyncStatus.ServerError => "Server error",
        StudentDirectorySyncStatus.InvalidResponse => "Invalid server response",
        StudentDirectorySyncStatus.SaveFailed => "Saved in memory only",
        StudentDirectorySyncStatus.Cleared => "Cleared on this headset",
        _ => s.ToString(),
    };

    private static string ClassText(List<CachedStudentClass> classes) =>
        classes == null || classes.Count == 0 ? "No class" : string.Join(", ", classes.Select(c => string.IsNullOrEmpty(c.classSection) ? c.className : $"{c.className} - {c.classSection}"));

    private static string Short(string sessionId) => string.IsNullOrEmpty(sessionId) ? "-" : sessionId.Substring(0, Math.Min(8, sessionId.Length));

    private static string ShortPath(string path) => path.Length > 48 ? "..." + path.Substring(path.Length - 45) : path;

    private static string StateColor(AssessmentUploadQueueEntry e) => AssessmentUploadQueueView.Classify(e) switch
    {
        AssessmentUploadQueueView.DisplayState.Uploaded or AssessmentUploadQueueView.DisplayState.LegacyUploaded => "<color=#8FB98B>",
        AssessmentUploadQueueView.DisplayState.NoAssignment or AssessmentUploadQueueView.DisplayState.RequiresReview => $"<color=#{ColorUtility.ToHtmlStringRGB(Gold)}>",
        _ => $"<color=#{ColorUtility.ToHtmlStringRGB(TextColor)}>",
    };

    // ================================================================== construction

    private TMP_Text tableHeader, tableValueHeader;
    private RectTransform detailsTable;
    private readonly List<RectTransform> rowTables = new();

    private void BuildUi()
    {
        var actualSize = hostCanvas.rect.size;
        var size = new Vector2(800, 1100);

        root = NewRect("InstructorManagementPanel", hostCanvas, 0, 0, size.x, size.y);
        root.AddComponent<Image>().color = PanelColor;
        root.transform.localScale = Vector3.one * Mathf.Min(actualSize.x / size.x, actualSize.y / size.y);

        title = NewText(root.transform, "Title", "Instructor / Headset Management", 24, 14, size.x - 174, 52, 30, TextAlignmentOptions.Left, Gold);
        NewButton(root.transform, "CloseButton", "Close", size.x - 130, 12, 110, 44, Close);

        // ---------------- PIN view
        pinGroup = NewRect("PinGroup", root.transform, 0, 80, size.x, size.y - 80);
        pinPrompt = NewText(pinGroup.transform, "PinPrompt", "Instructor Access", 0, 20, size.x, 40, 28, TextAlignmentOptions.Center, TextColor);
        pinSlots = NewText(pinGroup.transform, "PinSlots", "", 0, 80, size.x, 60, 44, TextAlignmentOptions.Center, TextColor);
        // "Del", not a backspace glyph: the project's LiberationSans SDF has no U+232B (rendered as a box).
        string[] keys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "Clear", "0", "Del" };
        float kw = 130, kh = 70, gap = 12, kx = (size.x - (3 * kw + 2 * gap)) / 2f;
        for (int i = 0; i < keys.Length; i++)
        {
            string k = keys[i];
            float x = kx + (i % 3) * (kw + gap), y = 170 + (i / 3) * (kh + gap);
            Action press = k == "Clear" ? ClearPin : k == "Del" ? Backspace : () => PressDigit(k[0] - '0');
            NewButton(pinGroup.transform, "Key" + k, k, x, y, kw, kh, press, fontSize: 30);
        }
        float by = 170 + 4 * (kh + gap) + 14;
        NewButton(pinGroup.transform, "UnlockButton", "Unlock", size.x / 2f - 250, by, 240, 72, SubmitPin, color: ActiveTabColor);
        NewButton(pinGroup.transform, "PinBackButton", "Back", size.x / 2f + 10, by, 240, 72, PinBack);

        // ---------------- main (tabs) view
        mainGroup = NewRect("MainGroup", root.transform, 0, 80, size.x, size.y - 80);
        string[] tabs = { "Students", "Sessions", "Status", "Settings" };
        float tw = (size.x - 40 - 3 * 8) / 4f;
        for (int i = 0; i < tabs.Length; i++)
        {
            View v = View.Students + i;
            tabButtons.Add(NewButton(mainGroup.transform, "Tab" + tabs[i], tabs[i], 20 + i * (tw + 8), 0, tw, 52, () => ShowTab(v)));
        }
        summary = NewText(mainGroup.transform, "Summary", "", 24, 72, size.x - 48, 100, 22, TextAlignmentOptions.TopLeft, TextColor);
        var searchGo = NewRect("Search", mainGroup.transform, 24, 180, size.x - 48, 52);
        searchGo.AddComponent<Image>().color = RowColor;
        searchField = BuildInputField(searchGo, "Search name, class or code");
        searchField.onValueChanged.AddListener(SetStudentSearch);

        tableHeader = NewText(mainGroup.transform, "TableHeader", "", 38, 242, size.x - 76, 30, 20, TextAlignmentOptions.Left, MutedText);
        tableValueHeader = NewText(mainGroup.transform, "ValueHeader", "", 254, 242, size.x - 292, 30, 20, TextAlignmentOptions.Left, MutedText);
        var listRoot = NewRect("Rows", mainGroup.transform, 24, 280, size.x - 48, 74 * RowsPerPage);
        for (int i = 0; i < 12; i++)
        {
            int index = i;
            var b = NewButton(listRoot.transform, "Row" + i, "", 0, i * 64, size.x - 48, 58, () => OnRowPressed(index), fontSize: 18, align: TextAlignmentOptions.MidlineLeft, color: RowColor);
            rowButtons.Add(b);
            rowLabels.Add(b.GetComponentInChildren<TMP_Text>());
            var table = LobbyTableLayout.Create(b.transform, "Columns");
            table.GetComponent<VerticalLayoutGroup>().spacing = 0;
            rowTables.Add(table);
            b.gameObject.SetActive(false);
        }
        float pagerY = 280 + 74 * RowsPerPage + 12;
        prevPage = NewButton(mainGroup.transform, "PrevPage", "▲ Prev", 20, pagerY, 120, 40, PreviousPage, fontSize: 18);
        nextPage = NewButton(mainGroup.transform, "NextPage", "Next ▼", size.x - 140, pagerY, 120, 40, NextPage, fontSize: 18);
        pageLabel = NewText(mainGroup.transform, "PageLabel", "", 150, pagerY, size.x - 300, 40, 16, TextAlignmentOptions.Center, MutedText);
        float footerY = size.y - 80 - 150;
        footerA = NewButton(mainGroup.transform, "FooterA", "", 20, footerY, (size.x - 50) / 2f, 64, () => footerAAction?.Invoke(), fontSize: 22);
        footerB = NewButton(mainGroup.transform, "FooterB", "", 30 + (size.x - 50) / 2f, footerY, (size.x - 50) / 2f, 64, () => footerBAction?.Invoke(), fontSize: 22);
        footerALabel = footerA.GetComponentInChildren<TMP_Text>();
        footerBLabel = footerB.GetComponentInChildren<TMP_Text>();

        feedback = NewText(root.transform, "Feedback", "", 20, size.y - 64, size.x - 40, 56, 22, TextAlignmentOptions.Center, Gold);

        // ---------------- confirm overlay
        confirmGroup = NewRect("ConfirmGroup", root.transform, 0, 0, size.x, size.y);
        confirmGroup.AddComponent<Image>().color = new Color(0, 0, 0, 0.75f);
        var box = NewRect("Box", confirmGroup.transform, 30, size.y / 2f - 220, size.x - 60, 440);
        box.AddComponent<Image>().color = PanelColor;
        confirmTitle = NewText(box.transform, "ConfirmTitle", "", 20, 20, size.x - 100, 60, 24, TextAlignmentOptions.TopLeft, Gold);
        confirmBody = NewText(box.transform, "ConfirmBody", "", 20, 86, size.x - 100, 250, 24, TextAlignmentOptions.TopLeft, TextColor);
        NewButton(box.transform, "CancelButton", "Cancel", 20, 350, (size.x - 120) / 2f, 70, ConfirmNo);
        var confirmBtn = NewButton(box.transform, "ConfirmButton", "Confirm", 40 + (size.x - 120) / 2f, 350, (size.x - 120) / 2f, 70, ConfirmYes, color: DangerColor);
        confirmBtn.transform.SetParent(confirmGroup.transform, true);   // found by name from the group
        confirmGroup.SetActive(false);

        // ---------------- details overlay
        detailsGroup = NewRect("DetailsGroup", root.transform, 0, 0, size.x, size.y);
        detailsGroup.AddComponent<Image>().color = new Color(0, 0, 0, 0.75f);
        var dbox = NewRect("Box", detailsGroup.transform, 20, 70, size.x - 40, size.y - 150);
        dbox.AddComponent<Image>().color = PanelColor;
        detailsBody = NewText(dbox.transform, "DetailsBody", "", 20, 20, size.x - 80, size.y - 290, 19, TextAlignmentOptions.TopLeft, TextColor);
        detailsBody.alpha = 0; // preserve exact copy and diagnostic API; render aligned rows instead.
        var viewportGo = NewRect("DetailsViewport", dbox.transform, 20, 20, size.x - 80, size.y - 310);
        viewportGo.AddComponent<Image>().color = RowColor;
        viewportGo.AddComponent<RectMask2D>();
        detailsTable = LobbyTableLayout.Create(viewportGo.transform, "DetailsTable");
        detailsTable.anchorMin = new Vector2(0,1); detailsTable.anchorMax = Vector2.one;
        detailsTable.pivot = new Vector2(.5f,1); detailsTable.sizeDelta = Vector2.zero;
        detailsTable.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = viewportGo.AddComponent<ScrollRect>();
        scroll.viewport = (RectTransform)viewportGo.transform; scroll.content = detailsTable;
        scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
        float dy = size.y - 150 - 100;
        detailsRetry = NewButton(dbox.transform, "RetryUploadButton", "Retry Upload", 20, dy, 240, 72, PressDetailsRetry, color: ActiveTabColor);
        detailsAux = NewButton(dbox.transform, "AuxButton", "", 20, dy, 300, 72, () => detailsAuxAction?.Invoke(), fontSize: 22);
        detailsAuxLabel = detailsAux.GetComponentInChildren<TMP_Text>();
        NewButton(dbox.transform, "DetailsCloseButton", "Close", size.x - 40 - 200, dy, 180, 72, CloseDetails);
        detailsGroup.SetActive(false);

        pinGroup.SetActive(false);
        mainGroup.SetActive(false);
    }

    private GameObject NewRect(string name, Transform parent, float x, float y, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
        return go;
    }

    private TMP_Text NewText(Transform parent, string name, string text, float x, float y, float w, float h, float size, TextAlignmentOptions align, Color color)
    {
        var go = NewRect(name, parent, x, y, w, h);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.alignment = align;
        t.color = color;
        t.richText = true;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.raycastTarget = false;
        return t;
    }

    private Button NewButton(Transform parent, string name, string label, float x, float y, float w, float h, Action onClick,
        float fontSize = 22, TextAlignmentOptions align = TextAlignmentOptions.Center, Color? color = null)
    {
        var go = NewRect(name, parent, x, y, w, h);
        var img = go.AddComponent<Image>();
        img.color = color ?? ButtonColor;
        var b = go.AddComponent<Button>();
        var colors = b.colors;
        colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.6f);
        b.colors = colors;
        b.onClick.AddListener(() => onClick?.Invoke());
        var t = NewText(go.transform, "Label", label, align == TextAlignmentOptions.Center ? 6 : 16, 0, w - (align == TextAlignmentOptions.Center ? 12 : 28), h, fontSize, align, TextColor);
        t.overflowMode = TextOverflowModes.Ellipsis;
        return b;
    }

    private TMP_InputField BuildInputField(GameObject host, string placeholder)
    {
        var rt = (RectTransform)host.transform;
        var area = NewRect("TextArea", host.transform, 12, 4, rt.sizeDelta.x - 24, rt.sizeDelta.y - 8);
        area.AddComponent<RectMask2D>();
        var ph = NewText(area.transform, "Placeholder", placeholder, 0, 0, rt.sizeDelta.x - 24, rt.sizeDelta.y - 8, 18, TextAlignmentOptions.MidlineLeft, MutedText);
        var txt = NewText(area.transform, "Text", "", 0, 0, rt.sizeDelta.x - 24, rt.sizeDelta.y - 8, 18, TextAlignmentOptions.MidlineLeft, TextColor);
        txt.textWrappingMode = TextWrappingModes.NoWrap;
        var field = host.AddComponent<TMP_InputField>();
        field.textViewport = (RectTransform)area.transform;
        field.textComponent = txt;
        field.placeholder = ph;
        field.characterLimit = 40;
        if (font != null) field.fontAsset = font;
        return field;
    }
}
