// Assets/_Project/Scripts/Interaction/FingerprintStationPlaceholder.cs
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// PLACEHOLDER - NOT THE REAL FINGERPRINTING INTERACTION.
///
/// The state/gating half of requiresFingerprinting is fully wired and real:
/// EvidenceStateManager.MarkFingerprinted records the step, and Sealed -> Processed
/// is genuinely blocked until it has happened on any item whose EvidenceDefinition
/// sets requiresFingerprinting. What does NOT exist is the player-facing action that
/// should call it - dusting, lifting, and comparing a print is a whole interaction of
/// its own and building it was out of scope for this pass.
///
/// This component stands in for that interaction so the gate can be exercised and
/// tested end to end. It is deliberately crude: stand near it, press the trigger, and
/// every currently-Sealed item that needs fingerprinting is marked as processed at
/// once. A real implementation would be per-item, would require the physical dusting
/// motion, and would let the player get it WRONG - which matters, because a step that
/// cannot be failed measures nothing.
///
/// UVRevealable is the closest existing mechanic (it reveals fingerprint smudges under
/// UV light) but it is NOT a substitute: it runs at discovery time, before the item is
/// even Found, whereas this step belongs after Sealed. They are different moments in
/// the procedure that happen to share the word "fingerprint".
///
/// TO REPLACE: delete this component and call
/// EvidenceStateManager.Instance.MarkFingerprinted(evidenceId, ToolType.IOC) from the
/// real interaction instead. Nothing else needs to change - the gate reads the record,
/// not this script.
/// </summary>
public class FingerprintStationPlaceholder : MonoBehaviour
{
    [Tooltip("PLACEHOLDER. How close the player's head must be for the prompt to appear.")]
    [SerializeField] private float useRadius = 1.0f;

    [Tooltip("PLACEHOLDER. Which evidence items this station can process — leave empty to process every registered item that is Collected and needs fingerprinting.")]
    [SerializeField] private string[] restrictToEvidenceIds;

    private const string PromptText = "[Trigger] Process fingerprints (PLACEHOLDER)";

    private InputAction useAction;
    private bool promptShown;

    private void Awake()
    {
        Debug.LogWarning($"[FingerprintStationPlaceholder] '{name}' is a PLACEHOLDER standing in for the real fingerprint-processing interaction. See the class comment before shipping anything that depends on it.", this);
        useAction = new InputAction("FingerprintPlaceholder_Use", InputActionType.Button, "<XRController>{RightHand}/triggerPressed");
    }

    private void OnEnable() => useAction.Enable();

    private void OnDisable()
    {
        useAction.Disable();
        if (promptShown)
        {
            NotificationManager.HidePrompt();
            promptShown = false;
        }
    }

    private void OnDestroy() => useAction?.Dispose();

    private void Update()
    {
        var povCamera = Camera.main;
        if (povCamera == null || EvidenceStateManager.Instance == null)
        {
            return;
        }

        bool inRange = (povCamera.transform.position - transform.position).sqrMagnitude <= useRadius * useRadius;
        bool shouldShow = inRange && !PlayerUIGate.IsBlocked;

        if (shouldShow != promptShown)
        {
            promptShown = shouldShow;
            if (promptShown)
            {
                NotificationManager.ShowPrompt(PromptText);
            }
            else
            {
                NotificationManager.HidePrompt();
            }
        }

        if (promptShown && useAction.WasPressedThisFrame())
        {
            ProcessEligible();
        }
    }

    private void ProcessEligible()
    {
        if (restrictToEvidenceIds == null || restrictToEvidenceIds.Length == 0)
        {
            Debug.LogWarning("[FingerprintStationPlaceholder] No restrictToEvidenceIds set; this placeholder can only process items it is told about. Assign the ids it should handle.", this);
            NotificationManager.Notify("Nothing configured to process.");
            return;
        }

        int processed = 0;
        foreach (var evidenceId in restrictToEvidenceIds)
        {
            if (string.IsNullOrEmpty(evidenceId))
            {
                continue;
            }

            if (EvidenceStateManager.Instance.MarkFingerprinted(evidenceId, ToolType.IOC) == TransitionResult.Applied)
            {
                processed++;
            }
        }

        NotificationManager.Notify(processed > 0
            ? $"Fingerprints processed on {processed} item(s)."
            : "Nothing here is ready for fingerprinting.");
    }
}
