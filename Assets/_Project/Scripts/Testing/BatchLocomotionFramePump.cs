#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Batch-mode equivalent of the SDK's WaitForEndOfFrame origin sync, for preset diagnostics only.</summary>
[DefaultExecutionOrder(32000)]
public sealed class BatchLocomotionFramePump : MonoBehaviour
{
    private Action finishFrame;
    public static void Activate()
    {
        if (!Application.isBatchMode || !AutomatedTestSandbox.IsActive) return;
        SceneManager.sceneLoaded -= SceneLoaded;
        SceneManager.sceneLoaded += SceneLoaded;
        Install();
    }
    private static void SceneLoaded(Scene scene, LoadSceneMode mode) => Install();
    private static void Install()
    {
        if (!Application.isBatchMode || !AutomatedTestSandbox.IsActive) return;
        foreach (var locomotor in FindObjectsByType<Oculus.Interaction.Locomotion.FirstPersonLocomotor>())
            if (locomotor.GetComponent<BatchLocomotionFramePump>() == null)
            {
                var pump = locomotor.gameObject.AddComponent<BatchLocomotionFramePump>();
                var method = locomotor.GetType().GetMethod("LastUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
                if (method == null) throw new MissingMethodException("The SDK locomotor LastUpdate hook changed.");
                pump.finishFrame = (Action)Delegate.CreateDelegate(typeof(Action), locomotor, method);
            }
    }
    private void LateUpdate() => finishFrame?.Invoke();
}
#endif
