using UnityEngine;

/// <summary>Menu audio continues while gameplay is paused and clicks survive scene reloads.</summary>
public sealed class SubmarineMenuAudio : MonoBehaviour
{
    private static SubmarineMenuAudio current;
    private AudioSource music;
    private AudioSource effects;
    private AudioClip hoverClip;
    private AudioClip clickClip;
    private float musicVolume;
    private float hoverVolume;
    private float clickVolume;
    private bool musicVisible;
    public float MasterLevel { get; private set; }
    public float MusicLevel { get; private set; }
    public float EffectsLevel { get; private set; }

    public static SubmarineMenuAudio GetOrCreate()
    {
        if (current != null) return current;
        current = FindAnyObjectByType<SubmarineMenuAudio>();
        if (current != null) return current;
        return new GameObject("MenuAudio").AddComponent<SubmarineMenuAudio>();
    }

    private void Awake()
    {
        if (current != null && current != this) { Destroy(gameObject); return; }
        current = this;
        DontDestroyOnLoad(gameObject);
        MasterLevel = Mathf.Clamp01(PlayerPrefs.GetFloat("Audio.Master", 1f));
        MusicLevel = Mathf.Clamp01(PlayerPrefs.GetFloat("Audio.MenuMusic", 1f));
        EffectsLevel = Mathf.Clamp01(PlayerPrefs.GetFloat("Audio.MenuEffects", 1f));
        AudioListener.volume = MasterLevel;
        music = CreateSource();
        music.loop = true;
        music.volume = 0f;
        effects = CreateSource();
    }

    private AudioSource CreateSource()
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
        source.priority = 32;
        return source;
    }

    public void Configure(AudioClip background, AudioClip hover, AudioClip click,
        float backgroundVolume, float hoverGain, float clickGain)
    {
        if (music.clip != background)
        {
            music.Stop();
            music.clip = background;
            music.volume = 0f;
        }
        hoverClip = hover;
        clickClip = click;
        musicVolume = Mathf.Clamp01(backgroundVolume);
        hoverVolume = Mathf.Clamp01(hoverGain);
        clickVolume = Mathf.Clamp01(clickGain);
    }

    public void SetMusicVisible(bool visible)
    {
        musicVisible = visible;
        if (visible && music.clip != null && !music.isPlaying) music.Play();
    }

    public void PlayHover()
    {
        if (hoverClip != null) effects.PlayOneShot(hoverClip, hoverVolume * EffectsLevel);
    }

    public void PlayClick()
    {
        if (clickClip != null) effects.PlayOneShot(clickClip, clickVolume * EffectsLevel);
    }

    public void SetMasterLevel(float value)
    {
        MasterLevel = Mathf.Clamp01(value);
        AudioListener.volume = MasterLevel;
        PlayerPrefs.SetFloat("Audio.Master", MasterLevel);
    }

    public void SetMusicLevel(float value)
    {
        MusicLevel = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat("Audio.MenuMusic", MusicLevel);
    }

    public void SetEffectsLevel(float value)
    {
        EffectsLevel = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat("Audio.MenuEffects", EffectsLevel);
    }

    private void Update()
    {
        music.volume = Mathf.MoveTowards(music.volume, musicVisible ? musicVolume * MusicLevel : 0f,
            Time.unscaledDeltaTime * 0.6f);
        if (!musicVisible && music.volume <= 0f && music.isPlaying) music.Stop();
    }

    private void OnDestroy()
    {
        if (current == this) current = null;
    }
}
