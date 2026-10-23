// Assets/_Project/Scripts/Interaction/FingerprintStationTool.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Real dust -&gt; lift -&gt; compare interaction for the DEFAULT (post-Sealed) fingerprint
/// shape - items whose print is read from a sealed container back at the lab, after
/// the item is already bagged (currently EVD-017 and EVD-018 - see IsEligible). The
/// on-scene shape (fingerprintBeforeCollection - the knife, EVD-014; the gloves,
/// EVD-019) no longer uses this station at all: both now go through
/// FingerprintKitTool + FingerprintTapeTool's collision-based dust/lift instead,
/// generalized off EvidenceFingerprintMarker rather than each item aiming a raycast
/// or a proximity-prompt at a fixed station (see FingerprintKitTool's own class
/// comment). EVD-019 used this station's proximity-prompt mechanism until this task -
/// removed from restrictToEvidenceIds accordingly.
///
/// Not a generalization of FingerprintKitTool's targeting model: by the time an item
/// is eligible here (must be Sealed - see IsEligible), EvidenceBagTool.TryInsert has
/// already disabled every collider on its EvidenceProp and swapped its renderer for a
/// bagged visual (see EvidenceBagTool.DisablePhysicalPresence/ApplyBaggedVisual), and
/// TrySeal has parented it into a fixed crate slot. There is no live object left to aim
/// a raycast at. This is a fixed station instead - the same proximity-prompt pattern
/// FingerprintStationPlaceholder used - that resolves its own current target from
/// whichever configured id is Sealed, requires fingerprinting, and isn't done yet.
///
/// Reused from FingerprintKitTool: the staged-click interaction style, the "generated
/// placeholder material, no art asset required" convention, and calling into
/// EvidenceStateManager's real gated methods only. NOT reused: FingerprintKitTool's
/// TrySealAndProcess bundling of Collected+Sealed+Fingerprinted+Processed into one
/// click - that bundling is correct only because collecting a lifted print IS
/// fingerprinting it. For these three items Collected and Sealed already happened
/// through EvidenceBagTool; this station only calls MarkFingerprinted (which itself
/// still enforces status == Sealed) and then MarkMatched for an item with an authored
/// match identity - see TryCompare.
///
/// Genuinely failable, unlike the placeholder it replaces: Lift only succeeds within
/// [minDustCoverage, maxDustCoverage]. Too little dust OR too much both smudge the
/// print and reset progress to zero - a real wrong answer with a real cost, not a
/// single trigger press that always succeeds.
/// </summary>
public class FingerprintStationTool : MonoBehaviour
{
    public static FingerprintStationTool Instance { get; private set; }

    private class ExamState
    {
        public int dustCoverage;
        public bool lifted;
    }

    [Tooltip("How close the player's head must be for the prompt to appear.")]
    [SerializeField] private float useRadius = 2.5f;

    [Tooltip("Which evidence items this station can process, tried in order - only ids that are Sealed, requiresFingerprinting, and not yet fingerprinted are ever offered.")]
    [SerializeField] private string[] restrictToEvidenceIds;

    [Header("Dust window (real failability - see class comment)")]
    [SerializeField] private int minDustCoverage = 3;
    [SerializeField] private int maxDustCoverage = 6;

    [Header("Real models (optional) - powder jar, brush, and lifting tape from the")]
    [Header("Low poly v2 pack. All three static single-mesh, no rig/animation - see")]
    [Header("class comment. A generated placeholder cube is used instead if any is unset.")]
    [SerializeField] private Mesh powderMesh;
    [SerializeField] private Material powderMaterial;
    [SerializeField] private Mesh brushMesh;
    [SerializeField] private Material brushMaterial;
    [SerializeField] private Mesh tapeMesh;
    [SerializeField] private Material tapeMaterial;

    [Header("Visual fallback (optional - a generated placeholder pad is created if unset)")]
    [Tooltip("Also doubles as the dust-coverage progress indicator when real models are in use - see class comment on why coverage still needs a generated visual regardless.")]
    [SerializeField] private Renderer padRenderer;

    private static readonly Color CleanColor = new Color(0.75f, 0.75f, 0.78f);
    private static readonly Color DustedColor = new Color(0.15f, 0.13f, 0.1f);
    private static readonly Color LiftedColor = new Color(0.2f, 0.6f, 0.9f);

    private InputAction dustOrCompareAction;
    private InputAction liftAction;
    private bool promptShown;
    private BoxCollider stationCollider;

    // Cosmetic-only brush-stroke nudge on each dust press, since POWDER_BRUSH_30K is a
    // single static mesh with no rig or animation clip (confirmed - see class comment)
    // - a baked "brushing" animation isn't something the asset supports, so this is a
    // small code-driven position offset instead. Never read by TryDust/TryLift/TryCompare
    // - purely a side effect, same as ApplyPadColor already was.
    private const float BrushStrokeDuration = 0.25f;
    private Transform brushTransform;
    private Vector3 brushRestLocalPosition;
    private float brushStrokeTimer;

    // Hidden until a print is actually lifted for the current target, then shown - see
    // Update(). Only one physical tape prop exists (one station), so its visibility
    // tracks whichever item is currently the resolved target.
    private GameObject tapeVisual;

    private readonly Dictionary<string, ExamState> examStates = new();

    /// <summary>Exposed for GreyboxFlowTest: the exact dust-press count guaranteed to land inside the good window.</summary>
    public int MinGoodDustCoverage => minDustCoverage;

    private void Awake()
    {
        Instance = this;

        dustOrCompareAction = new InputAction("FingerprintStation_DustOrCompare", InputActionType.Button, "<XRController>{RightHand}/triggerPressed");
        liftAction = new InputAction("FingerprintStation_Lift", InputActionType.Button, "<XRController>{RightHand}/gripPressed");

        EnsureVisual();
    }

    private void OnEnable()
    {
        dustOrCompareAction.Enable();
        liftAction.Enable();
    }

    private void OnDisable()
    {
        dustOrCompareAction.Disable();
        liftAction.Disable();
        if (promptShown)
        {
            NotificationManager.HidePrompt();
            promptShown = false;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
        dustOrCompareAction?.Dispose();
        liftAction?.Dispose();
    }

    private void Update()
    {
        var povCamera = Camera.main;
        if (povCamera == null || EvidenceStateManager.Instance == null)
        {
            return;
        }

        string targetId = GetCurrentTargetId();
        bool inRange = IsInRangeOf(targetId, povCamera.transform.position);
        bool shouldShow = inRange && !PlayerUIGate.IsBlocked && targetId != null;

        if (shouldShow != promptShown)
        {
            promptShown = shouldShow;
            if (promptShown)
            {
                NotificationManager.ShowPrompt(BuildPromptText(targetId));
            }
            else
            {
                NotificationManager.HidePrompt();
            }
        }
        else if (promptShown)
        {
            // Keep the prompt's stage/count current as the exam advances in place.
            NotificationManager.ShowPrompt(BuildPromptText(targetId));
        }

        UpdateBrushStroke();
        UpdateTapeVisibility(targetId);

        if (!promptShown)
        {
            return;
        }

        if (liftAction.WasPressedThisFrame())
        {
            TryLift(targetId);
        }
        else if (dustOrCompareAction.WasPressedThisFrame())
        {
            var state = GetState(targetId);
            if (state.lifted)
            {
                TryCompare(targetId);
            }
            else
            {
                TryDust(targetId);
            }
        }
    }

    private string BuildPromptText(string targetId)
    {
        var state = GetState(targetId);
        return state.lifted
            ? GameDialogue.Text("prompt-fingerprint-station-compare", targetId)
            : GameDialogue.Text("prompt-fingerprint-station-dust-lift", targetId, state.dustCoverage);
    }

    /// <summary>Earliest configured id that is Sealed, requires fingerprinting, and isn't done yet - or null.</summary>
    private string GetCurrentTargetId()
    {
        if (restrictToEvidenceIds == null)
        {
            return null;
        }

        foreach (var id in restrictToEvidenceIds)
        {
            if (!string.IsNullOrEmpty(id) && IsEligible(id, out _))
            {
                return id;
            }
        }
        return null;
    }

    /// <summary>
    /// Whether the player is close enough to actually use the station for targetId.
    /// For the default shape (EVD-017/EVD-018), that's proximity to this fixed
    /// station's own transform, exactly as before - by the time those items are
    /// eligible here (Sealed), EvidenceBagTool has already disabled their physical
    /// presence and parented them into a crate slot, so "at the station" is the only
    /// meaningful location left.
    ///
    /// The fingerprintBeforeCollection branch below is defensive dead code as of this
    /// task - both on-scene items that ever used this station (the knife, EVD-014;
    /// the gloves, EVD-019) now go through FingerprintKitTool + FingerprintTapeTool's
    /// collision-based dust/lift instead, and neither is in restrictToEvidenceIds any
    /// more (see this class's own class comment). Left in place rather than deleted:
    /// it is still the CORRECT rule for any future onScene item someone adds back to
    /// restrictToEvidenceIds by mistake, and the reasoning that motivated it is worth
    /// keeping on record - it was confirmed live as a real, reproducible gap, not a
    /// hypothetical, back when the knife still used this shape: the station sat
    /// ~8.8m from the knife's own EvidenceProp and ~6.4m from the gloves', both far
    /// outside useRadius, meaning a real player standing next to such an item
    /// (required to swab it) could never simultaneously be in range of this station -
    /// the prompt, and therefore TryDust/TryLift/TryCompare, could never become
    /// reachable through real play.
    /// </summary>
    private bool IsInRangeOf(string targetId, Vector3 playerPosition)
    {
        if (targetId == null)
        {
            return false;
        }

        Vector3 referencePosition = transform.position;

        var record = EvidenceStateManager.Instance?.GetRecord(targetId);
        if (record?.definition != null && record.definition.fingerprintBeforeCollection)
        {
            var prop = EvidenceProp.FindById(targetId);
            if (prop != null)
            {
                referencePosition = prop.transform.position;
            }
        }

        return (playerPosition - referencePosition).sqrMagnitude <= useRadius * useRadius;
    }

    private bool IsEligible(string evidenceId, out string reason)
    {
        var record = EvidenceStateManager.Instance != null ? EvidenceStateManager.Instance.GetRecord(evidenceId) : null;
        if (record == null || record.definition == null)
        {
            reason = GameDialogue.Text("sys-unknown-evidence");
            return false;
        }
        if (!record.definition.requiresFingerprinting)
        {
            reason = GameDialogue.Text("fingerprint-not-needed");
            return false;
        }
        if (record.definition.fingerprintBeforeCollection)
        {
            reason = GameDialogue.Text("fingerprint-use-on-scene-kit");
            return false;
        }
        if (record.fingerprintingDone)
        {
            reason = GameDialogue.Text("fingerprint-already-done");
            return false;
        }

        // EvidenceStateManager.MarkFingerprinted itself enforces the real precondition
        // (Sealed by default, or PhotographedFinal for a fingerprintBeforeCollection
        // item like the gloves - on-scene sub-steps now have to complete before
        // Logged, not merely before Collected) - mirrored here only so the station can
        // explain WHY before the player commits to a dust/lift attempt, not as a
        // second source of truth. See MarkFingerprinted's own comment for the full
        // reasoning on both shapes.
        bool onScene = record.definition.fingerprintBeforeCollection;
        EvidenceStatus requiredStatus = onScene ? EvidenceStatus.PhotographedFinal : EvidenceStatus.Sealed;
        if (record.status != requiredStatus)
        {
            reason = onScene ? GameDialogue.Text("fingerprint-photograph-first") : GameDialogue.Text("fingerprint-seal-first");
            return false;
        }

        // Same mirroring reasoning as the status check above, for the gloves' own
        // extra precondition (EvidenceDefinition.requiresFlipBeforeFingerprinting) -
        // without this, TryDust/TryLift would silently succeed on a surface that
        // hasn't been turned over yet, only for the final TryCompare to refuse
        // (MarkFingerprinted's own check) after the player already committed dust and
        // a lift attempt to it. Confirmed live as a real gap, not a hypothetical.
        if (onScene && record.definition.requiresFlipBeforeFingerprinting && !record.isFlipped)
        {
            reason = GameDialogue.Text("fingerprint-flip-first");
            return false;
        }
        reason = null;
        return true;
    }

    private ExamState GetState(string evidenceId)
    {
        if (!examStates.TryGetValue(evidenceId, out var state))
        {
            state = new ExamState();
            examStates[evidenceId] = state;
        }
        return state;
    }

    // Decays a small position nudge on the real brush model back to rest - the only
    // "brushing" motion available since POWDER_BRUSH_30K carries no rig or animation
    // clip (confirmed in Step 1). No-op if real models aren't in use.
    private void UpdateBrushStroke()
    {
        if (brushTransform == null || brushStrokeTimer <= 0f)
        {
            return;
        }

        brushStrokeTimer -= Time.deltaTime;
        if (brushStrokeTimer <= 0f)
        {
            brushStrokeTimer = 0f;
            brushTransform.localPosition = brushRestLocalPosition;
            return;
        }

        float t = brushStrokeTimer / BrushStrokeDuration;
        float wave = Mathf.Sin(t * Mathf.PI) * 0.03f;
        brushTransform.localPosition = brushRestLocalPosition + new Vector3(wave, 0f, 0f);
    }

    // The real tape model only appears once a print has actually been lifted for the
    // current target - it represents the lifted print itself, so showing it earlier
    // would be a lie. No-op if real models aren't in use.
    private void UpdateTapeVisibility(string targetId)
    {
        if (tapeVisual == null)
        {
            return;
        }

        bool shouldShow = targetId != null && GetState(targetId).lifted;
        if (shouldShow != tapeVisual.activeSelf)
        {
            tapeVisual.SetActive(shouldShow);
        }
    }

    /// <summary>
    /// One dusting stroke on evidenceId (or the auto-resolved current target if null).
    /// Public so GreyboxFlowTest can exercise this exact logic directly.
    /// </summary>
    public bool TryDust(string evidenceId = null)
    {
        evidenceId ??= GetCurrentTargetId();
        if (evidenceId == null)
        {
            NotificationManager.Notify(GameDialogue.Text("fingerprint-nothing-ready"), DialogueKind.Error);
            return false;
        }
        if (!IsEligible(evidenceId, out var reason))
        {
            NotificationManager.Notify(reason, DialogueKind.Error);
            return false;
        }

        var state = GetState(evidenceId);
        if (state.lifted)
        {
            return false;
        }

        state.dustCoverage++;
        ApplyPadColor(Color.Lerp(CleanColor, DustedColor, Mathf.Clamp01((float)state.dustCoverage / maxDustCoverage)));
        brushStrokeTimer = BrushStrokeDuration; // cosmetic only - see UpdateBrushStroke
        return true;
    }

    /// <summary>
    /// The real, failable lift: only succeeds with dust coverage inside
    /// [minDustCoverage, maxDustCoverage]. Too little or too much both smudge the
    /// print and reset coverage to zero - see class comment. Public for GreyboxFlowTest.
    /// </summary>
    public TransitionResult TryLift(string evidenceId = null)
    {
        evidenceId ??= GetCurrentTargetId();
        if (evidenceId == null)
        {
            NotificationManager.Notify(GameDialogue.Text("fingerprint-nothing-ready"), DialogueKind.Error);
            return TransitionResult.Violation;
        }
        if (!IsEligible(evidenceId, out var reason))
        {
            NotificationManager.Notify(reason, DialogueKind.Error);
            return TransitionResult.Violation;
        }

        var state = GetState(evidenceId);
        if (state.lifted)
        {
            return TransitionResult.Duplicate;
        }

        if (state.dustCoverage < minDustCoverage || state.dustCoverage > maxDustCoverage)
        {
            string smudgeId = state.dustCoverage < minDustCoverage ? "fingerprint-smudged-underdusted" : "fingerprint-smudged-overdusted";
            Debug.Log($"[FingerprintStationTool] Lift on {evidenceId} failed at {state.dustCoverage} strokes (needs {minDustCoverage}-{maxDustCoverage}) - {smudgeId}.");
            GameDialogue.Say(smudgeId);
            state.dustCoverage = 0;
            ApplyPadColor(CleanColor);
            return TransitionResult.Violation;
        }

        state.lifted = true;
        ApplyPadColor(LiftedColor);
        Debug.Log($"[FingerprintStationTool] Lifted a good print from {evidenceId} ({state.dustCoverage} strokes).");
        return TransitionResult.Applied;
    }

    /// <summary>
    /// The real, gated compare: MarkFingerprinted (which itself still requires Sealed)
    /// then, for an item with an authored match identity, MarkMatched. Still requires
    /// Sealed - never bundles Collected/Sealed/Processed the way
    /// FingerprintKitTool does for EVD-016 (see class comment for why that bundling
    /// doesn't apply here). Public for GreyboxFlowTest.
    /// </summary>
    public TransitionResult TryCompare(string evidenceId = null)
    {
        evidenceId ??= GetCurrentTargetId();
        if (evidenceId == null)
        {
            NotificationManager.Notify(GameDialogue.Text("fingerprint-nothing-ready"), DialogueKind.Error);
            return TransitionResult.Violation;
        }

        var state = GetState(evidenceId);
        if (!state.lifted)
        {
            Debug.Log($"[FingerprintStationTool] Can't compare {evidenceId}: no print lifted yet.");
            NotificationManager.Notify(GameDialogue.Text("fingerprint-lift-before-compare"), DialogueKind.Error);
            return TransitionResult.Violation;
        }

        var result = EvidenceStateManager.Instance.MarkFingerprinted(evidenceId, ToolType.IOC);
        if (result == TransitionResult.Applied)
        {
            // This legacy processing tool cannot identify a print by processing it.
            // Identification is resolved by the comparison desk's selected reference.

            string stationName = EvidenceStateManager.Instance.GetDisplayName(evidenceId);
            GameDialogue.Say("fingerprint-processed", stationName);
            examStates.Remove(evidenceId);
            ApplyPadColor(CleanColor);
        }
        else
        {
            NotificationManager.Notify(GameDialogue.Text("fingerprint-item-not-ready"), DialogueKind.Error);
        }
        return result;
    }

    private void ApplyPadColor(Color color)
    {
        if (padRenderer != null)
        {
            padRenderer.material.color = color;
        }
    }

    // Fixes the placeholder's zero-collider, zero-visual station. When the real
    // powder/brush/tape models are assigned, builds the real composition (see class
    // comment); otherwise falls back to a single generated pad, same "nothing here
    // depends on an art asset existing" convention FingerprintKitTool's own
    // dusted-material generator already uses. Either way, the collider is fit to
    // whatever ends up visible - never left at hardcoded bounds that could drift from
    // the real geometry.
    private void EnsureVisual()
    {
        stationCollider = GetComponent<BoxCollider>();
        if (stationCollider == null)
        {
            stationCollider = gameObject.AddComponent<BoxCollider>();
            stationCollider.isTrigger = true;
        }

        bool useRealModels = powderMesh != null && powderMaterial != null
            && brushMesh != null && brushMaterial != null
            && tapeMesh != null && tapeMaterial != null;

        if (useRealModels)
        {
            BuildRealModel("PowderJar", powderMesh, powderMaterial,
                new Vector3(-0.12f, 0.42f, 0f), Quaternion.identity, 0.04f);

            var brush = BuildRealModel("Brush", brushMesh, brushMaterial,
                new Vector3(0.12f, 0.42f, 0f), Quaternion.Euler(0f, 0f, 75f), 0.08f);
            brushTransform = brush.transform;
            brushRestLocalPosition = brushTransform.localPosition;

            var tape = BuildRealModel("Tape", tapeMesh, tapeMaterial,
                new Vector3(0f, 0.42f, 0.15f), Quaternion.identity, 0.05f);
            tape.SetActive(false); // shown only once a print is actually lifted - see UpdateTapeVisibility
            tapeVisual = tape;

            BuildCoverageIndicator(new Vector3(0f, 0.4f, -0.18f), new Vector3(0.14f, 0.015f, 0.1f));
        }
        else
        {
            BuildCoverageIndicator(new Vector3(0f, 0.5f, 0f), new Vector3(0.4f, 0.05f, 0.3f));
        }

        FitColliderToVisuals();
    }

    private GameObject BuildRealModel(string name, Mesh mesh, Material material, Vector3 localPosition, Quaternion localRotation, float scale)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = localRotation;
        go.transform.localScale = Vector3.one * scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    // The dust-coverage progress meter. Kept generated regardless of whether real
    // models are in use: none of powder/brush/tape encode "how many strokes so far" -
    // confirmed in Step 1 (static single meshes, no blend shapes, one material each) -
    // so a real per-stroke visual would be forcing something the assets don't actually
    // support. This is the deliberate fallback instead.
    private void BuildCoverageIndicator(Vector3 localPosition, Vector3 localScale)
    {
        var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pad.name = "CoverageIndicator";
        pad.transform.SetParent(transform, false);
        pad.transform.localPosition = localPosition;
        pad.transform.localScale = localScale;
        Destroy(pad.GetComponent<Collider>());
        padRenderer = pad.GetComponent<Renderer>();
        padRenderer.material.color = CleanColor;
    }

    // Wraps whatever ended up visible (real models or the fallback pad) rather than
    // trusting a hardcoded size - the placeholder's original collider was sized for a
    // generated cube and would have been wrong the moment real geometry replaced it.
    private void FitColliderToVisuals()
    {
        var renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return;
        }

        Bounds combined = renderers[0].bounds;
        foreach (var r in renderers)
        {
            combined.Encapsulate(r.bounds);
        }

        // Safe to treat world size as local size here: the station root carries no
        // rotation and unit scale (confirmed in the scene), so world and local axes
        // are aligned and unscaled.
        stationCollider.center = transform.InverseTransformPoint(combined.center);
        stationCollider.size = combined.size + Vector3.one * 0.05f;
    }
}
