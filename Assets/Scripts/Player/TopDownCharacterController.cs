using ReturnToTheEigth.Core;
using ReturnToTheEigth.Events;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ReturnToTheEigth.Player
{
    /// <summary>Moves a gravity-free 2D body on XY and publishes time-shift requests.</summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    [DisallowMultipleComponent]
    public sealed class TopDownCharacterController : MonoBehaviour
    {
        private const string MoveActionPath = "Player/Move";
        private const string TimeShiftActionPath = "Player/TimeShift";
        private const string MissingInputError = "The 2D player requires Player/Move and Player/TimeShift actions.";
        private const float DefaultMovementSpeed = 0.3f;
        private const float InputThreshold = 0.0001f;
        private const float UnitMagnitude = 1f;
        private const float Zero = 0f;

        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private VoidEventChannelSO timeShiftRequestedChannel;
        [SerializeField] private GameStateEventChannelSO gameStateChannel;
        [SerializeField, Min(Zero)] private float movementSpeed = DefaultMovementSpeed;
        private Rigidbody2D body;
        private InputAction moveAction;
        private InputAction timeShiftAction;
        private bool movementEnabled = true;
        public Vector2 FacingDirection { get; private set; } = Vector2.down;
        private bool AllowsGameplay => gameStateChannel == null || gameStateChannel.CurrentState == GameState.Exploration;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.gravityScale = Zero;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            InputAction sourceMove = inputActions != null ? inputActions.FindAction(MoveActionPath) : null;
            InputAction sourceShift = inputActions != null ? inputActions.FindAction(TimeShiftActionPath) : null;
            if (sourceMove == null || sourceShift == null)
            {
                Debug.LogError(MissingInputError, this);
                enabled = false;
                return;
            }
            moveAction = sourceMove.Clone();
            timeShiftAction = sourceShift.Clone();
        }

        private void OnEnable()
        {
            moveAction?.Enable();
            if (timeShiftAction != null)
            {
                timeShiftAction.performed += HandleTimeShift;
                timeShiftAction.Enable();
            }
        }

        private void OnDisable()
        {
            moveAction?.Disable();
            if (timeShiftAction != null)
            {
                timeShiftAction.performed -= HandleTimeShift;
                timeShiftAction.Disable();
            }
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }
        }

        private void OnDestroy()
        {
            moveAction?.Dispose();
            timeShiftAction?.Dispose();
        }

        private void FixedUpdate()
        {
            Vector2 direction = movementEnabled && AllowsGameplay && moveAction != null
                ? Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), UnitMagnitude) : Vector2.zero;
            if (direction.sqrMagnitude > InputThreshold)
            {
                FacingDirection = direction.normalized;
            }
            body.linearVelocity = direction * movementSpeed;
        }

        /// <summary>Enables or stops movement without changing the independent interaction/time-travel bindings.</summary>
        public void SetMovementEnabled(bool isEnabled)
        {
            movementEnabled = isEnabled;
            if (!isEnabled && body != null)
            {
                body.linearVelocity = Vector2.zero;
            }
        }

        private void HandleTimeShift(InputAction.CallbackContext context)
        {
            if (AllowsGameplay)
            {
                timeShiftRequestedChannel?.RaiseEvent();
            }
        }
    }
}
