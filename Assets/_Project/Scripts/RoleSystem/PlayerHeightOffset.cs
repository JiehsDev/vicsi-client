using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Local comfort offset over floor tracking: head and hands translate together, never scale.</summary>
public class PlayerHeightOffset : MonoBehaviour
{
    public const string PreferenceKey = "ViCSI.PlayerHeight.Target.v1";
    [SerializeField] private OVRCameraRig cameraRig;
    [SerializeField] private float minimumHeight = 1.40f;
    [SerializeField] private float maximumHeight = 2.00f;
    [SerializeField] private float defaultHeight = 1.70f;
    [SerializeField] private float heightStep = .10f;
    [SerializeField] private float heightOffset;
    private float calibratedHeight;
    private bool calibrated;
    private float originY;
    public float HeightOffset { get => heightOffset; set { heightOffset = value; Apply(); } }
    public float CurrentHeight => calibratedHeight + heightOffset;
    public float MinimumHeight => minimumHeight;
    public float MaximumHeight => maximumHeight;
    public float DefaultHeight => defaultHeight;
    public float HeightStep => heightStep;
    public bool UsesPreferredHeight => PlayerPrefs.HasKey(PreferenceKey);
    public OVRCameraRig Rig => cameraRig;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetBootstrap() { SceneManager.sceneLoaded -= SceneLoaded; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap() { SceneManager.sceneLoaded += SceneLoaded; EnsureHeightOwner(); }
    private static void SceneLoaded(Scene scene, LoadSceneMode mode) => EnsureHeightOwner();
    private static void EnsureHeightOwner()
    {
        var rig = Object.FindAnyObjectByType<OVRCameraRig>();
        if (rig != null && Object.FindAnyObjectByType<PlayerHeightOffset>() == null)
            rig.gameObject.AddComponent<PlayerHeightOffset>();
    }
    private void Awake()
    {
        if (cameraRig == null) cameraRig = Object.FindAnyObjectByType<OVRCameraRig>();
        calibratedHeight = defaultHeight;
        originY = cameraRig != null && cameraRig.trackingSpace != null ? cameraRig.trackingSpace.localPosition.y : 0;
        // Floor calibration is the default; supersede the old authored -0.4 offset.
        heightOffset = UsesPreferredHeight ? Mathf.Clamp(PlayerPrefs.GetFloat(PreferenceKey, defaultHeight), minimumHeight, maximumHeight) - calibratedHeight : 0;
        Apply();
    }
    private void LateUpdate()
    {
        // First reliable pose only: never counteract normal crouching/head movement.
        if (calibrated || cameraRig == null || !OVRManager.isHmdPresent
            || !OVRPlugin.GetNodePositionTracked(OVRPlugin.Node.EyeCenter)) return;
        float physicalHeight = cameraRig.trackingSpace.InverseTransformPoint(cameraRig.centerEyeAnchor.position).y;
        if (physicalHeight < .5f || physicalHeight > 2.5f) return;
        calibrated = true;
        calibratedHeight = physicalHeight;
        heightOffset = UsesPreferredHeight ? Mathf.Clamp(PlayerPrefs.GetFloat(PreferenceKey, defaultHeight), minimumHeight, maximumHeight) - calibratedHeight : 0;
        Apply();
    }
    public bool AdjustHeight(float delta) => SetPreferredHeight(CurrentHeight + delta);
    public bool SetPreferredHeight(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) return false;
        float target = Mathf.Clamp(value, minimumHeight, maximumHeight);
        float newOffset = target - calibratedHeight;
        if (!HasHeadClearance(newOffset - heightOffset)) return false;
        heightOffset = newOffset;
        Apply();
        PlayerPrefs.SetFloat(PreferenceKey, target);
        PlayerPrefs.Save();
        return true;
    }
    public bool ResetHeight()
    {
        if (!HasHeadClearance(-heightOffset)) return false;
        PlayerPrefs.DeleteKey(PreferenceKey);
        PlayerPrefs.Save();
        heightOffset = 0;
        Apply();
        return true;
    }
    private bool HasHeadClearance(float rise)
    {
        if (rise <= 0 || cameraRig == null || cameraRig.centerEyeAnchor == null) return true;
        foreach (var hit in Physics.SphereCastAll(cameraRig.centerEyeAnchor.position, .12f, Vector3.up, rise, ~0, QueryTriggerInteraction.Ignore))
            if (!hit.transform.IsChildOf(cameraRig.transform.root) && hit.collider.GetComponentInParent<EvidenceProp>() == null)
                return false;
        return true;
    }
    private void Apply()
    {
        if (cameraRig == null || cameraRig.trackingSpace == null) return;
        var position = cameraRig.trackingSpace.localPosition;
        position.y = originY + heightOffset;
        cameraRig.trackingSpace.localPosition = position;
    }
    private void OnValidate()
    {
        minimumHeight = Mathf.Clamp(minimumHeight, 1f, 2.5f);
        maximumHeight = Mathf.Max(minimumHeight, maximumHeight);
        defaultHeight = Mathf.Clamp(defaultHeight, minimumHeight, maximumHeight);
        heightStep = Mathf.Clamp(heightStep, .01f, .1f);
    }
}
