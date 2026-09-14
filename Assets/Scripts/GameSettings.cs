using UnityEngine;

/// <summary>
/// Game-wide audio (and later) settings. Survives menu → game scene.
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

    [Tooltip("Optional music sources to drive from the Music slider (BGM etc.).")]
    [SerializeField] AudioSource[] musicSources;
    [SerializeField] AudioSource[] sfxSources;

    public float Master => master;
    public float Music => music;
    public float Sfx => sfx;

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
    }

    public void SetSfx(float v)
    {
        sfx = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(PrefSfx, sfx);
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
        if (musicSources == null) return;
        foreach (var s in musicSources)
            if (s != null) s.volume = music * master;
    }

    void ApplySfx()
    {
        if (sfxSources == null) return;
        foreach (var s in sfxSources)
            if (s != null) s.volume = sfx * master;
    }

    public void Save() => PlayerPrefs.Save();
}
