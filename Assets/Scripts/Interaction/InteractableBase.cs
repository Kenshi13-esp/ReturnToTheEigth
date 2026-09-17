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
}
