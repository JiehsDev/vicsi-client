// Assets/Editor/SceneLightingRealismSetup.cs
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// One-click realism pass for the open scene: makes sure RenderSettings.sun points at the
/// scene's directional light (needed for the skybox sun disc and ambient probe to line up
/// with the actual light direction), gives every non-directional light that currently has
/// shadows set to None a soft shadow so it looks right the moment it's switched on, and adds
/// a scene-local post-processing Volume with a subtle Color Adjustments grade (existing global
/// SampleSceneProfile already handles Bloom/Vignette/Tonemapping - this only tones down the
/// default flat/oversaturated look a bit further for this scene). Tune the profile's
/// Color Adjustments values in the Inspector afterward and check it under an HMD - this can't
/// be previewed headlessly.
/// </summary>
public static class SceneLightingRealismSetup
{
    private const string ProfileFolder = "Assets/_Project/Art/PostProcessing";
    private const string ProfileAssetName = "CSI_Environment_LightingProfile.asset";
    private const string VolumeObjectName = "PostProcessing_Realism";

    [MenuItem("Tools/VICSI/Improve Scene Lighting Realism")]
    public static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();
        int shadowsFixed = FixSunAndShadows();
        bool volumeCreated = EnsureRealismVolume();

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[SceneLightingRealismSetup] Sun assigned, {shadowsFixed} light(s) given soft shadows, " +
                   $"realism Volume {(volumeCreated ? "created" : "already present")}.");
    }

    private static int FixSunAndShadows()
    {
        var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int shadowsFixed = 0;

        foreach (var light in lights)
        {
            if (light.type == LightType.Directional && RenderSettings.sun == null)
            {
                RenderSettings.sun = light;
            }

            if (light.type != LightType.Directional && light.shadows == LightShadows.None)
            {
                Undo.RecordObject(light, "Enable Soft Shadows");
                light.shadows = LightShadows.Soft;
                shadowsFixed++;
            }
        }

        return shadowsFixed;
    }

    private static bool EnsureRealismVolume()
    {
        var existing = GameObject.Find(VolumeObjectName);
        if (existing != null && existing.GetComponent<Volume>() != null)
        {
            return false;
        }

        if (!Directory.Exists(ProfileFolder))
        {
            Directory.CreateDirectory(ProfileFolder);
            AssetDatabase.Refresh();
        }

        string profilePath = $"{ProfileFolder}/{ProfileAssetName}";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var colorAdjustments = profile.Add<ColorAdjustments>(true);
            colorAdjustments.contrast.overrideState = true;
            colorAdjustments.contrast.value = 10f;
            colorAdjustments.saturation.overrideState = true;
            colorAdjustments.saturation.value = -8f;
            AssetDatabase.CreateAsset(profile, profilePath);
        }

        var volumeObject = existing != null ? existing : new GameObject(VolumeObjectName);
        Undo.RegisterCreatedObjectUndo(volumeObject, "Create Realism Volume");
        var volume = volumeObject.GetComponent<Volume>();
        if (volume == null)
        {
            volume = volumeObject.AddComponent<Volume>();
        }
        volume.isGlobal = true;
        volume.priority = 1;
        volume.sharedProfile = profile;

        return true;
    }
}
