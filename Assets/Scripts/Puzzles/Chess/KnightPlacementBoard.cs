using System;
using System.Collections;
using System.Collections.Generic;
using ReturnToTheEigth.CameraSystem;
using ReturnToTheEigth.Events;
using UnityEngine;

namespace ReturnToTheEigth.Puzzles
{
    /// <summary>Knight placement puzzle: several lit cells, only <see cref="correctCell"/> wins. The placed knight appears on the chosen cell; a wrong cell shakes the camera and restarts.</summary>
    [DisallowMultipleComponent]
    public sealed class KnightPlacementBoard : MonoBehaviour
    {
        private const string WinMessage = "you win";
        private const string MissingKnightError = "KnightPlacementBoard has no placed knight assigned.";
        private const string MissingGridWarning = "KnightPlacementBoard could not find a PuzzleGrid.";
        private const string CorrectCellNotLitWarning = "KnightPlacementBoard: correctCell {0} has no PlacementHighlight; the puzzle cannot be solved.";
        private const string DuplicateHighlightWarning = "Two PlacementHighlights share cell {0}.";
        private const float DefaultEvaluationDelay = 0.35f;
        private const float DefaultShakeDuration = 0.5f;
        private const float DefaultShakeMagnitude = 0.18f;
        private const float DefaultRestartDelay = 0.7f;
        private const float Zero = 0f;
        private const float GizmoInset = 0.9f;
        private static readonly Color CandidateGizmoColor = new Color(1f, 0.9f, 0.3f, 0.5f);
        private static readonly Color CorrectGizmoColor = new Color(0.3f, 1f, 0.4f, 0.7f);

        [SerializeField] private PuzzleGrid grid;
        [Tooltip("Hidden knight that appears on the chosen cell (the piece the player brings from the inventory).")]
        [SerializeField] private ChessPiece placedKnight;
        [SerializeField] private Vector2Int correctCell = new Vector2Int(4, 4);
        [SerializeField] private CameraShake cameraShake;
        [SerializeField, Min(Zero)] private float evaluationDelay = DefaultEvaluationDelay;
        [SerializeField, Min(Zero)] private float shakeDuration = DefaultShakeDuration;
        [SerializeField, Min(Zero)] private float shakeMagnitude = DefaultShakeMagnitude;
        [SerializeField, Min(Zero)] private float restartDelay = DefaultRestartDelay;
        [SerializeField] private VoidEventChannelSO puzzleSolvedChannel;
        private readonly List<PlacementHighlight> highlights = new List<PlacementHighlight>();
        private Coroutine placementRoutine;

        public event Action Placed;
        public event Action Solved;
        public event Action Failed;
        public event Action Restarted;

        public PuzzleGrid Grid => grid;
        public ChessPiece PlacedKnight => placedKnight;
        public bool IsSolved { get; private set; }
        public bool IsBusy => placementRoutine != null;

        private void Awake()
        {
            if (grid == null) grid = GetComponent<PuzzleGrid>();
            if (grid == null) grid = FindAnyObjectByType<PuzzleGrid>();
            if (grid == null) Debug.LogWarning(MissingGridWarning, this);
            if (cameraShake == null) cameraShake = FindAnyObjectByType<CameraShake>();
            if (placedKnight == null) Debug.LogError(MissingKnightError, this);
        }

        private void Start()
        {
            if (!IsCandidate(correctCell)) Debug.LogWarning(string.Format(CorrectCellNotLitWarning, correctCell), this);
            if (placedKnight != null) placedKnight.SetVisible(false);
        }

        /// <summary>True when a highlight marks the cell as a possible placement.</summary>
        public bool IsCandidate(Vector2Int cell)
        {
            return GetHighlightAt(cell) != null;
        }

        /// <summary>Places the knight on a lit cell and evaluates it after a short delay. Returns false when the placement is not allowed right now.</summary>
        public bool TryPlaceKnight(Vector2Int cell)
        {
            if (IsBusy || IsSolved || placedKnight == null || !IsCandidate(cell)) return false;
            SetHighlightsVisible(false);
            placedKnight.PlaceAt(cell);
            placedKnight.SetVisible(true);
            Placed?.Invoke();
            placementRoutine = StartCoroutine(EvaluateRoutine(cell));
            return true;
        }

        /// <summary>Hides the placed knight and lights the candidate cells again.</summary>
        public void Restart()
        {
            if (placementRoutine != null)
            {
                StopCoroutine(placementRoutine);
                placementRoutine = null;
            }
            if (placedKnight != null) placedKnight.SetVisible(false);
            SetHighlightsVisible(true);
            Restarted?.Invoke();
        }

        /// <summary>Registers a lit cell. Called by <see cref="PlacementHighlight.OnEnable"/>.</summary>
        public void Register(PlacementHighlight highlight)
        {
            if (highlight == null || highlights.Contains(highlight)) return;
            PlacementHighlight existing = GetHighlightAt(highlight.Cell);
            if (existing != null) Debug.LogWarning(string.Format(DuplicateHighlightWarning, highlight.Cell), highlight);
            highlights.Add(highlight);
        }

        /// <summary>Unregisters a lit cell. Called by <see cref="PlacementHighlight.OnDisable"/>.</summary>
        public void Unregister(PlacementHighlight highlight)
        {
            highlights.Remove(highlight);
        }

        private PlacementHighlight GetHighlightAt(Vector2Int cell)
        {
            for (int i = 0; i < highlights.Count; i++)
            {
                if (highlights[i] != null && highlights[i].Cell == cell) return highlights[i];
            }
            return null;
        }

        private void SetHighlightsVisible(bool visible)
        {
            for (int i = 0; i < highlights.Count; i++)
            {
                if (highlights[i] != null) highlights[i].SetVisible(visible);
            }
        }

        private IEnumerator EvaluateRoutine(Vector2Int cell)
        {
            yield return new WaitForSeconds(evaluationDelay);
            if (cell == correctCell)
            {
                IsSolved = true;
                placementRoutine = null;
                Debug.Log(WinMessage, this);
                if (puzzleSolvedChannel != null) puzzleSolvedChannel.RaiseEvent();
                Solved?.Invoke();
                yield break;
            }
            Failed?.Invoke();
            if (cameraShake != null) cameraShake.Shake(shakeDuration, shakeMagnitude);
            yield return new WaitForSeconds(Mathf.Max(shakeDuration, restartDelay));
            placementRoutine = null;
            Restart();
        }

        private void OnDrawGizmos()
        {
            PuzzleGrid gizmoGrid = grid != null ? grid : GetComponent<PuzzleGrid>();
            if (gizmoGrid == null) return;
            Vector3 size = new Vector3(gizmoGrid.CellSize * GizmoInset, gizmoGrid.CellSize * GizmoInset, Zero);
            PlacementHighlight[] sceneHighlights = Application.isPlaying
                ? highlights.ToArray()
                : FindObjectsByType<PlacementHighlight>();
            Gizmos.color = CandidateGizmoColor;
            foreach (PlacementHighlight highlight in sceneHighlights)
            {
                if (highlight == null || highlight.Cell == correctCell) continue;
                Gizmos.DrawWireCube(gizmoGrid.CellToWorld(highlight.Cell), size);
            }
            Gizmos.color = CorrectGizmoColor;
            Gizmos.DrawWireCube(gizmoGrid.CellToWorld(correctCell), size);
        }
    }
}
