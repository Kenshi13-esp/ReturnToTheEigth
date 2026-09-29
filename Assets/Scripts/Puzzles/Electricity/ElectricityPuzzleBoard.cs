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
        private const string ElectricityRewardNotice = "Has conseguido un fragmento de la foto familiar.";
        private const float Zero = 0f;
        private const int FirstIndex = 0;
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
        private static readonly PuzzleReward[] CompletionRewards =
        {
            new PuzzleReward(PuzzleItemIds.ElectricityPuzzlePaintingFragment, 1)
        };

        [SerializeField] private PuzzleGrid grid;
        [SerializeField] private ElectricityPuzzleTile[] tiles;
        [SerializeField] private Vector2Int inputCell = new Vector2Int(0, 0);
        [SerializeField] private Vector2Int outputCell = new Vector2Int(3, 3);
        [SerializeField] private GameObject straightWirePrefab;
        [SerializeField] private GameObject cornerWirePrefab;
        [SerializeField] private GameObject teeWirePrefab;
        [SerializeField] private GameObject crossWirePrefab;
        [SerializeField] private string puzzleId = PuzzleId;

        private ElectricityPuzzleTile[,] tilesByCell;

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
            ConfigureTileWireVisuals();
        }

        private void Start()
        {
            IsSolved = GameManager.Instance != null && GameManager.Instance.IsPuzzleCompleted(puzzleId);
            RefreshPowerAndSolveState();
        }

        /// <summary>Returns whether the supplied cell is inside the configured board.</summary>
        public bool IsInside(Vector2Int cell)
        {
            return grid != null && grid.IsInside(cell);
        }

        /// <summary>Sets the selection highlight on the tile at the supplied board cell.</summary>
        public void SetTileSelected(Vector2Int cell, bool selected)
        {
            if (!IsInside(cell) || tilesByCell == null)
            {
                return;
            }

            ElectricityPuzzleTile tile = tilesByCell[cell.x, cell.y];
            tile?.SetSelected(selected);
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

                    tile.SetPowered(powered[x, y]);
                    if (powered[x, y])
                    {
                        PoweredTileCount++;
                    }
                }
            }

            if (!IsSolved && IsSolvedConfiguration())
            {
                IsSolved = true;
                PuzzleVictoryAudio.Play();
                GameManager gameManager = GameManager.Instance;
                if (gameManager != null)
                {
                    gameManager.MarkPuzzleCompleted(puzzleId);
                    if (gameManager.TryGrantPuzzleRewards(puzzleId, CompletionRewards))
                    {
                        gameManager.SetPendingRewardNotice(ElectricityRewardNotice);
                    }
                }
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

        private void ConfigureTileWireVisuals()
        {
            if (tilesByCell == null)
            {
                return;
            }

            foreach (ElectricityPuzzleTile tile in tilesByCell)
            {
                tile?.ConfigureWireVisuals(straightWirePrefab, cornerWirePrefab, teeWirePrefab, crossWirePrefab);
            }
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


        private static bool HasPort(ElectricityPorts ports, ElectricityPorts port)
        {
            return (ports & port) != 0;
        }
    }
}
