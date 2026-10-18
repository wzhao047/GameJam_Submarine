using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SubmarineEngineAudio : MonoBehaviour
{
    [Header("已有系统")]
    public SubmarineController controller;
    public SubmarinePower power;

    [Header("发动机音效：留空使用临时机械声")]
    public AudioClip engineClip;

    [Header("音量")]
    [Range(0f, 1f)]
    public float idleVolume = 0.06f;

    [Range(0f, 1f)]
    public float runningVolume = 0.22f;

    [Header("音调")]
    public float idlePitch = 0.7f;
    public float runningPitch = 1.3f;

    [Header("声音变化")]
    public float responseSpeed = 1f;

    [Tooltip("对应最高音量和音调的航速")]
    public float referenceSpeed = 5f;

    private AudioSource source;
    private AudioClip generatedClip;

    private void Awake()
    {
        source = GetComponent<AudioSource>();

        source.playOnAwake = false;
        source.loop = true;

        // 暂时使用二维声音，离台维修时仍然能听到。
        source.spatialBlend = 0f;

        source.volume = 0f;
        source.pitch = idlePitch;
    }

    private void Start()
    {
        if (controller == null || power == null)
        {
            Debug.LogError(
                "请连接发动机声音的 Controller 和 Power。",
                this
            );

            enabled = false;
            return;
        }

        if (engineClip == null)
            generatedClip = CreateTemporaryEngineSound();

        source.clip = engineClip != null
            ? engineClip
            : generatedClip;

        source.Play();
    }

    private void Update()
    {
        if (source == null || controller == null || power == null)
            return;

        bool running =
            controller.isActiveAndEnabled &&
            power.HasPower;

        float speedRatio = Mathf.Clamp01(
            controller.CurrentSpeed /
            Mathf.Max(0.1f, referenceSpeed)
        );

        float targetVolume = running
            ? Mathf.Lerp(idleVolume, runningVolume, speedRatio)
            : 0f;

        float targetPitch = Mathf.Lerp(
            idlePitch,
            runningPitch,
            speedRatio
        );

        float response =
            Mathf.Max(0.01f, responseSpeed) * Time.deltaTime;

        source.volume = Mathf.MoveTowards(
            source.volume,
            targetVolume,
            response
        );

        source.pitch = Mathf.MoveTowards(
            source.pitch,
            Mathf.Clamp(targetPitch, 0.1f, 3f),
            response
        );

        if (running && !source.isPlaying)
            source.Play();

        if (!running && source.volume <= 0.001f)
            source.Stop();
    }

    private AudioClip CreateTemporaryEngineSound()
    {
        const int sampleRate = 22050;
        const int durationSeconds = 1;

        int sampleCount = sampleRate * durationSeconds;
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)sampleRate;

            // 整数频率保证一秒循环首尾连续。
            float low =
                Mathf.Sin(2f * Mathf.PI * 40f * time);

            float middle =
                Mathf.Sin(2f * Mathf.PI * 80f * time) * 0.35f;

            float high =
                Mathf.Sin(2f * Mathf.PI * 120f * time) * 0.15f;

            float vibration =
                0.85f +
                Mathf.Sin(2f * Mathf.PI * 7f * time) * 0.15f;

            samples[i] =
                (low + middle + high) * vibration * 0.4f;
        }

        AudioClip clip = AudioClip.Create(
            "Temporary Engine Loop",
            sampleCount,
            1,
            sampleRate,
            false
        );

        clip.SetData(samples, 0);
        return clip;
    }

    private void OnDisable()
    {
        if (source != null)
            source.Stop();
    }

    private void OnDestroy()
    {
        if (generatedClip != null)
            Destroy(generatedClip);
    }
}