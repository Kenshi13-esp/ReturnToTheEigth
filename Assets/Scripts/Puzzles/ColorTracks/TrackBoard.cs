using System.Collections.Generic;
using ReturnToTheEigth.Events;
using UnityEngine;

namespace ReturnToTheEigth.Puzzles
{
    /// <summary>Defines the track cells, tracks registered pieces, resolves slides and detects the solved state.</summary>
    [DisallowMultipleComponent]
    public sealed class TrackBoard : MonoBehaviour
    {
        private const char TrackCharacter = '#';
        private const string SolvedLog = "Puzzle de colores resuelto: todas las ollas están sobre su círculo.";
        private const string RowSizeWarning = "TrackBoard: trackRows must contain grid.rows strings of grid.columns characters each.";
        private const string SliderOffTrackWarning = "TrackBoard: slider '{0}' starts on a cell that is not passable for its color.";
        private const string DuplicateTargetWarning = "TrackBoard: target '{0}' shares a cell or color with another target.";
        private const int FirstIndex = 0;
        private const int LastOffset = 1;
        private const float GizmoDepth = 0f;
        private static readonly Color TrackGizmoColor = new Color(0.4f, 0.7f, 1f, 0.25f);
        private static readonly Color TargetGizmoAlpha = new Color(1f, 1f, 1f, 0.35f);

        [SerializeField] private PuzzleGrid grid;
        [SerializeField] private string[] trackRows = { ".#.", "###", ".#." };
        [SerializeField] private VoidEventChannelSO puzzleSolvedChannel;
        private readonly List<TrackSlider> sliders = new List<TrackSlider>();
        private readonly List<ColorTarget> targets = new List<ColorTarget>();

        public PuzzleGrid Grid => grid;
        public bool IsSolved { get; private set; }

        private void Awake()
        {
            ResolveGrid();
        }

        /// <summary>True when the cell is inside the grid and marked with '#' in <c>trackRows</c> (first string is the top row).</summary>
        public bool IsTrack(Vector2Int cell)
        {
            if (grid == null || trackRows == null || !grid.IsInside(cell)) return false;
            int rowIndex = trackRows.Length - LastOffset - cell.y;
            if (rowIndex < FirstIndex || rowIndex >= trackRows.Length) return false;
            string row = trackRows[rowIndex];
            return row != null && cell.x < row.Length && row[cell.x] == TrackCharacter;
        }

        /// <summary>Returns the registered target occupying the cell, or null.</summary>
        public ColorTarget GetTargetAt(Vector2Int cell)
        {
            for (int index = FirstIndex; index < targets.Count; index++)
            {
                ColorTarget target = targets[index];
                if (target != null && target.Cell == cell) return target;
            }
            return null;
        }

        /// <summary>True when a piece of the supplied color may occupy the cell: any track cell or the target of its own color.</summary>
        public bool IsPassable(Vector2Int cell, PuzzleColorId color)
        {
            if (IsTrack(cell)) return true;
            ColorTarget target = GetTargetAt(cell);
            return target != null && target.ColorId == color;
        }

        /// <summary>Returns the registered slider whose logical cell matches, or null.</summary>
        public TrackSlider GetSliderAt(Vector2Int cell)
        {
            for (int index = FirstIndex; index < sliders.Count; index++)
            {
                TrackSlider slider = sliders[index];
                if (slider != null && slider.Cell == cell) return slider;
            }
            return null;
        }

        /// <summary>Slides from the piece's cell in the given direction until blocked; may return the current cell.</summary>
        public Vector2Int ComputeSlideDestination(TrackSlider slider, Vector2Int direction)
        {
            Vector2Int current = slider.Cell;
            if (direction == Vector2Int.zero) return current;
            while (true)
            {
                Vector2Int next = current + direction;
                if (!IsPassable(next, slider.ColorId) || GetSliderAt(next) != null) return current;
                current = next;
            }
        }

        /// <summary>Registers a sliding piece for occupancy and solve checks.</summary>
        public void Register(TrackSlider slider)
        {
            if (slider != null && !sliders.Contains(slider)) sliders.Add(slider);
        }

        /// <summary>Removes a sliding piece from occupancy and solve checks.</summary>
        public void Unregister(TrackSlider slider)
        {
            sliders.Remove(slider);
        }

        /// <summary>Registers a color target; warns when it duplicates another target's cell or color.</summary>
        public void Register(ColorTarget target)
        {
            if (target == null || targets.Contains(target)) return;
            for (int index = FirstIndex; index < targets.Count; index++)
            {
                ColorTarget other = targets[index];
                if (other != null && (other.Cell == target.Cell || other.ColorId == target.ColorId))
                {
                    Debug.LogWarning(string.Format(DuplicateTargetWarning, target.name), target);
                    break;
                }
            }
            targets.Add(target);
        }

        /// <summary>Removes a color target.</summary>
        public void Unregister(ColorTarget target)
        {
            targets.Remove(target);
        }

        /// <summary>Warns when a piece starts on a cell that its color cannot occupy.</summary>
        public void ValidateStartCell(TrackSlider slider)
        {
            if (slider != null && !IsPassable(slider.Cell, slider.ColorId))
            {
                Debug.LogWarning(string.Format(SliderOffTrackWarning, slider.name), slider);
            }
        }

        /// <summary>Re-evaluates the solved state after a slide; raises the solved channel once.</summary>
        public void NotifySlideFinished(TrackSlider slider)
        {
            if (IsSolved || targets.Count == FirstIndex) return;
            for (int index = FirstIndex; index < targets.Count; index++)
            {
                ColorTarget target = targets[index];
                if (target == null) continue;
                TrackSlider occupant = GetSliderAt(target.Cell);
                if (occupant == null || occupant.IsMoving || occupant.ColorId != target.ColorId) return;
            }
            IsSolved = true;
            Debug.Log(SolvedLog, this);
            puzzleSolvedChannel?.RaiseEvent();
        }

        private void ResolveGrid()
        {
            if (grid == null) grid = GetComponent<PuzzleGrid>();
            if (grid == null) grid = FindAnyObjectByType<PuzzleGrid>();
        }

        private void OnValidate()
        {
            ResolveGrid();
            if (grid == null || trackRows == null) return;
            bool valid = trackRows.Length == grid.Size.y;
            for (int index = FirstIndex; valid && index < trackRows.Length; index++)
            {
                valid = trackRows[index] != null && trackRows[index].Length == grid.Size.x;
            }
            if (!valid) Debug.LogWarning(RowSizeWarning, this);
        }

        private void OnDrawGizmos()
        {
            if (grid == null) return;
            Vector3 cellExtent = new Vector3(grid.CellSize, grid.CellSize, GizmoDepth);
            Gizmos.color = TrackGizmoColor;
            for (int column = FirstIndex; column < grid.Size.x; column++)
            {
                for (int row = FirstIndex; row < grid.Size.y; row++)
                {
                    Vector2Int cell = new Vector2Int(column, row);
                    if (IsTrack(cell)) Gizmos.DrawCube(grid.CellToWorld(cell), cellExtent);
                }
            }
            ColorTarget[] gizmoTargets = Application.isPlaying
                ? targets.ToArray() : FindObjectsByType<ColorTarget>();
            for (int index = FirstIndex; index < gizmoTargets.Length; index++)
            {
                ColorTarget target = gizmoTargets[index];
                if (target == null) continue;
                Gizmos.color = PuzzleColorPalette.GetColor(target.ColorId) * TargetGizmoAlpha;
                Gizmos.DrawCube(grid.CellToWorld(target.Cell), cellExtent);
            }
        }
    }
}
