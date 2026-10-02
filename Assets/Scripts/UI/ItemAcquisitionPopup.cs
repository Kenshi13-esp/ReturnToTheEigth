using System;
using System.Collections;
using System.Collections.Generic;
using ReturnToTheEigth.Core;
using ReturnToTheEigth.Puzzles;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ReturnToTheEigth.UI
{
    /// <summary>Shows a pausing item-acquisition screen and exposes item presentation data to the inventory UI.</summary>
    [DisallowMultipleComponent]
    public sealed class ItemAcquisitionPopup : MonoBehaviour
    {
        private const string ContinuePrompt = "Press E / X to accept";
        private const string ChangeItemPrompt = "Left / Right  Select item";
        private const string MultipleItemsTitle = "ITEMS ACQUIRED";
        private const string SingleItemTitle = "ITEM ACQUIRED";
        private const string MotherFragmentDescription = "A fragment of the family portrait showing the mother.";
        private const string FatherFragmentDescription = "A fragment of the family portrait showing the father.";
        private const string SisterFragmentDescription = "A fragment of the family portrait showing the sister.";
        private const string EdwardFragmentDescription = "A fragment of the family portrait showing Edward.";
        private const string HallSceneName = "Hall";
        private const float OverlayAlpha = 0.7f;
        private const float PopupWidthRatio = 0.75f;
        private const float PopupHeightRatio = 0.74f;
        private const float PopupTitleHeightRatio = 0.12f;
        private const float PopupGridTopRatio = 0.15f;
        private const float PopupGridHeightRatio = 0.53f;
        private const float PopupDescriptionTopRatio = 0.71f;
        private const float PopupDescriptionHeightRatio = 0.18f;
        private const float PopupControlsTopRatio = 0.92f;
        private const float PopupHorizontalPaddingRatio = 0.05f;
        private const float PopupCardInsetRatio = 0.025f;
        private const float PopupCardIconHeightRatio = 0.69f;
        private const float PopupCardNameHeightRatio = 0.23f;
        private const int BorderThickness = 2;
        private const int MaximumItemsPerRow = 3;
        private const int FirstItemIndex = 0;

        private static readonly string[] InventoryItemOrder =
        {
            PuzzleItemIds.ColorTrackPuzzlePaintingFragment,
            PuzzleItemIds.ChessPuzzlePaintingFragment,
            PuzzleItemIds.PianoPuzzlePaintingFragment,
            PuzzleItemIds.ElectricityPuzzlePaintingFragment,
            PuzzleItemIds.ChessKnightPiece,
            PuzzleItemIds.KitchenLeftDoorKey,
            PuzzleItemIds.PianoSheetMusic
        };

        private static readonly Color OverlayColor = new Color(0f, 0f, 0f, OverlayAlpha);
        private static readonly Color PanelColor = new Color(0.055f, 0.055f, 0.075f, 0.97f);
        private static readonly Color BorderColor = new Color(0.72f, 0.7f, 0.68f, 0.72f);
        private static readonly Color CardColor = new Color(0.15f, 0.15f, 0.17f, 0.95f);
        private static readonly Color SelectedCardColor = new Color(0.29f, 0.27f, 0.29f, 1f);
        private static readonly Color TextColor = new Color(0.91f, 0.91f, 0.89f, 1f);
        private static readonly Color SubtleTextColor = new Color(0.78f, 0.79f, 0.8f, 0.95f);

        [SerializeField] private Sprite motherPhotoSprite;
        [SerializeField] private Sprite fatherPhotoSprite;
        [SerializeField] private Sprite sisterPhotoSprite;
        [SerializeField] private Sprite edwardPhotoSprite;
        [SerializeField] private Sprite chessKnightSprite;
        [SerializeField] private Sprite kitchenKeySprite;
        [SerializeField] private Sprite pianoSheetMusicSprite;

        private readonly List<AcquiredItem> acquiredItems = new List<AcquiredItem>();
        private GameManager gameManager;
        private Coroutine pendingDisplayRoutine;
        private int selectedItemIndex;
        private bool hasReleasedAcceptInput;

        /// <summary>Gets the active popup instance, if the current session has one.</summary>
        public static ItemAcquisitionPopup Instance { get; private set; }

        /// <summary>Gets whether the item-acquisition modal currently blocks gameplay.</summary>
        public static bool IsOpen => Instance != null && Instance.acquiredItems.Count > 0;

        /// <summary>Gets the stable display order used by the inventory, including the four portrait fragments.</summary>
        public static IReadOnlyList<string> DisplayOrder => InventoryItemOrder;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                enabled = false;
                return;
            }

            Instance = this;
        }

        private void OnEnable()
        {
            if (Instance != null && Instance != this)
            {
                enabled = false;
                return;
            }

            Instance = this;
            gameManager = GameManager.Instance;
            SceneManager.sceneLoaded += HandleSceneLoaded;

            TryShowPendingAcquisitions();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            if (pendingDisplayRoutine != null)
            {
                StopCoroutine(pendingDisplayRoutine);
                pendingDisplayRoutine = null;
            }

            if (acquiredItems.Count > 0)
            {
                List<PuzzleReward> undisplayedRewards = new List<PuzzleReward>();
                for (int itemIndex = 0; itemIndex < acquiredItems.Count; itemIndex++)
                {
                    AcquiredItem item = acquiredItems[itemIndex];
                    undisplayedRewards.Add(new PuzzleReward(item.ItemId, item.Amount));
                }

                gameManager?.QueuePendingAcquisitionRewards(undisplayedRewards);
                acquiredItems.Clear();
                gameManager?.ResumeAfterItemAcquisition();
            }

            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (gameManager == null)
                gameManager = GameManager.Instance;

            if (!IsOpen)
            {
                TryShowPendingAcquisitions();
                if (!IsOpen)
                    return;
            }

            if (gameManager != null && gameManager.CurrentGameState != GameState.Paused)
                gameManager.PauseForItemAcquisition();

            if (!hasReleasedAcceptInput)
            {
                if (!IsAcceptInputHeld())
                    hasReleasedAcceptInput = true;

                return;
            }

            if (WasAcceptInputPressed())
            {
                ClosePopup();
                return;
            }

            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;
            if (acquiredItems.Count <= 1)
                return;

            bool nextItemPressed = keyboard != null
                    && (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame
                        || keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
                || gamepad != null
                    && (gamepad.dpad.right.wasPressedThisFrame || gamepad.dpad.down.wasPressedThisFrame);
            bool previousItemPressed = keyboard != null
                    && (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame
                        || keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
                || gamepad != null
                    && (gamepad.dpad.left.wasPressedThisFrame || gamepad.dpad.up.wasPressedThisFrame);

            if (nextItemPressed)
                selectedItemIndex = (selectedItemIndex + 1) % acquiredItems.Count;
            else if (previousItemPressed)
                selectedItemIndex = (selectedItemIndex - 1 + acquiredItems.Count) % acquiredItems.Count;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != HallSceneName || pendingDisplayRoutine != null)
                return;

            pendingDisplayRoutine = StartCoroutine(TryShowPendingAfterSceneLoad());
        }

        private IEnumerator TryShowPendingAfterSceneLoad()
        {
            yield return null;
            pendingDisplayRoutine = null;
            TryShowPendingAcquisitions();
        }

        private void OnGUI()
        {
            if (!IsOpen)
                return;

            int previousDepth = GUI.depth;
            Color previousColor = GUI.color;
            GUI.depth = -100;
            DrawSolidRect(new Rect(0f, 0f, Screen.width, Screen.height), OverlayColor);

            Rect safeArea = GameTextGUI.GetSafeAreaRect();
            float popupWidth = safeArea.width * PopupWidthRatio;
            float popupHeight = safeArea.height * PopupHeightRatio;
            Rect popupRect = new Rect(safeArea.center.x - popupWidth * 0.5f,
                safeArea.center.y - popupHeight * 0.5f, popupWidth, popupHeight);
            DrawPopup(popupRect);

            GUI.color = previousColor;
            GUI.depth = previousDepth;
        }

        /// <summary>Gets the icon, localized name and short description for a stable inventory item ID.</summary>
        public bool TryGetItemPresentation(string itemId, out Sprite icon, out string displayName, out string description)
        {
            icon = null;
            displayName = string.Empty;
            description = string.Empty;

            switch (itemId)
            {
                case PuzzleItemIds.ColorTrackPuzzlePaintingFragment:
                    icon = motherPhotoSprite;
                    displayName = "Mother's Portrait";
                    description = MotherFragmentDescription;
                    return true;
                case PuzzleItemIds.ChessPuzzlePaintingFragment:
                    icon = fatherPhotoSprite;
                    displayName = "Father's Portrait";
                    description = FatherFragmentDescription;
                    return true;
                case PuzzleItemIds.PianoPuzzlePaintingFragment:
                    icon = sisterPhotoSprite;
                    displayName = "Sister's Portrait";
                    description = SisterFragmentDescription;
                    return true;
                case PuzzleItemIds.ElectricityPuzzlePaintingFragment:
                    icon = edwardPhotoSprite;
                    displayName = "Edward's Portrait Fragment";
                    description = EdwardFragmentDescription;
                    return true;
                case PuzzleItemIds.ChessKnightPiece:
                    icon = chessKnightSprite;
                    displayName = "Chess Knight";
                    description = "A chess knight piece. It may help complete the board.";
                    return true;
                case PuzzleItemIds.KitchenLeftDoorKey:
                    icon = kitchenKeySprite;
                    displayName = "Antique Key";
                    description = "An antique key that unlocks a door in the mansion.";
                    return true;
                case PuzzleItemIds.PianoSheetMusic:
                    icon = pianoSheetMusicSprite;
                    displayName = "Family Sheet Music";
                    description = "A family sheet of music for the old piano.";
                    return true;
                default:
                    return false;
            }
        }

        private void TryShowPendingAcquisitions()
        {
            if (IsOpen)
                return;

            if (gameManager == null)
                gameManager = GameManager.Instance;

            if (gameManager == null
                || gameManager.CurrentGameState != GameState.Exploration
                || SceneManager.GetActiveScene().name != HallSceneName
                || !gameManager.TryConsumePendingAcquisitionRewards(out List<PuzzleReward> pendingRewards))
            {
                return;
            }

            AddRewardsToItems(acquiredItems, pendingRewards);
            if (acquiredItems.Count == 0)
                return;

            SortAndClampSelection(acquiredItems);
            hasReleasedAcceptInput = !IsAcceptInputHeld();
            gameManager.PauseForItemAcquisition();
        }

        private void AddRewardsToItems(List<AcquiredItem> targetItems, IReadOnlyList<PuzzleReward> rewards)
        {
            for (int rewardIndex = 0; rewardIndex < rewards.Count; rewardIndex++)
            {
                PuzzleReward reward = rewards[rewardIndex];
                if (reward == null || reward.Amount <= 0
                    || !TryGetItemPresentation(reward.ItemId, out _, out _, out _))
                {
                    continue;
                }

                AcquiredItem existingItem = FindAcquiredItem(targetItems, reward.ItemId);
                if (existingItem != null)
                    existingItem.Amount += reward.Amount;
                else
                    targetItems.Add(new AcquiredItem(reward.ItemId, reward.Amount));
            }
        }

        private void SortAndClampSelection(List<AcquiredItem> items)
        {
            items.Sort(CompareAcquiredItems);
            if (items.Count > 0)
                selectedItemIndex = Mathf.Clamp(selectedItemIndex, FirstItemIndex, items.Count - 1);
        }

        private void ClosePopup()
        {
            acquiredItems.Clear();
            selectedItemIndex = FirstItemIndex;
            hasReleasedAcceptInput = false;
            if (gameManager != null)
            {
                gameManager.TryConsumePendingRewardNotice(out _);
                gameManager.ResumeAfterItemAcquisition();
            }
        }

        private static bool IsAcceptInputHeld()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;
            return keyboard != null && (keyboard.eKey.isPressed || keyboard.xKey.isPressed)
                || gamepad != null && (gamepad.buttonWest.isPressed || gamepad.buttonSouth.isPressed);
        }

        private static bool WasAcceptInputPressed()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;
            return keyboard != null
                    && (keyboard.eKey.wasPressedThisFrame || keyboard.xKey.wasPressedThisFrame)
                || gamepad != null
                    && (gamepad.buttonWest.wasPressedThisFrame || gamepad.buttonSouth.wasPressedThisFrame);
        }

        private void DrawPopup(Rect popup)
        {
            DrawPanel(popup);
            float horizontalPadding = popup.width * PopupHorizontalPaddingRatio;
            Rect titleRect = new Rect(popup.x + horizontalPadding,
                popup.y + popup.height * 0.025f,
                popup.width - horizontalPadding * 2f,
                popup.height * PopupTitleHeightRatio);
            DrawText(titleRect, acquiredItems.Count > 1 ? MultipleItemsTitle : SingleItemTitle,
                TextAnchor.MiddleCenter, TextColor);

            Rect gridRect = new Rect(popup.x + horizontalPadding,
                popup.y + popup.height * PopupGridTopRatio,
                popup.width - horizontalPadding * 2f,
                popup.height * PopupGridHeightRatio);
            DrawAllAcquiredItems(gridRect);

            Rect descriptionRect = new Rect(popup.x + horizontalPadding,
                popup.y + popup.height * PopupDescriptionTopRatio,
                popup.width - horizontalPadding * 2f,
                popup.height * PopupDescriptionHeightRatio);
            DrawSelectedItemDescription(descriptionRect);

            Rect controlsRect = new Rect(popup.x + horizontalPadding,
                popup.y + popup.height * PopupControlsTopRatio,
                popup.width - horizontalPadding * 2f,
                popup.height * 0.055f);
            DrawControlPrompts(controlsRect);
        }

        private void DrawAllAcquiredItems(Rect gridRect)
        {
            int columnCount = Mathf.Min(acquiredItems.Count, MaximumItemsPerRow);
            int rowCount = Mathf.CeilToInt(acquiredItems.Count / (float)columnCount);
            float cellWidth = gridRect.width / columnCount;
            float cellHeight = gridRect.height / rowCount;
            float cardInset = Mathf.Min(cellWidth, cellHeight) * PopupCardInsetRatio;

            for (int itemIndex = 0; itemIndex < acquiredItems.Count; itemIndex++)
            {
                AcquiredItem item = acquiredItems[itemIndex];
                int rowIndex = itemIndex / columnCount;
                int columnIndex = itemIndex % columnCount;
                Rect card = new Rect(gridRect.x + columnIndex * cellWidth + cardInset,
                    gridRect.y + rowIndex * cellHeight + cardInset,
                    cellWidth - cardInset * 2f,
                    cellHeight - cardInset * 2f);
                DrawSolidRect(card, itemIndex == selectedItemIndex ? SelectedCardColor : CardColor);
                if (itemIndex == selectedItemIndex)
                    DrawBorder(card, BorderColor, BorderThickness);

                TryGetItemPresentation(item.ItemId, out Sprite icon, out string displayName, out _);
                float iconHeight = card.height * PopupCardIconHeightRatio;
                float iconWidth = card.width * 0.78f;
                Rect iconBounds = new Rect(card.center.x - iconWidth * 0.5f,
                    card.y + card.height * 0.035f, iconWidth, iconHeight);
                DrawSprite(FitSpriteRect(iconBounds, icon), icon);

                Rect nameRect = new Rect(card.x + card.width * 0.05f,
                    card.y + card.height * (PopupCardIconHeightRatio + 0.045f),
                    card.width * 0.9f,
                    card.height * PopupCardNameHeightRatio);
                string itemName = item.Amount > 1
                    ? string.Format("{0} x{1}", displayName, item.Amount)
                    : displayName;
                DrawText(nameRect, itemName, TextAnchor.MiddleCenter, TextColor);
            }
        }

        private void DrawSelectedItemDescription(Rect rect)
        {
            AcquiredItem selectedItem = acquiredItems[selectedItemIndex];
            TryGetItemPresentation(selectedItem.ItemId, out _, out string displayName, out string description);
            DrawPanel(rect);

            float textInset = rect.width * 0.025f;
            float nameWidth = rect.width * 0.27f;
            DrawText(new Rect(rect.x + textInset, rect.y + rect.height * 0.12f,
                    nameWidth, rect.height * 0.76f),
                displayName, TextAnchor.MiddleLeft, TextColor);
            DrawText(new Rect(rect.x + nameWidth + textInset * 2f,
                    rect.y + rect.height * 0.12f,
                    rect.width - nameWidth - textInset * 3f,
                    rect.height * 0.76f),
                description, TextAnchor.MiddleLeft, SubtleTextColor);
        }

        private void DrawPanel(Rect panel)
        {
            DrawSolidRect(panel, BorderColor);
            Rect innerPanel = new Rect(panel.x + BorderThickness, panel.y + BorderThickness,
                panel.width - BorderThickness * 2f, panel.height - BorderThickness * 2f);
            DrawSolidRect(innerPanel, PanelColor);
        }

        private void DrawControlPrompts(Rect rect)
        {
            string prompt = acquiredItems.Count > 1
                ? string.Format("{0}     {1}", ChangeItemPrompt, ContinuePrompt)
                : ContinuePrompt;
            DrawText(rect, prompt, TextAnchor.MiddleCenter, SubtleTextColor);
        }

        private static void DrawBorder(Rect rect, Color color, int thickness)
        {
            DrawSolidRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            DrawSolidRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            DrawSolidRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            DrawSolidRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private static Rect FitSpriteRect(Rect bounds, Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
                return bounds;

            Rect spriteRect = sprite.textureRect;
            if (spriteRect.width <= 0f || spriteRect.height <= 0f)
                return bounds;

            float scale = Mathf.Min(bounds.width / spriteRect.width, bounds.height / spriteRect.height);
            float width = spriteRect.width * scale;
            float height = spriteRect.height * scale;
            return new Rect(bounds.center.x - width * 0.5f,
                bounds.center.y - height * 0.5f, width, height);
        }

        private void DrawText(Rect rect, string text, TextAnchor alignment, Color color)
        {
            GameTextGUI.DrawText(rect, text, alignment, color);
        }

        private static void DrawSprite(Rect rect, Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
                return;

            Rect spriteRect = sprite.textureRect;
            Rect textureCoordinates = new Rect(
                spriteRect.x / sprite.texture.width,
                spriteRect.y / sprite.texture.height,
                spriteRect.width / sprite.texture.width,
                spriteRect.height / sprite.texture.height);
            GUI.DrawTextureWithTexCoords(rect, sprite.texture, textureCoordinates, true);
        }

        private static void DrawSolidRect(Rect rect, Color color)
        {
            Color previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private static AcquiredItem FindAcquiredItem(List<AcquiredItem> items, string itemId)
        {
            for (int index = 0; index < items.Count; index++)
            {
                if (items[index].ItemId == itemId)
                    return items[index];
            }

            return null;
        }

        private static int CompareAcquiredItems(AcquiredItem first, AcquiredItem second)
        {
            return GetDisplayOrderIndex(first.ItemId).CompareTo(GetDisplayOrderIndex(second.ItemId));
        }

        private static int GetDisplayOrderIndex(string itemId)
        {
            for (int index = 0; index < InventoryItemOrder.Length; index++)
            {
                if (InventoryItemOrder[index] == itemId)
                    return index;
            }

            return InventoryItemOrder.Length;
        }

        private sealed class AcquiredItem
        {
            public AcquiredItem(string itemId, int amount)
            {
                ItemId = itemId;
                Amount = amount;
            }

            public string ItemId { get; }
            public int Amount { get; set; }
        }
    }
}
