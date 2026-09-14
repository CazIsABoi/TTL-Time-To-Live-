using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Main-scene overlay: reverse iris, world-size setup (incl. custom), ESC pause/settings.
/// Wire Pause Action like GridBuildPlacer / OrbitCamera (InputActionReference), or leave empty — Escape still works.
/// </summary>
[RequireComponent(typeof(PanelRenderer))]
public class RunOverlayUI : MonoBehaviour
{
    [Header("Scenes")]
    [SerializeField] string mainMenuSceneName = "MainMenu";

    [Header("Input (same pattern as placer / camera)")]
    [Tooltip("Drag PlayerActions → Pause (Esc). Optional — Escape key works even if empty.")]
    [SerializeField] InputActionReference pauseAction;

    [Header("Iris")]
    [SerializeField] float irisOpenDuration = 0.85f;

    [Header("World sizes (columns × rows)")]
    [SerializeField] Vector2Int compactSize = new Vector2Int(10, 5);
    [SerializeField] Vector2Int standardSize = new Vector2Int(16, 8);
    [SerializeField] Vector2Int vastSize = new Vector2Int(24, 12);
    [SerializeField] Vector2Int customMin = new Vector2Int(2, 2);
    [SerializeField] Vector2Int customMax = new Vector2Int(256, 128);
    [SerializeField] Vector2Int customDefault = new Vector2Int(16, 8);

    [Header("Refs (optional — auto-found)")]
    [SerializeField] ExpandingIsland island;
    [SerializeField] GridManager grid;

    PanelRenderer panelRenderer;
    VisualElement root;
    VisualElement irisLayer;
    VisualElement irisCircle;
    VisualElement worldSetup;
    VisualElement pauseOverlay;
    VisualElement customSizeRow;
    Label sizeSummary;
    Label masterValue;
    Label musicValue;
    Label sfxValue;
    Slider masterSlider;
    Slider musicSlider;
    Slider sfxSlider;
    IntegerField customCols;
    IntegerField customRows;
    Button sizeCompact;
    Button sizeStandard;
    Button sizeVast;
    Button sizeCustom;

    int uiVersion = -1;
    Vector2Int selectedSize;
    string selectedLabel = "STANDARD";
    bool usingCustom;
    bool setupDone;
    bool paused;
    bool irisBusy;

    void Awake()
    {
        GameSettings.EnsureExists();
        if (island == null) island = FindAnyObjectByType<ExpandingIsland>();
        if (grid == null) grid = GridManager.Instance ?? FindAnyObjectByType<GridManager>();
        selectedSize = standardSize;
        // Upgrade old serialized caps (was 48×24)
        if (customMax.x < 128 || customMax.y < 64)
            customMax = new Vector2Int(256, 128);
        if (customMin.x < 1) customMin = new Vector2Int(2, 2);
    }

    void OnEnable()
    {
        panelRenderer = GetComponent<PanelRenderer>();
        if (panelRenderer == null)
        {
            Debug.LogError("RunOverlayUI needs a Panel Renderer.");
            return;
        }
        panelRenderer.RegisterUIReloadCallback(OnUIReload);

        if (pauseAction != null && pauseAction.action != null)
        {
            pauseAction.action.performed += OnPausePerformed;
            pauseAction.action.Enable();
        }
    }

    void OnDisable()
    {
        if (panelRenderer != null)
            panelRenderer.UnregisterUIReloadCallback(OnUIReload);

        if (pauseAction != null && pauseAction.action != null)
        {
            pauseAction.action.performed -= OnPausePerformed;
            pauseAction.action.Disable();
        }
        Time.timeScale = 1f;
    }

    void Update()
    {
        // Fallback when Pause Action isn't wired yet
        if (pauseAction != null && pauseAction.action != null) return;
        if (!setupDone || irisBusy) return;
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
            TogglePause();
    }

    void OnPausePerformed(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        if (!setupDone || irisBusy) return;
        TogglePause();
    }

    void OnUIReload(PanelRenderer renderer, VisualElement visualRoot, int version)
    {
        if (version == uiVersion) return;
        uiVersion = version;
        root = visualRoot;

        irisLayer = root.Q("iris-layer");
        irisCircle = root.Q("iris-circle");
        worldSetup = root.Q("world-setup");
        pauseOverlay = root.Q("pause-overlay");
        customSizeRow = root.Q("custom-size-row");
        sizeSummary = root.Q<Label>("size-summary");
        masterValue = root.Q<Label>("master-value");
        musicValue = root.Q<Label>("music-value");
        sfxValue = root.Q<Label>("sfx-value");
        masterSlider = root.Q<Slider>("master-slider");
        musicSlider = root.Q<Slider>("music-slider");
        sfxSlider = root.Q<Slider>("sfx-slider");
        customCols = root.Q<IntegerField>("custom-cols");
        customRows = root.Q<IntegerField>("custom-rows");
        sizeCompact = root.Q<Button>("size-compact");
        sizeStandard = root.Q<Button>("size-standard");
        sizeVast = root.Q<Button>("size-vast");
        sizeCustom = root.Q<Button>("size-custom");

        sizeCompact?.RegisterCallback<ClickEvent>(_ => SelectPreset(compactSize, "COMPACT", sizeCompact));
        sizeStandard?.RegisterCallback<ClickEvent>(_ => SelectPreset(standardSize, "STANDARD", sizeStandard));
        sizeVast?.RegisterCallback<ClickEvent>(_ => SelectPreset(vastSize, "VAST", sizeVast));
        sizeCustom?.RegisterCallback<ClickEvent>(_ => SelectCustom());

        if (customCols != null)
        {
            customCols.value = customDefault.x;
            customCols.UnregisterValueChangedCallback(OnCustomChanged);
            customCols.RegisterValueChangedCallback(OnCustomChanged);
        }
        if (customRows != null)
        {
            customRows.value = customDefault.y;
            customRows.UnregisterValueChangedCallback(OnCustomChanged);
            customRows.RegisterValueChangedCallback(OnCustomChanged);
        }

        root.Q<Button>("btn-deploy")?.RegisterCallback<ClickEvent>(_ => Deploy());
        root.Q<Button>("btn-resume")?.RegisterCallback<ClickEvent>(_ => SetPaused(false));
        root.Q<Button>("btn-quit-menu")?.RegisterCallback<ClickEvent>(_ => QuitToMenu());

        BindSettingsSliders();

        setupDone = false;
        paused = false;
        usingCustom = false;
        SetPauseVisible(false);
        SetCustomRowVisible(false);
        if (worldSetup != null) worldSetup.RemoveFromClassList("hidden");

        SelectPreset(standardSize, "STANDARD", sizeStandard);
        StartCoroutine(OpenIrisThenShowSetup());
    }

    void BindSettingsSliders()
    {
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
        RefreshSettingLabels();
        gs.ApplyAll();
    }

    void OnMasterChanged(ChangeEvent<float> evt)
    {
        GameSettings.Instance.SetMaster(evt.newValue);
        RefreshSettingLabels();
    }

    void OnMusicChanged(ChangeEvent<float> evt)
    {
        GameSettings.Instance.SetMusic(evt.newValue);
        RefreshSettingLabels();
    }

    void OnSfxChanged(ChangeEvent<float> evt)
    {
        GameSettings.Instance.SetSfx(evt.newValue);
        RefreshSettingLabels();
    }

    void RefreshSettingLabels()
    {
        var gs = GameSettings.Instance;
        if (gs == null) return;
        if (masterValue != null) masterValue.text = Mathf.RoundToInt(gs.Master * 100f) + "%";
        if (musicValue != null) musicValue.text = Mathf.RoundToInt(gs.Music * 100f) + "%";
        if (sfxValue != null) sfxValue.text = Mathf.RoundToInt(gs.Sfx * 100f) + "%";
    }

    void ClearSizeSelection()
    {
        sizeCompact?.RemoveFromClassList("selected");
        sizeStandard?.RemoveFromClassList("selected");
        sizeVast?.RemoveFromClassList("selected");
        sizeCustom?.RemoveFromClassList("selected");
    }

    void SelectPreset(Vector2Int size, string label, Button active)
    {
        usingCustom = false;
        selectedSize = size;
        selectedLabel = label;
        SetCustomRowVisible(false);
        ClearSizeSelection();
        active?.AddToClassList("selected");
        UpdateSummary();
    }

    void SelectCustom()
    {
        usingCustom = true;
        SetCustomRowVisible(true);
        ClearSizeSelection();
        sizeCustom?.AddToClassList("selected");
        ReadCustomIntoSelection();
        UpdateSummary();
    }

    void OnCustomChanged(ChangeEvent<int> evt)
    {
        if (!usingCustom) return;
        ReadCustomIntoSelection();
        UpdateSummary();
    }

    void ReadCustomIntoSelection()
    {
        int c = customCols != null ? customCols.value : customDefault.x;
        int r = customRows != null ? customRows.value : customDefault.y;
        c = Mathf.Clamp(c, customMin.x, customMax.x);
        r = Mathf.Clamp(r, customMin.y, customMax.y);
        if (customCols != null && customCols.value != c) customCols.SetValueWithoutNotify(c);
        if (customRows != null && customRows.value != r) customRows.SetValueWithoutNotify(r);
        selectedSize = new Vector2Int(c, r);
        selectedLabel = "CUSTOM";
    }

    void UpdateSummary()
    {
        if (sizeSummary == null) return;
        sizeSummary.text = $"{selectedLabel}  ·  {selectedSize.x} × {selectedSize.y}";
    }

    void SetCustomRowVisible(bool visible)
    {
        if (customSizeRow == null) return;
        if (visible) customSizeRow.RemoveFromClassList("hidden");
        else customSizeRow.AddToClassList("hidden");
    }

    IEnumerator OpenIrisThenShowSetup()
    {
        irisBusy = true;
        if (irisLayer != null) irisLayer.RemoveFromClassList("hidden");
        if (irisCircle != null)
        {
            irisCircle.style.transitionDuration = new List<TimeValue> { new TimeValue(0f) };
            irisCircle.RemoveFromClassList("open-done");
            irisCircle.AddToClassList("open-start");
            irisCircle.style.scale = new Scale(new Vector3(90f, 90f, 1f));
            yield return null;
            irisCircle.style.transitionDuration = new List<TimeValue>
            {
                new TimeValue(irisOpenDuration * 1000f, TimeUnit.Millisecond)
            };
            irisCircle.RemoveFromClassList("open-start");
            irisCircle.AddToClassList("open-done");
            irisCircle.style.scale = new Scale(new Vector3(0.01f, 0.01f, 1f));
        }

        yield return new WaitForSecondsRealtime(irisOpenDuration);

        if (irisLayer != null) irisLayer.AddToClassList("hidden");
        irisBusy = false;
    }

    void Deploy()
    {
        if (setupDone || irisBusy) return;

        if (usingCustom)
            ReadCustomIntoSelection();

        if (island == null) island = FindAnyObjectByType<ExpandingIsland>();
        if (grid == null) grid = GridManager.Instance ?? FindAnyObjectByType<GridManager>();

        int w = Mathf.Max(1, selectedSize.x);
        int h = Mathf.Max(1, selectedSize.y);

        if (grid != null)
            grid.SetSize(w, h);

        if (island != null)
        {
            island.ConfigureSize(w, h);
            island.BeginGeneration();
        }
        else
            Debug.LogError("RunOverlayUI: no ExpandingIsland in scene.");

        setupDone = true;
        if (worldSetup != null) worldSetup.AddToClassList("hidden");
    }

    void TogglePause() => SetPaused(!paused);

    void SetPaused(bool value)
    {
        if (!setupDone) return;
        paused = value;
        Time.timeScale = paused ? 0f : 1f;
        SetPauseVisible(paused);
        if (!paused)
            GameSettings.Instance?.Save();
    }

    void SetPauseVisible(bool visible)
    {
        if (pauseOverlay == null) return;
        if (visible) pauseOverlay.RemoveFromClassList("hidden");
        else pauseOverlay.AddToClassList("hidden");
    }

    void QuitToMenu()
    {
        Time.timeScale = 1f;
        GameSettings.Instance?.Save();
        if (string.IsNullOrEmpty(mainMenuSceneName))
        {
            Debug.LogError("RunOverlayUI: set mainMenuSceneName.");
            return;
        }
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
