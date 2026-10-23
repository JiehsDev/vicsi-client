// Assets/_Project/Scripts/Interaction/ChalkGuideSegmentLink.cs
using UnityEngine;

/// <summary>
/// Sits on one chalk-guide "Segment_i" LineRenderer alongside a reference to its
/// start and end "Point" Transforms, and keeps the drawn line glued to wherever
/// those two points currently are - every frame in Play mode, and continuously in
/// the Editor too (see [ExecuteAlways]) so dragging a point in the Scene view while
/// NOT playing visibly redraws every line touching it, live.
///
/// Deliberately reference-based rather than Transform-parenting: a LineRenderer's
/// endpoints are static baked coordinates (set once via SetPosition), completely
/// independent of any Transform hierarchy - parenting a point under a segment would
/// move the point along with the segment for free, but the drawn line itself would
/// NOT follow, since nothing about Unity's Transform system touches a LineRenderer's
/// stored position data. Worse, each segment spans TWO points (its own index and the
/// next one around the ring - see EvidenceChalkGuide.Initialize), so a clean 1:1
/// parent/child relationship could only ever "own" one end anyway. Two live Transform
/// references, redrawn every tick, sidesteps both problems: it doesn't matter how a
/// point ends up in a new position (dragged directly, or moved via anything it's
/// itself parented under), every segment referencing it updates to match.
///
/// worldSpace on the LineRenderer (see the segment's own setup) is what makes this
/// simple: positions read straight off Transform.position, no per-segment local-space
/// conversion needed regardless of where this GameObject's own transform happens to
/// sit.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(LineRenderer))]
public class ChalkGuideSegmentLink : MonoBehaviour
{
    [Tooltip("This segment's own point (index i) - typically 'Point_i', a sibling under the same guide.")]
    [SerializeField] private Transform startPoint;
    [Tooltip("The next point around the ring (index i+1, wrapping) - typically 'Point_(i+1)'.")]
    [SerializeField] private Transform endPoint;

    private LineRenderer line;

    private void Awake()
    {
        line = GetComponent<LineRenderer>();
    }

    /// <summary>
    /// (Re)points this segment at a different pair of points - called by
    /// EvidenceChalkGuide.Initialize, since which point index wraps to which
    /// (segment i's end is point (i+1) % count) depends on THIS item's own
    /// chalkPointCount, not whatever the template's own full point count is. A
    /// template authored for 24 points but configured for an item that only needs 4
    /// must wrap segment 3's end back to point 0, not point 4 (part of the
    /// deactivated tail) - see Initialize's own comment.
    /// </summary>
    public void Configure(Transform newStartPoint, Transform newEndPoint)
    {
        startPoint = newStartPoint;
        endPoint = newEndPoint;
    }

    private void LateUpdate()
    {
        if (line == null)
        {
            line = GetComponent<LineRenderer>();
        }

        if (line == null || startPoint == null || endPoint == null)
        {
            return;
        }

        line.SetPosition(0, startPoint.position);
        line.SetPosition(1, endPoint.position);
    }
}
