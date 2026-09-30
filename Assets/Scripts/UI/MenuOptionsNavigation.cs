using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ReturnToTheEigth.UI
{
    /// <summary>Opens menu panels and restores the main menu when a panel is closed.</summary>
    [DisallowMultipleComponent]
    public sealed class MenuOptionsNavigation : MonoBehaviour
    {
        [SerializeField] private GameObject sourceMenuPanel;
        [SerializeField] private GameObject optionsPanel;
        [SerializeField] private GameObject controlsPanel;
        [SerializeField] private GameObject creditsPanel;
        [SerializeField] private GameObject backgroundDimmer;

        /// <summary>Opens the options panel from the main menu.</summary>
        public void OpenOptions()
        {
            OpenPanel(optionsPanel, "options");
        }

        /// <summary>Opens the controls panel from the main menu.</summary>
        public void OpenControls()
        {
            OpenPanel(controlsPanel, "controls");
        }

        /// <summary>Opens the credits panel from the main menu.</summary>
        public void OpenCredits()
        {
            OpenPanel(creditsPanel, "credits");
        }

        /// <summary>Closes every secondary panel and restores the source menu.</summary>
        public void ReturnToSourceMenu()
        {
            SetActiveIfAssigned(optionsPanel, false);
            SetActiveIfAssigned(controlsPanel, false);
            SetActiveIfAssigned(creditsPanel, false);
            SetActiveIfAssigned(sourceMenuPanel, true);
            SetActiveIfAssigned(backgroundDimmer, false);
            SelectFirstSelectable(sourceMenuPanel);
        }

        /// <summary>Closes the application when the Exit button animation has finished.</summary>
        public void ExitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OpenPanel(GameObject panel, string panelDescription)
        {
            if (panel == null)
            {
                Debug.LogWarning($"MenuOptionsNavigation requires a {panelDescription} panel.", this);
                return;
            }

            SetActiveIfAssigned(sourceMenuPanel, false);
            SetActiveIfAssigned(optionsPanel, false);
            SetActiveIfAssigned(controlsPanel, false);
            SetActiveIfAssigned(creditsPanel, false);
            SetActiveIfAssigned(backgroundDimmer, true);
            panel.SetActive(true);
            SelectFirstSelectable(panel, panel == optionsPanel ? "SliderMusic" : null);
        }

        private static void SelectFirstSelectable(GameObject panel, string preferredSelectableName = null)
        {
            if (panel == null || EventSystem.current == null)
                return;

            if (!string.IsNullOrEmpty(preferredSelectableName))
            {
                Transform preferredTransform = panel.transform.Find(preferredSelectableName);
                Selectable preferredSelectable = preferredTransform != null
                    ? preferredTransform.GetComponent<Selectable>()
                    : null;

                if (preferredSelectable != null && preferredSelectable.IsInteractable())
                {
                    EventSystem.current.SetSelectedGameObject(preferredSelectable.gameObject);
                    return;
                }
            }

            Selectable[] selectableItems = panel.GetComponentsInChildren<Selectable>(false);
            foreach (Selectable selectable in selectableItems)
            {
                if (selectable == null || !selectable.IsInteractable())
                    continue;

                EventSystem.current.SetSelectedGameObject(selectable.gameObject);
                return;
            }
        }

        private static void SetActiveIfAssigned(GameObject target, bool isActive)
        {
            if (target != null)
                target.SetActive(isActive);
        }
    }
}
