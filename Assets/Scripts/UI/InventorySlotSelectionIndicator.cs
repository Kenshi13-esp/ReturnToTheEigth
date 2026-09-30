using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ReturnToTheEigth.UI
{
    /// <summary>Keeps the inventory frame aligned with the selected inventory slot.</summary>
    [DisallowMultipleComponent]
    public sealed class InventorySlotSelectionIndicator : MonoBehaviour
    {
        private const string SelectorObjectName = "InventorySelector";

        [SerializeField] private RectTransform selector;

        private void Awake()
        {
            if (selector == null)
            {
                Transform selectorTransform = transform.Find(SelectorObjectName);
                selector = selectorTransform as RectTransform;
            }

            if (selector == null)
            {
                Debug.LogWarning("InventorySlotSelectionIndicator requires an InventorySelector RectTransform.", this);
                enabled = false;
                return;
            }

            selector.SetAsLastSibling();
        }

        private void LateUpdate()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
                return;

            GameObject selectedGameObject = eventSystem.currentSelectedGameObject;
            if (selectedGameObject == null || selectedGameObject.transform.parent != transform ||
                selectedGameObject.GetComponent<Button>() == null)
            {
                return;
            }

            RectTransform selectedSlot = selectedGameObject.transform as RectTransform;
            if (selectedSlot != null)
                selector.anchoredPosition = selectedSlot.anchoredPosition;
        }
    }
}
