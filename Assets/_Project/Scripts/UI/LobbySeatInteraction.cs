using System.Collections;
using System.Linq;
using Oculus.Interaction.Locomotion;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Character = Oculus.Interaction.Locomotion.CharacterController;

/// <summary>Opt-in lobby seating using the rig's supported eye-level teleport, with live headset tracking.</summary>
public sealed class LobbySeatInteraction : MonoBehaviour
{
    [SerializeField] Transform seat;
    [SerializeField] float seatedEyeAboveCushion = .86f;
    [SerializeField] float interactionRadius = 1.75f;
    public bool IsSeated { get; private set; }
    public bool IsTransitioning { get; private set; }
    public Vector3 SeatedEyePosition => Seat.position + Vector3.up * seatedEyeAboveCushion;
    public bool CanToggle => isActiveAndEnabled && !IsTransitioning && Ready && MenuAllowsSeatInput
        && (IsSeated || InRange);
    Transform Seat => seat != null ? seat : transform;
    InputAction toggle;
    FirstPersonLocomotor locomotor;
    Character character;
    TMP_Text hint;
    GameObject hintRoot;
    LobbyCanvasPlacement menu;
    bool holdsMovement;
    float previousExitDistance;
    Vector3 returnFeet;
    Quaternion returnFacing;

    public void Configure(Transform anchor) => seat = anchor;
    bool Ready => SceneManager.GetActiveScene().name == "Main" && Camera.main != null
        && ResolveRig() && locomotor.isActiveAndEnabled;
    bool ResolveRig()
    {
        if (locomotor == null) locomotor = FindAnyObjectByType<FirstPersonLocomotor>();
        if (character == null) character = FindAnyObjectByType<Character>();
        return locomotor != null && character != null;
    }
    bool InRange
    {
        get
        {
            if (Camera.main == null) return false;
            var delta = Camera.main.transform.position - Seat.position; delta.y = 0;
            return delta.sqrMagnitude <= interactionRadius * interactionRadius;
        }
    }
    // CompetitiveMenuUI owns A/B and the menu gate. X is a separate lobby-only action.
    // Every other modal (settings, instructor management, etc.) blocks seating normally.
    bool MenuAllowsSeatInput => !PlayerUIGate.IsBlocked
        || PlayerUIGate.LockOwnerNames.All(name => name == "CompetitiveMenuUI");

    void Awake()
    {
        toggle = new InputAction("Lobby_SitStand", InputActionType.Button, "<XRController>{LeftHand}/primaryButton");
#if UNITY_EDITOR
        toggle.AddBinding("<Keyboard>/f");
#endif
    }
    void OnEnable() => toggle.Enable();
    void Start() => BuildHint();
    void Update()
    {
        if (CanToggle && toggle.WasPressedThisFrame())
        {
            if (IsSeated) Stand(); else Sit();
        }
        // Walking away physically leaves the seat; do not pull a tracked head back into it.
        if (IsSeated && !IsTransitioning && Camera.main != null)
        {
            var drift = Camera.main.transform.position - Seat.position; drift.y = 0;
            if (drift.magnitude > .8f) ReleaseSeat();
        }
    }
    void LateUpdate()
    {
        if (hintRoot == null) return;
        if (menu == null) menu = FindAnyObjectByType<LobbyCanvasPlacement>();
        bool show = menu != null && CanToggle;
        hintRoot.SetActive(show);
        if (!show) return;
        hint.text = IsSeated ? "[X] Stand up" : "[X] Sit in chair";
        hintRoot.transform.SetPositionAndRotation(menu.transform.position + Vector3.down * .59f
            - menu.transform.forward * .025f, menu.transform.rotation);
    }
    public bool Sit()
    {
        if (IsSeated || !CanToggle) return false;
        var head = Camera.main.transform;
        returnFeet = new Vector3(head.position.x, character.Pose.position.y - character.Height * .5f, head.position.z);
        var offset = head.position - Seat.position; offset.y = 0;
        if (offset.magnitude < .55f) returnFeet -= Seat.right * .85f;
        returnFacing = FlatFacing(head.forward);
        previousExitDistance = locomotor.ExitHotspotDistance;
        locomotor.ExitHotspotDistance = .85f;
        LocomotionSuspender.Suspend(); holdsMovement = true;
        IsSeated = true;
        Move(SeatedEyePosition, FlatFacing(Seat.forward), LocomotionEvent.TranslationType.AbsoluteEyeLevel);
        StartCoroutine(Settle());
        return true;
    }
    public bool Stand()
    {
        if (!IsSeated || !CanToggle) return false;
        Move(returnFeet, returnFacing, LocomotionEvent.TranslationType.Absolute);
        ReleaseSeat();
        StartCoroutine(Settle());
        return true;
    }
    void Move(Vector3 position, Quaternion facing, LocomotionEvent.TranslationType translation)
    {
        // Relative yaw compensates the current tracked head direction, not only the rig's origin.
        Quaternion yaw = facing * Quaternion.Inverse(FlatFacing(Camera.main.transform.forward));
        locomotor.HandleLocomotionEvent(new LocomotionEvent(-32010, new Pose(position, yaw),
            translation, LocomotionEvent.RotationType.Relative));
    }
    static Quaternion FlatFacing(Vector3 direction)
    {
        direction.y = 0;
        return Quaternion.LookRotation(direction.sqrMagnitude > .001f ? direction.normalized : Vector3.forward, Vector3.up);
    }
    IEnumerator Settle()
    {
        IsTransitioning = true;
        yield return null;
        yield return null;
        IsTransitioning = false;
    }
    void ReleaseSeat()
    {
        if (!IsSeated && !holdsMovement) return;
        IsSeated = false;
        if (locomotor != null)
        {
            locomotor.ExitHotspotDistance = previousExitDistance;
            locomotor.EnableMovement();
        }
        if (holdsMovement) { holdsMovement = false; LocomotionSuspender.Resume(); }
    }
    void BuildHint()
    {
        hintRoot = new GameObject("Lobby chair action", typeof(RectTransform), typeof(Canvas));
        SceneManager.MoveGameObjectToScene(hintRoot, gameObject.scene);
        hintRoot.transform.localScale = Vector3.one * .001f;
        var canvas = hintRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 90;
        var rect = (RectTransform)hintRoot.transform; rect.sizeDelta = new Vector2(470,64);
        var background = ForensicUIStyle.Rect(rect,"Chair action background",ForensicUIStyle.PanelBackground).rectTransform;
        background.anchorMin = Vector2.zero; background.anchorMax = Vector2.one; background.offsetMin = background.offsetMax = Vector2.zero;
        hint = ForensicUIStyle.Text(rect,"Sit or stand",29,ForensicUIStyle.Accent,FontStyles.Normal,TextAlignmentOptions.Center);
        hint.rectTransform.anchorMin = Vector2.zero; hint.rectTransform.anchorMax = Vector2.one;
        hint.rectTransform.offsetMin = hint.rectTransform.offsetMax = Vector2.zero;
    }
    void OnDisable()
    {
        toggle?.Disable(); StopAllCoroutines(); IsTransitioning = false; ReleaseSeat();
        if (hintRoot != null) hintRoot.SetActive(false);
    }
    void OnDestroy() { toggle?.Dispose(); if (hintRoot != null) Destroy(hintRoot); }
}
