using UnityEngine;

/// <summary>Gentle local camera rotation layered over the existing station and impact controls.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(-100)]
public sealed class UnderwaterCameraSway : MonoBehaviour
{
    [Header("Underwater drift")]
    [Tooltip("Maximum pitch, yaw and roll in degrees. Roll creates the floating sensation.")]
    [SerializeField] private Vector3 rotationAmplitude = new Vector3(3f, 3f, 3f);
    [Tooltip("Noise travel speed. Lower values create slower, broader movements.")]
    [SerializeField, Min(0f)] private float driftSpeed = 0.16f;
    [Tooltip("Seconds to gently introduce the effect after enabling it.")]
    [SerializeField, Min(0f)] private float fadeInDuration = 2.5f;
    [SerializeField] private int noiseSeed = 731;

    private Quaternion baseRotation;
    private Quaternion appliedRotation;
    private bool hasOffset;
    private float elapsed;
    private float noiseOrigin;

    private void OnEnable()
    {
        elapsed = 0f;
        noiseOrigin = (noiseSeed & 65535) * 0.0137f;
        hasOffset = false;
    }

    private void Update()
    {
        // Remove our previous frame before station controls run. Never undo an external rotation.
        RestoreRotation();
    }

    private void LateUpdate()
    {
        elapsed += Time.deltaTime;
        float fade = fadeInDuration <= 0f ? 1f :
            Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / fadeInDuration));
        float phase = elapsed * Mathf.Max(0f, driftSpeed);
        Vector3 angles = new Vector3(
            Sample(phase, 11.3f),
            Sample(phase * 0.83f, 47.1f),
            Sample(phase * 0.67f, 89.7f));

        baseRotation = transform.localRotation;
        appliedRotation = baseRotation * Quaternion.Euler(Vector3.Scale(angles, rotationAmplitude) * fade);
        transform.localRotation = appliedRotation;
        hasOffset = true;
    }

    private float Sample(float phase, float channel)
    {
        // Two noise scales avoid a repetitive sine-wave rhythm; each axis follows a different path.
        float broad = Mathf.PerlinNoise(noiseOrigin + phase, channel) * 2f - 1f;
        float detail = Mathf.PerlinNoise(noiseOrigin + phase * 1.93f, channel + 123.4f) * 2f - 1f;
        return Mathf.Clamp(broad * 0.8f + detail * 0.2f, -1f, 1f);
    }

    private void RestoreRotation()
    {
        if (!hasOffset) return;
        if (Quaternion.Angle(transform.localRotation, appliedRotation) < 0.001f)
            transform.localRotation = baseRotation;
        hasOffset = false;
    }

    private void OnDisable()
    {
        RestoreRotation();
    }
}
