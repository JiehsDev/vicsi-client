using UnityEngine;

/// <summary>Scene-owned stereo ambience, blended by the existing house footprint. No gameplay ownership.</summary>
[DisallowMultipleComponent]
public sealed class SceneAmbience : MonoBehaviour
{
    [SerializeField] private SceneCordon houseBoundary;
    [SerializeField] private AudioSource exterior;
    [SerializeField] private AudioSource interior;
    [SerializeField] private AudioSource treeRustle;
    [SerializeField] private AudioLowPassFilter exteriorFilter;
    [SerializeField] private AudioLowPassFilter treeFilter;
    [Header("Mix")]
    [SerializeField, Range(0, 1)] private float exteriorVolume = .22f;
    [SerializeField, Range(0, 1)] private float interiorVolume = .18f;
    [SerializeField, Range(0, 1)] private float treeVolume = .07f;
    [SerializeField, Range(0, 1)] private float exteriorLeakInside = .1f;
    [SerializeField, Min(.1f)] private float transitionSeconds = 1.8f;
    [SerializeField, Range(0, 1)] private float dialogueVolumeMultiplier = .55f;
    [SerializeField, Min(.1f)] private float duckSeconds = .35f;
    [SerializeField, Range(300, 22000)] private float indoorExteriorCutoff = 1200;

    public AudioSource Exterior => exterior;
    public AudioSource Interior => interior;
    public AudioSource TreeRustle => treeRustle;
    public float InsideBlend { get; private set; }
    public float DialogueGain { get; private set; } = 1;

    private void OnEnable()
    {
        var camera = Camera.main;
        InsideBlend = camera != null && houseBoundary != null && houseBoundary.Contains(camera.transform.position) ? 1 : 0;
        DialogueGain = 1;
        ApplyMix();
        StartTrack(exterior); StartTrack(interior); StartTrack(treeRustle);
    }
    private static void StartTrack(AudioSource source)
    {
        if (source != null && source.clip != null) { GameAudioSettings.RouteSource(source); source.loop = true; source.Play(); }
    }
    private void OnDisable()
    {
        StopTrack(exterior); StopTrack(interior); StopTrack(treeRustle);
    }
    private static void StopTrack(AudioSource source)
    {
        if (source != null) { source.Stop(); source.volume = 0; }
    }
    private void Update()
    {
        var camera = Camera.main;
        if (camera == null) return;
        bool dialogue = PlayerUIGate.IsBlocked || (STCSNotificationUI.Instance != null && STCSNotificationUI.Instance.IsShowing)
            || (NotificationManager.Instance != null && NotificationManager.Instance.UI != null && NotificationManager.Instance.UI.CurrentTooltip != null);
        Advance(camera.transform.position, dialogue, Time.unscaledDeltaTime);
    }

    /// <summary>Mix changes are independent of investigation phases, scoring and scaled-time pauses.</summary>
    public void Advance(Vector3 head, bool dialogue, float deltaTime)
    {
        float target = houseBoundary != null && houseBoundary.Contains(head) ? 1 : 0;
        InsideBlend = Mathf.Lerp(InsideBlend, target, 1 - Mathf.Exp(-Mathf.Max(0, deltaTime) / Mathf.Max(.1f, transitionSeconds)));
        DialogueGain = Mathf.Lerp(DialogueGain, dialogue ? dialogueVolumeMultiplier : 1,
            1 - Mathf.Exp(-Mathf.Max(0, deltaTime) / Mathf.Max(.1f, duckSeconds)));
        ApplyMix();
    }
    private void ApplyMix()
    {
        float outdoors = Mathf.Lerp(1, exteriorLeakInside, InsideBlend) * DialogueGain;
        if (exterior != null) exterior.volume = exteriorVolume * outdoors;
        if (treeRustle != null) treeRustle.volume = treeVolume * outdoors;
        if (interior != null) interior.volume = interiorVolume * InsideBlend * DialogueGain;
        float cutoff = Mathf.Lerp(22000, indoorExteriorCutoff, InsideBlend);
        if (exteriorFilter != null) exteriorFilter.cutoffFrequency = cutoff;
        if (treeFilter != null) treeFilter.cutoffFrequency = cutoff;
    }
}
