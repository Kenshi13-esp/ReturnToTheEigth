using ReturnToTheEigth.Core;
using ReturnToTheEigth.Events;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ReturnToTheEigth.Puzzles
{
    /// <summary>Returns to Hall when the color-track puzzle is solved.</summary>
    public sealed class ColorPuzzleFeedback : MonoBehaviour
    {
        private const string HallSceneName = "Hall";

        [SerializeField] private VoidEventChannelSO puzzleSolvedChannel;
        private bool isSolved;

        private void OnEnable()
        {
            if (puzzleSolvedChannel != null) puzzleSolvedChannel.OnEventRaised += HandlePuzzleSolved;
        }

        private void OnDisable()
        {
            if (puzzleSolvedChannel != null) puzzleSolvedChannel.OnEventRaised -= HandlePuzzleSolved;
        }

        private void Start()
        {
            TrackBoard board = FindAnyObjectByType<TrackBoard>();
            if (board != null && board.IsSolved) HandlePuzzleSolved();
        }

        private void HandlePuzzleSolved()
        {
            if (isSolved) return;

            isSolved = true;
            GameManager.Instance?.SetGameState(GameState.Exploration);
            SceneManager.LoadSceneAsync(HallSceneName, LoadSceneMode.Single);
        }
    }
}
