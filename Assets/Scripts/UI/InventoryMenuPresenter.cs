using System.Collections.Generic;
using ReturnToTheEigth.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ReturnToTheEigth.UI
{
    /// <summary>Displays the current session inventory and selected-item description in the pause menu.</summary>
    [DisallowMultipleComponent]
    public sealed class InventoryMenuPresenter : MonoBehaviour
    {
        private const string InventoryGridPath = "PauseMenuPanel/InventoryGrid";
        private const string ItemNamePath = "PauseMenuPanel/InventoryDescriptionPanel/InventoryDescriptionViewport/InventoryDescriptionContent/InventoryItemName";
        private const string ItemDescriptionPath = "PauseMenuPanel/InventoryDescriptionPanel/InventoryDescriptionViewport/InventoryDescriptionContent/InventoryItemDescription";
        private const string EmptyItemName = "Select an item";
        private const string EmptyItemDescription = "The item's description will appear here.";
        private const string ItemIconObjectName = "InventoryItemIcon";
        private const string ItemCountObjectName = "InventoryItemCount";
        private const string TypographyFontName = "TypographySilver";
        private const int FirstItemIndex = 0;
        private const int InventorySlotCount = 8;
        private const float IconInsetRatio = 0.14f;
        private const float CountLabelFontSize = 10f;
        private static readonly Color ItemCountColor = new Color(1f, 0.96f, 0.85f, 1f);

        private readonly List<string> visibleItemIds = new List<string>();
        private readonly List<int> visibleItemCounts = new List<int>();
        private readonly Image[] itemIconImages = new Image[InventorySlotCount];
        private readonly Text[] itemCountLabels = new Text[InventorySlotCount];
        private readonly Button[] inventoryButtons = new Button[InventorySlotCount];
        private GameManager gameManager;
        private ItemAcquisitionPopup itemAcquisitionPopup;
        private Text itemNameLabel;
        private Text itemDescriptionLabel;
        private Transform inventoryGrid;
        private int selectedItemIndex;

        private void Awake()
        {
            inventoryGrid = transform.Find(InventoryGridPath);
            Transform itemNameTransform = transform.Find(ItemNamePath);
            Transform itemDescriptionTransform = transform.Find(ItemDescriptionPath);
            itemNameLabel = itemNameTransform != null ? itemNameTransform.GetComponent<Text>() : null;
            itemDescriptionLabel = itemDescriptionTransform != null
                ? itemDescriptionTransform.GetComponent<Text>() : null;

            for (int slotIndex = 0; slotIndex < InventorySlotCount; slotIndex++)
            {
                Transform slot = inventoryGrid != null
                    ? inventoryGrid.Find(string.Format("InventorySlot{0:00}", slotIndex + 1))
                    : null;
                if (slot == null)
                    continue;

                inventoryButtons[slotIndex] = slot.GetComponent<Button>();
                if (inventoryButtons[slotIndex] != null)
                {
                    int capturedIndex = slotIndex;
                    inventoryButtons[slotIndex].onClick.AddListener(() => SelectInventoryItem(capturedIndex));
                }

                CreateItemVisuals(slot, slotIndex);
            }

            SetEmptyDescription();
        }

        private void OnEnable()
        {
            gameManager = GameManager.Instance;
            if (gameManager != null)
                gameManager.InventoryChanged += RefreshInventory;

            RefreshInventory();
        }

        private void OnDisable()
        {
            if (gameManager != null)
                gameManager.InventoryChanged -= RefreshInventory;
        }

        private void Update()
        {
            if (inventoryGrid == null || !inventoryGrid.gameObject.activeInHierarchy || EventSystem.current == null)
                return;

            for (int slotIndex = 0; slotIndex < inventoryButtons.Length; slotIndex++)
            {
                Button button = inventoryButtons[slotIndex];
                if (button != null && button.gameObject == EventSystem.current.currentSelectedGameObject)
                {
                    SelectInventoryItem(slotIndex);
                    return;
                }
            }
        }

        private void RefreshInventory()
        {
            if (gameManager == null)
                gameManager = GameManager.Instance;
            if (itemAcquisitionPopup == null)
                itemAcquisitionPopup = ItemAcquisitionPopup.Instance;

            visibleItemIds.Clear();
            visibleItemCounts.Clear();
            IReadOnlyList<string> itemOrder = ItemAcquisitionPopup.DisplayOrder;
            if (gameManager != null)
            {
                for (int orderIndex = 0; orderIndex < itemOrder.Count && visibleItemIds.Count < InventorySlotCount; orderIndex++)
                {
                    string itemId = itemOrder[orderIndex];
                    int itemCount = gameManager.GetItemCount(itemId);
                    if (itemCount <= 0)
                        continue;

                    visibleItemIds.Add(itemId);
                    visibleItemCounts.Add(itemCount);
                }
            }

            for (int slotIndex = 0; slotIndex < itemIconImages.Length; slotIndex++)
            {
                Image iconImage = itemIconImages[slotIndex];
                if (iconImage == null)
                    continue;

                Sprite icon = null;
                bool hasItem = slotIndex < visibleItemIds.Count && itemAcquisitionPopup != null;
                if (hasItem)
                {
                    hasItem = itemAcquisitionPopup.TryGetItemPresentation(
                        visibleItemIds[slotIndex], out icon, out _, out _);
                }

                iconImage.enabled = hasItem && icon != null;
                if (icon != null)
                    iconImage.sprite = icon;

                bool hasCount = slotIndex < visibleItemCounts.Count && visibleItemCounts[slotIndex] > 1;
                if (itemCountLabels[slotIndex] != null)
                {
                    itemCountLabels[slotIndex].text = hasCount
                        ? visibleItemCounts[slotIndex].ToString()
                        : string.Empty;
                }
            }

            if (visibleItemIds.Count == 0)
            {
                selectedItemIndex = FirstItemIndex;
                SetEmptyDescription();
                return;
            }

            selectedItemIndex = Mathf.Clamp(selectedItemIndex, FirstItemIndex, visibleItemIds.Count - 1);
            UpdateDescription(selectedItemIndex);
        }

        private void CreateItemVisuals(Transform slot, int slotIndex)
        {
            if (slotIndex >= itemIconImages.Length)
                return;

            GameObject iconObject = new GameObject(ItemIconObjectName,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObject.transform.SetParent(slot, false);
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(IconInsetRatio, IconInsetRatio);
            iconRect.anchorMax = new Vector2(1f - IconInsetRatio, 1f - IconInsetRatio);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;

            Image iconImage = iconObject.GetComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            iconImage.enabled = false;
            itemIconImages[slotIndex] = iconImage;

            GameObject countObject = new GameObject(ItemCountObjectName,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            countObject.transform.SetParent(slot, false);
            RectTransform countRect = countObject.GetComponent<RectTransform>();
            countRect.anchorMin = new Vector2(0.58f, 0f);
            countRect.anchorMax = new Vector2(1f, 0.38f);
            countRect.offsetMin = Vector2.zero;
            countRect.offsetMax = Vector2.zero;

            Text countLabel = countObject.GetComponent<Text>();
            countLabel.font = Resources.Load<Font>(TypographyFontName);
            countLabel.fontSize = Mathf.RoundToInt(CountLabelFontSize);
            countLabel.alignment = TextAnchor.LowerRight;
            countLabel.color = ItemCountColor;
            countLabel.raycastTarget = false;
            itemCountLabels[slotIndex] = countLabel;
        }

        private void SelectInventoryItem(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= visibleItemIds.Count)
            {
                SetEmptyDescription();
                return;
            }

            selectedItemIndex = slotIndex;
            UpdateDescription(selectedItemIndex);
        }

        private void UpdateDescription(int itemIndex)
        {
            if (itemIndex < 0 || itemIndex >= visibleItemIds.Count)
            {
                SetEmptyDescription();
                return;
            }

            if (itemAcquisitionPopup == null)
                itemAcquisitionPopup = ItemAcquisitionPopup.Instance;

            if (itemAcquisitionPopup != null
                && itemAcquisitionPopup.TryGetItemPresentation(visibleItemIds[itemIndex],
                    out _, out string displayName, out string description))
            {
                if (itemNameLabel != null)
                    itemNameLabel.text = displayName;
                if (itemDescriptionLabel != null)
                    itemDescriptionLabel.text = description;
            }
        }

        private void SetEmptyDescription()
        {
            if (itemNameLabel != null)
                itemNameLabel.text = EmptyItemName;
            if (itemDescriptionLabel != null)
                itemDescriptionLabel.text = EmptyItemDescription;
        }
    }
}
