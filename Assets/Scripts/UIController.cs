using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(PanelRenderer))]
public class UIController : MonoBehaviour
{
    ProgressBar healthBar;
    Label scoreLabel;
    Label waveLabel;
    VisualElement root;
    VisualElement waveButton;
    VisualElement hand;
    VisualElement deckTop;
    VisualElement discardTop;

    [SerializeField] OrbitCamera orbitCamera;

    struct Parked
    {
        public VisualElement e;
        public Translate hidden;
    }

    readonly List<Parked> parked = new();

    void OnEnable()
    {
        GetComponent<PanelRenderer>().RegisterUIReloadCallback(OnUIReload);
        if (orbitCamera != null)
            orbitCamera.OnIntroFinished += ShowHud;
    }

    void OnDisable()
    {
        GetComponent<PanelRenderer>().UnregisterUIReloadCallback(OnUIReload);
        if (orbitCamera != null)
            orbitCamera.OnIntroFinished -= ShowHud;
    }

    void OnUIReload(PanelRenderer renderer, VisualElement visualRoot, int version)
    {
        root = visualRoot;
        healthBar = root.Q<ProgressBar>("health-bar");
        scoreLabel = root.Q<Label>("score-label");
        waveLabel = root.Q<Label>("wave-label");
        waveButton = root.Q(className: "wave-button");
        hand = root.Q(className: "Hand");
        deckTop = root.Q("deck-top");
        discardTop = root.Q("discard-top");

        var healthLabel = root.Q("health-label"); // the one in your screenshot

        Apply(100, 100);
        HideHudImmediate();
        Park(healthLabel, new Translate(0, -160));
    }

    public void HideHudImmediate()
    {
        parked.Clear();
        Park(healthBar, new Translate(-240, 0));
        Park(scoreLabel, new Translate(-240, 0));
        Park(waveLabel, new Translate(-240, 0));
        Park(waveButton, new Translate(0, -160));
        Park(hand, new Translate(0, 160));
        Park(deckTop, new Translate(-240, 160));
        Park(discardTop, new Translate(240, 160));
        root?.Query(className: "hud-panel").ForEach(e => Park(e, Guess(e)));
    }

    public void ShowHud()
    {
        root?.schedule.Execute(ReleaseHud).StartingIn(16);
    }

    void Park(VisualElement e, Translate off)
    {
        if (e == null) return;
        e.style.transitionProperty = new List<StylePropertyName> { "translate", "opacity" };
        e.style.transitionDuration = new List<TimeValue> { new TimeValue(0, TimeUnit.Second) };
        e.style.translate = off;
        e.style.opacity = 0f;
        parked.Add(new Parked { e = e, hidden = off });
    }

    void ReleaseHud()
    {
        for (int i = 0; i < parked.Count; i++)
        {
            var e = parked[i].e;
            if (e == null) continue;
            e.style.transitionDuration = new List<TimeValue> { new TimeValue(450, TimeUnit.Millisecond) };
            e.style.transitionTimingFunction = new List<EasingFunction> { EasingMode.EaseOut };
            e.style.translate = new Translate(0, 0);
            e.style.opacity = 1f;
        }
    }

    static Translate Guess(VisualElement e)
    {
        var r = e.worldBound;
        if (r.yMin < 90f) return new Translate(0, -160);
        if (r.xMin < 90f) return new Translate(-240, 0);
        if (r.xMax > (e.panel?.visualTree.worldBound.width ?? 1920f) - 90f)
            return new Translate(240, 0);
        return new Translate(0, 160);
    }

    public void SetHealth(int current, int max)
    {
        if (healthBar == null) return;
        Apply(current, max);
    }

    public void SetScore(int score)
    {
        if (scoreLabel != null) scoreLabel.text = $"Score: {score}";
    }

    public void SetWave(int wave)
    {
        if (waveLabel != null) waveLabel.text = $"Wave: {wave}";
    }

    void Apply(int current, int max)
    {
        if (healthBar == null) return;
        healthBar.lowValue = 0;
        healthBar.highValue = max;
        healthBar.value = current;
        healthBar.title = $"{current}/{max}";
    }
}