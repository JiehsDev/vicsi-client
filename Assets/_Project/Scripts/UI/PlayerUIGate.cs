// Assets/_Project/Scripts/UI/PlayerUIGate.cs
using System.Collections.Generic;

/// <summary>
/// Scene-wide "is some exclusive UI/interaction currently up" flag, so
/// unrelated systems - the utility menu, the tool wheel, a world-space
/// interaction prompt, anything opened later - can check IsBlocked before
/// opening instead of silently stacking on top of each other. This is what
/// the camera viewfinder (PhotographTool), UtilityMenuController, and
/// ToolWheelController all currently share the left controller's X/Y
/// buttons around, without knowing about each other directly - e.g. holding
/// X to aim the camera would also crack open the utility menu underneath it.
///
/// Each caller registers itself with a token (normally "this") while its
/// screen/interaction is active and unregisters when it closes. A set
/// rather than a raw counter, so an unbalanced or duplicate Enter()/Exit()
/// call (e.g. a stale reference surviving a scene reload, or a defensive
/// Exit() in OnDisable firing after the normal close path already did) can't
/// corrupt another blocker's state - removing a token that was never added,
/// or was already removed, is always safe.
/// </summary>
public static class PlayerUIGate
{
    private static readonly HashSet<object> blockers = new();

    public static bool IsBlocked => blockers.Count > 0;

    public static void Enter(object token) => blockers.Add(token);
    public static void Exit(object token) => blockers.Remove(token);
}
