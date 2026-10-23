// Assets/_Project/Scripts/UI/UIManager.cs
using UnityEngine;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Environment scene to load on Start")]
    [SerializeField] private string environmentSceneName = "CSI_Environment";

    public void HideResearchPanels()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    private void Start()
    {
        if (CompetitiveEdition.Enabled) { HideResearchPanels(); return; }
        mainMenuPanel.SetActive(true);
        settingsPanel.SetActive(false);
    }

    // Which tool the player starts with is now chosen in-scene via the tool
    // wheel, not a pre-game role pick - Start just drops the player straight
    // into the environment.
    public void OnStartPressed()
    {
        if (CompetitiveEdition.Enabled) return;
        SceneManager.LoadScene(environmentSceneName);
    }

    public void OnSettingsPressed()
    {
        mainMenuPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    public void OnBackFromSettingsPressed()
    {
        settingsPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }
}