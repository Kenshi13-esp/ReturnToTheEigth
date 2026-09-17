using System;
using ReturnToTheEigth.Core;
using UnityEngine;

namespace ReturnToTheEigth.Events
{
    /// <summary>Retains the current game state for listeners enabled after the state changes.</summary>
    [CreateAssetMenu(menuName = MenuPath, fileName = DefaultFileName)]
    public sealed class GameStateEventChannelSO : ScriptableObject
    {
        private const string MenuPath = "Return To The Eigth/Events/Game State Channel";
        private const string DefaultFileName = "GameStateChannel";
        [NonSerialized] private GameState currentState = GameState.Exploration;

        public GameState CurrentState => currentState;
        public event Action<GameState> OnGameStateChanged;

        /// <summary>Stores and broadcasts a game-state change.</summary>
        public void RaiseGameStateChanged(GameState newState)
        {
            currentState = newState;
            OnGameStateChanged?.Invoke(newState);
        }
    }
}
