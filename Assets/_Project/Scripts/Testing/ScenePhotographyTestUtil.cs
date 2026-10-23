// Assets/_Project/Scripts/Testing/ScenePhotographyTestUtil.cs
using System.Reflection;
using UnityEngine;

/// <summary>
/// Completes the scene photography route the way a player does - standing in each point's
/// zone, aiming at its target and releasing the shutter - by driving ScenePhotographyManager's
/// own shutter handler with the headset camera placed for each point. Evidence work is
/// gated on this route (stepRequirements: ScenePhotographyCompleted), so any test that
/// marks, chalks, swabs, fingerprints or collects evidence runs it first.
/// </summary>
public static class ScenePhotographyTestUtil
{
    /// <summary>Walks every incomplete point in order. Returns true when the route is complete.</summary>
    public static bool CompleteRoute()
    {
        var photos = ScenePhotographyManager.Instance;
        if (photos == null)
        {
            return false;
        }
        if (photos.IsScenePhotographyComplete)
        {
            return true;
        }

        // Scene photography starts at the First Responder's turnover; open the investigation
        // (test bypass) and activate the first point now rather than after its start delay.
        if (SceneTurnover.IsPending)
        {
            SceneTurnover.Controller.CompleteWithoutDialogue();
        }
        typeof(ScenePhotographyManager).GetMethod("ActivateFirstPoint", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(photos, null);

        var handleShutter = typeof(ScenePhotographyManager).GetMethod("HandleShutter", BindingFlags.NonPublic | BindingFlags.Instance);
        var tool = Object.FindFirstObjectByType<PhotographTool>(FindObjectsInactive.Include);
        var cam = Camera.main;
        if (handleShutter == null || tool == null || cam == null)
        {
            return false;
        }

        foreach (var point in photos.Points)
        {
            if (point.IsCompleted)
            {
                continue;
            }
            cam.transform.position = point.transform.position + Vector3.up * 1.6f;
            cam.transform.rotation = point.cameraTarget != null
                ? Quaternion.LookRotation(point.cameraTarget.position - cam.transform.position)
                : Quaternion.LookRotation(point.transform.forward);
            handleShutter.Invoke(photos, new object[] { tool, false });
        }
        return photos.IsScenePhotographyComplete;
    }
}
