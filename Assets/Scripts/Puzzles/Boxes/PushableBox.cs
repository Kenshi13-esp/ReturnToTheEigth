using System.Collections;
using ReturnToTheEigth.Interaction;
using ReturnToTheEigth.Player;
using UnityEngine;

namespace ReturnToTheEigth.Puzzles
{
    /// <summary>Present-era box that can be grabbed and moved one cell at a time, optionally mirroring a Past copy.</summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    [DisallowMultipleComponent]
    public sealed class PushableBox : InteractableBase
    {
        private const string GrabPrompt = "Agarrar caja";
        private const string ReleasePrompt = "Soltar caja";
        private const string MissingGridWarning = "PushableBox could not find a PuzzleGrid in the scene.";
        public const float InteractionRadius = 0.45f;
        private const float DefaultStepDuration = 0.2f;
        private const float MinimumStepDuration = 0.01f;
        private const float DefaultCellSize = 0.48f;
        private const float BoxVisualCellFill = 0.92f;
        private const float ColliderVisualFill = 0.9f;
        private const float MinimumScale = 0.0001f;
        private const float Zero = 0f;
        private const float One = 1f;
        private static readonly WaitForFixedUpdate WaitForPhysicsStep = new WaitForFixedUpdate();

        [SerializeField] private PuzzleGrid grid;
        [SerializeField] private Transform pastCounterpart;
        [SerializeField, Min(MinimumStepDuration)] private float stepDuration = DefaultStepDuration;
        private Rigidbody2D body;
        private BoxCollider2D boxCollider;
        private Coroutine moveRoutine;
        private Vector2Int moveTargetCell;

        public override string InteractionPrompt => IsGrabbed ? ReleasePrompt : GrabPrompt;
        public Vector2Int Cell => grid != null ? grid.WorldToCell(body.position) : Vector2Int.zero;
        public bool IsMoving => moveRoutine != null;
        public bool IsGrabbed { get; set; }
        public float StepDuration => stepDuration;
        public Collider2D Collider => boxCollider;
        public Renderer VisualRenderer => GetComponent<Renderer>();

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            boxCollider = GetComponent<BoxCollider2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = Zero;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            if (grid == null) grid = FindAnyObjectByType<PuzzleGrid>();
            float cellSize = grid != null ? grid.CellSize : DefaultCellSize;
            ConfigureBoxSizeAndCollider(transform, cellSize);
            ConfigureBoxSizeAndCollider(pastCounterpart, cellSize);
            if (grid == null)
            {
                Debug.LogWarning(MissingGridWarning, this);
                return;
            }
            SnapToCell(grid.WorldToCell(transform.position));
        }

        private void OnDisable()
        {
            if (moveRoutine != null)
            {
                StopCoroutine(moveRoutine);
                moveRoutine = null;
                SnapToCell(moveTargetCell);
            }
            IsGrabbed = false;
        }

        /// <summary>Toggles the grab state through the interactor's <see cref="BoxDragController"/>.</summary>
        public override void Interact(GameObject interactor)
        {
            BoxDragController dragController = interactor != null ? interactor.GetComponent<BoxDragController>() : null;
            dragController?.ToggleGrab(this);
        }

        /// <summary>True when the neighbouring cell in the supplied direction is inside the grid and unoccupied.</summary>
        public bool CanMove(Vector2Int direction, Collider2D ignoredPlayer)
        {
            return grid != null && !IsMoving && grid.IsCellFree(Cell + direction, boxCollider, ignoredPlayer);
        }

        /// <summary>Starts a one-cell interpolated move; ignored while a previous move is still running.</summary>
        public void Move(Vector2Int direction)
        {
            if (grid == null || IsMoving) return;
            moveTargetCell = Cell + direction;
            moveRoutine = StartCoroutine(MoveRoutine(grid.CellToWorld(moveTargetCell)));
        }

        private IEnumerator MoveRoutine(Vector2 target)
        {
            Vector2 start = body.position;
            float duration = Mathf.Max(MinimumStepDuration, stepDuration);
            float elapsed = Zero;
            while (elapsed < duration)
            {
                yield return WaitForPhysicsStep;
                elapsed += Time.fixedDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float easedProgress = Mathf.SmoothStep(Zero, One, progress);
                body.MovePosition(Vector2.Lerp(start, target, easedProgress));
            }
            moveRoutine = null;
            SnapToCell(moveTargetCell);
        }

        private void SnapToCell(Vector2Int cell)
        {
            if (grid == null || body == null) return;
            Vector2 center = grid.CellToWorld(cell);
            body.position = center;
            transform.position = new Vector3(center.x, center.y, transform.position.z);
            SyncPastCounterpart();
        }

        private static void ConfigureBoxSizeAndCollider(Transform boxTransform, float cellSize)
        {
            if (boxTransform == null) return;

            SpriteRenderer spriteRenderer = boxTransform.GetComponentInChildren<SpriteRenderer>(true);
            Vector3 visualSize = Vector3.one;
            Vector3 visualCenter = Vector3.zero;
            bool hasSprite = spriteRenderer != null && spriteRenderer.sprite != null;
            if (hasSprite)
            {
                Bounds spriteBounds = spriteRenderer.sprite.bounds;
                visualSize = spriteBounds.size;
                visualCenter = spriteBounds.center;
            }
            else
            {
                MeshFilter meshFilter = boxTransform.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    Bounds meshBounds = meshFilter.sharedMesh.bounds;
                    visualSize = meshBounds.size;
                    visualCenter = meshBounds.center;
                }
            }

            Vector3 parentScale = boxTransform.parent != null ? boxTransform.parent.lossyScale : Vector3.one;
            float scaleX = cellSize * BoxVisualCellFill
                / Mathf.Max(MinimumScale, visualSize.x * Mathf.Abs(parentScale.x));
            float scaleY = cellSize * BoxVisualCellFill
                / Mathf.Max(MinimumScale, visualSize.y * Mathf.Abs(parentScale.y));
            Vector3 localScale = boxTransform.localScale;
            localScale.x = hasSprite ? Mathf.Min(scaleX, scaleY) : scaleX;
            localScale.y = hasSprite ? Mathf.Min(scaleX, scaleY) : scaleY;
            boxTransform.localScale = localScale;

            if (hasSprite && spriteRenderer.transform != boxTransform)
            {
                Transform visualTransform = spriteRenderer.transform;
                visualTransform.localScale = Vector3.one;
                Vector3 visualPosition = visualTransform.localPosition;
                visualPosition.x = -visualCenter.x;
                visualPosition.y = -visualCenter.y;
                visualTransform.localPosition = visualPosition;
            }

            BoxCollider2D boxCollider = boxTransform.GetComponent<BoxCollider2D>();
            if (boxCollider == null) return;

            Vector3 worldScale = boxTransform.lossyScale;
            boxCollider.size = new Vector2(
                cellSize * ColliderVisualFill / Mathf.Max(MinimumScale, Mathf.Abs(worldScale.x)),
                cellSize * ColliderVisualFill / Mathf.Max(MinimumScale, Mathf.Abs(worldScale.y)));
            boxCollider.offset = Vector2.zero;
            boxCollider.isTrigger = false;
        }

        private void SyncPastCounterpart()
        {
            if (pastCounterpart == null) return;
            Vector3 position = transform.position;
            pastCounterpart.position = new Vector3(position.x, position.y, pastCounterpart.position.z);
        }
    }
}
