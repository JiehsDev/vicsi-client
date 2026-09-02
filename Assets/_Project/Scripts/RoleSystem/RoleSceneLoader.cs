// Assets/_Project/Scripts/RoleSystem/RoleSceneLoader.cs
using UnityEngine;

/// <summary>
/// Runs once when CSI_Environment loads and fires the scene-entered STCS beat, plus
/// the matching SceneEntered session-log event. Named for its historical job -
/// loading a separate scene per role (Role_Photographer / Role_IOC) and equipping
/// the player's selected starting role - both removed along with RoleConfig now that
/// the unified design has one investigator with every tool on the wheel and no
/// pre-scene role selection. The class/file name is kept as-is to avoid re-wiring
/// the CSI_Environment component; don't bolt unrelated Phase 1 gating (PPE, sign-in,
/// tape-crossing) onto this class under this name later - give that its own script.
/// </summary>
public class RoleSceneLoader : MonoBehaviour
{
    private void Start()
    {
        STCSManager.Instance?.Fire("scene_entered");
        SessionLogger.Instance?.LogEvent(SessionEventType.SceneEntered);
    }
}