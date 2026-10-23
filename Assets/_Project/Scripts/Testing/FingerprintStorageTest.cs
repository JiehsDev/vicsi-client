using System.Collections;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;

/// <summary>Physical kit handlers -> derived held card -> actual right B input -> checkpoint. No uploads.</summary>
public sealed class FingerprintStorageTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    private readonly StringBuilder report = new("=== FingerprintStorageTest ===\n");
    private int failures;
    public void RunAllTests() { DontDestroyOnLoad(gameObject); StartCoroutine(Run()); }
    private void Check(string label, bool ok) { report.AppendLine("  " + (ok ? "PASS: " : "FAIL: ") + label); if (!ok) failures++; }
    private static void Clear() { STCSNotificationUI.Instance.HideImmediately(); NotificationManager.Instance.UI.ClearTooltip(); }
    private IEnumerator Run()
    {
        AutomatedTestSandbox.Activate(nameof(FingerprintStorageTest));
        yield return null;
        var reasoning = ReasoningCheckpointManager.Instance;
        reasoning.enabled = false; HypothesisCheckpointManager.Instance.enabled = false; ProceduralTrainingGuidance.Instance.enabled = false;
        Clear();
        var state = EvidenceStateManager.Instance; var inventory = EvidenceInventory.Instance; var held = HeldItemCoordinator.Instance;
        var kit = FingerprintKitTool.Instance; var tape = FingerprintTapeTool.Instance;
        var knife = state.GetRecord("EVD-014"); var marker = EvidenceFingerprintMarker.FindByEvidenceId("EVD-014");
        var prompt = FindAnyObjectByType<HeldEvidenceStorePrompt>();
        var hand = FindAnyObjectByType<OVRCameraRig>().rightHandAnchor;
        string sample = EvidenceInventory.FingerprintSampleId("EVD-014");
        var powderVisual = marker.transform.Find("PowderedFingerprintVisual");
        var latentRenderer = marker.GetComponent<Renderer>();
        Check("Fresh print hides the linked powder model", powderVisual != null && !powderVisual.gameObject.activeSelf);
        marker.GetComponent<UVRevealable>().TriggerReveal();
        Check("UV discovery shows only the latent mark", latentRenderer.enabled && !powderVisual.gameObject.activeSelf);
        Check("Fresh session has no lifted or stored print", !knife.fingerprintLifted && !knife.fingerprintingDone && !inventory.IsInInventory(sample));
        Check("Comparison action UI stays hidden without an eligible card",!FingerprintComparisonStation.Instance.IsShowing&&!FingerprintComparisonStation.Instance.IsActionPromptVisible);
        Check("Premature lift is refused", kit.TryLift(marker) != TransitionResult.Applied && !knife.fingerprintLifted);
        Clear();
        ScenePhotographyTestUtil.CompleteRoute();
        // Earlier documentation/blood procedure has its own real-tool regression suite.
        knife.status = EvidenceStatus.PhotographedFinal; knife.swabbingDone = true; knife.swabSampleSealed = true;
        Check("Measured knife is sketched before powdering", MeasurementTestUtil.CompleteSketchFor("EVD-014"));
        Check("Powder required before brushing", !kit.TryDust(marker));
        kit.TryLoadDust();
        double deadline = Time.realtimeSinceStartupAsDouble + 12;
        while (!marker.IsDusted && Time.realtimeSinceStartupAsDouble < deadline) { kit.TryDust(marker); yield return null; }
        Check("Powder and sustained brush reveal the print", marker.IsDusted);
        var powderRenderer = powderVisual.GetComponentInChildren<Renderer>(true);
        Check("Completed powdering shows the supplied textured model instead of the box", powderVisual.gameObject.activeSelf
            && powderRenderer.enabled && !latentRenderer.enabled
            && powderRenderer.sharedMaterial.mainTexture != null
            && powderRenderer.sharedMaterial.name == "Matte_White_Fingerprint_Powder");
        marker.GetComponent<UVRevealable>().TriggerReveal();
        Check("Further UV reveal cannot restore the gray box", !latentRenderer.enabled && powderVisual.gameObject.activeSelf);
        Clear();
        held.ToggleEquipTool(ToolType.FingerprintTape, hand);
        var before = FinalDeductionAssessment.Score(FinalDeductionAssessment.CaptureLive(), false, FinalDeductionAssessment.Rules);
        var prefabField = typeof(FingerprintTapeTool).GetField("liftedVisualPrefab", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var prefab = prefabField.GetValue(tape);
        prefabField.SetValue(tape, null);
        Check("Missing card model cannot consume the print", kit.TryLift(marker) != TransitionResult.Applied && !knife.fingerprintLifted && marker.gameObject.activeSelf);
        Check("Failed lift preserves the powdered visual", powderVisual.gameObject.activeInHierarchy && !latentRenderer.enabled);
        prefabField.SetValue(tape, prefab); Clear();
        Check("Lift succeeds with existing wired card", kit.TryLift(marker) == TransitionResult.Applied);
        Check("Lift is distinct from collection and match", knife.fingerprintLifted && !knife.fingerprintingDone && !state.IsFingerprintMatched("EVD-014"));
        Check("Card replaces tool in held-item coordinator", held.CurrentCategory == HeldItemCoordinator.HeldCategory.Evidence && held.CurrentEvidenceId == sample && PlayerToolRegistry.CurrentRole == ToolType.None);
        Check("Derived card has source/type/visual and requires explicit storage", inventory.GetSampleSource(sample) == "EVD-014" && inventory.IsFingerprintSample(sample)
            && inventory.HeldVisual != null && inventory.HeldVisual.activeInHierarchy && inventory.RequiresExplicitStorage && !inventory.IsInInventory(sample));
        Check("No duplicate roster evidence or knife custody change", state.GetRecord(sample) == null && knife.custodyLocation == EvidenceCustodyLocation.Scene && knife.status == EvidenceStatus.PhotographedFinal);
        Check("Original marker removed and duplicate lift refused", !marker.gameObject.activeSelf && kit.TryLift(marker) != TransitionResult.Applied);
        Check("Successful tape lift also removes the powdered model", !powderVisual.gameObject.activeInHierarchy);
        Check("RC-03 and vendor lead remain gated before storage", !reasoning.IsEligible("RC-03-FINGERPRINT") && !VendorFollowupInterview.Instance.FollowupUnlocked);
        Clear(); yield return null; yield return null;
        Check("Held-card guidance appears before comparison", prompt.IsPromptShown);
        // Exercise the existing XR right-secondaryButton binding, not a direct store callback.
        InputSystem.RegisterLayout("{\"name\":\"ViCSIFingerprintTestController\",\"extend\":\"XRController\",\"controls\":[{\"name\":\"secondaryButton\",\"layout\":\"Button\"}]}");
        var device = InputSystem.AddDevice("ViCSIFingerprintTestController");
        InputSystem.SetDeviceUsage(device, UnityEngine.InputSystem.CommonUsages.RightHand);
        InputSystem.QueueDeltaStateEvent((ButtonControl)device["secondaryButton"], true);
        yield return null; yield return null;
        InputSystem.QueueDeltaStateEvent((ButtonControl)device["secondaryButton"], false);
        yield return null; InputSystem.RemoveDevice(device);
        Check("Actual B cannot store an unmatched fingerprint", !inventory.IsInInventory(sample) && inventory.CurrentHeldEvidenceId==sample && !knife.fingerprintingDone);
        Clear();
        Check("Wrong reference cannot identify or store the card", !FingerprintComparisonTestUtil.CompareSourceWithReference("EVD-014",0)
            && !state.IsFingerprintMatched("EVD-014") && !inventory.ConfirmSampleStorage() && inventory.CurrentHeldEvidenceId==sample);
        Check("Pending comparison blocks unrelated tools and source recording",ProcedureInterlock.BlocksUnrelatedActions
            && !PlayerToolRegistry.CanEquip(ToolType.Recorder) && !state.CanAdvanceTo("EVD-014",EvidenceStatus.Logged));
        Clear();
        Check("Correct comparison identifies the held card before storage",FingerprintComparisonTestUtil.CompareStoredSource("EVD-014")
            &&state.IsFingerprintMatched("EVD-014")&&!inventory.IsExplicitlyStoredSample(sample)&&inventory.CurrentHeldEvidenceId==sample);
        Clear();yield return null;yield return null;
        device=InputSystem.AddDevice("ViCSIFingerprintTestController");InputSystem.SetDeviceUsage(device,UnityEngine.InputSystem.CommonUsages.RightHand);
        InputSystem.QueueDeltaStateEvent((ButtonControl)device["secondaryButton"],true);yield return null;yield return null;
        InputSystem.QueueDeltaStateEvent((ButtonControl)device["secondaryButton"],false);yield return null;InputSystem.RemoveDevice(device);
        InputSystem.RemoveLayout("ViCSIFingerprintTestController");
        Check("Actual right B stores the matched fingerprint and frees the hand", inventory.IsInInventory(sample) && inventory.IsExplicitlyStoredSample(sample)
            && inventory.GetCustody(sample) == EvidenceCustodyLocation.Inventory && inventory.CurrentHeldEvidenceId == null);
        Check("Storage retains confirmed processing and identity", knife.fingerprintingDone && state.IsFingerprintMatched("EVD-014")
            && knife.fingerprintLabMatchIdentities.SequenceEqual(new[]{"Paul"}));
        Check("Matched storage releases the mandatory continuation gate",!ProcedureInterlock.BlocksUnrelatedActions&&!VendorFollowupInterview.Instance.FollowupUnlocked);
        Check("Completed comparison does not advertise another matching action",!FingerprintComparisonStation.Instance.CanOpen
            &&!FingerprintComparisonStation.Instance.IsActionPromptVisible&&!FingerprintComparisonStation.Instance.GuidanceNeeded);
        Check("Stored sample preserves EVD-014 association", inventory.GetSampleSource(sample) == "EVD-014" && inventory.GetDefinition(sample).evidenceId == sample);
        SessionLogger.Instance.Flush();
        var sampleEvents = SessionLogReader.ReadEvents(SessionLogger.Instance.ActiveLogFilePath)
            .Where(e => e.eventType == "EvidenceAddedToInventory" && e.targetId == sample).ToArray();
        Check("Session history logs one non-scored fingerprint sample with its source association", sampleEvents.Length == 1
            && SessionLogReader.PayloadValue(sampleEvents[0], "sourceEvidenceId") == "EVD-014"
            && SessionLogReader.PayloadValue(sampleEvents[0], "sampleType") == "Fingerprint"
            && SessionLogReader.PayloadValue(sampleEvents[0], "scored") == "false");
        Check("Knife remains available for recording and packaging", knife.status == EvidenceStatus.PhotographedFinal && knife.custodyLocation == EvidenceCustodyLocation.Scene && !inventory.IsInInventory("EVD-014"));
        var after = FinalDeductionAssessment.Score(FinalDeductionAssessment.CaptureLive(), false, FinalDeductionAssessment.Rules);
        Check("Lifting/storing does not change assessment score", before.totalScore == after.totalScore && before.reasoningComponent == after.reasoningComponent
            && before.relationshipComponent == after.relationshipComponent && before.conclusionComponent == after.conclusionComponent);
        Check("Correct reference identity survives storage",state.IsFingerprintMatched("EVD-014")&&knife.fingerprintLabMatchResult=="Paul"&&knife.fingerprintLabMatchIdentities.SequenceEqual(new[]{"Paul"}));
        reasoning.Evaluate();
        Check("Comparison makes RC-03 eligible without unlocking vendor", reasoning.IsEligible("RC-03-FINGERPRINT") && !VendorFollowupInterview.Instance.FollowupUnlocked);
        Check("Confirming RC-03 unlocks vendor through existing condition", reasoning.RecordResponse("RC-03-FINGERPRINT", "RC-03-FINGERPRINT-C") && VendorFollowupInterview.Instance.FollowupUnlocked);
        Check("Locked checkpoint cannot repeat", !reasoning.RecordResponse("RC-03-FINGERPRINT", "RC-03-FINGERPRINT-C"));
        Clear();
        Check("Stored print can be re-equipped and stored again", held.EquipEvidence(sample, hand) && held.TryStoreHeldEvidence());
        yield return SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().path); yield return null;
        knife = EvidenceStateManager.Instance.GetRecord("EVD-014");
        Check("Restart clears lifted flag, collection and sample inventory", !knife.fingerprintLifted && !knife.fingerprintingDone && !EvidenceInventory.Instance.AllEntryIds.Contains(sample));
        Check("Restart restores undusted marker and locked reasoning/vendor", !EvidenceFingerprintMarker.FindByEvidenceId("EVD-014").IsDusted
            && !ReasoningCheckpointManager.Instance.IsAnswered("RC-03-FINGERPRINT") && !VendorFollowupInterview.Instance.FollowupUnlocked);
        marker = EvidenceFingerprintMarker.FindByEvidenceId("EVD-014");
        Check("Restart hides powder and restores latent UV presentation", !marker.transform.Find("PowderedFingerprintVisual").gameObject.activeSelf);
        marker.GetComponent<UVRevealable>().TriggerReveal();
        Check("Fresh UV reveal after restart displays the latent mark", marker.GetComponent<Renderer>().enabled
            && !marker.transform.Find("PowderedFingerprintVisual").gameObject.activeSelf);
        report.AppendLine(failures == 0 ? "=== PASS ===" : "=== " + failures + " FAILURE(S) ===");
        Report = report.ToString(); IsDone = true; Debug.Log(Report);
    }
}



