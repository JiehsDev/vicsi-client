#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System.Collections;
using Oculus.Interaction.Input;
using Oculus.Interaction.UnityXR;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;

/// <summary>
/// Connects Meta XR Operator's OpenXR inputs to the OVR-based rig in the isolated
/// .playtest app. It supplies input only; gameplay, collision and scoring stay in charge.
/// Excluded from release players and never enabled in the research application.
/// </summary>
public sealed class QuestRemoteInputBridge : MonoBehaviour
{
    private OVRCameraRig rig;
    private InputAction leftPose, rightPose;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (!Application.identifier.EndsWith(".competitive.playtest")) return;
        var host = new GameObject("Quest Remote Input Bridge (playtest only)");
        DontDestroyOnLoad(host);
        host.AddComponent<QuestRemoteInputBridge>();
    }

    private void Awake()
    {
        leftPose = new InputAction("Remote left grip", binding: "<XRController>{LeftHand}/devicePose");
        rightPose = new InputAction("Remote right grip", binding: "<XRController>{RightHand}/devicePose");
        leftPose.Enable();
        rightPose.Enable();
        SceneManager.sceneLoaded += Loaded;
        StartCoroutine(Connect());
    }

    private void Loaded(Scene scene, LoadSceneMode mode) => StartCoroutine(Connect());

    private IEnumerator Connect()
    {
        // Let the existing rig finish configuring its controller data sources.
        yield return null;
        yield return null;
        if (rig != null) rig.UpdatedAnchors -= SyncAnchors;
        rig = FindFirstObjectByType<OVRCameraRig>();
        if (rig == null) yield break;
        foreach (var controller in FindObjectsByType<Controller>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!(controller.ModifyDataFromSource is FromOVRControllerDataSource)) continue;
            var config = controller.GetData().Config;
            if (config == null || config.TrackingToWorldTransformer == null) continue;
            var host = new GameObject("Remote OpenXR " + config.Handedness);
            host.SetActive(false);
            host.transform.SetParent(controller.transform, false);
            var source = host.AddComponent<FromUnityXRControllerDataSource>();
            source.InjectHandedness(config.Handedness);
            source.InjectTrackingToWorldTransformer(config.TrackingToWorldTransformer);
            controller.enabled = false;
            controller.InjectModifyDataFromSource(source);
            controller.InjectUpdateAfter(source);
            host.SetActive(true);
            controller.enabled = true;
            Debug.Log("[RemoteInputBridge] Connected " + config.Handedness);
        }
        rig.UpdatedAnchors += SyncAnchors;
    }

    private void SyncAnchors(OVRCameraRig cameraRig)
    {
        Sync(leftPose, cameraRig.leftHandAnchor);
        Sync(rightPose, cameraRig.rightHandAnchor);
    }

    private static void Sync(InputAction action, Transform anchor)
    {
        if (anchor == null || action.activeControl == null) return;
        var pose = action.ReadValue<PoseState>();
        if (pose.isTracked) anchor.SetLocalPositionAndRotation(pose.position, pose.rotation);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= Loaded;
        if (rig != null) rig.UpdatedAnchors -= SyncAnchors;
        leftPose?.Dispose();
        rightPose?.Dispose();
    }
}
#endif
