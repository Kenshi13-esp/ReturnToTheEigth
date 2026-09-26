using System.Collections.Generic;
using ReturnToTheEigth.Core;
using ReturnToTheEigth.Puzzles;
using UnityEngine;

namespace ReturnToTheEigth.Puzzles.Electricity
{
    /// <summary>Owns the 4x4 wire board, tracks current flow from A and detects when it reaches B.</summary>
    [DisallowMultipleComponent]
    public sealed class ElectricityPuzzleBoard : MonoBehaviour
    {
        private const string MissingGridWarning = "ElectricityPuzzleBoard could not find a PuzzleGrid.";
        private const string DuplicateTileWarning = "ElectricityPuzzleBoard has duplicate tiles at cell {0}.";
        private const string MissingTileWarning = "ElectricityPuzzleBoard has no tile at cell {0}.";
        private const string PuzzleId = "ElectricityPuzzle";
        private const float TerminalReach = 0.92f;
        private const float TerminalEdgeInset = 0.5f;
        private const float TerminalLineWidth = 0.075f;
        private const float Zero = 0f;
        private const int FirstIndex = 0;
        private const int LinePointCount = 2;
        private const int SortingOrder = 4;
        private const string LineShaderName = "Universal Render Pipeline/Unlit";
        private const string FallbackLineShaderName = "Sprites/Default";
        private static readonly ElectricityPorts[] PortDirections =
        {
            ElectricityPorts.North,
            ElectricityPorts.East,
            ElectricityPorts.South,
            ElectricityPorts.West
        };
        private static readonly Vector2Int[] CellDirections =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };
        private static readonly ElectricityPorts[] OppositePorts =
        {
            ElectricityPorts.South,
            ElectricityPorts.West,
            ElectricityPorts.North,
            ElectricityPorts.East
        };
        private static readonly Color InputColor = new Color(0.2f, 0.95f, 0.25f, 1f);
        private static readonly Color OutputColor = new Color(1f, 0.16f, 0.12f, 1f);
        private static Material terminalMaterial;

        [SerializeField] private PuzzleGrid grid;
        [SerializeField] private ElectricityPuzzleTile[] tiles;
        [SerializeField] private Vector2Int inputCell = new Vector2Int(0, 0);
        [SerializeField] private Vector2Int outputCell = new Vector2Int(3, 3);
        [SerializeField] private Transform inputTerminalAnchor;
        [SerializeField] private Transform outputTerminalAnchor;
        [SerializeField] private string puzzleId = PuzzleId;

        private ElectricityPuzzleTile[,] tilesByCell;
        private bool[,] energizedCells;
        private LineRenderer inputTerminal;
        private LineRenderer outputTerminal;
        private Material inputTerminalMaterial;
        private Material outputTerminalMaterial;

        /// <summary>Raised once when every tile belongs to one closed electrical network from A to B.</summary>
        public event System.Action Solved;

        /// <summary>Gets the grid coordinate conversion and board bounds.</summary>
        public PuzzleGrid Grid => grid;

        /// <summary>Gets whether the electricity network has been solved.</summary>
        public bool IsSolved { get; private set; }

        /// <summary>Gets the number of cells reached by current flowing from A.</summary>
        public int PoweredTileCount { get; private set; }

        /// <summary>Gets the total number of cells in the grid.</summary>
        public int TileCount => grid != null ? grid.Size.x * grid.Size.y : 0;

        private void Awake()
        {
            ResolveGrid();
            BuildTileLookup();
            if (grid != null)
            {
                energizedCells = new bool[grid.Size.x, grid.Size.y];
            }
        }

        private void Start()
        {
            IsSolved = GameManager.Instance != null && GameManager.Instance.IsPuzzleCompleted(puzzleId);
            CreateTerminalVisuals();
            RefreshPowerAndSolveState();
        }

        private void OnDestroy()
        {
            if (inputTerminalMaterial != null)
            {
                Destroy(inputTerminalMaterial);
            }
            if (outputTerminalMaterial != null)
            {
                Destroy(outputTerminalMaterial);
            }
        }

        /// <summary>Returns whether the supplied cell is inside the configured board.</summary>
        public bool IsInside(Vector2Int cell)
        {
            return grid != null && grid.IsInside(cell);
        }

        /// <summary>Rotates the tile at the given cell and reevaluates the complete network.</summary>
        public bool TryRotateTile(Vector2Int cell)
        {
            if (IsSolved || !IsInside(cell))
            {
                return false;
            }

            ElectricityPuzzleTile tile = tilesByCell[cell.x, cell.y];
            if (tile == null || !tile.RotateClockwise())
            {
                return false;
            }

            RefreshPowerAndSolveState();
            return true;
        }

        /// <summary>Checks whether current from A reaches the output terminal B.</summary>
        public bool IsSolvedConfiguration()
        {
            if (!TryCollectPoweredTiles(out bool[,] powered) || !IsInside(outputCell))
            {
                return false;
            }

            ElectricityPuzzleTile outputTile = tilesByCell[outputCell.x, outputCell.y];
            return powered[outputCell.x, outputCell.y]
                && outputTile != null
                && HasPort(outputTile.CurrentPorts, ElectricityPorts.East);
        }

        private void RefreshPowerAndSolveState()
        {
            if (grid == null || tilesByCell == null)
            {
                return;
            }

            bool[,] powered = new bool[grid.Size.x, grid.Size.y];
            TryCollectPoweredTiles(out powered);
            PoweredTileCount = FirstIndex;
            for (int x = FirstIndex; x < grid.Size.x; x++)
            {
                for (int y = FirstIndex; y < grid.Size.y; y++)
                {
                    ElectricityPuzzleTile tile = tilesByCell[x, y];
                    if (tile == null)
                    {
                        continue;
                    }

                    energizedCells[x, y] |= powered[x, y];
                    tile.SetPowered(energizedCells[x, y]);
                    if (energizedCells[x, y])
                    {
                        PoweredTileCount++;
                    }
                }
            }

            UpdateTerminalColors();
            if (!IsSolved && IsSolvedConfiguration())
            {
                IsSolved = true;
                GameManager.Instance?.MarkPuzzleCompleted(puzzleId);
                Solved?.Invoke();
            }
        }

        private bool TryCollectPoweredTiles(out bool[,] powered)
        {
            powered = grid != null ? new bool[grid.Size.x, grid.Size.y] : new bool[0, 0];
            if (grid == null || tilesByCell == null || !IsInside(inputCell))
            {
                return false;
            }

            ElectricityPuzzleTile inputTile = tilesByCell[inputCell.x, inputCell.y];
            if (inputTile == null || !HasPort(inputTile.CurrentPorts, ElectricityPorts.West))
            {
                return true;
            }

            Queue<Vector2Int> frontier = new Queue<Vector2Int>();
            powered[inputCell.x, inputCell.y] = true;
            frontier.Enqueue(inputCell);
            while (frontier.Count > FirstIndex)
            {
                Vector2Int cell = frontier.Dequeue();
                ElectricityPuzzleTile tile = tilesByCell[cell.x, cell.y];
                for (int directionIndex = FirstIndex; directionIndex < PortDirections.Length; directionIndex++)
                {
                    if (!HasPort(tile.CurrentPorts, PortDirections[directionIndex]))
                    {
                        continue;
                    }

                    Vector2Int neighborCell = cell + CellDirections[directionIndex];
                    if (!grid.IsInside(neighborCell) || powered[neighborCell.x, neighborCell.y])
                    {
                        continue;
                    }

                    ElectricityPuzzleTile neighbor = tilesByCell[neighborCell.x, neighborCell.y];
                    if (neighbor == null || !HasPort(neighbor.CurrentPorts, OppositePorts[directionIndex]))
                    {
                        continue;
                    }

                    powered[neighborCell.x, neighborCell.y] = true;
                    frontier.Enqueue(neighborCell);
                }
            }

            return true;
        }

        private void BuildTileLookup()
        {
            if (grid == null)
            {
                return;
            }

            Vector2Int size = grid.Size;
            tilesByCell = new ElectricityPuzzleTile[size.x, size.y];
            if (tiles == null || tiles.Length == FirstIndex)
            {
                tiles = FindObjectsByType<ElectricityPuzzleTile>();
            }

            foreach (ElectricityPuzzleTile tile in tiles)
            {
                if (tile == null || !grid.IsInside(tile.Cell))
                {
                    continue;
                }

                ElectricityPuzzleTile existing = tilesByCell[tile.Cell.x, tile.Cell.y];
                if (existing != null)
                {
                    Debug.LogWarning(string.Format(DuplicateTileWarning, tile.Cell), tile);
                    continue;
                }

                tilesByCell[tile.Cell.x, tile.Cell.y] = tile;
            }
        }

        private void ResolveGrid()
        {
            if (grid == null)
            {
                grid = GetComponent<PuzzleGrid>();
            }
            if (grid == null)
            {
                grid = FindAnyObjectByType<PuzzleGrid>();
            }
            if (grid == null)
            {
                Debug.LogWarning(MissingGridWarning, this);
            }
        }


        private void OnDrawGizmos()
        {
            if (grid == null)
            {
                grid = GetComponent<PuzzleGrid>();
            }
            if (grid == null || !grid.IsInside(inputCell) || !grid.IsInside(outputCell))
            {
                return;
            }

            Vector2 inputCenter = grid.CellToWorld(inputCell);
            Vector2 outputCenter = grid.CellToWorld(outputCell);
            Vector3 inputStart = inputTerminalAnchor != null
                ? inputTerminalAnchor.position
                : new Vector3(inputCenter.x - TerminalReach, inputCenter.y, -0.03f);
            Vector3 inputEdge = new Vector3(inputCenter.x - TerminalEdgeInset, inputCenter.y, inputStart.z);
            Vector3 outputStart = outputTerminalAnchor != null
                ? outputTerminalAnchor.position
                : new Vector3(outputCenter.x + TerminalReach, outputCenter.y, -0.03f);
            Vector3 outputEdge = new Vector3(outputCenter.x + TerminalEdgeInset, outputCenter.y, outputStart.z);
            Gizmos.color = InputColor;
            Gizmos.DrawLine(inputStart, inputEdge);
            Gizmos.color = OutputColor;
            Gizmos.DrawLine(outputEdge, outputStart);
        }

        private void CreateTerminalVisuals()
        {
            if (grid == null)
            {
                return;
            }

            Vector2 inputCenter = grid.CellToWorld(inputCell);
            Vector2 outputCenter = grid.CellToWorld(outputCell);
            Vector3 inputStart = inputTerminalAnchor != null
                ? inputTerminalAnchor.position
                : new Vector3(inputCenter.x - TerminalReach, inputCenter.y, -0.03f);
            Vector3 inputEdge = new Vector3(inputCenter.x - TerminalEdgeInset, inputCenter.y, inputStart.z);
            Vector3 outputStart = outputTerminalAnchor != null
                ? outputTerminalAnchor.position
                : new Vector3(outputCenter.x + TerminalReach, outputCenter.y, -0.03f);
            Vector3 outputEdge = new Vector3(outputCenter.x + TerminalEdgeInset, outputCenter.y, outputStart.z);
            inputTerminal = ConfigureTerminalLine(inputTerminal, inputTerminalAnchor, "InputA", inputStart, inputEdge, InputColor);
            outputTerminal = ConfigureTerminalLine(outputTerminal, outputTerminalAnchor, "OutputB", outputStart, outputEdge, OutputColor);
        }

        private LineRenderer ConfigureTerminalLine(
            LineRenderer renderer, Transform anchor, string objectName, Vector3 start, Vector3 end, Color color)
        {
            if (renderer == null)
            {
                if (anchor != null)
                {
                    renderer = anchor.GetComponent<LineRenderer>();
                    if (renderer == null)
                    {
                        renderer = anchor.gameObject.AddComponent<LineRenderer>();
                    }
                }
                else
                {
                    GameObject terminalObject = new GameObject(objectName);
                    terminalObject.transform.SetParent(transform, false);
                    renderer = terminalObject.AddComponent<LineRenderer>();
                }
            }

            renderer.useWorldSpace = true;
            renderer.positionCount = LinePointCount;
            renderer.startWidth = TerminalLineWidth;
            renderer.endWidth = TerminalLineWidth;
            renderer.sortingOrder = SortingOrder;
            Material material = new Material(GetTerminalMaterial());
            renderer.sharedMaterial = material;
            material.color = color;
            if (objectName == "InputA")
            {
                inputTerminalMaterial = material;
            }
            else
            {
                outputTerminalMaterial = material;
            }
            renderer.startColor = color;
            renderer.endColor = color;
            renderer.enabled = true;
            renderer.SetPosition(FirstIndex, start);
            renderer.SetPosition(FirstIndex + 1, end);
            return renderer;
        }

        private void UpdateTerminalColors()
        {
            ElectricityPuzzleTile inputTile = tilesByCell[inputCell.x, inputCell.y];
            ElectricityPuzzleTile outputTile = tilesByCell[outputCell.x, outputCell.y];
            Color inputColor = inputTile != null && HasPort(inputTile.CurrentPorts, ElectricityPorts.West)
                ? InputColor : InputColor * 0.25f;
            Color outputColor = outputTile != null && HasPort(outputTile.CurrentPorts, ElectricityPorts.East)
                ? OutputColor : OutputColor * 0.25f;
            if (inputTerminal != null)
            {
                inputTerminal.startColor = inputColor;
                inputTerminal.endColor = inputColor;
            }
            if (inputTerminalMaterial != null)
            {
                inputTerminalMaterial.color = inputColor;
            }
            if (outputTerminal != null)
            {
                outputTerminal.startColor = outputColor;
                outputTerminal.endColor = outputColor;
            }
            if (outputTerminalMaterial != null)
            {
                outputTerminalMaterial.color = outputColor;
            }
        }

        private static bool HasPort(ElectricityPorts ports, ElectricityPorts port)
        {
            return (ports & port) != 0;
        }

        private static Material GetTerminalMaterial()
        {
            if (terminalMaterial != null)
            {
                return terminalMaterial;
            }

            Shader shader = Shader.Find(LineShaderName);
            if (shader == null)
            {
                shader = Shader.Find(FallbackLineShaderName);
            }
            if (shader != null)
            {
                terminalMaterial = new Material(shader);
                terminalMaterial.color = Color.white;
            }
            return terminalMaterial;
        }
    }
}
