using UnityEngine;

[DefaultExecutionOrder(500)]
[RequireComponent(typeof(AudioSource))]
public class RepairCollisionAudio : MonoBehaviour
{
    [Header("Hull system / 船体系统")]
    public SubmarineHull hull;

    [Header("Optional impact sound / 可选碰撞音效")]
    public AudioClip impactClip;

    [Header("Volume / 音量")]
    [Range(0f, 1f)]
    public float volume = 0.65f;

    private AudioSource source;
    private AudioClip generatedClip;
    private int previousCollisionCount;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;

        // 使用全局声音，维修时也能听见。
        source.spatialBlend = 0f;

        if (impactClip == null)
            generatedClip = CreateImpactSound();
    }

    private void Start()
    {
        if (hull == null)
        {
            Debug.LogError("请绑定 Hull 船体组件。", this);
            enabled = false;
            return;
        }

        previousCollisionCount = hull.CollisionCount;
    }

    private void LateUpdate()
    {
        int currentCount = hull.CollisionCount;

        if (currentCount > previousCollisionCount)
        {
            AudioClip clip = impactClip != null
                ? impactClip
                : generatedClip;

            source.PlayOneShot(clip, volume);
        }

        previousCollisionCount = currentCount;
    }

    private AudioClip CreateImpactSound()
    {
        const int sampleRate = 22050;
        const float duration = 0.45f;

        int sampleCount = Mathf.RoundToInt(
            sampleRate * duration
        );

        float[] samples = new float[sampleCount];
        System.Random random = new System.Random(17);

        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)sampleRate;

            float attack = Mathf.Clamp01(time / 0.005f);
            float tail = Mathf.Clamp01(
                (duration - time) / 0.05f
            );

            float envelope =
                attack * tail * Mathf.Exp(-time * 12f);

            float rumble =
                Mathf.Sin(2f * Mathf.PI * 65f * time) * 0.55f
                + Mathf.Sin(2f * Mathf.PI * 113f * time) * 0.2f;

            float noise =
                ((float)random.NextDouble() * 2f - 1f) * 0.25f;

            samples[i] = (rumble + noise) * envelope;
        }

        AudioClip clip = AudioClip.Create(
            "Generated Hull Impact",
            sampleCount,
            1,
            sampleRate,
            false
        );

        clip.SetData(samples, 0);
        return clip;
    }

    private void OnDestroy()
    {
        if (generatedClip != null)
            Destroy(generatedClip);
    }
}