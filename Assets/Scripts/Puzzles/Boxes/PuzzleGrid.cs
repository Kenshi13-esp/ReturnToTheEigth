using UnityEngine;

namespace ReturnToTheEigth.Puzzles
{
    /// <summary>Cell/world conversion and Physics2D-based occupancy queries for a rectangular XY grid.</summary>
    [DisallowMultipleComponent]
    public sealed class PuzzleGrid : MonoBehaviour
    {
        private const float DefaultCellSize = 0.48f;
        private const float DefaultOriginX = -1.44f;
        private const float DefaultOriginY = -1.2f;
        private const int DefaultColumns = 6;
        private const int DefaultRows = 5;
        private const int MinimumDimension = 1;
        private const float MinimumCellSize = 0.01f;
        private const float OccupancyShrink = 0.9f;
        private const float HalfCell = 0.5f;
        private const float NoRotation = 0f;
        private const int OverlapCapacity = 32;
        private const int FirstIndex = 0;
        private const int AllLayers = -1;
        private static readonly Color GridGizmoColor = new Color(0.3f, 0.9f, 0.6f, 0.6f);

        [SerializeField, Min(MinimumCellSize)] private float cellSize = DefaultCellSize;
        [SerializeField] private Vector2 origin = new Vector2(DefaultOriginX, DefaultOriginY);
        [SerializeField, Min(MinimumDimension)] private int columns = DefaultColumns;
        [SerializeField, Min(MinimumDimension)] private int rows = DefaultRows;
        [SerializeField] private LayerMask solidLayers = AllLayers;
        private readonly Collider2D[] overlapBuffer = new Collider2D[OverlapCapacity];

        public float CellSize => cellSize;
        public Vector2 Origin => origin;
        public Vector2Int Size => new Vector2Int(columns, rows);

        /// <summary>Returns the cell containing the supplied world position (may lie outside the grid).</summary>
        public Vector2Int WorldToCell(Vector2 worldPosition)
        {
            Vector2 local = (worldPosition - origin) / cellSize;
            return new Vector2Int(Mathf.FloorToInt(local.x), Mathf.FloorToInt(local.y));
        }

        /// <summary>Returns the world-space center of the supplied cell.</summary>
        public Vector2 CellToWorld(Vector2Int cell)
        {
            return origin + new Vector2((cell.x + HalfCell) * cellSize, (cell.y + HalfCell) * cellSize);
        }

        /// <summary>True when the cell lies within the configured columns and rows.</summary>
        public bool IsInside(Vector2Int cell)
        {
            return cell.x >= FirstIndex && cell.y >= FirstIndex && cell.x < columns && cell.y < rows;
        }

        /// <summary>True when the cell is inside the grid and no solid, non-ignored collider overlaps its shrunken area.</summary>
        public bool IsCellFree(Vector2Int cell, params Collider2D[] ignored)
        {
            if (!IsInside(cell)) return false;
            Physics2D.SyncTransforms();
            Vector2 center = CellToWorld(cell);
            Vector2 size = Vector2.one * (cellSize * OccupancyShrink);
            ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(solidLayers);
            int hitCount = Physics2D.OverlapBox(center, size, NoRotation, filter, overlapBuffer);
            if (hitCount == overlapBuffer.Length) return false;
            for (int index = FirstIndex; index < hitCount; index++)
            {
                Collider2D hit = overlapBuffer[index];
                if (hit.isTrigger || IsIgnored(hit, ignored)) continue;
                return false;
            }
            return true;
        }

        private static bool IsIgnored(Collider2D hit, Collider2D[] ignored)
        {
            if (ignored == null) return false;
            for (int index = FirstIndex; index < ignored.Length; index++)
            {
                Collider2D candidate = ignored[index];
                if (candidate != null && (candidate == hit || hit.transform.IsChildOf(candidate.transform))) return true;
            }
            return false;
        }

        private void OnValidate()
        {
            cellSize = Mathf.Max(MinimumCellSize, cellSize);
            columns = Mathf.Max(MinimumDimension, columns);
            rows = Mathf.Max(MinimumDimension, rows);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = GridGizmoColor;
            Vector3 cellExtent = new Vector3(cellSize, cellSize, NoRotation);
            for (int column = FirstIndex; column < columns; column++)
            {
                for (int row = FirstIndex; row < rows; row++)
                {
                    Gizmos.DrawWireCube(CellToWorld(new Vector2Int(column, row)), cellExtent);
                }
            }
        }
    }
}
