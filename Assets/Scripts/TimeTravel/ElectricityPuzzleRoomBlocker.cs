using ReturnToTheEigth.Core;
using UnityEngine;

namespace ReturnToTheEigth.TimeTravel
{
    /// <summary>Blocks the Present piano room after the electricity puzzle is completed.</summary>
    [RequireComponent(typeof(BoxCollider2D))]
    [DisallowMultipleComponent]
    public sealed class ElectricityPuzzleRoomBlocker : MonoBehaviour
    {
        private const string ElectricityPuzzleId = "ElectricityPuzzle";

        private BoxCollider2D roomCollider;

        private void Awake()
        {
            roomCollider = GetComponent<BoxCollider2D>();
            roomCollider.isTrigger = false;
            UpdateBlockerState();
        }

        private void OnEnable()
        {
            UpdateBlockerState();
        }

        private void Update()
        {
            UpdateBlockerState();
        }

        private void UpdateBlockerState()
        {
            if (roomCollider == null)
            {
                return;
            }

            GameManager gameManager = GameManager.Instance;
            bool shouldBlockRoom = gameManager != null
                && gameManager.IsPuzzleCompleted(ElectricityPuzzleId);
            if (roomCollider.enabled != shouldBlockRoom)
            {
                roomCollider.enabled = shouldBlockRoom;
            }
        }
    }
}
