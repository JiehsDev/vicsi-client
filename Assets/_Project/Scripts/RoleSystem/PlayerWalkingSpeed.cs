using Oculus.Interaction.Locomotion;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Headset-local walking preference composed with the camera's temporary slowdown.</summary>
public sealed class PlayerWalkingSpeed : MonoBehaviour
{
    public const string PreferenceKey = "ViCSI.PlayerWalkingSpeed.Multiplier.v1";
    [SerializeField] private FirstPersonLocomotor locomotor;
    [SerializeField] private float minimumMultiplier = .5f;
    [SerializeField] private float maximumMultiplier = 2f;
    [SerializeField] private float speedStep = .1f;
    private float baseWalking, baseRunning, baseCrouching, aimMultiplier = 1f;
    public float CurrentMultiplier { get; private set; } = 1f;
    public float SpeedStep => speedStep;
    public bool UsesPreferredSpeed => PlayerPrefs.HasKey(PreferenceKey);
    public FirstPersonLocomotor Locomotor => locomotor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetBootstrap() { SceneManager.sceneLoaded -= SceneLoaded; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap() { SceneManager.sceneLoaded += SceneLoaded; EnsureOwner(); }
    private static void SceneLoaded(Scene scene, LoadSceneMode mode) => EnsureOwner();
    public static PlayerWalkingSpeed EnsureOwner()
    {
        var mover = Object.FindAnyObjectByType<FirstPersonLocomotor>();
        return mover != null ? EditableSceneVisuals.Component<PlayerWalkingSpeed>(mover.gameObject) : null;
    }
    private void Awake()
    {
        if (locomotor == null) locomotor = GetComponent<FirstPersonLocomotor>();
        if (locomotor == null) return;
        baseWalking = locomotor.SpeedFactor;
        baseRunning = locomotor.RunningSpeedFactor;
        baseCrouching = locomotor.CrouchSpeedFactor;
        float saved = PlayerPrefs.GetFloat(PreferenceKey, 1f);
        CurrentMultiplier = float.IsNaN(saved) || float.IsInfinity(saved) ? 1f : Mathf.Clamp(saved, minimumMultiplier, maximumMultiplier);
        Apply();
    }
    public bool AdjustSpeed(float delta) => SetPreferredSpeed(CurrentMultiplier + delta);
    public bool SetPreferredSpeed(float value)
    {
        if (locomotor == null || float.IsNaN(value) || float.IsInfinity(value)) return false;
        CurrentMultiplier = Mathf.Clamp(value, minimumMultiplier, maximumMultiplier);
        Apply();
        PlayerPrefs.SetFloat(PreferenceKey, CurrentMultiplier);
        PlayerPrefs.Save();
        return true;
    }
    public void ResetSpeed()
    {
        CurrentMultiplier = 1f;
        PlayerPrefs.DeleteKey(PreferenceKey);
        PlayerPrefs.Save();
        Apply();
    }
    public void SetAimMultiplier(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) return;
        aimMultiplier = Mathf.Clamp(value, .05f, 1f);
        Apply();
    }
    private void Apply()
    {
        if (locomotor == null) return;
        locomotor.SpeedFactor = baseWalking * CurrentMultiplier * aimMultiplier;
        locomotor.RunningSpeedFactor = baseRunning * CurrentMultiplier * aimMultiplier;
        locomotor.CrouchSpeedFactor = baseCrouching * CurrentMultiplier;
    }
    private void OnValidate()
    {
        minimumMultiplier = Mathf.Clamp(minimumMultiplier, .1f, 1f);
        maximumMultiplier = Mathf.Max(1f, minimumMultiplier, maximumMultiplier);
        speedStep = Mathf.Clamp(speedStep, .01f, .5f);
    }
}
