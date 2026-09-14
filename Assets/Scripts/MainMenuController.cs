using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

[RequireComponent(typeof(PanelRenderer))]
public class MainMenuController : MonoBehaviour
{
    [Header("Scenes")]
    [SerializeField] string gameSceneName = "SampleScene";

    [Header("Play iris")]
    [SerializeField] float irisDuration = 0.9f;
    [SerializeField] float loadDelayAfterIris = 0.05f;

    [Header("Audio (optional)")]
    [SerializeField] AudioSource menuMusic;
    [SerializeField] float menuMusicVolume = 0.35f;

    [Header("Audio mix targets (optional)")]
    [SerializeField] AudioSource[] musicSources;
    [SerializeField] AudioSource[] sfxSources;

    PanelRenderer panelRenderer;
    VisualElement root;
    VisualElement settingsOverlay;
    VisualElement playIris;
    VisualElement irisCircle;
    Label idleLabel;
    Label masterValue;
    Label musicValue;
    Label sfxValue;
    Slider masterSlider;
    Slider musicSlider;
    Slider sfxSlider;
    int uiVersion = -1;

    float menuEnteredAt;
    bool transitioning;
    IVisualElementScheduledItem idleTicker;

    const string PrefMaster = "settings.master";
    const string PrefMusic = "settings.music";
    const string PrefSfx = "settings.sfx";

    void OnEnable()
    {
        panelRenderer = GetComponent<PanelRenderer>();
        if (panelRenderer == null)
        {
            Debug.LogError("MainMenuController needs a Panel Renderer on this GameObject.");
            return;
        }
        panelRenderer.RegisterUIReloadCallback(OnUIReload);
    }

    void OnDisable()
    {
        if (panelRenderer != null)
            panelRenderer.UnregisterUIReloadCallback(OnUIReload);
        idleTicker?.Pause();
        idleTicker = null;
    }

    void OnUIReload(PanelRenderer renderer, VisualElement visualRoot, int version)
    {
        if (version == uiVersion)
            return;
        uiVersion = version;

        root = visualRoot;
        Bind(root);
        ApplySavedVolumes();
        SetSettingsOpen(false);
        ResetIris();
        StartMenuMusic();

        menuEnteredAt = Time.unscaledTime;
        transitioning = false;
        UpdateIdleLabel();

        idleTicker?.Pause();
        idleTicker = root.schedule.Execute(UpdateIdleLabel).Every(50);

        root.focusable = true;
        root.RegisterCallback<KeyDownEvent>(OnKeyDown);
        root.Focus();
    }

    void Bind(VisualElement r)
    {
        r.Q<Button>("btn-play")?.RegisterCallback<ClickEvent>(_ => Play());
        r.Q<Button>("btn-settings")?.RegisterCallback<ClickEvent>(_ => SetSettingsOpen(true));
        r.Q<Button>("btn-quit")?.RegisterCallback<ClickEvent>(_ => Quit());
        r.Q<Button>("btn-settings-close")?.RegisterCallback<ClickEvent>(_ => SetSettingsOpen(false));

        settingsOverlay = r.Q("settings-overlay");
        playIris = r.Q("play-iris");
        irisCircle = r.Q("iris-circle");
        idleLabel = r.Q<Label>("idle-label");
        masterValue = r.Q<Label>("master-value");
        musicValue = r.Q<Label>("music-value");
        sfxValue = r.Q<Label>("sfx-value");
        masterSlider = r.Q<Slider>("master-slider");
        musicSlider = r.Q<Slider>("music-slider");
        sfxSlider = r.Q<Slider>("sfx-slider");

        float master = PlayerPrefs.GetFloat(PrefMaster, 0.8f);
        float music = PlayerPrefs.GetFloat(PrefMusic, 0.7f);
        float sfx = PlayerPrefs.GetFloat(PrefSfx, 0.85f);

        if (masterSlider != null)
        {
            masterSlider.UnregisterValueChangedCallback(OnMasterChanged);
            masterSlider.value = master;
            masterSlider.RegisterValueChangedCallback(OnMasterChanged);
        }
        if (musicSlider != null)
        {
            musicSlider.UnregisterValueChangedCallback(OnMusicChanged);
            musicSlider.value = music;
            musicSlider.RegisterValueChangedCallback(OnMusicChanged);
        }
        if (sfxSlider != null)
        {
            sfxSlider.UnregisterValueChangedCallback(OnSfxChanged);
            sfxSlider.value = sfx;
            sfxSlider.RegisterValueChangedCallback(OnSfxChanged);
        }
    }

    void UpdateIdleLabel()
    {
        if (idleLabel == null || transitioning) return;
        float t = Mathf.Max(0f, Time.unscaledTime - menuEnteredAt);
        int totalTenths = Mathf.FloorToInt(t * 10f);
        int tenths = totalTenths % 10;
        int secs = (totalTenths / 10) % 60;
        int mins = totalTenths / 600;
        idleLabel.text = $"DWELL  {mins:00}:{secs:00}.{tenths}";
    }

    void ResetIris()
    {
        if (playIris != null)
            playIris.AddToClassList("hidden");
        if (irisCircle != null)
        {
            irisCircle.RemoveFromClassList("expand");
            irisCircle.style.transitionDuration = new List<TimeValue> { new TimeValue(0f) };
            irisCircle.style.scale = new Scale(new Vector3(0.05f, 0.05f, 1f));
        }
    }

    void OnMasterChanged(ChangeEvent<float> evt)
    {
        ApplyMaster(evt.newValue);
        PlayerPrefs.SetFloat(PrefMaster, evt.newValue);
    }

    void OnMusicChanged(ChangeEvent<float> evt)
    {
        ApplyMusic(evt.newValue);
        PlayerPrefs.SetFloat(PrefMusic, evt.newValue);
    }

    void OnSfxChanged(ChangeEvent<float> evt)
    {
        ApplySfx(evt.newValue);
        PlayerPrefs.SetFloat(PrefSfx, evt.newValue);
    }

    void ApplySavedVolumes()
    {
        ApplyMaster(PlayerPrefs.GetFloat(PrefMaster, 0.8f));
        ApplyMusic(PlayerPrefs.GetFloat(PrefMusic, 0.7f));
        ApplySfx(PlayerPrefs.GetFloat(PrefSfx, 0.85f));
    }

    void StartMenuMusic()
    {
        if (menuMusic == null) return;
        menuMusic.loop = true;
        menuMusic.spatialBlend = 0f;
        menuMusic.priority = 0;
        float master = masterSlider != null ? masterSlider.value : PlayerPrefs.GetFloat(PrefMaster, 0.8f);
        float music = musicSlider != null ? musicSlider.value : PlayerPrefs.GetFloat(PrefMusic, 0.7f);
        menuMusic.volume = menuMusicVolume * music * master;
        if (!menuMusic.isPlaying) menuMusic.Play();
    }

    void OnKeyDown(KeyDownEvent evt)
    {
        if (transitioning) return;
        if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
        {
            if (settingsOverlay != null && !settingsOverlay.ClassListContains("hidden"))
                SetSettingsOpen(false);
            else
                Play();
            evt.StopPropagation();
        }
        else if (evt.keyCode == KeyCode.Escape)
        {
            bool open = settingsOverlay != null && !settingsOverlay.ClassListContains("hidden");
            SetSettingsOpen(!open);
            evt.StopPropagation();
        }
    }

    void SetSettingsOpen(bool open)
    {
        if (settingsOverlay == null || transitioning) return;
        if (open) settingsOverlay.RemoveFromClassList("hidden");
        else settingsOverlay.AddToClassList("hidden");
    }

    void Play()
    {
        if (transitioning) return;
        PlayerPrefs.Save();
        if (string.IsNullOrEmpty(gameSceneName))
        {
            Debug.LogError("MainMenuController: set gameSceneName.");
            return;
        }
        StartCoroutine(PlayWithIris());
    }

    IEnumerator PlayWithIris()
    {
        transitioning = true;
        SetSettingsOpen(false);

        // Lock buttons
        root?.Query<Button>().ForEach(b => b.SetEnabled(false));

        if (playIris != null && irisCircle != null)
        {
            playIris.RemoveFromClassList("hidden");
            playIris.pickingMode = PickingMode.Position; // block clicks during wipe

            // force starting scale then expand next frame so transition fires
            irisCircle.style.transitionDuration = new List<TimeValue> { new TimeValue(0f) };
            irisCircle.style.scale = new Scale(new Vector3(0.05f, 0.05f, 1f));
            irisCircle.RemoveFromClassList("expand");

            yield return null;

            irisCircle.style.transitionDuration = new List<TimeValue>
            {
                new TimeValue(irisDuration * 1000f, TimeUnit.Millisecond)
            };
            irisCircle.AddToClassList("expand");
            irisCircle.style.scale = new Scale(new Vector3(90f, 90f, 1f));
        }

        // fade menu music during iris
        float fadeT = 0f;
        float startVol = menuMusic != null ? menuMusic.volume : 0f;
        while (fadeT < irisDuration)
        {
            fadeT += Time.unscaledDeltaTime;
            if (menuMusic != null)
                menuMusic.volume = Mathf.Lerp(startVol, 0f, fadeT / irisDuration);
            yield return null;
        }

        if (loadDelayAfterIris > 0f)
            yield return new WaitForSecondsRealtime(loadDelayAfterIris);

        SceneManager.LoadScene(gameSceneName);
    }

    void Quit()
    {
        if (transitioning) return;
        PlayerPrefs.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void ApplyMaster(float v)
    {
        AudioListener.volume = Mathf.Clamp01(v);
        if (masterValue != null) masterValue.text = Mathf.RoundToInt(v * 100f) + "%";
        if (musicSlider != null) ApplyMusic(musicSlider.value);
        else ApplyMusic(PlayerPrefs.GetFloat(PrefMusic, 0.7f));
        if (sfxSlider != null) ApplySfx(sfxSlider.value);
        else ApplySfx(PlayerPrefs.GetFloat(PrefSfx, 0.85f));
    }

    void ApplyMusic(float v)
    {
        v = Mathf.Clamp01(v);
        if (musicValue != null) musicValue.text = Mathf.RoundToInt(v * 100f) + "%";
        float master = masterSlider != null ? masterSlider.value : PlayerPrefs.GetFloat(PrefMaster, 0.8f);
        if (menuMusic != null && !transitioning) menuMusic.volume = menuMusicVolume * v * master;
        if (musicSources != null)
            foreach (var s in musicSources)
                if (s != null) s.volume = v * master;
    }

    void ApplySfx(float v)
    {
        v = Mathf.Clamp01(v);
        if (sfxValue != null) sfxValue.text = Mathf.RoundToInt(v * 100f) + "%";
        float master = masterSlider != null ? masterSlider.value : PlayerPrefs.GetFloat(PrefMaster, 0.8f);
        if (sfxSources != null)
            foreach (var s in sfxSources)
                if (s != null) s.volume = v * master;
    }
}
