// Assets/_Project/Scripts/Assessment/LobbyIdentificationController.cs
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Lobby (Main.unity) student identification: "Start Investigation" swaps the start menu for
/// the student-code screen (code / Continue Without Code -> "Is this you?"). Only once the
/// student confirms does the scenario load; the confirmed student travels with
/// StudentIdentificationHandoff and PreSessionFlowController binds them as the owner of the
/// scenario's session the moment it starts - so the player never meets the code screen
/// inside the crime scene.
///
/// Same flow, validator, directory cache and device key as the in-scene panel
/// (PreSessionFlowController): online check first, offline cached lookup when the headset is
/// offline, Continue Without Code -> the existing test student. Backing out returns to the
/// menu and clears any pending identity.
/// </summary>
public class LobbyIdentificationController : MonoBehaviour
{
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private StudentIdentificationPanel identificationPanel;
    [Tooltip("Same configuration asset as the upload services (online code validation).")]
    [SerializeField] private AssessmentUploadConfiguration configuration;
    [SerializeField] private string environmentSceneName = "CSI_Environment";

    private bool loading;

    private void Awake()
    {
        StudentIdentificationHandoff.MarkLobbyVisited();
        if (identificationPanel == null) return;
        var config = configuration;
        identificationPanel.SetFlow(new StudentIdentificationFlow(
            new UnityWebRequestVrCodeValidator(
                () => config,
                () => StudentDirectoryService.Instance != null ? StudentDirectoryService.Instance.DeviceKey : VrDeviceKey.Resolve()),
            () => StudentDirectoryService.Instance != null ? StudentDirectoryService.Instance.Cache : null,
            () => config != null ? config.vrCodeValidationTimeoutSeconds : 5f));
        identificationPanel.OnIdentified += HandleIdentified;
        identificationPanel.OnCancelled += HandleCancelled;
        identificationPanel.Hide();
    }

    private void OnDestroy()
    {
        if (identificationPanel == null) return;
        identificationPanel.OnIdentified -= HandleIdentified;
        identificationPanel.OnCancelled -= HandleCancelled;
    }

    /// <summary>Button: Start Investigation.</summary>
    public void BeginIdentification()
    {
        if (loading) return;
        StudentIdentificationHandoff.Clear();
        if (identificationPanel == null)
        {
            // No lobby panel wired: fall back to identifying inside the scenario, as before.
            LoadScenario();
            return;
        }
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        identificationPanel.Show();
    }

    private void HandleIdentified(IdentifiedStudent student)
    {
        StudentIdentificationHandoff.Set(student);
        LoadScenario();
    }

    private void HandleCancelled()
    {
        StudentIdentificationHandoff.Clear();
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
    }

    private void LoadScenario()
    {
        if (loading) return;
        loading = true;
        SceneManager.LoadScene(environmentSceneName);
    }
}
