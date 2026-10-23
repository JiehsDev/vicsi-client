// Assets/_Project/Scripts/CaseFile/EvidenceCustodyLocation.cs

/// <summary>
/// WHERE a piece of evidence physically is right now, tracked completely independently
/// of EvidenceStatus (WHAT procedural step it has reached - see that enum's own class
/// comment). The two axes answer different questions and must never be conflated: an
/// item can be Sealed and sitting in Inventory, Sealed and Held, Sealed and already in
/// the EvidenceBox, or even Processed while still sitting in Inventory (fingerprinting
/// a sealed container doesn't require the physical bag to have reached the box first -
/// see EvidenceStateManager.CheckProcessed, which has never depended on box placement).
///
/// A fresh EvidenceRecord starts at Scene (the physical prop is out in the world) and
/// only ever moves forward through this enum via EvidenceInventory - nothing else
/// should assign EvidenceRecord.custodyLocation directly.
///
/// Explicit values, matching EvidenceStatus/ToolType/FingerprintLabStatus's own
/// documented discipline in this project: nothing here is persisted as a raw int today
/// (SessionLogger persists custody transitions as ToString() payload strings, the same
/// safe convention SessionEventType itself uses), but keeping the values explicit costs
/// nothing and avoids ever having to relearn this rule the hard way if that changes.
/// </summary>
public enum EvidenceCustodyLocation
{
    /// <summary>The physical prop is still out in the scene, un-admitted to inventory. Every item starts here.</summary>
    Scene = 0,

    /// <summary>Sealed and admitted to EvidenceInventory, but not currently in the player's hand.</summary>
    Inventory = 1,

    /// <summary>Equipped from inventory and currently in the player's hand.</summary>
    Held = 2,

    /// <summary>Deposited at an EvidenceBoxDepositZone. Terminal - see EvidenceInventory.Deposit.</summary>
    EvidenceBox = 3
}
