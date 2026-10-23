// Assets/_Project/Scripts/Assessment/StudentIdentificationHandoff.cs
using UnityEngine;

/// <summary>
/// Carries the student confirmed in the lobby (Main.unity, LobbyIdentificationController)
/// into the scenario scene, where PreSessionFlowController binds it as the owner of the
/// scenario's session. Identification happens in the lobby; ownership is still bound to the
/// session that actually gets played, exactly as before.
///
/// In memory only, consumed once: never persisted, and carries only the stable student id
/// plus display data (no code, no token). A scenario scene opened without going through the
/// lobby (Editor play, automated tests) finds nothing here; LobbyVisited tells it whether the
/// lobby ran at all this app run (see PreSessionFlowController's direct-run John Doe path).
/// </summary>
public static class StudentIdentificationHandoff
{
    private static IdentifiedStudent pending;

    public static bool HasPending => pending != null;

    /// <summary>True once the lobby (Main.unity) has run in this app run. False means the scenario scene was started directly (Editor play).</summary>
    public static bool LobbyVisited { get; private set; }

    /// <summary>Lobby: called on load so a scenario loaded afterwards knows it came through the lobby.</summary>
    public static void MarkLobbyVisited() => LobbyVisited = true;

    /// <summary>Lobby: the student confirmed who they are; the scenario is about to load.</summary>
    public static void Set(IdentifiedStudent student) => pending = student;

    /// <summary>Lobby: the student backed out before starting.</summary>
    public static void Clear() => pending = null;

    /// <summary>Scenario: takes the lobby's student, once.</summary>
    public static bool TryTake(out IdentifiedStudent student)
    {
        student = pending;
        pending = null;
        return student != null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        pending = null;
        LobbyVisited = false;
    }
}
