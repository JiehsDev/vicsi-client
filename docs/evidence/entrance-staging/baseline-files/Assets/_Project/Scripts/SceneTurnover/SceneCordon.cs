// Assets/_Project/Scripts/SceneTurnover/SceneCordon.cs
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Placeholder crime-scene cordon, established by the First Responder before the player
/// arrives. Scene dressing only - not evidence, not scored, never placed by the player - and
/// it stays in place for the whole investigation. Arrival entry requires briefing, gloves and check-in;
/// an early crossing returns the tracking origin to the last exterior head position.
/// Tape/posts stay secured during handover, investigation and completion.
/// </summary>
[DefaultExecutionOrder(30000)] // Check deferred SDK teleports before its end-of-frame origin sync.
public class SceneCordon : MonoBehaviour
{
    [Tooltip("The protected area in local space. Entry requires briefing, worn gloves and confirmed check-in.")]
    [SerializeField] private Bounds protectedArea = new Bounds(new Vector3(0f, 1.5f, 7.5f), new Vector3(11f, 4f, 15f));

    [Tooltip("Catalog entry shown when the player enters early. Blank = no warning.")]
    [SerializeField] private string earlyEntryWarningId = "fr-early-entry-warning";

    [Tooltip("Tape span closing the controlled entrance. Remains active throughout the session.")]
    [FormerlySerializedAs("entranceTape")]
    [SerializeField] private GameObject entranceClosed;

    [Tooltip("Legacy opened entrance visual. Kept inactive throughout the session.")]
    [SerializeField] private GameObject entranceOpen;

    [Tooltip("Controlled entrance boundary in cordon-local space. Zero size uses only the protected area. Early entry returns the player outside until briefing, gloves and check-in are complete.")]
    [SerializeField] private Bounds controlledEntryApproach;

    private bool wasInside;
    private Vector3 safeHeadPosition;
    private bool hasSafePosition;
    private OVRCameraRig rig;
    private Oculus.Interaction.Locomotion.FirstPersonLocomotor locomotor;
    private Oculus.Interaction.Locomotion.CharacterController character;
    private float safeFeetY;
    public ArrivalPPEStation PPEStation { get; private set; }
    public bool PPEReady => PPEStation != null && PPEStation.GlovesEquipped;
    public SceneAccessRegister AccessRegister { get; private set; }
    public bool CanEnter => !SceneTurnover.IsGated || (!SceneTurnover.IsPending && PPEReady && AccessRegister != null && AccessRegister.IsAuthorized);
    public int EntryReturns { get; private set; }

    /// <summary>Number of early-entry warnings shown this session (diagnostics/tests).</summary>
    public int EarlyEntryWarnings { get; private set; }
    private void Awake()
    {
        if (entranceClosed != null) entranceClosed.SetActive(true);
        if (entranceOpen != null) entranceOpen.SetActive(false);
        rig = FindAnyObjectByType<OVRCameraRig>();
        locomotor = FindAnyObjectByType<Oculus.Interaction.Locomotion.FirstPersonLocomotor>();
        character = FindAnyObjectByType<Oculus.Interaction.Locomotion.CharacterController>();
        var prefab = Resources.Load<GameObject>("ArrivalPPEStation");
        if (prefab != null)
        {
            var station = Instantiate(prefab); // Own root: independent of CordonVisible's tape renderers.
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(station, gameObject.scene);
            PPEStation = station.GetComponent<ArrivalPPEStation>();
            PPEStation.Initialize(this);
            var left = transform.Find("EntrancePost_L"); var right = transform.Find("EntrancePost_R");
            if (left != null && right != null)
            {
                Vector3 across = (right.position - left.position).normalized;
                Vector3 inward = Vector3.Cross(across, Vector3.up);
                Vector3 entry = (left.position + right.position) * .5f;
                if (Vector3.Dot(transform.TransformPoint(protectedArea.center) - entry, inward) < 0) inward = -inward;
                Vector3 at = right.position + across * .32f - inward * .65f;
                at.y = rig != null ? rig.transform.position.y : 0;
                // Find the exterior support surface rather than assuming the tape-post Y is the floor.
                foreach (var hit in Physics.RaycastAll(at + Vector3.up * 2, Vector3.down, 4, ~0, QueryTriggerInteraction.Ignore))
                    if (hit.normal.y > .9f && (rig == null || !hit.transform.IsChildOf(rig.transform))
                        && !hit.transform.IsChildOf(station.transform) && Mathf.Abs(hit.point.y - at.y) < .4f) { at.y = hit.point.y; break; }
                Vector3 signFacing = rig != null ? at - rig.transform.position : inward;
                signFacing.y = 0;
                station.transform.SetPositionAndRotation(at, Quaternion.LookRotation(signFacing.normalized));
            }
        }
        else Debug.LogError("Arrival PPE station prefab is missing; scene entry remains locked.", this);
        AccessRegister = gameObject.AddComponent<SceneAccessRegister>();
        AccessRegister.Initialize(this);
    }
    private void Start()
    {
        var head = Camera.main;
        if (head != null && !RequiresBriefingAt(head.transform.position))
        { RememberExterior(head.transform.position); }
        else if (head != null && rig != null)
        {
            // A room-scale pose can already extend beyond the tape on the first tracked frame.
            safeHeadPosition = new Vector3(rig.transform.position.x, head.transform.position.y, rig.transform.position.z);
            hasSafePosition = !RequiresBriefingAt(safeHeadPosition);
            safeFeetY = character != null ? character.Pose.position.y - character.Height * .5f : rig.transform.position.y;
        }
    }
    private void OnDestroy() { if (PPEStation != null) Destroy(PPEStation.gameObject); }
    private void RememberExterior(Vector3 head)
    {
        safeHeadPosition = head; hasSafePosition = true;
        safeFeetY = character != null ? character.Pose.position.y - character.Height * .5f : rig.transform.position.y;
    }

    /// <summary>Diagnostic check of the physical entrance tape; it stays closed after handover.</summary>
    public bool EntranceIsOpen => entranceClosed == null || !entranceClosed.activeSelf;

    /// <summary>
    /// Whether the secured cordon, including the closed entrance tape, is present and rendering.
    /// </summary>
    public bool CordonVisible
    {
        get
        {
            if (!isActiveAndEnabled) return false;
            int shown = 0;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (entranceOpen != null && r.transform.IsChildOf(entranceOpen.transform)) continue;
                if (!r.enabled || !r.gameObject.activeInHierarchy) return false;
                shown++;
            }
            return shown > 0;
        }
    }

    /// <summary>Whether a world-space point lies inside the protected area.</summary>
    public bool Contains(Vector3 worldPoint) => protectedArea.Contains(transform.InverseTransformPoint(worldPoint));

    /// <summary>Shared arrival entry boundary; protected-area geometry remains separately available through Contains.</summary>
    public bool RequiresBriefingAt(Vector3 worldPoint) => Contains(worldPoint)
        || (controlledEntryApproach.size.sqrMagnitude > .0001f
            && controlledEntryApproach.Contains(transform.InverseTransformPoint(worldPoint)));

    public bool IsWithinSecurityFootprint(Vector3 worldPoint)
    {
        var p = transform.InverseTransformPoint(worldPoint);
        bool In(Bounds b) => p.x >= b.min.x && p.x <= b.max.x && p.z >= b.min.z && p.z <= b.max.z;
        return In(protectedArea) || (controlledEntryApproach.size.sqrMagnitude > .0001f && In(controlledEntryApproach));
    }
    private bool FootprintSamples(Vector3 point, float margin, bool inside)
    {
        // Sample the union, not shrunken individual boxes: their internal join is not an exit.
        for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++)
            if (IsWithinSecurityFootprint(point + new Vector3(x * margin, 0, z * margin)) != inside) return false;
        return true;
    }
    public bool IsSecurelyInside(Vector3 point, float margin) => FootprintSamples(point, margin, true);
    public bool IsSecurelyOutside(Vector3 point, float margin) => FootprintSamples(point, margin, false);
    private void LateUpdate()
    {
        EnforceEntry();
        var head = Camera.main;
        if (head != null && rig != null && head.transform.IsChildOf(rig.transform))
            AccessRegister?.ObserveBoundary(head.transform.position, character != null ? character.Pose.position : head.transform.position, CanEnter);
    }
    private void EnforceEntry()
    {
        var head = Camera.main;
        bool trackedHead = head != null && rig != null && head.transform.IsChildOf(rig.transform);
        bool inside = head != null && (RequiresBriefingAt(head.transform.position)
            || (trackedHead && character != null && RequiresBriefingAt(character.Pose.position)));
        if (inside && !wasInside && !CanEnter)
        {
            EarlyEntryWarnings++;
            GameDialogue.Say(SceneTurnover.IsPending ? earlyEntryWarningId : !PPEReady ? "ppe-entry-required" : "access-entry-required");
        }
        wasInside = inside;
        if (!trackedHead) return;
        if (!inside) { RememberExterior(head.transform.position); return; }
        if (CanEnter || !hasSafePosition) return;
        if (locomotor != null && character != null && locomotor.isActiveAndEnabled)
        {
            Vector3 feet = new Vector3(safeHeadPosition.x, safeFeetY, safeHeadPosition.z);
            // Queue through the SDK to clear velocity; reset both capsule and origin now,
            // clearing its accumulated delta so LastUpdate cannot undo this correction.
            locomotor.HandleLocomotionEvent(new Oculus.Interaction.Locomotion.LocomotionEvent(
                -30000, new Pose(feet, character.Pose.rotation),
                Oculus.Interaction.Locomotion.LocomotionEvent.TranslationType.Absolute,
                Oculus.Interaction.Locomotion.LocomotionEvent.RotationType.None));
            character.SetPosition(feet + Vector3.up * character.Height * .5f);
            locomotor.ResetPlayerToCharacter();
        }
        else
        {
            Vector3 correction = safeHeadPosition - head.transform.position; correction.y = 0;
            rig.transform.position += correction;
        }
        EntryReturns++;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0f, 0.5f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(protectedArea.center, protectedArea.size);
    }
#endif
}
