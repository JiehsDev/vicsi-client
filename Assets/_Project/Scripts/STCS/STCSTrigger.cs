// Assets/_Project/Scripts/STCS/STCSTrigger.cs
using System;
using UnityEngine;

/// <summary>
/// One STCS trigger moment. triggerId is matched manually against whatever
/// interaction script calls STCSManager.Instance.Fire(triggerId) - e.g.
/// "scene_entered", "evidence_014_photographed".
/// </summary>
[Serializable]
public class STCSTrigger
{
    public string triggerId;

    [Tooltip("Lines to play, as ViCSIDialogueCatalog ids (text and timing are edited in the catalog).")]
    public string[] lineIds;

    [Tooltip("Legacy: used only when lineIds is empty. Content now lives in ViCSIDialogueCatalog.")]
    public DialogueLinePool pool;
    public bool firedOnce;

    [Tooltip("If true, every line in the pool plays in order (distinct sequential teammate lines) instead of one random line.")]
    public bool playAllLinesInSequence;
}
