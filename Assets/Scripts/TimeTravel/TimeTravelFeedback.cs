using ReturnToTheEigth.Core;
using ReturnToTheEigth.Events;
using ReturnToTheEigth.Interaction;
using ReturnToTheEigth.Puzzles;
using UnityEngine;

namespace ReturnToTheEigth.TimeTravel
{
    /// <summary>Compact 2D prototype HUD with controls and time-travel rejection feedback.</summary>
    public sealed class TimeTravelFeedback : MonoBehaviour
    {
        private const string PresentTitle = "PRESENTE / 32 BITS";
        private const string PastTitle = "PASADO / 8 BITS";
        private const string ControlsText = "WASD: mover\nR: cambiar época\nE: interactuar";
        private const string BlockedText = "Viaje bloqueado:\nel destino está ocupado.";
        private const string InteractPrefix = "E: ";
        private const string PrototypeText = "Prototipo 2D\nMuros con colisión";
        private const float FeedbackDuration = 2f;
        private const float RewardNoticeDuration = 3.5f;
        private const float PanelX = 8f;
        private const float PanelY = 8f;
        private const float PanelWidth = 174f;
        private const float PanelHeight = 170f;
        private const float Inset = 8f;
        private const float TitleHeight = 24f;
        private const float ControlsHeight = 58f;
        private const float MessageHeight = 68f;
        private const int FontSize = 12;
        private const float InteractionPanelWidthRatio = 0.5f;
        private const float InteractionPanelHeight = 84f;
        private const float InteractionPanelBottomInset = 20f;
        private const float InteractionPanelHorizontalPadding = 20f;
        private const float InteractionPanelVerticalPadding = 10f;
        private const int InteractionFontSize = 32;
        private const string InteractionFontResourceName = "TypographySilver";
        private static readonly Color PastColor = new Color(1f, 0.85f, 0.45f, 1f);
        private static readonly Color PresentColor = new Color(0.5f, 0.87f, 1f, 1f);
        private static readonly Color RewardNoticeColor = new Color(0.65f, 1f, 0.65f, 1f);
        [SerializeField] private TimelineEventChannelSO timelineChangedChannel;
        [SerializeField] private VoidEventChannelSO transitionBlockedChannel;
        [SerializeField] private PlayerInteraction playerInteraction;
        private TimelineEra currentEra;
        private float blockedUntil = float.NegativeInfinity;
        private float rewardNoticeUntil = float.NegativeInfinity;
        private string activeRewardNotice;
        private GUIStyle labelStyle;
        private GUIStyle interactionTextStyle;
        private Texture2D interactionPanelTexture;
        private Font interactionFont;

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
            if (labelStyle == null) labelStyle = new GUIStyle(GUI.skin.label) { fontSize = FontSize, wordWrap = true };
            GUI.Box(new Rect(PanelX, PanelY, PanelWidth, PanelHeight), GUIContent.none);
            float x = PanelX + Inset;
            float y = PanelY + Inset;
            float width = PanelWidth - Inset - Inset;
            labelStyle.normal.textColor = currentEra == TimelineEra.Past ? PastColor : PresentColor;
            GUI.Label(new Rect(x, y, width, TitleHeight), currentEra == TimelineEra.Past ? PastTitle : PresentTitle, labelStyle);
            labelStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(x, y + TitleHeight, width, ControlsHeight), ControlsText, labelStyle);
            bool blocked = Time.unscaledTime < blockedUntil;
            bool rewardNoticeActive = Time.unscaledTime < rewardNoticeUntil
                && !string.IsNullOrWhiteSpace(activeRewardNotice);
            bool showingRewardNotice = !blocked && rewardNoticeActive;
            labelStyle.normal.textColor = blocked ? Color.yellow : showingRewardNotice ? RewardNoticeColor : Color.white;
            string message = blocked ? BlockedText : showingRewardNotice ? activeRewardNotice : PrototypeText;
            GUI.Label(new Rect(x, y + TitleHeight + ControlsHeight, width, MessageHeight), message, labelStyle);
            DrawInteractionPrompt(rewardNoticeActive);
        }

        private void DrawInteractionPrompt(bool showingRewardNotice)
        {
            InteractableBase target = playerInteraction != null ? playerInteraction.CurrentInteractable : null;
            bool showingInteractionPrompt = target != null && target.isActiveAndEnabled;
            if (!showingRewardNotice && !showingInteractionPrompt)
            {
                return;
            }

            EnsureInteractionPromptStyles();
            float panelWidth = Screen.width * InteractionPanelWidthRatio;
            float panelX = (Screen.width - panelWidth) * 0.5f;
            float panelY = Screen.height - InteractionPanelHeight - InteractionPanelBottomInset;
            Rect panelRect = new Rect(panelX, panelY, panelWidth, InteractionPanelHeight);
            Color previousGuiColor = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(panelRect, interactionPanelTexture, ScaleMode.StretchToFill, false);

            string message;
            if (showingRewardNotice)
            {
                message = activeRewardNotice;
            }
            else
            {
                bool isWrongSideDoor = target is DoorController door && door.IsPlayerOnWrongSide;
                bool shouldShowPrefix = true;
                if (target is DoorController promptDoor)
                {
                    shouldShowPrefix = promptDoor.ShouldShowInteractionPrefix;
                }
                message = isWrongSideDoor || !shouldShowPrefix
                    ? target.InteractionPrompt
                    : InteractPrefix + target.InteractionPrompt;
            }

            Rect textRect = new Rect(panelX + InteractionPanelHorizontalPadding,
                panelY + InteractionPanelVerticalPadding,
                panelWidth - InteractionPanelHorizontalPadding * 2f,
                InteractionPanelHeight - InteractionPanelVerticalPadding * 2f);
            GUI.Label(textRect, message, interactionTextStyle);
            GUI.color = previousGuiColor;
        }

        private void EnsureInteractionPromptStyles()
        {
            if (interactionPanelTexture == null)
            {
                interactionPanelTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                interactionPanelTexture.SetPixel(0, 0, Color.black);
                interactionPanelTexture.Apply();
            }

            if (interactionFont == null)
            {
                interactionFont = Resources.Load<Font>(InteractionFontResourceName);
            }

            if (interactionTextStyle == null)
            {
                interactionTextStyle = new GUIStyle(GUI.skin.label)
                {
                    font = interactionFont,
                    fontSize = InteractionFontSize,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true,
                    clipping = TextClipping.Clip
                };
                interactionTextStyle.normal.textColor = Color.white;
                interactionTextStyle.hover.textColor = Color.white;
                interactionTextStyle.active.textColor = Color.white;
                interactionTextStyle.focused.textColor = Color.white;
                interactionTextStyle.onNormal.textColor = Color.white;
                interactionTextStyle.onHover.textColor = Color.white;
                interactionTextStyle.onActive.textColor = Color.white;
                interactionTextStyle.onFocused.textColor = Color.white;
            }
            else
            {
                interactionTextStyle.font = interactionFont;
                interactionTextStyle.fontSize = InteractionFontSize;
                interactionTextStyle.normal.textColor = Color.white;
                interactionTextStyle.hover.textColor = Color.white;
                interactionTextStyle.active.textColor = Color.white;
                interactionTextStyle.focused.textColor = Color.white;
                interactionTextStyle.onNormal.textColor = Color.white;
                interactionTextStyle.onHover.textColor = Color.white;
                interactionTextStyle.onActive.textColor = Color.white;
                interactionTextStyle.onFocused.textColor = Color.white;
            }
        }

        private void OnDestroy()
        {
            if (interactionPanelTexture != null)
            {
                Destroy(interactionPanelTexture);
            }
        }

        private void HandleTimelineChanged(TimelineEra era) { currentEra = era; blockedUntil = float.NegativeInfinity; }
        private void HandleTransitionBlocked() { blockedUntil = Time.unscaledTime + FeedbackDuration; }
    }
}
