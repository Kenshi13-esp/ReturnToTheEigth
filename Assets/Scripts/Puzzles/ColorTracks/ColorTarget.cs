using UnityEngine;

namespace ReturnToTheEigth.Puzzles
{
    /// <summary>Colored circle that a <see cref="TrackSlider"/> of the same color must reach.</summary>
    [RequireComponent(typeof(MeshRenderer))]
    [DisallowMultipleComponent]
    public sealed class ColorTarget : MonoBehaviour
    {
        private const float DarkenAmount = 0.35f;
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");

        [SerializeField] private TrackBoard board;
        [SerializeField] private PuzzleColorId colorId = PuzzleColorId.Red;
        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock propertyBlock;

        public PuzzleColorId ColorId => colorId;
        public Vector2Int Cell => ResolveCell();

        private void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            propertyBlock = new MaterialPropertyBlock();
            if (board == null) board = FindAnyObjectByType<TrackBoard>();
            ApplyColor();
            SnapToCell();
        }

        private void OnEnable()
        {
            if (board != null) board.Register(this);
        }

        private void OnDisable()
        {
            if (board != null) board.Unregister(this);
        }

        private Vector2Int ResolveCell()
        {
            PuzzleGrid grid = board != null ? board.Grid : null;
            if (grid == null) grid = FindAnyObjectByType<PuzzleGrid>();
            return grid != null ? grid.WorldToCell(transform.position) : Vector2Int.zero;
        }

        private void SnapToCell()
        {
            PuzzleGrid grid = board != null ? board.Grid : null;
            if (grid == null) return;
            Vector2 center = grid.CellToWorld(grid.WorldToCell(transform.position));
            transform.position = new Vector3(center.x, center.y, transform.position.z);
        }

        private void ApplyColor()
        {
            Color color = Color.Lerp(PuzzleColorPalette.GetColor(colorId), Color.black, DarkenAmount);
            meshRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorProperty, color);
            meshRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
