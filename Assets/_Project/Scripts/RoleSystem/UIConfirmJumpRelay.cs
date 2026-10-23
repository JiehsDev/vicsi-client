using Oculus.Interaction;
using Oculus.Interaction.Locomotion;
using UnityEngine;
using UnityEngine.Events;

/// <summary>Preserves Meta's jump source, but resolves its request after all UI Updates.</summary>
[DefaultExecutionOrder(32000)]
public sealed class UIConfirmJumpRelay : MonoBehaviour
{
    private LocomotionActionsBroadcaster broadcaster;
    private ActiveStateUnityEventWrapper source;
    private int requestedFrame = -1;
    public int ForwardedCount { get; private set; }
    public int SuppressedCount { get; private set; }

    public static void Install(ActiveStateUnityEventWrapper wrapper)
    {
        var evt = wrapper.WhenActivated;
        for (int i = 0; i < evt.GetPersistentEventCount(); i++)
        {
            if (evt.GetPersistentMethodName(i) != "Jump"
                || evt.GetPersistentTarget(i) is not LocomotionActionsBroadcaster target) continue;
            if (wrapper.GetComponent<UIConfirmJumpRelay>() != null) return;
            var relay = wrapper.gameObject.AddComponent<UIConfirmJumpRelay>();
            relay.broadcaster = target; relay.source = wrapper;
            // Runtime-only: leave the package prefab and all other event listeners intact.
            evt.SetPersistentListenerState(i, UnityEventCallState.Off);
            evt.AddListener(relay.RequestJump);
            return;
        }
    }
    public void RequestJump() => requestedFrame = Time.frameCount;
    private void LateUpdate()
    {
        // Poll even if the Jump object was suspended: PlayerUIGate also polls on every UI read.
        bool blocked = PlayerUIGate.SuppressGameplayJump;
        if (requestedFrame != Time.frameCount) return;
        requestedFrame = -1;
        if (blocked) { SuppressedCount++; return; }
        ForwardedCount++; broadcaster.Jump();
    }
    private void OnDisable() => requestedFrame = -1;
    private void OnDestroy() { if (source != null) source.WhenActivated.RemoveListener(RequestJump); }
}
