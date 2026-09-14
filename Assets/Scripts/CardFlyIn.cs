using System;
using UnityEngine;
using UnityEngine.UIElements;

// Fly animations for hand cards, based on the prototyped approach:
// delta = pile.worldBound.center - slotCard.worldBound.center
// card.style.translate = delta   (no transition, via the .in-flight class)
// next frame: card.style.translate = (0,0)   (transition on, .in-flight removed)
//
// Wired from HandManager.BindCardToSlot() (fly-in from the draw pile) and
// HandManager.DiscardCurrentHand() (fly-out to the discard pile).
public static class CardFlyIn
{
    // Card appears to "fly in" from the draw pile into its hand slot.
    public static void PlayFlyIn(VisualElement card, VisualElement drawPile, VisualElement slot)
    {
        if (card == null || drawPile == null || slot == null)
            return;

        Vector2 delta = drawPile.worldBound.center - slot.worldBound.center;

        // Snap to the offset instantly (no transition) via .in-flight.
        card.AddToClassList("in-flight");
        card.style.translate = new Translate(delta.x, delta.y);

        // Next frame: remove .in-flight (restores the .card transition) and
        // animate back to (0,0) - this is the actual "flying in" motion.
        card.schedule.Execute(() =>
        {
            card.RemoveFromClassList("in-flight");
            card.style.translate = new Translate(0, 0);
        }).StartingIn(0);
    }

    // Card animates from its hand slot to the discard pile, then calls
    // onComplete once the transition has finished (so the caller can hide
    // it / reset it for the next draw).
    public static void PlayFlyOut(VisualElement card, VisualElement slot, VisualElement discardPile, Action onComplete)
    {
        if (card == null || slot == null || discardPile == null)
        {
            onComplete?.Invoke();
            return;
        }

        Vector2 delta = discardPile.worldBound.center - slot.worldBound.center;

        // Make sure the normal .card transition is active, then animate
        // towards the discard pile.
        card.RemoveFromClassList("in-flight");
        card.style.translate = new Translate(delta.x, delta.y);

        // Wait for the .card transition-duration (0.35s) to finish, then
        // snap back to (0,0) and hide, ready for the next draw.
        card.schedule.Execute(() =>
        {
            card.AddToClassList("in-flight"); // suppress transition for the snap-back
            card.style.translate = new Translate(0, 0);
            card.AddToClassList("hidden");
            onComplete?.Invoke();
        }).StartingIn(360);
    }
}
