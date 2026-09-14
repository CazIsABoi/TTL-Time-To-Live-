using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class WaveButtonUI : MonoBehaviour
{
    Button button;
    Label impossibleLabel;
    GameController gameController;
    HandManager handManager;
    PanelRenderer panelRenderer;
    int uiVersion = -1;

    [SerializeField] OrbitCamera orbitCamera;
    [SerializeField] BgmLayers bgm;
    bool introDone;

    void OnEnable()
    {
        gameController = GetComponent<GameController>();
        handManager = GetComponent<HandManager>();
        panelRenderer = GetComponent<PanelRenderer>();

        if (panelRenderer == null)
        {
            Debug.LogError("WaveButtonUI needs a Panel Renderer on this GameObject.");
            return;
        }

        panelRenderer.RegisterUIReloadCallback(OnUIReload);

        introDone = orbitCamera == null;
        if (orbitCamera != null)
            orbitCamera.OnIntroFinished += HandleIntroFinished;
        else
            bgm?.ToBuild();
    }

    void OnDisable()
    {
        if (panelRenderer != null)
            panelRenderer.UnregisterUIReloadCallback(OnUIReload);

        if (button != null)
            button.clicked -= OnWaveClicked;

        if (orbitCamera != null)
            orbitCamera.OnIntroFinished -= HandleIntroFinished;
    }

    void HandleIntroFinished()
    {
        introDone = true;
        bgm?.ToBuild(); // intro clip → build clip on BGM_Build, wave stays silent
        if (gameController != null && gameController.IsBetweenWaves)
            ShowAgain();
    }

    void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
    {
        if (version == uiVersion) return;
        uiVersion = version;
        UnbindButton();

        button = root.Q<Button>("wave-button");
        if (button == null)
        {
            Debug.LogError("No Button named 'wave-button' in Layout.uxml.");
            return;
        }

        button.clicked += OnWaveClicked;
        impossibleLabel = root.Q<Label>("impossible-label");
        if (!introDone) HideForIntro();
    }

    void HideForIntro()
    {
        if (button == null) return;
        button.style.display = DisplayStyle.None;
        button.style.opacity = 0f;
        button.pickingMode = PickingMode.Ignore;
        button.SetEnabled(false);
    }

    void UnbindButton()
    {
        if (button != null)
        {
            button.clicked -= OnWaveClicked;
            button = null;
        }
    }

    void Update()
    {
        if (gameController == null || button == null) return;
        if (!introDone)
        {
            if (button.style.display != DisplayStyle.None)
                HideForIntro();
            return;
        }
        if (gameController.IsBetweenWaves)
            ShowAgain();
        UpdateImpossibleState();
    }

    void UpdateImpossibleState()
    {
        if (impossibleLabel != null)
            impossibleLabel.style.display = gameController.IsExitReachable ? DisplayStyle.None : DisplayStyle.Flex;

        if (button == null) return;

        bool blocked = gameController.IsBetweenWaves && !gameController.IsExitReachable;
        button.SetEnabled(!blocked);
        if (blocked) button.AddToClassList("disabled");
        else button.RemoveFromClassList("disabled");
        button.style.opacity = blocked ? 0.4f : 1f;
        button.pickingMode = blocked ? PickingMode.Ignore : PickingMode.Position;
    }

    void OnWaveClicked()
    {
        if (gameController != null && gameController.IsBetweenWaves && gameController.IsExitReachable)
        {
            handManager?.DiscardCurrentHand();
            gameController.SetIsBetweenWaves(false);
            bgm?.ToWave();
            PlayExit();
        }
    }

    void PlayExit()
    {
        if (button == null) return;
        button.pickingMode = PickingMode.Ignore;
        button.SetEnabled(false);
        button.style.transitionProperty = new List<StylePropertyName> { "translate", "opacity" };
        button.style.transitionDuration = new List<TimeValue> { new TimeValue(450, TimeUnit.Millisecond) };
        button.style.transitionTimingFunction = new List<EasingFunction> { EasingMode.EaseIn };
        button.style.translate = new Translate(0, -160);
        button.style.opacity = 0f;
        button.schedule.Execute(Hide).StartingIn(450);
    }

    void Hide()
    {
        if (button != null)
            button.style.display = DisplayStyle.None;
    }

    public void ShowAgain()
    {
        if (button == null) return;
        button.style.translate = new Translate(0, 0);
        button.style.opacity = 1f;
        button.style.display = DisplayStyle.Flex;
        button.pickingMode = PickingMode.Position;
        button.SetEnabled(true);
    }
}