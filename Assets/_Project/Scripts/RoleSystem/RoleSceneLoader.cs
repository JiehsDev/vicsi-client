// Assets/_Project/Scripts/RoleSystem/RoleSceneLoader.cs
using UnityEngine;
using UnityEngine.SceneManagement;

public class RoleSceneLoader : MonoBehaviour
{
    private const string PhotographerScene = "Role_Photographer";
    private const string IOCScene = "Role_IOC";

    private void Start()
    {
        string sceneToLoad = RoleConfig.SelectedRole switch
        {
            RoleId.Photographer => PhotographerScene,
            RoleId.IOC => IOCScene,
            _ => null
        };

        if (sceneToLoad == null)
        {
            Debug.LogWarning("[RoleSceneLoader] No role was selected before entering the environment scene.");
            return;
        }

        var loadOp = SceneManager.LoadSceneAsync(sceneToLoad, LoadSceneMode.Additive);
        loadOp.completed += _ => STCSManager.Instance?.Fire("scene_entered");
    }
}