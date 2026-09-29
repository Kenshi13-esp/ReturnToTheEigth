using UnityEngine;

namespace ReturnToTheEigth.Interaction
{
    /// <summary>Common contract for doors, notes, levers, keys, and puzzles.</summary>
    public abstract class InteractableBase : MonoBehaviour
    {
        public abstract string InteractionPrompt { get; }

        /// <summary>Executes this object's interaction for the supplied player.</summary>
        public abstract void Interact(GameObject interactor);
    }

    /// <summary>Optional narrative text displayed while the player is near an interactable.</summary>
    public interface IProximityDescription
    {
        /// <summary>Gets the narrative description shown while the player is nearby.</summary>
        string ProximityDescription { get; }
    }

    /// <summary>Optional visual feedback hook for interactables whose visible target is not a child sprite.</summary>
    public interface IInteractionHighlightTarget
    {
        /// <summary>Shows or clears this interactable's selected proximity feedback.</summary>
        void SetInteractionHighlighted(bool highlighted);
    }
}
