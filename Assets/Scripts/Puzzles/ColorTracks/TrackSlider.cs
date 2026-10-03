using System.Collections;
using ReturnToTheEigth.Core;
using UnityEngine;

namespace ReturnToTheEigth.Puzzles
{
    [RequireComponent(typeof(MeshRenderer))]
    [DisallowMultipleComponent]
    public sealed class TrackSlider : MonoBehaviour
    {
        private const string MissingBoardWarning = "TrackSlider could not find a TrackBoard in the scene.";
        private const string ScrapingSoundId = "metal-scraping";
        private const float DefaultSlideSpeed = 6f;
        private const float DefaultSelectedScale = 1.15f;
        private const float DefaultHoveredScale = 1.08f;
        private const float MinimumSlideSpeed = 0.1f;
        private const float MinimumScale = 0.1f;
        private const float One = 1f;
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");

        [SerializeField] private TrackBoard board;
        [SerializeField] private PuzzleColorId colorId = PuzzleColorId.Red;
        [SerializeField, Min(MinimumSlideSpeed)] private float slideSpeed = DefaultSlideSpeed;
        [SerializeField, Min(MinimumScale)] private float selectedScale = DefaultSelectedScale;
        private MeshRenderer meshRenderer;
        private SpriteRenderer[] spriteRenderers;
        private Color[] baseSpriteColors;
        private MaterialPropertyBlock propertyBlock;
        private Vector3 baseScale;
        private Coroutine slideRoutine;
        private bool isSelected;
        private bool isHovered;
        private bool visualStateInitialized;

        public PuzzleColorId ColorId => colorId;
        public Vector2Int Cell { get; private set; }
        public bool IsMoving => slideRoutine != null;
        public bool IsSelected
        {
            get => isSelected;
            set { isSelected = value; ApplyVisual(); }
        }
        public bool IsHovered
        {
            get => isHovered;
            set
            {
                if (isHovered == value) return;
                isHovered = value;
                ApplyVisual();
            }
        }
        private PuzzleGrid Grid => board != null ? board.Grid : null;

        private void Awake()
        {
            InitializeVisualState();
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
            SoundManager.Play(ScrapingSoundId);
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

        private void InitializeVisualState()
        {
            if (visualStateInitialized) return;

            meshRenderer = GetComponent<MeshRenderer>();
            spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            baseSpriteColors = new Color[spriteRenderers.Length];
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                baseSpriteColors[i] = spriteRenderers[i].color;
            }

            propertyBlock = new MaterialPropertyBlock();
            baseScale = transform.localScale;
            visualStateInitialized = true;
        }

        private void ApplyVisual()
        {
            InitializeVisualState();
            Color pieceColor = PuzzleColorPalette.GetColor(colorId);
            Color highlightedPieceColor = isSelected || isHovered
                ? PuzzleInteractionPalette.GetHighlightedColor(pieceColor)
                : pieceColor;

            if (meshRenderer != null)
            {
                meshRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(BaseColorProperty, highlightedPieceColor);
                meshRenderer.SetPropertyBlock(propertyBlock);
            }

            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i] == null) continue;
                spriteRenderers[i].color = isSelected || isHovered
                    ? PuzzleInteractionPalette.GetHighlightedColor(baseSpriteColors[i])
                    : baseSpriteColors[i];
            }

            float visualScale = isSelected ? selectedScale : isHovered ? DefaultHoveredScale : One;
            transform.localScale = baseScale * visualScale;
        }
    }
}
