using UnityEngine;

namespace ReturnToTheEigth.Puzzles
{
    /// <summary>Chess piece on the puzzle grid. Shows a colored placeholder quad until <see cref="artSprite"/> is assigned.</summary>
    [DisallowMultipleComponent]
    public sealed class ChessPiece : MonoBehaviour
    {
        private const string MissingGridWarning = "ChessPiece could not find a PuzzleGrid in the scene.";
        private const float One = 1f;
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");

        [SerializeField] private ChessPieceType pieceType = ChessPieceType.Pawn;
        [SerializeField] private bool isWhite = true;
        [SerializeField] private PuzzleGrid grid;
        [SerializeField] private Vector2Int startCell;
        [Tooltip("When false the piece starts hidden (e.g. the piece the player places from the inventory).")]
        [SerializeField] private bool visibleAtStart = true;
        [SerializeField] private MeshRenderer placeholderRenderer;
        [SerializeField] private SpriteRenderer artRenderer;
        [Tooltip("Optional art. When assigned, the sprite is shown and the placeholder quad is hidden.")]
        [SerializeField] private Sprite artSprite;
        private MaterialPropertyBlock propertyBlock;
        private bool isVisible = true;

        public ChessPieceType PieceType => pieceType;
        public bool IsWhite => isWhite;
        public Vector2Int StartCell => startCell;
        public Vector2Int Cell { get; private set; }
        public bool IsVisible => isVisible;

        private void Awake()
        {
            if (grid == null) grid = FindAnyObjectByType<PuzzleGrid>();
            if (grid == null) Debug.LogWarning(MissingGridWarning, this);
            isVisible = visibleAtStart;
            ApplyArt();
        }

        private void Start()
        {
            PlaceAt(startCell);
        }

        private void OnValidate()
        {
            ApplyArt();
        }

        /// <summary>Snaps the piece to the center of the given cell and records it as the logical cell.</summary>
        public void PlaceAt(Vector2Int cell)
        {
            Cell = cell;
            if (grid == null) return;
            Vector2 center = grid.CellToWorld(cell);
            transform.position = new Vector3(center.x, center.y, transform.position.z);
        }

        /// <summary>Shows or hides the piece renderers without disabling the GameObject.</summary>
        public void SetVisible(bool visible)
        {
            isVisible = visible;
            ApplyArt();
        }

        private void ApplyArt()
        {
            bool useSprite = artSprite != null && artRenderer != null;
            if (artRenderer != null)
            {
                artRenderer.sprite = artSprite;
                artRenderer.enabled = useSprite && isVisible;
            }
            if (placeholderRenderer == null) return;
            placeholderRenderer.enabled = !useSprite && isVisible;
            if (useSprite) return;
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            placeholderRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorProperty, ChessPiecePlaceholder.GetPlaceholderColor(isWhite));
            placeholderRenderer.SetPropertyBlock(propertyBlock);
            float size = ChessPiecePlaceholder.GetPlaceholderScale(pieceType);
            placeholderRenderer.transform.localScale = new Vector3(size, size, One);
        }
    }
}
