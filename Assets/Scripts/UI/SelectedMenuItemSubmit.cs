using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ReturnToTheEigth.UI
{
    /// <summary>Submits the selected UI control in this EventSystem when E is pressed.</summary>
    [DisallowMultipleComponent]
    public sealed class SelectedMenuItemSubmit : MonoBehaviour
    {
        private EventSystem menuEventSystem;

        private void Awake()
        {
            menuEventSystem = GetComponent<EventSystem>();
        }

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame)
                return;

            if (menuEventSystem == null || !menuEventSystem.isActiveAndEnabled)
                return;

            GameObject selectedItem = menuEventSystem.currentSelectedGameObject;
            if (selectedItem == null || !selectedItem.activeInHierarchy)
                return;

            BaseEventData submitEvent = new BaseEventData(menuEventSystem);
            ExecuteEvents.Execute(
                selectedItem,
                submitEvent,
                ExecuteEvents.submitHandler);
        }
    }
}
