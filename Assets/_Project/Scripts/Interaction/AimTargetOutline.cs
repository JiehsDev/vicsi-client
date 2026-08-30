// Assets/_Project/Scripts/Interaction/AimTargetOutline.cs
using UnityEngine;

/// <summary>
/// Draws a white wireframe box around whatever EvidenceProp a PhotographTool
/// is currently aimed at, so the player can see exactly what's in frame
/// before pressing the shutter. Builds its own LineRenderer marker at
/// runtime (a single 16-point path retracing 3 edges to cover all 12 cube
/// edges without needing multiple renderers), so it needs no prefab wiring
/// beyond a PhotographTool reference. Only touches PhotographTool's public
/// OnAimTargetChanged event, so it's reusable against any future aiming
/// tool exposing the same event shape.
/// </summary>
[RequireComponent(typeof(PhotographTool))]
public class AimTargetOutline : MonoBehaviour
{
    // Corner index path tracing all 12 edges of a box with the minimum
    // number of retraced edges (3), since a cube's vertex graph has 8
    // odd-degree vertices and an Eulerian path needs at most 2.
    private static readonly int[] EdgePath = { 0, 1, 2, 3, 0, 4, 5, 1, 5, 6, 2, 6, 7, 3, 7, 4 };

    [SerializeField] private PhotographTool photographTool;
    [SerializeField] private Color outlineColor = Color.white;
    [SerializeField] private float lineWidth = 0.01f;

    private LineRenderer line;
    private EvidenceProp currentTarget;
    private Renderer targetRenderer;
    private readonly Vector3[] corners = new Vector3[8];

    private void Awake()
    {
        if (photographTool == null)
        {
            photographTool = GetComponent<PhotographTool>();
        }

        var markerObject = new GameObject("AimOutlineMarker");
        markerObject.transform.SetParent(null);
        line = markerObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = false;
        line.positionCount = EdgePath.Length;
        line.widthMultiplier = lineWidth;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = outlineColor;
        line.endColor = outlineColor;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.enabled = false;
    }

    private void OnEnable()
    {
        if (photographTool != null)
        {
            photographTool.OnAimTargetChanged += HandleAimTargetChanged;
        }
    }

    private void OnDisable()
    {
        if (photographTool != null)
        {
            photographTool.OnAimTargetChanged -= HandleAimTargetChanged;
        }
    }

    private void OnDestroy()
    {
        if (line != null)
        {
            Destroy(line.gameObject);
        }
    }

    private void HandleAimTargetChanged(EvidenceProp target)
    {
        currentTarget = target;
        targetRenderer = target != null ? target.GetComponentInChildren<Renderer>() : null;

        bool show = targetRenderer != null;
        line.enabled = show;
        if (show)
        {
            UpdateOutlineToBounds(targetRenderer.bounds);
        }
    }

    private void LateUpdate()
    {
        if (currentTarget == null || targetRenderer == null)
        {
            return;
        }

        UpdateOutlineToBounds(targetRenderer.bounds);
    }

    private void UpdateOutlineToBounds(Bounds bounds)
    {
        Vector3 c = bounds.center;
        Vector3 e = bounds.extents;

        corners[0] = c + new Vector3(-e.x, -e.y, -e.z);
        corners[1] = c + new Vector3(e.x, -e.y, -e.z);
        corners[2] = c + new Vector3(e.x, e.y, -e.z);
        corners[3] = c + new Vector3(-e.x, e.y, -e.z);
        corners[4] = c + new Vector3(-e.x, -e.y, e.z);
        corners[5] = c + new Vector3(e.x, -e.y, e.z);
        corners[6] = c + new Vector3(e.x, e.y, e.z);
        corners[7] = c + new Vector3(-e.x, e.y, e.z);

        for (int i = 0; i < EdgePath.Length; i++)
        {
            line.SetPosition(i, corners[EdgePath[i]]);
        }
    }
}
