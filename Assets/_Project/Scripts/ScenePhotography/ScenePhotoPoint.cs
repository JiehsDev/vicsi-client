// Assets/_Project/Scripts/ScenePhotography/ScenePhotoPoint.cs
using UnityEngine;

/// <summary>
/// One required (or optional) scene-photography position: where the player stands, and
/// what the camera must face. Placed on the floor at the standing spot; everything about
/// it is authored here in the Inspector - ScenePhotographyManager discovers these at
/// Start and orders them by sequenceIndex, so adding, removing or reordering a point is
/// a scene edit, never a code change.
/// </summary>
public class ScenePhotoPoint : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("Stable id used in logs and future requirement checks. Never rename once data exists.")]
    public string photoPointId;

    [Tooltip("Player-facing name of the view, e.g. \"Exterior establishing shot\".")]
    public string displayName;

    [Tooltip("ViCSIDialogueCatalog id of the instruction shown when this point becomes the active one (edit the wording in the catalog).")]
    public string instructionId;
    [Tooltip("Legacy: used only when instructionId is empty. Wording now lives in ViCSIDialogueCatalog.")]
    [TextArea(2, 4)]
    public string instructionText;

    /// <summary>Whether this point has an instruction to show when it becomes active.</summary>
    public bool HasInstruction => !string.IsNullOrEmpty(instructionId) || !string.IsNullOrEmpty(instructionText);

    /// <summary>Shows this point's instruction through the catalog (legacy text only if no id is set).</summary>
    public void ShowInstruction()
    {
        if (!string.IsNullOrEmpty(instructionId)) GameDialogue.Say(instructionId);
        else if (!string.IsNullOrEmpty(instructionText)) NotificationManager.Notify(instructionText);
    }

    [Header("Sequence")]
    [Tooltip("Order within the guided route (ascending). Gaps are fine - leave room to insert steps later.")]
    public int sequenceIndex;

    public ScenePhotoPhase phase;

    [Tooltip("Required points must all be completed for scene photography to be complete. An optional point can be skipped by completing the next point.")]
    public bool isRequired = true;

    [Tooltip("Relative value for later assessment scoring. Not read by any scoring yet.")]
    [Min(0f)] public float scoreWeight = 1f;

    [Header("Standing zone")]
    [Tooltip("Horizontal radius (m) around this transform the player's head must be within. Forgiving on purpose for Quest.")]
    [Min(0.1f)] public float standingRadius = 0.75f;

    [Tooltip("Max height of the player's head above this point (m) - stops a zone matching from the floor above.")]
    [Min(0.5f)] public float maxHeadHeight = 2.5f;

    [Header("Camera direction")]
    [Tooltip("What the camera must face. If empty, this transform's forward is the required direction.")]
    public Transform cameraTarget;

    [Tooltip("Max angle (degrees) between the camera's forward and the direction to the target.")]
    [Range(5f, 90f)] public float angleTolerance = 30f;

    [Header("Visual")]
    [Tooltip("Floor marker shown for the active point while the player is outside its standing zone.")]
    public GameObject zoneVisual;

    public bool IsCompleted { get; private set; }

    /// <summary>True while this point's floor marker is actually showing.</summary>
    public bool IsZoneVisible => zoneVisual != null && zoneVisual.activeInHierarchy;

    private bool highlighted;   // what the route wants (current point); shown only once the investigation has begun

    public string LogLabel => string.IsNullOrEmpty(photoPointId) ? name : photoPointId;

    private void Awake()
    {
        SetHighlighted(false);
    }

    private void LateUpdate()
    {
        ApplyVisibility();
    }

    public bool ContainsPlayer(Vector3 headPosition)
    {
        Vector3 p = transform.position;
        float dx = headPosition.x - p.x;
        float dz = headPosition.z - p.z;
        float height = headPosition.y - p.y;
        return dx * dx + dz * dz <= standingRadius * standingRadius && height > -0.5f && height <= maxHeadHeight;
    }

    /// <summary>Angle (degrees) between the camera's forward and the required direction.</summary>
    public float AngleFrom(Transform cameraTransform)
    {
        Vector3 required = cameraTarget != null
            ? cameraTarget.position - cameraTransform.position
            : transform.forward;
        return Vector3.Angle(cameraTransform.forward, required);
    }

    internal void MarkCompleted()
    {
        IsCompleted = true;
        SetHighlighted(false);
    }

    internal void ResetState()
    {
        IsCompleted = false;
        SetHighlighted(false);
    }

    internal void SetHighlighted(bool on)
    {
        highlighted = on;
        ApplyVisibility();
    }

    /// <summary>
    /// Shows the active, unfinished marker after turnover while the player is outside
    /// the existing capture zone. Leaving without a successful photo restores it;
    /// hiding this visual never changes capture eligibility or route progress.
    /// </summary>
    internal void ApplyVisibility()
    {
        if (zoneVisual != null)
        {
            bool show = highlighted && !IsCompleted && !SceneTurnover.IsInvestigationBlocked;
            if (show)
            {
                var head = Camera.main;
                show = head == null || !ContainsPlayer(head.transform.position);
            }
            if (zoneVisual.activeSelf != show) zoneVisual.SetActive(show);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = IsCompleted ? new Color(0.3f, 1f, 0.4f, 0.8f) : new Color(0.2f, 0.9f, 1f, 0.8f);
        const int segments = 32;
        Vector3 c = transform.position + Vector3.up * 0.02f;
        for (int i = 0; i < segments; i++)
        {
            float a0 = i * Mathf.PI * 2f / segments;
            float a1 = (i + 1) * Mathf.PI * 2f / segments;
            Gizmos.DrawLine(c + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * standingRadius,
                            c + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * standingRadius);
        }

        Vector3 eye = transform.position + Vector3.up * 1.6f;
        Gizmos.DrawLine(eye, cameraTarget != null ? cameraTarget.position : eye + transform.forward * 2f);
        UnityEditor.Handles.Label(eye + Vector3.up * 0.2f, $"{sequenceIndex}: {photoPointId}");
    }
#endif
}
