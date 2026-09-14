using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(PanelRenderer))]
public class HandManager : MonoBehaviour
{
    [SerializeField] private CardDatabase cardDatabase;
    [SerializeField] private RarityWeightConfig rarityWeightConfig;

    [SerializeField] OrbitCamera orbitCamera;
    [SerializeField] bool dealOpeningHandAfterIntro = true;
    bool openingHandDealt;

    private System.Random rng;

    private PanelRenderer panelRenderer;
    private int uiVersion = -1;
    private Button drawPileButton;   // "deck-top"
    private Button discardPileButton; // "discard-top"
    private VisualElement drawPileElement;   // "deck-top"
    private VisualElement discardPileElement; // "discard-top"
    private readonly VisualElement[] slotElements = new VisualElement[5]; // Slot1..Slot5
    private readonly Button[] cardButtons = new Button[5];                // card1..card5

    private List<CardData> drawPile = new List<CardData>();
    private List<CardData> discardPile = new List<CardData>();
    private CardData[] hand = new CardData[5];

    // Remaining stack count per hand slot (mirrors CardData.amount at draw
    // time, decremented by ConsumeSelectedCard as the card is placed).
    private readonly int[] handAmounts = new int[5];

    // Cards currently eligible to be drawn (already unlocked). Used so
    // RefreshUnlockedCards doesn't re-add cards already in the pool.
    private readonly HashSet<CardData> availableCards = new HashSet<CardData>();

    private int selectedIndex = -1;

    // Raised whenever the selected hand card changes (index -1 = deselected).
    // GridBuildPlacer listens to this to know which card's prefab to build.
    public event System.Action<int, CardData> SelectionChanged;

    public int SelectedIndex => selectedIndex;
    public CardData SelectedCard => (selectedIndex >= 0 && selectedIndex < hand.Length) ? hand[selectedIndex] : null;

    // Runtime copy of the configured weights so gameplay upgrades can modify
    // odds (e.g. boost Legendary chance) without touching the shared asset.
    private Dictionary<CardRarity, float> runtimeWeights;

    private void Awake()
    {
        if (RunSeed.Instance == null)
        {
            Debug.LogError("HandManager needs a RunSeed in the scene.");
            rng = new System.Random();
        }
        else
        {
            rng = RunSeed.Instance.CreateRng("hand");
        }

        runtimeWeights = rarityWeightConfig != null
            ? rarityWeightConfig.ToRuntimeDictionary()
            : new Dictionary<CardRarity, float>();

        // Seed the draw pile with only the cards the player has unlocked so
        // far (score-gated, persisted via CardUnlocks/PlayerPrefs). Each
        // card can contribute more than one copy to the pile, per
        // CardData.copiesInDeck.
        drawPile = new List<CardData>();
        if (cardDatabase != null)
        {
            foreach (var card in cardDatabase.allCards)
            {
                if (card == null) continue;
                if (CardUnlocks.IsUnlocked(card))
                {
                    int copies = Mathf.Max(1, card.copiesInDeck);
                    for (int i = 0; i < copies; i++)
                        drawPile.Add(card);
                    availableCards.Add(card);
                }
            }
        }
        ShuffleInPlace(drawPile);
    }

    // Called (e.g. from GameController.OnEnemyDied) whenever score changes,
    // to add any newly-affordable cards into the draw pile and persist their
    // unlock so they stay unlocked in future runs.
    public void RefreshUnlockedCards(int currentScore)
    {
        if (cardDatabase == null) return;

        bool added = false;
        foreach (var card in cardDatabase.allCards)
        {
            if (card == null || availableCards.Contains(card)) continue;
            if (card.unlockScore <= 0 || card.unlockScore > currentScore) continue;

            CardUnlocks.Unlock(card);
            availableCards.Add(card);
            int copies = Mathf.Max(1, card.copiesInDeck);
            for (int i = 0; i < copies; i++)
                drawPile.Add(card);
            added = true;
        }

        if (added)
        {
            ShuffleInPlace(drawPile);
            UpdatePileCounts();
        }
    }

    private void OnEnable()
    {
        panelRenderer = GetComponent<PanelRenderer>();
        if (panelRenderer == null)
        {
            Debug.LogError("HandManager needs a Panel Renderer on this GameObject.");
            return;
        }
        panelRenderer.RegisterUIReloadCallback(OnUIReload);

        if (orbitCamera != null)
            orbitCamera.OnIntroFinished += DealOpeningHand;

        if (!dealOpeningHandAfterIntro)
            DealOpeningHand();
    }

    private void OnDisable()
    {
        if (panelRenderer != null)
            panelRenderer.UnregisterUIReloadCallback(OnUIReload);
        if (orbitCamera != null)
            orbitCamera.OnIntroFinished -= DealOpeningHand;
    }

    private void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
    {
        if (version == uiVersion)
            return;
        uiVersion = version;
        BindUiElements(root);

        if (openingHandDealt)
        {
            ApplyCurrentHandToUi();
            UpdatePileCounts();
        }
        else
        {
            HideHandSlots();
            UpdatePileCounts();
        }
    }
    void HideHandSlots()
    {
        for (int i = 0; i < cardButtons.Length; i++)
        {
            if (cardButtons[i] == null) continue;
            cardButtons[i].AddToClassList("hidden");
            cardButtons[i].Q<Label>("amount-label")?.AddToClassList("amount-hidden");
        }
    }

    void DealOpeningHand()
    {
        if (openingHandDealt) return;
        openingHandDealt = true;
        StartNewHand();
    }

    private void BindUiElements(VisualElement root)
    {
        drawPileButton = root.Q<Button>("deck-top");
        discardPileButton = root.Q<Button>("discard-top");
        drawPileElement = drawPileButton;
        discardPileElement = discardPileButton;

        for (int i = 0; i < 5; i++)
        {
            slotElements[i] = root.Q<VisualElement>("Slot" + (i + 1));
            cardButtons[i] = root.Q<Button>("card" + (i + 1));

            if (cardButtons[i] != null)
            {
                int index = i;
                cardButtons[i].clicked += () => OnCardClicked(index);
            }
        }
    }

    private void OnCardClicked(int index)
    {
        ToggleSelect(index);
    }

    // Updates the deck/discard pile buttons' text to show how many cards
    // are currently in each pile.
    private void UpdatePileCounts()
    {
        if (drawPileButton != null)
            drawPileButton.text = drawPile.Count.ToString();
        if (discardPileButton != null)
            discardPileButton.text = discardPile.Count.ToString();
    }

    // Selects the given hand slot (used by both card clicks and the 1-5 key
    // bindings). Clicking/pressing the already-selected slot deselects it.
    public void ToggleSelect(int index)
    {
        if (index < 0 || index >= hand.Length || hand[index] == null)
            return;

        if (selectedIndex == index)
        {
            Deselect();
            return;
        }

        selectedIndex = index;
        ApplySelectedVisuals();
        SelectionChanged?.Invoke(selectedIndex, SelectedCard);
    }

    public void Deselect()
    {
        if (selectedIndex == -1)
            return;

        selectedIndex = -1;
        ApplySelectedVisuals();
        SelectionChanged?.Invoke(-1, null);
    }

    private void ApplySelectedVisuals()
    {
        for (int i = 0; i < cardButtons.Length; i++)
        {
            if (cardButtons[i] == null)
                continue;

            if (i == selectedIndex)
                cardButtons[i].AddToClassList("selected");
            else
                cardButtons[i].RemoveFromClassList("selected");
        }
    }

    // Called by GridBuildPlacer once the selected card has been successfully
    // placed in the world. Decrements the card's remaining stack amount; if
    // any amount is left, the card stays selected/in-hand (so the player can
    // keep placing it) and only its amount label is updated. Once the stack
    // reaches 0 it's removed from the hand, sent to the discard pile, plays
    // the fly-out animation, and the selection is cleared.
    public void ConsumeSelectedCard()
    {
        if (selectedIndex < 0 || selectedIndex >= hand.Length)
            return;

        int index = selectedIndex;
        CardData card = hand[index];
        if (card == null)
        {
            Deselect();
            return;
        }

        handAmounts[index] = Mathf.Max(0, handAmounts[index] - 1);

        if (handAmounts[index] > 0)
        {
            // Still has copies left - keep it selected/in-hand and just
            // refresh the amount label.
            UpdateAmountLabel(index, card);
            return;
        }

        hand[index] = null;
        discardPile.Add(card);
        selectedIndex = -1;

        Button button = cardButtons[index];
        if (button != null)
        {
            button.RemoveFromClassList("selected");

            if (slotElements[index] != null && discardPileElement != null)
                CardFlyIn.PlayFlyOut(button, slotElements[index], discardPileElement, onComplete: null);
            else
                button.AddToClassList("hidden");
        }

        UpdatePileCounts();
        SelectionChanged?.Invoke(-1, null);
    }

    // Draws a single card, reshuffling the discard pile into the draw pile
    // only when the draw pile is empty (per design: no per-wave reset).
    private CardData DrawOne()
    {
        if (drawPile.Count == 0)
        {
            ReshuffleDiscardIntoDrawPile();
        }

        if (drawPile.Count == 0)
        {
            // Nothing left to draw anywhere (draw pile and discard both empty).
            return null;
        }

        CardData picked = PickWeightedRandom(drawPile);
        drawPile.Remove(picked);
        UpdatePileCounts();
        return picked;
    }

    private void ReshuffleDiscardIntoDrawPile()
    {
        drawPile.AddRange(discardPile);
        discardPile.Clear();
        ShuffleInPlace(drawPile);
        UpdatePileCounts();
    }

    private void ShuffleInPlace(List<CardData> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private CardData PickWeightedRandom(List<CardData> candidates)
    {
        float totalWeight = 0f;
        foreach (var card in candidates)
        {
            totalWeight += GetRuntimeWeight(card.rarity);
        }

        if (totalWeight <= 0f)
        {
            // No usable weights configured - fall back to a uniform pick.
            return candidates[rng.Next(candidates.Count)];
        }

        float roll = (float)(rng.NextDouble() * totalWeight);
        float cumulative = 0f;
        foreach (var card in candidates)
        {
            cumulative += GetRuntimeWeight(card.rarity);
            if (roll <= cumulative)
                return card;
        }

        // Fallback for floating point edge cases.
        return candidates[candidates.Count - 1];
    }

    private float GetRuntimeWeight(CardRarity rarity)
    {
        return runtimeWeights.TryGetValue(rarity, out float weight) ? weight : 0f;
    }

    // Called at the start of a wave: discards whatever is left in hand, then
    // draws 5 fresh cards, binding each onto the existing Slot1-5 card UI.
    public void StartNewHand()
    {
        Deselect();
        DiscardHand();

        for (int i = 0; i < hand.Length; i++)
        {
            CardData card = DrawOne();
            hand[i] = card;
            handAmounts[i] = card != null ? Mathf.Max(1, card.amount) : 0;
            BindCardToSlot(i, card, playFlyIn: true);
        }

        UpdatePileCounts();
    }

    // Called when the wave button is pressed: any cards still in hand fly
    // out to the discard pile (Slay the Spire-style end-of-turn discard),
    // then get hidden/reset once their animation finishes.
    public void DiscardCurrentHand()
    {
        Deselect();
        for (int i = 0; i < hand.Length; i++)
        {
            CardData card = hand[i];
            if (card == null)
                continue;

            discardPile.Add(card);
            hand[i] = null;
            handAmounts[i] = 0;

            Button button = cardButtons[i];
            if (button == null || slotElements[i] == null || discardPileElement == null)
                continue;

            CardFlyIn.PlayFlyOut(button, slotElements[i], discardPileElement, onComplete: null);
        }

        UpdatePileCounts();
    }

    // Re-applies whatever is currently in `hand` to the bound UI elements
    // without drawing/discarding or playing the fly-in animation. Used after
    // a panel reload so the hand stays visible if it was drawn before the
    // panel finished loading (e.g. the initial hand in OnEnable).
    private void ApplyCurrentHandToUi()
    {
        for (int i = 0; i < hand.Length; i++)
        {
            BindCardToSlot(i, hand[i], playFlyIn: false);
        }
    }

    private static readonly string[] AllRarityCssClasses =
    {
        "rarity-common", "rarity-uncommon", "rarity-rare", "rarity-epic", "rarity-legendary"
    };

    private void BindCardToSlot(int index, CardData card, bool playFlyIn)
    {
        Button button = cardButtons[index];
        if (button == null)
            return;

        var titleLabel = button.Q<Label>("title");

        if (card == null)
        {
            // No card left to draw - use the .card.hidden USS class (snaps
            // opacity/translate instantly) rather than display:none.
            button.AddToClassList("hidden");
            var emptyAmountLabel = button.Q<Label>("amount-label");
            emptyAmountLabel?.AddToClassList("amount-hidden");
            return;
        }

        button.RemoveFromClassList("hidden");
        if (titleLabel != null)
            titleLabel.text = card.title;

        foreach (var cssClass in AllRarityCssClasses)
            button.RemoveFromClassList(cssClass);
        button.AddToClassList(card.RarityCssClass);

        UpdateAmountLabel(index, card);

        // TODO: populate "art" once card art is available.

        if (playFlyIn && drawPileElement != null && slotElements[index] != null)
            CardFlyIn.PlayFlyIn(button, drawPileElement, slotElements[index]);
    }

    private void DiscardHand()
    {
        for (int i = 0; i < hand.Length; i++)
        {
            if (hand[i] != null)
                discardPile.Add(hand[i]);

            hand[i] = null;
            handAmounts[i] = 0;
        }
    }

    // Shows/hides the "amount-label" child of a card button based on the
    // slot's remaining stack count, toggling the "amount-hidden" USS class
    // so visibility is fully controlled from Layout.uss.
    private void UpdateAmountLabel(int index, CardData card)
    {
        Button button = cardButtons[index];
        if (button == null)
            return;

        var amountLabel = button.Q<Label>("amount-label");
        if (amountLabel == null)
            return;

        int amount = handAmounts[index];
        if (card == null || amount <= 1)
        {
            amountLabel.AddToClassList("amount-hidden");
        }
        else
        {
            amountLabel.RemoveFromClassList("amount-hidden");
            amountLabel.text = amount.ToString();
        }
    }
}
