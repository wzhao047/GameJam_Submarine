using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SubmarineSonarAudio : MonoBehaviour
{
    [Header("����ϵͳ")]
    public SubmarineController controller;
    public SubmarineSonar sonar;

    [Header("��ʾ��������ʹ����ʱ������")]
    public AudioClip pingClip;

    [Range(0f, 1f)]
    public float volume = 0.5f;

    [Header("��ʱ��ʾ��")]
    public float frequency = 900f;
    public float duration = 0.3f;

    private AudioSource source;
    private AudioClip generatedClip;



    private void Awake()
    {
        source = GetComponent<AudioSource>();

        source.playOnAwake = false;
        source.loop = false;

        // �뿪��ʻ̨������������
        source.spatialBlend = 0f;
    }

    private void Start()
    {
        if (controller == null || sonar == null)
        {
            Debug.LogError(
                "���������������� Controller �� Sonar��",
                this
            );

            enabled = false;
            return;
        }

        if (pingClip == null)
            generatedClip = CreateTemporaryPing();

        sonar.ScanCompleted += PlayScanPing;
    }

    private void Update()
    {
        if (!controller.isActiveAndEnabled || !sonar.IsWorking || GameMenuController.IsInputBlocked)
            source.Stop();
    }

    private void PlayScanPing()
    {
        if (!isActiveAndEnabled || controller == null || !controller.isActiveAndEnabled
            || sonar == null || !sonar.IsWorking || GameMenuController.IsInputBlocked
            || GameMenuController.IsStartMenuVisible)
            return;

        AudioClip clip = pingClip != null ? pingClip : generatedClip;
        if (clip != null) source.PlayOneShot(clip, volume);
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

            // �̴������������˥����
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
        if (sonar != null) sonar.ScanCompleted -= PlayScanPing;
        if (generatedClip != null)
            Destroy(generatedClip);
    }
}