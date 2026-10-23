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
public static partial class PlayerUIGate
{
    private static readonly HashSet<object> blockers = new();

    public static bool IsBlocked => blockers.Count > 0;

    public static void Enter(object token)
    {
        if (token == null || !blockers.Add(token)) return;
        activatedFrames[token] = UnityEngine.Time.frameCount;
        ProtectHeldConfirm();
    }
    public static void Exit(object token)
    {
        if (token == null || !blockers.Remove(token)) return;
        activatedFrames.Remove(token);
        ProtectHeldConfirm();
    }

    /// <summary>
    /// True if something OTHER than <paramref name="token"/> currently holds the gate -
    /// the same question IsBlocked answers, except a lock the caller itself already
    /// holds doesn't count. For a panel that legitimately holds the gate for as long as
    /// it's open (e.g. EvidenceInventoryUI, which stays open across multiple actions
    /// rather than closing on each one the way ToolWheelController does), plain
    /// IsBlocked always reads true for its own gated actions once it has entered -
    /// indistinguishable from a genuinely different screen being up. This lets such a
    /// panel still refuse when something ELSE is blocking, without ever blocking
    /// itself.
    /// </summary>
    public static bool IsBlockedByOther(object token)
    {
        foreach (var blocker in blockers)
        {
            if (!Equals(blocker, token))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Diagnostic-only surface for the dev-only in-headset overlay (see DevDiagnosticOverlay) - current lock count.</summary>
    public static int LockCount => blockers.Count;

    /// <summary>
    /// Diagnostic-only surface for the dev-only in-headset overlay - one descriptive
    /// name per current lock, so a stuck gate can be attributed to the component that
    /// holds it. A plain (non-MonoBehaviour) token, e.g. PreSessionFlowController's own
    /// private uiGateToken object, has no useful ToString(), so it reports its declaring
    /// type via reflection when available and falls back to the runtime type name
    /// otherwise - either way this never throws and never blocks Enter/Exit.
    /// </summary>
    public static IEnumerable<string> LockOwnerNames
    {
        get
        {
            foreach (var token in blockers)
            {
                if (token is UnityEngine.Object unityObject && unityObject != null)
                {
                    yield return unityObject.GetType().Name;
                }
                else
                {
                    yield return token?.GetType().Name ?? "null";
                }
            }
        }
    }
}
