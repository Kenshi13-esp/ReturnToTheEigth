using System.Collections;
using ReturnToTheEigth.Interaction;
using ReturnToTheEigth.Player;
using UnityEngine;

namespace ReturnToTheEigth.Puzzles
{
    /// <summary>Present-era box that can be grabbed and moved one cell at a time, mirroring its Past copy.</summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    [DisallowMultipleComponent]
    public sealed class PushableBox : InteractableBase
    {
        private const string GrabPrompt = "Agarrar caja";
        private const string ReleasePrompt = "Soltar caja";
        private const string MissingGridWarning = "PushableBox could not find a PuzzleGrid in the scene.";
        private const string MissingPastWarning = "PushableBox has no pastCounterpart; the box will not exist in the Past.";
        private const float DefaultStepDuration = 0.2f;
        private const float MinimumStepDuration = 0.01f;
        private const float Zero = 0f;
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

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            boxCollider = GetComponent<BoxCollider2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = Zero;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            if (grid == null) grid = FindAnyObjectByType<PuzzleGrid>();
            if (grid == null)
            {
                Debug.LogWarning(MissingGridWarning, this);
                return;
            }
            if (pastCounterpart == null) Debug.LogWarning(MissingPastWarning, this);
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
                body.MovePosition(Vector2.Lerp(start, target, Mathf.Clamp01(elapsed / duration)));
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

        private void SyncPastCounterpart()
        {
            if (pastCounterpart == null) return;
            Vector3 position = transform.position;
            pastCounterpart.position = new Vector3(position.x, position.y, pastCounterpart.position.z);
        }
    }
}
