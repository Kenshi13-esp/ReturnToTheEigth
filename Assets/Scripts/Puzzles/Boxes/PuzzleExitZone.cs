using ReturnToTheEigth.Core;
using ReturnToTheEigth.Events;
using UnityEngine;

namespace ReturnToTheEigth.Puzzles
{
    /// <summary>Trigger cell that marks the puzzle as solved the first time the player steps onto it.</summary>
    [RequireComponent(typeof(BoxCollider2D))]
    [DisallowMultipleComponent]
    public sealed class PuzzleExitZone : MonoBehaviour
    {
        private const string PlayerLayerName = "Player";
        private const string SolvedLog = "Puzzle de cajas resuelto: el jugador ha alcanzado la salida.";
        private const string DefaultPuzzleId = "BoxPuzzle";

        [SerializeField] private VoidEventChannelSO puzzleSolvedChannel;
        [SerializeField] private LayerMask playerLayers;
        [SerializeField] private string puzzleId = DefaultPuzzleId;

        public bool IsSolved { get; private set; }

        private void Awake()
        {
            IsSolved = GameManager.Instance != null && GameManager.Instance.IsPuzzleCompleted(puzzleId);
            GetComponent<BoxCollider2D>().enabled = !IsSolved;
        }

        private void Start()
        {
            if (IsSolved) DisablePuzzleInteractions();
        }

        private void Reset()
        {
            playerLayers = LayerMask.GetMask(PlayerLayerName);
            GetComponent<BoxCollider2D>().isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (IsSolved || !IsPlayer(other)) return;
            IsSolved = true;
            GetComponent<BoxCollider2D>().enabled = false;
            GameManager.Instance?.MarkPuzzleCompleted(puzzleId);
            DisablePuzzleInteractions();
            Debug.Log(SolvedLog, this);
            puzzleSolvedChannel?.RaiseEvent();
        }

        private void DisablePuzzleInteractions()
        {
            PushableBox[] boxes = FindObjectsByType<PushableBox>();
            foreach (PushableBox box in boxes)
            {
                box.enabled = false;
            }
        }

        private bool IsPlayer(Collider2D other)
        {
            return other != null && (playerLayers.value & (1 << other.gameObject.layer)) != 0;
        }
    }
}
