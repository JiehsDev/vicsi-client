using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

/// <summary>Headset-local bus volumes; authored source balance and dialogue ducking remain independent.</summary>
public static class GameAudioSettings
{
    public const string AmbiencePreference = "ViCSI.Audio.AmbienceVolume.v1";
    public const string SfxPreference = "ViCSI.Audio.SfxVolume.v1";
    public const string AmbienceParameter = "AmbienceVolume";
    public const string SfxParameter = "SfxVolume";
    private static AudioMixer mixer;
    public static AudioMixer Mixer => mixer != null ? mixer : mixer = Resources.Load<AudioMixer>("GameAudioMixer");
    public static float AmbienceVolume => Read(AmbiencePreference);
    public static float SfxVolume => Read(SfxPreference);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetBootstrap() { mixer = null; SceneManager.sceneLoaded -= SceneLoaded; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap() { SceneManager.sceneLoaded += SceneLoaded; RouteSceneSources(); Apply(); }
    private static void SceneLoaded(Scene scene, LoadSceneMode mode) { RouteSceneSources(); Apply(); }
    private static float Read(string key)
    {
        float value = PlayerPrefs.GetFloat(key, 1);
        return float.IsNaN(value) || float.IsInfinity(value) ? 1 : Mathf.Clamp01(value);
    }
    public static bool SetAmbience(float value) => Set(AmbiencePreference, value);
    public static bool SetSfx(float value) => Set(SfxPreference, value);
    private static bool Set(string key, float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) return false;
        PlayerPrefs.SetFloat(key, Mathf.Clamp01(value)); PlayerPrefs.Save(); Apply(); return true;
    }
    public static void ResetVolumes()
    {
        PlayerPrefs.DeleteKey(AmbiencePreference); PlayerPrefs.DeleteKey(SfxPreference); PlayerPrefs.Save(); Apply();
    }
    public static float Decibels(float volume) => volume <= 0 ? -80 : Mathf.Max(-80, 20 * Mathf.Log10(Mathf.Clamp01(volume)));
    public static void Apply()
    {
        if (Mixer == null) return;
        Mixer.SetFloat(AmbienceParameter, Decibels(AmbienceVolume));
        Mixer.SetFloat(SfxParameter, Decibels(SfxVolume));
    }
    public static void RouteSceneSources()
    {
        foreach (var source in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include)) RouteSource(source);
    }
    public static void RouteSource(AudioSource source)
    {
        if (source == null || Mixer == null) return;
        if (source.outputAudioMixerGroup != null && source.outputAudioMixerGroup.audioMixer != Mixer) return;
        var groups = Mixer.FindMatchingGroups(source.GetComponentInParent<SceneAmbience>() != null ? "Ambience" : "SFX");
        if (groups.Length == 1) source.outputAudioMixerGroup = groups[0];
    }
}
