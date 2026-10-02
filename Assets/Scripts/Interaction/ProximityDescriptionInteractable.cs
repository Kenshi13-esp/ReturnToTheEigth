using ReturnToTheEigth.Core;
using UnityEngine;

namespace ReturnToTheEigth.Interaction
{
    /// <summary>Provides a proximity-only narrative description without performing an interaction.</summary>
    [RequireComponent(typeof(BoxCollider2D))]
    [DisallowMultipleComponent]
    public sealed class ProximityDescriptionInteractable : InteractableBase, IProximityDescription
    {
        private const string NoPuzzleCompletionGate = "";

        [SerializeField, TextArea] private string proximityDescription;
        [SerializeField] private string puzzleIdToHideAfterCompletion = NoPuzzleCompletionGate;

        /// <summary>Gets the narrative description shown while the player is near this object.</summary>
        public string ProximityDescription
        {
            get
            {
                GameManager gameManager = GameManager.Instance;
                if (!string.IsNullOrWhiteSpace(puzzleIdToHideAfterCompletion)
                    && gameManager != null
                    && gameManager.IsPuzzleCompleted(puzzleIdToHideAfterCompletion))
                {
                    return string.Empty;
                }

                return proximityDescription;
            }
        }

        /// <summary>Returns the configured narrative description as this object's interaction prompt.</summary>
        public override string InteractionPrompt => ProximityDescription;

        /// <summary>Does nothing because this object only provides proximity feedback.</summary>
        public override void Interact(GameObject interactor) { }
    }
}
