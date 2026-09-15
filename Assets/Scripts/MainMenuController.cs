using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine.Video;

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

    [Header("Background video")]
    [SerializeField] VideoClip trailerClip;
    [SerializeField] bool loopTrailer = true;
    [SerializeField] bool muteTrailer = true;
    [SerializeField] Vector2Int videoRtSize = new Vector2Int(1920, 1080);

    [Header("Patch notes")]
    [SerializeField] TextAsset patchNotesAsset;
    [TextArea(8, 24)]
    [SerializeField] string patchNotesFallback =
        "TIME TO LIVE — Patch Notes\n\nv0.1\n• Main menu, shared run seed, field-kit HUD\n• World size setup, layered BGM, breaker walls\n";

    PanelRenderer panelRenderer;
    VideoPlayer videoPlayer;
    RenderTexture videoRt;

    VisualElement root;
    VisualElement videoBg;
    VisualElement playIris;
    VisualElement irisCircle;
    VisualElement panelNotes;
    VisualElement panelSettings;
    Button tabNotes;
    Button tabSettings;
    Label idleLabel;
    Label notesBody;
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
    IVisualElementScheduledItem videoBlit;
    string activeTab = "notes";

    void OnEnable()
    {
        panelRenderer = GetComponent<PanelRenderer>();
        if (panelRenderer == null)
        {
            Debug.LogError("MainMenuController needs a Panel Renderer on this GameObject.");
            return;
        }
        EnsureVideoPlayer();
        panelRenderer.RegisterUIReloadCallback(OnUIReload);
        GameSettings.EnsureExists();
        GameSettings.Instance.ApplyAll();
    }

    void OnDisable()
    {
        if (panelRenderer != null)
            panelRenderer.UnregisterUIReloadCallback(OnUIReload);
        idleTicker?.Pause();
        idleTicker = null;
        videoBlit?.Pause();
        videoBlit = null;
        if (videoPlayer != null && videoPlayer.isPlaying)
            videoPlayer.Pause();
    }

    void OnDestroy()
    {
        if (videoRt != null)
        {
            videoRt.Release();
            Destroy(videoRt);
            videoRt = null;
        }
    }

    void Update()
    {
        if (transitioning) return;
        if (menuMusic != null && GameSettings.Instance != null)
            menuMusic.volume = menuMusicVolume * GameSettings.MusicScale * GameSettings.Instance.Master;
    }

    void EnsureVideoPlayer()
    {
        videoPlayer = GetComponent<VideoPlayer>();
        if (videoPlayer == null)
            videoPlayer = gameObject.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = loopTrailer;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.audioOutputMode = muteTrailer
            ? VideoAudioOutputMode.None
            : VideoAudioOutputMode.Direct;
        if (videoRt == null)
        {
            int w = Mathf.Max(640, videoRtSize.x);
            int h = Mathf.Max(360, videoRtSize.y);
            videoRt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32);
            videoRt.Create();
        }
        videoPlayer.targetTexture = videoRt;
        if (trailerClip != null)
            videoPlayer.clip = trailerClip;
    }

    void OnUIReload(PanelRenderer renderer, VisualElement visualRoot, int version)
    {
        if (version == uiVersion) return;
        uiVersion = version;

        root = visualRoot;
        Bind(root);
        ResetIris();
        SetupContent();

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
        r.Q<Button>("btn-quit")?.RegisterCallback<ClickEvent>(_ => Quit());

        tabNotes = r.Q<Button>("tab-notes");
        tabSettings = r.Q<Button>("tab-settings");
        tabNotes?.RegisterCallback<ClickEvent>(_ => ShowTab("notes"));
        tabSettings?.RegisterCallback<ClickEvent>(_ => ShowTab("settings"));

        videoBg = r.Q("video-bg");
        playIris = r.Q("play-iris");
        irisCircle = r.Q("iris-circle");
        idleLabel = r.Q<Label>("idle-label");
        panelNotes = r.Q("panel-notes");
        panelSettings = r.Q("panel-settings");
        notesBody = r.Q<Label>("notes-body");
        masterValue = r.Q<Label>("master-value");
        musicValue = r.Q<Label>("music-value");
        sfxValue = r.Q<Label>("sfx-value");
        masterSlider = r.Q<Slider>("master-slider");
        musicSlider = r.Q<Slider>("music-slider");
        sfxSlider = r.Q<Slider>("sfx-slider");

        var gs = GameSettings.EnsureExists();
        if (masterSlider != null)
        {
            masterSlider.UnregisterValueChangedCallback(OnMasterChanged);
            masterSlider.value = gs.Master;
            masterSlider.RegisterValueChangedCallback(OnMasterChanged);
        }
        if (musicSlider != null)
        {
            musicSlider.UnregisterValueChangedCallback(OnMusicChanged);
            musicSlider.value = gs.Music;
            musicSlider.RegisterValueChangedCallback(OnMusicChanged);
        }
        if (sfxSlider != null)
        {
            sfxSlider.UnregisterValueChangedCallback(OnSfxChanged);
            sfxSlider.value = gs.Sfx;
            sfxSlider.RegisterValueChangedCallback(OnSfxChanged);
        }
        RefreshLabels();
    }

    void SetupContent()
    {
        string notes = patchNotesAsset != null ? patchNotesAsset.text : patchNotesFallback;
        if (notesBody != null) notesBody.text = notes;

        EnsureVideoPlayer();
        if (trailerClip != null)
        {
            videoPlayer.clip = trailerClip;
            if (!videoPlayer.isPlaying) videoPlayer.Play();
        }

        videoBlit?.Pause();
        videoBlit = root.schedule.Execute(BlitVideo).Every(33);

        ShowTab("notes");
        StartMenuMusic();
    }

    void BlitVideo()
    {
        if (videoBg == null || videoRt == null) return;
        videoBg.style.backgroundImage = Background.FromRenderTexture(videoRt);
    }

    void ShowTab(string tab)
    {
        activeTab = tab;
        SetSelected(tabNotes, tab == "notes");
        SetSelected(tabSettings, tab == "settings");
        SetVisible(panelNotes, tab == "notes");
        SetVisible(panelSettings, tab == "settings");
        if (tab != "settings")
            GameSettings.Instance?.Save();
    }

    static void SetSelected(Button b, bool on)
    {
        if (b == null) return;
        if (on) b.AddToClassList("selected");
        else b.RemoveFromClassList("selected");
    }

    static void SetVisible(VisualElement e, bool on)
    {
        if (e == null) return;
        if (on) e.RemoveFromClassList("hidden");
        else e.AddToClassList("hidden");
    }

    void OnMasterChanged(ChangeEvent<float> evt)
    {
        GameSettings.Instance.SetMaster(evt.newValue);
        RefreshLabels();
    }

    void OnMusicChanged(ChangeEvent<float> evt)
    {
        GameSettings.Instance.SetMusic(evt.newValue);
        RefreshLabels();
    }

    void OnSfxChanged(ChangeEvent<float> evt)
    {
        GameSettings.Instance.SetSfx(evt.newValue);
        RefreshLabels();
    }

    void RefreshLabels()
    {
        var gs = GameSettings.Instance;
        if (gs == null) return;
        if (masterValue != null) masterValue.text = Mathf.RoundToInt(gs.Master * 100f) + "%";
        if (musicValue != null) musicValue.text = Mathf.RoundToInt(gs.Music * 100f) + "%";
        if (sfxValue != null) sfxValue.text = Mathf.RoundToInt(gs.Sfx * 100f) + "%";
    }

    void UpdateIdleLabel()
    {
        if (idleLabel == null || transitioning) return;
        float t = Mathf.Max(0f, Time.unscaledTime - menuEnteredAt);
        int totalTenths = Mathf.FloorToInt(t * 10f);
        int tenths = totalTenths % 10;
        int secs = (totalTenths / 10) % 60;
        int mins = totalTenths / 600;
        idleLabel.text = $"{mins:00}:{secs:00}.{tenths}";
    }

    void ResetIris()
    {
        if (playIris != null) playIris.AddToClassList("hidden");
        if (irisCircle != null)
        {
            irisCircle.RemoveFromClassList("expand");
            irisCircle.style.transitionDuration = new List<TimeValue> { new TimeValue(0f) };
            irisCircle.style.scale = new Scale(new Vector3(0.05f, 0.05f, 1f));
        }
    }

    void StartMenuMusic()
    {
        if (menuMusic == null) return;
        menuMusic.loop = true;
        menuMusic.spatialBlend = 0f;
        menuMusic.priority = 0;
        var gs = GameSettings.EnsureExists();
        menuMusic.volume = menuMusicVolume * gs.Music * gs.Master;
        if (!menuMusic.isPlaying) menuMusic.Play();
    }

    void OnKeyDown(KeyDownEvent evt)
    {
        if (transitioning) return;
        if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
        {
            Play();
            evt.StopPropagation();
        }
        else if (evt.keyCode == KeyCode.Escape)
        {
            ShowTab(activeTab == "settings" ? "notes" : "settings");
            evt.StopPropagation();
        }
    }

    void Play()
    {
        if (transitioning) return;
        GameSettings.Instance?.Save();
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
        if (videoPlayer != null) videoPlayer.Stop();
        root?.Query<Button>().ForEach(b => b.SetEnabled(false));

        if (playIris != null && irisCircle != null)
        {
            playIris.RemoveFromClassList("hidden");
            playIris.pickingMode = PickingMode.Position;
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
        GameSettings.Instance?.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
