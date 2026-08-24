// Assets/_Project/Scripts/RoleSystem/RoleSceneLoader.cs
using UnityEngine;

/// <summary>
/// Runs once when CSI_Environment loads. Used to load a separate scene per role
/// (Role_Photographer / Role_IOC); now every tool prop lives directly in this one
/// scene, so there's nothing to load. This just fires the scene-entered STCS beat
/// and, if the player picked a role with a matching PlayerTool, marks that tool as
/// the character's starting one. Roles without a physical tool (IOC, TeamLeader,
/// CaseAnalyst, ...) are skipped silently - that's an expected case, not an error.
/// </summary>
public class RoleSceneLoader : MonoBehaviour
{
    private void Start()
    {
        var startingRole = RoleConfig.SelectedRole;
        if (startingRole != RoleId.None && PlayerToolRegistry.GetTool(startingRole) != null)
        {
            PlayerToolRegistry.RequestEquip(startingRole);
        }

        STCSManager.Instance?.Fire("scene_entered");
    }
}