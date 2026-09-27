using ReturnToTheEigth.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ReturnToTheEigth.Puzzles.Electricity
{
    /// <summary>Returns to Hall when the complete electricity circuit is formed.</summary>
    [DisallowMultipleComponent]
    public sealed class ElectricityPuzzleFeedback : MonoBehaviour
    {
        private const string HallSceneName = "Hall";

        [SerializeField] private ElectricityPuzzleBoard board;
        private bool isReturningToHall;

        private void Awake()
        {
            if (board == null) board = FindAnyObjectByType<ElectricityPuzzleBoard>();
        }

        private void OnEnable()
        {
            if (board != null) board.Solved += HandlePuzzleSolved;
        }

        private void OnDisable()
        {
            if (board != null) board.Solved -= HandlePuzzleSolved;
        }

        private void Start()
        {
            if (board != null && board.IsSolved) HandlePuzzleSolved();
        }

        private void HandlePuzzleSolved()
        {
            if (isReturningToHall) return;

            isReturningToHall = true;
            GameManager.Instance?.SetGameState(GameState.Exploration);
            SceneManager.LoadSceneAsync(HallSceneName, LoadSceneMode.Single);
        }
    }
}
