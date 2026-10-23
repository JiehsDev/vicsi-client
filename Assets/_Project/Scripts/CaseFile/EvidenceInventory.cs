// Assets/_Project/Scripts/CaseFile/EvidenceInventory.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Owns evidence CUSTODY (EvidenceCustodyLocation) and the evidence half of "what's in
/// the player's hand" - the sealed-item counterpart to PlayerToolRegistry, which owns
/// tools. Deliberately does NOT decide mutual exclusion between tools and evidence, or
/// whether an equip/unequip SHOULD happen right now - that ordering is
/// HeldItemCoordinator's job. This class only ever does what it's told (Add/EquipHeld/
/// UnequipHeld/Deposit) and is the single place that actually owns the data and the
/// held-in-hand visual once those calls happen.
///
/// Singleton MonoBehaviour, same Instance pattern as EvidenceStateManager/SessionLogger/
/// NotificationManager - not a static class, per this feature's own explicit direction
/// to prefer a scene-manager component over unnecessary static mutable state where
/// either would work.
/// </summary>
public class EvidenceInventory : MonoBehaviour
{
    public static EvidenceInventory Instance { get; private set; }

    [Tooltip("Maximum number of items this inventory can hold at once. 0 = unlimited (default) - unless a real design requirement caps it, this should stay unlimited.")]
    [Min(0)]
    [SerializeField] private int capacity = 0;

    private class Entry
    {
        public EvidenceDefinition definition;
        public GameObject spawnedHeldVisual;
        public string sampleSourceId;
        public string sampleType = "Blood";
        public EvidenceCustodyLocation sampleCustody;
        public bool sampleStored;
        public bool bagged;
        public bool bagStored;
        public string depositedBoxId;
    }

    private readonly Dictionary<string, Entry> entries = new();

    /// <summary>evidenceId of whatever is currently spawned in the hand, or null if nothing is held from inventory.</summary>
    public string CurrentHeldEvidenceId { get; private set; }

    /// <summary>Fired after any Add/EquipHeld/UnequipHeld/Deposit call that actually changed something - e.g. for EvidenceInventoryUI to refresh its list.</summary>
    public event Action OnInventoryChanged;

    /// <summary>Fired alongside OnInventoryChanged with the specific (evidenceId, previous, new) custody transition - for listeners that care which item moved, not just that something did.</summary>
    public event Action<string, EvidenceCustodyLocation, EvidenceCustodyLocation> OnCustodyChanged;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    internal void PrepareDevelopmentHeldFingerprint(string sourceId, Transform hand)
    {
        SceneTestStart.RequirePreparation();
        var state = EvidenceStateManager.Instance;
        foreach (var record in state.AllRecords)
            if (record.definition.requiresFingerprinting && record.definition.evidenceId != sourceId) state.MarkMatched(record.definition.evidenceId);
        string id = FingerprintSampleId(sourceId);
        entries[id].sampleStored = false;
        if (!EquipHeld(id, hand)) throw new InvalidOperationException("Could not hold the comparison card.");
    }
    internal void PrepareDevelopmentStart(GameObject samplePrefab, GameObject fingerprintPrefab = null)
    {
        SceneTestStart.RequirePreparation();
        foreach (var record in EvidenceStateManager.Instance.AllRecords)
        {
            var def = record.definition;
            if (def.examinedInPlace) continue;
            if (def.requiresSwabbing)
            {
                string id = RegisterContainedSample(def.evidenceId, samplePrefab);
                if (id == null) throw new InvalidOperationException("Could not seed sample: " + def.evidenceId);
                entries[id].sampleStored = true;
                entries[id].sampleCustody = EvidenceCustodyLocation.Inventory;
            }
            if (!def.collectedBySwab)
                entries.Add(def.evidenceId, new Entry { definition = def, bagged = true, bagStored = true });
            if (def.requiresFingerprinting && fingerprintPrefab != null)
            {
                string printId = FingerprintSampleId(def.evidenceId);
                var display = Instantiate(def); display.evidenceId = printId;
                display.displayName = "Fingerprint Sample – " + def.PlayerDisplayName;
                display.heldPrefab = fingerprintPrefab;
                entries.Add(printId,new Entry { definition=display,sampleSourceId=def.evidenceId,sampleType="Fingerprint",
                    sampleStored=true,sampleCustody=EvidenceCustodyLocation.Inventory });
            }
            var previous = record.custodyLocation;
            record.custodyLocation = EvidenceCustodyLocation.Inventory;
            // Keep original prop identities/anchors active in the measurement fixture.
            // The same tape targeting/late-measurement rules still apply.
            OnCustodyChanged?.Invoke(def.evidenceId, previous, record.custodyLocation);
        }
        OnInventoryChanged?.Invoke();
    }
#endif

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        foreach (var entry in entries.Values)
            if (entry.sampleSourceId != null) Destroy(entry.definition);
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public bool IsInInventory(string evidenceId) => !string.IsNullOrEmpty(evidenceId) && entries.TryGetValue(evidenceId, out var entry)
        && GetCustody(evidenceId) != EvidenceCustodyLocation.EvidenceBox
        && (entry.sampleSourceId == null || entry.sampleStored) && (!entry.bagged || entry.bagStored);
    // Existing custody metadata survives final deposit, without becoming a second inventory.
    public bool IsExplicitlyStoredSample(string id) => id != null && entries.TryGetValue(id, out var entry) && entry.sampleStored;
    public bool IsExplicitlyStoredBag(string id) => id != null && entries.TryGetValue(id, out var entry) && entry.bagStored;
    public bool IsSecuredCustody(string id) => GetCustody(id) == EvidenceCustodyLocation.Inventory || GetCustody(id) == EvidenceCustodyLocation.EvidenceBox;
    private int InventoryCount => System.Linq.Enumerable.Count(entries.Keys, id => GetCustody(id) != EvidenceCustodyLocation.EvidenceBox);

    public string GetSampleSource(string itemId) => itemId != null && entries.TryGetValue(itemId, out var entry) ? entry.sampleSourceId : null;
    public string GetSampleType(string itemId) => itemId != null && entries.TryGetValue(itemId, out var entry)
        && entry.sampleSourceId != null ? entry.sampleType : null;
    public bool IsFingerprintSample(string itemId) => GetSampleType(itemId) == "Fingerprint";
    public static string FingerprintSampleId(string sourceId) => "fingerprint-sample:" + sourceId;
    public bool IsSampleSealed(string itemId) => GetSampleSource(itemId) != null
        && (IsFingerprintSample(itemId)
            ? EvidenceStateManager.Instance?.GetRecord(GetSampleSource(itemId))?.fingerprintLifted == true
            : EvidenceStateManager.Instance?.GetRecord(GetSampleSource(itemId))?.swabSampleSealed == true);
    public bool RequiresExplicitSampleStorage => CurrentHeldEvidenceId != null && entries.TryGetValue(CurrentHeldEvidenceId, out var entry)
        && entry.sampleSourceId != null && !entry.sampleStored;
    public bool IsBaggedEvidence(string itemId) => itemId != null && entries.TryGetValue(itemId, out var entry) && entry.bagged;
    public bool RequiresExplicitBagStorage => CurrentHeldEvidenceId != null && entries.TryGetValue(CurrentHeldEvidenceId, out var entry)
        && entry.bagged && !entry.bagStored;
    public bool RequiresExplicitStorage => RequiresExplicitSampleStorage || RequiresExplicitBagStorage;
    public string PendingFingerprintItemId
    {
        get
        {
            foreach (var pair in entries)
                if (pair.Value.sampleType == "Fingerprint" && !pair.Value.sampleStored
                    && EvidenceStateManager.Instance?.GetRecord(pair.Value.sampleSourceId)?.fingerprintLifted == true) return pair.Key;
            return null;
        }
    }
    public GameObject HeldVisual => CurrentHeldEvidenceId != null && entries.TryGetValue(CurrentHeldEvidenceId, out var entry) ? entry.spawnedHeldVisual : null;
    public bool CanAcceptSample(string sourceId)
    {
        if (!ProcedureInterlock.AllowsHandling(sourceId)) return false;
        var source = EvidenceStateManager.Instance?.GetRecord(sourceId);
        if (source?.definition == null || RequiresExplicitSampleStorage || (capacity > 0 && InventoryCount >= capacity)) return false;
        return !entries.ContainsKey(source.definition.collectedBySwab ? sourceId : "blood-sample:" + sourceId);
    }

    // The same inventory owns derived trace containers. No EvidenceRecord is invented for a knife sample.
    // A collectedBySwab source retains its existing inventory ID, avoiding a second spatter sample.
    public string RegisterContainedSample(string sourceId, GameObject prefab)
    {
        var source = EvidenceStateManager.Instance?.GetRecord(sourceId);
        if (source?.definition == null || !source.swabbingDone || prefab == null || !CanAcceptSample(sourceId)) return null;
        string itemId = source.definition.collectedBySwab ? sourceId : "blood-sample:" + sourceId;
        if (entries.ContainsKey(itemId)) return null;
        var display = Instantiate(source.definition);
        display.displayName = "Blood Sample – " + source.definition.PlayerDisplayName;
        display.heldPrefab = prefab;
        display.heldLocalPosition = new Vector3(-.05f, .02f, .08f);
        display.heldLocalEulerAngles = Vector3.zero;
        display.heldLocalScale = Vector3.one * 1.5f;
        entries.Add(itemId, new Entry { definition = display, sampleSourceId = sourceId, sampleCustody = EvidenceCustodyLocation.Inventory });
        return itemId;
    }

    public bool CanAcceptFingerprintSample(string sourceId) => EvidenceStateManager.Instance?.CanLiftFingerprint(sourceId) == true
        && !RequiresExplicitStorage && !entries.ContainsKey(FingerprintSampleId(sourceId))
        && (capacity <= 0 || InventoryCount < capacity);

    // A derived inventory entry, never another roster EvidenceRecord or the knife ID.
    public string RegisterLiftedFingerprint(string sourceId, GameObject prefab)
    {
        if (prefab == null || !CanAcceptFingerprintSample(sourceId)) return null;
        var source = EvidenceStateManager.Instance.GetRecord(sourceId);
        string itemId = FingerprintSampleId(sourceId);
        var display = Instantiate(source.definition);
        display.evidenceId = itemId;
        display.displayName = "Fingerprint Sample – " + source.definition.PlayerDisplayName;
        display.heldPrefab = prefab;
        display.heldLocalPosition = new Vector3(-.043f, .035f, .045f);
        display.heldLocalEulerAngles = Vector3.zero;
        display.heldLocalScale = Vector3.one;
        entries.Add(itemId, new Entry { definition = display, sampleSourceId = sourceId,
            sampleType = "Fingerprint", sampleCustody = EvidenceCustodyLocation.Scene });
        return itemId;
    }

    internal void CancelUnheldFingerprint(string itemId)
    {
        if (itemId == CurrentHeldEvidenceId || !IsFingerprintSample(itemId) || entries[itemId].sampleStored) return;
        Destroy(entries[itemId].definition);
        entries.Remove(itemId);
    }

    public bool IsHeld(string evidenceId) => !string.IsNullOrEmpty(evidenceId) && evidenceId == CurrentHeldEvidenceId;

    public bool IsDeposited(string evidenceId) => GetCustody(evidenceId) == EvidenceCustodyLocation.EvidenceBox;

    public EvidenceCustodyLocation GetCustody(string evidenceId)
    {
        if (evidenceId != null && entries.TryGetValue(evidenceId, out var entry) && entry.sampleSourceId != null) return entry.sampleCustody;
        var record = EvidenceStateManager.Instance?.GetRecord(evidenceId);
        return record?.custodyLocation ?? EvidenceCustodyLocation.Scene;
    }

    /// <summary>Selectable inventory/held entries. Final deposited custody metadata stays tracked but is excluded from this list.</summary>
    public IEnumerable<string> AllEntryIds => System.Linq.Enumerable.Where(entries.Keys, id => GetCustody(id) != EvidenceCustodyLocation.EvidenceBox);
    public IReadOnlyList<string> AllTrackedEntryIds => entries.Keys.OrderBy(id=>id,StringComparer.Ordinal).ToList().AsReadOnly();
    public string GetDepositedBoxId(string id) => entries.TryGetValue(id,out var entry) ? entry.depositedBoxId : null;
    public bool AllCustodyDeposited => entries.Count>0 && CurrentHeldEvidenceId==null && entries.Keys.All(IsDeposited);

    public string CustodyTransferBlockReason(IReadOnlyList<string> expectedIds)
    {
        if (ProcedureInterlock.BlocksUnrelatedActions) return ProcedureInterlock.Reason;
        if (CurrentHeldEvidenceId!=null || RequiresExplicitStorage) return "Store the item in your hand before the custody pass.";
        var ids=AllEntryIds.OrderBy(id=>id,StringComparer.Ordinal).ToList();
        if(ids.Count==0) return "There is no stored evidence to transfer.";
        if(expectedIds==null || expectedIds.Count!=ids.Count || !new HashSet<string>(ids).SetEquals(expectedIds))
            return "Inventory changed. Review the updated list before confirming.";
        foreach(var id in ids)
        {
            var entry=entries[id]; var source=EvidenceStateManager.Instance?.GetRecord(entry.sampleSourceId ?? id);
            if(source?.definition==null || source.definition.examinedInPlace || GetCustody(id)!=EvidenceCustodyLocation.Inventory)
                return "Secure every evidence item in inventory before the custody pass.";
            if(entry.sampleSourceId!=null)
            {
                if(IsFingerprintSample(id) && !EvidenceStateManager.Instance.IsFingerprintMatched(entry.sampleSourceId))
                    return "Confirm the fingerprint comparison for " + entry.definition.PlayerDisplayName + " before the custody pass.";
                if(!entry.sampleStored || !IsSampleSealed(id)) return "Seal and store " + entry.definition.PlayerDisplayName + " before the custody pass.";
            }
            else if(!entry.bagged || !entry.bagStored || !EvidenceStateManager.Instance.IsAtOrPastStatus(id,EvidenceStatus.Sealed))
                return "Seal and store " + entry.definition.PlayerDisplayName + " before the custody pass.";
        }
        return null;
    }

    /// <summary>Validate the whole reviewed set before mutation. Samples keep their own custody;
    /// a derived sample never changes its source evidence's location.</summary>
    public bool TransferStoredToBox(string boxId, IReadOnlyList<string> expectedIds, out string reason)
    {
        reason=CustodyTransferBlockReason(expectedIds);
        if(reason!=null) return false;
        if(string.IsNullOrWhiteSpace(boxId)) { reason="The evidence box has no custody identifier."; return false; }
        var ids=expectedIds.ToArray();
        var previous=ids.ToDictionary(id=>id,GetCustody);
        string transferId=Guid.NewGuid().ToString("N");
        // Commit every custody record before invoking any observer.
        foreach(var id in ids)
        {
            var entry=entries[id]; entry.depositedBoxId=boxId;
            if(entry.sampleSourceId!=null) entry.sampleCustody=EvidenceCustodyLocation.EvidenceBox;
            if(entry.sampleSourceId==null || entry.sampleSourceId==id)
                EvidenceStateManager.Instance.GetRecord(id).custodyLocation=EvidenceCustodyLocation.EvidenceBox;
        }
        foreach(var id in ids)
        {
            var entry=entries[id];
            SessionLogger.Instance?.LogEvent(SessionEventType.EvidenceDeposited,id,new Dictionary<string,string> {
                {"previousCustodyLocation",previous[id].ToString()},{"newCustodyLocation",EvidenceCustodyLocation.EvidenceBox.ToString()},
                {"boxId",boxId},{"transferId",transferId},{"sourceEvidenceId",entry.sampleSourceId ?? id},
                {"sampleType",entry.sampleSourceId==null ? "" : entry.sampleType},{"scored","false"} });
        }
        foreach(var id in ids) OnCustodyChanged?.Invoke(id,previous[id],EvidenceCustodyLocation.EvidenceBox);
        PlayDepositSound();
        OnInventoryChanged?.Invoke();
        return true;
    }

    public EvidenceDefinition GetDefinition(string evidenceId) => evidenceId != null && entries.TryGetValue(evidenceId, out var entry) ? entry.definition : null;

    public bool CanAcceptBaggedEvidence(string evidenceId)
    {
        if (!ProcedureInterlock.AllowsHandling(evidenceId)) return false;
        if (string.IsNullOrEmpty(evidenceId)) return false;
        var record = EvidenceStateManager.Instance?.GetRecord(evidenceId);
        if (record?.definition?.heldPrefab == null || RequiresExplicitStorage) return false;
        if (entries.TryGetValue(evidenceId, out var pending))
            return pending.bagged && !pending.bagStored && CurrentHeldEvidenceId == null
                && record.custodyLocation == EvidenceCustodyLocation.Scene;
        return record?.definition?.heldPrefab != null && !entries.ContainsKey(evidenceId)
            && !RequiresExplicitStorage && (capacity <= 0 || InventoryCount < capacity);
    }

    // Registration reserves this same evidence ID for a held bag, not inventory custody.
    // EquipHeld hides the loose prop only once its completed held visual exists.
    public bool RegisterBaggedEvidence(string evidenceId)
    {
        if (!CanAcceptBaggedEvidence(evidenceId)
            || !EvidenceStateManager.Instance.IsAtOrPastStatus(evidenceId, EvidenceStatus.Sealed)) return false;
        if (entries.ContainsKey(evidenceId)) return true; // Retry an interrupted hand transfer, same reserved ID.
        entries.Add(evidenceId, new Entry { definition = EvidenceStateManager.Instance.GetRecord(evidenceId).definition, bagged = true });
        return true;
    }

    public bool ConfirmBagStorage()
    {
        if (CurrentHeldEvidenceId == null || !entries.TryGetValue(CurrentHeldEvidenceId, out var entry) || !entry.bagged
            || !EvidenceStateManager.Instance.IsAtOrPastStatus(CurrentHeldEvidenceId, EvidenceStatus.Sealed)) return false;
        if (!entry.bagStored)
            SessionLogger.Instance?.LogEvent(SessionEventType.EvidenceAddedToInventory, CurrentHeldEvidenceId,
                new Dictionary<string, string> {
                    { "previousCustodyLocation", EvidenceCustodyLocation.Held.ToString() },
                    { "newCustodyLocation", EvidenceCustodyLocation.Inventory.ToString() }
                });
        entry.bagStored = true;
        return true;
    }

    /// <summary>
    /// Admits a sealed item to the inventory - Scene -> Inventory. Requires the item's
    /// EvidenceStatus to already be Sealed or later (MarkSealed must have already
    /// succeeded; this method never itself advances procedure state) and that no
    /// inventory record already exists for this id - duplicates are rejected outright,
    /// never silently overwritten.
    ///
    /// Hides the scene EvidenceProp ONLY on success. A caller (EvidenceBagTool/
    /// SwabTool.TrySeal) must never hide the scene object itself - that is this
    /// method's job specifically so a failed admission (unregistered id, not actually
    /// Sealed, inventory full) leaves the item visible and recoverable instead of
    /// disappearing. See this feature's own "partial success" requirement.
    /// </summary>
    public bool Add(string evidenceId)
    {
        if (!ProcedureInterlock.AllowsHandling(evidenceId)) return false;
        if (string.IsNullOrEmpty(evidenceId))
        {
            Debug.LogWarning("[EvidenceInventory] Add: blank evidenceId.");
            return false;
        }

        if (entries.ContainsKey(evidenceId))
        {
            // The on-scene spatter sample already exists; later procedure completion never creates another container.
            if (GetSampleSource(evidenceId) == evidenceId) return true;
            Debug.LogWarning($"[EvidenceInventory] Add({evidenceId}): already in inventory - rejecting duplicate.");
            return false;
        }

        var record = EvidenceStateManager.Instance?.GetRecord(evidenceId);
        if (record == null || record.definition == null)
        {
            Debug.LogWarning($"[EvidenceInventory] Add({evidenceId}): no EvidenceRecord/definition registered - refusing.");
            return false;
        }

        if (!EvidenceStateManager.Instance.IsAtOrPastStatus(evidenceId, EvidenceStatus.Sealed))
        {
            Debug.LogWarning($"[EvidenceInventory] Add({evidenceId}): status is {record.status}, must be Sealed or later - refusing.");
            return false;
        }

        if (capacity > 0 && InventoryCount >= capacity)
        {
            Debug.LogWarning($"[EvidenceInventory] Add({evidenceId}): inventory is full (capacity {capacity}) - refusing.");
            return false;
        }

        entries[evidenceId] = new Entry { definition = record.definition };

        var previous = record.custodyLocation;
        record.custodyLocation = EvidenceCustodyLocation.Inventory;

        // Hide the scene representation now that admission has genuinely succeeded -
        // never before this point (see class comment on partial success). A no-op if
        // no matching scene EvidenceProp is currently registered (e.g. it was already
        // deactivated, or this id never had a loose scene prop to begin with).
        var prop = EvidenceProp.FindById(evidenceId);
        if (prop != null)
        {
            prop.gameObject.SetActive(false);
        }

        SessionLogger.Instance?.LogEvent(SessionEventType.EvidenceAddedToInventory, evidenceId, new Dictionary<string, string>
        {
            { "previousCustodyLocation", previous.ToString() },
            { "newCustodyLocation", record.custodyLocation.ToString() }
        });

        OnCustodyChanged?.Invoke(evidenceId, previous, record.custodyLocation);
        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Spawns/activates this item's held representation at handAnchor and marks it
    /// Held (Inventory -> Held). Refuses and logs an Inspector-visible warning - never
    /// falls back to showing the raw crime-scene model - if the item isn't currently in
    /// Inventory, or if its EvidenceDefinition has no heldPrefab configured. Does NOT
    /// itself decide whether a tool/other evidence should be unequipped first - that
    /// ordering is HeldItemCoordinator's job; this method only ever does what it's told.
    /// </summary>
    public bool EquipHeld(string evidenceId, Transform handAnchor)
    {
        if (!ProcedureInterlock.AllowsHandling(evidenceId)) return false;
        if (string.IsNullOrEmpty(evidenceId) || handAnchor == null || IsDeposited(evidenceId))
        {
            return false;
        }

        if (!entries.TryGetValue(evidenceId, out var entry))
        {
            Debug.LogWarning($"[EvidenceInventory] EquipHeld({evidenceId}): not in inventory.");
            return false;
        }

        var record = EvidenceStateManager.Instance?.GetRecord(evidenceId);
        bool newlyBagged = entry.bagged && !entry.bagStored;
        if (entry.sampleSourceId == null && (record == null || (record.custodyLocation != EvidenceCustodyLocation.Inventory
            && !(newlyBagged && record.custodyLocation == EvidenceCustodyLocation.Scene))))
        {
            Debug.LogWarning($"[EvidenceInventory] EquipHeld({evidenceId}): custody is {(record != null ? record.custodyLocation.ToString() : "unknown")}, must be Inventory.");
            return false;
        }

        if (entry.definition.heldPrefab == null)
        {
            Debug.LogWarning($"[EvidenceInventory] EquipHeld({evidenceId}): no heldPrefab configured on its EvidenceDefinition - refusing rather than showing the raw scene model. Assign a placeholder or real held prefab in the Inspector.", entry.definition);
            return false;
        }

        // Defensive: a previous held visual for a DIFFERENT item should already have
        // been cleared by HeldItemCoordinator calling UnequipHeld first, but this can
        // never leak two spawned visuals regardless of caller discipline.
        if (CurrentHeldEvidenceId != null && CurrentHeldEvidenceId != evidenceId)
        {
            if (RequiresExplicitStorage) return false;
            UnequipHeld();
        }

        if (entry.spawnedHeldVisual == null)
        {
            entry.spawnedHeldVisual = Instantiate(entry.definition.heldPrefab, handAnchor);
            entry.spawnedHeldVisual.name = $"HeldEvidence_{evidenceId}";
        }
        else
        {
            entry.spawnedHeldVisual.transform.SetParent(handAnchor, false);
        }

        entry.spawnedHeldVisual.transform.localPosition = entry.definition.heldLocalPosition;
        entry.spawnedHeldVisual.transform.localRotation = Quaternion.Euler(entry.definition.heldLocalEulerAngles);
        entry.spawnedHeldVisual.transform.localScale = entry.definition.heldLocalScale;
        entry.spawnedHeldVisual.SetActive(true);

        if (newlyBagged)
        {
            var prop = EvidenceProp.FindById(evidenceId);
            if (prop != null) prop.gameObject.SetActive(false);
        }

        var previous = GetCustody(evidenceId);
        if (entry.sampleSourceId != null) entry.sampleCustody = EvidenceCustodyLocation.Held;
        if (record != null) record.custodyLocation = EvidenceCustodyLocation.Held;
        CurrentHeldEvidenceId = evidenceId;

        SessionLogger.Instance?.LogEvent(SessionEventType.EvidenceEquipped, evidenceId, new Dictionary<string, string>
        {
            { "previousCustodyLocation", previous.ToString() },
            { "newCustodyLocation", EvidenceCustodyLocation.Held.ToString() }
        });

        OnCustodyChanged?.Invoke(evidenceId, previous, EvidenceCustodyLocation.Held);
        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Returns the currently held item (if any) to Inventory - Held -> Inventory. Safe
    /// no-op if nothing is held. Never called for a successful deposit - see Deposit
    /// instead (Held -> EvidenceBox is a different transition with its own log event).
    /// </summary>
    public void UnequipHeld()
    {
        if (RequiresExplicitStorage) return;
        if (CurrentHeldEvidenceId == null)
        {
            return;
        }

        string evidenceId = CurrentHeldEvidenceId;
        CurrentHeldEvidenceId = null;

        if (entries.TryGetValue(evidenceId, out var entry) && entry.spawnedHeldVisual != null)
        {
            entry.spawnedHeldVisual.SetActive(false);
        }

        if (entry != null && entry.sampleSourceId != null)
        {
            entry.sampleCustody = EvidenceCustodyLocation.Inventory;
            if (EvidenceStateManager.Instance?.GetRecord(evidenceId) == null)
            {
                SessionLogger.Instance?.LogEvent(SessionEventType.EvidenceUnequipped, evidenceId);
                OnCustodyChanged?.Invoke(evidenceId, EvidenceCustodyLocation.Held, EvidenceCustodyLocation.Inventory);
                OnInventoryChanged?.Invoke();
            }
        }
        var record = EvidenceStateManager.Instance?.GetRecord(evidenceId);
        if (record == null)
        {
            return;
        }

        var previous = record.custodyLocation;
        record.custodyLocation = EvidenceCustodyLocation.Inventory;

        SessionLogger.Instance?.LogEvent(SessionEventType.EvidenceUnequipped, evidenceId, new Dictionary<string, string>
        {
            { "previousCustodyLocation", previous.ToString() },
            { "newCustodyLocation", record.custodyLocation.ToString() }
        });

        OnCustodyChanged?.Invoke(evidenceId, previous, record.custodyLocation);
        OnInventoryChanged?.Invoke();
    }

    public bool ConfirmSampleStorage()
    {
        if (CurrentHeldEvidenceId == null || !entries.TryGetValue(CurrentHeldEvidenceId, out var entry) || entry.sampleSourceId == null) return false;
        if (!IsSampleSealed(CurrentHeldEvidenceId)) return false;
        if (!entry.sampleStored && IsFingerprintSample(CurrentHeldEvidenceId))
        {
            var state = EvidenceStateManager.Instance;
            // Identification must be confirmed at the desk while this card is held.
            if (!state.IsFingerprintMatched(entry.sampleSourceId) || !state.GetRecord(entry.sampleSourceId).fingerprintingDone) return false;
            entry.sampleStored = true;
            SessionLogger.Instance?.LogEvent(SessionEventType.EvidenceAddedToInventory, CurrentHeldEvidenceId,
                new Dictionary<string, string> { { "sourceEvidenceId", entry.sampleSourceId },
                    { "sampleType", "Fingerprint" }, { "scored", "false" } });
            // Securing the lift establishes custody, not identity. Comparison at
            // the examination desk resolves the match and its narrative findings.
            SessionLogger.Instance?.Flush();
            return true;
        }
        if (!entry.sampleStored) SessionLogger.Instance?.LogEvent(SessionEventType.EvidenceAddedToInventory, CurrentHeldEvidenceId);
        entry.sampleStored = true;
        return true;
    }

    /// <summary>
    /// Finalizes a deposit at an evidence box - Held -> EvidenceBox - and removes the
    /// item from the selectable inventory while retaining its custody metadata (IsDeposited still answers
    /// true afterward, read from EvidenceStateManager's own record; custodyLocation is
    /// never reverted from EvidenceBox once set). Eligibility is entirely
    /// EvidenceBoxDepositZone's job, re-validated there before this is ever called -
    /// this method trusts its caller and only re-checks the one invariant it alone can
    /// guarantee: that evidenceId is genuinely the item currently held.
    /// </summary>
    public bool Deposit(string evidenceId, string boxId)
    {
        if (RequiresExplicitStorage) return false;
        if (string.IsNullOrEmpty(evidenceId) || evidenceId != CurrentHeldEvidenceId)
        {
            Debug.LogWarning($"[EvidenceInventory] Deposit({evidenceId}): not the currently held item.");
            return false;
        }

        var record = EvidenceStateManager.Instance?.GetRecord(evidenceId);
        if (record == null)
        {
            return false;
        }

        if (entries.TryGetValue(evidenceId, out var entry) && entry.spawnedHeldVisual != null)
        {
            Destroy(entry.spawnedHeldVisual);
        }

        // Preserve provenance for closeout; AllEntryIds still excludes deposited entries.
        if (entries.TryGetValue(evidenceId, out entry))
        {
            entry.spawnedHeldVisual = null;
            if (entry.sampleSourceId != null) entry.sampleCustody = EvidenceCustodyLocation.EvidenceBox;
        }
        CurrentHeldEvidenceId = null;

        var previous = record.custodyLocation;
        record.custodyLocation = EvidenceCustodyLocation.EvidenceBox;

        SessionLogger.Instance?.LogEvent(SessionEventType.EvidenceDeposited, evidenceId, new Dictionary<string, string>
        {
            { "previousCustodyLocation", previous.ToString() },
            { "newCustodyLocation", record.custodyLocation.ToString() },
            { "boxId", boxId ?? string.Empty }
        });

        OnCustodyChanged?.Invoke(evidenceId, previous, record.custodyLocation);
        PlayDepositSound();
        OnInventoryChanged?.Invoke();
        return true;
    }
    private void PlayDepositSound()
    {
        var box=FindAnyObjectByType<EvidenceBoxDepositZone>();
        InteractionSfx.Play(InteractionSound.Place,box!=null ? box.transform.position : transform.position);
    }
}
