using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;
using static UnityEngine.Android.AndroidBuild;

public class WaveButtonUI : MonoBehaviour
{
    Button button;
    Label impossibleLabel;
    GameController gameController;
    HandManager handManager;
    PanelRenderer panelRenderer;
    int uiVersion = -1;

    [SerializeField] OrbitCamera orbitCamera;
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
    }

    void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
    {
        if (version == uiVersion)
            return;

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
    }
    void UnbindButton()
    {
        if (button != null)
        {
            button.clicked -= OnWaveClicked;
            button = null;
        }
    }
    private void Update()
    {
        if (gameController == null) return;
        if (introDone && gameController.IsBetweenWaves)
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
        if (blocked)
            button.AddToClassList("disabled");
        else
            button.RemoveFromClassList("disabled");
        button.style.opacity = blocked ? 0.4f : 1f;
        button.pickingMode = blocked ? PickingMode.Ignore : PickingMode.Position;
    }

    void OnWaveClicked()
    {
        if (gameController != null && gameController.IsBetweenWaves && gameController.IsExitReachable)
        {
            handManager?.DiscardCurrentHand();
            gameController.SetIsBetweenWaves(false);
            PlayExit();
        }
    }

    void PlayExit()
    {
        if (button == null) return;
        button.pickingMode = PickingMode.Ignore;
        button.SetEnabled(false);
        button.RemoveFromClassList("exit-down");
        button.RemoveFromClassList("exit-up");
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
        button.RemoveFromClassList("exit-up");
        button.RemoveFromClassList("exit-down");
        button.pickingMode = PickingMode.Position;
        button.SetEnabled(true);
    }
}