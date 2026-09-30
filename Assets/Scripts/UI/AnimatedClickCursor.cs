using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ReturnToTheEigth.UI
{
    /// <summary>Displays a software cursor that follows either the hardware mouse or a gamepad-driven virtual mouse.</summary>
    [DisallowMultipleComponent]
    public sealed class AnimatedClickCursor : MonoBehaviour
    {
        private const float DefaultFrameDurationSeconds = 0.1f;
        private const float MinimumFrameDurationSeconds = 0.01f;

        [SerializeField] private Sprite idleCursorSprite;
        [SerializeField] private Sprite[] clickAnimationSprites;
        [SerializeField] private float frameDurationSeconds = DefaultFrameDurationSeconds;

        private Image softwareCursorGraphic;
        private RectTransform softwareCursorTransform;
        private RectTransform softwareCursorCanvas;
        private Camera softwareCursorCanvasCamera;
        private VirtualMouseInput virtualMouseInput;
        private Vector2 previousHardwareMousePosition;
        private bool hasPreviousHardwareMousePosition;
        private bool usesSoftwareCursor;
        private Coroutine clickAnimationCoroutine;
        private bool hardwareCursorWasVisible;
        private bool hasCapturedHardwareCursorVisibility;

        private void Awake()
        {
            softwareCursorGraphic = GetComponent<Image>();
            softwareCursorTransform = GetComponent<RectTransform>();
            virtualMouseInput = GetComponent<VirtualMouseInput>();

            Canvas cursorCanvas = GetComponentInParent<Canvas>();
            softwareCursorCanvas = cursorCanvas != null ? cursorCanvas.transform as RectTransform : null;
            usesSoftwareCursor = softwareCursorGraphic != null && softwareCursorCanvas != null;

            if (cursorCanvas != null && cursorCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                softwareCursorCanvasCamera = cursorCanvas.worldCamera;

            if (idleCursorSprite == null && softwareCursorGraphic != null)
                idleCursorSprite = softwareCursorGraphic.sprite;

            if (softwareCursorGraphic != null)
            {
                softwareCursorGraphic.raycastTarget = false;
                softwareCursorGraphic.enabled = true;
                if (idleCursorSprite != null)
                    softwareCursorGraphic.sprite = idleCursorSprite;
            }

            if (clickAnimationSprites == null)
                clickAnimationSprites = new Sprite[0];
        }

        private void OnEnable()
        {
            hardwareCursorWasVisible = Cursor.visible;
            hasCapturedHardwareCursorVisibility = true;

            if (!usesSoftwareCursor)
                return;

            Cursor.visible = false;
            Mouse hardwareMouse = GetCurrentHardwareMouse();
            Vector2 cursorScreenPosition = hardwareMouse != null
                ? hardwareMouse.position.ReadValue()
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            if (hardwareMouse != null)
            {
                previousHardwareMousePosition = cursorScreenPosition;
                hasPreviousHardwareMousePosition = true;
            }

            SynchronizeSoftwareCursorWithMouse(ClampToScreen(cursorScreenPosition));
        }

        private void Start()
        {
            if (virtualMouseInput == null || virtualMouseInput.virtualMouse == null || softwareCursorTransform == null)
                return;

            Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(
                softwareCursorCanvasCamera,
                softwareCursorTransform.position);
            InputState.Change(virtualMouseInput.virtualMouse.position, screenPosition);
        }

        private void Update()
        {
            Mouse hardwareMouse = GetCurrentHardwareMouse();
            if (hardwareMouse != null)
            {
                Vector2 hardwareMousePosition = hardwareMouse.position.ReadValue();
                if (!hasPreviousHardwareMousePosition || hardwareMousePosition != previousHardwareMousePosition)
                {
                    previousHardwareMousePosition = hardwareMousePosition;
                    hasPreviousHardwareMousePosition = true;
                    if (usesSoftwareCursor)
                        SynchronizeSoftwareCursorWithMouse(ClampToScreen(hardwareMousePosition));
                }

                if (hardwareMouse.leftButton.wasPressedThisFrame)
                    PlayClickAnimation();
            }

            if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
                PlayClickAnimation();
        }

        private void OnDisable()
        {
            if (clickAnimationCoroutine != null)
            {
                StopCoroutine(clickAnimationCoroutine);
                clickAnimationCoroutine = null;
            }

            if (softwareCursorGraphic != null && idleCursorSprite != null)
                softwareCursorGraphic.sprite = idleCursorSprite;

            if (hasCapturedHardwareCursorVisibility)
            {
                Cursor.visible = hardwareCursorWasVisible;
                hasCapturedHardwareCursorVisibility = false;
            }

            hasPreviousHardwareMousePosition = false;
        }

        private Mouse GetCurrentHardwareMouse()
        {
            Mouse currentMouse = Mouse.current;
            if (currentMouse == null)
                return null;

            Mouse virtualMouse = virtualMouseInput != null ? virtualMouseInput.virtualMouse : null;
            return currentMouse == virtualMouse ? null : currentMouse;
        }

        private static Vector2 ClampToScreen(Vector2 screenPosition)
        {
            float maximumX = Mathf.Max(Screen.width - 1, 0);
            float maximumY = Mathf.Max(Screen.height - 1, 0);
            return new Vector2(
                Mathf.Clamp(screenPosition.x, 0, maximumX),
                Mathf.Clamp(screenPosition.y, 0, maximumY));
        }

        private void SynchronizeSoftwareCursorWithMouse(Vector2 screenPosition)
        {
            if (softwareCursorTransform == null || softwareCursorCanvas == null)
                return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    softwareCursorCanvas,
                    screenPosition,
                    softwareCursorCanvasCamera,
                    out Vector2 localPosition))
                return;

            Vector2 canvasOffset = Vector2.Scale(softwareCursorCanvas.rect.size, softwareCursorCanvas.pivot);
            softwareCursorTransform.anchoredPosition = localPosition + canvasOffset;
        }

        private void PlayClickAnimation()
        {
            if (clickAnimationCoroutine != null || softwareCursorGraphic == null || !HasUsableClickFrame())
                return;

            clickAnimationCoroutine = StartCoroutine(PlayClickAnimationFrames());
        }

        private IEnumerator PlayClickAnimationFrames()
        {
            float frameDuration = Mathf.Max(frameDurationSeconds, MinimumFrameDurationSeconds);

            foreach (Sprite frame in clickAnimationSprites)
            {
                if (frame == null)
                    continue;

                softwareCursorGraphic.sprite = frame;
                yield return new WaitForSecondsRealtime(frameDuration);
            }

            softwareCursorGraphic.sprite = idleCursorSprite;
            clickAnimationCoroutine = null;
        }

        private bool HasUsableClickFrame()
        {
            if (clickAnimationSprites == null)
                return false;

            foreach (Sprite frame in clickAnimationSprites)
            {
                if (frame != null)
                    return true;
            }

            return false;
        }
    }
}
