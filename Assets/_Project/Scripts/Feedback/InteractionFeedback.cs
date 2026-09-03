// Assets/_Project/Scripts/Feedback/InteractionFeedback.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Scene-wide acknowledgement feedback: a haptic pulse, a short tone, and an optional
/// scale pop, for actions that previously produced nothing a player could perceive.
/// Static entry point in the same shape as NotificationManager.Notify, so any script
/// can call InteractionFeedback.Confirm(...) without holding a reference, and calling
/// it in a scene that has no InteractionFeedback is a silent no-op rather than a
/// throw.
///
/// This is acknowledgement, not judgement, and the distinction is a design constraint
/// rather than a stylistic one. Two signals behave OPPOSITELY here and must not be
/// collapsed into one idea of "success vs failure":
///
///   PROCEDURAL COMPLIANCE - must be obvious. Acting out of order is a hard-gated,
///   transparent requirement, so a refusal should be immediately distinguishable from
///   an accepted action. That is the Blocked channel, and it deliberately differs from
///   Confirm in pitch, length and haptic pattern.
///
///   IDENTIFICATION CORRECTNESS - must stay hidden. Whether the object a player just
///   marked was really evidence is the thing being assessed, so it can never be fed
///   back during the run. Marking a decoy must be indistinguishable from marking the
///   murder weapon.
///
/// There are exactly two channels:
///
///   Confirm - "the game registered what you did." Fires whether or not what you did
///             was correct. Marking the designed distractor confirms exactly like
///             marking the murder weapon, because a scenario that felt different for
///             the two would be telling the student the answer.
///
///   Blocked - "the game refused to do that." Reserved for procedural refusals the
///             player must be able to tell apart from an action that simply didn't
///             land: skipping a required step, reclaiming a marker off already
///             documented evidence. Never used to signal a wrong identification.
///
/// Audio is generated procedurally at runtime rather than shipped as assets. The
/// project has no authored SFX (the only clips in it are XR Interaction Toolkit
/// sample assets, which a package update can remove), and a missing AudioClip
/// reference fails silently - exactly the failure mode this pass exists to remove.
/// Assign confirmClip/blockedClip to override with real audio when it exists.
/// </summary>
public class InteractionFeedback : MonoBehaviour
{
    public static InteractionFeedback Instance { get; private set; }

    [Header("Audio (optional — procedural tones are generated if left empty)")]
    [SerializeField] private AudioClip confirmClip;
    [SerializeField] private AudioClip blockedClip;
    [SerializeField, Range(0f, 1f)] private float volume = 0.5f;

    [Header("Haptics")]
    [Tooltip("Amplitude of the short pulse on a confirmed action.")]
    [SerializeField, Range(0f, 1f)] private float confirmAmplitude = 0.35f;
    [SerializeField] private float confirmDuration = 0.06f;

    [Tooltip("A blocked action pulses twice, harder, so it is distinguishable through a controller without looking.")]
    [SerializeField, Range(0f, 1f)] private float blockedAmplitude = 0.75f;
    [SerializeField] private float blockedDuration = 0.09f;

    [Header("Scale pop")]
    [SerializeField] private float pulseScale = 1.25f;
    [SerializeField] private float pulseSeconds = 0.18f;

    private AudioSource audioSource;

    /// <summary>
    /// A running scale pop, plus the scale the target had BEFORE it started.
    ///
    /// Storing the base scale is what makes a rapid repeat safe, and it is not
    /// optional: StartCoroutine runs the body up to its first yield synchronously, so
    /// a second Confirm in the same frame would otherwise capture the scale the first
    /// tween had ALREADY inflated and treat that as "normal". Five presses in a frame
    /// compounded to +12% and stayed there, because every tween restored faithfully -
    /// just to the wrong value.
    /// </summary>
    private class PulseState
    {
        public Vector3 BaseScale;
        public Coroutine Routine;
    }

    private readonly Dictionary<Transform, PulseState> activePulses = new();

    private void Awake()
    {
        Instance = this;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        if (confirmClip == null)
        {
            confirmClip = BuildTone("Tone_Confirm", 880f, 0.09f, 18f);
        }
        if (blockedClip == null)
        {
            blockedClip = BuildTone("Tone_Blocked", 165f, 0.18f, 7f);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// "Registered." Optionally pops <paramref name="pulseTarget"/>'s scale, e.g. the
    /// tent that was just placed.
    /// </summary>
    public static void Confirm(Transform pulseTarget = null)
    {
        if (Instance == null)
        {
            return;
        }

        Instance.Play(Instance.confirmClip);
        Instance.Pulse(Instance.confirmAmplitude, Instance.confirmDuration, 1);
        Instance.StartPulse(pulseTarget);
    }

    /// <summary>"Refused." A procedural refusal, never a wrong answer — see the class comment.</summary>
    public static void Blocked(Transform pulseTarget = null)
    {
        if (Instance == null)
        {
            return;
        }

        Instance.Play(Instance.blockedClip);
        Instance.Pulse(Instance.blockedAmplitude, Instance.blockedDuration, 2);
        Instance.StartPulse(pulseTarget);
    }

    private void Play(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            // PlayOneShot rather than Play so a quick repeat overlaps instead of
            // cutting the previous tone off mid-envelope.
            audioSource.PlayOneShot(clip, volume);
        }
    }

    private void Pulse(float amplitude, float duration, int count)
    {
        StartCoroutine(PulseRoutine(amplitude, duration, count));
    }

    private IEnumerator PulseRoutine(float amplitude, float duration, int count)
    {
        for (int i = 0; i < count; i++)
        {
            SendHaptic(XRNode.LeftHand, amplitude, duration);
            SendHaptic(XRNode.RightHand, amplitude, duration);

            if (i < count - 1)
            {
                yield return new WaitForSeconds(duration + 0.05f);
            }
        }
    }

    // Both hands, because most of these actions don't know which controller performed
    // them - the tent tool can be held in either, and a blocked transition can come
    // from the state machine with no controller involved at all. Guessing wrong is
    // worse than pulsing both.
    private static void SendHaptic(XRNode node, float amplitude, float duration)
    {
        var device = InputDevices.GetDeviceAtXRNode(node);
        if (device.isValid
            && device.TryGetHapticCapabilities(out var capabilities)
            && capabilities.supportsImpulse)
        {
            device.SendHapticImpulse(0u, amplitude, duration);
        }
    }

    private void StartPulse(Transform target)
    {
        if (target == null)
        {
            return;
        }

        Vector3 baseScale;

        if (activePulses.TryGetValue(target, out var running))
        {
            if (running.Routine != null)
            {
                StopCoroutine(running.Routine);
            }

            // Inherit the scale from before the interrupted tween, and snap back to it
            // now, so the restart begins from rest rather than from mid-arc.
            baseScale = running.BaseScale;
            target.localScale = baseScale;
        }
        else
        {
            baseScale = target.localScale;
        }

        var state = new PulseState { BaseScale = baseScale };
        activePulses[target] = state;
        state.Routine = StartCoroutine(ScalePulse(target, state));
    }

    private IEnumerator ScalePulse(Transform target, PulseState state)
    {
        float elapsed = 0f;

        while (elapsed < pulseSeconds && target != null)
        {
            elapsed += Time.deltaTime;
            // One up-and-back arc: sin over half a period peaks at the midpoint and
            // returns to exactly 1 at the end, so no separate settle step is needed.
            float arc = Mathf.Sin(Mathf.Clamp01(elapsed / pulseSeconds) * Mathf.PI);
            target.localScale = state.BaseScale * Mathf.LerpUnclamped(1f, pulseScale, arc);
            yield return null;
        }

        if (target == null)
        {
            yield break;
        }

        target.localScale = state.BaseScale;

        // Only clear the entry if it is still OURS - a newer pulse may have replaced
        // it while this one was being torn down.
        if (activePulses.TryGetValue(target, out var current) && current == state)
        {
            activePulses.Remove(target);
        }
    }

    /// <summary>
    /// A decaying sine, built once at Awake. Deliberately tiny and dependency-free:
    /// the point is that these cues cannot silently be absent because someone forgot
    /// to drag a clip into a slot.
    /// </summary>
    private static AudioClip BuildTone(string clipName, float frequency, float seconds, float decay)
    {
        const int SampleRate = 44100;
        int sampleCount = Mathf.Max(1, Mathf.RoundToInt(SampleRate * seconds));
        var samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)SampleRate;
            samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * Mathf.Exp(-decay * t);
        }

        var clip = AudioClip.Create(clipName, sampleCount, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
