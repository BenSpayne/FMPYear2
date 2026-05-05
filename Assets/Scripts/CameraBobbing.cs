using UnityEngine;
using System.Collections;

/// <summary>
/// Adds subtle randomized camera bobbing/micro-movements to simulate handheld camera feel.
/// Movements happen in pairs: either vertical (up/down) OR horizontal (left/right), never diagonal.
/// Attach to any GameObject (not necessarily the camera) and assign the target camera in the inspector.
/// </summary>
public class CameraBobbing : MonoBehaviour
{
    [Header("Target Camera")]
    [Tooltip("The camera to apply bobbing to. If left empty, uses the camera this script is attached to.")]
    [SerializeField] private Camera targetCamera;

    [Header("Bobbing Intensity")]
    [Tooltip("How far the camera can move vertically (up/down).")]
    [SerializeField] private float verticalBobbingAmount = 0.15f;
    [Tooltip("How far the camera can move horizontally (left/right).")]
    [SerializeField] private float horizontalBobbingAmount = 0.1f;

    [Header("Timing")]
    [Tooltip("Minimum seconds between bobbing movements.")]
    [SerializeField] private float minTimeBetweenBobs = 1.5f;
    [Tooltip("Maximum seconds between bobbing movements.")]
    [SerializeField] private float maxTimeBetweenBobs = 4f;
    [Tooltip("How long each bobbing movement takes to complete.")]
    [SerializeField] private float bobbingDuration = 0.8f;

    [Header("Movement Style")]
    [Tooltip("Types of bobbing movement patterns available.")]
    [SerializeField] private BobbingPattern[] availablePatterns = new BobbingPattern[]
    {
        BobbingPattern.SingleOvershoot,
        BobbingPattern.BackAndForth,
        BobbingPattern.SingleSmooth,
        BobbingPattern.DoubleBounce
    };

    [Tooltip("Chance that the camera stays still instead of bobbing (0 = always bob, 1 = never bob).")]
    [Range(0f, 1f)]
    [SerializeField] private float skipChance = 0.1f;

    [Header("Return Style")]
    [Tooltip("How the camera returns to its original position after bobbing.")]
    [SerializeField] private ReturnStyle returnStyle = ReturnStyle.Smooth;

    [Header("Enable/Disable")]
    [SerializeField] private bool enableBobbing = true;

    // ── Nested Types ──────────────────────────────────────────────────────────

    public enum BobbingPattern
    {
        /// <summary>Camera moves in one direction and smoothly returns.</summary>
        SingleSmooth,
        /// <summary>Camera moves in one direction, slightly overshoots returning, then settles.</summary>
        SingleOvershoot,
        /// <summary>Camera moves in one direction, then opposite, then returns to origin.</summary>
        BackAndForth,
        /// <summary>Camera does a quick double bounce in the same axis.</summary>
        DoubleBounce
    }

    public enum ReturnStyle
    {
        /// <summary>Smoothly lerps back to origin.</summary>
        Smooth,
        /// <summary>Snaps back instantly (abrupt, mechanical feel).</summary>
        Instant,
        /// <summary>Eases back with a slight wobble.</summary>
        Eased
    }

    // ── Private State ─────────────────────────────────────────────────────────

    private Vector3 originalPosition;
    private Coroutine bobbingCoroutine;
    private float nextBobTime;
    private Transform cameraTransform;

    // ─────────────────────────────────────────────────────────────────────────
    //  UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────────

    void Start()
    {
        InitializeCamera();
    }

    void OnEnable()
    {
        InitializeCamera();
    }

    void InitializeCamera()
    {
        // If no camera assigned, try to use the camera on this GameObject
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }

        // If still null, try to find main camera
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
        {
            Debug.LogError("[CameraBobbing] No camera assigned and no camera found on this GameObject. Disabling bobbing.", this);
            enableBobbing = false;
            return;
        }

        cameraTransform = targetCamera.transform;
        originalPosition = cameraTransform.position;
        ScheduleNextBob();
    }

    void Update()
    {
        if (!enableBobbing || targetCamera == null) return;

        // Check if it's time for the next bob
        if (Time.time >= nextBobTime && bobbingCoroutine == null)
        {
            bobbingCoroutine = StartCoroutine(PerformBobRoutine());
        }
    }

    void OnDisable()
    {
        if (bobbingCoroutine != null)
        {
            StopCoroutine(bobbingCoroutine);
            bobbingCoroutine = null;
        }
        // Return to original position
        if (targetCamera != null)
        {
            targetCamera.transform.position = originalPosition;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  SCHEDULING
    // ─────────────────────────────────────────────────────────────────────────

    void ScheduleNextBob()
    {
        nextBobTime = Time.time + Random.Range(minTimeBetweenBobs, maxTimeBetweenBobs);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  MAIN BOBBING ROUTINE
    // ─────────────────────────────────────────────────────────────────────────

    IEnumerator PerformBobRoutine()
    {
        // Random chance to skip this bob entirely (for natural feel)
        if (Random.value < skipChance)
        {
            ScheduleNextBob();
            bobbingCoroutine = null;
            yield break;
        }

        // Update original position in case the camera has moved (scrolling etc)
        originalPosition = cameraTransform.position;

        // Randomly choose axis: vertical (up/down) or horizontal (left/right)
        bool isVertical = Random.value < 0.5f;

        // Randomly choose a pattern from available ones
        if (availablePatterns == null || availablePatterns.Length == 0)
        {
            ScheduleNextBob();
            bobbingCoroutine = null;
            yield break;
        }

        BobbingPattern pattern = availablePatterns[Random.Range(0, availablePatterns.Length)];

        // Execute the chosen pattern on the chosen axis
        switch (pattern)
        {
            case BobbingPattern.SingleSmooth:
                yield return StartCoroutine(SingleSmooth(isVertical));
                break;
            case BobbingPattern.SingleOvershoot:
                yield return StartCoroutine(SingleOvershoot(isVertical));
                break;
            case BobbingPattern.BackAndForth:
                yield return StartCoroutine(BackAndForth(isVertical));
                break;
            case BobbingPattern.DoubleBounce:
                yield return StartCoroutine(DoubleBounce(isVertical));
                break;
        }

        // Ensure we end exactly at original position
        cameraTransform.position = originalPosition;

        // Schedule next bob
        ScheduleNextBob();
        bobbingCoroutine = null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  BOBBING PATTERNS
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Moves in one direction, then smoothly returns to origin.
    /// </summary>
    IEnumerator SingleSmooth(bool isVertical)
    {
        float amount = GetBobbingAmount(isVertical);
        float direction = Random.value < 0.5f ? 1f : -1f;
        Vector3 offset = GetOffsetVector(isVertical, amount * direction);

        float halfDuration = bobbingDuration * 0.5f;
        yield return StartCoroutine(MoveToPosition(originalPosition + offset, halfDuration, EaseOutQuad));
        yield return StartCoroutine(MoveToPosition(originalPosition, halfDuration, EaseInOutQuad));
    }

    /// <summary>
    /// Moves in one direction, overshoots returning, then settles at origin.
    /// </summary>
    IEnumerator SingleOvershoot(bool isVertical)
    {
        float amount = GetBobbingAmount(isVertical);
        float direction = Random.value < 0.5f ? 1f : -1f;
        Vector3 offset = GetOffsetVector(isVertical, amount * direction);
        Vector3 overshootOffset = GetOffsetVector(isVertical, amount * -0.3f * direction);

        float thirdDuration = bobbingDuration / 3f;

        yield return StartCoroutine(MoveToPosition(originalPosition + offset, thirdDuration, EaseOutQuad));
        yield return StartCoroutine(MoveToPosition(originalPosition + overshootOffset, thirdDuration, EaseInOutQuad));
        yield return StartCoroutine(MoveToPosition(originalPosition, thirdDuration, EaseInQuad));
    }

    /// <summary>
    /// Moves in one direction, then opposite direction, then back to origin.
    /// </summary>
    IEnumerator BackAndForth(bool isVertical)
    {
        float amount = GetBobbingAmount(isVertical);
        float direction = Random.value < 0.5f ? 1f : -1f;
        Vector3 offset1 = GetOffsetVector(isVertical, amount * direction);
        Vector3 offset2 = GetOffsetVector(isVertical, amount * -direction * 0.7f);

        float thirdDuration = bobbingDuration / 3f;

        yield return StartCoroutine(MoveToPosition(originalPosition + offset1, thirdDuration, EaseOutQuad));
        yield return StartCoroutine(MoveToPosition(originalPosition + offset2, thirdDuration, EaseInOutQuad));
        yield return StartCoroutine(MoveToPosition(originalPosition, thirdDuration, returnStyle == ReturnStyle.Eased ? EaseOutBack : EaseInQuad));
    }

    /// <summary>
    /// Quick double bounce in the same axis.
    /// </summary>
    IEnumerator DoubleBounce(bool isVertical)
    {
        float amount = GetBobbingAmount(isVertical);
        float direction = Random.value < 0.5f ? 1f : -1f;
        Vector3 offset1 = GetOffsetVector(isVertical, amount * direction);
        Vector3 offset2 = GetOffsetVector(isVertical, amount * -direction * 0.8f);
        Vector3 offset3 = GetOffsetVector(isVertical, amount * direction * 0.4f);

        float quarterDuration = bobbingDuration / 4f;

        yield return StartCoroutine(MoveToPosition(originalPosition + offset1, quarterDuration, EaseOutQuad));
        yield return StartCoroutine(MoveToPosition(originalPosition + offset2, quarterDuration, EaseInOutQuad));
        yield return StartCoroutine(MoveToPosition(originalPosition + offset3, quarterDuration, EaseOutQuad));
        yield return StartCoroutine(MoveToPosition(originalPosition, quarterDuration, EaseInQuad));
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────────────────────────────────────

    float GetBobbingAmount(bool isVertical)
    {
        return isVertical ? verticalBobbingAmount : horizontalBobbingAmount;
    }

    Vector3 GetOffsetVector(bool isVertical, float amount)
    {
        return isVertical ? new Vector3(0, amount, 0) : new Vector3(amount, 0, 0);
    }

    IEnumerator MoveToPosition(Vector3 target, float duration, System.Func<float, float> easingFunction)
    {
        if (duration <= 0f)
        {
            cameraTransform.position = target;
            yield break;
        }

        Vector3 startPos = cameraTransform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = easingFunction(t);

            switch (returnStyle)
            {
                case ReturnStyle.Instant:
                    cameraTransform.position = target;
                    yield break;
                case ReturnStyle.Smooth:
                case ReturnStyle.Eased:
                default:
                    cameraTransform.position = Vector3.Lerp(startPos, target, easedT);
                    break;
            }

            yield return null;
        }

        cameraTransform.position = target;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  EASING FUNCTIONS
    // ─────────────────────────────────────────────────────────────────────────

    float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
    float EaseInQuad(float t) => t * t;
    float EaseInOutQuad(float t) => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

    float EaseOutBack(float t)
    {
        float c1 = 1.70158f;
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  PUBLIC API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Assign a new camera to bob at runtime.
    /// </summary>
    public void SetTargetCamera(Camera cam)
    {
        targetCamera = cam;
        InitializeCamera();
    }

    public void SetBobbingEnabled(bool enabled)
    {
        enableBobbing = enabled;
        if (!enabled)
        {
            if (bobbingCoroutine != null)
            {
                StopCoroutine(bobbingCoroutine);
                bobbingCoroutine = null;
            }
            if (targetCamera != null)
            {
                targetCamera.transform.position = originalPosition;
            }
        }
        else
        {
            if (targetCamera != null)
            {
                originalPosition = targetCamera.transform.position;
            }
            ScheduleNextBob();
        }
    }

    /// <summary>
    /// Updates the origin position. Call this if the camera moves externally (e.g. scrolling).
    /// </summary>
    public void UpdateOriginalPosition()
    {
        if (targetCamera != null)
        {
            originalPosition = targetCamera.transform.position;
        }
    }

    public void SetBobbingAmounts(float vertical, float horizontal)
    {
        verticalBobbingAmount = vertical;
        horizontalBobbingAmount = horizontal;
    }

    public void SetTiming(float minTime, float maxTime, float duration)
    {
        minTimeBetweenBobs = minTime;
        maxTimeBetweenBobs = maxTime;
        bobbingDuration = duration;
    }
}