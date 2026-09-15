using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Game-wide settings (DDOL). Menu can change prefs without scene audio sources;
/// main-scene BGM/SFX pick them up via multipliers / registration.
/// </summary>
public class GameSettings : MonoBehaviour
{
    public static GameSettings Instance { get; private set; }

    public const string PrefMaster = "settings.master";
    public const string PrefMusic = "settings.music";
    public const string PrefSfx = "settings.sfx";

    [SerializeField] float master = 0.8f;
    [SerializeField] float music = 0.7f;
    [SerializeField] float sfx = 0.85f;

    readonly List<AudioSource> musicSources = new();
    readonly List<AudioSource> sfxSources = new();

    public float Master => master;
    public float Music => music;
    public float Sfx => sfx;

    /// <summary>Multiply a authored BGM volume by the Music slider (Master is AudioListener).</summary>
    public static float MusicScale
    {
        get
        {
            var g = Instance != null ? Instance : EnsureExists();
            return g != null ? Mathf.Clamp01(g.music) : 1f;
        }
    }

    public static float SfxScale
    {
        get
        {
            var g = Instance != null ? Instance : EnsureExists();
            return g != null ? Mathf.Clamp01(g.sfx) : 1f;
        }
    }

    public static GameSettings EnsureExists()
    {
        if (Instance != null) return Instance;
        var existing = FindAnyObjectByType<GameSettings>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }
        var go = new GameObject("GameSettings");
        Instance = go.AddComponent<GameSettings>();
        DontDestroyOnLoad(go);
        Instance.Load();
        Instance.ApplyAll();
        return Instance;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Load();
        ApplyAll();
    }

    public void Load()
    {
        master = PlayerPrefs.GetFloat(PrefMaster, 0.8f);
        music = PlayerPrefs.GetFloat(PrefMusic, 0.7f);
        sfx = PlayerPrefs.GetFloat(PrefSfx, 0.85f);
    }

    public void SetMaster(float v)
    {
        master = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(PrefMaster, master);
        ApplyAll();
    }

    public void SetMusic(float v)
    {
        music = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(PrefMusic, music);
        ApplyMusic();
        FindAnyObjectByType<BgmLayers>()?.RefreshVolumesFromSettings();
    }

    public void SetSfx(float v)
    {
        sfx = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(PrefSfx, sfx);
        ApplySfx();
    }

    public void RegisterMusic(AudioSource source)
    {
        if (source == null || musicSources.Contains(source)) return;
        musicSources.Add(source);
        ApplyMusic();
    }

    public void RegisterSfx(AudioSource source)
    {
        if (source == null || sfxSources.Contains(source)) return;
        sfxSources.Add(source);
        ApplySfx();
    }

    public void ApplyAll()
    {
        AudioListener.volume = master;
        ApplyMusic();
        ApplySfx();
    }

    void ApplyMusic()
    {
        // Only nudge sources that aren't mid-authored fade — BgmLayers also scales via MusicScale.
        foreach (var s in musicSources)
            if (s != null) s.volume = Mathf.Min(s.volume, 1f);
    }

    void ApplySfx()
    {
        foreach (var s in sfxSources)
            if (s != null) s.volume = sfx; // SFX sources usually stay at authored level * SfxScale at PlayOneShot sites
    }

    public void Save() => PlayerPrefs.Save();
}
