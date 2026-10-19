using UnityEngine;

public class LeakWaterFeedback : MonoBehaviour
{
    [Header("Â©Ë®Î¬ÐÞÂß¼­")]
    public LeakRepairInteraction repair;

    [Header("Â©Ë®Á£×Ó")]
    public ParticleSystem waterParticles;

    [Header("Leaking water audio")]
    public AudioClip leakLoopClip;
    [Range(0f, 1f)] public float leakVolume = 0.45f;
    [Min(0.01f)] public float leakFadeIn = 0.25f;
    [Min(0.01f)] public float leakFadeOut = 0.35f;
    private AudioSource leakSource;
    private float leakBlend;

    private void Awake()
    {
        if (repair == null) repair = GetComponent<LeakRepairInteraction>();
        leakSource = gameObject.AddComponent<AudioSource>();
        leakSource.playOnAwake = false;
        leakSource.loop = true;
        leakSource.spatialBlend = 0f;
        leakSource.volume = 0f;
    }

    private void UpdateLeakAudio()
    {
        bool running = repair != null && repair.isActiveAndEnabled &&
            !GameMenuController.IsInputBlocked && Time.timeScale > 0f;
        if (!running || leakLoopClip == null)
        {
            leakSource.Stop();
            leakBlend = 0f;
            return;
        }
        bool leaking = repair.IsLeaking;
        leakBlend = Mathf.MoveTowards(leakBlend, leaking ? 1f : 0f,
            Time.deltaTime / Mathf.Max(0.01f, leaking ? leakFadeIn : leakFadeOut));
        if (leakSource.clip != leakLoopClip)
        {
            leakSource.Stop();
            leakSource.clip = leakLoopClip;
        }
        leakSource.volume = leakVolume * Mathf.SmoothStep(0f, 1f, leakBlend);
        if (leakBlend > 0f && !leakSource.isPlaying) leakSource.Play();
        if (leakBlend <= 0f && leakSource.isPlaying) leakSource.Stop();
    }

    private void OnDestroy()
    {
        if (leakSource != null) Destroy(leakSource);
    }

    private void Update()
    {
        UpdateLeakAudio();
        if (waterParticles == null) return;

        bool shouldLeak =
            repair != null && repair.IsLeaking;

        if (shouldLeak)
        {
            if (!waterParticles.isPlaying)
                waterParticles.Play(true);
        }
        else if (waterParticles.isPlaying)
        {
            StopWater();
        }
    }

    private void StopWater()
    {
        if (waterParticles == null) return;

        waterParticles.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear
        );
    }

    private void OnDisable()
    {
        if (leakSource != null) leakSource.Stop();
        leakBlend = 0f;
        StopWater();
    }
}