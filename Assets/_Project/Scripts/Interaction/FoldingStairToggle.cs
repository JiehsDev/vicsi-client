// Assets/_Project/Scripts/Interaction/FoldingStairToggle.cs
using System.Collections;
using UnityEngine;

/// <summary>
/// Swings a hinged assembly (e.g. the house's fold-down attic ladder) between
/// a closed pose flush with the ceiling and an open pose hanging down into
/// the room, by rotating target directly around a world-space hinge point/
/// axis - the same ease-in/out coroutine pattern PhotographTool uses for its
/// aim/zoom transitions. Deliberately does NOT reparent target under a pivot
/// object: target is typically a nested prefab instance root (e.g. an
/// imported house module's stair piece), and Unity silently refuses to
/// reparent a nested prefab instance root outside the normal Editor
/// drag-and-drop flow. Rotating around an explicit world hingePoint/hingeAxis
/// at runtime has no such restriction and needs no hierarchy changes at all -
/// target's own children (the ladder rungs and their colliders) move with it
/// automatically since they're still its ordinary children, so the ladder is
/// climbable/walkable the moment it's open with no extra collider wiring.
/// </summary>
public class FoldingStairToggle : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 hingePoint;
    [SerializeField] private Vector3 hingeAxis = Vector3.right;
    [SerializeField] private float openAngle = -80f;
    [SerializeField] private float transitionSeconds = 1f;

    public bool IsOpen { get; private set; }

    private Vector3 closedPosition;
    private Quaternion closedRotation;
    private float currentAngle;
    private bool initialized;
    private Coroutine animateRoutine;

    private static float SmoothStep01(float k) => k * k * (3f - 2f * k);

    private void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        closedPosition = target.position;
        closedRotation = target.rotation;
        initialized = true;
    }

    public void Toggle() => SetOpen(!IsOpen);

    public void SetOpen(bool open)
    {
        EnsureInitialized();

        if (open == IsOpen && animateRoutine == null)
        {
            return;
        }

        IsOpen = open;

        if (animateRoutine != null)
        {
            StopCoroutine(animateRoutine);
        }
        animateRoutine = StartCoroutine(AnimateSwing(open));
    }

    private void ApplyAngle(float angle)
    {
        Quaternion swing = Quaternion.AngleAxis(angle, hingeAxis);
        target.position = hingePoint + swing * (closedPosition - hingePoint);
        target.rotation = swing * closedRotation;
        currentAngle = angle;
    }

    private IEnumerator AnimateSwing(bool open)
    {
        EnsureInitialized();
        float fromAngle = currentAngle;
        float toAngle = open ? openAngle : 0f;

        float t = 0f;
        while (t < transitionSeconds)
        {
            t += Time.deltaTime;
            float k = SmoothStep01(Mathf.Clamp01(t / transitionSeconds));
            ApplyAngle(Mathf.Lerp(fromAngle, toAngle, k));
            yield return null;
        }

        ApplyAngle(toAngle);
        animateRoutine = null;
    }
}
