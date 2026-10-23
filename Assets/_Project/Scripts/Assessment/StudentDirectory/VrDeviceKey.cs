// Assets/_Project/Scripts/Assessment/StudentDirectory/VrDeviceKey.cs
using System;
using System.IO;
using UnityEngine;

/// <summary>
/// The headset's device credential (the dashboard's VR_DEVICE_SYNC_KEY), used as a
/// bearer token for every device-authenticated VR endpoint: student-directory,
/// validate-code and session-upload-token. One loader for all of them.
///
/// Never stored in an asset, a scene, PlayerPrefs, the student directory or the upload
/// queue, and never logged. Each headset reads it from
/// &lt;persistentDataPath&gt;/vicsi_vr_device_sync_key.txt (provisioned per device, e.g.
/// `adb push`). In the Editor only, a component may pass an override for local testing.
/// </summary>
public static class VrDeviceKey
{
    public const string FileName = "vicsi_vr_device_sync_key.txt";

    /// <summary>The key, or null if this device has none. editorOverride is honored only in the Editor.</summary>
    public static string Resolve(string editorOverride = null)
    {
        try
        {
            string path = Path.Combine(Application.persistentDataPath, FileName);
            if (File.Exists(path))
            {
                string key = File.ReadAllText(path).Trim();
                if (!string.IsNullOrEmpty(key))
                {
                    return key;
                }
            }
        }
        catch (Exception)
        {
            // Unreadable key file = no key; callers report NotConfigured.
        }
        return Application.isEditor && !string.IsNullOrWhiteSpace(editorOverride) ? editorOverride.Trim() : null;
    }
}
