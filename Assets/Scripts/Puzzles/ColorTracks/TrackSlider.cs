using System.Collections;
using UnityEngine;

namespace ReturnToTheEigth.Puzzles
{
    [RequireComponent(typeof(MeshRenderer))]
    [DisallowMultipleComponent]
    public sealed class TrackSlider : MonoBehaviour
    {
        private const string MissingBoardWarning = "TrackSlider could not find a TrackBoard in the scene.";
        private const float DefaultSlideSpeed = 6f;
        private const float DefaultSelectedScale = 1.15f;
        private const float MinimumSlideSpeed = 0.1f;
        private const float MinimumScale = 0.1f;
        private const float SelectedBrighten = 0.3f;
        private const float One = 1f;
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");

        [SerializeField] private TrackBoard board;
        [SerializeField] private PuzzleColorId colorId = PuzzleColorId.Red;
        [SerializeField, Min(MinimumSlideSpeed)] private float slideSpeed = DefaultSlideSpeed;
        [SerializeField, Min(MinimumScale)] private float selectedScale = DefaultSelectedScale;
        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock propertyBlock;
        private Vector3 baseScale;
        private Coroutine slideRoutine;
        private bool isSelected;

        public PuzzleColorId ColorId => colorId;
        public Vector2Int Cell { get; private set; }
        public bool IsMoving => slideRoutine != null;
        public bool IsSelected
        {
            get => isSelected;
            set { isSelected = value; ApplyVisual(); }
        }
        private PuzzleGrid Grid => board != null ? board.Grid : null;

        private void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            propertyBlock = new MaterialPropertyBlock();
            baseScale = transform.localScale;
            if (board == null) board = FindAnyObjectByType<TrackBoard>();
            if (board == null) Debug.LogWarning(MissingBoardWarning, this);
            ApplyVisual();
            if (Grid != null)
            {
                Cell = Grid.WorldToCell(transform.position);
                SnapToCell(Cell);
            }
        }

        private void OnEnable()
        {
            if (board != null) board.Register(this);
        }

        private void Start()
        {
            if (board != null) board.ValidateStartCell(this);
        }

        private void OnDisable()
        {
            if (slideRoutine != null)
            {
                StopCoroutine(slideRoutine);
                slideRoutine = null;
                SnapToCell(Cell);
            }
            IsSelected = false;
            if (board != null) board.Unregister(this);
        }

        public void SlideTo(Vector2Int destination)
        {
            if (IsMoving || Grid == null || destination == Cell) return;
            Cell = destination;
            slideRoutine = StartCoroutine(SlideRoutine(Grid.CellToWorld(destination)));
        }

        private IEnumerator SlideRoutine(Vector2 target)
        {
            float speed = slideSpeed * Grid.CellSize;
            Vector3 destination = new Vector3(target.x, target.y, transform.position.z);
            while ((transform.position - destination).sqrMagnitude > float.Epsilon)
            {
                transform.position = Vector3.MoveTowards(transform.position, destination, speed * Time.deltaTime);
                yield return null;
            }
            slideRoutine = null;
            SnapToCell(Cell);
            board.NotifySlideFinished(this);
        }

        private void SnapToCell(Vector2Int cell)
        {
            if (Grid == null) return;
            Vector2 center = Grid.CellToWorld(cell);
            transform.position = new Vector3(center.x, center.y, transform.position.z);
        }

        private void ApplyVisual()
        {
            if (meshRenderer == null) return;
            Color color = PuzzleColorPalette.GetColor(colorId);
            if (isSelected) color = Color.Lerp(color, Color.white, SelectedBrighten);
            meshRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorProperty, color);
            meshRenderer.SetPropertyBlock(propertyBlock);
            transform.localScale = baseScale * (isSelected ? selectedScale : One);
        }
    }
}
