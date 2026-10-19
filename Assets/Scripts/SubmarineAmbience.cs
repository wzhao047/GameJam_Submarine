using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SubmarineController))]
public sealed class SubmarineAmbience : MonoBehaviour
{
    [Header("Depth ambience clips")]
    public AudioClip shallowClip;
    public AudioClip deepClip;
    public AudioClip abyssClip;
    [Min(1f)] public float deepDepth = 200f;
    [Min(2f)] public float abyssDepth = 450f;
    [Range(0f, 1f)] public float ambienceVolume = 0.35f;
    [Min(0.05f)] public float loopCrossfade = 1.5f;
    [Min(0.05f)] public float depthBlendSeconds = 3f;
    [Range(10f, 22000f)] public float shallowCutoff = 3000f;
    [Range(10f, 22000f)] public float abyssCutoff = 650f;

    [Header("Unknown objects brushing past the hull")]
    public AudioClip[] passingClips;
    [Range(0f, 1f)] public float passingVolume = 0.5f;
    public Vector2 passingInterval = new Vector2(25f, 55f);
    [Range(0f, 1f)] public float passingStereoWidth = 0.8f;

    private SubmarineController controller;
    private SubmarineHull hull;
    private LoopLayer[] layers;
    private AudioSource passingSource;
    private float passingTimer;
    private float passingDirection;
    private float passingElapsed;
    private float passingLength;
    private int lastPassingIndex = -1;

    private void Awake()
    {
        controller = GetComponent<SubmarineController>();
        hull = GetComponent<SubmarineHull>();
        layers = new[] { new LoopLayer(gameObject), new LoopLayer(gameObject), new LoopLayer(gameObject) };
        passingSource = NewSource(gameObject);
        ResetPassingTimer();
    }

    private void Update()
    {
        bool running = controller.isActiveAndEnabled &&
            !GameMenuController.IsInputBlocked && Time.timeScale > 0f &&
            (hull == null || !hull.HasFailed);
        if (!running)
        {
            StopAudio();
            return;
        }

        float firstDepth = Mathf.Max(1f, deepDepth);
        float lastDepth = Mathf.Max(firstDepth + 1f, abyssDepth);
        float depth = controller.Depth;
        float shallowWeight = 1f - Mathf.Clamp01(depth / firstDepth);
        float abyssWeight = Mathf.Clamp01((depth - firstDepth) / (lastDepth - firstDepth));
        float deepWeight = 1f - shallowWeight - abyssWeight;
        float cutoff = Mathf.Lerp(shallowCutoff, abyssCutoff, Mathf.Clamp01(depth / lastDepth));
        layers[0].Update(shallowClip, shallowWeight * ambienceVolume, cutoff, this);
        layers[1].Update(deepClip, deepWeight * ambienceVolume, cutoff, this);
        layers[2].Update(abyssClip, abyssWeight * ambienceVolume, cutoff, this);

        if (hull != null && hull.IsTouchingReef)
        {
            passingSource.Stop();
            return;
        }
        if (passingSource.isPlaying)
        {
            passingElapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(passingElapsed / Mathf.Max(0.01f, passingLength));
            passingSource.panStereo = Mathf.Lerp(-passingDirection, passingDirection, progress) * passingStereoWidth;
            float envelope = Mathf.Min(Mathf.Clamp01(passingElapsed / 0.35f),
                Mathf.Clamp01((passingLength - passingElapsed) / 0.6f));
            passingSource.volume = passingVolume * envelope;
            return;
        }
        passingTimer -= Time.deltaTime;
        if (passingTimer <= 0f)
        {
            PlayUnknownPass();
            ResetPassingTimer();
        }
    }

    private void ResetPassingTimer()
    {
        float minimum = Mathf.Max(1f, Mathf.Min(passingInterval.x, passingInterval.y));
        passingTimer = Random.Range(minimum, Mathf.Max(minimum, Mathf.Max(passingInterval.x, passingInterval.y)));
    }

    [ContextMenu("Preview Unknown Hull Pass (Play Mode)")]
    public void PlayUnknownPass()
    {
        if (!Application.isPlaying || !isActiveAndEnabled || passingSource == null ||
            GameMenuController.IsInputBlocked || Time.timeScale <= 0f ||
            !controller.isActiveAndEnabled || (hull != null && (hull.HasFailed || hull.IsTouchingReef)) ||
            passingClips == null || passingClips.Length == 0) return;
        int index = Random.Range(0, passingClips.Length);
        if (passingClips.Length > 1 && index == lastPassingIndex)
            index = (index + 1) % passingClips.Length;
        AudioClip clip = passingClips[index];
        if (clip == null) return;
        lastPassingIndex = index;
        passingSource.clip = clip;
        passingSource.pitch = Random.Range(0.9f, 1.05f);
        passingDirection = Random.value < 0.5f ? -1f : 1f;
        passingSource.panStereo = -passingDirection * passingStereoWidth;
        passingSource.volume = 0f;
        passingElapsed = 0f;
        passingLength = clip.length / passingSource.pitch;
        passingSource.Play();
    }

    private static AudioSource NewSource(GameObject owner)
    {
        AudioSource source = owner.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.volume = 0f;
        return source;
    }

    private void StopAudio()
    {
        if (layers != null) foreach (LoopLayer layer in layers) layer.Stop();
        if (passingSource != null) passingSource.Stop();
    }

    private void OnDisable() => StopAudio();

    private void OnDestroy()
    {
        StopAudio();
        if (layers != null) foreach (LoopLayer layer in layers) layer.Dispose();
        if (passingSource != null) Destroy(passingSource);
    }

    // Each layer overlaps two scheduled voices to soften short recording seams.
    private sealed class LoopLayer
    {
        private AudioSource current, next;
        private AudioLowPassFilter currentFilter, nextFilter;
        private bool scheduled;
        private double start, nextStart;
        private float fade, volume;

        public LoopLayer(GameObject owner)
        {
            // A filter belongs to one voice, so sources use separate children.
            GameObject a = new GameObject("Ambience Voice");
            GameObject b = new GameObject("Ambience Voice");
            a.transform.SetParent(owner.transform, false);
            b.transform.SetParent(owner.transform, false);
            current = NewSource(a);
            next = NewSource(b);
            currentFilter = a.AddComponent<AudioLowPassFilter>();
            nextFilter = b.AddComponent<AudioLowPassFilter>();
        }

        public void Update(AudioClip clip, float targetVolume, float cutoff, SubmarineAmbience settings)
        {
            if (clip == null || clip.length <= 0f) { Stop(); return; }
            if (current.clip != clip) Stop();
            volume = Mathf.MoveTowards(volume, targetVolume, Time.deltaTime / Mathf.Max(0.05f, settings.depthBlendSeconds));
            currentFilter.cutoffFrequency = nextFilter.cutoffFrequency = cutoff;
            double now = AudioSettings.dspTime;
            if (!scheduled)
            {
                fade = Mathf.Clamp(settings.loopCrossfade, 0.01f, clip.length * 0.45f);
                current.clip = next.clip = clip;
                start = now + 0.03;
                nextStart = start + clip.length - fade;
                current.PlayScheduled(start);
                next.PlayScheduled(nextStart);
                scheduled = true;
            }
            if (now >= start + clip.length)
            {
                AudioSource old = current; current = next; next = old;
                AudioLowPassFilter oldFilter = currentFilter; currentFilter = nextFilter; nextFilter = oldFilter;
                start = nextStart;
                nextStart = start + clip.length - fade;
                if (nextStart <= now) { Stop(); return; }
                next.volume = 0f;
                next.PlayScheduled(nextStart);
            }
            current.volume = volume * Envelope(now - start, clip.length);
            next.volume = volume * Envelope(now - nextStart, clip.length);
        }

        private float Envelope(double time, float length)
        {
            if (time < 0 || time >= length) return 0f;
            return Mathf.SmoothStep(0f, 1f, Mathf.Min(Mathf.Clamp01((float)time / fade),
                Mathf.Clamp01((length - (float)time) / fade)));
        }

        public void Stop()
        {
            current.Stop(); next.Stop();
            current.volume = next.volume = volume = 0f;
            scheduled = false;
        }

        public void Dispose()
        {
            if (current != null) Object.Destroy(current.gameObject);
            if (next != null) Object.Destroy(next.gameObject);
        }
    }
}
