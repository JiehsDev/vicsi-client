// Assets/_Project/Scripts/SceneTurnover/FirstResponderHandoff.cs
using UnityEngine;

/// <summary>
/// Presentation only: the First Responder holds the scene notes (the Recorder's clipboard
/// model) until the scene is turned over, then the item disappears from their hand - as if
/// handed to the investigator. Not evidence, not scored, read by no logic. Lives on the NPC
/// root beside FirstResponderTurnover; same turnover hook as SceneCordon.
/// </summary>
public class FirstResponderHandoff : MonoBehaviour
{
    [Tooltip("The item held by the First Responder (e.g. the clipboard parented to their hand bone). Hidden at turnover.")]
    [SerializeField] private GameObject heldItem;

    /// <summary>Whether the First Responder is still holding the item (turnover not done).</summary>
    public bool IsHolding => heldItem != null && heldItem.activeSelf;

    private void Start()
    {
        if (SceneTurnover.IsPending) SceneTurnover.OnCompleted += HandleSceneTurnedOver;
        else HandleSceneTurnedOver();
    }

    private void OnDestroy() => SceneTurnover.OnCompleted -= HandleSceneTurnedOver;

    private void HandleSceneTurnedOver()
    {
        SceneTurnover.OnCompleted -= HandleSceneTurnedOver;
        if (heldItem != null) heldItem.SetActive(false);
    }
}
