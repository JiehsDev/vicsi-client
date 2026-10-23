// Assets/_Project/Scripts/Assessment/StudentIdentificationPanel.cs
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pre-session "who is playing?" screen (Phase 3), shown by PreSessionFlowController
/// before investigation unlocks:
///
///   Code entry:   [student code]  Continue | Continue Without Code
///   Real student: "Ana Reyes" / "CRIM 101 - A" / "Is this you?"     Continue | Change Code
///   Test mode:    "Test Mode" / "This session will be assigned to John Doe."  Continue | Back
///
/// Continue on the confirmation raises OnIdentified with the stable student; the
/// controller binds the session's owner at that moment. All decisions live in
/// StudentIdentificationFlow; this class only shows them. Scene-authored like
/// PairingCodeIdentityPanel: every element is a required reference, every string is
/// Inspector-editable. Never shows grades, codes, or anything from the server beyond
/// name and class.
/// </summary>
public class StudentIdentificationPanel : MonoBehaviour
{
    [Header("Copy")]
    [SerializeField, TextArea] private string instructionsText = "Enter your student code to begin.";
    [SerializeField] private string checkingMessage = "Checking...";
    [SerializeField] private string invalidFormatMessage = "Student codes have 8 letters and numbers, like K7M4-PQ2D.";
    [SerializeField] private string notRecognizedMessage = "That code isn't recognized. Check it and try again, or ask your instructor.";
    [SerializeField] private string disabledMessage = "This code has been disabled. Please ask your instructor.";
    [SerializeField] private string neverSyncedMessage = "This headset hasn't downloaded the class list yet. Connect it to the internet once, or choose Continue Without Code.";
    [SerializeField] private string testModeUnavailableMessage = "Test mode isn't available until this headset has synced with the server once.";
    [SerializeField] private string confirmQuestion = "Is this you?";
    [SerializeField] private string testModeTitle = "Test Mode";
    [SerializeField] private string testModeDetailFormat = "This session will be assigned to {0}.";
    [SerializeField] private string continueLabel = "Continue";
    [SerializeField] private string changeCodeLabel = "Change Code";
    [SerializeField] private string backLabel = "Back";

    [Header("Scene-authored UI - code entry")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private GameObject codeEntryGroup;
    [SerializeField] private TMP_Text instructionsLabel;
    [SerializeField] private TMP_InputField codeInputField;
    [SerializeField] private TMP_Text errorLabel;
    [SerializeField] private TMP_Text statusLabel;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button continueWithoutCodeButton;
    [Tooltip("Optional - lobby only: returns to the start menu from code entry. Leave empty where backing out isn't possible (the scenario scene).")]
    [SerializeField] private Button cancelButton;

    [Header("Scene-authored UI - confirmation")]
    [SerializeField] private GameObject confirmationGroup;
    [SerializeField] private RectTransform confirmationTable; // Optional lobby presentation only.
    [SerializeField] private TMP_Text confirmTitleLabel;
    [SerializeField] private TMP_Text confirmDetailLabel;
    [SerializeField] private TMP_Text confirmQuestionLabel;
    [SerializeField] private Button confirmButton;
    [SerializeField] private TMP_Text confirmButtonLabel;
    [SerializeField] private Button backButton;
    [SerializeField] private TMP_Text backButtonLabel;

    /// <summary>Raised once, when the student confirms who they are.</summary>
    public event Action<IdentifiedStudent> OnIdentified;

    /// <summary>Raised when the student backs out of code entry (only possible where cancelButton is assigned).</summary>
    public event Action OnCancelled;

    private StudentIdentificationFlow flow;
    private IdentifiedStudent pending;
    private bool busy;

    public bool IsShowing => panelRoot != null && panelRoot.activeSelf;

    /// <summary>The flow to use; PreSessionFlowController supplies one wired to the real services (tests supply fakes).</summary>
    public void SetFlow(StudentIdentificationFlow identificationFlow) => flow = identificationFlow;

    private void Awake()
    {
        if (continueButton != null) continueButton.onClick.AddListener(HandleContinueClicked);
        if (continueWithoutCodeButton != null) continueWithoutCodeButton.onClick.AddListener(HandleNoCodeClicked);
        if (confirmButton != null) confirmButton.onClick.AddListener(HandleConfirmClicked);
        if (backButton != null) backButton.onClick.AddListener(HandleBackClicked);
        if (cancelButton != null) cancelButton.onClick.AddListener(Cancel);
    }

    public void Show()
    {
        if (panelRoot != null) { HudFollowCamera.ConfigureShared(panelRoot, this, 600); panelRoot.SetActive(true); }
        ShowCodeEntry(clearInput: true);
    }

    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void ShowCodeEntry(bool clearInput)
    {
        pending = null;
        if (codeEntryGroup != null) codeEntryGroup.SetActive(true);
        if (confirmationGroup != null) confirmationGroup.SetActive(false);
        if (instructionsLabel != null) instructionsLabel.text = instructionsText;
        if (clearInput && codeInputField != null) codeInputField.text = string.Empty;
        SetError(null);
        SetStatus(null);
        SetInteractable(true);
    }

    // Public entry points so tests (and any future keypad) can drive the screen without clicking.
    public async void SubmitCode(string typedCode)
    {
        if (busy || flow == null) return;
        busy = true;
        SetInteractable(false);
        SetError(null);
        SetStatus(checkingMessage);
        IdentificationAttempt attempt;
        try
        {
            attempt = await flow.IdentifyByCodeAsync(typedCode);
        }
        finally
        {
            busy = false;
        }
        if (this == null) return; // destroyed while awaiting
        SetStatus(null);
        SetInteractable(true);
        if (attempt.Outcome == IdentificationOutcome.Identified)
        {
            ShowConfirmation(attempt.Student);
            return;
        }
        SetError(attempt.Outcome switch
        {
            IdentificationOutcome.InvalidFormat => invalidFormatMessage,
            IdentificationOutcome.Disabled => disabledMessage,
            IdentificationOutcome.DirectoryNeverSynced => neverSyncedMessage,
            _ => notRecognizedMessage,
        });
    }

    public void ChooseContinueWithoutCode()
    {
        if (busy || flow == null) return;
        var attempt = flow.ContinueWithoutCode();
        if (attempt.Outcome != IdentificationOutcome.Identified)
        {
            SetError(testModeUnavailableMessage);
            return;
        }
        ShowConfirmation(attempt.Student);
    }

    public void Confirm()
    {
        if (pending == null) return;
        var student = pending;
        pending = null;
        Hide();
        OnIdentified?.Invoke(student);
    }

    public void Back() => ShowCodeEntry(clearInput: false);

    /// <summary>Leaves code entry without identifying anyone (lobby "Back to Menu").</summary>
    public void Cancel()
    {
        if (busy) return;
        pending = null;
        Hide();
        OnCancelled?.Invoke();
    }

    /// <summary>What the confirmation screen currently shows - for tests and diagnostics.</summary>
    public string ConfirmationTitle => confirmTitleLabel != null ? confirmTitleLabel.text : null;
    public string ConfirmationDetail => confirmDetailLabel != null ? confirmDetailLabel.text : null;
    public string ErrorText => errorLabel != null && errorLabel.gameObject.activeSelf ? errorLabel.text : null;
    public bool IsConfirming => pending != null;
    public IdentifiedStudent PendingStudent => pending;

    private void ShowConfirmation(IdentifiedStudent student)
    {
        pending = student;
        if (codeEntryGroup != null) codeEntryGroup.SetActive(false);
        if (confirmationGroup != null) confirmationGroup.SetActive(true);

        if (student.IsTestSession)
        {
            if (confirmTitleLabel != null) confirmTitleLabel.text = testModeTitle;
            if (confirmDetailLabel != null) confirmDetailLabel.text = string.Format(testModeDetailFormat, student.DisplayName);
            if (confirmQuestionLabel != null) confirmQuestionLabel.gameObject.SetActive(false);
            if (backButtonLabel != null) backButtonLabel.text = backLabel;
        }
        else
        {
            if (confirmTitleLabel != null) confirmTitleLabel.text = student.DisplayName;
            if (confirmDetailLabel != null) confirmDetailLabel.text = student.ClassDisplay;
            if (confirmQuestionLabel != null)
            {
                confirmQuestionLabel.gameObject.SetActive(true);
                confirmQuestionLabel.text = confirmQuestion;
            }
            if (backButtonLabel != null) backButtonLabel.text = changeCodeLabel;
        }
        if (confirmationTable != null)
        {
            LobbyTableLayout.Clear(confirmationTable);
            LobbyTableLayout.Row(confirmationTable, student.IsTestSession ? "Mode" : "Name", confirmTitleLabel.text, confirmTitleLabel.font);
            LobbyTableLayout.Row(confirmationTable, student.IsTestSession ? "Account" : "Class", confirmDetailLabel.text, confirmDetailLabel.font);
            confirmTitleLabel.alpha = confirmDetailLabel.alpha = 0;
        }
        if (confirmButtonLabel != null) confirmButtonLabel.text = continueLabel;
    }

    private void HandleContinueClicked() => SubmitCode(codeInputField != null ? codeInputField.text : string.Empty);
    private void HandleNoCodeClicked() => ChooseContinueWithoutCode();
    private void HandleConfirmClicked() => Confirm();
    private void HandleBackClicked() => Back();

    private void SetInteractable(bool interactable)
    {
        if (continueButton != null) continueButton.interactable = interactable;
        if (continueWithoutCodeButton != null) continueWithoutCodeButton.interactable = interactable;
        if (codeInputField != null) codeInputField.interactable = interactable;
        if (cancelButton != null) cancelButton.interactable = interactable;
    }

    private void SetError(string message)
    {
        if (errorLabel == null) return;
        errorLabel.text = message ?? string.Empty;
        errorLabel.gameObject.SetActive(!string.IsNullOrEmpty(message));
    }

    private void SetStatus(string message)
    {
        if (statusLabel == null) return;
        statusLabel.text = message ?? string.Empty;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (panelRoot == null || codeInputField == null || continueButton == null || continueWithoutCodeButton == null
            || confirmationGroup == null || confirmTitleLabel == null || confirmButton == null || backButton == null)
        {
            Debug.LogWarning($"[{name}] StudentIdentificationPanel has unassigned UI references.", this);
        }
    }
#endif
}
