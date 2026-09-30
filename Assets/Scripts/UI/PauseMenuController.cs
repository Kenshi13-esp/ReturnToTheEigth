using ReturnToTheEigth.Core;
using ReturnToTheEigth.Events;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ReturnToTheEigth.UI
{
    /// <summary>Opens the original pause menu with Escape or gamepad Start and routes its menu actions.</summary>
    [DisallowMultipleComponent]
    public sealed class PauseMenuController : MonoBehaviour
    {
        private const string MainMenuSceneName = "MainMenu";

        [SerializeField] private GameObject pauseDimmer;
        [SerializeField] private GameObject pauseMenuPanel;
        [SerializeField] private GameObject optionsPanel;
        [SerializeField] private GameObject controlsPanel;
        [SerializeField] private GameObject gamepadCursor;
        [SerializeField] private GameObject persistentEventSystem;
        [SerializeField] private VoidEventChannelSO pauseRequestedChannel;
        [SerializeField] private VoidEventChannelSO resumeRequestedChannel;

        private bool isPauseMenuVisible;
        private EventSystem pauseEventSystem;

        private void Awake()
        {
            if (persistentEventSystem != null)
                pauseEventSystem = persistentEventSystem.GetComponent<EventSystem>();

            SetPauseMenuVisible(false);
        }

        private void Update()
        {
            if (isPauseMenuVisible || SceneManager.GetActiveScene().name == MainMenuSceneName)
                return;

            bool escapePressed = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            bool gamepadStartPressed = Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame;
            if (!escapePressed && !gamepadStartPressed)
                return;

            if (GameManager.Instance == null || GameManager.Instance.CurrentGameState == GameState.GameOver)
                return;

            OpenPauseMenu();
        }

        private void LateUpdate()
        {
            if (isPauseMenuVisible)
                KeepGamepadCursorOnTop();
        }

        /// <summary>Closes the pause menu and restores the gameplay state from before pausing.</summary>
        public void ResumeGame()
        {
            if (!isPauseMenuVisible)
                return;

            SetPauseMenuVisible(false);
            resumeRequestedChannel?.RaiseEvent();
        }

        /// <summary>Loads MainMenu after restoring gameplay time and hides the persistent pause interface.</summary>
        public void QuitToMainMenu()
        {
            if (!isPauseMenuVisible)
                return;

            AsyncOperation sceneLoad = SceneManager.LoadSceneAsync(MainMenuSceneName, LoadSceneMode.Single);
            if (sceneLoad == null)
            {
                Debug.LogError($"Could not load the '{MainMenuSceneName}' scene.", this);
                return;
            }

            resumeRequestedChannel?.RaiseEvent();
            SetPauseMenuVisible(false);
            if (persistentEventSystem != null)
                persistentEventSystem.SetActive(false);
        }

        /// <summary>Shows the original options panel while keeping gameplay paused.</summary>
        public void OpenOptionsMenu()
        {
            if (!isPauseMenuVisible || optionsPanel == null)
                return;

            SetActiveIfAssigned(pauseMenuPanel, false);
            SetActiveIfAssigned(controlsPanel, false);
            optionsPanel.SetActive(true);
            SelectFirstInteractable(optionsPanel, "SliderMusic");
            KeepGamepadCursorOnTop();
        }

        /// <summary>Shows the original controls panel while keeping gameplay paused.</summary>
        public void OpenControlsMenu()
        {
            if (!isPauseMenuVisible || controlsPanel == null)
                return;

            SetActiveIfAssigned(pauseMenuPanel, false);
            SetActiveIfAssigned(optionsPanel, false);
            controlsPanel.SetActive(true);
            SelectFirstInteractable(controlsPanel, "ButtonBack");
            KeepGamepadCursorOnTop();
        }

        /// <summary>Returns from options or controls to the original pause menu.</summary>
        public void ReturnToPauseMenu()
        {
            if (!isPauseMenuVisible)
                return;

            SetActiveIfAssigned(optionsPanel, false);
            SetActiveIfAssigned(controlsPanel, false);
            SetActiveIfAssigned(pauseMenuPanel, true);
            SelectFirstInteractable(pauseMenuPanel, "ButtonResumeGame");
            KeepGamepadCursorOnTop();
        }

        private void OpenPauseMenu()
        {
            if (isPauseMenuVisible)
                return;

            if (persistentEventSystem != null)
                persistentEventSystem.SetActive(true);

            SetPauseMenuVisible(true);
            pauseRequestedChannel?.RaiseEvent();
        }

        private void SetPauseMenuVisible(bool isVisible)
        {
            isPauseMenuVisible = isVisible;

            SetActiveIfAssigned(pauseDimmer, isVisible);
            SetActiveIfAssigned(optionsPanel, false);
            SetActiveIfAssigned(controlsPanel, false);
            SetActiveIfAssigned(pauseMenuPanel, isVisible);
            SetActiveIfAssigned(gamepadCursor, isVisible);

            if (isVisible)
                SelectFirstInteractable(pauseMenuPanel, "ButtonResumeGame");
            else if (pauseEventSystem != null)
                pauseEventSystem.SetSelectedGameObject(null);
        }

        private void SelectFirstInteractable(GameObject panel, string preferredName)
        {
            if (panel == null || pauseEventSystem == null || !pauseEventSystem.isActiveAndEnabled)
                return;

            Transform preferredTransform = panel.transform.Find(preferredName);
            Selectable preferredSelectable = preferredTransform != null
                ? preferredTransform.GetComponent<Selectable>()
                : null;

            if (preferredSelectable != null && preferredSelectable.IsInteractable())
            {
                pauseEventSystem.SetSelectedGameObject(preferredSelectable.gameObject);
                return;
            }

            Selectable[] selectables = panel.GetComponentsInChildren<Selectable>(false);
            foreach (Selectable selectable in selectables)
            {
                if (selectable == null || !selectable.IsInteractable())
                    continue;

                pauseEventSystem.SetSelectedGameObject(selectable.gameObject);
                return;
            }
        }

        private void KeepGamepadCursorOnTop()
        {
            if (gamepadCursor != null)
                gamepadCursor.transform.SetAsLastSibling();
        }

        private static void SetActiveIfAssigned(GameObject target, bool isActive)
        {
            if (target != null)
                target.SetActive(isActive);
        }
    }
}
