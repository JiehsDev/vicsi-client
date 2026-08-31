// Assets/_Project/Scripts/Interaction/EvidenceTentPickupCoordinator.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks every placed evidence tent's EvidenceTentPickup and picks the
/// single nearest one within its own pickup radius of a given head position -
/// the arbitration behind "the player has a pickup radius, and only one tent
/// inside it is ever the pickup candidate at a time", even when several
/// tents are placed close together. Each EvidenceTentPickup registers itself
/// here in OnEnable and unregisters in OnDisable instead of scripts having to
/// discover each other directly.
/// </summary>
public static class EvidenceTentPickupCoordinator
{
    private static readonly HashSet<EvidenceTentPickup> activeTents = new();

    public static void Register(EvidenceTentPickup tent) => activeTents.Add(tent);
    public static void Unregister(EvidenceTentPickup tent) => activeTents.Remove(tent);

    /// <summary>The single closest registered tent whose own pickup radius horizontally contains headPosition, or null if none qualify.</summary>
    public static EvidenceTentPickup FindNearestInRange(Vector3 headPosition)
    {
        EvidenceTentPickup nearest = null;
        float nearestSqrDistance = float.MaxValue;

        foreach (var tent in activeTents)
        {
            // Horizontal-only distance: tents are placed on the floor, so a
            // tent right at the player's feet still sits ~1.6-1.8m below a
            // standing head - that vertical gap alone shouldn't disqualify it
            // just because the player hasn't crouched down to eye-level with it.
            Vector3 offset = tent.transform.position - headPosition;
            offset.y = 0f;
            float sqrDistance = offset.sqrMagnitude;

            if (sqrDistance > tent.PickupRadiusSqr || sqrDistance >= nearestSqrDistance)
            {
                continue;
            }

            nearestSqrDistance = sqrDistance;
            nearest = tent;
        }

        return nearest;
    }
}
