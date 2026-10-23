// Assets/_Project/Scripts/Assessment/PairingCodeIdentityPanel.cs
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The student-facing pairing screen: type the short-lived code shown on vicsi-dashboard,
/// Connect exchanges it via PairingIdentityService, Cancel dismisses without pairing. On a
/// successful exchange the entry group hides and a separate confirmation group appears
/// ("Connected as <student>, <class>"); Continue there is what actually dismisses the
/// panel and lets play proceed - a deliberate two-step reveal so a student always sees who
/// they connected as before starting, rather than the exchange silently vanishing the panel.
///
/// Deliberately SCENE-AUTHORED / PREFAB-BASED, not runtime-generated - every visual
/// element below is a required [SerializeField] reference to a real GameObject placed
/// in the scene or prefab, unlike CompletionScreenUI/EndingManager's own runtime-canvas
/// fallback recipe (acceptable there for an internal dev screen; not acceptable here per
/// this feature's own explicit instruction for the identity screen specifically).
///
/// Every piece of copy and every timing value is Inspector-editable - no string or
/// number below is hardcoded in a way that would require a C# change to retune.
/// </summary>
public class PairingCodeIdentityPanel : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private AssessmentUploadConfiguration configuration;

    [Header("Pairing code")]
    [Tooltip("Expected length of the pairing code students type - purely a UX hint (max input length, spacing); the server is always the actual authority on whether a code is valid.")]
    [Min(1)]
    [SerializeField] private int pairingCodeLength = 6;

    [Tooltip("Shown specifically when the server reports PAIRING_CODE_EXPIRED, instead of the server's own message - lets this screen's wording be retuned independently.")]
    [SerializeField] private string codeExpirationMessage = "That code has expired. Ask your instructor to generate a new one from the dashboard.";

    [Tooltip("Shown specifically when the server reports PAIRING_CODE_INVALID (wrong/unrecognized code).")]
    [SerializeField] private string codeInvalidMessage = "That code isn't recognized. Check it and try again.";

    [Header("Retry policy (this screen only - independent of AssessmentUploadConfiguration's own upload retry policy)")]
    [Tooltip("Seconds to wait for one exchange attempt before treating it as a timeout - passed through to PairingIdentityService via configuration.requestTimeoutSeconds; shown here only as a read-only reference for whoever is tuning this panel.")]
    [SerializeField] private float retryTimeoutSeconds = 20f;

    [Tooltip("Maximum consecutive failed exchange attempts before Connect is disabled and the student is told to ask an instructor for help.")]
    [Min(1)]
    [SerializeField] private int maxAttempts = 5;

    [Header("Copy")]
    [SerializeField, TextArea] private string instructionsText = "Enter the pairing code shown on the dashboard to start your assessment session.";
    [SerializeField] private string connectingMessage = "Connecting...";
    [SerializeField] private string genericErrorMessage = "Something went wrong. Try again.";
    [SerializeField] private string tooManyAttemptsMessage = "Too many failed attempts. Please ask your instructor for help.";
    [SerializeField] private string confirmationStudentFormat = "Connected as {0}";
    [SerializeField] private string confirmationClassFormat = "{0}";

    [Header("Scene-authored UI - code entry (required, not runtime-generated)")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private GameObject codeEntryGroup;
    [SerializeField] private TMP_Text instructionsLabel;
    [SerializeField] private TMP_InputField codeInputField;
    [SerializeField] private TMP_Text errorLabel;
    [SerializeField] private TMP_Text statusLabel;
    [SerializeField] private Button connectButton;
    [SerializeField] private Button cancelButton;

    [Header("Scene-authored UI - connected confirmation (required, not runtime-generated)")]
    [SerializeField] private GameObject confirmationGroup;
    [SerializeField] private TMP_Text confirmedStudentLabel;
    [SerializeField] private TMP_Text confirmedClassLabel;
    [SerializeField] private Button confirmationContinueButton;

    public event Action OnPairingCompleted;
    public event Action OnCancelled;

    /// <summary>Read-only diagnostic surface for the dev-only in-headset overlay (see DevDiagnosticOverlay) - mirrors panelRoot's active state without exposing the field itself.</summary>
    public bool IsPanelActive => panelRoot != null && panelRoot.activeSelf;
    /// <summary>Read-only diagnostic surface for the dev-only in-headset overlay - panelRoot IS the PairingUICanvas GameObject; exposed separately so the overlay can check activeInHierarchy (catches a disabled ancestor, which activeSelf alone would miss) distinctly from this component's own tracked state.</summary>
    public GameObject PanelRootObject => panelRoot;

    private int failedAttempts;
    private CancellationTokenSource requestCts;
    private bool isSubmitting;

    private void Awake()
    {
        if (panelRoot == null || codeEntryGroup == null || instructionsLabel == null || codeInputField == null
            || errorLabel == null || connectButton == null || cancelButton == null
            || confirmationGroup == null || confirmedStudentLabel == null || confirmedClassLabel == null || confirmationContinueButton == null)
        {
            Debug.LogError($"[{nameof(PairingCodeIdentityPanel)}] One or more required scene-authored UI references are unassigned on {name}. This panel is scene-authored by design and has no runtime fallback - assign every field in the Inspector.", this);
        }

        if (codeInputField != null)
        {
            codeInputField.characterLimit = pairingCodeLength;
        }
    }

    private void OnEnable()
    {
        if (connectButton != null) connectButton.onClick.AddListener(HandleConnectClicked);
        if (cancelButton != null) cancelButton.onClick.AddListener(HandleCancelClicked);
        if (confirmationContinueButton != null) confirmationContinueButton.onClick.AddListener(HandleConfirmationContinueClicked);

        failedAttempts = 0;
        SetError(string.Empty);
        SetStatus(string.Empty);
        if (instructionsLabel != null) instructionsLabel.text = instructionsText;
        if (codeInputField != null) codeInputField.text = string.Empty;
        SetInteractable(true);
        ShowCodeEntry();
    }

    private void OnDisable()
    {
        if (connectButton != null) connectButton.onClick.RemoveListener(HandleConnectClicked);
        if (cancelButton != null) cancelButton.onClick.RemoveListener(HandleCancelClicked);
        if (confirmationContinueButton != null) confirmationContinueButton.onClick.RemoveListener(HandleConfirmationContinueClicked);
        requestCts?.Cancel();
    }

    public void Show()
    {
        if (panelRoot != null) { HudFollowCamera.ConfigureShared(panelRoot, this, 600); panelRoot.SetActive(true); }
    }

    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void ShowCodeEntry()
    {
        if (codeEntryGroup != null) codeEntryGroup.SetActive(true);
        if (confirmationGroup != null) confirmationGroup.SetActive(false);
    }

    private void ShowConfirmation()
    {
        if (codeEntryGroup != null) codeEntryGroup.SetActive(false);
        if (confirmationGroup != null) confirmationGroup.SetActive(true);

        var identity = PairingIdentityService.Instance;
        if (confirmedStudentLabel != null)
        {
            confirmedStudentLabel.text = string.Format(confirmationStudentFormat, identity?.StudentDisplayName ?? string.Empty);
        }
        if (confirmedClassLabel != null)
        {
            confirmedClassLabel.text = string.Format(confirmationClassFormat, identity?.ClassDisplayName ?? string.Empty);
        }
    }

    private void HandleCancelClicked()
    {
        requestCts?.Cancel();
        Hide();
        OnCancelled?.Invoke();
    }

    private void HandleConfirmationContinueClicked()
    {
        Hide();
        OnPairingCompleted?.Invoke();
    }

    private async void HandleConnectClicked()
    {
        if (isSubmitting || PairingIdentityService.Instance == null)
        {
            return;
        }

        string code = codeInputField != null ? codeInputField.text : string.Empty;
        if (string.IsNullOrWhiteSpace(code))
        {
            SetError("Enter the code shown on the dashboard.");
            return;
        }

        isSubmitting = true;
        SetInteractable(false);
        SetError(string.Empty);
        SetStatus(connectingMessage);

        requestCts = new CancellationTokenSource();
        var result = await PairingIdentityService.Instance.ExchangePairingCodeAsync(code, requestCts.Token);

        isSubmitting = false;
        SetStatus(string.Empty);

        if (result.Success)
        {
            ShowConfirmation();
            return;
        }

        failedAttempts++;

        if (failedAttempts >= maxAttempts)
        {
            SetError(tooManyAttemptsMessage);
            SetInteractable(false);
            return;
        }

        SetError(ResolveErrorMessage(result));
        SetInteractable(true);
    }

    /// <summary>Prefers this panel's own Inspector-configured copy for the two cases that most benefit from local wording control (expired/invalid code); falls back to the server's own safe message, then a fully generic one.</summary>
    private string ResolveErrorMessage(PairingIdentityService.PairingExchangeResult result)
    {
        switch (result.ErrorCode)
        {
            case "PAIRING_CODE_EXPIRED":
                return !string.IsNullOrEmpty(codeExpirationMessage) ? codeExpirationMessage : result.ErrorMessage;
            case "PAIRING_CODE_INVALID":
                return !string.IsNullOrEmpty(codeInvalidMessage) ? codeInvalidMessage : result.ErrorMessage;
            default:
                return !string.IsNullOrEmpty(result.ErrorMessage) ? result.ErrorMessage : genericErrorMessage;
        }
    }

    private void SetInteractable(bool interactable)
    {
        if (connectButton != null) connectButton.interactable = interactable;
        if (codeInputField != null) codeInputField.interactable = interactable;
    }

    private void SetError(string message)
    {
        if (errorLabel != null) errorLabel.text = message ?? string.Empty;
    }

    private void SetStatus(string message)
    {
        if (statusLabel != null) statusLabel.text = message ?? string.Empty;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (configuration != null && codeInputField != null)
        {
            codeInputField.characterLimit = pairingCodeLength;
        }
    }
#endif
}
