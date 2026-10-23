using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;

/// <summary>Real utility-wheel/XR input, height transform invariants and destructive scene-navigation checks in the offline sandbox.</summary>
public sealed class SettingsPauseMenuTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    private readonly StringBuilder report = new();
    private int failures;
    private InputDevice right, left;
    private bool hadPreference;
    private float oldPreference;
    private bool hadSpeedPreference;
    private float oldSpeedPreference;
    public void RunAllTests() { DontDestroyOnLoad(gameObject); StartCoroutine(Run()); }
    private void Check(string name, bool ok) { if (!ok) failures++; report.AppendLine((ok ? "PASS: " : "FAIL: ") + name); }
    private void OnEnable() => SceneManager.sceneLoaded += SceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= SceneLoaded;
    private void SceneLoaded(Scene scene, LoadSceneMode mode) => Isolate();
    private static void Isolate()
    {
        AutomatedTestSandbox.Activate(nameof(SettingsPauseMenuTest));
        var pre = Object.FindAnyObjectByType<PreSessionFlowController>();
        if (pre != null) { pre.enabled = false; var token = typeof(PreSessionFlowController).GetField("uiGateToken", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(pre); if (token != null) PlayerUIGate.Exit(token); }
        Object.FindAnyObjectByType<StudentIdentificationPanel>(FindObjectsInactive.Include)?.Hide();
        if (ReasoningCheckpointManager.Instance != null) ReasoningCheckpointManager.Instance.enabled = false;
        if (HypothesisCheckpointManager.Instance != null) HypothesisCheckpointManager.Instance.enabled = false;
        if (ProceduralTrainingGuidance.Instance != null) ProceduralTrainingGuidance.Instance.enabled = false;
        if (NarrativeEventManager.Instance != null) { NarrativeEventManager.Instance.enabled = false; NarrativeEventManager.Instance.StopAllCoroutines(); }
        STCSNotificationUI.Instance?.HideImmediately();
        Object.FindAnyObjectByType<HypothesisCheckpointUI>()?.AnswerForTest(0, 0);
        NotificationManager.Instance?.UI.ClearTooltip(); NotificationManager.Instance?.UI.HidePrompt();
    }
    private IEnumerator Press(InputDevice device, string control, bool held)
    {
        InputSystem.QueueDeltaStateEvent((ButtonControl)device[control], held ? 1f : 0f);
        yield return null; yield return null;
    }
    private static void Click(SettingsPauseMenu menu, string id)
    {
        var target = menu.Pointer.Targets.First(t => t.id == "settings-" + id);
        menu.ResponsiveLayout.EnsureVisible(target.rect);
        Vector3 point = menu.Pointer.WorldCentre(target.id);
        menu.Pointer.ClickWithRay(new Ray(point - target.rect.forward * .5f, target.rect.forward));
    }
    private IEnumerator Run()
    {
        hadPreference = PlayerPrefs.HasKey(PlayerHeightOffset.PreferenceKey);
        oldPreference = PlayerPrefs.GetFloat(PlayerHeightOffset.PreferenceKey);
        hadSpeedPreference = PlayerPrefs.HasKey(PlayerWalkingSpeed.PreferenceKey);
        oldSpeedPreference = PlayerPrefs.GetFloat(PlayerWalkingSpeed.PreferenceKey);
        try
        {
            yield return null; Isolate(); yield return null;
            InputSystem.RegisterLayout("{\"name\":\"ViCSISettingsController\",\"extend\":\"XRController\",\"controls\":[{\"name\":\"primaryButton\",\"layout\":\"Button\",\"format\":\"FLT\"},{\"name\":\"secondaryButton\",\"layout\":\"Button\",\"format\":\"FLT\"},{\"name\":\"triggerPressed\",\"layout\":\"Button\",\"format\":\"FLT\"},{\"name\":\"thumbstick\",\"layout\":\"Vector2\",\"aliases\":[\"primary2DAxis\"]}]}");
            right = InputSystem.AddDevice("ViCSISettingsController"); InputSystem.SetDeviceUsage(right, CommonUsages.RightHand);
            left = InputSystem.AddDevice("ViCSISettingsController"); InputSystem.SetDeviceUsage(left, CommonUsages.LeftHand);
            var menu = Object.FindAnyObjectByType<SettingsPauseMenu>();
            Check("Settings owner bootstraps into scenario without manual wiring", menu != null && menu.Height != null && !menu.IsOpen);
            if (menu == null) yield break;
            var wheel = Object.FindAnyObjectByType<UtilityMenuController>();
            var entries = (List<UtilityMenuController.MenuEntry>)typeof(UtilityMenuController).GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(wheel);
            var available = entries.Where(e => e.IsAvailable).ToList();
            int index = available.FindIndex(e => e.displayName == "Settings");
            Check("Existing utility wheel has exactly one functional Settings entry", index >= 0 && available.Count(e => e.displayName == "Settings") == 1);
            yield return Press(left, "primaryButton", true);
            Check("Actual left X opens the existing utility wheel", wheel.IsCanvasActive);
            float angle = (index + .5f) * 2 * Mathf.PI / available.Count;
            InputSystem.QueueDeltaStateEvent((Vector2Control)right["thumbstick"], new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)));
            yield return null; yield return null;
            InputSystem.QueueDeltaStateEvent((ButtonControl)right["primaryButton"], 1f);
            yield return Press(left, "primaryButton", false);
            Check("X release opens Settings and simultaneous held A cannot immediately select Close", menu.IsOpen);
            Check("Settings owns A at modal priority and suppresses jump/tools", PlayerUIGate.ConfirmOwnerToken == menu && PlayerUIGate.SuppressGameplayJump && ToolInputLock.HasUIOwners);
            yield return Press(right, "primaryButton", false);
            InputSystem.QueueDeltaStateEvent((Vector2Control)right["thumbstick"], Vector2.zero);
            var follow = menu.Panel.GetComponent<HudFollowCamera>();
            Check("Tablet panel enrolls in wall-safe shared layout above dialogue", follow.SharedLayout && follow.ObstacleAwarePlacement && follow.PlacementPriority > PlayerUIGate.CriticalPriority
                && ((RectTransform)menu.Panel.transform).rect.width * menu.Panel.transform.lossyScale.x < .7f && follow.LocalOffset.y < 0);
            // A clear virtual fixture exercises real settings panel bounds near a wall.
            var cameraObject = new GameObject("SettingsGeometryCamera"); var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
            camera.transform.position = new Vector3(40, 1.7f, 40);
            typeof(HudFollowCamera).GetField("targetCamera", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(follow, camera);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "SettingsWallFixture";
            wall.transform.position = camera.transform.position + Vector3.forward * .9f; wall.transform.localScale = new Vector3(1.4f, 3, .08f);
            Physics.SyncTransforms(); yield return new WaitForSecondsRealtime(.5f);
            Check("Settings remains readable on the near side of a wall", !follow.IsLayoutSuppressed && follow.PanelFits(camera.transform.position, menu.Panel.transform.position, menu.Panel.transform.rotation));
            wall.transform.position = camera.transform.position + Vector3.forward * .30f;
            wall.transform.localScale = new Vector3(10, 3, .08f);
            Physics.SyncTransforms(); yield return null; yield return null;
            Check("Settings defers its canvas when no readable wall-safe slot fits", follow.IsLayoutSuppressed);
            yield return Press(right, "secondaryButton", true);
            Check("B still closes a spatially hidden Settings panel and releases its locks", !menu.IsOpen && !PlayerUIGate.IsBlocked);
            yield return Press(right, "secondaryButton", false);
            Destroy(wall);
            menu.Show(); yield return new WaitForSecondsRealtime(.3f);
            menu.enabled = false;
            Check("Disabling Settings releases its modal ownership and hides its panel", !menu.IsOpen && !PlayerUIGate.IsBlocked);
            menu.enabled = true; menu.Show(); yield return null;
            Check("Settings can reopen after disable/enable without stale holds", menu.IsOpen && PlayerUIGate.ConfirmOwnerToken == menu);
            CaptureMenu(menu);
            var speed = menu.WalkingSpeed;
            speed.ResetSpeed(); float defaultWalking = speed.Locomotor.SpeedFactor;
            float defaultRunning = speed.Locomotor.RunningSpeedFactor, defaultCrouching = speed.Locomotor.CrouchSpeedFactor;
            Check("Normal authored movement factors remain nonzero", defaultWalking>0 && defaultRunning>0 && defaultCrouching>0);
            yield return null; Click(menu, "speed-plus");
            Check("Walking speed plus saves 110% and changes real locomotion", Mathf.Abs(speed.CurrentMultiplier - 1.1f) < .001f
                && Mathf.Abs(speed.Locomotor.SpeedFactor - defaultWalking * 1.1f) < .001f && speed.UsesPreferredSpeed);
            yield return null; Click(menu, "speed-minus");
            Check("Walking speed minus restores 100%", Mathf.Abs(speed.CurrentMultiplier - 1f) < .001f);
            speed.SetPreferredSpeed(20);
            Check("Walking speed clamps to 200%", Mathf.Abs(speed.CurrentMultiplier - 2f) < .001f);
            speed.SetPreferredSpeed(-20);
            Check("Walking speed clamps to 50%", Mathf.Abs(speed.CurrentMultiplier - .5f) < .001f);
            Check("Invalid speed values are rejected", !speed.SetPreferredSpeed(float.NaN) && !speed.SetPreferredSpeed(float.PositiveInfinity));
            yield return null; Click(menu, "speed-reset");
            Check("Reset Walking Speed clears the preference and restores authored movement factors", !speed.UsesPreferredSpeed
                && Mathf.Abs(speed.Locomotor.SpeedFactor-defaultWalking)<.001f
                && Mathf.Abs(speed.Locomotor.RunningSpeedFactor-defaultRunning)<.001f
                && Mathf.Abs(speed.Locomotor.CrouchSpeedFactor-defaultCrouching)<.001f);
            speed.SetPreferredSpeed(1.4f);
            var photograph = Object.FindAnyObjectByType<PhotographTool>();
            var aim = typeof(PhotographTool).GetMethod("SetAiming", BindingFlags.Instance | BindingFlags.NonPublic);
            float aimFactor = (float)typeof(PhotographTool).GetField("aimMoveSpeedMultiplier", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(photograph);
            aim.Invoke(photograph, new object[]{true});
            Check("Actual camera aiming slowdown composes with walking preference", Mathf.Abs(speed.Locomotor.SpeedFactor-defaultWalking*1.4f*aimFactor)<.001f);
            speed.SetPreferredSpeed(1.6f); aim.Invoke(photograph, new object[]{false});
            Check("Lowering camera restores the latest preferred speed without stale cached values", Mathf.Abs(speed.Locomotor.SpeedFactor-defaultWalking*1.6f)<.001f);
            speed.SetPreferredSpeed(1.3f);
            yield return null; menu.Cancel(); yield return null;
            Check("Speed row remains in the existing scrollable settings panel", menu.Pointer.Targets.Count(t=>t.id.StartsWith("settings-speed-"))==3
                && menu.Panel.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.text=="130%"));
            Check("Reopening Settings restores its navigation instructions", menu.Panel.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.text=="Right stick: select   A / trigger: confirm   B: close"));
            CaptureMenu(menu);
            var height = menu.Height; var rig = height.Rig;
            var locomotor = Object.FindAnyObjectByType<Oculus.Interaction.Locomotion.FirstPersonLocomotor>();
            locomotor.enabled = false; // isolate the distant height fixture from locomotor catch-up
            var tracking = rig.trackingSpace; Vector3 oldTracking = tracking.localPosition;
            tracking.localPosition += new Vector3(1000, 0, 1000); // empty space beyond the expanded neighborhood
            var rootScale = rig.transform.lossyScale;
            Vector3 headLocal = rig.centerEyeAnchor.localPosition, leftLocal = rig.leftHandAnchor.localPosition, rightLocal = rig.rightHandAnchor.localPosition;
            height.ResetHeight(); float before = height.CurrentHeight;
            Vector3 beforeHead = rig.centerEyeAnchor.position, beforeLeft = rig.leftHandAnchor.position, beforeRight = rig.rightHandAnchor.position;
            Click(menu, "plus");
            Check("Height plus increases configured height by 0.10 m", Mathf.Abs(height.CurrentHeight - before - .10f) < .001f);
            Check("Head and both hands receive the same translation without changing tracking poses or scale",
                Vector3.Distance(rig.centerEyeAnchor.position - beforeHead, rig.leftHandAnchor.position - beforeLeft) < .001f
                && Vector3.Distance(rig.centerEyeAnchor.position - beforeHead, rig.rightHandAnchor.position - beforeRight) < .001f
                && rig.centerEyeAnchor.localPosition == headLocal && rig.leftHandAnchor.localPosition == leftLocal && rig.rightHandAnchor.localPosition == rightLocal && rig.transform.lossyScale == rootScale);
            yield return null;
            Click(menu, "minus"); Check("Height minus decreases configured height by 0.10 m", Mathf.Abs(height.CurrentHeight - before) < .001f);
            yield return null;
            bool maximum = height.SetPreferredHeight(9); Check("Height clamps to 2.00 m", maximum && Mathf.Abs(height.CurrentHeight - 2) < .001f);
            bool minimum = height.SetPreferredHeight(-9); Check("Height clamps to 1.40 m", minimum && Mathf.Abs(height.CurrentHeight - 1.4f) < .001f);
            Check("Invalid height values are rejected", !height.SetPreferredHeight(float.NaN) && !height.SetPreferredHeight(float.PositiveInfinity));
            yield return null; Click(menu, "reset"); Check("Reset Height restores calibrated/default height and clears preference", Mathf.Abs(height.CurrentHeight - before) < .001f && !height.UsesPreferredHeight);
            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube); ceiling.transform.position = rig.centerEyeAnchor.position + Vector3.up * .22f; ceiling.transform.localScale = new Vector3(2, .05f, 2);
            Physics.SyncTransforms(); Check("Height increase is refused under a low ceiling", !height.SetPreferredHeight(2)); Destroy(ceiling);
            height.SetPreferredHeight(1.75f); tracking.localPosition = new Vector3(oldTracking.x, tracking.localPosition.y, oldTracking.z);
            locomotor.enabled = true;
            yield return null; menu.RequestRestart(); yield return null;
            Check("Restart shows the requested warning and keeps progress until confirmed", menu.Confirmation == "restart" && SceneManager.GetActiveScene().name == "CSI_Environment");
            Click(menu, "cancel"); Check("Restart Cancel returns to Settings without changing session", menu.IsOpen && menu.Confirmation == null);
            yield return null; menu.RequestLobbyReturn(); yield return null; Click(menu, "cancel");
            Check("Lobby Cancel leaves current scenario active", menu.IsOpen && menu.Confirmation == null && SceneManager.GetActiveScene().name == "CSI_Environment");
            yield return Press(right, "secondaryButton", true);
            Check("B closes Settings without lingering modal/tool ownership", !menu.IsOpen && !PlayerUIGate.IsBlocked);
            yield return Press(right, "secondaryButton", false);
            yield return null;
            Check("Locomotion remains enabled and transforms remain finite after closing", Object.FindAnyObjectByType<Oculus.Interaction.Locomotion.FirstPersonLocomotor>().isActiveAndEnabled
                && !float.IsNaN(rig.transform.position.y) && rig.transform.lossyScale == rootScale && !ToolInputLock.HasUIOwners);
            Check("Real tool equipment remains usable after changing height and closing Settings",
                HeldItemCoordinator.Instance.ToggleEquipTool(ToolType.Photographer, rig.rightHandAnchor));
            HeldItemCoordinator.Instance.HolsterCurrentTool();
            // Real fresh A selection on the panel, rather than a direct action call.
            menu.Show(); yield return null;
            var plus = menu.Pointer.Targets.First(t => t.id == "settings-plus");
            menu.ResponsiveLayout.EnsureVisible(plus.rect);
            menu.Pointer.RayOverride = () => new Ray(menu.Pointer.WorldCentre("settings-plus") - plus.rect.forward * .5f, plus.rect.forward);
            yield return null; float freshBefore = height.CurrentHeight;
            locomotor.enabled = false;
            tracking.localPosition += new Vector3(1000, 0, 1000);
            yield return Press(right, "primaryButton", true);
            Check("Fresh A selects height plus once and remains suppressed against jumping", Mathf.Abs(height.CurrentHeight - freshBefore - .10f) < .001f && PlayerUIGate.SuppressGameplayJump);
            yield return Press(right, "primaryButton", false);
            menu.Pointer.RayOverride = null; menu.Close();
            height.SetPreferredHeight(1.75f); tracking.localPosition = new Vector3(oldTracking.x, tracking.localPosition.y, oldTracking.z);
            locomotor.enabled = true;
            var knife = EvidenceStateManager.Instance.GetRecord("EVD-014"); knife.status = EvidenceStatus.Processed; knife.fingerprintingDone = true;
            EvidenceStateManager.Instance.GetRecord("BODY-A").bodyRemovalStatus = BodyRemovalStatus.Removed;
            var reason = ReasoningCheckpointManager.Instance; reason.Evaluate(); reason.RecordResponse("RC-01-PETER", "RC-01-PETER-C");
            string session = SessionLogger.Instance.SessionId;
            menu.Show(); yield return null; menu.RequestRestart(); yield return null; Click(menu, "confirm");
            while (SceneManager.GetActiveScene().name != "CSI_Environment" || SessionLogger.Instance == null || SessionLogger.Instance.SessionId == session) yield return null;
            yield return null; Isolate(); menu = Object.FindAnyObjectByType<SettingsPauseMenu>();
            Check("Confirmed restart fully reloads the scenario and creates a fresh session", SceneTurnover.IsPending && SessionLogger.Instance.SessionId != session && !menu.IsOpen);
            Check("Height preference persists through actual scenario restart", Mathf.Abs(menu.Height.CurrentHeight - 1.75f) < .001f && menu.Height.UsesPreferredHeight);
            Check("Walking speed preference persists through actual scenario restart", Mathf.Abs(menu.WalkingSpeed.CurrentMultiplier-1.3f)<.001f && menu.WalkingSpeed.UsesPreferredSpeed);
            CheckResetState("Restart");
            Check("Incomplete restart attempt is not added to upload queue", AutomatedTestSandbox.Queue.LoadIndex().entries.Count == 0);
            session = SessionLogger.Instance.SessionId;
            cameraObject = new GameObject("SettingsLobbyReturnCamera"); camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
            camera.transform.position = new Vector3(40, 1.7f, 40);
            typeof(HudFollowCamera).GetField("targetCamera", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(menu.Panel.GetComponent<HudFollowCamera>(), camera);
            menu.Show(); yield return null; menu.RequestLobbyReturn(); yield return null; Click(menu, "confirm");
            while (SceneManager.GetActiveScene().name != "Main") yield return null;
            yield return null;
            Check("Confirmed return uses the authored Main lobby route and releases all modal state", SceneManager.GetActiveScene().name == "Main" && !PlayerUIGate.IsBlocked && !ToolInputLock.HasUIOwners);
            Check("Height preference persists into Main without uploading an incomplete attempt", Object.FindAnyObjectByType<PlayerHeightOffset>().UsesPreferredHeight && AutomatedTestSandbox.Queue.LoadIndex().entries.Count == 0 && !StudentIdentificationHandoff.HasPending);
            Check("Walking speed preference persists into Main", Mathf.Abs(Object.FindAnyObjectByType<PlayerWalkingSpeed>().CurrentMultiplier-1.3f)<.001f);
            // Exercise the existing lobby's Start route, with its diagnostic fallback when identity UI is absent.
            var lobby = Object.FindAnyObjectByType<LobbyIdentificationController>();
            var panelField = typeof(LobbyIdentificationController).GetField("identificationPanel", BindingFlags.Instance | BindingFlags.NonPublic);
            panelField.SetValue(lobby, null); lobby.BeginIdentification();
            while (SceneManager.GetActiveScene().name != "CSI_Environment") yield return null;
            yield return null; Isolate();
            Check("Existing lobby Start route creates another fresh scenario session", SessionLogger.Instance.SessionId != session && SceneTurnover.IsPending);
            CheckResetState("Next lobby-started session");
            Check("Height preference survives starting another session", Mathf.Abs(Object.FindAnyObjectByType<PlayerHeightOffset>().CurrentHeight - 1.75f) < .001f);
            Check("Walking speed preference survives starting another session", Mathf.Abs(Object.FindAnyObjectByType<PlayerWalkingSpeed>().CurrentMultiplier-1.3f)<.001f);
        }
        finally
        {
            if (right != null) InputSystem.RemoveDevice(right); if (left != null) InputSystem.RemoveDevice(left);
            InputSystem.RemoveLayout("ViCSISettingsController");
            if (hadPreference) PlayerPrefs.SetFloat(PlayerHeightOffset.PreferenceKey, oldPreference); else PlayerPrefs.DeleteKey(PlayerHeightOffset.PreferenceKey);
            if (hadSpeedPreference) PlayerPrefs.SetFloat(PlayerWalkingSpeed.PreferenceKey, oldSpeedPreference); else PlayerPrefs.DeleteKey(PlayerWalkingSpeed.PreferenceKey);
            PlayerPrefs.Save();
        }
        report.AppendLine($"=== {(failures == 0 ? "PASS" : "FAIL")}: SettingsPauseMenuTest ==="); Report = report.ToString(); IsDone = true;
    }
    private void CheckResetState(string context)
    {
        Check(context + " restores IOC/observation, vendor, BODY-A, all evidence, reasoning and closeout", SceneTurnover.IOCObservations.Count == 0 && SceneTurnover.InvestigationSeconds == 0
            && !VendorFollowupInterview.Instance.FollowupUnlocked && !VendorFollowupInterview.Instance.IsDiscovered
            && EvidenceStateManager.Instance.GetRecord("BODY-A").bodyRemovalStatus == BodyRemovalStatus.NotPrepared
            && EvidenceStateManager.Instance.AllRecords.All(r => r.status == EvidenceStatus.NotFound && !r.fingerprintingDone && !r.swabbingDone)
            && ReasoningCheckpointManager.Instance.Records.Count == 0 && !EvidenceInventory.Instance.AllEntryIds.Any()
            && EndingManager.Instance.AcceptedEnding == null && !SOCOCloseout.Instance.InventoryConfirmed && !SOCOCloseout.Instance.FinalSurveyCompleted
            && !SOCOCloseout.Instance.IOCConfirmed && !SOCOCloseout.Instance.ReleaseCompleted);
    }
    private static void CaptureMenu(SettingsPauseMenu menu)
    {
#if UNITY_EDITOR
        var speed = menu.Pointer.Targets.First(t=>t.id=="settings-speed-plus");
        menu.ResponsiveLayout.EnsureVisible(speed.rect);
        Canvas.ForceUpdateCanvases();
        var probe = new GameObject("SettingsPreviewCamera"); var camera = probe.AddComponent<Camera>();
        camera.transform.position = menu.Panel.transform.position - menu.Panel.transform.forward * 1.1f;
        camera.transform.rotation = menu.Panel.transform.rotation;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f,.03f,.04f);
        camera.fieldOfView = 42;
        var target = new RenderTexture(900, 900, 24);
        var texture = new Texture2D(900, 900, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 900, 900), 0, 0); texture.Apply();
            System.IO.Directory.CreateDirectory("docs/evidence/walking-speed-settings-2026-10-21");
            System.IO.File.WriteAllBytes("docs/evidence/walking-speed-settings-2026-10-21/menu-preview.png", texture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous; camera.targetTexture = null; target.Release();
            Destroy(texture); Destroy(target); Destroy(probe);
        }
#endif
    }
}
