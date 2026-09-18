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

        [SerializeField] private VoidEventChannelSO puzzleSolvedChannel;
        [SerializeField] private LayerMask playerLayers;

        public bool IsSolved { get; private set; }

        private void Reset()
        {
            playerLayers = LayerMask.GetMask(PlayerLayerName);
            GetComponent<BoxCollider2D>().isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (IsSolved || !IsPlayer(other)) return;
            IsSolved = true;
            Debug.Log(SolvedLog, this);
            puzzleSolvedChannel?.RaiseEvent();
        }

        private bool IsPlayer(Collider2D other)
        {
            return other != null && (playerLayers.value & (1 << other.gameObject.layer)) != 0;
        }
    }
}
