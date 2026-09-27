using ReturnToTheEigth.Core;
using ReturnToTheEigth.TimeTravel;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ReturnToTheEigth.Interaction
{
    /// <summary>Loads the colour-track puzzle when the player interacts with the Past-era fire-cooking sprite.</summary>
    [RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
    [DisallowMultipleComponent]
    public sealed class ColorPuzzlePortalInteractable : InteractableBase
    {
        private const string PuzzleSceneName = "ColorTrackPuzzle";
        private const string PuzzleId = "ColorTrackPuzzle";
        private const string InteractionPromptText = "Entrar al puzle de colores";
        private const float DefaultInteractionRadius = 0.9f;
        private const float TriggerPadding = 0.1f;

        /// <summary>Maximum query radius required to find colour puzzle entrances.</summary>
        public const float MaximumInteractionRadius = DefaultInteractionRadius;

        private BoxCollider2D interactionCollider;
        private SpriteRenderer targetRenderer;

        /// <summary>Gets the interaction radius of this entrance.</summary>
        public float InteractionRadius => DefaultInteractionRadius;

        /// <summary>Gets the world-space interaction point at the centre of the visible sprite.</summary>
        public Vector2 InteractionPoint => targetRenderer != null
            ? targetRenderer.bounds.center
            : transform.position;

        /// <summary>Gets the prompt shown while the player can interact with this sprite.</summary>
        public override string InteractionPrompt => InteractionPromptText;

        private void Awake()
        {
            interactionCollider = GetComponent<BoxCollider2D>();
            targetRenderer = GetComponent<SpriteRenderer>();
            interactionCollider.isTrigger = true;
            if (targetRenderer != null && targetRenderer.sprite != null)
            {
                interactionCollider.offset = new Vector2(targetRenderer.sprite.bounds.center.x, targetRenderer.sprite.bounds.center.y);
                interactionCollider.size = new Vector2(
                    targetRenderer.sprite.bounds.size.x + TriggerPadding,
                    targetRenderer.sprite.bounds.size.y + TriggerPadding);
            }
            if (GameManager.Instance != null && GameManager.Instance.IsPuzzleCompleted(PuzzleId))
            {
                DisablePortal();
            }
        }

        /// <summary>Saves the player's position and timeline, then loads the colour-track puzzle.</summary>
        public override void Interact(GameObject interactor)
        {
            if (interactor == null
                || (GameManager.Instance != null && GameManager.Instance.IsPuzzleCompleted(PuzzleId)))
            {
                return;
            }

            TimelineEra returnEra = FindAnyObjectByType<TimeTravelManager>()?.CurrentEra ?? TimelineEra.Past;
            GameManager.Instance?.SetPendingPlayerReturnState(interactor.transform.position, returnEra);
            SceneManager.LoadSceneAsync(PuzzleSceneName, LoadSceneMode.Single);
        }

        private void DisablePortal()
        {
            if (interactionCollider != null) interactionCollider.enabled = false;
            enabled = false;
        }
    }
}
