using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class AudioSettingsTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    readonly StringBuilder report = new("=== AudioSettingsTest ===\n");
    int failures;
    void Check(string name, bool pass) { report.AppendLine((pass ? "PASS: " : "FAIL: ") + name); if (!pass) failures++; }
    public void RunAllTests() { AutomatedTestSandbox.Activate(nameof(AudioSettingsTest)); DontDestroyOnLoad(gameObject); StartCoroutine(Run()); }
    static void Isolate()
    {
        AutomatedTestSandbox.Activate(nameof(AudioSettingsTest));
        var pre = Object.FindAnyObjectByType<PreSessionFlowController>();
        if (pre != null) { pre.enabled = false; PlayerUIGate.Exit(typeof(PreSessionFlowController).GetField("uiGateToken", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pre)); }
        Object.FindAnyObjectByType<StudentIdentificationPanel>(FindObjectsInactive.Include)?.Hide();
        RelationshipReviewTestUtil.BeginCaseFixture();
        if (NarrativeEventManager.Instance != null) { NarrativeEventManager.Instance.enabled = false; NarrativeEventManager.Instance.StopAllCoroutines(); }
    }
    static void Click(SettingsPauseMenu menu, string id)
    {
        var target = menu.Pointer.Targets.First(t => t.id == "settings-" + id);
        menu.ResponsiveLayout.EnsureVisible(target.rect);
        var point = menu.Pointer.WorldCentre(target.id);
        menu.Pointer.ClickWithRay(new Ray(point - target.rect.forward * .5f, target.rect.forward));
    }
    static bool Bus(string name, float value) => GameAudioSettings.Mixer.GetFloat(name, out float actual) && Mathf.Abs(actual - value) < .02f;
    static string Display(SettingsPauseMenu menu, string name) => menu.Panel.GetComponentsInChildren<TMP_Text>(true).First(t => t.name == name).text;
    static void ClearPlacementFixture(SettingsPauseMenu menu)
    {
        // As in SettingsPauseMenuTest: desktop diagnostics have no tracked headset pose.
        var go = new GameObject("Audio settings placement camera", typeof(Camera));
        var camera = go.GetComponent<Camera>(); camera.enabled = false; camera.transform.position = new Vector3(40, 1.7f, 40);
        typeof(HudFollowCamera).GetField("targetCamera", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(menu.Panel.GetComponent<HudFollowCamera>(), camera);
    }
    IEnumerator Run()
    {
        bool hadAmbience = PlayerPrefs.HasKey(GameAudioSettings.AmbiencePreference), hadSfx = PlayerPrefs.HasKey(GameAudioSettings.SfxPreference);
        float oldAmbience = PlayerPrefs.GetFloat(GameAudioSettings.AmbiencePreference), oldSfx = PlayerPrefs.GetFloat(GameAudioSettings.SfxPreference);
        SettingsPauseMenu menu = null;
        try
        {
            yield return null; Isolate(); yield return null;
            menu = FindAnyObjectByType<SettingsPauseMenu>();
            Check("Mixer and existing in-game Settings exist", GameAudioSettings.Mixer != null && menu != null);
            if (GameAudioSettings.Mixer == null || menu == null) { Finish(); yield break; }
            GameAudioSettings.ResetVolumes(); ClearPlacementFixture(menu); menu.Show(); yield return new WaitForSecondsRealtime(.3f);
            Check("Settings is readable and accepts input at a clear tracked-pose fixture", !menu.Panel.GetComponent<HudFollowCamera>().IsLayoutSuppressed && PlayerUIGate.CanUsePanel(menu));
            Check("In-game Settings contains all five audio controls", new[] { "ambience-minus", "ambience-plus", "sfx-minus", "sfx-plus", "audio-reset" }.All(id => menu.Pointer.Targets.Any(t => t.id == "settings-" + id)));
            Check("Both labels display the default 100%", Display(menu, "ambience-value") == "100%" && Display(menu, "sfx-value") == "100%");
            Check("Default bus gain preserves authored mix", Bus(GameAudioSettings.AmbienceParameter, 0) && Bus(GameAudioSettings.SfxParameter, 0));
            Click(menu, "ambience-minus");
            Check("Real pointer lowers ambience to 90% and saves immediately", Mathf.Abs(GameAudioSettings.AmbienceVolume - .9f) < .001f && Display(menu, "ambience-value") == "90%" && PlayerPrefs.HasKey(GameAudioSettings.AmbiencePreference));
            Check("Ambience control changes only its mixer bus", Bus(GameAudioSettings.AmbienceParameter, GameAudioSettings.Decibels(.9f)) && Bus(GameAudioSettings.SfxParameter, 0));
            yield return null; Click(menu, "sfx-minus");
            Check("Real pointer independently lowers SFX to 90%", Mathf.Abs(GameAudioSettings.SfxVolume - .9f) < .001f && Display(menu, "sfx-value") == "90%" && Bus(GameAudioSettings.SfxParameter, GameAudioSettings.Decibels(.9f)));
            yield return null; Click(menu, "ambience-plus");
            Check("Ambience plus restores 100% without resetting SFX", Display(menu, "ambience-value") == "100%" && Display(menu, "sfx-value") == "90%");
            GameAudioSettings.SetAmbience(-5); GameAudioSettings.SetSfx(5);
            Check("Volume clamps independently to 0-100%", GameAudioSettings.AmbienceVolume == 0 && GameAudioSettings.SfxVolume == 1);
            Check("0% mutes the ambience bus while effects remain audible", Bus(GameAudioSettings.AmbienceParameter, -80) && Bus(GameAudioSettings.SfxParameter, 0));
            GameAudioSettings.SetAmbience(.5f); GameAudioSettings.SetSfx(0);
            Check("0% SFX leaves the ambience bus independent", Bus(GameAudioSettings.SfxParameter, -80) && Bus(GameAudioSettings.AmbienceParameter, GameAudioSettings.Decibels(.5f)));
            Check("Non-finite volumes are rejected without changing preferences", !GameAudioSettings.SetAmbience(float.NaN) && !GameAudioSettings.SetSfx(float.PositiveInfinity) && GameAudioSettings.AmbienceVolume == .5f && GameAudioSettings.SfxVolume == 0);
            GameAudioSettings.RouteSceneSources();
            var sources = FindObjectsByType<AudioSource>(FindObjectsInactive.Include);
            var ambience = FindAnyObjectByType<SceneAmbience>();
            Check("All three ambient sources use only the ambience bus", ambience.GetComponentsInChildren<AudioSource>().All(s => s.outputAudioMixerGroup != null && s.outputAudioMixerGroup.name == "Ambience"));
            Check("Active and holstered SFX sources use the SFX bus", sources.Where(s => s.GetComponentInParent<SceneAmbience>() == null).All(s => s.outputAudioMixerGroup != null && s.outputAudioMixerGroup.name == "SFX"));
            float baseVolume = ambience.Exterior.volume;
            GameAudioSettings.SetAmbience(.25f); GameAudioSettings.SetAmbience(.25f);
            Check("Repeated changes do not compound source volume", ambience.Exterior.volume == baseVolume && Bus(GameAudioSettings.AmbienceParameter, GameAudioSettings.Decibels(.25f)));
            menu.Close(); menu.Show(); yield return null;
            Check("Reopened menu displays saved volume values", Display(menu, "ambience-value") == "25%" && Display(menu, "sfx-value") == "0%");
            yield return null; Click(menu, "audio-reset");
            Check("Reset restores both buses and clears preferences", GameAudioSettings.AmbienceVolume == 1 && GameAudioSettings.SfxVolume == 1 && !PlayerPrefs.HasKey(GameAudioSettings.AmbiencePreference) && !PlayerPrefs.HasKey(GameAudioSettings.SfxPreference) && Bus(GameAudioSettings.AmbienceParameter, 0) && Bus(GameAudioSettings.SfxParameter, 0));
            Check("Reset refreshes both percentage labels", Display(menu, "ambience-value") == "100%" && Display(menu, "sfx-value") == "100%");
            GameAudioSettings.SetAmbience(.3f); GameAudioSettings.SetSfx(.6f); menu.Close();
            yield return SceneManager.LoadSceneAsync("Assets/_Project/Scenes/Scenarios/CSI_Environment.unity");
            yield return null; Isolate(); yield return null;
            menu = FindAnyObjectByType<SettingsPauseMenu>(); ClearPlacementFixture(menu); menu.Show(); yield return new WaitForSecondsRealtime(.3f);
            Check("Scenario reload retains both saved headset volumes", Display(menu, "ambience-value") == "30%" && Display(menu, "sfx-value") == "60%" && Bus(GameAudioSettings.AmbienceParameter, GameAudioSettings.Decibels(.3f)) && Bus(GameAudioSettings.SfxParameter, GameAudioSettings.Decibels(.6f)));
            Check("Reload does not duplicate ambience", FindObjectsByType<SceneAmbience>().Length == 1);
            Check("Audio controls do not change deduction weights", FinalDeductionAssessment.Rules.checkpointWeight == 60 && FinalDeductionAssessment.Rules.relationshipWeight == 25 && FinalDeductionAssessment.Rules.conclusionWeight == 15);
            CaptureMenu(menu);
        }
        finally
        {
            if (menu != null) menu.Close();
            if (hadAmbience) PlayerPrefs.SetFloat(GameAudioSettings.AmbiencePreference, oldAmbience); else PlayerPrefs.DeleteKey(GameAudioSettings.AmbiencePreference);
            if (hadSfx) PlayerPrefs.SetFloat(GameAudioSettings.SfxPreference, oldSfx); else PlayerPrefs.DeleteKey(GameAudioSettings.SfxPreference);
            PlayerPrefs.Save(); GameAudioSettings.Apply();
        }
        Finish();
    }
    static void CaptureMenu(SettingsPauseMenu menu)
    {
        menu.ResponsiveLayout.EnsureVisible(menu.Pointer.Targets.First(t => t.id == "settings-sfx-plus").rect);
        Canvas.ForceUpdateCanvases();
        var go = new GameObject("Audio settings preview", typeof(Camera)); var camera = go.GetComponent<Camera>();
        camera.transform.SetPositionAndRotation(menu.Panel.transform.position - menu.Panel.transform.forward * 1.1f, menu.Panel.transform.rotation);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .03f, .04f); camera.fieldOfView = 42;
        var target = new RenderTexture(900, 900, 24); var image = new Texture2D(900, 900, TextureFormat.RGB24, false); var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 900, 900), 0, 0); image.Apply();
            System.IO.File.WriteAllBytes("docs/evidence/audio-settings-2026-10-22/settings-preview.png", image.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; camera.targetTexture = null; target.Release(); Destroy(target); Destroy(image); Destroy(go); }
    }
    void Finish() { report.AppendLine(failures == 0 ? "=== PASS, 0 failures ===" : "=== FAIL: " + failures + " failure(s) ==="); Report = report.ToString(); IsDone = true; }
}
