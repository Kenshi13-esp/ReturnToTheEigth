using ReturnToTheEigth.Core;
using ReturnToTheEigth.Player;
using ReturnToTheEigth.TimeTravel;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ReturnToTheEigth.Interaction
{
    /// <summary>Loads the chess puzzle from the ChessPortal marker.</summary>
    [RequireComponent(typeof(BoxCollider2D), typeof(LineRenderer))]
    [DisallowMultipleComponent]
    public sealed class ChessPortalInteractable : InteractableBase
    {
        private const string PuzzleSceneName = "ChessPuzle";
        private const string PuzzleId = "KnightPuzzle";
        private const string InteractionPromptText = "Entrar al puzle de ajedrez";
        private const string MissingChessPiecePromptText = "Necesitas la pieza de caballo del puzle de color";
        private const string ChessAssetObjectName = "Chess";
        private const string PastTableObjectName = "pasttable";
        private const string SpriteShaderName = "Universal Render Pipeline/2D/Sprite-Unlit-Default";
        private const float HalfExtent = 0.3f;
        private const float ChessInteractionRadius = 0.2f;
        private const float InteractionRadiusSquared = ChessInteractionRadius * ChessInteractionRadius;
        private const float OutlineWidth = 0.04f;
        private const int OutlinePointCount = 4;
        private const int ChessSortingOrderOffset = 2;
        private const int PortalSortingOrderOffset = 1;
        private static readonly Color OutlineColor = Color.white;

        [SerializeField] private PlayerInteraction playerInteraction;

        /// <summary>Maximum query radius required to find chess portals.</summary>
        public const float MaximumInteractionRadius = ChessInteractionRadius;

        private BoxCollider2D interactionCollider;
        private LineRenderer outlineRenderer;
        private Material runtimeOutlineMaterial;
        private bool isHighlighted;

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
            ConfigureChessAssetSorting();
            if (GameManager.Instance != null && GameManager.Instance.IsPuzzleCompleted(PuzzleId))
            {
                DisablePortal();
            }
        }

        private void OnEnable()
        {
            ConfigureChessAssetSorting();
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
            Vector2 offset = playerPosition - InteractionPoint;
            SetHighlighted(HasChessPiece && offset.sqrMagnitude <= InteractionRadiusSquared);
        }

        private void OnDestroy()
        {
            if (runtimeOutlineMaterial != null)
            {
                Destroy(runtimeOutlineMaterial);
            }
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
            interactionCollider.enabled = false;
            outlineRenderer.enabled = false;
            enabled = false;
        }

        private void ConfigureChessAssetSorting()
        {
            Transform[] sceneTransforms = FindObjectsByType<Transform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
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

        private void ConfigureOutline()
        {
            outlineRenderer.useWorldSpace = false;
            outlineRenderer.loop = true;
            outlineRenderer.positionCount = OutlinePointCount;
            outlineRenderer.startWidth = OutlineWidth;
            outlineRenderer.endWidth = OutlineWidth;
            outlineRenderer.startColor = OutlineColor;
            outlineRenderer.endColor = OutlineColor;
            outlineRenderer.enabled = false;
            outlineRenderer.SetPosition(0, new Vector3(-HalfExtent, -HalfExtent, 0f));
            outlineRenderer.SetPosition(1, new Vector3(-HalfExtent, HalfExtent, 0f));
            outlineRenderer.SetPosition(2, new Vector3(HalfExtent, HalfExtent, 0f));
            outlineRenderer.SetPosition(3, new Vector3(HalfExtent, -HalfExtent, 0f));
        }

        private void SetHighlighted(bool highlighted)
        {
            if (outlineRenderer == null || isHighlighted == highlighted)
            {
                return;
            }

            isHighlighted = highlighted;
            outlineRenderer.enabled = highlighted;
            outlineRenderer.startColor = OutlineColor;
            outlineRenderer.endColor = OutlineColor;
            if (runtimeOutlineMaterial != null)
            {
                runtimeOutlineMaterial.color = OutlineColor;
            }
        }
    }
}
