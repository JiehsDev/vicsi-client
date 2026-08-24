// Assets/_Project/Scripts/UI/UIManager.cs
using UnityEngine;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject roleSelectPanel;

    [Header("Environment scene to load after role selection")]
    [SerializeField] private string environmentSceneName = "CSI_Environment";

    private void Start()
    {
        mainMenuPanel.SetActive(true);
        roleSelectPanel.SetActive(false);
    }

    public void OnStartPressed()
    {
        mainMenuPanel.SetActive(false);
        roleSelectPanel.SetActive(true);
    }

    public void SelectPhotographerRole()
    {
        RoleConfig.SetRole(RoleId.Photographer);
        SceneManager.LoadScene(environmentSceneName);
    }

    public void SelectIOCRole()
    {
        RoleConfig.SetRole(RoleId.IOC);
        SceneManager.LoadScene(environmentSceneName);
    }
}