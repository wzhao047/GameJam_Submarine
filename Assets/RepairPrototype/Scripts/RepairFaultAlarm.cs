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

    [Range(0f, 1f)]
    public float volume = 0.4f;

    private AudioSource audioSource;
    private AudioClip generatedClip;

    private bool previousLeak;
    private bool previousCooling;
    private bool previousFuse;

    private void Start()
    {
        if (leak == null || cooling == null || fuse == null)
        {
            Debug.LogError("请绑定警报脚本的三种维修系统。", this);
            enabled = false;
            return;
        }

        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;

        // 使用二维声音，切换操作位置后仍然能听见。
        audioSource.spatialBlend = 0f;

        if (alarmClip == null)
            generatedClip = CreateTemporaryAlarm();
    }

    private void Update()
    {
        bool currentLeak = leak.HasFault;
        bool currentCooling = cooling.HasFault;
        bool currentFuse = fuse.HasFault;

        bool newFault =
            (currentLeak && !previousLeak) ||
            (currentCooling && !previousCooling) ||
            (currentFuse && !previousFuse);

        previousLeak = currentLeak;
        previousCooling = currentCooling;
        previousFuse = currentFuse;

        if (!newFault) return;

        AudioClip clip = alarmClip != null
            ? alarmClip
            : generatedClip;

        if (clip != null)
            audioSource.PlayOneShot(clip, volume);
    }

    private AudioClip CreateTemporaryAlarm()
    {
        const int sampleRate = 44100;
        const float duration = 0.36f;

        float[] samples = new float[
            Mathf.CeilToInt(sampleRate * duration)
        ];

        for (int i = 0; i < samples.Length; i++)
        {
            float time = (float)i / sampleRate;

            // 两次短促提示音，中间留空隙。
            float localTime = time < 0.2f
                ? time
                : time - 0.2f;

            if (localTime >= 0f && localTime < 0.12f)
            {
                float envelope = Mathf.Sin(
                    Mathf.PI * localTime / 0.12f
                );

                samples[i] =
                    Mathf.Sin(2f * Mathf.PI * 900f * time)
                    * envelope * 0.5f;
            }
        }

        AudioClip clip = AudioClip.Create(
            "Temporary Repair Alarm",
            samples.Length,
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