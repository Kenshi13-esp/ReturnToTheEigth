using UnityEngine;

namespace ReturnToTheEigth.Interaction
{
    /// <summary>Provides a proximity-only narrative description without performing an interaction.</summary>
    [RequireComponent(typeof(BoxCollider2D))]
    [DisallowMultipleComponent]
    public sealed class ProximityDescriptionInteractable : InteractableBase, IProximityDescription
    {
        [SerializeField, TextArea] private string proximityDescription;

        /// <summary>Gets the narrative description shown while the player is near this object.</summary>
        public string ProximityDescription => proximityDescription;

        /// <summary>Returns the configured narrative description as this object's interaction prompt.</summary>
        public override string InteractionPrompt => proximityDescription;

        /// <summary>Does nothing because this object only provides proximity feedback.</summary>
        public override void Interact(GameObject interactor) { }
    }
}
