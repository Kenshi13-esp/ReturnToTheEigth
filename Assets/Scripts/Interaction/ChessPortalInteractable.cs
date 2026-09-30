using ReturnToTheEigth.Core;
using ReturnToTheEigth.TimeTravel;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ReturnToTheEigth.Interaction
{
    /// <summary>Loads the chess puzzle from the ChessPortal marker.</summary>
    [RequireComponent(typeof(BoxCollider2D), typeof(LineRenderer))]
    [DisallowMultipleComponent]
    public sealed class ChessPortalInteractable : InteractableBase, IInteractionHighlightTarget
    {
        private const string PuzzleSceneName = "ChessPuzle";
        private const string PuzzleId = "KnightPuzzle";
        private const string InteractionPromptText = "Enter the chess puzzle";
        private const string MissingChessPiecePromptText = "You need the knight piece from the color puzzle";
        private const string ChessAssetObjectName = "Chess";
        private const string PastTableObjectName = "pasttable";
        private const float HalfExtent = 0.3f;
        private const float ChessInteractionRadius = 0.5f;
        private const float HighlightBlend = 0.42f;
        private const int ChessSortingOrderOffset = 2;
        private const int PortalSortingOrderOffset = 1;
        private static readonly Color InteractionHighlightColor = new Color(1f, 0.82f, 0.42f, 1f);

        /// <summary>Maximum query radius required to find chess portals.</summary>
        public const float MaximumInteractionRadius = ChessInteractionRadius;

        private BoxCollider2D interactionCollider;
        private LineRenderer outlineRenderer;
        private SpriteRenderer chessSpriteRenderer;
        private Color originalChessSpriteColor = Color.white;
        private bool hasOriginalChessSpriteColor;

        /// <summary>Gets the interaction radius of the chess portal.</summary>
        public float InteractionRadius => ChessInteractionRadius;

        /// <summary>Gets the world-space interaction point at the bottom-center of the portal square.</summary>
        public Vector2 InteractionPoint => transform.TransformPoint(new Vector3(0f, -HalfExtent, 0f));

        /// <summary>Gets the prompt shown while the player can interact with the chess portal.</summary>
        public override string InteractionPrompt => HasChessPiece ? InteractionPromptText : MissingChessPiecePromptText;

        private bool HasChessPiece => GameManager.Instance != null &&
            GameManager.Instance.HasItem(PuzzleItemIds.ChessKnightPiece);

        private void Awake()
        {
            interactionCollider = GetComponent<BoxCollider2D>();
            interactionCollider.isTrigger = true;
            interactionCollider.size = new Vector2(HalfExtent * 2f, HalfExtent * 2f);

            outlineRenderer = GetComponent<LineRenderer>();
            if (outlineRenderer != null) outlineRenderer.enabled = false;

            ConfigureChessAssetSorting();
            SetInteractionHighlighted(false);
            if (GameManager.Instance != null && GameManager.Instance.IsPuzzleCompleted(PuzzleId))
            {
                DisablePortal();
            }
        }

        private void OnEnable()
        {
            ConfigureChessAssetSorting();
        }

        /// <summary>Saves the player's exploration state and loads the chess puzzle.</summary>
        public override void Interact(GameObject interactor)
        {
            if (interactor == null)
            {
                return;
            }

            GameManager gameManager = GameManager.Instance;
            if (gameManager == null || gameManager.IsPuzzleCompleted(PuzzleId) ||
                !gameManager.TryConsumeItem(PuzzleItemIds.ChessKnightPiece))
            {
                return;
            }

            TimelineEra returnEra = FindAnyObjectByType<TimeTravelManager>()?.CurrentEra ?? TimelineEra.Present;
            gameManager.SetPendingPlayerReturnState(interactor.transform.position, returnEra);
            SceneManager.LoadSceneAsync(PuzzleSceneName, LoadSceneMode.Single);
        }

        private void DisablePortal()
        {
            SetInteractionHighlighted(false);
            interactionCollider.enabled = false;
            if (outlineRenderer != null) outlineRenderer.enabled = false;
            enabled = false;
        }

        private void ConfigureChessAssetSorting()
        {
            Transform[] sceneTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include);
            Transform chessTransform = null;
            Transform tableTransform = null;

            foreach (Transform sceneTransform in sceneTransforms)
            {
                if (chessTransform == null && sceneTransform.name == ChessAssetObjectName)
                {
                    chessTransform = sceneTransform;
                }
                else if (tableTransform == null && sceneTransform.name == PastTableObjectName)
                {
                    tableTransform = sceneTransform;
                }

                if (chessTransform != null && tableTransform != null)
                {
                    break;
                }
            }

            if (chessTransform == null)
            {
                return;
            }

            SpriteRenderer targetSpriteRenderer = chessTransform.GetComponent<SpriteRenderer>()
                ?? chessTransform.GetComponentInChildren<SpriteRenderer>(true);
            if (targetSpriteRenderer != null && chessSpriteRenderer != targetSpriteRenderer)
            {
                chessSpriteRenderer = targetSpriteRenderer;
                originalChessSpriteColor = chessSpriteRenderer.color;
                hasOriginalChessSpriteColor = true;
            }

            SortingGroup tableSortingGroup = tableTransform != null
                ? tableTransform.GetComponent<SortingGroup>()
                    ?? tableTransform.GetComponentInChildren<SortingGroup>(true)
                : null;
            Renderer tableRenderer = tableTransform != null
                ? tableTransform.GetComponent<Renderer>()
                    ?? tableTransform.GetComponentInChildren<Renderer>(true)
                : null;
            int tableSortingLayerId = tableSortingGroup != null
                ? tableSortingGroup.sortingLayerID
                : tableRenderer != null ? tableRenderer.sortingLayerID : 0;
            int tableSortingOrder = tableSortingGroup != null
                ? tableSortingGroup.sortingOrder
                : tableRenderer != null ? tableRenderer.sortingOrder : 0;

            SortingGroup chessSortingGroup = chessTransform.GetComponent<SortingGroup>();
            if (chessSortingGroup == null)
            {
                chessSortingGroup = chessTransform.gameObject.AddComponent<SortingGroup>();
            }

            chessSortingGroup.sortingLayerID = tableSortingLayerId;
            chessSortingGroup.sortingOrder = tableSortingOrder + ChessSortingOrderOffset;
            if (outlineRenderer != null)
            {
                outlineRenderer.sortingLayerID = tableSortingLayerId;
                outlineRenderer.sortingOrder = tableSortingOrder + PortalSortingOrderOffset;
            }
        }

        /// <summary>Tints the visible chess asset with the same warm highlight used by nearby interactables.</summary>
        public void SetInteractionHighlighted(bool highlighted)
        {
            if (chessSpriteRenderer == null || !hasOriginalChessSpriteColor) return;
            if (!highlighted)
            {
                chessSpriteRenderer.color = originalChessSpriteColor;
                return;
            }

            Color highlightColor = new Color(InteractionHighlightColor.r, InteractionHighlightColor.g,
                InteractionHighlightColor.b, originalChessSpriteColor.a);
            chessSpriteRenderer.color = Color.Lerp(originalChessSpriteColor, highlightColor, HighlightBlend);
        }
    }
}
