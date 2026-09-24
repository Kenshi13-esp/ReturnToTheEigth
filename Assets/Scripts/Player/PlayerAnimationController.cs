using ReturnToTheEigth.Events;
using ReturnToTheEigth.TimeTravel;
using UnityEngine;

namespace ReturnToTheEigth.Player
{
    /// <summary>Drives era-specific player animation from movement and facing direction.</summary>
    [RequireComponent(typeof(Animator), typeof(TopDownCharacterController), typeof(Rigidbody2D))]
    [DisallowMultipleComponent]
    public sealed class PlayerAnimationController : MonoBehaviour
    {
        private const string MoveXParameter = "MoveX";
        private const string MoveYParameter = "MoveY";
        private const string IsMovingParameter = "IsMoving";
        private const string SpeedParameter = "Speed";
        private const float MovementThreshold = 0.01f;
        private const float MovementThresholdSquared = MovementThreshold * MovementThreshold;
        private const float Zero = 0f;
        private const float UnitMagnitude = 1f;

        private static readonly int MoveXHash = Animator.StringToHash(MoveXParameter);
        private static readonly int MoveYHash = Animator.StringToHash(MoveYParameter);
        private static readonly int IsMovingHash = Animator.StringToHash(IsMovingParameter);
        private static readonly int SpeedHash = Animator.StringToHash(SpeedParameter);

        [SerializeField] private TopDownCharacterController characterController;
        [SerializeField] private Animator animator;
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private TimelineEventChannelSO timelineChangedChannel;
        [SerializeField] private RuntimeAnimatorController presentAnimatorController;
        [SerializeField] private RuntimeAnimatorController pastAnimatorController;

        private Vector2 lastFacingDirection = Vector2.down;

        private void Awake()
        {
            if (characterController == null)
                characterController = GetComponent<TopDownCharacterController>();
            if (animator == null)
                animator = GetComponent<Animator>();
            if (body == null)
                body = GetComponent<Rigidbody2D>();

            if (characterController == null || animator == null || body == null)
            {
                Debug.LogWarning("PlayerAnimationController requires an Animator, TopDownCharacterController, and Rigidbody2D.", this);
                enabled = false;
                return;
            }

            lastFacingDirection = characterController.FacingDirection.normalized;
            if (lastFacingDirection.sqrMagnitude <= MovementThresholdSquared)
                lastFacingDirection = Vector2.down;
        }

        private void OnEnable()
        {
            if (timelineChangedChannel != null)
                timelineChangedChannel.OnTimelineChanged += HandleTimelineChanged;

            ConfigureForEra(TimelineEra.Present);
        }

        private void OnDisable()
        {
            if (timelineChangedChannel != null)
                timelineChangedChannel.OnTimelineChanged -= HandleTimelineChanged;
        }

        private void Update()
        {
            if (animator == null || !animator.enabled || animator.runtimeAnimatorController == null || body == null)
                return;

            Vector2 velocity = body.linearVelocity;
            bool isMoving = velocity.sqrMagnitude > MovementThresholdSquared;
            if (isMoving)
            {
                Vector2 facingDirection = characterController != null
                    ? characterController.FacingDirection
                    : velocity.normalized;
                if (facingDirection.sqrMagnitude > MovementThresholdSquared)
                    lastFacingDirection = facingDirection.normalized;
            }

            animator.SetFloat(MoveXHash, lastFacingDirection.x);
            animator.SetFloat(MoveYHash, lastFacingDirection.y);
            animator.SetBool(IsMovingHash, isMoving);
            animator.SetFloat(SpeedHash, isMoving ? velocity.magnitude : Zero);
        }

        /// <summary>Sets the direction retained by the idle animation.</summary>
        public void SetFacingDirection(Vector2 direction)
        {
            if (direction.sqrMagnitude <= MovementThresholdSquared)
                return;

            lastFacingDirection = Vector2.ClampMagnitude(direction, UnitMagnitude);
            if (animator != null && animator.enabled && animator.runtimeAnimatorController != null)
            {
                animator.SetFloat(MoveXHash, lastFacingDirection.x);
                animator.SetFloat(MoveYHash, lastFacingDirection.y);
            }
        }

        private void HandleTimelineChanged(TimelineEra era)
        {
            ConfigureForEra(era);
        }

        private void ConfigureForEra(TimelineEra era)
        {
            if (animator == null)
                return;

            RuntimeAnimatorController eraController = era == TimelineEra.Present
                ? presentAnimatorController
                : pastAnimatorController;
            if (eraController == null)
            {
                animator.enabled = false;
                return;
            }

            animator.runtimeAnimatorController = eraController;
            animator.enabled = true;
            animator.SetFloat(MoveXHash, lastFacingDirection.x);
            animator.SetFloat(MoveYHash, lastFacingDirection.y);
            animator.SetBool(IsMovingHash, false);
            animator.SetFloat(SpeedHash, Zero);
        }
    }
}
