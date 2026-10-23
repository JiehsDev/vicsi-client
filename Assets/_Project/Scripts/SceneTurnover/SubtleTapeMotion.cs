using UnityEngine;

/// <summary>Millimetre-scale visual motion only; never changes cordon state or colliders.</summary>
public sealed class SubtleTapeMotion : MonoBehaviour
{
    [SerializeField] private Transform[] strips;
    private Quaternion[] rotations;
    private void Awake()
    {
        rotations = new Quaternion[strips.Length];
        for (int i = 0; i < strips.Length; i++)
            if (strips[i] != null) rotations[i] = strips[i].localRotation;
    }
    private void Update()
    {
        for (int i = 0; i < strips.Length; i++)
            if (strips[i] != null && strips[i].gameObject.activeInHierarchy)
                strips[i].localRotation = rotations[i] * Quaternion.Euler(
                    0f, Mathf.Sin(Time.time * .65f + i * 1.3f) * .15f, 0f);
    }
    private void OnDisable()
    {
        if (rotations == null) return;
        for (int i = 0; i < strips.Length; i++)
            if (strips[i] != null) strips[i].localRotation = rotations[i];
    }
}
