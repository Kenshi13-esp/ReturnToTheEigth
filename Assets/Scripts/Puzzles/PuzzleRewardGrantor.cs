using System.Collections.Generic;
using ReturnToTheEigth.Core;
using ReturnToTheEigth.Events;
using UnityEngine;

namespace ReturnToTheEigth.Puzzles
{
    /// <summary>Grants one puzzle's configured item rewards when its solve channel is raised.</summary>
    [DisallowMultipleComponent]
    public sealed class PuzzleRewardGrantor : MonoBehaviour
    {
        private const string MissingSolveChannelWarning = "PuzzleRewardGrantor requires a puzzle solved event channel.";
        private const string InvalidRewardConfigurationWarning = "PuzzleRewardGrantor requires a puzzle ID and at least one configured reward.";
        private const string MissingGameManagerWarning = "Puzzle rewards were not granted because no GameManager exists in the current session.";

        [SerializeField] private string puzzleId;
        [SerializeField] private List<PuzzleReward> rewards = new List<PuzzleReward>();
        [SerializeField] private VoidEventChannelSO puzzleSolvedChannel;
        [SerializeField, TextArea] private string rewardNotificationMessage;

        private void OnEnable()
        {
            if (puzzleSolvedChannel == null)
            {
                Debug.LogWarning(MissingSolveChannelWarning, this);
                return;
            }

            puzzleSolvedChannel.OnEventRaised += HandlePuzzleSolved;
        }

        private void OnDisable()
        {
            if (puzzleSolvedChannel != null)
            {
                puzzleSolvedChannel.OnEventRaised -= HandlePuzzleSolved;
            }
        }

        private void HandlePuzzleSolved()
        {
            if (string.IsNullOrWhiteSpace(puzzleId) || rewards == null || rewards.Count == 0)
            {
                Debug.LogWarning(InvalidRewardConfigurationWarning, this);
                return;
            }

            GameManager gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                Debug.LogWarning(MissingGameManagerWarning, this);
                return;
            }

            if (gameManager.TryGrantPuzzleRewards(puzzleId, rewards) &&
                !string.IsNullOrWhiteSpace(rewardNotificationMessage))
            {
                gameManager.SetPendingRewardNotice(rewardNotificationMessage);
            }
        }
    }
}
