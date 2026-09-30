using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace ReturnToTheEigth.UI
{
    /// <summary>Configures pointer input so menus work with a mouse or a gamepad-driven virtual mouse.</summary>
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
        private const string VirtualCursorMoveActionName = "Menu Virtual Cursor Move";
        private const string VirtualCursorClickActionName = "Menu Virtual Cursor Click";
        private const string GamepadLeftStickBinding = "<Gamepad>/leftStick";
        private const string GamepadSouthButtonBinding = "<Gamepad>/buttonSouth";

        [SerializeField] private InputActionAsset actionsAsset;
        [SerializeField] private InputSystemUIInputModule inputModule;
        [SerializeField] private bool pointerOnlyControl;

        private readonly List<InputAction> ownedVirtualMouseActions = new List<InputAction>();

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

            bool refreshInputModule = inputModule.isActiveAndEnabled;
            if (refreshInputModule)
                inputModule.enabled = false;

            inputModule.actionsAsset = actionsAsset;
            inputModule.pointerBehavior = UIPointerBehavior.AllPointersAsIs;
            inputModule.point = CreateActionReference(PointActionName);
            inputModule.leftClick = CreateActionReference(ClickActionName);
            inputModule.middleClick = CreateActionReference(MiddleClickActionName);
            inputModule.rightClick = CreateActionReference(RightClickActionName);
            inputModule.scrollWheel = CreateActionReference(ScrollWheelActionName);
            inputModule.move = pointerOnlyControl ? null : CreateActionReference(NavigateActionName);
            inputModule.submit = pointerOnlyControl ? null : CreateActionReference(SubmitActionName);
            inputModule.cancel = CreateActionReference(CancelActionName);

            ConfigureVirtualMouseInputs();

            if (refreshInputModule)
                inputModule.enabled = true;
        }

        private void OnDestroy()
        {
            foreach (InputAction inputAction in ownedVirtualMouseActions)
            {
                if (inputAction.enabled)
                    inputAction.Disable();

                inputAction.Dispose();
            }

            ownedVirtualMouseActions.Clear();
        }

        private void ConfigureVirtualMouseInputs()
        {
            VirtualMouseInput[] virtualMouseInputs = Object.FindObjectsByType<VirtualMouseInput>(
                FindObjectsInactive.Include);

            foreach (VirtualMouseInput virtualMouseInput in virtualMouseInputs)
            {
                InputAction moveAction = new InputAction(
                    VirtualCursorMoveActionName,
                    InputActionType.Value,
                    binding: GamepadLeftStickBinding,
                    expectedControlType: "Vector2");
                InputAction clickAction = new InputAction(
                    VirtualCursorClickActionName,
                    InputActionType.Button,
                    binding: GamepadSouthButtonBinding,
                    expectedControlType: "Button");

                ownedVirtualMouseActions.Add(moveAction);
                ownedVirtualMouseActions.Add(clickAction);
                virtualMouseInput.stickAction = new InputActionProperty(moveAction);
                virtualMouseInput.leftButtonAction = new InputActionProperty(clickAction);
            }
        }

        private InputActionReference CreateActionReference(string actionName)
        {
            InputAction action = actionsAsset.FindAction(actionName, true);
            return InputActionReference.Create(action);
        }
    }
}
