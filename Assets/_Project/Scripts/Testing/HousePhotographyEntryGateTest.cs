using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>Real IOC progression, outdoor shutters, doorway collision and room-scale containment.</summary>
public sealed class HousePhotographyEntryGateTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    readonly StringBuilder report = new("=== HousePhotographyEntryGateTest ===\n");
    int failures;
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    void Check(string name, bool pass) { report.AppendLine((pass ? "PASS: " : "FAIL: ") + name); if (!pass) failures++; }
    public void RunAllTests() => StartCoroutine(Run());
    IEnumerator Run()
    {
        AutomatedTestSandbox.Activate(nameof(HousePhotographyEntryGateTest));
        RelationshipReviewTestUtil.BeginCaseFixture();
        yield return null;
        var gate = FindAnyObjectByType<HousePhotographyEntryGate>();
        var cordon = FindAnyObjectByType<SceneCordon>();
        var photos = ScenePhotographyManager.Instance;
        var assessment = FindAnyObjectByType<IOCAssessmentInteraction>();
        Check("Saved gate, collider, route and IOC workflow exist", gate != null && gate.EntranceWall != null && photos != null && cordon != null && assessment != null);
        if (gate == null || photos == null || cordon == null || assessment == null) { Finish(); yield break; }
        var wall = gate.EntranceWall;
        Check("Invisible wall has no renderer and uses solid collision", gate.GetComponentsInChildren<Renderer>().Length == 0 && !wall.isTrigger);
        Check("Fresh session starts at IOC turnover without photography wall", SceneTurnover.IsPending && !gate.IsBlocking && !wall.enabled);
        Check("Existing briefing gate remains enforced", !cordon.CanEnter);
        SceneTurnover.Controller.CompleteWithoutDialogue();
        gate.Refresh();
        Check("IOC assessment permits authorized entry before photography", SceneTurnover.Phase == ScenarioPhase.IOCAssessment && cordon.CanEnter && !wall.enabled);
        bool observed = true;
        foreach (var observation in assessment.Definition.observations) observed &= SceneTurnover.RecordIOCObservation(observation.observationId);
        Check("IOC body, evidence and security observations remain recordable", observed && SceneTurnover.AssessmentReady);
        Check("IOC assessment completes normally", SceneTurnover.CompleteIOCAssessment());
        Check("IOC decision has no photography obstruction", !gate.IsBlocking && !wall.enabled && cordon.CanEnter);
        Check("SOCO can be requested normally", SceneTurnover.DecideSOCOAssistance(true));
        Check("SOCO request keeps prior entry rules", !gate.IsBlocking && !wall.enabled && cordon.CanEnter);
        Check("SOCO handover can begin normally", SceneTurnover.TryAdvance(ScenarioPhase.SOCOTurnover));
        Check("SOCO handover has no photography obstruction", !gate.IsBlocking && !wall.enabled && cordon.CanEnter);
        Check("SOCO investigation begins normally", SceneTurnover.TryAdvance(ScenarioPhase.SOCOInvestigation));
        Check("SOCO entry blocks immediately before outdoor photographs", gate.IsBlocking && wall.enabled && !cordon.CanEnter);
        Physics.SyncTransforms();
        var frame = GameObject.Find("Vicsi_House").transform.Find("DoorFrame").GetComponent<Renderer>().bounds;
        Check("Wall spans the actual front doorway", Mathf.Abs(wall.bounds.center.x - frame.center.x) < .01f && Mathf.Abs(wall.bounds.center.z - frame.min.z) < .01f && wall.bounds.size.x >= frame.size.x && wall.bounds.min.y <= frame.min.y + .01f);
        var character = FindAnyObjectByType<Oculus.Interaction.Locomotion.CharacterController>();
        Check("Player locomotion collides with wall layer", (character.LayerMask.value & (1 << wall.gameObject.layer)) != 0);
        var ray = new Ray(wall.bounds.center - Vector3.forward * 1.5f, Vector3.forward);
        Check("Forward doorway crossing hits the enabled wall", wall.Raycast(ray, out _, 3f));
        Check("All required outdoor photo stations remain outside the wall and hot zone", photos.Points.Where(p => p.isRequired && (p.phase == ScenePhotoPhase.Exterior || p.phase == ScenePhotoPhase.Entrance)).All(p => !cordon.RequiresBriefingAt(p.transform.position + Vector3.up * 1.6f) && !wall.bounds.Contains(p.transform.position + Vector3.up * 1.6f)));

        var original = Camera.main;
        var rig = original.GetComponentInParent<OVRCameraRig>();
        bool tracked = rig.enabled; rig.enabled = false; original.tag = "Untagged";
        var fixture = new GameObject("Outdoor entry test camera", typeof(Camera)); fixture.tag = "MainCamera";
        fixture.transform.SetParent(rig.transform, true);
        var camera = fixture.GetComponent<Camera>();
        var locomotor = FindAnyObjectByType<Oculus.Interaction.Locomotion.FirstPersonLocomotor>();
        locomotor.InjectPlayerEyes(camera.transform);
        try
        {
            var enforce = typeof(SceneCordon).GetMethod("EnforceEntry", Private);
            camera.transform.position = new Vector3(wall.bounds.center.x, 2.8f, wall.bounds.min.z - 1f);
            enforce.Invoke(cordon, null);
            int returns = cordon.EntryReturns;
            camera.transform.position += Vector3.forward * 2f;
            enforce.Invoke(cordon, null);
            Check("Room-scale or teleport bypass is returned outside", cordon.EntryReturns == returns + 1 && !cordon.RequiresBriefingAt(camera.transform.position));
            STCSNotificationUI.Instance?.HideImmediately();
            typeof(ScenePhotographyManager).GetMethod("ActivateFirstPoint", Private).Invoke(photos, null);
            Capture(photos.Points.First(p => p.phase == ScenePhotoPhase.Exterior), camera);
            Check("Exterior shutter completes the establishing photograph", photos.IsPhaseCompleted(ScenePhotoPhase.Exterior));
            Check("Entrance shot is still required before entry", gate.IsBlocking && wall.enabled && !cordon.CanEnter);
            Capture(photos.Points.First(p => p.phase == ScenePhotoPhase.Entrance), camera);
            Check("Entrance shutter completes the approach photograph", photos.IsPhaseCompleted(ScenePhotoPhase.Entrance));
            Check("Door opens immediately after both outdoor photographs", gate.OutdoorPhotographyComplete && !gate.IsBlocking && !wall.enabled && cordon.CanEnter);
            Check("Interior route remains unfinished and accessible", !photos.IsScenePhotographyComplete && photos.CurrentPoint != null && photos.CurrentPoint.phase == ScenePhotoPhase.Overall);
            Check("Open gate no longer collides with crossing", !Physics.RaycastAll(ray, 3f).Any(hit => hit.collider == wall));
            Check("Remaining real shutter route still completes", ScenePhotographyTestUtil.CompleteRoute());
            gate.Refresh();
            Check("Completed route keeps entry open", !wall.enabled && cordon.CanEnter);
        }
        finally
        {
            locomotor.InjectPlayerEyes(original.transform); original.tag = "MainCamera"; rig.enabled = tracked; Destroy(fixture);
        }
        Finish();
    }
    static void Capture(ScenePhotoPoint point, Camera camera)
    {
        camera.transform.position = point.transform.position + Vector3.up * 1.6f;
        camera.transform.rotation = point.cameraTarget != null ? Quaternion.LookRotation(point.cameraTarget.position - camera.transform.position) : point.transform.rotation;
        typeof(ScenePhotographyManager).GetMethod("HandleShutter", Private).Invoke(ScenePhotographyManager.Instance,
            new object[] { Object.FindAnyObjectByType<PhotographTool>(FindObjectsInactive.Include), false });
    }
    void Finish() { report.AppendLine(failures == 0 ? "=== PASS, 0 failures ===" : "=== FAIL: " + failures + " failure(s) ==="); Report = report.ToString(); IsDone = true; }
}
