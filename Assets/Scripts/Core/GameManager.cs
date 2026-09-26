using System;
using System.Collections.Generic;
using ReturnToTheEigth.Events;
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

        [SerializeField] private GameStateEventChannelSO gameStateChannel;
        [SerializeField] private VoidEventChannelSO pauseRequestedChannel;
        [SerializeField] private VoidEventChannelSO resumeRequestedChannel;
        [SerializeField] private DoorStateEventChannelSO doorStateChannel;
        [SerializeField] private bool persistAcrossScenes = true;

        public static GameManager Instance { get; private set; }
        public GameState CurrentGameState { get; private set; } = GameState.Exploration;
        private GameState stateBeforePause = GameState.Exploration;
        private Vector3 pendingPlayerReturnPosition;
        private TimelineEra pendingPlayerReturnEra = TimelineEra.Present;
        private bool hasPendingPlayerReturnPosition;
        private readonly HashSet<string> completedPuzzleIds = new HashSet<string>(StringComparer.Ordinal);

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

        /// <summary>Returns whether the puzzle with the supplied stable identifier has been completed this session.</summary>
        public bool IsPuzzleCompleted(string puzzleId)
        {
            return !string.IsNullOrWhiteSpace(puzzleId) && completedPuzzleIds.Contains(puzzleId);
        }

        /// <summary>Marks the puzzle with the supplied stable identifier as completed for the rest of this session.</summary>
        public void MarkPuzzleCompleted(string puzzleId)
        {
            if (!string.IsNullOrWhiteSpace(puzzleId))
            {
                completedPuzzleIds.Add(puzzleId);
            }
        }

        /// <summary>Stores the player's exploration position and timeline so they can be restored after a puzzle returns.</summary>
        public void SetPendingPlayerReturnState(Vector3 position, TimelineEra era)
        {
            pendingPlayerReturnPosition = position;
            pendingPlayerReturnEra = era;
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
