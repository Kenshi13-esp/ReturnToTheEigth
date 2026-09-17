using ReturnToTheEigth.Events;
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
