using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum SceneTestStartingPoint
{
    NormalPlay = 0,
    IOCDone = 1,
    PhotographyDone = 2,
    AllEvidenceProceduresDone = 3,
    MeasurementsDone = 4,
    SketchDone = 5,
    InvestigationSubmitted = 6,
    KnifeBagging = 7,
    BottleFingerprinting = 8,
    BodyRecording = 9,
    AllEvidenceMeasured = 10,
    CustodyPass = 11,
    KnifeScalePhotography = 12,
    BloodstainScalePhotography = 13,
    BottleScalePhotography = 14,
    FingerprintComparison = 15
}

/// <summary>Opt-in manual testing fixtures. Normal play never enters the fixture path.
/// Reloads the entire scene for a restart; never partially resets a live investigation.</summary>
[DefaultExecutionOrder(-32000)]
[DisallowMultipleComponent]
public sealed class SceneTestStart : MonoBehaviour
{
    [Tooltip("Editor/development builds only. Normal Play is always used in release builds. The Scene Test Start window can override this locally in the Editor.")]
    public SceneTestStartingPoint startingPoint = SceneTestStartingPoint.NormalPlay;

    public static SceneTestStart Instance { get; private set; }
    public static bool IsActive { get; private set; }
    public static bool IsPreparing { get; private set; }
    public SceneTestStartingPoint ActivePoint { get; private set; }
    public bool IsReady { get; private set; }
    public string Failure { get; private set; }
    public Vector3 PreparedFeet { get; private set; }
    public const string EditorSelectionKey = "ViCSI.SceneTestStart.Selection";

    // Display order is independent of serialized enum values and saved selections.
    public static readonly SceneTestStartingPoint[] MenuOrder = {
        SceneTestStartingPoint.NormalPlay, SceneTestStartingPoint.IOCDone, SceneTestStartingPoint.PhotographyDone,
        SceneTestStartingPoint.KnifeScalePhotography, SceneTestStartingPoint.BloodstainScalePhotography,
        SceneTestStartingPoint.BottleScalePhotography, SceneTestStartingPoint.AllEvidenceMeasured,
        SceneTestStartingPoint.BodyRecording, SceneTestStartingPoint.BottleFingerprinting, SceneTestStartingPoint.KnifeBagging,
        SceneTestStartingPoint.FingerprintComparison, SceneTestStartingPoint.AllEvidenceProceduresDone,
        SceneTestStartingPoint.MeasurementsDone, SceneTestStartingPoint.SketchDone,
        SceneTestStartingPoint.CustodyPass, SceneTestStartingPoint.InvestigationSubmitted
    };

    public static string PhotoEvidenceId(SceneTestStartingPoint point) => point switch
    {
        SceneTestStartingPoint.KnifeScalePhotography => "EVD-014",
        SceneTestStartingPoint.BloodstainScalePhotography => "EVD-015",
        SceneTestStartingPoint.BottleScalePhotography => "EVD-018",
        _ => null
    };

    public static SceneTestStartingPoint Milestone(SceneTestStartingPoint point) => point switch
    {
        SceneTestStartingPoint.KnifeBagging or SceneTestStartingPoint.BottleFingerprinting
            or SceneTestStartingPoint.BodyRecording or SceneTestStartingPoint.AllEvidenceMeasured
            or SceneTestStartingPoint.KnifeScalePhotography or SceneTestStartingPoint.BloodstainScalePhotography
            or SceneTestStartingPoint.BottleScalePhotography => SceneTestStartingPoint.PhotographyDone,
        SceneTestStartingPoint.CustodyPass or SceneTestStartingPoint.FingerprintComparison => SceneTestStartingPoint.AllEvidenceProceduresDone,
        _ => point
    };

    public static string Label(SceneTestStartingPoint point) => point switch
    {
        SceneTestStartingPoint.IOCDone => "IOC done",
        SceneTestStartingPoint.PhotographyDone => "Photography done",
        SceneTestStartingPoint.AllEvidenceProceduresDone => "All evidence procedures done",
        SceneTestStartingPoint.MeasurementsDone => "Measurements done",
        SceneTestStartingPoint.SketchDone => "Sketch done",
        SceneTestStartingPoint.InvestigationSubmitted => "Investigation submitted",
        SceneTestStartingPoint.KnifeBagging => "Knife bagging",
        SceneTestStartingPoint.BottleFingerprinting => "Bottle fingerprinting",
        SceneTestStartingPoint.BodyRecording => "Body recording",
        SceneTestStartingPoint.AllEvidenceMeasured => "All evidence measured",
        SceneTestStartingPoint.CustodyPass => "Custody pass",
        SceneTestStartingPoint.KnifeScalePhotography => "Knife + ruler photo",
        SceneTestStartingPoint.BloodstainScalePhotography => "Bloodstain + ruler photo",
        SceneTestStartingPoint.BottleScalePhotography => "Bottle + ruler photo",
        SceneTestStartingPoint.FingerprintComparison => "Fingerprint comparison",
        _ => "Normal play"
    };

    public static string NextActivity(SceneTestStartingPoint point) => point switch
    {
        SceneTestStartingPoint.IOCDone => "Scene photography route; camera equipped. Raise with X, follow the standing circles and use the trigger.",
        SceneTestStartingPoint.PhotographyDone => "Discover evidence, take its original photo, chalk and place markers, then photograph with the ruler where required.",
        SceneTestStartingPoint.AllEvidenceProceduresDone => "Measuring and sketching (original positions retained)",
        SceneTestStartingPoint.MeasurementsDone => "Empty sketch with measurement cards ready",
        SceneTestStartingPoint.SketchDone => "Final reasoning and conclusion submission",
        SceneTestStartingPoint.InvestigationSubmitted => "Six stored items ready; pass custody at the evidence box, then inventory review, final survey, IOC report and release.",
        SceneTestStartingPoint.KnifeBagging => "Grab the ready knife with right grip; hold left trigger, insert into bag, release trigger, then B to store",
        SceneTestStartingPoint.BottleFingerprinting => "Final photo and measurements done. Load powder, brush, lift, carry the card to the examination desk, confirm a match, then B to store.",
        SceneTestStartingPoint.BodyRecording => "Aim the Recorder at the body, confirm its three findings, then holster and press A to prepare removal.",
        SceneTestStartingPoint.AllEvidenceMeasured => "Scene Sketch placement is next, before sampling, fingerprints and recording. Blood samples must be contained, dried, paper packaged, sealed and B stored; refresh gloves and swabs at supplies as needed.",
        SceneTestStartingPoint.CustodyPass => "Six sealed items in inventory; approach the empty box, press A, review and confirm the custody pass.",
        SceneTestStartingPoint.KnifeScalePhotography or SceneTestStartingPoint.BloodstainScalePhotography
            or SceneTestStartingPoint.BottleScalePhotography => "Original photo and markers ready; camera equipped. Approach the item, X to raise, A to place the ruler, frame both until green, trigger to photograph; A removes the ruler.",
        SceneTestStartingPoint.FingerprintComparison => "A held knife fingerprint card awaits comparison at the outside desk. Press A to examine references, confirm a match, close comparison, then B to store.",
        _ => "Full scenario"
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null; IsActive = false; IsPreparing = false;
    }

    private void Awake()
    {
        Instance = this;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        ActivePoint = startingPoint;
#if UNITY_EDITOR
        int selected = UnityEditor.SessionState.GetInt(EditorSelectionKey, -1);
        if (selected >= 0 && Enum.IsDefined(typeof(SceneTestStartingPoint), selected))
            ActivePoint = (SceneTestStartingPoint)selected;
        // Existing automated suites own their own fresh-session setup.
        if (UnityEditor.SessionState.GetBool("ViCSI.Phase5.Pending", false)
            && !UnityEditor.SessionState.GetBool("ViCSI.SceneTestStart.Diagnostic", false))
            ActivePoint = SceneTestStartingPoint.NormalPlay;
#endif
        if (!Enum.IsDefined(typeof(SceneTestStartingPoint), ActivePoint)) ActivePoint = SceneTestStartingPoint.NormalPlay;
        IsActive = ActivePoint != SceneTestStartingPoint.NormalPlay;
        IsPreparing = IsActive;
        if (IsActive) AutomatedTestSandbox.Activate(nameof(SceneTestStart));
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    internal static void RequirePreparation()
    {
        if (!IsActive || !IsPreparing || !AutomatedTestSandbox.IsActive)
            throw new InvalidOperationException("Development fixtures require an isolated test start.");
    }

    private TMP_Text indicator;
    private IEnumerator Start()
    {
        if (!IsActive) yield break;
        BuildIndicator();
        // All managers' Start methods (including the photo route and contextual reasoning)
        // must finish before applying a fixture. Dialogue is suppressed during preparation.
        yield return null;
        yield return null;
        try
        {
            ApplyFixture();
        }
        catch (Exception ex)
        {
            Failure = ex.Message;
            Debug.LogError("[SceneTestStart] Fixture failed: " + ex);
        }
        finally { IsPreparing = false; }
        // Teleport events are consumed by the locomotor at the end of the frame.
        yield return null;
        if (Failure == null && ActivePoint == SceneTestStartingPoint.KnifeBagging)
        {
            var rig = FindFirstObjectByType<OVRCameraRig>();
            if (rig == null || !HeldItemCoordinator.Instance.ToggleEquipTool(ToolType.EvidenceCollector, rig.leftHandAnchor))
                Failure = "Could not equip the evidence bag for the knife test.";
        }
        if (Failure == null && ActivePoint == SceneTestStartingPoint.BottleFingerprinting)
        {
            var rig = FindFirstObjectByType<OVRCameraRig>();
            if (rig == null || !HeldItemCoordinator.Instance.ToggleEquipTool(ToolType.FingerprintKit, rig.rightHandAnchor))
                Failure = "Could not equip the fingerprint kit for the bottle test.";
        }
        if (Failure == null && (ActivePoint == SceneTestStartingPoint.IOCDone || PhotoEvidenceId(ActivePoint) != null))
        {
            var rig = FindFirstObjectByType<OVRCameraRig>();
            if (rig == null || !HeldItemCoordinator.Instance.ToggleEquipTool(ToolType.Photographer, rig.rightHandAnchor))
                Failure = "Could not equip the camera for the photography test.";
        }
        if (Failure == null && (ActivePoint == SceneTestStartingPoint.BodyRecording || ActivePoint == SceneTestStartingPoint.AllEvidenceMeasured))
        {
            var rig = FindFirstObjectByType<OVRCameraRig>();
            if (rig == null || !HeldItemCoordinator.Instance.ToggleEquipTool(ToolType.Recorder, rig.rightHandAnchor))
                Failure = "Could not equip the Recorder for the body test.";
        }
        IsReady = Failure == null;
        indicator.text = Failure == null
            ? "TEST MODE - " + Label(ActivePoint) + "\n" + NextActivity(ActivePoint) + "\nAssessment uploads disabled"
            : "TEST SETUP FAILED\n" + Failure + "\nStop Play and check the Console";
        // The activity being tested keeps its real instructions; skipped activity dialogue
        // was never queued and cannot resurface later.
        if (IsReady && ActivePoint == SceneTestStartingPoint.IOCDone)
            ScenePhotographyManager.Instance.CurrentPoint?.ShowInstruction();
    }

    private void ApplyFixture()
    {
        if (EvidenceStateManager.Instance == null || ScenePhotographyManager.Instance == null
            || CrimeSceneSketchManager.Instance == null || MeasurementStateManager.Instance == null)
            throw new InvalidOperationException("Open the CSI_Environment scene with its managers.");
        AutomatedTestSandbox.Activate(nameof(SceneTestStart));
        SceneTurnover.PrepareDevelopmentStart();
        var photos = ScenePhotographyManager.Instance;
        var milestone = Milestone(ActivePoint);
        photos.PrepareDevelopmentStart(milestone >= SceneTestStartingPoint.PhotographyDone);

        string photoId = PhotoEvidenceId(ActivePoint);
        if (photoId != null) PrepareScalePhotography(photoId);

        if (ActivePoint == SceneTestStartingPoint.AllEvidenceMeasured)
            PrepareMeasuredEvidence();

        if (ActivePoint == SceneTestStartingPoint.KnifeBagging)
        {
            EvidenceStateManager.Instance.PrepareDevelopmentStart("EVD-014");
            if (!MeasurementTestUtil.CompleteSketchFor("EVD-014")) throw new InvalidOperationException("Knife sketch fixture incomplete.");
            EvidenceChalkGuide.PrepareDevelopmentStart("EVD-014");
            if (!EvidenceStateManager.Instance.CanAdvanceTo("EVD-014", EvidenceStatus.Collected))
                throw new InvalidOperationException("Knife fixture is not ready for collection.");
        }
        if (ActivePoint == SceneTestStartingPoint.BottleFingerprinting)
        {
            EvidenceStateManager.Instance.PrepareDevelopmentStart("EVD-018",true);
            EvidenceChalkGuide.PrepareDevelopmentStart("EVD-018");
            var manager = MeasurementStateManager.Instance;
            var requirement = manager.RequirementSet.requirements.Single(r=>r.targetEvidenceId=="EVD-018");
            foreach(var reference in new[]{requirement.referenceAId,requirement.referenceBId})
            {
                if (!manager.TryGetSceneDistance(requirement.requirementId,reference,out var distance))
                    throw new InvalidOperationException("Missing bottle measurement anchor.");
                manager.RecordMeasurement(requirement.requirementId,reference,distance);
            }
            if (!MeasurementTestUtil.CompleteSketchFor("EVD-018")) throw new InvalidOperationException("Bottle sketch fixture incomplete.");
            if (EvidenceStateManager.Instance.GetFingerprintBlockReason("EVD-018") != null)
                throw new InvalidOperationException("Bottle measurements are incomplete.");
        }

        if (ActivePoint == SceneTestStartingPoint.BodyRecording)
        {
            // Prepare only documentation needed by the existing global measurement
            // gate. No report, sample, collection, removal or scored answer is seeded.
            foreach (var id in MeasurementStateManager.Instance.RequirementSet.ScenePairRoster())
            {
                var state = EvidenceStateManager.Instance;
                state.MarkFound(id, ToolType.None);
                state.MarkPhotographedRaw(id, ToolType.Photographer);
                if (id == "BODY-A" || state.GetRecord(id).definition.measurementAfterFinalPhoto)
                {
                    state.MarkChalkMarked(id, ToolType.ChalkMark);
                    if (id == "BODY-A") state.MarkTentedWithLetter(id, ToolType.LetterTentMarker, "A");
                    else state.MarkTented(id, ToolType.EvidenceMarker, EvidenceDisplay.Number(id));
                    state.MarkPhotographedFinal(id, ToolType.Photographer);
                    EvidenceChalkGuide.PrepareDevelopmentStart(id);
                }
            }
            CompleteMeasurements();
            if (!MeasurementTestUtil.CompleteSketchFor("BODY-A")) throw new InvalidOperationException("Body recording requires prior sketch placement.");
            if (!EvidenceStateManager.Instance.CanAdvanceTo("BODY-A", EvidenceStatus.Logged))
                throw new InvalidOperationException("Body fixture is not ready for recording.");
        }

        if (milestone >= SceneTestStartingPoint.AllEvidenceProceduresDone)
        {
            PrepareMeasuredEvidence();
            foreach (var element in CrimeSceneSketchManager.Instance.Definition.evidenceElements)
                if (!MeasurementTestUtil.CompleteSketchFor(element.evidenceId)) throw new InvalidOperationException("Processing preset requires prior sketch: " + element.evidenceId);
            EvidenceStateManager.Instance.PrepareDevelopmentStart(awaitFingerprintComparison:ActivePoint==SceneTestStartingPoint.FingerprintComparison);
            foreach (var record in EvidenceStateManager.Instance.AllRecords.Where(r => r.definition.requiresChalk))
                EvidenceChalkGuide.PrepareDevelopmentStart(record.definition.evidenceId);
            SOCOCloseout.Instance.PrepareDevelopmentInventory(true);
            if (ActivePoint != SceneTestStartingPoint.FingerprintComparison)
                VendorFollowupInterview.Instance?.PrepareDevelopmentStart();
            FindingStateManager.Instance?.RefreshFindings();
            RelationshipStateManager.Instance?.RefreshUnlocks();
            ConclusionStateManager.Instance?.RefreshConclusions();
        }
        if (milestone >= SceneTestStartingPoint.MeasurementsDone)
            CompleteMeasurements();
        if (milestone >= SceneTestStartingPoint.SketchDone || ActivePoint == SceneTestStartingPoint.CustodyPass)
            CompleteSketch();

        // Mark skipped prompts as skipped, without manufacturing scored student answers.
        ReasoningCheckpointManager.Instance?.SkipEligibleForDevelopmentStart();
        HypothesisCheckpointManager.Instance?.SkipPastForDevelopmentStart(
            milestone >= SceneTestStartingPoint.AllEvidenceProceduresDone || ActivePoint == SceneTestStartingPoint.BottleFingerprinting
                || ActivePoint == SceneTestStartingPoint.BodyRecording || ActivePoint == SceneTestStartingPoint.AllEvidenceMeasured
                || photoId != null);
        ObjectiveStateManager.Instance?.RefreshAll();

        if (ActivePoint == SceneTestStartingPoint.InvestigationSubmitted || ActivePoint == SceneTestStartingPoint.CustodyPass)
        {
            EndingManager.Instance.PrepareDevelopmentCloseout();
            if (!SOCOCloseout.Instance.AllAccountedFor)
                throw new InvalidOperationException("Closeout fixture has unsecured evidence.");
        }
        if (ActivePoint == SceneTestStartingPoint.FingerprintComparison)
            EvidenceInventory.Instance.PrepareDevelopmentHeldFingerprint("EVD-014", FindAnyObjectByType<OVRCameraRig>().rightHandAnchor);
        STCSNotificationUI.Instance?.HideImmediately();
        PlacePlayer();
    }

    private static void PrepareScalePhotography(string id)
    {
        var state = EvidenceStateManager.Instance;
        var prop = EvidenceProp.FindById(id);
        var record = state.GetRecord(id);
        if (prop == null || record == null) throw new InvalidOperationException("Missing photograph subject: " + id);
        state.MarkFound(id, ToolType.None);
        state.MarkPhotographedRaw(id, ToolType.Photographer);
        if (record.definition.requiresChalk)
        {
            state.MarkChalkMarked(id, ToolType.ChalkMark);
            EvidenceChalkGuide.PrepareDevelopmentStart(id);
        }
        int number = EvidenceDisplay.Number(id);
        state.MarkTented(id, ToolType.EvidenceMarker, number);
        var tents = FindFirstObjectByType<EvidenceTentTool>(FindObjectsInactive.Include);
        tents.PrepareDevelopmentMarker(number, id, prop.transform.position + Vector3.right * .25f);
        if (record.definition.requiresTraceMarker)
        {
            state.MarkTraceTented(id, ToolType.EvidenceMarker, 4);
            tents.PrepareDevelopmentMarker(4, id, prop.transform.position + Vector3.forward * .25f, true);
        }
        if (record.status != EvidenceStatus.Marked || !state.CanAdvanceTo(id, EvidenceStatus.PhotographedFinal))
            throw new InvalidOperationException("Scaled photograph prerequisites incomplete: " + id);
    }

    private static void CompleteMeasurements()
    {
        var manager = MeasurementStateManager.Instance;
        foreach (var r in manager.RequirementSet.requirements.Where(r => r != null && r.required))
        {
            foreach (var reference in new[] { r.referenceAId, r.referenceBId })
            {
                if (!manager.TryGetSceneDistance(r.requirementId, reference, out float distance))
                    throw new InvalidOperationException("Missing measurement anchor: " + r.targetEvidenceId);
                manager.RecordMeasurement(r.requirementId, reference, distance);
            }
        }
        foreach (var pair in manager.AllPairs)
        {
            if (!manager.TryGetPairSceneDistance(pair.EvidenceA, pair.EvidenceB, out float distance))
                throw new InvalidOperationException("Missing pair anchors: " + pair.PairKey);
            manager.RecordPairMeasurement(pair.EvidenceA, pair.EvidenceB, distance);
        }
        if (!manager.AreSceneMeasurementsComplete) throw new InvalidOperationException("Measurement fixture incomplete.");
    }

    private static void PrepareMeasuredEvidence()
    {
        var state = EvidenceStateManager.Instance;
        int number = 4;
        foreach (var record in state.AllRecords)
        {
            string id = record.definition.evidenceId;
            state.MarkFound(id, ToolType.None);
            state.MarkPhotographedRaw(id, ToolType.Photographer);
            if (record.definition.requiresChalk)
            {
                state.MarkChalkMarked(id, ToolType.ChalkMark);
                EvidenceChalkGuide.PrepareDevelopmentStart(id);
            }
            if (record.definition.examinedInPlace) state.MarkTentedWithLetter(id,ToolType.LetterTentMarker,"A");
            else state.MarkTented(id,ToolType.EvidenceMarker,EvidenceDisplay.Number(id));
            if (record.definition.requiresTraceMarker) state.MarkTraceTented(id,ToolType.EvidenceMarker,number++);
            // Body/spatter need final photography before measuring; the other items
            // use the opposite order. Preserve each definition's actual gate.
            if (record.definition.requiresBodyRemoval || record.definition.measurementAfterFinalPhoto)
                state.MarkPhotographedFinal(id,ToolType.Photographer);
        }
        CompleteMeasurements();
        var tents = FindFirstObjectByType<EvidenceTentTool>(FindObjectsInactive.Include);
        var letters = FindFirstObjectByType<LetterTentTool>(FindObjectsInactive.Include);
        foreach (var record in state.AllRecords)
        {
            string id = record.definition.evidenceId;
            if (!record.definition.requiresBodyRemoval && !record.definition.measurementAfterFinalPhoto)
                state.MarkPhotographedFinal(id,ToolType.Photographer);
            if (record.status != EvidenceStatus.PhotographedFinal)
                throw new InvalidOperationException("Measured evidence fixture not ready: " + id);
            if (record.definition.examinedInPlace) letters.PrepareDevelopmentMarker(id);
            else
            {
                Vector3 point = EvidenceProp.FindById(id).transform.position;
                tents.PrepareDevelopmentMarker(record.tentNumber.Value,id,point + Vector3.right*.25f);
                if (record.traceTentNumber.HasValue)
                    tents.PrepareDevelopmentMarker(record.traceTentNumber.Value,id,point + Vector3.forward*.25f,true);
            }
        }
        tents.PrepareDevelopmentMarker(number,null,new Vector3(15.7f,1.2f,10.9f));
    }

    private static void CompleteSketch()
    {
        var sketch = CrimeSceneSketchManager.Instance;
        foreach (var e in sketch.Definition.evidenceElements.Where(e => e != null && e.required))
            sketch.PlaceEvidence(e.evidenceId, e.correctAnchorId);
        foreach (var slot in sketch.Definition.measurementSlots.Where(s => s != null && s.required))
            sketch.PlaceMeasurement(SketchMeasurementCard.MakeCardId(slot.requirementId, slot.referenceId), slot.slotId);
        if (!sketch.Submit()) throw new InvalidOperationException("Sketch fixture could not submit.");
    }

    private void PlacePlayer()
    {
        Vector3 feet; Vector3 target;
        if (ActivePoint == SceneTestStartingPoint.IOCDone)
        {
            var point = ScenePhotographyManager.Instance.Points.First();
            feet = point.transform.position;
            target = point.cameraTarget != null ? point.cameraTarget.position : feet + point.transform.forward;
        }
        else
        {
            // Clear approach to the rear kitchen, outside evidence notice radii.
            // Approaching an item must still exercise its actual discovery trigger.
            feet = new Vector3(15.55f, 1.2f, 10.8f);
            target = EvidenceProp.FindById("EVD-018") != null
                ? EvidenceProp.FindById("EVD-018").transform.position
                : new Vector3(17.75f, 2.26f, 14.3f);
            if (ActivePoint == SceneTestStartingPoint.KnifeBagging)
                target = EvidenceProp.FindById("EVD-014").transform.position;
            if (ActivePoint == SceneTestStartingPoint.BottleFingerprinting)
            {
                target = EvidenceProp.FindById("EVD-018").transform.position;
            }
            if (ActivePoint == SceneTestStartingPoint.BodyRecording || ActivePoint == SceneTestStartingPoint.AllEvidenceMeasured)
                target = EvidenceProp.FindById("BODY-A").transform.position;
            if (PhotoEvidenceId(ActivePoint) is string photoId)
                target = EvidenceProp.FindById(photoId).transform.position;
        }
        if (ActivePoint == SceneTestStartingPoint.FingerprintComparison)
        {
            var station = FingerprintComparisonStation.Instance;
            if (station == null) throw new InvalidOperationException("Missing fingerprint comparison desk.");
            target = station.transform.position;
            feet = target + Vector3.back * .9f;
            if (Physics.Raycast(feet + Vector3.up * 2, Vector3.down, out var ground, 5, ~0, QueryTriggerInteraction.Ignore))
                feet.y = ground.point.y + .02f;
        }
        if (ActivePoint == SceneTestStartingPoint.CustodyPass)
        {
            target = EvidenceBoxDepositZone.Instance.transform.position;
            feet = target + EvidenceBoxDepositZone.Instance.transform.forward * .4f;
            if (Physics.Raycast(feet + Vector3.up * 2, Vector3.down, out var floor, 5, ~0, QueryTriggerInteraction.Ignore)) feet.y = floor.point.y + .02f;
        }
        PreparedFeet = feet;
        var locomotor = FindFirstObjectByType<Oculus.Interaction.Locomotion.FirstPersonLocomotor>();
        if (locomotor == null) throw new InvalidOperationException("Missing player locomotor.");
        Vector3 direction = target - feet; direction.y = 0;
        // Use the SDK's own teleport path: it moves the rig origin and capsule together,
        // preserves tracked head/hand poses, clears velocity and grounds the player.
        locomotor.HandleLocomotionEvent(new Oculus.Interaction.Locomotion.LocomotionEvent(
            -32000, new Pose(feet, Quaternion.LookRotation(direction)),
            Oculus.Interaction.Locomotion.LocomotionEvent.TranslationType.Absolute,
            Oculus.Interaction.Locomotion.LocomotionEvent.RotationType.Absolute));
    }

    public void RestartSelectedTest()
    {
        if (!IsActive || IsPreparing) return;
        IsReady = false;
#if UNITY_EDITOR
        // A test session stays isolated until Play stops. Switching to Normal play
        // requires a new Play session, rather than a misleading normal-looking reload.
        if (UnityEditor.SessionState.GetInt(EditorSelectionKey, (int)ActivePoint) == 0)
            UnityEditor.SessionState.SetInt(EditorSelectionKey, (int)ActivePoint);
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
            gameObject.scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
        SceneManager.LoadScene(gameObject.scene.buildIndex);
#endif
    }

    private void BuildIndicator()
    {
        var go = new GameObject("Scene Test Indicator", typeof(RectTransform), typeof(Canvas));
        go.transform.SetParent(transform, false);
        go.transform.localScale = Vector3.one * .0006f;
        var rect = (RectTransform)go.transform; rect.sizeDelta = new Vector2(1000, 140);
        var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true; canvas.sortingOrder = 100;
        var follow = go.AddComponent<HudFollowCamera>(); follow.Distance = 1.2f;
        follow.LocalOffset = new Vector3(0, .32f, 0);
        var text = new GameObject("Test Status", typeof(RectTransform), typeof(TextMeshProUGUI));
        text.transform.SetParent(rect, false);
        indicator = text.GetComponent<TextMeshProUGUI>(); indicator.fontSize = 22;
        indicator.color = new Color(1, .84f, .2f); indicator.alignment = TextAlignmentOptions.Center;
        indicator.raycastTarget = false; indicator.text = "TEST MODE - preparing " + Label(ActivePoint);
        indicator.rectTransform.anchorMin = Vector2.zero; indicator.rectTransform.anchorMax = Vector2.one;
        indicator.rectTransform.offsetMin = indicator.rectTransform.offsetMax = Vector2.zero;
    }
#else
    public void RestartSelectedTest() { }
#endif
}
