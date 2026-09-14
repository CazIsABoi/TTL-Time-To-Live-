using System.Collections;
using System.Collections.Generic;
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

    PanelRenderer panelRenderer;
    VisualElement root;
    VisualElement playIris;
    VisualElement irisCircle;
    Label idleLabel;
    int uiVersion = -1;

    float menuEnteredAt;
    bool transitioning;
    IVisualElementScheduledItem idleTicker;

    void OnEnable()
    {
        panelRenderer = GetComponent<PanelRenderer>();
        if (panelRenderer == null)
        {
            Debug.LogError("MainMenuController needs a Panel Renderer on this GameObject.");
            return;
        }
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
    }

    void OnUIReload(PanelRenderer renderer, VisualElement visualRoot, int version)
    {
        if (version == uiVersion) return;
        uiVersion = version;

        root = visualRoot;
        Bind(root);
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
        r.Q<Button>("btn-quit")?.RegisterCallback<ClickEvent>(_ => Quit());
        playIris = r.Q("play-iris");
        irisCircle = r.Q("iris-circle");
        idleLabel = r.Q<Label>("idle-label");
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
        float master = GameSettings.Instance != null ? GameSettings.Instance.Master : 0.8f;
        float music = GameSettings.Instance != null ? GameSettings.Instance.Music : 0.7f;
        menuMusic.volume = menuMusicVolume * music * master;
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
    }

    void Play()
    {
        if (transitioning) return;
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
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
