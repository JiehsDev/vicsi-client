// Assets/_Project/Scripts/Interaction/CameraShutterEffect.cs
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Blacks out the viewfinder for an instant when a photo is taken, like a
/// real camera's shutter blades snapping shut and back open. Builds its own
/// full-rect black Image at runtime against this GameObject's own
/// RectTransform, so it just needs to sit on the viewfinder canvas alongside
/// a PhotographTool reference - no other wiring. Separate from
/// PhotographTool's flashLight (a physical light for photographic flash);
/// this is a viewfinder-local visual only, and reusable against any future
/// aiming tool that exposes the same OnPhotoCaptured event.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class CameraShutterEffect : MonoBehaviour
{
    [SerializeField] private PhotographTool photographTool;
    [SerializeField] private float closeSeconds = 0.04f;
    [SerializeField] private float holdSeconds = 0.03f;
    [SerializeField] private float openSeconds = 0.1f;

    private Image shutterImage;
    private Coroutine shutterRoutine;

    private void Awake()
    {
        if (photographTool == null)
        {
            photographTool = GetComponentInParent<PhotographTool>();
        }

        var go = new GameObject("ShutterBlade", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        shutterImage = go.GetComponent<Image>();
        shutterImage.color = new Color(0f, 0f, 0f, 0f);
        shutterImage.raycastTarget = false;
    }

    private void OnEnable()
    {
        if (photographTool != null)
        {
            photographTool.OnPhotoCaptured += HandlePhotoCaptured;
        }
    }

    private void OnDisable()
    {
        if (photographTool != null)
        {
            photographTool.OnPhotoCaptured -= HandlePhotoCaptured;
        }

        if (shutterRoutine != null)
        {
            StopCoroutine(shutterRoutine);
            shutterRoutine = null;
        }
        if (shutterImage != null)
        {
            shutterImage.color = new Color(0f, 0f, 0f, 0f);
        }
    }

    private void HandlePhotoCaptured()
    {
        if (shutterRoutine != null)
        {
            StopCoroutine(shutterRoutine);
        }
        shutterRoutine = StartCoroutine(ShutterRoutine());
    }

    private IEnumerator ShutterRoutine()
    {
        yield return Fade(0f, 1f, closeSeconds);
        yield return new WaitForSeconds(holdSeconds);
        yield return Fade(1f, 0f, openSeconds);
        shutterRoutine = null;
    }

    private IEnumerator Fade(float from, float to, float seconds)
    {
        if (seconds <= 0f)
        {
            shutterImage.color = new Color(0f, 0f, 0f, to);
            yield break;
        }

        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(from, to, Mathf.Clamp01(t / seconds));
            shutterImage.color = new Color(0f, 0f, 0f, a);
            yield return null;
        }

        shutterImage.color = new Color(0f, 0f, 0f, to);
    }
}
