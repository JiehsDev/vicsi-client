using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using PlayerController = Oculus.Interaction.Locomotion.CharacterController;

/// <summary>Confirmed comparison gate, destination waypoint, house traversal and physical vendor handoff.</summary>
public sealed class VendorPathGuideTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    private readonly StringBuilder report = new("=== VendorPathGuideTest ===\n");
    private int failures;
    public void RunAllTests() { DontDestroyOnLoad(gameObject); StartCoroutine(Run()); }
    private void Check(string label, bool ok) { report.AppendLine((ok ? "PASS: " : "FAIL: ") + label); if (!ok) failures++; }
    private static void Clear()
    {
        STCSNotificationUI.Instance?.HideImmediately();
        NotificationManager.Instance?.UI.ClearTooltip();
    }
    private IEnumerator Run()
    {
        AutomatedTestSandbox.Activate(nameof(VendorPathGuideTest));
        yield return null;
        var reasoning = ReasoningCheckpointManager.Instance; reasoning.enabled = false;
        HypothesisCheckpointManager.Instance.enabled = false;
        ProceduralTrainingGuidance.Instance.enabled = false;
        if (NarrativeEventManager.Instance != null) { NarrativeEventManager.Instance.enabled = false; NarrativeEventManager.Instance.StopAllCoroutines(); }
        Clear();
        Check("Scene photography is complete before the matched-print route fixture", ScenePhotographyTestUtil.CompleteRoute());
        yield return null; Clear();
        var witness = FindAnyObjectByType<VendorWitnessInteraction>();
        var guide = witness.GetComponent<VendorPathGuide>();
        var interview = witness.Interview;
        var destination = FindAnyObjectByType<ProcedureDestinationGuide>();
        Check("Only the physical vendor owns one path guide", guide != null && witness.GetComponents<VendorPathGuide>().Length == 1
            && FindObjectsByType<VendorPathGuide>(FindObjectsSortMode.None).Length == 1);
        if (guide == null) { Finish(); yield break; }
        var route = guide.Route;
        Check("Scene-specific route starts at the rear kitchen and exits toward the actual vendor", route != null && route.sceneName == witness.gameObject.scene.name
            && route.waypoints.Length >= 2 && Vector2.Distance(new Vector2(route.waypoints.Last().x, route.waypoints.Last().z), new Vector2(witness.InteractionPosition.x, witness.InteractionPosition.z)) < witness.InteractionRadius);
        var vendorPosition = witness.transform.position;
        var original = Camera.main; string oldTag = original.tag; original.tag = "Untagged";
        var rig = original.GetComponentInParent<OVRCameraRig>(); bool rigEnabled = rig.enabled; rig.enabled = false;
        var fixture = new GameObject("Vendor path test camera", typeof(Camera));
        var camera = fixture.GetComponent<Camera>(); camera.tag = "MainCamera";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var headField = typeof(VendorWitnessInteraction).GetField("playerHead", flags);
        var previousHead = headField.GetValue(witness); headField.SetValue(witness, camera.transform);
        var token = new object();
        try
        {
            camera.transform.position = route.waypoints[0] + Vector3.up * 1.6f;
            camera.transform.LookAt(route.waypoints[1]);
            guide.Refresh(); destination.Refresh();
            Check("No vendor navigation before knife fingerprint identification", !guide.IsVisible && !interview.FollowupUnlocked && destination.TargetId != "vendor-followup");
            var state = EvidenceStateManager.Instance;
            var knife = state.GetRecord("EVD-014");
            knife.fingerprintingDone = true; knife.fingerprintLabStatus = FingerprintLabStatus.SentToLab;
            Check("Real knife lab identification still resolves Paul", state.MarkMatched("EVD-014") == TransitionResult.Applied && knife.fingerprintLabMatchResult == "Paul");
            reasoning.Evaluate(); Clear(); guide.Refresh();
            destination.Refresh();
            Check("Confirmed comparison activates vendor trail and labelled waypoint before any reasoning response", reasoning.IsEligible("RC-03-FINGERPRINT")
                && !reasoning.IsAnswered("RC-03-FINGERPRINT") && guide.IsVisible && interview.RequiresFollowup
                && destination.IsVisible && destination.TargetId == "vendor-followup" && destination.GuidanceText.Contains("vendor")
                && Vector3.Distance(destination.TargetPosition, witness.InteractionPosition + Vector3.up * 1.4f) < .01f);
            reasoning.RecordResponse("RC-01-PETER", "RC-01-PETER-C");
            Clear(); guide.Refresh(); Check("A different reasoning answer leaves the confirmed vendor lead active", guide.IsVisible);
            var ui = FindAnyObjectByType<HypothesisCheckpointUI>(FindObjectsInactive.Include);
            var definition = reasoning.Definitions.checkpoints.Single(c => c.id == "RC-03-FINGERPRINT");
            Check("Current fingerprint question still integrates with its stable hypothesis", definition.integratedHypothesisId == "HYP-02");
            ui.ShowReasoning(definition, answer => reasoning.RecordResponse(definition.id, answer));
            ui.SelectResponse(0); guide.Refresh();
            destination.Refresh();
            Check("Reasoning modal temporarily hides navigation without relocking the vendor lead", ui.IsShowing && !guide.IsVisible && !destination.IsVisible
                && interview.RequiresFollowup && !reasoning.IsAnswered(definition.id));
            yield return null; ui.ConfirmSelection(); Clear(); guide.Refresh();
            Check("A low-weight reasoning answer preserves the already confirmed lead and its original scoring", reasoning.IsAnswered(definition.id)
                && reasoning.Records.Single(r => r.CheckpointId == definition.id).Weight == 1 && interview.FollowupUnlocked && guide.IsVisible);
            Check("Trail has readable ground chevrons with a finite render mesh", guide.ArrowPositions.Count >= 10
                && guide.VisualObject.GetComponent<MeshFilter>().sharedMesh.vertexCount == guide.ArrowPositions.Count * 12
                && guide.ArrowPositions.All(p => float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z)));
            Check("Breadcrumbs have no collision, input or UI raycaster", guide.VisualObject.GetComponentsInChildren<Collider>().Length == 0
                && guide.VisualObject.GetComponents<UnityEngine.EventSystems.BaseRaycaster>().Length == 0);
            Check("Existing stereo-safe depth-tested cue shader is reused", guide.VisualObject.GetComponent<Renderer>().sharedMaterial.shader.name == "ViCSI/Evidence Discovery Dot");
            if (System.Environment.GetCommandLineArgs().Contains("-vendorPathPreview")) CapturePreview(camera, guide);
            int beforeEvents = SessionLogger.Instance.EventCounts.Values.Sum();
            var scoreBefore = FinalDeductionAssessment.Score(FinalDeductionAssessment.CaptureLive(), false, FinalDeductionAssessment.Rules);
            // Isolate navigation writes from normal rigidbody settling during the
            // surrounding asynchronous photo/dialogue/locomotion tests.
            var positions = FindObjectsByType<EvidenceProp>(FindObjectsSortMode.None).ToDictionary(p => p, p => p.transform.position);
            for (int i = 0; i < 15; i++) guide.Refresh();
            Check("Repeated navigation refresh preserves all evidence transforms", positions.All(p => p.Key.transform.position == p.Value));
            var scoreAfter = FinalDeductionAssessment.Score(FinalDeductionAssessment.CaptureLive(), false, FinalDeductionAssessment.Rules);
            Check("Visual refresh writes no session events and changes no score", beforeEvents == SessionLogger.Instance.EventCounts.Values.Sum() && scoreBefore.totalScore == scoreAfter.totalScore);
            PlayerUIGate.Enter(token); guide.Refresh(); Check("Modal ownership hides path without acquiring input locks", !guide.IsVisible); PlayerUIGate.Exit(token);
            guide.Refresh(); Check("Path returns when the modal closes", guide.IsVisible);
            STCSNotificationUI.Instance.ShowLine("Vendor path test dialogue", "System", "System", "vendor-path-test");
            guide.Refresh(); Check("Spoken guidance hides navigation", !guide.IsVisible); Clear(); guide.Refresh();
            using (VendorPathGuide.HideForCapture())
            {
                Check("Photo capture hides arrows synchronously", !guide.IsVisible);
                using (VendorPathGuide.HideForCapture()) { guide.Refresh(); Check("Nested capture stays hidden", !guide.IsVisible); }
                Check("Inner disposal does not restore outer capture", !guide.IsVisible);
            }
            Check("After capture arrows return", guide.IsVisible);
            bool rendered = false, visibleInPhoto = false;
            System.Action<ScriptableRenderContext, Camera> captureCheck = (context, cam) =>
            { if (cam.name == "PhotoCaptureCamera") { rendered = true; visibleInPhoto |= guide.IsVisible || destination.IsVisible; } };
            RenderPipelineManager.beginCameraRendering += captureCheck;
            var listenerObject = new GameObject("Vendor path photo capture fixture");
            try
            {
                var listener = listenerObject.AddComponent<PhotoCaptureListener>();
                listener.CaptureScenePhoto(ScenePhotographyManager.Instance.Points[0]);
                Check("Actual photo render excludes path and waypoint and restores them afterward", rendered && !visibleInPhoto && guide.IsVisible && destination.IsVisible);
            }
            finally { RenderPipelineManager.beginCameraRendering -= captureCheck; Destroy(listenerObject); }
            Check("Every generated arrow has floor support and points toward the next exit-route leg", guide.ArrowPositions.Select((p,i) =>
                Physics.Raycast(p + Vector3.up * .1f, Vector3.down, out var hit, .2f, Physics.DefaultRaycastLayers & ~(1 << 2), QueryTriggerInteraction.Ignore)
                && Mathf.Abs(hit.point.y + .035f - p.y) < .01f && hit.normal.y > .9f && Mathf.Abs(guide.ArrowDirections[i].y) < .001f).All(x => x));
            ValidateTraversal(route);
            camera.transform.position = new Vector3(16.9f, 1.63f, .7f); Clear(); guide.Refresh();
            Check("Walking outside trims the interior trail behind the player", guide.IsVisible && guide.ArrowPositions.All(p => p.z < 2));
            camera.transform.position = route.waypoints[0] + Vector3.up * 5.5f; guide.Refresh();
            Check("Another floor cannot create a shortcut through the house", !guide.IsVisible);
            camera.transform.position = witness.InteractionPosition + Vector3.right * 1.2f; camera.transform.position = new Vector3(camera.transform.position.x, 1.63f, camera.transform.position.z);
            guide.Refresh(); Check("Arriving within the unchanged vendor interaction radius hides the path", !guide.IsVisible && witness.PlayerInRange());
            camera.transform.position = new Vector3(16.9f, 1.63f, .7f); guide.Refresh(); Check("Leaving without interviewing restores the path", guide.IsVisible);
            camera.transform.position = new Vector3(witness.InteractionPosition.x + 1.2f, 1.63f, witness.InteractionPosition.z);
            Clear(); bool opened = witness.Interact(); yield return null;
            guide.Refresh(); Check("Original physical vendor prompt/panel still opens without arrows", opened && interview.IsShowing && !guide.IsVisible);
            bool asked = interview.AskAnyoneElse(); guide.Refresh();
            Check("Follow-up uses original question and hides path throughout speaking", asked && interview.IsTalking && !guide.IsVisible);
            yield return VendorFollowupTest.DrainDialogue(interview);
            Clear(); camera.transform.position = new Vector3(16.9f, 1.63f, .7f); guide.Refresh();
            Check("Discovered vendor statement retires arrows permanently for this session", interview.IsDiscovered && !guide.IsVisible);
            destination.Refresh();
            Check("Delivered vendor answer also retires the waypoint and completion requirement", !destination.IsVisible && destination.TargetId == null
                && VendorFollowupInterview.CompletionBlockReason == null);
            guide.enabled = false; Check("Disable hides the world visual immediately", !guide.IsVisible); guide.enabled = true;
            Check("Vendor interaction remains at its authored position", witness.transform.position == vendorPosition);
        }
        finally
        {
            PlayerUIGate.Exit(token); headField.SetValue(witness, previousHead);
            original.tag = oldTag; rig.enabled = rigEnabled; Destroy(fixture);
        }
        yield return SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().path); yield return null; yield return null;
        var fresh = FindAnyObjectByType<VendorPathGuide>();
        Check("Scene restart resets both the lead gate and path visibility", fresh != null && !fresh.IsVisible && !VendorFollowupInterview.Instance.FollowupUnlocked);
        Finish();
    }
    private void ValidateTraversal(VendorPathRoute route)
    {
        var source = FindFirstObjectByType<PlayerController>();
        var probe = new GameObject("Vendor route capsule probe") { layer = 2 };
        // Meta locomotion uses standalone Physics.CapsuleCast queries, which do not
        // consult Physics.IgnoreCollision pairs. The probe replaces this player's capsule.
        var liveCapsule = (CapsuleCollider)typeof(PlayerController).GetField("_capsule", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(source);
        bool liveCapsuleEnabled = liveCapsule.enabled;
        liveCapsule.enabled = false;
        try
        {
            var capsule = probe.AddComponent<CapsuleCollider>(); capsule.radius = source.Radius; capsule.height = 1.7f;
            var controller = probe.AddComponent<PlayerController>(); controller.InjectCapsule(capsule);
            controller.LayerMask = source.LayerMask.value & ~(1 << 2); controller.MaxStep = source.MaxStep;
            controller.MaxSlopeAngle = source.MaxSlopeAngle; controller.SkinWidth = source.SkinWidth; controller.MaxReboundSteps = source.MaxReboundSteps;
            controller.SetPosition(route.waypoints[0] + Vector3.up * (controller.Height * .5f + .01f)); Physics.SyncTransforms(); controller.TryGround(.08f);
            foreach (var point in route.waypoints.Skip(1))
            {
                for (int i = 0; i < 1000; i++)
                {
                    Vector3 feet = controller.Pose.position - Vector3.up * controller.Height * .5f;
                    Vector3 direction = point - feet; direction.y = 0;
                    controller.Move(Vector3.ClampMagnitude(direction, 1.5f / 90) + Vector3.down * .02f); Physics.SyncTransforms();
                }
                var actual = controller.Pose.position - Vector3.up * controller.Height * .5f;
                if (point == route.waypoints.Last())
                {
                    // The endpoint identifies the vendor, whose solid collider must
                    // remain intact. Arrival means entering the actual talk radius,
                    // rather than overlapping the NPC's body.
                    var vendor = FindAnyObjectByType<VendorWitnessInteraction>();
                    Vector3 offset = actual - vendor.InteractionPosition; offset.y = 0;
                    report.AppendLine("INFO: vendor arrival feet=" + actual.ToString("F3") + "; target=" + point.ToString("F3")
                        + "; interaction=" + vendor.InteractionPosition.ToString("F3") + "; distance=" + offset.magnitude.ToString("F3"));
                    // VendorWitnessInteraction measures horizontal distance; the
                    // approach crosses outdoor ground with a different elevation.
                    Check("Real player capsule reaches the vendor's interaction radius without entering the NPC collider",
                        offset.magnitude <= vendor.InteractionRadius);
                }
                else Check("Real player capsule can follow the exit route to " + point.ToString("F2") + " actual=" + actual.ToString("F2"), Vector3.Distance(actual, point) < .18f);
            }
        }
        finally { DestroyImmediate(probe); liveCapsule.enabled = liveCapsuleEnabled; Physics.SyncTransforms(); }
    }
    private void CapturePreview(Camera camera, VendorPathGuide guide)
    {
        var target = RenderTexture.GetTemporary(1120, 800, 24);
        var previousTarget = camera.targetTexture; var previousActive = RenderTexture.active;
        var image = new Texture2D(1120, 800, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0,0,1120,800),0,0); image.Apply();
            System.IO.Directory.CreateDirectory("docs/evidence/vendor-path-guide");
            System.IO.File.WriteAllBytes("docs/evidence/vendor-path-guide/kitchen-trail.png", image.EncodeToPNG());
            var withPath = image.GetPixels32();
            guide.VisualObject.GetComponent<Renderer>().enabled = false;
            camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0,0,1120,800),0,0); image.Apply();
            var withoutPath = image.GetPixels32(); int changed = 0;
            for (int i=0; i<withPath.Length; i++)
            {
                var a=withPath[i]; var b=withoutPath[i];
                if (a.g>a.r+20 && a.b>a.r+20 && Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b)>30) changed++;
            }
            Check("Native camera pixels show cyan arrows in the real kitchen view [pixels="+changed+"]", changed>20);
        }
        finally
        {
            camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target); Destroy(image); guide.Refresh();
        }
    }
    private void Finish()
    {
        report.AppendLine(failures == 0 ? "=== PASS, 0 failures ===" : "=== FAIL: " + failures + " failure(s) ===");
        Report = report.ToString(); IsDone = true;
    }
}
