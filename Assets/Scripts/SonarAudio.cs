using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SubmarineSonarAudio : MonoBehaviour
{
    [Header("已有系统")]
    public SubmarineController controller;
    public SubmarineSonar sonar;

    [Header("提示音：留空使用临时声呐音")]
    public AudioClip pingClip;

    [Range(0f, 1f)]
    public float volume = 0.5f;

    [Header("临时提示音")]
    public float frequency = 900f;
    public float duration = 0.3f;

    private AudioSource source;
    private AudioClip generatedClip;

    private float scanTimer;

    private void Awake()
    {
        source = GetComponent<AudioSource>();

        source.playOnAwake = false;
        source.loop = false;

        // 离开驾驶台后仍能听到。
        source.spatialBlend = 0f;
    }

    private void Start()
    {
        if (controller == null || sonar == null)
        {
            Debug.LogError(
                "请连接声呐声音的 Controller 和 Sonar。",
                this
            );

            enabled = false;
            return;
        }

        if (pingClip == null)
            generatedClip = CreateTemporaryPing();
    }

    private void Update()
    {
        // 胜利或失败后，驾驶脚本会被停用。
        if (!controller.isActiveAndEnabled)
        {
            source.Stop();
            return;
        }

        if (!sonar.IsWorking)
        {
            source.Stop();
            return;
        }

        // 声呐断电时计时暂停，恢复后继续。
        scanTimer += Time.deltaTime;

        float period = Mathf.Max(0.2f, sonar.scanPeriod);

        if (scanTimer < period)
            return;

        scanTimer %= period;

        AudioClip clip = pingClip != null
            ? pingClip
            : generatedClip;

        if (clip != null)
            source.PlayOneShot(clip, volume);
    }

    private AudioClip CreateTemporaryPing()
    {
        const int sampleRate = 22050;

        float length = Mathf.Clamp(duration, 0.05f, 2f);
        float toneFrequency = Mathf.Clamp(
            frequency,
            100f,
            5000f
        );

        int sampleCount = Mathf.CeilToInt(
            sampleRate * length
        );

        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)sampleRate;
            float progress = time / length;

            // 短促起音，随后逐渐衰减。
            float attack = Mathf.Clamp01(time / 0.008f);
            float decay = Mathf.Exp(-progress * 6f);
            float endFade = Mathf.Clamp01(
                (length - time) / 0.015f
            );

            float tone = Mathf.Sin(
                2f * Mathf.PI * toneFrequency * time
            );

            samples[i] =
                tone * attack * decay * endFade * 0.8f;
        }

        AudioClip clip = AudioClip.Create(
            "Temporary Sonar Ping",
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