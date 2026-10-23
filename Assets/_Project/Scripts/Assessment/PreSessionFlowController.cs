// Assets/_Project/Scripts/Assessment/PreSessionFlowController.cs
using UnityEngine;

/// <summary>
/// Orchestrates the required pre-session flow (Part 1):
///
///   Application start
///   -> Check for pending uploads          (already automatic - see note below)
///   -> Require student pairing            (blocks interaction/locomotion, shows the panel)
///   -> Confirm student and class          (panel's own confirmation screen, already built)
///   -> Confirm the paired scenario matches this device's scenario
///   -> Start the assigned scenario / Begin SessionLogger  (already automatic - see note below)
///   -> Allow investigation                (unblocks interaction/locomotion)
///
/// Deliberately a SEPARATE component from PairingCodeIdentityPanel - the panel stays
/// exactly what it already is (a scene-authored UI surface with zero flow/gameplay
/// logic of its own), and this controller owns sequencing/gating instead of that logic
/// living inside the panel. Nothing about PairingCodeIdentityPanel's own serialized
/// fields or Inspector wiring changes for this feature.
///
/// TWO STEPS ABOVE ARE INTENTIONALLY NOT "handled" by this controller, because they are
/// already true by construction elsewhere in this project - changing either would mean
/// modifying already-working systems this feature was explicitly told not to touch
/// unless necessary:
///
///   "Check for pending uploads" - AssessmentUploadService.OnEnable() already calls
///   ResumePendingUploads() automatically on every launch/domain reload (see that
///   class's own comment). Nothing here needs to trigger it again.
///
///   "Begin SessionLogger" - SessionLogger.Awake() already starts recording
///   unconditionally the moment _Managers loads, BY DESIGN (see SessionLogger's own
///   class comment: "deliberately not triggered externally... always recording from
///   the moment the scene's managers spin up, before any other script's Start() could
///   fire an event it would otherwise miss"). Gating SessionLogger itself would fight
///   that documented design. Instead, this controller gates INTERACTION: with
///   PlayerUIGate blocked and locomotion paused, no meaningful evidence event can occur
///   before pairing succeeds, so no meaningful assessed session content exists before
///   that point even though the logger's own clock started earlier. What actually
///   becomes an uploaded "assessment session" is gated correctly regardless - see
///   AssessmentUploadService.QueueSessionForUpload, which only ever fires from
///   EndingManager.FinishSubmission, which cannot be reached without having played
///   through the (interaction-gated) investigation first.
///
/// "Show only assignments returned by the server" is satisfied by construction too:
/// this project has no assignment-BROWSING UI in Unity at all (that is the dashboard's
/// job - see /pairing in vicsi-dashboard). The only assignment/scenario/class/student
/// values ever displayed here are whatever PairingIdentityService resolved from the
/// server's own exchange response.
/// </summary>
/// <remarks>
/// PHASE 3 - STUDENT CODE IDENTIFICATION (the normal classroom path): when
/// identificationPanel is assigned, the student enters their VR access code (or chooses
/// Continue Without Code -> the existing test student), confirms who they are, and the
/// CURRENT SessionLogger session is permanently bound to that stable studentId
/// (SessionOwnership) before investigation unlocks. Uploads later authorize per session
/// for that bound owner (AssessmentUploadService's VR path) - no pairing token involved.
/// The legacy pairing panel is used only if identificationPanel is not assigned, and the
/// John Doe development bypass only when developmentBypassEnabled is explicitly set.
/// </remarks>
public class PreSessionFlowController : MonoBehaviour
{
    [Header("Student identification (Phase 3 - normal path)")]
    [Tooltip("The student-code / Continue Without Code screen. When assigned, this is the identification path; the pairing panel below is the fallback only when this is empty.")]
    [SerializeField] private StudentIdentificationPanel identificationPanel;

    [Tooltip("When this scene is started directly (Editor play, no lobby): ON = assign the session to the existing John Doe test student (as \"Continue Without Code\" does). OFF (default) = just open the scene with no owner - nothing is attributed, and a finished session stays parked (AwaitingPairing) instead of uploading. Keep OFF so editor/test plays never land on John Doe. Either way the code panel never blocks a direct start.")]
    [SerializeField] private bool assignDirectRunsToTestStudent = false;
    [Tooltip("Upload/endpoint configuration used for online code validation (same asset as the upload services).")]
    [SerializeField] private AssessmentUploadConfiguration identificationConfiguration;

    [Header("Dependencies (editable)")]
    [SerializeField] private PairingCodeIdentityPanel pairingPanel;
    [SerializeField] private PairingIdentityService pairingIdentityService;
    [SerializeField] private ScenarioDefinition expectedScenario;

    [Header("Locomotion pause while pairing (editable — assign whatever this rig actually uses)")]
    [Tooltip("Disabled (Behaviour.enabled = false) while the pairing panel owns the UI, re-enabled once pairing is confirmed. Deliberately untyped/generic (any Behaviour, e.g. a Continuous Move Provider or teleportation component) rather than hardcoded to one locomotion package, so this stays editable without a code change if the locomotion setup changes.")]
    [SerializeField] private Behaviour[] locomotionComponentsToPauseWhilePairing;

    [Header("Development bypass (explicit opt-in only)")]
    [Tooltip("DEVELOPMENT ONLY - leave OFF for classroom builds. When true, Start() skips identification entirely and " +
        "applies the John Doe identity below with its captured long-lived pairing token, so every session on this " +
        "build belongs to John Doe through the LEGACY pairing upload path. Normal play uses the identification " +
        "screen instead, where Continue Without Code reaches the same existing John Doe through the regular " +
        "per-session upload authorization (no captured token).")]
    [SerializeField] private bool developmentBypassEnabled = false;
    [SerializeField] private string developmentBypassStudentDisplayName = "John Doe";
    [SerializeField] private string developmentBypassClassDisplayName = "ViCSI Dev Test Class - Z";

    [Header("Development bypass - real captured token for John Doe")]
    [Tooltip("REQUIRED for this bypass to actually upload sessions. A REAL assessment_tokens row minted directly " +
        "in the dev Supabase project for the real 'John Doe' test student (see " +
        "supabase/seed/seed-john-doe-bypass-identity.mjs in the vicsi-dashboard repo - same table/shape " +
        "POST /api/v1/device-pairings/exchange itself writes, just with a long expiry instead of that route's " +
        "fixed 4 hours, so this survives an extended dev/testing period without needing to be re-minted). " +
        "Sessions completed under this bypass upload through the exact same pipeline as a normally paired " +
        "student. If left blank, Start() refuses to silently proceed - see the 'identity could not be resolved' " +
        "check there - rather than uploading under a broken null-token identity. Intentionally EMPTY: the token " +
        "previously captured here was revoked server-side and must never be committed to source, a scene or a " +
        "build. The normal path (Continue Without Code -> SessionOwnership -> per-session VR upload token) does " +
        "not use this field.")]
    [SerializeField] private string developmentBypassRealAssessmentToken = "";
    [SerializeField] private string developmentBypassRealTokenExpiresAtUtc = "";
    [SerializeField] private string developmentBypassRealAssignmentId = "60edbee9-b292-41d3-9adf-059d3f286c28";
    [SerializeField] private string developmentBypassRealScenarioId = "CSI-ENVIRONMENT-001";
    [SerializeField] private string developmentBypassRealScenarioVersion = "1.0.0";
    [SerializeField] private string developmentBypassRealScoringRulesVersion = "1.0.0";

    private readonly object uiGateToken = new();
    private bool sessionUnlocked;

    /// <summary>Read-only diagnostic surface for the dev-only in-headset overlay (see DevDiagnosticOverlay) - true once UnlockInvestigation() has run. No behavior implication of its own.</summary>
    public bool SessionUnlocked => sessionUnlocked;

    // TODO BEFORE FINAL DEPLOYMENT: Restore mandatory student pairing and remove
    // temporary John Doe identity. Restoration steps:
    //   1. Set developmentBypassEnabled back to false (or delete the whole
    //      "Development bypass" block below it once pairing is fully restored).
    //   2. This re-enables the original entry point unconditionally: the
    //      pairingPanel.Show() call at the bottom of this method, reached via the
    //      normal (pairingPanel == null || pairingIdentityService == null) /
    //      LockInvestigation() / pairingPanel.OnPairingCompleted path below -
    //      none of that original code was deleted, only skipped while the bypass
    //      is true.
    //   3. Revoke the John Doe assessment_tokens row (and optionally the whole
    //      John Doe profile/class/enrollment/assignment) in the dev Supabase
    //      project - see supabase/seed/seed-john-doe-bypass-identity.mjs's own
    //      header in the vicsi-dashboard repo.
    private void Start()
    {
        if (CompetitiveEdition.Enabled)
        {
            identificationPanel?.Hide(); pairingPanel?.Hide(); UnlockInvestigation(); return;
        }
        if (SceneTestStart.IsActive) { UnlockInvestigation(); return; }
        // TEMPORARY DEVELOPMENT BYPASS - restore student pairing before final
        // deployment. Deliberately unconditional (no Application.isEditor /
        // Debug.isDebugBuild check) - applies to every build right now, including
        // a normal (non-development) Quest build, per this feature's own explicit
        // requirement not to rely on Debug.isDebugBuild. See developmentBypassEnabled's
        // own tooltip and the TODO comment immediately above this method.
        if (developmentBypassEnabled)
        {
            bool hasRealToken = !string.IsNullOrEmpty(developmentBypassRealAssessmentToken)
                && !string.IsNullOrEmpty(developmentBypassRealAssignmentId)
                && !string.IsNullOrEmpty(developmentBypassRealScenarioId);

            if (!hasRealToken)
            {
                // Fail safely (Requirement 7): never silently proceed under a
                // broken/no-token identity - that would let a session get played
                // and then sit uploading under nobody. Investigation stays locked,
                // same as the "pairingPanel/pairingIdentityService not assigned"
                // guard just below this block.
                Debug.LogError("[PreSessionFlowController] Temporary test student identity could not be resolved " +
                    "(developmentBypassRealAssessmentToken/AssignmentId/ScenarioId not configured - see " +
                    "supabase/seed/seed-john-doe-bypass-identity.mjs in the vicsi-dashboard repo). Refusing to start " +
                    "an unattributable session. Investigation will remain locked.");
                LockInvestigation();
                return;
            }

            Debug.LogWarning("[PreSessionFlowController] TEMPORARY DEVELOPMENT BYPASS is ON - skipping real student " +
                "pairing for ALL builds (including Quest). Every session right now is attributed to " +
                $"'{developmentBypassStudentDisplayName}'. Restore mandatory pairing before final deployment - see " +
                "the TODO comment above this method.");
            ApplyBypassIdentity();
            UnlockInvestigation();
            return;
        }

        if (identificationPanel != null)
        {
            LockInvestigation();
            var config = identificationConfiguration;
            var flow = new StudentIdentificationFlow(
                new UnityWebRequestVrCodeValidator(
                    () => config,
                    () => StudentDirectoryService.Instance != null ? StudentDirectoryService.Instance.DeviceKey : VrDeviceKey.Resolve()),
                () => StudentDirectoryService.Instance != null ? StudentDirectoryService.Instance.Cache : null,
                () => config != null ? config.vrCodeValidationTimeoutSeconds : 5f);
            identificationPanel.SetFlow(flow);
            identificationPanel.OnIdentified += HandleStudentIdentified;
            if (StudentIdentificationHandoff.TryTake(out IdentifiedStudent fromLobby))
            {
                // Identified in the lobby before this scene loaded: bind this session's owner
                // now, no second prompt. If binding fails, HandleStudentIdentified shows the
                // panel so the student can identify here instead.
                HandleStudentIdentified(fromLobby);
                return;
            }
            if (!StudentIdentificationHandoff.LobbyVisited)
            {
                // Scenario scene started directly (Editor play) - there is no lobby to identify
                // in, and the in-scene code panel would just block the scene.
                if (!assignDirectRunsToTestStudent)
                {
                    Debug.LogWarning("[PreSessionFlowController] Scene started directly (no lobby): opened without a student owner (assignDirectRunsToTestStudent is off).");
                    UnlockUnassignedDirectRun();
                    return;
                }
                // Same synced test student "Continue Without Code" uses, flagged as a test session.
                var direct = flow.ContinueWithoutCode();
                if (direct.Outcome == IdentificationOutcome.Identified)
                {
                    Debug.LogWarning($"[PreSessionFlowController] Scene started directly (no lobby): session assigned to test student '{direct.Student.DisplayName}'.");
                    HandleStudentIdentified(direct.Student);
                    return;
                }
                Debug.LogWarning("[PreSessionFlowController] Scene started directly, but the test student (John Doe) isn't in the synced student directory - opening without an owner.");
                UnlockUnassignedDirectRun();
                return;
            }
            identificationPanel.Show();
            return;
        }

        if (pairingPanel == null || pairingIdentityService == null)
        {
            Debug.LogError("[PreSessionFlowController] pairingPanel/pairingIdentityService not assigned - cannot gate the session. Investigation will remain blocked.");
            LockInvestigation();
            return;
        }

        LockInvestigation();
        pairingPanel.OnPairingCompleted += HandlePairingCompleted;
        pairingPanel.OnCancelled += HandlePairingCancelled;
        pairingPanel.Show();
    }

    /// <summary>
    /// The student confirmed who they are: bind THIS session's owner permanently, then
    /// unlock. From here on nothing on this headset can change who the session belongs
    /// to (SessionOwnershipStore never overwrites a bound session).
    /// </summary>
    private void HandleStudentIdentified(IdentifiedStudent student)
    {
        SessionOwnerRecord record;
        try
        {
            record = SessionOwnership.BindCurrentSession(student);
        }
        catch (System.IO.InvalidDataException ex)
        {
            Debug.LogError("[PreSessionFlowController] " + ex.Message + " Investigation stays locked.");
            identificationPanel.Show();
            return;
        }
        if (record == null || record.studentId != student.StudentId)
        {
            Debug.LogError("[PreSessionFlowController] Could not bind this session to the identified student - investigation stays locked.");
            identificationPanel.Show();
            return;
        }
        CurrentOwner = record;
        UnlockInvestigation();
    }

    /// <summary>The owner bound to the current session, once identified (null before, or on the legacy paths).</summary>
    public SessionOwnerRecord CurrentOwner { get; private set; }

    // An ownerless direct start is deliberately playable. Scope that permission to this
    // session, rather than treating every missing owner (including lobby failures) as a bypass.
    private string unassignedDirectSessionId;
    internal bool AllowsUnassignedCheckIn(string sessionId) => sessionUnlocked
        && !string.IsNullOrEmpty(unassignedDirectSessionId)
        && string.Equals(unassignedDirectSessionId, sessionId, System.StringComparison.Ordinal);

    private void UnlockUnassignedDirectRun()
    {
        unassignedDirectSessionId = SessionLogger.Instance != null ? SessionLogger.Instance.SessionId : null;
        UnlockInvestigation();
    }

    private void OnDestroy()
    {
        if (identificationPanel != null)
        {
            identificationPanel.OnIdentified -= HandleStudentIdentified;
        }
        if (pairingPanel != null)
        {
            pairingPanel.OnPairingCompleted -= HandlePairingCompleted;
            pairingPanel.OnCancelled -= HandlePairingCancelled;
        }
        // Always release the gate token on destroy, regardless of sessionUnlocked -
        // PlayerUIGate.Exit on a token that was never added (or already removed) is
        // explicitly safe by that class's own design.
        PlayerUIGate.Exit(uiGateToken);
    }

    private void HandlePairingCompleted()
    {
        if (pairingIdentityService == null || !pairingIdentityService.IsPaired)
        {
            // Should not be reachable - OnPairingCompleted only fires after a
            // successful exchange - but never trust an event alone to prove state.
            Debug.LogError("[PreSessionFlowController] OnPairingCompleted fired but PairingIdentityService is not actually paired.");
            pairingPanel.Show();
            return;
        }

        // Defense-in-depth only: the server's own consume_pairing_code RPC already
        // refuses a scenario mismatch (SCENARIO_MISMATCH) before OnPairingCompleted can
        // ever fire, since PairingIdentityService sends this device's own scenarioId in
        // the exchange request. This check exists purely in case that ever regresses -
        // never redesigned/loosened, and never the primary enforcement point.
        if (expectedScenario != null && pairingIdentityService.AssignedScenarioId != expectedScenario.scenarioId)
        {
            Debug.LogError(
                $"[PreSessionFlowController] Paired scenario '{pairingIdentityService.AssignedScenarioId}' does not match this device's scenario '{expectedScenario.scenarioId}' - refusing to start. This should have been rejected server-side already.");
            pairingIdentityService.ClearIdentity();
            pairingPanel.Show();
            return;
        }

        UnlockInvestigation();
    }

    private void HandlePairingCancelled()
    {
        // Per this feature's own requirement: cancelling never creates an assessment
        // session. Simplest correct behavior - investigation stays locked and the panel
        // is shown again, since there is no way to proceed without pairing. Never calls
        // UnlockInvestigation().
        pairingPanel.Show();
    }

    private void UnlockInvestigation()
    {
        if (sessionUnlocked) return; // idempotent - a duplicate/late event can't double-unlock
        sessionUnlocked = true;

        PlayerUIGate.Exit(uiGateToken);
        SetLocomotionEnabled(true);
    }

    private void LockInvestigation()
    {
        PlayerUIGate.Enter(uiGateToken);
        SetLocomotionEnabled(false);
    }

    private void SetLocomotionEnabled(bool enabled)
    {
        if (locomotionComponentsToPauseWhilePairing == null) return;
        foreach (var component in locomotionComponentsToPauseWhilePairing)
        {
            if (component != null) component.enabled = enabled;
        }
    }

    /// <summary>TEMPORARY DEVELOPMENT BYPASS, all builds - see developmentBypassEnabled's own tooltip. Sets the John Doe identity so the rest of the flow (and anything reading PairingIdentityService.IsPaired) behaves exactly as if a real pairing had just succeeded, without any network call - identical downstream behavior to a normal paired student, since this is a real captured token from the real backend.</summary>
    private void ApplyBypassIdentity()
    {
        bool hasRealToken = !string.IsNullOrEmpty(developmentBypassRealAssessmentToken);

        System.DateTime expiry = System.DateTime.UtcNow.AddHours(4);
        if (hasRealToken && !string.IsNullOrEmpty(developmentBypassRealTokenExpiresAtUtc)
            && System.DateTime.TryParse(developmentBypassRealTokenExpiresAtUtc, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out var parsedExpiry))
        {
            expiry = parsedExpiry;
        }

        if (hasRealToken)
        {
            Debug.LogWarning("[PreSessionFlowController] Development bypass is using a REAL captured assessment token - sessions completed now will actually upload to the backend under whatever student that token belongs to.");
        }

        pairingIdentityService?.SetIdentityForTesting(
            developmentBypassStudentDisplayName,
            developmentBypassClassDisplayName,
            assessmentToken: hasRealToken ? developmentBypassRealAssessmentToken : null,
            tokenExpiresAtUtc: expiry,
            assignedAssessmentAssignmentId: hasRealToken ? developmentBypassRealAssignmentId : null,
            assignedScenarioId: hasRealToken ? developmentBypassRealScenarioId : null,
            assignedScenarioVersion: hasRealToken ? developmentBypassRealScenarioVersion : null,
            assignedScoringRulesVersion: hasRealToken ? developmentBypassRealScoringRulesVersion : null);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (developmentBypassEnabled)
        {
            Debug.LogWarning($"[{name}] TEMPORARY DEVELOPMENT BYPASS is ON and now applies to EVERY build (Editor, Development Build, and a normal Quest build alike - it is no longer gated on Application.isEditor/Debug.isDebugBuild). Every session will be attributed to '{developmentBypassStudentDisplayName}'. Restore mandatory student pairing before final deployment - see the TODO comment above Start().", this);
        }
    }
#endif
}
