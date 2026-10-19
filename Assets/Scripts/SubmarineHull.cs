using UnityEngine;

using TMPro;



[DefaultExecutionOrder(50)]
public class SubmarineHull : MonoBehaviour

{

    [Header("Hull display / 船体状态文字")]

    public TMP_Text hullStatus;



    [Header("Camera shake / 镜头震动")]

    public Transform shakeCamera;

    public float shakeDuration = 0.45f;

    public float shakeStrength = 0.035f;

    public float shakeFrequency = 28f;



    [Header("Collision / 碰撞")]

    public float submarineRadius = 3f;



    [Header("Cabin light / 船舱氛围灯")]

    public Light cabinDamageLight;



    [Tooltip("第一次碰撞后的闪烁频率")]

    public float flickerFrequency = 3f;



    [Tooltip("第二次碰撞后，红灯相对原灯的亮度")]

    public float redIntensityMultiplier = 1.5f;



    [Header("Continuous reef scraping")]
    [Min(0f)] public float reefShakeStrength = 0.16f;
    [Min(0.1f)] public float reefShakeFrequency = 42f;
    [Tooltip("Seconds for the camera to ease back after leaving a reef.")]
    [Min(0.01f)] public float reefCameraReturnDuration = 0.8f;
    private Vector3 reefLastShakeOffset;
    private float reefReturnElapsed;
    private float reefShakePhase;
    public AudioClip reefScrapeClip;
    [Range(0f, 1f)] public float reefScrapeVolume = 0.5f;
    [Tooltip("Seconds of overlap between repeated scrape clips.")]
    [Min(0.05f)] public float reefScrapeCrossfade = 0.6f;
    [Tooltip("Random duration range for each scrape segment, in seconds.")]
    public Vector2 reefScrapeDuration = new Vector2(1.2f, 3.8f);
    public Vector2 reefScrapePitch = new Vector2(0.88f, 1.08f);
    [Min(0.01f)] public float reefScrapeFadeIn = 0.3f;
    [Min(0.01f)] public float reefScrapeFadeOut = 0.7f;
    [Tooltip("Play the entire recording each pass; ignores Duration and random start position.")]
    public bool reefScrapePlayFullClip = false;
    [Tooltip("Choose a random start position for each segment. Off starts from the beginning.")]
    public bool reefScrapeRandomStart = true;
    [Tooltip("Randomize each segment's loudness. Off uses the configured Volume unchanged.")]
    public bool reefScrapeRandomVolume = true;
    [Tooltip("Minimum and maximum volume multipliers when Random Volume is enabled.")]
    public Vector2 reefScrapeVolumeRange = new Vector2(0.65f, 1f);
    private AudioSource reefScrapeNextSource;
    private AudioSource reefScrapeSource;
    private ScrapeSegment reefCurrentSegment;
    private ScrapeSegment reefNextSegment;
    private bool reefLoopScheduled;
    private float reefContactVolume;

    private struct ScrapeSegment
    {
        public double start, end;
        public float fadeIn, fadeOut, gain;
    }
    private AudioClip generatedReefScrape;
    private bool touchingReef;
    public bool IsTouchingReef => touchingReef;
    private SubmarineController controller;

    [Header("Ocean system / 海域系统")]
    public RepairOceanDirector ocean;

    private SubmarinePower power;

    private SubmarineOxygen oxygen;



    private int collisionCount;
    private float safeSeconds;
    private float damageProtectionUntil;
    private bool touchingHazard;
    public int TotalCollisions { get; private set; }
    public bool IsScrapingReef => touchingReef;
    public float LastDamageTime { get; private set; } = -100f;

    private bool failed;


    private Vector3 previousPosition;



    private Color normalLightColor;

    private float normalLightIntensity;

    private bool normalLightEnabled;



    private float shakeRemaining;

    private float shakeElapsed;

    private Vector3 cameraBasePosition;

    private Vector3 lastAppliedPosition;

    private bool hasCameraOffset;



    public bool HasFailed => failed;

    public int CollisionCount => collisionCount;



    public float HullPercentage =>

        Mathf.Clamp01((3f - collisionCount) / 3f) * 100f;



    private void Awake()

    {

        controller = GetComponent<SubmarineController>();


        power = GetComponent<SubmarinePower>();

        oxygen = GetComponent<SubmarineOxygen>();



        if (shakeCamera == null && Camera.main != null)

            shakeCamera = Camera.main.transform;



        if (cabinDamageLight != null)

        {

            normalLightColor = cabinDamageLight.color;

            normalLightIntensity = cabinDamageLight.intensity;

            normalLightEnabled = cabinDamageLight.enabled;

        }

    }



    private void Start()

    {

        if (controller == null || ocean == null)

        {

            Debug.LogError(
                "SubmarineHull：请绑定 Ocean，并挂在 Submarine 上。",
                this
            );

            enabled = false;

            return;

        }



        reefScrapeSource = gameObject.AddComponent<AudioSource>();
        reefScrapeSource.playOnAwake = false;
        reefScrapeSource.loop = false;
        reefScrapeSource.spatialBlend = 0f;
        reefScrapeNextSource = gameObject.AddComponent<AudioSource>();
        reefScrapeNextSource.playOnAwake = false;
        reefScrapeNextSource.loop = false;
        reefScrapeNextSource.spatialBlend = 0f;
        if (reefScrapeClip == null) generatedReefScrape = CreateReefScrape();
        previousPosition = GetPosition();

        RefreshStatus();

    }



    private void LateUpdate()

    {

        RestoreCameraOffset();



        if (!failed && controller.isActiveAndEnabled)

            CheckCollision();



        UpdateReefContact();
        UpdateRecovery();
        UpdateCameraShake();

        UpdateCabinLight();

        RefreshStatus();

    }



    private Vector3 GetPosition()

    {

        Vector2 position = controller.NavigationPosition;



        return new Vector3(

            position.x,

            controller.Depth,

            position.y

        );

    }



    private void CheckCollision()
    {
        touchingHazard = false;
        Vector3 currentPosition = GetPosition();

        if (ocean.isActiveAndEnabled && ocean.Ready)
        {
            CheckPool(ocean.Rocks, currentPosition);
            CheckPool(ocean.Fish, currentPosition);
            CheckPool(ocean.Reefs, currentPosition);
            CheckPool(ocean.OxygenCanisters, currentPosition);
        }

        previousPosition = currentPosition;
    }

    private void CheckPool(
        RepairOceanContact[] pool,
        Vector3 currentPosition)
    {
        foreach (RepairOceanContact item in pool)
        {
            if (failed) return;

            if (item == null || !item.isActiveAndEnabled ||
                !item.Active)
                continue;

            bool touched;

            if (item.kind == RepairContactKind.Reef)
            {
                touched = CrossesReef(
                    previousPosition, currentPosition, item);
            }
            else
            {
                Vector3 previousTarget =
                    item.position - item.velocity * Time.deltaTime;

                Vector3 start = previousPosition - previousTarget;
                Vector3 end = currentPosition - item.position;
                // Sonar rewards use the displayed X/Z plane: hidden depth must
                // not prevent collection when the marker reaches the submarine.
                if (item.kind == RepairContactKind.OxygenCanister || item.kind == RepairContactKind.Battery)
                {
                    start.y = 0f;
                    end.y = 0f;
                }
                Vector3 movement = end - start;

                float t = movement.sqrMagnitude > 0.000001f
                    ? Mathf.Clamp01(-Vector3.Dot(start, movement)
                        / movement.sqrMagnitude)
                    : 0f;

                float radius = Mathf.Max(0f, submarineRadius)
                    + Mathf.Max(0f, item.radius);

                touched = (start + movement * t).sqrMagnitude
                    <= radius * radius;
            }

            if (!touched) { item.ResetHitAfterSeparation(); continue; }
            if (item.kind == RepairContactKind.Rock || item.kind == RepairContactKind.Reef) touchingHazard = true;
            if (!item.CanDamage) continue;
            if ((item.kind == RepairContactKind.OxygenCanister || item.kind == RepairContactKind.Battery) &&
                (GameMenuController.IsInputBlocked || Time.timeScale <= 0f ||
                 oxygen == null || !oxygen.isActiveAndEnabled || oxygen.HasFailed))
                continue;

            // 每次生成的同一个目标只触发一次反馈。
            item.RegisterHit();

            if (item.kind == RepairContactKind.OxygenCanister || item.kind == RepairContactKind.Battery)
            {
                if (item.kind == RepairContactKind.Battery)
                {
                    if (power != null) power.AddBattery(25f);
                    VoyageModeSession.Current?.CollectedBattery();
                }
                else if (oxygen != null) oxygen.AddOxygen(ocean.oxygenRestoreSeconds);
                item.Deactivate();
            }
            else if (item.kind == RepairContactKind.Fish)
            {
                // 鱼群只震动，不损伤船体，也不停止潜艇。
                shakeRemaining = Mathf.Max(0f, shakeDuration);
                shakeElapsed = 0f;
            }
            else
            {
                TakeCollisionDamage();
            }
        }
    }

    private void UpdateReefContact()
    {
        touchingReef = false;
        if (!failed && controller != null && controller.isActiveAndEnabled &&
            !GameMenuController.IsInputBlocked && Time.timeScale > 0f &&
            ocean != null && ocean.isActiveAndEnabled && ocean.Ready)
        {
            Vector3 position = GetPosition();
            foreach (RepairOceanContact reef in ocean.Reefs)
            {
                // Contact persists after the one-time collision damage was registered.
                if (reef != null && reef.isActiveAndEnabled && reef.Active &&
                    CrossesReef(position, position, reef))
                {
                    touchingReef = true;
                    break;
                }
            }
        }
        if (reefScrapeSource == null) return;
        if (failed || controller == null || !controller.isActiveAndEnabled ||
            GameMenuController.IsInputBlocked || Time.timeScale <= 0f)
        {
            StopReefScrape();
            return;
        }
        float fadeTime = touchingReef ? reefScrapeFadeIn : reefScrapeFadeOut;
        reefContactVolume = Mathf.MoveTowards(reefContactVolume, touchingReef ? 1f : 0f,
            Time.deltaTime / Mathf.Max(0.01f, fadeTime));
        if (!touchingReef && reefContactVolume <= 0f)
        {
            StopReefScrape();
            return;
        }
        AudioClip clip = reefScrapeClip != null ? reefScrapeClip : generatedReefScrape;
        UpdateReefScrapeLoop(clip);
    }

    private void StopReefScrape()
    {
        if (reefScrapeSource != null) reefScrapeSource.Stop();
        if (reefScrapeNextSource != null) reefScrapeNextSource.Stop();
        reefLoopScheduled = false;
        reefContactVolume = 0f;
    }

    private ScrapeSegment ScheduleScrape(AudioSource source, AudioClip clip, double start, float fadeIn)
    {
        float minimumPitch = Mathf.Clamp(Mathf.Min(reefScrapePitch.x, reefScrapePitch.y), 0.5f, 2f);
        source.pitch = Random.Range(minimumPitch,
            Mathf.Clamp(Mathf.Max(reefScrapePitch.x, reefScrapePitch.y), minimumPitch, 2f));
        float available = clip.length / source.pitch;
        float minimumDuration = Mathf.Clamp(Mathf.Min(reefScrapeDuration.x, reefScrapeDuration.y), 0.01f, available);
        float maximumDuration = Mathf.Clamp(Mathf.Max(reefScrapeDuration.x, reefScrapeDuration.y), minimumDuration, available);
        float duration = reefScrapePlayFullClip ? available : Random.Range(minimumDuration, maximumDuration);
        source.clip = clip;
        source.volume = 0f;
        // Vary the portion of the recording as well as pitch and duration.
        source.time = reefScrapeRandomStart && !reefScrapePlayFullClip
            ? Random.Range(0f, Mathf.Max(0f, clip.length - duration * source.pitch))
            : 0f;
        source.PlayScheduled(start);
        source.SetScheduledEndTime(start + duration);
        return new ScrapeSegment
        {
            start = start,
            end = start + duration,
            fadeIn = Mathf.Min(fadeIn, duration * 0.45f),
            fadeOut = Mathf.Clamp(reefScrapeCrossfade, 0.01f, duration * 0.4f),
            gain = reefScrapeRandomVolume
                ? Random.Range(Mathf.Clamp01(Mathf.Min(reefScrapeVolumeRange.x, reefScrapeVolumeRange.y)),
                    Mathf.Clamp01(Mathf.Max(reefScrapeVolumeRange.x, reefScrapeVolumeRange.y)))
                : 1f
        };
    }

    private void UpdateReefScrapeLoop(AudioClip clip)
    {
        if (clip == null || clip.length <= 0f) { StopReefScrape(); return; }
        if (reefScrapeSource.clip != clip)
        {
            float contactVolume = reefContactVolume;
            StopReefScrape();
            reefContactVolume = contactVolume;
        }
        double now = AudioSettings.dspTime;
        if (!reefLoopScheduled)
        {
            reefCurrentSegment = ScheduleScrape(reefScrapeSource, clip, now + 0.03, reefScrapeFadeIn);
            QueueNextScrape(clip);
            reefLoopScheduled = true;
        }
        if (now >= reefCurrentSegment.end)
        {
            AudioSource finished = reefScrapeSource;
            reefScrapeSource = reefScrapeNextSource;
            reefScrapeNextSource = finished;
            reefCurrentSegment = reefNextSegment;
            if (now >= reefCurrentSegment.end - reefCurrentSegment.fadeOut)
            {
                // Recover after a long frame without piling up scheduled voices.
                StopReefScrape();
                return;
            }
            QueueNextScrape(clip);
        }
        float volume = reefScrapeVolume * reefContactVolume;
        reefScrapeSource.volume = volume * ScrapeEnvelope(now, reefCurrentSegment);
        reefScrapeNextSource.volume = volume * ScrapeEnvelope(now, reefNextSegment);
    }

    private void QueueNextScrape(AudioClip clip)
    {
        double requestedStart = reefCurrentSegment.end - reefCurrentSegment.fadeOut;
        reefNextSegment = ScheduleScrape(reefScrapeNextSource, clip,
            requestedStart, reefCurrentSegment.fadeOut);
        // Short segments may require a shorter shared overlap.
        float duration = (float)(reefNextSegment.end - reefNextSegment.start);
        reefCurrentSegment.fadeOut = reefNextSegment.fadeIn;
        reefNextSegment.start = reefCurrentSegment.end - reefCurrentSegment.fadeOut;
        reefNextSegment.end = reefNextSegment.start + duration;
        reefScrapeNextSource.SetScheduledStartTime(reefNextSegment.start);
        reefScrapeNextSource.SetScheduledEndTime(reefNextSegment.end);
    }
    private static float ScrapeEnvelope(double now, ScrapeSegment segment)
    {
        if (now < segment.start || now >= segment.end) return 0f;
        float fadeIn = Mathf.Clamp01((float)(now - segment.start) / Mathf.Max(0.001f, segment.fadeIn));
        float fadeOut = Mathf.Clamp01((float)(segment.end - now) / Mathf.Max(0.001f, segment.fadeOut));
        return segment.gain * Mathf.SmoothStep(0f, 1f, Mathf.Min(fadeIn, fadeOut));
    }    // Periodic metallic squeal and rough low rumble; seamless two-second loop.
    private AudioClip CreateReefScrape()
    {
        const int sampleRate = 44100;
        const float duration = 2f;
        float[] samples = new float[(int)(sampleRate * duration)];
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)sampleRate;
            float cycle = 2f * Mathf.PI * t;
            float modulation = Mathf.Sin(cycle * 2f);
            float squeal = Mathf.Sin(cycle * 1340f + 5f * modulation) * 0.18f +
                Mathf.Sin(cycle * 1787f + 3f * Mathf.Sin(cycle * 3f)) * 0.12f;
            float grind = Mathf.Sin(cycle * 73f + 2f * Mathf.Sin(cycle * 31f)) * 0.16f +
                Mathf.Sin(cycle * 117f + 3f * Mathf.Sin(cycle * 47f)) * 0.12f;
            float roughness = 0.7f + 0.3f * Mathf.Sin(cycle * 19f);
            samples[i] = (squeal + grind) * roughness;
        }
        AudioClip clip = AudioClip.Create("Continuous Reef Metal Scrape", samples.Length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void OnDestroy()
    {
        if (generatedReefScrape != null) Destroy(generatedReefScrape);
        if (reefScrapeSource != null) Destroy(reefScrapeSource);
        if (reefScrapeNextSource != null) Destroy(reefScrapeNextSource);
    }
    private bool CrossesReef(
        Vector3 start, Vector3 end, RepairOceanContact reef)
    {
        float hullRadius = Mathf.Max(0f, submarineRadius);
        float top = Mathf.Min(reef.topDepth, reef.bottomDepth)
            - hullRadius;
        float bottom = Mathf.Max(reef.topDepth, reef.bottomDepth)
            + hullRadius;
        Vector3 movement = end - start;
        float enter = 0f;
        float exit = 1f;

        if (Mathf.Abs(movement.y) < 0.000001f)
        {
            if (start.y < top || start.y > bottom) return false;
        }
        else
        {
            float first = (top - start.y) / movement.y;
            float second = (bottom - start.y) / movement.y;
            enter = Mathf.Max(0f, Mathf.Min(first, second));
            exit = Mathf.Min(1f, Mathf.Max(first, second));
            if (enter > exit) return false;
        }

        Vector2 offset = new Vector2(
            start.x - reef.position.x, start.z - reef.position.z);
        Vector2 planar = new Vector2(movement.x, movement.z);

        float t = planar.sqrMagnitude > 0.000001f
            ? Mathf.Clamp(-Vector2.Dot(offset, planar)
                / planar.sqrMagnitude, enter, exit)
            : enter;

        float radius = hullRadius + Mathf.Max(0f, reef.radius);
        return (offset + planar * t).sqrMagnitude <= radius * radius;
    }

    private void TakeCollisionDamage()

    {

        if (failed)

            return;



        if (GameMenuController.IsInputBlocked || Time.timeScale <= 0f || Time.time < damageProtectionUntil) return;
        LastDamageTime = Time.time;
        damageProtectionUntil = Time.time + 2f;
        safeSeconds = 0f;
        TotalCollisions++;
        collisionCount = Mathf.Min(3, collisionCount + 1);



        shakeRemaining = Mathf.Max(0f, shakeDuration);

        shakeElapsed = 0f;



        if (collisionCount >= 3)

        {

            failed = true;

            controller.enabled = false;



            if (power != null)

                power.enabled = false;



            if (oxygen != null)

                oxygen.enabled = false;

        }

    }



    private void UpdateRecovery()
    {
        var mode = VoyageModeSession.Current;
        if (failed || mode == null || mode.Profile.healSeconds <= 0f || collisionCount == 0
            || GameMenuController.IsInputBlocked || controller == null || !controller.isActiveAndEnabled) return;
        if (touchingReef || touchingHazard) { safeSeconds = 0f; return; }
        safeSeconds += Time.deltaTime;
        if (safeSeconds < mode.Profile.healSeconds) return;
        safeSeconds = 0f;
        collisionCount--;
        mode.ReportHullRecovery();
    }
    private void UpdateCabinLight()

    {

        if (cabinDamageLight == null)

            return;



        if (collisionCount == 0)

        {

            cabinDamageLight.enabled = normalLightEnabled;

            cabinDamageLight.color = normalLightColor;

            cabinDamageLight.intensity = normalLightIntensity;

        }

        else if (collisionCount == 1)

        {

            cabinDamageLight.enabled = true;

            cabinDamageLight.color = normalLightColor;



            float wave = Mathf.Sin(

                Time.time * Mathf.PI * 2f

                * Mathf.Max(0.1f, flickerFrequency)

            );



            cabinDamageLight.intensity =

                normalLightIntensity * (wave > 0f ? 1f : 0.1f);

        }

        else if (collisionCount == 2)

        {

            cabinDamageLight.enabled = true;

            cabinDamageLight.color = Color.red;

            cabinDamageLight.intensity =

                normalLightIntensity

                * Mathf.Max(0f, redIntensityMultiplier);

        }

        else

        {

            cabinDamageLight.enabled = false;

        }

    }



    private void RestoreCameraOffset()

    {

        if (!hasCameraOffset || shakeCamera == null)

            return;



        if ((shakeCamera.localPosition - lastAppliedPosition)

            .sqrMagnitude < 0.00000001f)

        {

            shakeCamera.localPosition = cameraBasePosition;

        }

        else

        {

            shakeRemaining = 0f;

        }



        hasCameraOffset = false;

    }



    private void UpdateCameraShake()
    {
        if (shakeCamera == null) return;
        float dt = Time.deltaTime;
        Vector3 reefOffset;
        if (touchingReef)
        {
            reefShakePhase += dt * reefShakeFrequency;
            reefLastShakeOffset = ShakeWave(reefShakePhase) * Mathf.Max(0f, reefShakeStrength);
            reefReturnElapsed = 0f;
            reefOffset = reefLastShakeOffset;
        }
        else
        {
            reefReturnElapsed += dt;
            float progress = Mathf.Clamp01(reefReturnElapsed / Mathf.Max(0.01f, reefCameraReturnDuration));
            reefOffset = reefLastShakeOffset * (1f - Mathf.SmoothStep(0f, 1f, progress));
            if (progress >= 1f) reefLastShakeOffset = Vector3.zero;
        }

        Vector3 impactOffset = Vector3.zero;
        if (shakeRemaining > 0f)
        {
            shakeElapsed += dt;
            shakeRemaining = Mathf.Max(0f, shakeRemaining - dt);
            float fade = shakeRemaining / Mathf.Max(0.01f, shakeDuration);
            impactOffset = ShakeWave(shakeElapsed * shakeFrequency) *
                Mathf.Max(0f, shakeStrength) * fade * fade;
        }
        Vector3 offset = reefOffset + impactOffset;
        if (offset.sqrMagnitude < 0.0000000001f) return;
        cameraBasePosition = shakeCamera.localPosition;
        offset = shakeCamera.localRotation * offset;
        lastAppliedPosition = cameraBasePosition + offset;
        shakeCamera.localPosition = lastAppliedPosition;
        hasCameraOffset = true;
    }

    private static Vector3 ShakeWave(float phase)
    {
        return new Vector3(Mathf.Sin(phase * 1.7f),
            Mathf.Sin(phase * 2.3f + 0.8f),
            Mathf.Sin(phase * 1.3f + 1.6f) * 0.35f);
    }
    private void RefreshStatus()

    {

        if (hullStatus == null)

            return;



        hullStatus.color = collisionCount >= 2

            ? new Color(1f, 0.35f, 0.25f)

            : new Color(0.5f, 1f, 0.65f);



        hullStatus.text = failed

            ? "HULL DESTROYED - VOYAGE FAILED"

            : $"HULL {HullPercentage:F0}% | HITS {collisionCount}/3";

    }



    private void OnDisable()

    {
        touchingReef = false;
        reefLastShakeOffset = Vector3.zero;
        reefReturnElapsed = 0f;
        StopReefScrape();

        RestoreCameraOffset();

        shakeRemaining = 0f;

    }

}
