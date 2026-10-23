// Assets/_Project/Scripts/ScenePhotography/ScenePhotoPhase.cs

/// <summary>
/// Broad stage of scene (location) photography, outside-in. Explicit values: append new
/// phases at the end, never renumber - the names are logged and may be persisted.
/// Evidence close-ups are deliberately not a phase here; they belong to PhotographTool's
/// evidence flow.
/// </summary>
public enum ScenePhotoPhase
{
    Exterior = 0,
    Entrance = 1,
    Overall = 2,
    Context = 3
}
