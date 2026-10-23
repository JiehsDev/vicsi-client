using UnityEngine;

/// <summary>Front-door collision for the outdoor SOCO photo route. IOC access is unchanged.</summary>
[DisallowMultipleComponent]
public sealed class HousePhotographyEntryGate : MonoBehaviour
{
    [Tooltip("Editable invisible doorway wall. Enabled only while outdoor SOCO photographs are outstanding.")]
    [SerializeField] private BoxCollider entranceWall;

    public BoxCollider EntranceWall => entranceWall;
    public bool IsBlocking => SceneTurnover.IsGated
        && SceneTurnover.HasReachedPhase(ScenarioPhase.SOCOInvestigation)
        && !OutdoorPhotographyComplete;

    public bool OutdoorPhotographyComplete
    {
        get
        {
            var photos = ScenePhotographyManager.Instance;
            if (photos == null) return false;
            bool any = false;
            foreach (var point in photos.Points)
            {
                if (!point.isRequired || (point.phase != ScenePhotoPhase.Exterior && point.phase != ScenePhotoPhase.Entrance)) continue;
                any = true;
                if (!point.IsCompleted) return false;
            }
            return any;
        }
    }

    private void OnEnable()
    {
        SceneTurnover.OnPhaseChanged += PhaseChanged;
        ScenePhotographyManager.OnScenePhotoProgress += PhotoChanged;
        Refresh();
    }
    private void OnDisable()
    {
        SceneTurnover.OnPhaseChanged -= PhaseChanged;
        ScenePhotographyManager.OnScenePhotoProgress -= PhotoChanged;
        if (entranceWall != null) entranceWall.enabled = false;
    }
    private void PhaseChanged(ScenarioPhase _) => Refresh();
    private void PhotoChanged(string _) => Refresh();
    private void Update() => Refresh(); // Also covers manager startup and restored/development route state.
    public void Refresh()
    {
        if (entranceWall != null) entranceWall.enabled = isActiveAndEnabled && IsBlocking;
    }
}
