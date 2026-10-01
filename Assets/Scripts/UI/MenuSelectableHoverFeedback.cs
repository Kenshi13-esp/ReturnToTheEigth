using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ReturnToTheEigth.UI
{
    /// <summary>Highlights focused and hovered menu controls across every loaded scene.</summary>
    [DisallowMultipleComponent]
    public sealed class MenuSelectableHoverFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        private const float FocusScaleMultiplier = 1.08f;
        private Selectable selectable;
        private Vector3 originalLocalScale;
        private bool isPointerOver;
        private bool isSelected;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeForLoadedScenes()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            InstallOnSceneSelectables();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            InstallOnSceneSelectables();
        }

        private static void InstallOnSceneSelectables()
        {
            Selectable[] selectables = Object.FindObjectsByType<Selectable>(FindObjectsInactive.Include);
            for (int index = 0; index < selectables.Length; index++)
            {
                Selectable selectable = selectables[index];
                if (selectable == null || selectable.GetComponent<MenuButtonBreakageAnimation>() != null)
                {
                    continue;
                }

                if (selectable.GetComponent<MenuSelectableHoverFeedback>() == null)
                {
                    selectable.gameObject.AddComponent<MenuSelectableHoverFeedback>();
                }
            }
        }

        private void Awake()
        {
            selectable = GetComponent<Selectable>();
            originalLocalScale = transform.localScale;
        }

        private void OnEnable()
        {
            isSelected = selectable != null && EventSystem.current != null
                && EventSystem.current.currentSelectedGameObject == gameObject;
            ApplyFocusFeedback();
        }

        private void OnDisable()
        {
            isPointerOver = false;
            isSelected = false;
            transform.localScale = originalLocalScale;
        }

        /// <summary>Highlights this control while the pointer is over it.</summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            isPointerOver = true;
            ApplyFocusFeedback();
        }

        /// <summary>Removes pointer highlight unless this control is still selected.</summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            isPointerOver = false;
            ApplyFocusFeedback();
        }

        /// <summary>Highlights this control when keyboard or controller navigation selects it.</summary>
        public void OnSelect(BaseEventData eventData)
        {
            isSelected = true;
            ApplyFocusFeedback();
        }

        /// <summary>Removes selection highlight unless the pointer remains over this control.</summary>
        public void OnDeselect(BaseEventData eventData)
        {
            isSelected = false;
            ApplyFocusFeedback();
        }

        private void ApplyFocusFeedback()
        {
            if (selectable == null)
            {
                return;
            }

            bool isFocused = selectable.IsInteractable() && (isPointerOver || isSelected);
            transform.localScale = isFocused
                ? originalLocalScale * FocusScaleMultiplier
                : originalLocalScale;
        }
    }
}
