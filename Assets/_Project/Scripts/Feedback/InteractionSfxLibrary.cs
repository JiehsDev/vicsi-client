using UnityEngine;

public enum InteractionSound { Select, Confirm, Blocked, Shutter, Switch, Measure, Chalk, Writing, Paper, Page, Tape, Container, Place }

[CreateAssetMenu(menuName = "ViCSI/Interaction SFX Library")]
public sealed class InteractionSfxLibrary : ScriptableObject
{
    [Tooltip("Neutral action sounds; these never represent answer correctness.")]
    public AudioClip select, confirm, blocked, shutter, switchClick, measure, chalk, writing, paper, page, tape, container, place;
    public AudioClip[] concreteSteps, woodSteps, grassSteps;
    [Range(0,1)] public float uiVolume = .16f;
    [Range(0,1)] public float physicalVolume = .3f;
    [Range(0,1)] public float footstepVolume = .16f;
    public AudioClip Clip(InteractionSound sound) => sound switch
    {
        InteractionSound.Select=>select, InteractionSound.Confirm=>confirm, InteractionSound.Blocked=>blocked,
        InteractionSound.Shutter=>shutter, InteractionSound.Switch=>switchClick, InteractionSound.Measure=>measure,
        InteractionSound.Chalk=>chalk, InteractionSound.Writing=>writing, InteractionSound.Paper=>paper,
        InteractionSound.Page=>page, InteractionSound.Tape=>tape, InteractionSound.Container=>container,
        InteractionSound.Place=>place, _=>null
    };
}
