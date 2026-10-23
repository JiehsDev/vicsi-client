using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Raw-input fixture for UIComfortInputTest; never installed in an authored scene.</summary>
public sealed class UIComfortInputProbe : MonoBehaviour, Oculus.Interaction.IActiveState
{
    public bool Available;
    public int Count;
    public Action OnConfirm;
    public InputAction Action { get; private set; }
    public bool Active => Action != null && Action.IsPressed();
    public void Initialize(int priority)
    {
        Action = PlayerUIGate.CreateConfirmAction(this, name, () => Available, priority);
        Action.Enable();
    }
    private void Update()
    {
        if (PlayerUIGate.TryConsumeConfirm(this, Action)) { Count++; OnConfirm?.Invoke(); }
    }
    private void OnDestroy() { PlayerUIGate.Exit(this); Action?.Dispose(); }
}
