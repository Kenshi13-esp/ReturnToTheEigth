using ReturnToTheEigth.Core;
using ReturnToTheEigth.Events;
using ReturnToTheEigth.Interaction;
using ReturnToTheEigth.UI;
using UnityEngine;

namespace ReturnToTheEigth.TimeTravel
{
    /// <summary>Shows first-time family-frame guidance, pending reward notices and blocked time-travel feedback.</summary>
    public sealed class TimeTravelFeedback : MonoBehaviour
    {
        private const string BlockedText = "Viaje bloqueado: el destino está ocupado.";
        private const string FrameInteractionInstruction = "Pulsa {0} para interactuar con el cuadro.";
        private const float FeedbackDuration = 2f;
        private const float RewardNoticeDuration = 3.5f;
        private const float PanelWidthRatio = 0.82f;
        private const float PanelHeight = 116f;
        private const float PanelBottomInset = 24f;
        [SerializeField] private TimelineEventChannelSO timelineChangedChannel;
        [SerializeField] private VoidEventChannelSO transitionBlockedChannel;
        [SerializeField] private PlayerInteraction playerInteraction;
        private float blockedUntil = float.NegativeInfinity;
        private float rewardNoticeUntil = float.NegativeInfinity;
        private string activeRewardNotice;

        private void OnEnable()
        {
            if (timelineChangedChannel != null) timelineChangedChannel.OnTimelineChanged += HandleTimelineChanged;
            if (transitionBlockedChannel != null) transitionBlockedChannel.OnEventRaised += HandleTransitionBlocked;
            GameManager gameManager = GameManager.Instance;
            if (gameManager != null && gameManager.TryConsumePendingRewardNotice(out string pendingNotice))
            {
                activeRewardNotice = pendingNotice;
                rewardNoticeUntil = Time.unscaledTime + RewardNoticeDuration;
            }
        }

        private void OnDisable()
        {
            if (timelineChangedChannel != null) timelineChangedChannel.OnTimelineChanged -= HandleTimelineChanged;
            if (transitionBlockedChannel != null) transitionBlockedChannel.OnEventRaised -= HandleTransitionBlocked;
        }

        private void OnGUI()
        {
            if (FamilyPhotoFrameInteractable.HidesExplorationHud) return;
            bool blocked = Time.unscaledTime < blockedUntil;
            bool rewardNoticeActive = Time.unscaledTime < rewardNoticeUntil
                && !string.IsNullOrWhiteSpace(activeRewardNotice);
            InteractableBase target = playerInteraction != null ? playerInteraction.CurrentInteractable : null;
            bool showingFrameInteractionInstruction = target is FamilyPhotoFrameInteractable frame
                && frame.isActiveAndEnabled && frame.ShouldShowInitialInteractionHint;
            if (!blocked && !rewardNoticeActive && !showingFrameInteractionInstruction) return;

            string message;
            if (blocked)
            {
                message = BlockedText;
            }
            else if (rewardNoticeActive)
            {
                message = activeRewardNotice;
            }
            else
            {
                message = string.Format(FrameInteractionInstruction, InputPromptUtility.InteractControlLabel);
            }

            float panelWidth = Screen.width * PanelWidthRatio;
            Rect panelRect = new Rect((Screen.width - panelWidth) * 0.5f,
                Screen.height - PanelHeight - PanelBottomInset, panelWidth, PanelHeight);
            GameTextGUI.DrawLabel(panelRect, message, TextAnchor.MiddleCenter);
        }

        private void HandleTimelineChanged(TimelineEra era) { blockedUntil = float.NegativeInfinity; }
        private void HandleTransitionBlocked() { blockedUntil = Time.unscaledTime + FeedbackDuration; }
    }
}
