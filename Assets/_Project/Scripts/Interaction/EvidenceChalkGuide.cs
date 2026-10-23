// Assets/_Project/Scripts/Interaction/EvidenceChalkGuide.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-item chalk-tracing guide: a continuous, gap-free ring made of chalkPointCount
/// connected line segments arranged around a circle of chalkCircleRadius, in the
/// LOCAL space of the item's own EvidenceProp transform - so a grabbable item (the
/// knife) drags its guide along if picked up mid-trace, the same reasoning that put
/// the blood-on-knife stain decal under the knife's own transform rather than at a
/// baked world position. Built from connected segments rather than separate dot
/// markers so it always reads as a full circle, whether zero points are covered or
/// all of them are - each segment simply recolours as its starting point gets
/// covered, instead of a sparse row of unconnected dots. Oriented via
/// EvidenceProp.SurfaceNormalLocalAxis rather than transform.up directly, since that
/// assumption does not hold uniformly across this roster - see EvidenceProp's own
/// doc comment. Built entirely in local space, so it needs no per-frame
/// recomputation and is unaffected by the item's current world rotation.
///
/// Purely visual/geometric - this class knows nothing about EvidenceStateManager
/// transitions beyond reacting to OnEvidenceStatusChanged to know when to stop
/// evaluating and become the permanent mark. The actual MarkChalkMarked call is
/// ChalkMarkTool's job (see TryTraceAt), matching how every other tool in this
/// project owns its own ReportEvidence call rather than delegating it to a helper.
///
/// One instance is spawned automatically the moment any item reaches
/// PhotographedRaw (via the static listener below) - nothing else creates one, and
/// nothing needs to: this is a purely reactive, event-driven spawn, same pattern
/// SessionLogger/ProceduralGateValidator already use for their own subscriptions.
///
/// SCENE-AUTHORED, NOT BUILT FROM SCRATCH: SpawnFor clones ChalkMarkTool.
/// ChalkGuideTemplate - a real GameObject in the scene, kept inactive as a whole -
/// which already carries this component plus up to MaxTemplatePoints pre-built
/// "Point_i" (position-only) and "Segment_i" (LineRenderer) children, complete with
/// real material assets assigned in the Inspector. Initialize's job shrank from
/// "create chalkPointCount new GameObjects with LineRenderer components" to "find and
/// configure the first chalkPointCount of the ones already there, deactivate any
/// unused tail" - this class no longer calls `new GameObject` or `new Material`
/// anywhere. A future item that ever needs more than MaxTemplatePoints points would
/// need the template itself extended first; Initialize logs a warning and clamps
/// rather than silently under-covering if that ever happens.
/// </summary>
public class EvidenceChalkGuide : MonoBehaviour
{
    // How close the chalk tip must get to a point to cover it, as a fraction of this
    // item's OWN chalkCircleRadius rather than a fixed metres value. A fixed absolute
    // tolerance would either be far too generous for a small item's circle (the
    // whole circle would cover in one touch) or far too tight for the knife/bottle's
    // much larger ones - scaling with the item's own radius keeps the "sweep around
    // the shape" feel consistent regardless of how wildly circle size varies across
    // this roster.
    private const float ToleranceFraction = 0.5f;
    private const float LineWidthScale = 1.5f;

    private static readonly List<EvidenceChalkGuide> active = new();

    // Completed (permanent) outlines - no longer traceable, but still OWN the strokes made
    // on them, so retracing a finished outline never credits a neighbouring one.
    private static readonly List<EvidenceChalkGuide> completed = new();

    [Header("Materials (assign on the template)")]
    [Tooltip("Segment colour before that segment's starting point is covered. Assigned on ChalkMarkTool.ChalkGuideTemplate's own EvidenceChalkGuide component, carried through to every spawned clone by Instantiate. Falls back to a generated placeholder colour if left unassigned, same 'nothing here depends on an art asset existing' convention every other tool in this project already uses.")]
    [SerializeField] private Material uncoveredMaterial;
    [Tooltip("Segment colour once that segment's starting point is covered (and the permanent colour every segment locks to once the whole guide is marked - see MarkPermanent).")]
    [SerializeField] private Material coveredMaterial;

    private static Material generatedUncoveredFallback;
    private static Material generatedCoveredFallback;

    private static bool toolEquipped;

    public string EvidenceId { get; private set; }
    public bool IsComplete { get; private set; }
    public int PointCount => points?.Length ?? 0;
    public int CoveredCount => coveredCount;

    private Transform[] points;
    private bool[] covered;
    private LineRenderer[] pointRenderers;
    private int coveredCount;
    private float toleranceSqr;

    // A pre-authored guide (see SpawnFor's "existingAuthored" check) sits active and
    // visible in the scene by design - that is the whole point, seeing it without
    // entering Play mode. But the instant Play actually starts, it must go dark just
    // like a dynamically-cloned guide does (which simply doesn't exist yet at that
    // point) - otherwise a player would see a circle marking exactly where an
    // unfound item is, before ever reaching PhotographedRaw. Hidden here,
    // unconditionally, the moment this instance wakes up in a live Play session;
    // Initialize (called from SpawnFor once the real PhotographedRaw trigger fires)
    // is what reveals it again through the normal toolEquipped-gated path. Harmless
    // no-op for a freshly-template-cloned guide too - Initialize runs immediately
    // after in the same call and recomputes every renderer's enabled state anyway.
    private void Awake()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        foreach (var line in GetComponentsInChildren<LineRenderer>(true))
        {
            line.enabled = false;
        }
    }

    // --- static bootstrap / registry ---------------------------------------

    // Same "clear static state on every Play session start" discipline
    // PlayerToolRegistry already uses - domain reload can be disabled, and a stray
    // subscription or leftover registry entry from a previous session must not
    // survive into the next one.
    //
    // Deliberately AfterAssembliesLoaded, not SubsystemRegistration: PlayerToolRegistry's
    // own reset (SubsystemRegistration) sets OnToolGrabbed/OnToolHolstered to null as
    // part of clearing its static state, and Unity does not guarantee ordering between
    // two different classes' SubsystemRegistration methods. Subscribing at the same
    // phase raced that reset and could get silently wiped the instant after attaching -
    // confirmed as a real bug (equip/holster visibility never engaged), not a
    // hypothetical. AfterAssembliesLoaded is a strictly later phase, so
    // PlayerToolRegistry's own reset is guaranteed to have already run by the time this
    // subscribes.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    private static void Bootstrap()
    {
        active.Clear();
        completed.Clear();
        toolEquipped = false;

        EvidenceStateManager.OnEvidenceStatusChanged -= HandleAnyStatusChanged;
        EvidenceStateManager.OnEvidenceStatusChanged += HandleAnyStatusChanged;

        PlayerToolRegistry.OnToolGrabbed -= HandleToolGrabbed;
        PlayerToolRegistry.OnToolGrabbed += HandleToolGrabbed;
        PlayerToolRegistry.OnToolHolstered -= HandleToolHolstered;
        PlayerToolRegistry.OnToolHolstered += HandleToolHolstered;
    }

    private static void HandleAnyStatusChanged(string evidenceId, EvidenceStatus status)
    {
        if (status == EvidenceStatus.PhotographedRaw)
        {
            // No guide for an item whose procedure has no chalk step (requiresChalk) -
            // there is nothing to trace, and tracing must never fake a ChalkMarked.
            var definition = EvidenceStateManager.Instance?.GetRecord(evidenceId)?.definition;
            if (definition == null || definition.requiresChalk)
            {
                SpawnFor(evidenceId);
            }
        }
        else if (status == EvidenceStatus.ChalkMarked)
        {
            FindActive(evidenceId)?.MarkPermanent();
        }
    }

    private static void HandleToolGrabbed(ToolType role, PlayerTool tool)
    {
        if (role != ToolType.ChalkMark) return;
        toolEquipped = true;
        RefreshAllVisibility();
    }

    private static void HandleToolHolstered(ToolType role, PlayerTool tool)
    {
        if (role != ToolType.ChalkMark) return;
        toolEquipped = false;
        RefreshAllVisibility();
    }

    private static void RefreshAllVisibility()
    {
        foreach (var guide in active)
        {
            if (guide != null) guide.RefreshVisibility();
        }
    }

    /// <summary>The active (not-yet-complete) guide for this evidenceId, or null if none exists. Public so GreyboxFlowTest can drive a specific item's guide directly through the same points ChalkMarkTool.TryTraceAt would receive from a real trace.</summary>
    public static EvidenceChalkGuide FindActive(string evidenceId)
    {
        foreach (var guide in active)
        {
            if (guide != null && guide.EvidenceId == evidenceId) return guide;
        }
        return null;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    internal static void PrepareDevelopmentStart(string evidenceId)
    {
        SceneTestStart.RequirePreparation();
        SpawnFor(evidenceId);
        var guide = FindActive(evidenceId);
        if (guide == null) return;
        for (int i = 0; i < guide.PointCount; i++)
            guide.CoverPointsNear(guide.GetPointPosition(i), 1f);
        guide.MarkPermanent();
    }
#endif

    private static void SpawnFor(string evidenceId)
    {
        if (FindActive(evidenceId) != null)
        {
            // Duplicate/backward PhotographedRaw call re-firing (harmless per
            // EvidenceStateManager.SetStatus's own Duplicate handling) - don't spawn
            // a second guide on top of the first.
            return;
        }

        var esm = EvidenceStateManager.Instance;
        var prop = EvidenceProp.FindById(evidenceId);
        var record = esm != null ? esm.GetRecord(evidenceId) : null;
        if (prop == null || record?.definition == null)
        {
            return;
        }

        // Prefer an already-authored guide over cloning the template. An author can
        // hand-place a real "ChalkGuide_<id>" child directly under the prop in the
        // scene - visible and editable at edit time, no Play mode needed to see it -
        // and this adopts it here instead of instantiating a second one from the
        // template. Routes through InitializeFromAuthored, NOT Initialize - Initialize
        // unconditionally recomputes every point's position from the
        // chalkCircleRadius/chalkPointCount formula, which would silently discard any
        // hand-edit an author made to an authored guide's point positions the moment
        // it (re)spawns. Confirmed as a real bug, not a hypothetical: an earlier
        // version of this comment claimed re-running Initialize here was "safe", which
        // is only true if the authored positions happen to exactly match the formula's
        // output - any deliberate custom shape got silently overwritten back to a
        // perfect circle on every PhotographedRaw. See InitializeFromAuthored's own
        // comment for what IS still applied (runtime-driven bits only).
        var existingAuthored = prop.transform.Find("ChalkGuide_" + evidenceId);
        if (existingAuthored != null)
        {
            existingAuthored.gameObject.SetActive(true);
            var authoredGuide = existingAuthored.GetComponent<EvidenceChalkGuide>();
            if (authoredGuide == null)
            {
                Debug.LogWarning($"[EvidenceChalkGuide] Authored ChalkGuide_{evidenceId} has no EvidenceChalkGuide component - adding one with default (generated-fallback) materials. Add the component and assign uncoveredMaterial/coveredMaterial on it directly for a real authored look.");
                authoredGuide = existingAuthored.gameObject.AddComponent<EvidenceChalkGuide>();
            }
            authoredGuide.InitializeFromAuthored(evidenceId, record.definition);
            return;
        }

        var template = ChalkMarkTool.Instance != null ? ChalkMarkTool.Instance.ChalkGuideTemplate : null;
        if (template == null)
        {
            // Handled gracefully rather than silently doing nothing - see
            // ChalkMarkTool.chalkGuideTemplate's own tooltip for why this matters.
            Debug.LogWarning($"[EvidenceChalkGuide] Can't spawn a guide for {evidenceId}: ChalkMarkTool.chalkGuideTemplate is unassigned.");
            return;
        }

        var go = Instantiate(template, prop.transform, false);
        go.name = "ChalkGuide_" + evidenceId;
        go.SetActive(true); // the template itself is kept inactive

        if (prop.OverrideChalkGuideTransform)
        {
            // Per-prop authored exception - see EvidenceProp.overrideChalkGuideTransform's
            // own comment. Still a child of the prop, just not derived from its transform.
            go.transform.localPosition = prop.ChalkGuideLocalPosition;
            go.transform.localRotation = Quaternion.Euler(prop.ChalkGuideLocalEulerAngles);
            go.transform.localScale = prop.ChalkGuideLocalScale;
        }
        else
        {
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            // Counteract the prop's own visual scale. Some props in this roster are
            // decal-style GameObjects deliberately scaled down/non-uniformly purely for
            // rendering size (blood spatter: 0.35/0.35/1) - that scale must not also
            // shrink or distort the guide's real-world-metre point geometry, which is
            // defined in actual metres (chalkCircleRadius) and expects to sit under an
            // effectively unscaled parent. Confirmed as a real bug, not a hypothetical:
            // without this, a small co-located item's circle once collapsed to ~1mm
            // radius, and a single touch covered all its points at once, silently
            // defeating the entire tracing mechanic for that item.
            Vector3 parentScale = prop.transform.lossyScale;
            go.transform.localScale = new Vector3(
                Mathf.Approximately(parentScale.x, 0f) ? 1f : 1f / parentScale.x,
                Mathf.Approximately(parentScale.y, 0f) ? 1f : 1f / parentScale.y,
                Mathf.Approximately(parentScale.z, 0f) ? 1f : 1f / parentScale.z);
        }

        // The template already carries this component (cloned, not AddComponent'd -
        // see class comment on why that distinction matters for the material fields).
        var guide = go.GetComponent<EvidenceChalkGuide>();
        if (guide == null)
        {
            Debug.LogWarning($"[EvidenceChalkGuide] ChalkMarkTool.chalkGuideTemplate has no EvidenceChalkGuide component on its root - can't spawn a guide for {evidenceId}.");
            Destroy(go);
            return;
        }

        guide.Initialize(evidenceId, prop, record.definition);
    }

    /// <summary>
    /// The active guide nearest tipWorldPosition by circle-centre distance, or null if
    /// none are active. Same "resolve by nearest" principle EvidenceProp.
    /// FindNearestWithinRadius already uses for tenting - deliberately no radius gate
    /// here, unlike tenting: the chalk tip is expected to be right next to whichever
    /// item is actually being traced, so nearest-by-distance alone is enough, and a
    /// radius gate would only risk a genuine trace near a small item's circle failing
    /// to resolve to it because the player's hand strayed past an arbitrary cutoff.
    /// </summary>
    /// <summary>
    /// The guide owning the point nearest the chalk tip - the outline actually being traced.
    /// Every point counts, covered or not, and completed outlines still own their strokes:
    /// a stroke on an already-chalked stretch, or on a finished outline, credits nothing
    /// rather than spilling onto a neighbour. Returns null when the nearest outline is
    /// already complete.
    ///
    /// Guide CENTRES are not a safe proxy once outlines nest or sit close together: the
    /// blood spatter's outline lies inside the body's (a few cm apart on the west side) and
    /// the knife's circle is near the body's north-west edge, so "nearest centre" credited
    /// parts of one outline to another and none could be completed.
    /// </summary>
    public static EvidenceChalkGuide FindByNearestPoint(Vector3 tipWorldPosition)
    {
        EvidenceChalkGuide best = null;
        float bestSqr = float.MaxValue;
        foreach (var list in new[] { active, completed })
        {
            foreach (var guide in list)
            {
                if (guide == null || guide.points == null) continue;
                for (int i = 0; i < guide.points.Length; i++)
                {
                    if (guide.points[i] == null) continue;
                    float d = (guide.points[i].position - tipWorldPosition).sqrMagnitude;
                    if (d < bestSqr)
                    {
                        bestSqr = d;
                        best = guide;
                    }
                }
            }
        }
        return best != null && best.IsComplete ? null : best;
    }

    public static EvidenceChalkGuide FindNearestActive(Vector3 tipWorldPosition)
    {
        EvidenceChalkGuide nearest = null;
        float nearestSqr = float.MaxValue;

        foreach (var guide in active)
        {
            if (guide == null) continue;

            float distSqr = (guide.transform.position - tipWorldPosition).sqrMagnitude;
            if (distSqr < nearestSqr)
            {
                nearestSqr = distSqr;
                nearest = guide;
            }
        }

        return nearest;
    }

    // --- instance -----------------------------------------------------------

    private void Initialize(string evidenceId, EvidenceProp prop, EvidenceDefinition def)
    {
        EvidenceId = evidenceId;

        // The template's own pre-built children - found, not created. Any GameObject
        // directly under this clone's root named "Point_<i>"/"Segment_<i>" counts;
        // TemplateCapacity below is however many the template actually provides, read
        // fresh each spawn rather than hardcoded, so extending the template later
        // (more points) needs no code change here.
        int templateCapacity = CountTemplateChildren("Point_");

        int count = Mathf.Max(4, def.chalkPointCount);
        if (count > templateCapacity)
        {
            Debug.LogWarning($"[EvidenceChalkGuide] {evidenceId} wants chalkPointCount={count}, but ChalkMarkTool.chalkGuideTemplate only provides {templateCapacity} points - clamping. Extend the template to fix this properly.");
            count = templateCapacity;
        }

        float radius = Mathf.Max(0.01f, def.chalkCircleRadius);
        toleranceSqr = Mathf.Pow(radius * ToleranceFraction, 2f);

        // Canonical circle is built flat in the local X/Z plane (normal = local +Y),
        // then rotated so that normal lands on the item's own authored surface-normal
        // axis - entirely in local space, so this never needs to touch the item's
        // current world rotation at all.
        Quaternion localOrientation = Quaternion.FromToRotation(Vector3.up, prop.SurfaceNormalLocalAxis);

        var localOffsets = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            float angle = i * Mathf.PI * 2f / count;
            localOffsets[i] = localOrientation * new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }

        points = new Transform[count];
        covered = new bool[count];
        pointRenderers = new LineRenderer[count];

        float lineWidth = Mathf.Max(0.0015f, radius * 0.03f) * LineWidthScale;

        for (int i = 0; i < templateCapacity; i++)
        {
            var pointT = transform.Find("Point_" + i);
            var segmentT = transform.Find("Segment_" + i);

            if (i >= count)
            {
                // Unused tail this item doesn't need - hide it entirely, both the
                // (renderer-less) point marker and its trailing segment.
                if (pointT != null) pointT.gameObject.SetActive(false);
                if (segmentT != null) segmentT.gameObject.SetActive(false);
                continue;
            }

            if (pointT == null || segmentT == null)
            {
                Debug.LogWarning($"[EvidenceChalkGuide] Template is missing Point_{i}/Segment_{i} - guide for {evidenceId} will be incomplete.");
                continue;
            }

            pointT.gameObject.SetActive(true);
            pointT.localPosition = localOffsets[i];
            points[i] = pointT;

            var line = segmentT.GetComponent<LineRenderer>();
            segmentT.gameObject.SetActive(true);
            line.widthMultiplier = lineWidth;
            line.sharedMaterial = GetUncoveredMaterial();
            line.enabled = toolEquipped;

            pointRenderers[i] = line;
        }

        // Second pass, after every Point this item needs has its real position (the
        // wrap target for the LAST segment, point[(count-1+1) % count] == points[0],
        // must already be set - can't wire ChalkGuideSegmentLink references mid-loop
        // above before points[] is fully populated). Segment i always connects
        // points[i] to points[(i+1) % count] - this item's own wrap, not whatever the
        // template's full point count wraps at (see ChalkGuideSegmentLink.Configure's
        // own comment).
        for (int i = 0; i < count; i++)
        {
            var segmentT = transform.Find("Segment_" + i);
            if (segmentT == null) continue;

            var link = segmentT.GetComponent<ChalkGuideSegmentLink>();
            if (link == null)
            {
                link = segmentT.gameObject.AddComponent<ChalkGuideSegmentLink>();
            }
            link.Configure(points[i], points[(i + 1) % count]);
        }

        active.Add(this);
    }

    /// <summary>
    /// Sibling of Initialize for a guide that already exists, hand-authored, in the
    /// scene (see SpawnFor's "existingAuthored" branch). Deliberately does NOT touch
    /// any Point's position, any Segment's width, or the count of points in use -
    /// all of that is trusted exactly as the author left it, which is the entire
    /// point of authoring a guide by hand instead of letting it spawn from the
    /// generic template. Only applies the bits that are genuinely runtime state, not
    /// authored content: which material each segment currently shows (reset to
    /// uncovered - a fresh trace attempt starts clean) and whether it's currently
    /// visible (toolEquipped-gated, same as every other guide). Point/segment COUNT
    /// is read from however many Point_i/Segment_i pairs actually exist under this
    /// object, not from def.chalkPointCount - an authored guide is free to use a
    /// different count than the data says, same "trust the object" principle.
    /// </summary>
    private void InitializeFromAuthored(string evidenceId, EvidenceDefinition def)
    {
        EvidenceId = evidenceId;

        int count = CountTemplateChildren("Point_");
        if (count == 0)
        {
            Debug.LogWarning($"[EvidenceChalkGuide] Authored guide for {evidenceId} has no Point_0 child - nothing to trace.");
            active.Add(this);
            return;
        }

        float radius = Mathf.Max(0.01f, def.chalkCircleRadius);
        toleranceSqr = Mathf.Pow(radius * ToleranceFraction, 2f);

        points = new Transform[count];
        covered = new bool[count];
        pointRenderers = new LineRenderer[count];

        for (int i = 0; i < count; i++)
        {
            var pointT = transform.Find("Point_" + i);
            var segmentT = transform.Find("Segment_" + i);
            if (pointT == null || segmentT == null)
            {
                Debug.LogWarning($"[EvidenceChalkGuide] Authored guide for {evidenceId} is missing Point_{i}/Segment_{i} - guide will be incomplete.");
                continue;
            }

            pointT.gameObject.SetActive(true);
            points[i] = pointT;

            var line = segmentT.GetComponent<LineRenderer>();
            segmentT.gameObject.SetActive(true);
            line.sharedMaterial = GetUncoveredMaterial();
            line.enabled = toolEquipped;
            pointRenderers[i] = line;

            var link = segmentT.GetComponent<ChalkGuideSegmentLink>();
            if (link == null)
            {
                link = segmentT.gameObject.AddComponent<ChalkGuideSegmentLink>();
            }
            link.Configure(pointT, transform.Find("Point_" + ((i + 1) % count)));
        }

        active.Add(this);
    }

    private int CountTemplateChildren(string namePrefix)
    {
        int n = 0;
        while (transform.Find(namePrefix + n) != null)
        {
            n++;
        }
        return n;
    }

    /// <summary>World position of guide point index i - exposed for GreyboxFlowTest to drive ChalkMarkTool.TryTraceAt with real point positions instead of a synthetic shortcut.</summary>
    public Vector3 GetPointPosition(int index) => points[index].position;

    /// <summary>
    /// The real per-frame coverage check: any of this guide's uncovered points within
    /// tolerance of tipWorldPosition gets marked covered and its marker recoloured
    /// immediately, in real time, regardless of which point(s) get hit in what order.
    /// Returns true the one time this call causes the coverage fraction to reach
    /// completionThreshold - the caller (ChalkMarkTool.TryTraceAt) is responsible for
    /// reporting MarkChalkMarked when it does, exactly once, since this class has no
    /// EvidenceStateManager knowledge of its own.
    /// </summary>
    public bool IsTouching(Vector3 tipWorldPosition)
    {
        if(IsComplete || points==null) return false;
        foreach(var point in points)
            if(point!=null && (point.position-tipWorldPosition).sqrMagnitude<=toleranceSqr) return true;
        return false;
    }

    public bool CoverPointsNear(Vector3 tipWorldPosition, float completionThreshold)
    {
        if (IsComplete)
        {
            return false;
        }

        bool anyNewlyCovered = false;
        for (int i = 0; i < points.Length; i++)
        {
            if (covered[i] || points[i] == null)
            {
                continue;
            }

            if ((points[i].position - tipWorldPosition).sqrMagnitude <= toleranceSqr)
            {
                covered[i] = true;
                coveredCount++;
                if (pointRenderers[i] != null)
                {
                    pointRenderers[i].sharedMaterial = GetCoveredMaterial();
                }
                anyNewlyCovered = true;
            }
        }

        if (anyNewlyCovered && (float)coveredCount / points.Length >= completionThreshold)
        {
            // Set immediately, before the caller has reported anything - a re-entrant
            // call this same frame (there shouldn't be one, but nothing prevents it)
            // must see IsComplete already true and refuse to report a second time.
            IsComplete = true;
            return true;
        }

        return false;
    }

    // Reached only via OnEvidenceStatusChanged -> ChalkMarked, so this fires
    // regardless of whether that transition came from a real trace completing
    // (TryTraceAt above) or from a test/back-door call to MarkChalkMarked directly -
    // the guide's own permanent-mark behaviour must not depend on which one happened.
    private void MarkPermanent()
    {
        IsComplete = true;
        active.Remove(this);
        if (!completed.Contains(this)) completed.Add(this);

        // Detach from the evidence prop now that this mark is permanent: a chalk
        // outline is a record of where the item was found, not a property of the
        // item itself - it must stay put at the original location even after the
        // item is later collected, bagged, and moved into the evidence crate.
        // Confirmed necessary, not theoretical: EvidenceBagTool.ApplyBaggedVisual
        // disables every child renderer under the prop for items with an authored
        // sealed-bag model (the knife, the phone) to hide the loose art once bagged -
        // without detaching first, that pass silently hid this permanent mark right
        // along with it.
        transform.SetParent(null, true);

        foreach (var pointRenderer in pointRenderers)
        {
            if (pointRenderer != null)
            {
                pointRenderer.sharedMaterial = GetCoveredMaterial();
                pointRenderer.enabled = true;
            }
        }
    }

    private void RefreshVisibility()
    {
        if (IsComplete)
        {
            // Permanent - always visible from here on, equip state no longer matters.
            return;
        }

        foreach (var pointRenderer in pointRenderers)
        {
            if (pointRenderer != null)
            {
                pointRenderer.enabled = toolEquipped;
            }
        }
    }

    private void OnDestroy()
    {
        active.Remove(this);
        completed.Remove(this);
    }

    // Real material takes priority (assigned on the template - see the class
    // comment); the generated placeholder colour is only ever reached when no real
    // asset is wired, same "nothing here depends on an art asset existing"
    // convention SwabTool/FingerprintKitTool's own fallback generators already use.
    private Material GetUncoveredMaterial()
    {
        if (uncoveredMaterial != null)
        {
            return uncoveredMaterial;
        }

        if (generatedUncoveredFallback == null)
        {
            generatedUncoveredFallback = new Material(Shader.Find("Sprites/Default"))
            {
                color = new Color(0.55f, 0.55f, 0.55f, 0.9f)
            };
        }
        return generatedUncoveredFallback;
    }

    private Material GetCoveredMaterial()
    {
        if (coveredMaterial != null)
        {
            return coveredMaterial;
        }

        if (generatedCoveredFallback == null)
        {
            generatedCoveredFallback = new Material(Shader.Find("Sprites/Default"))
            {
                color = new Color(0.95f, 0.95f, 0.9f, 1f)
            };
        }
        return generatedCoveredFallback;
    }
}
