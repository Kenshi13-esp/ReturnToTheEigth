using ReturnToTheEigth.Core;
using ReturnToTheEigth.Player;
using ReturnToTheEigth.TimeTravel;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ReturnToTheEigth.Interaction
{
    /// <summary>Loads the colour-track puzzle when the player interacts with its highlighted square.</summary>
    [ExecuteAlways]
    [RequireComponent(typeof(BoxCollider2D), typeof(LineRenderer))]
    [DisallowMultipleComponent]
    public sealed class ColorPuzzlePortalInteractable : InteractableBase
    {
        private const string PuzzleSceneName = "ColorTrackPuzzle";
        private const string DefaultPuzzleId = "ColorTrackPuzzle";
        private const string InteractionPromptText = "Entrar al puzle de colores";
        private const string SpriteShaderName = "Universal Render Pipeline/2D/Sprite-Unlit-Default";
        private const float HalfExtent = 0.3f;
        private const float HighlightRadius = 0.9f;
        private const float HighlightRadiusSquared = HighlightRadius * HighlightRadius;
        private const float OutlineWidth = 0.04f;
        private const int OutlineSortingOrder = 10;
        private static readonly Color NormalOutlineColor = new Color(0.25f, 0.85f, 0.95f, 0.9f);
        private static readonly Color HighlightedOutlineColor = new Color(1f, 0.88f, 0.2f, 1f);

        [SerializeField] private PlayerInteraction playerInteraction;
        [SerializeField] private string puzzleId = DefaultPuzzleId;

        private BoxCollider2D interactionCollider;
        private LineRenderer outlineRenderer;
        private Material runtimeOutlineMaterial;
        private bool isHighlighted;

        /// <summary>Gets the prompt shown while the player can use this puzzle entrance.</summary>
        public override string InteractionPrompt => InteractionPromptText;

        private void OnValidate()
        {
            if (outlineRenderer == null)
            {
                outlineRenderer = GetComponent<LineRenderer>();
            }

            if (outlineRenderer != null)
            {
                ConfigureOutline();
            }
        }

        private void Awake()
        {
            interactionCollider = GetComponent<BoxCollider2D>();
            interactionCollider.isTrigger = true;
            interactionCollider.size = new Vector2(HalfExtent * 2f, HalfExtent * 2f);

            outlineRenderer = GetComponent<LineRenderer>();
            ConfigureOutline();
            if (playerInteraction == null)
            {
                playerInteraction = FindAnyObjectByType<PlayerInteraction>();
            }

            Shader outlineShader = Shader.Find(SpriteShaderName);
            if (outlineShader != null)
            {
                runtimeOutlineMaterial = new Material(outlineShader);
                outlineRenderer.material = runtimeOutlineMaterial;
            }

            SetHighlighted(false);
            if (GameManager.Instance != null && GameManager.Instance.IsPuzzleCompleted(puzzleId))
            {
                DisablePortal();
            }
        }

        private void Update()
        {
            if (playerInteraction == null)
            {
                playerInteraction = FindAnyObjectByType<PlayerInteraction>();
            }

            Vector2 playerPosition = playerInteraction != null
                ? playerInteraction.transform.position
                : Vector2.positiveInfinity;
            Vector2 offset = playerPosition - (Vector2)transform.position;
            SetHighlighted(offset.sqrMagnitude <= HighlightRadiusSquared);
        }

        private void OnDestroy()
        {
            if (runtimeOutlineMaterial != null)
            {
                Destroy(runtimeOutlineMaterial);
            }
        }

        /// <summary>Loads the colour-track puzzle scene when the player interacts with this entrance.</summary>
        public override void Interact(GameObject interactor)
        {
            if (interactor == null
                || (GameManager.Instance != null && GameManager.Instance.IsPuzzleCompleted(puzzleId)))
            {
                return;
            }

            TimelineEra returnEra = FindAnyObjectByType<TimeTravelManager>()?.CurrentEra ?? TimelineEra.Present;
            GameManager.Instance?.SetPendingPlayerReturnState(interactor.transform.position, returnEra);
            SceneManager.LoadSceneAsync(PuzzleSceneName, LoadSceneMode.Single);
        }

        private void DisablePortal()
        {
            interactionCollider.enabled = false;
            outlineRenderer.enabled = false;
            enabled = false;
        }

        private void ConfigureOutline()
        {
            outlineRenderer.useWorldSpace = false;
            outlineRenderer.loop = false;
            outlineRenderer.positionCount = 5;
            outlineRenderer.startWidth = OutlineWidth;
            outlineRenderer.endWidth = OutlineWidth;
            outlineRenderer.sortingOrder = OutlineSortingOrder;
            outlineRenderer.startColor = NormalOutlineColor;
            outlineRenderer.endColor = NormalOutlineColor;
            outlineRenderer.SetPosition(0, new Vector3(-HalfExtent, -HalfExtent, 0f));
            outlineRenderer.SetPosition(1, new Vector3(-HalfExtent, HalfExtent, 0f));
            outlineRenderer.SetPosition(2, new Vector3(HalfExtent, HalfExtent, 0f));
            outlineRenderer.SetPosition(3, new Vector3(HalfExtent, -HalfExtent, 0f));
            outlineRenderer.SetPosition(4, new Vector3(-HalfExtent, -HalfExtent, 0f));
        }

        private void SetHighlighted(bool highlighted)
        {
            if (outlineRenderer == null || isHighlighted == highlighted)
            {
                return;
            }

            isHighlighted = highlighted;
            Color outlineColor = highlighted ? HighlightedOutlineColor : NormalOutlineColor;
            outlineRenderer.startColor = outlineColor;
            outlineRenderer.endColor = outlineColor;
            if (runtimeOutlineMaterial != null)
            {
                runtimeOutlineMaterial.color = outlineColor;
            }
        }
    }
}
