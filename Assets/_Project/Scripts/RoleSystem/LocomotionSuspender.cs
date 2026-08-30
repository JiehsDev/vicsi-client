// Assets/_Project/Scripts/RoleSystem/LocomotionSuspender.cs
using System.Collections.Generic;
using Oculus.Interaction.Locomotion;
using UnityEngine;

/// <summary>
/// Shared "pause walking/turning while a menu is open" helper, used by
/// ToolWheelController, UtilityMenuController, and PhotoAlbumUI (and any
/// future full-screen UI) so none of them duplicate the same
/// find-and-disable-locomotion logic. Reference-counted: if two callers
/// suspend at once (e.g. the utility wheel opens the album and both briefly
/// want locomotion off), movement only resumes once every caller has called
/// Resume() - a stray extra Resume() call is also safe (clamped at zero).
/// </summary>
public static class LocomotionSuspender
{
    private static readonly List<GameObject> suspendedObjects = new();
    private static int suspendCount;

    public static void Suspend()
    {
        suspendCount++;
        if (suspendCount > 1)
        {
            // Already suspended by another caller - nothing new to do.
            return;
        }

        suspendedObjects.Clear();
        foreach (var turner in Object.FindObjectsByType<LocomotionAxisTurnerInteractor>(FindObjectsInactive.Exclude))
        {
            suspendedObjects.Add(turner.gameObject);
        }
        foreach (var slider in Object.FindObjectsByType<SlideLocomotionBroadcaster>(FindObjectsInactive.Exclude))
        {
            suspendedObjects.Add(slider.gameObject);
        }

        foreach (var go in suspendedObjects)
        {
            go.SetActive(false);
        }
    }

    public static void Resume()
    {
        if (suspendCount == 0)
        {
            return;
        }

        suspendCount--;
        if (suspendCount > 0)
        {
            // Still suspended by another caller.
            return;
        }

        foreach (var go in suspendedObjects)
        {
            if (go != null)
            {
                go.SetActive(true);
            }
        }
        suspendedObjects.Clear();
    }
}
