using ReturnToTheEigth.Core;
using ReturnToTheEigth.Player;
using ReturnToTheEigth.TimeTravel;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ReturnToTheEigth.Interaction
{
    /// <summary>Makes a sprite asset a reusable nearby puzzle entrance with an outline that appears on approach.</summary>
    [DisallowMultipleComponent]
    public sealed class PuzzleAssetInteractable : InteractableBase
    {
        private const string DefaultPrompt = "Entrar al puzle";
        private const string DefaultPuzzleSceneName = "ColorTrackPuzzle";
        private const string DefaultPuzzleId = "ColorTrackPuzzle";
        private const string OutlineShaderName = "Universal Render Pipeline/2D/Sprite-Unlit-Default";
        private const float DefaultInteractionRadius = 0.3f;
        private const float DefaultOutlineWidth = 0.02f;
        private const float DefaultTriggerPadding = 0.1f;
        private const int DefaultOutlineSortingOrder = 21;
        private const int OutlinePointCount = 5;
        private const float Zero = 0f;
        private static readonly Color DefaultOutlineColor = Color.white;

        [SerializeField] private PlayerInteraction playerInteraction;
        [SerializeField] private string sceneToLoad = DefaultPuzzleSceneName;
        [SerializeField] private string puzzleId = DefaultPuzzleId;
        [SerializeField] private string interactionPrompt = DefaultPrompt;
        [SerializeField, Min(Zero)] private float interactionRadius = DefaultInteractionRadius;
        [SerializeField, Min(Zero)] private float outlineWidth = DefaultOutlineWidth;
        [SerializeField, Min(Zero)] private float triggerPadding = DefaultTriggerPadding;
        [SerializeField] private int outlineSortingOrder = DefaultOutlineSortingOrder;
        [SerializeField] private Color outlineColor = DefaultOutlineColor;

        private SpriteRenderer targetRenderer;
        private BoxCollider2D interactionTrigger;
        private LineRenderer outlineRenderer;
        private Material runtimeOutlineMaterial;
        private bool isNearPlayer;
        private bool isConfigured;

        /// <summary>Gets the prompt displayed when the player is close enough to enter this puzzle.</summary>
        public override string InteractionPrompt => string.IsNullOrWhiteSpace(interactionPrompt)
            ? DefaultPrompt : interactionPrompt;

        /// <summary>Configures this asset as a puzzle entrance; use a unique, stable puzzle identifier per puzzle.</summary>
        public void Configure(string targetScene, string stablePuzzleId, PlayerInteraction player)
        {
            sceneToLoad = targetScene;
            puzzleId = stablePuzzleId;
            playerInteraction = player;
            InitializeAssetInteraction();
        }

        /// <summary>Assigns the player used for proximity checks without changing this asset's puzzle configuration.</summary>
        public void BindPlayer(PlayerInteraction player)
        {
            playerInteraction = player;
            InitializeAssetInteraction();
        }

        /// <summary>Overrides the prompt shown while the player can interact with this asset.</summary>
        public void SetInteractionPrompt(string prompt)
        {
            interactionPrompt = prompt;
        }

        private void Awake()
        {
            InitializeAssetInteraction();
        }

        private void Update()
        {
            if (!isConfigured || !isActiveAndEnabled)
            {
                return;
            }

            if (playerInteraction == null)
            {
                playerInteraction = FindAnyObjectByType<PlayerInteraction>();
            }

            bool isPuzzleCompleted = GameManager.Instance != null
                && GameManager.Instance.IsPuzzleCompleted(puzzleId);
            if (isPuzzleCompleted)
            {
                DisablePuzzleEntrance();
                return;
            }

            Bounds spriteBounds = targetRenderer.bounds;
            Vector3 playerPosition = playerInteraction != null
                ? playerInteraction.transform.position : Vector3.positiveInfinity;
            playerPosition.z = spriteBounds.center.z;
            bool shouldShowOutline = playerInteraction != null
                && spriteBounds.SqrDistance(playerPosition) <= interactionRadius * interactionRadius;
            SetOutlineVisible(shouldShowOutline);
        }

        private void OnDestroy()
        {
            if (runtimeOutlineMaterial != null)
            {
                Destroy(runtimeOutlineMaterial);
            }
        }

        /// <summary>Stores the exploration position and timeline, then loads the configured puzzle scene.</summary>
        public override void Interact(GameObject interactor)
        {
            if (!isConfigured || interactor == null || string.IsNullOrWhiteSpace(sceneToLoad)
                || (GameManager.Instance != null && GameManager.Instance.IsPuzzleCompleted(puzzleId)))
            {
                return;
            }

            TimelineEra returnEra = FindAnyObjectByType<TimeTravelManager>()?.CurrentEra ?? TimelineEra.Present;
            GameManager.Instance?.SetPendingPlayerReturnState(interactor.transform.position, returnEra);
            SceneManager.LoadSceneAsync(sceneToLoad, LoadSceneMode.Single);
        }

        private void InitializeAssetInteraction()
        {
            if (isConfigured)
            {
                UpdateTriggerAndOutlineBounds();
                ApplyCompletionState();
                return;
            }

            targetRenderer = GetComponent<SpriteRenderer>();
            if (targetRenderer == null)
            {
                targetRenderer = GetComponentInChildren<SpriteRenderer>(true);
            }

            if (targetRenderer == null)
            {
                return;
            }

            if (playerInteraction == null)
            {
                playerInteraction = FindAnyObjectByType<PlayerInteraction>();
            }

            interactionTrigger = GetComponent<BoxCollider2D>();
            if (interactionTrigger == null)
            {
                interactionTrigger = gameObject.AddComponent<BoxCollider2D>();
            }
            interactionTrigger.isTrigger = true;

            outlineRenderer = GetComponent<LineRenderer>();
            if (outlineRenderer == null)
            {
                outlineRenderer = gameObject.AddComponent<LineRenderer>();
            }

            ConfigureOutlineRenderer();
            isConfigured = true;
            UpdateTriggerAndOutlineBounds();
            ApplyCompletionState();
        }

        private void ConfigureOutlineRenderer()
        {
            outlineRenderer.useWorldSpace = true;
            outlineRenderer.loop = false;
            outlineRenderer.positionCount = OutlinePointCount;
            outlineRenderer.startWidth = outlineWidth;
            outlineRenderer.endWidth = outlineWidth;
            outlineRenderer.sortingLayerID = targetRenderer.sortingLayerID;
            outlineRenderer.sortingOrder = outlineSortingOrder;
            outlineRenderer.startColor = outlineColor;
            outlineRenderer.endColor = outlineColor;

            Shader outlineShader = Shader.Find(OutlineShaderName);
            if (outlineShader != null)
            {
                runtimeOutlineMaterial = new Material(outlineShader);
                outlineRenderer.material = runtimeOutlineMaterial;
            }

            outlineRenderer.enabled = false;
        }

        private void UpdateTriggerAndOutlineBounds()
        {
            if (targetRenderer == null || interactionTrigger == null || outlineRenderer == null)
            {
                return;
            }

            Bounds spriteBounds = targetRenderer.bounds;
            Vector3 lowerLeft = new Vector3(spriteBounds.min.x, spriteBounds.min.y, transform.position.z);
            Vector3 upperLeft = new Vector3(spriteBounds.min.x, spriteBounds.max.y, transform.position.z);
            Vector3 upperRight = new Vector3(spriteBounds.max.x, spriteBounds.max.y, transform.position.z);
            Vector3 lowerRight = new Vector3(spriteBounds.max.x, spriteBounds.min.y, transform.position.z);
            outlineRenderer.positionCount = OutlinePointCount;
            outlineRenderer.SetPosition(0, lowerLeft);
            outlineRenderer.SetPosition(1, upperLeft);
            outlineRenderer.SetPosition(2, upperRight);
            outlineRenderer.SetPosition(3, lowerRight);
            outlineRenderer.SetPosition(4, lowerLeft);

            Vector3 localCenter = transform.InverseTransformPoint(spriteBounds.center);
            Vector3 localSize = transform.InverseTransformVector(spriteBounds.size);
            interactionTrigger.offset = new Vector2(localCenter.x, localCenter.y);
            interactionTrigger.size = new Vector2(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y))
                + Vector2.one * triggerPadding;
        }

        private void ApplyCompletionState()
        {
            bool isPuzzleCompleted = GameManager.Instance != null
                && GameManager.Instance.IsPuzzleCompleted(puzzleId);
            if (isPuzzleCompleted)
            {
                DisablePuzzleEntrance();
            }
        }

        private void DisablePuzzleEntrance()
        {
            if (interactionTrigger != null)
            {
                interactionTrigger.enabled = false;
            }
            SetOutlineVisible(false);
            enabled = false;
        }

        private void SetOutlineVisible(bool visible)
        {
            if (outlineRenderer == null || isNearPlayer == visible)
            {
                return;
            }

            isNearPlayer = visible;
            outlineRenderer.enabled = visible;
        }
    }
}
