using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace ReturnToTheEigth.UI
{
    /// <summary>Assigns the project's UI input actions to the menu EventSystem module.</summary>
    [RequireComponent(typeof(InputSystemUIInputModule))]
    [DisallowMultipleComponent]
    public sealed class MenuInputSystemSetup : MonoBehaviour
    {
        private const string PointActionName = "UI/Point";
        private const string ClickActionName = "UI/Click";
        private const string MiddleClickActionName = "UI/MiddleClick";
        private const string RightClickActionName = "UI/RightClick";
        private const string ScrollWheelActionName = "UI/ScrollWheel";
        private const string NavigateActionName = "UI/Navigate";
        private const string SubmitActionName = "UI/Submit";
        private const string CancelActionName = "UI/Cancel";

        [SerializeField] private InputActionAsset actionsAsset;
        [SerializeField] private InputSystemUIInputModule inputModule;

        private void Awake()
        {
            if (inputModule == null)
                inputModule = GetComponent<InputSystemUIInputModule>();

            if (actionsAsset == null || inputModule == null)
            {
                Debug.LogWarning("MenuInputSystemSetup requires an InputActionAsset and InputSystemUIInputModule.", this);
                enabled = false;
                return;
            }

            inputModule.actionsAsset = actionsAsset;
            inputModule.point = CreateActionReference(PointActionName);
            inputModule.leftClick = CreateActionReference(ClickActionName);
            inputModule.middleClick = CreateActionReference(MiddleClickActionName);
            inputModule.rightClick = CreateActionReference(RightClickActionName);
            inputModule.scrollWheel = CreateActionReference(ScrollWheelActionName);
            inputModule.move = CreateActionReference(NavigateActionName);
            inputModule.submit = CreateActionReference(SubmitActionName);
            inputModule.cancel = CreateActionReference(CancelActionName);
        }

        private InputActionReference CreateActionReference(string actionName)
        {
            InputAction action = actionsAsset.FindAction(actionName, true);
            return InputActionReference.Create(action);
        }
    }
}
