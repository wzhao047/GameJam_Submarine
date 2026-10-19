using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class RepairFaultAlarm : MonoBehaviour
{
    [Header("三种维修系统")]
    public LeakRepairInteraction leak;
    public CoolingRepairInteraction cooling;
    public FusePuzzleInteraction fuse;

    [Header("警报音效，可留空使用临时提示音")]
    public AudioClip alarmClip;
    [Range(0f, 1f)] public float initialAlertVolume = 0.4f;

    [Header("Continuous unresolved fault alarm")]
    public AudioClip persistentAlarmClip;
    [Tooltip("Extra quiet time after the initial alert finishes.")]
    [Min(0f)] public float persistentAlarmDelay = 1f;
    [Min(0.01f)] public float persistentAlarmFadeIn = 2f;
    [Range(0.25f, 1.5f)] public float persistentAlarmPitch = 1f;
    private AudioClip generatedInitialAlert;
    private bool previousLeakFault, previousCoolingFault, previousFuseFault;
    private float persistentWait;
    private float persistentFade;

    [Range(0f, 1f)]
    public float volume = 0.4f;

    [Header("Repair startup confirmation")]
    public AudioClip repairCompleteClip;
    [Range(0f, 1f)] public float repairCompleteVolume = 0.65f;
    [Header("Leak sealed confirmation")]
    public AudioClip leakCompleteClip;
    [Range(0f, 1f)] public float leakCompleteVolume = 0.65f;
    private AudioClip generatedLeakClip;
    private AudioClip generatedStartupClip;
    private bool previousLeakRepaired;
    private bool previousCoolingRepaired;
    private bool previousFuseRepaired;

    private AudioSource audioSource;
    private AudioClip generatedClip;

    private AudioSource persistentAlarmSource;
    private AudioSource previewSource;
    private float previewElapsed;
    private float previewVolume;
    private bool previewFadeIn;


    private void Start()
    {
        if (leak == null || cooling == null || fuse == null)
        {
            Debug.LogError("请绑定警报脚本的三种维修系统。", this);
            enabled = false;
            return;
        }

        previousLeakRepaired = leak.Repaired;
        previousCoolingRepaired = cooling.Repaired;
        previousFuseRepaired = fuse.Repaired;
        if (leakCompleteClip == null)
            generatedLeakClip = CreateLeakConfirmation();
        if (repairCompleteClip == null)
            generatedStartupClip = CreateStartupConfirmation();
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;

        // 使用二维声音，切换操作位置后仍然能听见。
        audioSource.spatialBlend = 0f;

        persistentAlarmSource = gameObject.AddComponent<AudioSource>();
        persistentAlarmSource.playOnAwake = false;
        persistentAlarmSource.loop = true;
        persistentAlarmSource.spatialBlend = 0f;
        generatedClip = CreatePersistentAlarm();
        if (alarmClip == null) generatedInitialAlert = CreateInitialAlert();
    }

    private void Update()
    {
        if (previewSource != null && previewSource.isPlaying && previewFadeIn)
        {
            previewElapsed += Time.unscaledDeltaTime;
            previewSource.volume = previewVolume * Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01(previewElapsed / Mathf.Max(0.01f, persistentAlarmFadeIn)));
        }
        bool leakCompleted = leak.Repaired && !previousLeakRepaired;
        bool repairCompleted =
            (cooling.Repaired && !previousCoolingRepaired) ||
            (fuse.Repaired && !previousFuseRepaired);
        previousLeakRepaired = leak.Repaired;
        previousCoolingRepaired = cooling.Repaired;
        previousFuseRepaired = fuse.Repaired;
        if (leakCompleted) PlayLeakConfirmation();
        if (repairCompleted) PlayRepairConfirmation();

        bool unresolved = leak.HasFault || cooling.HasFault || fuse.HasFault;
        bool running = !GameMenuController.IsInputBlocked && Time.timeScale > 0f &&
            (leak.isActiveAndEnabled || cooling.isActiveAndEnabled || fuse.isActiveAndEnabled);
        if (!running)
        {
            persistentAlarmSource.Stop();
            return;
        }
        bool newFault = (leak.HasFault && !previousLeakFault) ||
            (cooling.HasFault && !previousCoolingFault) ||
            (fuse.HasFault && !previousFuseFault);
        previousLeakFault = leak.HasFault;
        previousCoolingFault = cooling.HasFault;
        previousFuseFault = fuse.HasFault;
        if (!unresolved)
        {
            persistentAlarmSource.Stop();
            persistentWait = persistentFade = 0f;
            return;
        }
        if (newFault)
        {
            AudioClip alert = alarmClip != null ? alarmClip : generatedInitialAlert;
            if (alert != null) audioSource.PlayOneShot(alert, initialAlertVolume);
            persistentWait = (alert != null ? alert.length : 0f) + Mathf.Max(0f, persistentAlarmDelay);
            persistentFade = 0f;
            persistentAlarmSource.Stop();
        }
        if (persistentWait > 0f)
        {
            persistentWait = Mathf.Max(0f, persistentWait - Time.deltaTime);
            return;
        }
        persistentFade = Mathf.MoveTowards(persistentFade, 1f,
            Time.deltaTime / Mathf.Max(0.01f, persistentAlarmFadeIn));
        AudioClip clip = persistentAlarmClip != null ? persistentAlarmClip : generatedClip;
        if (persistentAlarmSource.clip != clip)
        {
            persistentAlarmSource.Stop();
            persistentAlarmSource.clip = clip;
        }
        persistentAlarmSource.pitch = persistentAlarmPitch;
        persistentAlarmSource.volume = volume * Mathf.SmoothStep(0f, 1f, persistentFade);
        if (clip != null && !persistentAlarmSource.isPlaying) persistentAlarmSource.Play();
    }

    // Inspector preview uses an independent voice and never changes fault state.
    public void PreviewAlarmSound(int sound)
    {
        if (!Application.isPlaying || !isActiveAndEnabled) return;
        if (previewSource == null)
        {
            previewSource = gameObject.AddComponent<AudioSource>();
            previewSource.playOnAwake = false;
            previewSource.spatialBlend = 0f;
            previewSource.ignoreListenerPause = true;
        }
        StopSoundPreview();
        AudioClip clip;
        previewFadeIn = sound == 1;
        previewSource.pitch = previewFadeIn ? persistentAlarmPitch : 1f;
        switch (sound)
        {
            case 0:
                if (alarmClip == null && generatedInitialAlert == null) generatedInitialAlert = CreateInitialAlert();
                clip = alarmClip != null ? alarmClip : generatedInitialAlert;
                previewVolume = initialAlertVolume;
                break;
            case 1:
                if (persistentAlarmClip == null && generatedClip == null) generatedClip = CreatePersistentAlarm();
                clip = persistentAlarmClip != null ? persistentAlarmClip : generatedClip;
                previewVolume = volume;
                break;
            case 2:
                if (repairCompleteClip == null && generatedStartupClip == null) generatedStartupClip = CreateStartupConfirmation();
                clip = repairCompleteClip != null ? repairCompleteClip : generatedStartupClip;
                previewVolume = repairCompleteVolume;
                break;
            default:
                if (leakCompleteClip == null && generatedLeakClip == null) generatedLeakClip = CreateLeakConfirmation();
                clip = leakCompleteClip != null ? leakCompleteClip : generatedLeakClip;
                previewVolume = leakCompleteVolume;
                break;
        }
        previewSource.clip = clip;
        previewSource.loop = false;
        previewSource.volume = previewFadeIn ? 0f : previewVolume;
        previewSource.Play();
    }

    public void StopSoundPreview()
    {
        if (previewSource != null) previewSource.Stop();
        previewElapsed = 0f;
    }
    private void OnDisable()
    {
        StopSoundPreview();
        if (persistentAlarmSource != null) persistentAlarmSource.Stop();
    }
    [ContextMenu("Preview Repair Startup Sound (Play Mode)")]
    public void PlayRepairConfirmation()
    {
        if (!Application.isPlaying || audioSource == null) return;
        AudioClip clip = repairCompleteClip != null ? repairCompleteClip : generatedStartupClip;
        if (clip != null) audioSource.PlayOneShot(clip, repairCompleteVolume);
    }

    [ContextMenu("Preview Leak Sealed Sound (Play Mode)")]
    public void PlayLeakConfirmation()
    {
        if (!Application.isPlaying || audioSource == null) return;
        AudioClip clip = leakCompleteClip != null ? leakCompleteClip : generatedLeakClip;
        if (clip != null) audioSource.PlayOneShot(clip, leakCompleteVolume);
    }

    // Brief pressure release followed by a damped mechanical latch.
    private AudioClip CreateLeakConfirmation()
    {
        const int sampleRate = 44100;
        const float duration = 0.42f;
        float[] samples = new float[Mathf.CeilToInt(sampleRate * duration)];
        uint noiseState = 71;
        float filteredNoise = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)sampleRate;
            noiseState = noiseState * 1664525u + 1013904223u;
            float noise = (noiseState & 65535u) / 32767.5f - 1f;
            filteredNoise = Mathf.Lerp(filteredNoise, noise, 0.2f);
            float hiss = filteredNoise * Mathf.Min(1f, t / 0.008f) * Mathf.Exp(-t * 14f) * 0.55f;
            float latchTime = t - 0.16f;
            float latch = 0f;
            if (latchTime >= 0f)
            {
                latch = (Mathf.Sin(2f * Mathf.PI * 180f * latchTime) +
                    0.35f * Mathf.Sin(2f * Mathf.PI * 430f * latchTime)) *
                    Mathf.Min(1f, latchTime / 0.003f) * Mathf.Exp(-latchTime * 30f) * 0.45f;
            }
            float fadeOut = Mathf.Clamp01((duration - t) / 0.04f);
            samples[i] = Mathf.Clamp((hiss + latch) * fadeOut, -0.9f, 0.9f);
        }
        AudioClip clip = AudioClip.Create("Leak Sealed Confirmation", samples.Length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
    // Relay click, accelerating motor whirr, then two rising ready tones.
    private AudioClip CreateStartupConfirmation()
    {
        const int sampleRate = 44100;
        const float duration = 0.72f;
        float[] samples = new float[Mathf.CeilToInt(sampleRate * duration)];
        float phase = 0f;
        uint noiseState = 17;
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)sampleRate;
            float signal = 0f;
            if (t < 0.035f)
            {
                noiseState = noiseState * 1664525u + 1013904223u;
                float noise = (noiseState & 65535u) / 32767.5f - 1f;
                signal += noise * Mathf.Exp(-t * 160f) * 0.35f;
            }
            if (t < 0.4f)
            {
                phase += 2f * Mathf.PI * Mathf.Lerp(90f, 310f, t / 0.4f) / sampleRate;
                float envelope = Mathf.Sin(Mathf.PI * t / 0.4f);
                signal += (Mathf.Sin(phase) + 0.25f * Mathf.Sin(phase * 2f)) * envelope * 0.2f;
            }
            float noteStart = t < 0.49f ? 0.3f : 0.49f;
            float noteLength = t < 0.49f ? 0.16f : 0.23f;
            float noteTime = t - noteStart;
            if (noteTime >= 0f && noteTime < noteLength)
            {
                float frequency = t < 0.49f ? 660f : 990f;
                float envelope = Mathf.Min(1f, noteTime / 0.008f) *
                    Mathf.Min(1f, (noteLength - noteTime) / 0.06f);
                signal += Mathf.Sin(2f * Mathf.PI * frequency * noteTime) * envelope * 0.32f;
            }
            samples[i] = Mathf.Clamp(signal, -0.9f, 0.9f);
        }
        AudioClip clip = AudioClip.Create("Repair Startup Confirmation", samples.Length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
    // Low alternating warning pulses, distinct from the old short 900 Hz alert.
    private AudioClip CreatePersistentAlarm()
    {
        const int sampleRate = 44100;
        const float duration = 5f;
        float[] samples = new float[Mathf.RoundToInt(sampleRate * duration)];
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)sampleRate;
            float pulseTime = t % 2.5f;
            if (pulseTime >= 1f) continue;
            float frequency = t < 2.5f ? 370f : 290f;
            float envelope = Mathf.Min(1f, pulseTime / 0.025f) *
                Mathf.Clamp01((1f - pulseTime) / 0.25f);
            samples[i] = (Mathf.Sin(2f * Mathf.PI * frequency * pulseTime) +
                0.2f * Mathf.Sin(4f * Mathf.PI * frequency * pulseTime)) * envelope * 0.38f;
        }
        AudioClip clip = AudioClip.Create("Persistent Fault Warning", samples.Length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
    // Original two short 900 Hz pulses, played once when a new fault appears.
    private AudioClip CreateInitialAlert()
    {
        const int sampleRate = 44100;
        float[] samples = new float[Mathf.CeilToInt(sampleRate * 0.36f)];
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)sampleRate;
            float local = t < 0.2f ? t : t - 0.2f;
            if (local < 0.12f)
                samples[i] = Mathf.Sin(2f * Mathf.PI * 900f * t) *
                    Mathf.Sin(Mathf.PI * local / 0.12f) * 0.5f;
        }
        AudioClip clip = AudioClip.Create("Initial Fault Alert", samples.Length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
    private void OnDestroy()
    {
        if (previewSource != null) Destroy(previewSource);
        if (generatedInitialAlert != null) Destroy(generatedInitialAlert);
        if (persistentAlarmSource != null) Destroy(persistentAlarmSource);
        if (generatedLeakClip != null)
            Destroy(generatedLeakClip);
        if (generatedStartupClip != null)
            Destroy(generatedStartupClip);
        if (generatedClip != null)
            Destroy(generatedClip);
    }
}