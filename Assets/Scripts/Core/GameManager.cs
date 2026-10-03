using System;
using System.Collections.Generic;
using ReturnToTheEigth.Events;
using ReturnToTheEigth.Puzzles;
using ReturnToTheEigth.TimeTravel;
using UnityEngine;

namespace ReturnToTheEigth.Core
{
    /// <summary>Owns session state; only this object persists across scene loads.</summary>
    [DefaultExecutionOrder(ExecutionOrder)]
    [DisallowMultipleComponent]
    public sealed class GameManager : MonoBehaviour
    {
        private const int ExecutionOrder = -200;
        private const float RunningTimeScale = 1f;
        private const float StoppedTimeScale = 0f;
        private const string InvalidStateWarning = "Ignored an unsupported game state.";
        private const string InvalidInventoryOperationWarning = "Rejected an invalid inventory item ID or quantity.";
        private const string InvalidPuzzleRewardWarning = "Rejected invalid rewards for puzzle ID '{0}'.";
        private const string PuzzleVictoryResourcePath = "Sounds/X/PuzleWin";

        [SerializeField] private GameStateEventChannelSO gameStateChannel;
        [SerializeField] private VoidEventChannelSO pauseRequestedChannel;
        [SerializeField] private VoidEventChannelSO resumeRequestedChannel;
        [SerializeField] private DoorStateEventChannelSO doorStateChannel;
        [SerializeField] private bool persistAcrossScenes = true;
        private AudioClip puzzleVictoryClip;
        private AudioSource puzzleVictoryAudioSource;

        public static GameManager Instance { get; private set; }
        /// <summary>Gets the session door-state channel used by door controllers.</summary>
        public DoorStateEventChannelSO DoorStateChannel => doorStateChannel;
        public GameState CurrentGameState { get; private set; } = GameState.Exploration;
        /// <summary>Raised whenever the current inventory contents change.</summary>
        public event Action InventoryChanged;
        /// <summary>Raised with the item rewards added in one inventory grant.</summary>
        public event Action<IReadOnlyList<PuzzleReward>> ItemsAdded;
        /// <summary>Gets whether the player has examined the family portrait and unlocked timeline travel this session.</summary>
        public bool IsTimelineTravelUnlocked { get; private set; }
        /// <summary>Gets whether the family-frame interaction instruction has already been used this session.</summary>
        public bool HasUsedFamilyFrameInteraction { get; private set; }
        /// <summary>Gets whether the family-frame exit instruction has already been used this session.</summary>
        public bool HasUsedFamilyFrameExit { get; private set; }
        private GameState stateBeforePause = GameState.Exploration;
        private Vector3 pendingPlayerReturnPosition;
        private TimelineEra pendingPlayerReturnEra = TimelineEra.Present;
        private bool hasPendingPlayerReturnPosition;
        private string pendingRewardNotice;
        private readonly HashSet<string> completedPuzzleIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> itemCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly HashSet<string> puzzlesWithGrantedRewards = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<PuzzleReward> pendingAcquisitionRewards = new List<PuzzleReward>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (persistAcrossScenes)
            {
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
            }

            puzzleVictoryClip = Resources.Load<AudioClip>(PuzzleVictoryResourcePath);
            if (puzzleVictoryClip != null)
            {
                puzzleVictoryAudioSource = GetComponent<AudioSource>();
                if (puzzleVictoryAudioSource == null)
                {
                    puzzleVictoryAudioSource = gameObject.AddComponent<AudioSource>();
                }
                puzzleVictoryAudioSource.playOnAwake = false;
                puzzleVictoryAudioSource.spatialBlend = 0f;
                AudioSettingsController.RegisterSoundSource(puzzleVictoryAudioSource);
            }

            doorStateChannel?.ResetSession();
            ApplyState(GameState.Exploration);
        }

        private void OnEnable()
        {
            if (Instance != this)
            {
                return;
            }

            if (pauseRequestedChannel != null)
            {
                pauseRequestedChannel.OnEventRaised += PauseGame;
            }
            if (resumeRequestedChannel != null)
            {
                resumeRequestedChannel.OnEventRaised += ResumeGame;
            }
        }

        private void OnDisable()
        {
            if (pauseRequestedChannel != null)
            {
                pauseRequestedChannel.OnEventRaised -= PauseGame;
            }
            if (resumeRequestedChannel != null)
            {
                resumeRequestedChannel.OnEventRaised -= ResumeGame;
            }
        }

        private void OnDestroy()
        {
            if (Instance != this)
            {
                return;
            }

            Time.timeScale = RunningTimeScale;
            Instance = null;
        }

        /// <summary>Clears session progress and restores exploration before starting a new run from the main menu.</summary>
        public void ResetForNewGameSession()
        {
            IsTimelineTravelUnlocked = false;
            HasUsedFamilyFrameInteraction = false;
            HasUsedFamilyFrameExit = false;
            stateBeforePause = GameState.Exploration;
            pendingPlayerReturnPosition = Vector3.zero;
            pendingPlayerReturnEra = TimelineEra.Present;
            hasPendingPlayerReturnPosition = false;
            pendingRewardNotice = null;
            completedPuzzleIds.Clear();
            itemCounts.Clear();
            puzzlesWithGrantedRewards.Clear();
            pendingAcquisitionRewards.Clear();
            doorStateChannel?.ResetSession();
            InventoryChanged?.Invoke();
            ApplyState(GameState.Exploration);
        }


        /// <summary>Unlocks timeline travel after the player first examines the family portrait.</summary>
        public void UnlockTimelineTravel()
        {
            IsTimelineTravelUnlocked = true;
        }

        /// <summary>Records that the player has used the first family-frame interaction instruction this session.</summary>
        public void MarkFamilyFrameInteractionUsed()
        {
            HasUsedFamilyFrameInteraction = true;
        }

        /// <summary>Records that the player has used the family-frame exit instruction this session.</summary>
        public void MarkFamilyFrameExitUsed()
        {
            HasUsedFamilyFrameExit = true;
        }

        /// <summary>Returns whether the puzzle with the supplied stable identifier has been completed this session.</summary>
        public bool IsPuzzleCompleted(string puzzleId)
        {
            return !string.IsNullOrWhiteSpace(puzzleId) && completedPuzzleIds.Contains(puzzleId);
        }

        /// <summary>Marks a puzzle completed once per session and optionally plays its victory sound on its first completion.</summary>
        public void MarkPuzzleCompleted(string puzzleId, bool playVictorySound = true)
        {
            if (string.IsNullOrWhiteSpace(puzzleId) || !completedPuzzleIds.Add(puzzleId))
            {
                return;
            }

            if (playVictorySound && puzzleVictoryAudioSource != null && puzzleVictoryClip != null)
            {
                puzzleVictoryAudioSource.PlayOneShot(puzzleVictoryClip);
            }
        }

        /// <summary>Returns whether the session inventory contains at least one item with the supplied identifier.</summary>
        public bool HasItem(string itemId)
        {
            return GetItemCount(itemId) > 0;
        }

        /// <summary>Returns the session inventory count for an item, or zero for an invalid or absent identifier.</summary>
        public int GetItemCount(string itemId)
        {
            return !string.IsNullOrWhiteSpace(itemId) && itemCounts.TryGetValue(itemId, out int count)
                ? count : 0;
        }

        /// <summary>Adds a positive quantity of an item to the session inventory and optionally queues an acquisition popup.</summary>
        public bool AddItem(string itemId, int amount = 1, bool showAcquisitionPopup = true)
        {
            if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
            {
                Debug.LogWarning(InvalidInventoryOperationWarning, this);
                return false;
            }

            int currentCount = GetItemCount(itemId);
            if (amount > int.MaxValue - currentCount)
            {
                Debug.LogWarning(InvalidInventoryOperationWarning, this);
                return false;
            }

            itemCounts[itemId] = currentCount + amount;
            PuzzleReward addedReward = new PuzzleReward(itemId, amount);
            if (showAcquisitionPopup)
            {
                pendingAcquisitionRewards.Add(addedReward);
            }
            InventoryChanged?.Invoke();
            ItemsAdded?.Invoke(new[] { addedReward });
            return true;
        }

        /// <summary>Consumes a positive quantity of an item when the session inventory has enough.</summary>
        public bool TryConsumeItem(string itemId, int amount = 1)
        {
            if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
            {
                Debug.LogWarning(InvalidInventoryOperationWarning, this);
                return false;
            }

            if (!itemCounts.TryGetValue(itemId, out int currentCount) || currentCount < amount)
            {
                return false;
            }

            int remainingCount = currentCount - amount;
            if (remainingCount == 0)
            {
                itemCounts.Remove(itemId);
            }
            else
            {
                itemCounts[itemId] = remainingCount;
            }

            InventoryChanged?.Invoke();
            return true;
        }

        /// <summary>Grants a puzzle's configured rewards once per session after validating the complete reward list.</summary>
        public bool TryGrantPuzzleRewards(string puzzleId, IReadOnlyList<PuzzleReward> rewards)
        {
            if (string.IsNullOrWhiteSpace(puzzleId) || rewards == null || rewards.Count == 0 ||
                puzzlesWithGrantedRewards.Contains(puzzleId))
            {
                return false;
            }

            Dictionary<string, long> additions = new Dictionary<string, long>(StringComparer.Ordinal);
            for (int index = 0; index < rewards.Count; index++)
            {
                PuzzleReward reward = rewards[index];
                if (reward == null || string.IsNullOrWhiteSpace(reward.ItemId) || reward.Amount <= 0)
                {
                    Debug.LogWarning(string.Format(InvalidPuzzleRewardWarning, puzzleId), this);
                    return false;
                }

                additions.TryGetValue(reward.ItemId, out long currentAddition);
                long totalAddition = currentAddition + reward.Amount;
                long currentCount = GetItemCount(reward.ItemId);
                if (totalAddition > int.MaxValue || currentCount + totalAddition > int.MaxValue)
                {
                    Debug.LogWarning(string.Format(InvalidPuzzleRewardWarning, puzzleId), this);
                    return false;
                }

                additions[reward.ItemId] = totalAddition;
            }

            foreach (KeyValuePair<string, long> addition in additions)
            {
                itemCounts[addition.Key] = GetItemCount(addition.Key) + (int)addition.Value;
            }

            puzzlesWithGrantedRewards.Add(puzzleId);
            for (int rewardIndex = 0; rewardIndex < rewards.Count; rewardIndex++)
            {
                PuzzleReward reward = rewards[rewardIndex];
                pendingAcquisitionRewards.Add(new PuzzleReward(reward.ItemId, reward.Amount));
            }

            InventoryChanged?.Invoke();
            ItemsAdded?.Invoke(rewards);
            return true;
        }

        /// <summary>Stores a configured reward message for one-time display after returning to exploration.</summary>
        public bool SetPendingRewardNotice(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            pendingRewardNotice = message;
            return true;
        }

        /// <summary>Consumes a pending reward message once, clearing it from the session state.</summary>
        public bool TryConsumePendingRewardNotice(out string message)
        {
            message = pendingRewardNotice;
            if (string.IsNullOrWhiteSpace(pendingRewardNotice))
            {
                pendingRewardNotice = null;
                return false;
            }

            pendingRewardNotice = null;
            return true;
        }

        /// <summary>Consumes all item rewards queued for the next acquisition popup.</summary>
        public bool TryConsumePendingAcquisitionRewards(out List<PuzzleReward> rewards)
        {
            if (pendingAcquisitionRewards.Count == 0)
            {
                rewards = new List<PuzzleReward>();
                return false;
            }

            rewards = new List<PuzzleReward>(pendingAcquisitionRewards);
            pendingAcquisitionRewards.Clear();
            return true;
        }

        /// <summary>Returns undisplayed item rewards to the queue after an acquisition popup is interrupted.</summary>
        public void QueuePendingAcquisitionRewards(IReadOnlyList<PuzzleReward> rewards)
        {
            if (rewards == null)
            {
                return;
            }

            for (int rewardIndex = 0; rewardIndex < rewards.Count; rewardIndex++)
            {
                PuzzleReward reward = rewards[rewardIndex];
                if (reward != null && !string.IsNullOrWhiteSpace(reward.ItemId) && reward.Amount > 0)
                {
                    pendingAcquisitionRewards.Add(new PuzzleReward(reward.ItemId, reward.Amount));
                }
            }
        }


        /// <summary>Stores the player's physics position and always returns them to the Present era after a puzzle.</summary>
        public void SetPendingPlayerReturnState(GameObject player)
        {
            if (player == null)
            {
                return;
            }

            Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
            Vector3 playerPosition = playerBody != null
                ? new Vector3(playerBody.position.x, playerBody.position.y, player.transform.position.z)
                : player.transform.position;
            pendingPlayerReturnPosition = playerPosition;
            pendingPlayerReturnEra = TimelineEra.Present;
            hasPendingPlayerReturnPosition = true;
        }

        /// <summary>Reads the pending return timeline without consuming the saved player state.</summary>
        public bool TryGetPendingPlayerReturnEra(out TimelineEra era)
        {
            era = pendingPlayerReturnEra;
            return hasPendingPlayerReturnPosition;
        }

        /// <summary>Consumes the stored exploration position once, if a puzzle return is pending.</summary>
        public bool TryConsumePendingPlayerReturnPosition(out Vector3 position)
        {
            position = pendingPlayerReturnPosition;
            if (!hasPendingPlayerReturnPosition)
            {
                return false;
            }

            hasPendingPlayerReturnPosition = false;
            pendingPlayerReturnPosition = Vector3.zero;
            pendingPlayerReturnEra = TimelineEra.Present;
            return true;
        }

        /// <summary>Pauses gameplay and remembers the previous state while an item-acquisition modal is open.</summary>
        public void PauseForItemAcquisition()
        {
            if (CurrentGameState != GameState.GameOver && CurrentGameState != GameState.Paused)
                SetGameState(GameState.Paused);
        }

        /// <summary>Restores the game state that was active before the item-acquisition modal.</summary>
        public void ResumeAfterItemAcquisition()
        {
            if (CurrentGameState == GameState.Paused)
                SetGameState(stateBeforePause);
        }

        /// <summary>Changes session state and notifies input/UI systems through the state channel.</summary>
        public void SetGameState(GameState newGameState)
        {
            if (!System.Enum.IsDefined(typeof(GameState), newGameState))
            {
                Debug.LogWarning(InvalidStateWarning, this);
                return;
            }
            if (CurrentGameState == newGameState)
            {
                return;
            }
            if (newGameState == GameState.Paused)
            {
                stateBeforePause = CurrentGameState;
            }

            ApplyState(newGameState);
        }

        private void ApplyState(GameState newState)
        {
            CurrentGameState = newState;
            Time.timeScale = newState == GameState.Paused || newState == GameState.GameOver
                ? StoppedTimeScale : RunningTimeScale;
            gameStateChannel?.RaiseGameStateChanged(newState);
        }

        private void PauseGame()
        {
            if (CurrentGameState != GameState.GameOver)
            {
                SetGameState(GameState.Paused);
            }
        }

        private void ResumeGame()
        {
            if (CurrentGameState == GameState.Paused)
            {
                SetGameState(stateBeforePause);
            }
        }
    }
}
