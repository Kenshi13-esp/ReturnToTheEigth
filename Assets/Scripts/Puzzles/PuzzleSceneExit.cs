using ReturnToTheEigth.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ReturnToTheEigth.Puzzles
{
    /// <summary>Returns from an unsolved puzzle to Hall when the configured exit action is performed.</summary>
    [DisallowMultipleComponent]
    public sealed class PuzzleSceneExit : MonoBehaviour
    {
        private const string ExitActionPath = "Player/PuzzleExit";
        private const string MissingExitActionError = "PuzzleSceneExit requires the Player/PuzzleExit input action.";
        private const string HallSceneName = "Hall";

        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string itemIdRefundedOnExit;
        private InputAction exitAction;
        private bool isExiting;

        private void Awake()
        {
            InputAction source = inputActions != null ? inputActions.FindAction(ExitActionPath) : null;
            if (source == null)
            {
                Debug.LogError(MissingExitActionError, this);
                enabled = false;
                return;
            }

            exitAction = source.Clone();
        }

        private void OnEnable()
        {
            exitAction?.Enable();
        }

        private void OnDisable()
        {
            exitAction?.Disable();
        }

        private void OnDestroy()
        {
            exitAction?.Dispose();
        }


        private void Update()
        {
            if (isExiting || exitAction == null || !exitAction.WasPerformedThisFrame()) return;

            isExiting = true;
            GameManager gameManager = GameManager.Instance;
            if (gameManager != null)
            {
                gameManager.SetGameState(GameState.Exploration);
                if (!string.IsNullOrWhiteSpace(itemIdRefundedOnExit)) gameManager.AddItem(itemIdRefundedOnExit);
            }
            SceneManager.LoadSceneAsync(HallSceneName, LoadSceneMode.Single);
        }
    }
}
