using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ALIyerEdon;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
#endif

[DisallowMultipleComponent]
public sealed class GarageCustomizationManager : MonoBehaviour
{
    public enum CustomizationCategory
    {
        Color,
        Roof,
        Spoiler,
        Neon,
        Ring
    }

    [Serializable]
    private sealed class ProductPrice
    {
        [Tooltip("Inspector label for this visual option. The array index matches the source prefab's serialized visual array.")]
        public string itemName;

        [Tooltip("Set the actual game price here. Negative values are intentionally unavailable until configured.")]
        public int price = -1;
    }

    [Serializable]
    private sealed class CategoryEntry
    {
        public CustomizationCategory category;
        public Button categoryButton;
        public GameObject panelRoot;
        public Transform contentRoot;
        public Button[] itemButtons = Array.Empty<Button>();
        [Tooltip("One entry per real visual option. Index 0 is the free starter item; set the remaining prices before enabling the shop.")]
        public ProductPrice[] products = Array.Empty<ProductPrice>();
        public Color selectedItemColor = new Color(0.55f, 0.9f, 0.55f, 1f);
        public Button purchaseButton;
        public Button backButton;
       // public Text statusText;
        public GameObject notEnoughPopup;
    }

    [Serializable]
    private sealed class VehicleIdentity
    {
        public GameObject prefab;
        public string stableId;
        public int carId;
    }

    private sealed class VehicleVisuals
    {
        public Car_Color[] colors;
        public Roof_Loader[] roofs;
        public Spoiler_Loader[] spoilers;
        public Neon_Loader[] neons;
        public Ring_Loader[] rings;
    }

    private const string SavePrefix = "GarageCustomization.v1";
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [SerializeField] private CarSelect carSelect;
    [SerializeField] private GameObject customizationRoot;
    [SerializeField] private Button openCustomizationButton;
    [SerializeField] private Button closeCustomizationButton;
    [SerializeField] private Text[] balanceDisplays = Array.Empty<Text>();
    [SerializeField] private Slider suspensionSlider;
    [SerializeField] private CategoryEntry[] categories = Array.Empty<CategoryEntry>();
    [SerializeField] private VehicleIdentity[] vehicleIdentities = Array.Empty<VehicleIdentity>();
    [SerializeField, Min(0.05f)] private float playerPollInterval = 0.2f;

    private readonly Dictionary<Material, Color> sharedMaterialColors = new Dictionary<Material, Color>();
    private readonly Dictionary<Button, ColorBlock> itemButtonColors = new Dictionary<Button, ColorBlock>();
    private readonly HashSet<string> loggedMessages = new HashSet<string>();

    private GameObject currentPlayer;
    private VehicleIdentity currentIdentity;
    private VehicleVisuals currentVisuals;
    private CategoryEntry activeCategory;
    private int currentCarSelectIndex = -1;
    private int previewIndex = -1;
    private int selectedIndex = -1;

    private void Awake()
    {
        CacheSharedPaintColors();
    }

    private void Start()
    {
        if (categories == null)
        {
            Debug.LogError("GarageCustomizationManager has no category configuration.", this);
            categories = Array.Empty<CategoryEntry>();
        }

        if (balanceDisplays == null)
        {
            Debug.LogError("GarageCustomizationManager has no balance display list.", this);
            balanceDisplays = Array.Empty<Text>();
        }

        if (vehicleIdentities == null)
        {
            Debug.LogError("GarageCustomizationManager has no vehicle identity table.", this);
            vehicleIdentities = Array.Empty<VehicleIdentity>();
        }

        // Instantiate(carSelect.cars[PlayerPrefs.GetInt("CarID")], carSelect.spawnPoint.position, carSelect.spawnPoint.rotation);
        ValidateConfiguration();
        RefreshBalanceDisplay();
        BindUi();
        ShowCategoryHome();
        if (customizationRoot != null)
            customizationRoot.SetActive(false);
        StartCoroutine(ObserveCurrentPlayer());


    }

    private IEnumerator ObserveCurrentPlayer()
    {
        yield return null;
        RefreshCustomizationButton();

        var wait = new WaitForSecondsRealtime(Mathf.Max(0.05f, playerPollInterval));

        while (true)
        {
            if (carSelect == null)
            {
                LogErrorOnce("missing-car-select", "GarageCustomizationManager requires a reference to CarSelect.");
                yield return wait;
                continue;
            }

            var player = GameObject.FindGameObjectWithTag("Player");
            var selectionIndex = carSelect.ID;

            if (player != null && (player != currentPlayer || selectionIndex != currentCarSelectIndex))
            {
                yield return null;
                yield return null;

                var currentTaggedPlayer = GameObject.FindGameObjectWithTag("Player");
                if (currentTaggedPlayer == player)
                    TryAdoptPlayer(currentTaggedPlayer, selectionIndex);
            }

            RefreshCustomizationButton();
            yield return wait;
        }
    }

    private void TryAdoptPlayer(GameObject player, int selectionIndex)
    {
        if (carSelect == null)
        {
            LogErrorOnce("missing-car-select", "GarageCustomizationManager requires a reference to CarSelect.");
            return;
        }

        if (carSelect.cars == null || selectionIndex < 0 || selectionIndex >= carSelect.cars.Length || carSelect.cars[selectionIndex] == null)
        {
            LogErrorOnce("invalid-car-selection", "CarSelect.ID does not resolve to a configured Garage car prefab.");
            return;
        }

        var expectedPrefab = carSelect.cars[selectionIndex];
        var identity = FindIdentity(expectedPrefab);
        if (identity == null || string.IsNullOrWhiteSpace(identity.stableId))
        {
            LogErrorOnce("missing-vehicle-identity-" + selectionIndex, "No stable Garage vehicle identity is configured for the selected car prefab.");
            return;
        }

        var expectedCarId = expectedPrefab.GetComponent<CarID>();
        var liveCarId = player.GetComponent<CarID>();
        if (expectedCarId == null || liveCarId == null)
        {
            LogErrorOnce("missing-car-id-" + identity.stableId, "The selected Garage car or its Player instance has no root CarID component.");
            return;
        }

        if (player.name != expectedPrefab.name + "(Clone)" || liveCarId.carID != expectedCarId.carID)
            return;

        if (player == currentPlayer && selectionIndex == currentCarSelectIndex)
            return;

        if (currentCarSelectIndex >= 0 && selectionIndex != currentCarSelectIndex)
        {
            if (customizationRoot != null)
                customizationRoot.SetActive(false);
            activeCategory = null;
            previewIndex = -1;
            selectedIndex = -1;
        }

        currentPlayer = player;
        currentIdentity = identity;
        currentCarSelectIndex = selectionIndex;
        currentVisuals = ReadVehicleVisuals(player);
        previewIndex = -1;
        selectedIndex = -1;

        IsolatePaintMaterials(player);
        RestoreSharedPaintColors();
        ValidateVehicleVisuals();
        InitializeVehicleSaveData();
        ApplyInstalledCustomizations();
        RefreshBalanceDisplay();
        RefreshActiveCategory();
    }

    private VehicleVisuals ReadVehicleVisuals(GameObject player)
    {
        return new VehicleVisuals
        {
            colors = player.GetComponentsInChildren<Car_Color>(true),
            roofs = player.GetComponentsInChildren<Roof_Loader>(true),
            spoilers = player.GetComponentsInChildren<Spoiler_Loader>(true),
            neons = player.GetComponentsInChildren<Neon_Loader>(true),
            rings = player.GetComponentsInChildren<Ring_Loader>(true)
        };
    }

    private VehicleIdentity FindIdentity(GameObject prefab)
    {
        if (vehicleIdentities == null)
        {
            Debug.LogError("Garage vehicle identity table is missing.", this);
            return null;
        }

        foreach (var identity in vehicleIdentities)
        {
            if (identity != null && identity.prefab == prefab)
                return identity;
        }

        return null;
    }

    private void BindUi()
    {
        if (openCustomizationButton != null)
            openCustomizationButton.onClick.AddListener(OpenCustomizationPanel);
        else
            LogErrorOnce("missing-open-customization-button", "GarageCustomizationManager is missing its customization entry button.");

        if (closeCustomizationButton != null)
            closeCustomizationButton.onClick.AddListener(CloseCustomizationPanel);
        else
            LogErrorOnce("missing-close-customization-button", "GarageCustomizationManager is missing its customization close button.");

        if (categories == null)
            return;

        foreach (var entry in categories)
        {
            if (entry == null)
                continue;

            var categoryEntry = entry;
            if (categoryEntry.categoryButton != null)
                categoryEntry.categoryButton.onClick.AddListener(() => OpenCategory(categoryEntry));

            if (categoryEntry.purchaseButton != null)
                categoryEntry.purchaseButton.onClick.AddListener(() => ConfirmSelection(categoryEntry));

            if (categoryEntry.backButton != null)
                categoryEntry.backButton.onClick.AddListener(() => ReturnToCategoryHome(categoryEntry));

            var itemButtons = categoryEntry.itemButtons ?? Array.Empty<Button>();
            for (var i = 0; i < itemButtons.Length; i++)
            {
                var itemIndex = i;
                var itemButton = itemButtons[itemIndex];
                if (itemButton != null)
                {
                    if (!itemButtonColors.ContainsKey(itemButton))
                        itemButtonColors.Add(itemButton, itemButton.colors);
                    itemButton.onClick.AddListener(() => PreviewItem(categoryEntry, itemIndex));
                }
            }
        }
    }

    private void ShowCategoryHome()
    {
        activeCategory = null;
        previewIndex = -1;
        selectedIndex = -1;

        if (categories == null)
            return;
        var playerCar = GameObject.FindGameObjectWithTag("Player");

        Spoiler_Loader spoiler_Loader = playerCar.GetComponentInChildren<Spoiler_Loader>(true);
        bool hasSpoiler = spoiler_Loader.spoilers[0].GetComponentInChildren<Renderer>(true);

        foreach (var entry in categories)
        {
            if (entry == null)
                continue;

            if (entry.panelRoot != null)
                entry.panelRoot.SetActive(false);
            if (entry.categoryButton != null)
                entry.categoryButton.gameObject.SetActive(true);

            if (entry.category == CustomizationCategory.Spoiler && !hasSpoiler)
                entry.categoryButton.interactable = false;
        }

        if (suspensionSlider != null)
            suspensionSlider.gameObject.SetActive(true);

    }

    private void OpenCustomizationPanel()
    {
        if (customizationRoot == null)
        {
            LogErrorOnce("missing-customization-root", "GarageCustomizationManager is missing its customization panel root.");
            return;
        }

        if (!IsSelectedCarPurchased())
        {
            RefreshCustomizationButton();
            return;
        }

        customizationRoot.SetActive(true);
        customizationRoot.transform.SetAsLastSibling();
        ShowCategoryHome();
        if (closeCustomizationButton != null)
            closeCustomizationButton.transform.SetAsLastSibling();
    }

    private void CloseCustomizationPanel()
    {
        CancelPreview();
        ShowCategoryHome();
        if (customizationRoot != null)
            customizationRoot.SetActive(false);
    }

    private void RefreshCustomizationButton()
    {
        if (openCustomizationButton == null || carSelect == null)
            return;

        var isPurchased = IsSelectedCarPurchased();
        openCustomizationButton.interactable = isPurchased;
        openCustomizationButton.gameObject.SetActive(isPurchased);

        if (!isPurchased && customizationRoot != null && customizationRoot.activeSelf)
            CloseCustomizationPanel();

        if (customizationRoot != null && customizationRoot.activeSelf)
        {
            openCustomizationButton.gameObject.SetActive(false);
        }
    }

    private bool IsSelectedCarPurchased()
    {
        if (carSelect == null)
            return false;

        if (carSelect.cars == null || carSelect.ID < 0 || carSelect.ID >= carSelect.cars.Length ||
            carSelect.cars[carSelect.ID] == null)
        {
            LogErrorOnce("invalid-customization-car-selection", "CarSelect.ID does not resolve to a Garage car prefab.");
            return false;
        }

        return PlayerPrefs.GetInt("Car" + carSelect.ID) == 3;
    }

    private void OpenCategory(CategoryEntry entry)
    {
        if (entry == null || !categories.Contains(entry))
        {
            Debug.LogError("GarageCustomizationManager received an unknown customization category.", this);
            return;
        }

        if (currentIdentity == null || currentVisuals == null)
        {
            //SetStatus(entry, "The car is still loading.");
            return;
        }

        CancelPreview();
        activeCategory = entry;
        previewIndex = -1;
        selectedIndex = GetInstalledIndex(entry);

        foreach (var category in categories)
        {
            if (category == null)
                continue;

            if (category.panelRoot != null)
                category.panelRoot.SetActive(category == entry);
            if (category.categoryButton != null)
                category.categoryButton.gameObject.SetActive(false);
        }

        if (suspensionSlider != null)
            suspensionSlider.gameObject.SetActive(false);

        RefreshActiveCategory();
    }

    private void PreviewItem(CategoryEntry entry, int itemIndex)
    {
        if (!IsCurrentCategory(entry) || !IsItemAvailable(entry, itemIndex))
            return;

        previewIndex = itemIndex;
        selectedIndex = itemIndex;
        ApplyCustomization(entry.category, itemIndex);
        //SetStatus(entry, string.Empty);
        RefreshCategoryItems(entry);
    }

    private void ConfirmSelection(CategoryEntry entry)
    {
        if (!IsCurrentCategory(entry))
            return;

        var itemIndex = previewIndex >= 0 ? previewIndex : selectedIndex;
        if (!IsItemAvailable(entry, itemIndex))
        {
            //SetStatus(entry, "Select an item first.");
            return;
        }

        if (IsOwned(entry, itemIndex))
        {
            SaveInstalled(entry, itemIndex);
            previewIndex = -1;
            selectedIndex = itemIndex;
            ApplyCustomization(entry.category, itemIndex);
            //SetStatus(entry, "Installed.");
            RefreshCategoryItems(entry);
            return;
        }

        var price = GetPrice(entry, itemIndex);
        if (price < 0)
        {
            //SetStatus(entry, "Set this item's price in the Inspector.");
            return;
        }

        var balance = PlayerPrefs.GetInt("TotalScores");
        if (balance < price)
        {
            //SetStatus(entry, "Not enough coins.");
            entry.notEnoughPopup.SetActive(true);
            return;
        }

        PlayerPrefs.SetInt("TotalScores", balance - price);
        SetOwned(entry, itemIndex);
        SaveInstalled(entry, itemIndex);
        PlayerPrefs.Save();

        previewIndex = -1;
        selectedIndex = itemIndex;
        ApplyCustomization(entry.category, itemIndex);
        RefreshBalanceDisplay();
        //SetStatus(entry, "Purchased and installed.");
        RefreshCategoryItems(entry);
    }

    private void ReturnToCategoryHome(CategoryEntry entry)
    {
        if (!IsCurrentCategory(entry))
            return;

        CancelPreview();
        ShowCategoryHome();
        RefreshBalanceDisplay();
    }

    private void CancelPreview()
    {
        if (currentIdentity == null || currentVisuals == null || activeCategory == null || previewIndex < 0)
            return;

        var installed = GetInstalledIndex(activeCategory);
        ApplyCustomization(activeCategory.category, installed);
        previewIndex = -1;
        selectedIndex = installed;
        RefreshCategoryItems(activeCategory);
    }

    private void RefreshActiveCategory()
    {
        if (currentIdentity == null || currentVisuals == null)
            return;

        if (activeCategory == null)
        {
            foreach (var category in categories)
            {
                if (category != null && category.panelRoot != null)
                    category.panelRoot.SetActive(false);
            }

            return;
        }

        if (activeCategory.panelRoot == null)
        {
            LogErrorOnce("missing-panel-" + activeCategory.category, "Active Garage category has no panel reference.");
            return;
        }

        var installedIndex = GetInstalledIndex(activeCategory);
        if (previewIndex < 0)
        {
            selectedIndex = installedIndex;
            ApplyCustomization(activeCategory.category, installedIndex);
        }

        activeCategory.panelRoot.SetActive(true);
        RefreshCategoryItems(activeCategory);
    }

    private void RefreshCategoryItems(CategoryEntry entry)
    {
        if (entry == null)
            return;

        var availableCount = GetAvailableCount(entry.category);
        var itemButtons = entry.itemButtons ?? Array.Empty<Button>();
        for (var i = 0; i < itemButtons.Length; i++)
        {
            var button = itemButtons[i];
            if (button == null)
                continue;

            var available = i < availableCount;
            button.gameObject.SetActive(available);
            button.interactable = available;
            RefreshItemButtonColor(entry, button, i == (previewIndex >= 0 ? previewIndex : selectedIndex));
            if (!available)
                continue;

            if (entry.category == CustomizationCategory.Color)
            {
                var swatch = button.transform.GetChild(0).GetComponent<Image>();
                if (swatch != null && currentVisuals.colors.Length > 0 && i < currentVisuals.colors[0].colors.Length)
                    swatch.color = currentVisuals.colors[0].colors[i];
            }

            var label = GetButtonLabel(button);
            if (label != null)
                label.text = GetItemLabel(entry, i);
        }
 
        if (entry.purchaseButton != null)
        {
            var itemIndex = previewIndex >= 0 ? previewIndex : selectedIndex;
            var available = IsItemAvailable(entry, itemIndex);
            var owned = available && IsOwned(entry, itemIndex);
            var installed = available && itemIndex == GetInstalledIndex(entry);
            var price = available ? GetPrice(entry, itemIndex) : -1;
            var buttonLabel = GetButtonLabel(entry.purchaseButton);

            entry.purchaseButton.interactable = available && (owned || price >= 0);
            if (buttonLabel != null)
            {
                if (!available)
                    buttonLabel.text = "Select an item";
                else if (installed)
                    buttonLabel.text = "Installed";
                else if (owned)
                    buttonLabel.text = "Install";
                else if (price < 0)
                    buttonLabel.text = "Price not set";
                else
                    buttonLabel.text = "Buy " + price + " $";
            }
        }
    }

    private void RefreshItemButtonColor(CategoryEntry entry, Button button, bool isSelected)
    {
        if (!itemButtonColors.TryGetValue(button, out var originalColors))
            return;

        var colors = originalColors;
        if (isSelected)
        {
            colors.normalColor = entry.selectedItemColor;
            colors.highlightedColor = entry.selectedItemColor;
            colors.selectedColor = entry.selectedItemColor;
        }

        button.colors = colors;
    }

    private string GetItemLabel(CategoryEntry entry, int itemIndex)
    {
        var itemName = GetProductName(entry, itemIndex);
        if (itemIndex == GetInstalledIndex(entry))
        {
            
            return itemName + "\nInstalled";
        }
        if (itemIndex == previewIndex)
        {
            
            return itemName + "\nPreview";
        }
        if (IsOwned(entry, itemIndex))
        {
            
            return itemName + "\nOwned";
        }

        var price = GetPrice(entry, itemIndex);
        return price < 0 ? itemName + "\nPrice: ?" : itemName + "\n" + price + " $";
    }

    private static string GetProductName(CategoryEntry entry, int itemIndex)
    {
        if (entry.products != null && itemIndex >= 0 && itemIndex < entry.products.Length &&
            entry.products[itemIndex] != null && !string.IsNullOrWhiteSpace(entry.products[itemIndex].itemName))
        {
            return entry.products[itemIndex].itemName;
        }

        return entry.category.ToString();// + " " + itemIndex;
    }

    // private void SetStatus(CategoryEntry entry, string message)
    // {
    //     if (entry != null && entry.statusText != null)
    //         entry.statusText.text = message;
    // }

    private void RefreshBalanceDisplay()
    {
        var balance = PlayerPrefs.GetInt("TotalScores").ToString();
        if (carSelect != null && carSelect.TotalScores != null)
            carSelect.TotalScores.text = balance;

        foreach (var display in balanceDisplays)
        {
            if (display != null)
                display.text = balance;
        }
    }

    private bool IsCurrentCategory(CategoryEntry entry)
    {
        return entry != null && entry == activeCategory;
    }

    private bool IsItemAvailable(CategoryEntry entry, int itemIndex)
    {
        return entry != null && itemIndex >= 0 && itemIndex < GetAvailableCount(entry.category);
    }

    private int GetAvailableCount(CustomizationCategory category)
    {
        if (currentVisuals == null)
            return 0;

        switch (category)
        {
            case CustomizationCategory.Color:
                return GetColorCount(currentVisuals);
            case CustomizationCategory.Roof:
                return GetObjectArrayCount(currentVisuals.roofs, loader => loader.roofs);
            case CustomizationCategory.Spoiler:
                return GetObjectArrayCount(currentVisuals.spoilers, loader => loader.spoilers);
            case CustomizationCategory.Neon:
                return GetColorArrayCount(currentVisuals.neons, loader => loader.neons);
            case CustomizationCategory.Ring:
                return GetRingCount(currentVisuals.rings);
            default:
                Debug.LogError("Unsupported Garage customization category: " + category, this);
                return 0;
        }
    }

    private static int GetColorCount(VehicleVisuals visuals)
    {
        if (visuals.colors == null || visuals.colors.Length == 0)
            return 0;

        var count = int.MaxValue;
        foreach (var source in visuals.colors)
        {
            if (source == null || source.colors == null)
                return 0;

            count = Mathf.Min(count, source.colors.Length);
        }

        return count == int.MaxValue ? 0 : count;
    }

    private static int GetObjectArrayCount<TLoader>(TLoader[] loaders, Func<TLoader, GameObject[]> getItems)
        where TLoader : Component
    {
        if (loaders == null || loaders.Length == 0)
            return 0;

        var count = int.MaxValue;
        foreach (var loader in loaders)
        {
            var items = loader == null ? null : getItems(loader);
            if (items == null)
                return 0;

            count = Mathf.Min(count, items.Length);
        }

        return count == int.MaxValue ? 0 : count;
    }

    private static int GetColorArrayCount<TLoader>(TLoader[] loaders, Func<TLoader, Color[]> getItems)
        where TLoader : Component
    {
        if (loaders == null || loaders.Length == 0)
            return 0;

        var count = int.MaxValue;
        foreach (var loader in loaders)
        {
            var items = loader == null ? null : getItems(loader);
            if (items == null)
                return 0;

            count = Mathf.Min(count, items.Length);
        }

        return count == int.MaxValue ? 0 : count;
    }

    private static int GetRingCount(Ring_Loader[] loaders)
    {
        if (loaders == null || loaders.Length == 0)
            return 0;

        var count = int.MaxValue;
        foreach (var loader in loaders)
        {
            if (loader == null || loader.rings_1 == null || loader.rings_2 == null || loader.rings_3 == null || loader.rings_4 == null)
                return 0;

            count = Mathf.Min(count, loader.rings_1.Length, loader.rings_2.Length, loader.rings_3.Length, loader.rings_4.Length);
        }

        return count == int.MaxValue ? 0 : count;
    }

    private int GetPrice(CategoryEntry entry, int itemIndex)
    {
        if (entry == null || entry.products == null || itemIndex < 0 || itemIndex >= entry.products.Length ||
            entry.products[itemIndex] == null)
            return -1;

        return entry.products[itemIndex].price;
    }

    private bool IsOwned(CategoryEntry entry, int itemIndex)
    {
        if (currentIdentity == null || itemIndex < 0)
            return false;

        if (itemIndex == 0 && !PlayerPrefs.HasKey(GetOwnedKey(entry, itemIndex)))
            return true;

        return PlayerPrefs.GetInt(GetOwnedKey(entry, itemIndex), 0) == 1;
    }

    private void SetOwned(CategoryEntry entry, int itemIndex)
    {
        PlayerPrefs.SetInt(GetOwnedKey(entry, itemIndex), 1);
    }

    private void SaveInstalled(CategoryEntry entry, int itemIndex)
    {
        PlayerPrefs.SetInt(GetInstalledKey(entry), itemIndex);
        SaveLegacyCustomization(entry.category, itemIndex);
        PlayerPrefs.Save();
    }

    private void SaveLegacyCustomization(CustomizationCategory category, int itemIndex)
    {
        if (currentVisuals == null)
            return;

        switch (category)
        {
            case CustomizationCategory.Color:
                foreach (var source in currentVisuals.colors)
                    source.Change_Color(itemIndex);
                break;
            case CustomizationCategory.Roof:
                foreach (var loader in currentVisuals.roofs)
                    loader.Select_Roof(itemIndex);
                break;
            case CustomizationCategory.Spoiler:
                foreach (var loader in currentVisuals.spoilers)
                    loader.Select_Spoiler(itemIndex);
                break;
            case CustomizationCategory.Neon:
                foreach (var loader in currentVisuals.neons)
                    loader.Select_Neon(itemIndex);
                break;
            case CustomizationCategory.Ring:
                foreach (var loader in currentVisuals.rings)
                    loader.Select_Ring(itemIndex);
                break;
        }
    }

    private string GetOwnedKey(CategoryEntry entry, int itemIndex)
    {
        return SavePrefix + "." + currentIdentity.stableId + "." + entry.category + ".Owned." + itemIndex;
    }

    private string GetInstalledKey(CategoryEntry entry)
    {
        return SavePrefix + "." + currentIdentity.stableId + "." + entry.category + ".Installed";
    }

    private int GetInstalledIndex(CategoryEntry entry)
    {
        var count = GetAvailableCount(entry.category);
        if (count <= 0)
            return -1;

        var key = GetInstalledKey(entry);
        var index = PlayerPrefs.GetInt(key, 0);
        if (index >= 0 && index < count)
            return index;

        LogErrorOnce("invalid-install-" + key, "Saved Garage customization index is outside the available visual array. The free starter item is shown until the saved selection is corrected.");
        return 0;
    }

    private void InitializeVehicleSaveData()
    {
        var needsSave = false;
        foreach (var entry in categories)
        {
            if (entry == null)
                continue;

            var count = GetAvailableCount(entry.category);
            if (count <= 0)
            {
                LogErrorOnce("no-visuals-" + entry.category, "The selected car has no valid configured visual items for " + entry.category + ".");
                continue;
            }

            var installedKey = GetInstalledKey(entry);
            if (!PlayerPrefs.HasKey(installedKey))
            {
                PlayerPrefs.SetInt(installedKey, 0);
                needsSave = true;
            }

            var ownedKey = GetOwnedKey(entry, 0);
            if (PlayerPrefs.GetInt(ownedKey, 0) != 1)
            {
                PlayerPrefs.SetInt(ownedKey, 1);
                needsSave = true;
            }
        }

        if (needsSave)
            PlayerPrefs.Save();
    }

    private void ApplyInstalledCustomizations()
    {
        foreach (var entry in categories)
        {
            if (entry == null)
                continue;

            var installedIndex = GetInstalledIndex(entry);
            if (installedIndex >= 0)
                ApplyCustomization(entry.category, installedIndex);
        }
    }

    private void ApplyCustomization(CustomizationCategory category, int itemIndex)
    {
        if (currentVisuals == null || itemIndex < 0 || itemIndex >= GetAvailableCount(category))
        {
            Debug.LogError("Cannot apply invalid " + category + " customization index " + itemIndex + ".", this);
            return;
        }

        switch (category)
        {
            case CustomizationCategory.Color:
                foreach (var source in currentVisuals.colors)
                    ApplyColor(source, itemIndex);
                break;
            case CustomizationCategory.Roof:
                foreach (var loader in currentVisuals.roofs)
                    ApplyObjectChoice(loader.roofs, itemIndex);
                break;
            case CustomizationCategory.Spoiler:
                foreach (var loader in currentVisuals.spoilers)
                    ApplyObjectChoice(loader.spoilers, itemIndex);
                break;
            case CustomizationCategory.Neon:
                foreach (var loader in currentVisuals.neons)
                {
                    var light = loader.GetComponent<Light>();
                    if (light == null)
                    {
                        Debug.LogError("Neon_Loader has no Light component: " + loader.name, loader);
                        continue;
                    }

                    light.color = loader.neons[itemIndex];
                }
                break;
            case CustomizationCategory.Ring:
                foreach (var loader in currentVisuals.rings)
                    ApplyRingChoice(loader, itemIndex);
                break;
            default:
                Debug.LogError("Unsupported Garage customization category: " + category, this);
                break;
        }
    }

    private void ApplyObjectChoice(GameObject[] items, int itemIndex)
    {
        for (var i = 0; i < items.Length; i++)
        {
            if (items[i] == null)
            {
                Debug.LogError("A configured Garage visual item is missing at index " + i + ".", this);
                continue;
            }

            items[i].SetActive(i == itemIndex);
        }
    }

    private void ApplyRingChoice(Ring_Loader loader, int itemIndex)
    {
        SetRingObjects(loader.rings_1, itemIndex);
        SetRingObjects(loader.rings_2, itemIndex);
        SetRingObjects(loader.rings_3, itemIndex);
        SetRingObjects(loader.rings_4, itemIndex);
    }

    private void SetRingObjects(GameObject[] items, int itemIndex)
    {
        for (var i = 0; i < items.Length; i++)
        {
            if (items[i] == null)
            {
                Debug.LogError("A configured ring visual is missing at index " + i + ".", this);
                continue;
            }

            items[i].SetActive(i == itemIndex);
        }
    }

    private void ApplyColor(Car_Color source, int colorIndex)
    {
        var renderer = source.GetComponent<MeshRenderer>();
        if (renderer == null)
        {
            Debug.LogError("Car_Color requires a MeshRenderer on the same GameObject: " + source.name, source);
            return;
        }

        var materials = renderer.sharedMaterials;
        if (source.materialIndex < 0 || source.materialIndex >= materials.Length || materials[source.materialIndex] == null)
        {
            Debug.LogError("Car_Color has an invalid material index on " + source.name + ".", source);
            return;
        }

        var material = materials[source.materialIndex];
        var propertyId = material.HasProperty(BaseColorId) ? BaseColorId : ColorId;
        if (!material.HasProperty(propertyId))
        {
            Debug.LogError("Car paint material has neither _BaseColor nor _Color: " + material.name, material);
            return;
        }

        var block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block, source.materialIndex);
        block.SetColor(propertyId, source.colors[colorIndex]);
        renderer.SetPropertyBlock(block, source.materialIndex);
    }

    private void CacheSharedPaintColors()
    {
        if (carSelect == null || carSelect.cars == null)
            return;

        foreach (var prefab in carSelect.cars)
        {
            if (prefab == null)
                continue;

            foreach (var source in prefab.GetComponentsInChildren<Car_Color>(true))
            {
                if (source == null)
                    continue;

                var renderer = source.GetComponent<MeshRenderer>();
                if (renderer == null)
                {
                    Debug.LogError("Car_Color requires a MeshRenderer on the same GameObject: " + source.name, source);
                    continue;
                }

                var materials = renderer.sharedMaterials;
                if (source.materialIndex < 0 || source.materialIndex >= materials.Length || materials[source.materialIndex] == null)
                {
                    Debug.LogError("Car_Color has an invalid material index on prefab " + prefab.name + ".", source);
                    continue;
                }

                var material = materials[source.materialIndex];
                if (!sharedMaterialColors.ContainsKey(material))
                    sharedMaterialColors.Add(material, material.color);
            }
        }
    }

    private void IsolatePaintMaterials(GameObject player)
    {
        foreach (var source in player.GetComponentsInChildren<Car_Color>(true))
        {
            var renderer = source.GetComponent<MeshRenderer>();
            if (renderer == null)
                continue;

            var materials = renderer.sharedMaterials;
            if (source.materialIndex < 0 || source.materialIndex >= materials.Length)
                continue;

            var instanceMaterials = renderer.materials;
            if (source.materialIndex >= instanceMaterials.Length)
                Debug.LogError("Unable to isolate Car_Color material on " + source.name + ".", source);
        }
    }

    private void RestoreSharedPaintColors()
    {
        foreach (var pair in sharedMaterialColors)
        {
            if (pair.Key != null)
                pair.Key.color = pair.Value;
        }
    }

    private void ValidateVehicleVisuals()
    {
        if (currentVisuals == null)
            return;

        if (GetColorCount(currentVisuals) == 0)
            Debug.LogError("Current Player has no usable Car_Color color array.", currentPlayer);
        if (GetObjectArrayCount(currentVisuals.roofs, loader => loader.roofs) == 0)
            Debug.LogError("Current Player has no usable Roof_Loader.roofs array.", currentPlayer);
        if (GetObjectArrayCount(currentVisuals.spoilers, loader => loader.spoilers) == 0)
            Debug.LogError("Current Player has no usable Spoiler_Loader.spoilers array.", currentPlayer);
        if (GetColorArrayCount(currentVisuals.neons, loader => loader.neons) == 0)
            Debug.LogError("Current Player has no usable Neon_Loader.neons array.", currentPlayer);

        foreach (var loader in currentVisuals.rings)
        {
            if (loader == null)
            {
                Debug.LogError("Current Player contains a missing Ring_Loader reference.", currentPlayer);
                continue;
            }

            if (loader.rings_1 == null || loader.rings_2 == null || loader.rings_3 == null || loader.rings_4 == null)
            {
                Debug.LogError("Ring_Loader contains an unassigned visual array on " + loader.name + ".", loader);
                continue;
            }

            if (loader.rings_1.Length != loader.rings_2.Length ||
                loader.rings_1.Length != loader.rings_3.Length ||
                loader.rings_1.Length != loader.rings_4.Length)
            {
                Debug.LogError("Ring_Loader arrays have different lengths on " + loader.name + ". Only indices present in all four arrays can be used.", loader);
            }
        }
    }

    private void ValidateConfiguration()
    {
        if (carSelect == null)
            Debug.LogError("GarageCustomizationManager is missing its CarSelect reference.", this);
        if (customizationRoot == null || openCustomizationButton == null || closeCustomizationButton == null)
            Debug.LogError("GarageCustomizationManager is missing its customization panel or entry/close button references.", this);

        var categoriesSeen = new HashSet<CustomizationCategory>();
        foreach (var entry in categories)
        {
            if (entry == null)
            {
                Debug.LogError("GarageCustomizationManager contains an empty category configuration.", this);
                continue;
            }

            if (!categoriesSeen.Add(entry.category))
                Debug.LogError("Duplicate Garage category configuration: " + entry.category + ".", this);
            if (entry.categoryButton == null || entry.panelRoot == null || entry.contentRoot == null ||
                entry.purchaseButton == null || entry.backButton == null )// || entry.statusText == null)
            {
                Debug.LogError("Garage category " + entry.category + " has missing UI references.", this);
            }

            var expectedCount = GetMaximumPrefabOptionCount(entry.category);
            if (entry.itemButtons == null || entry.itemButtons.Length < expectedCount)
                Debug.LogError("Garage category " + entry.category + " has fewer product buttons than configured visual options.", this);

            if (entry.products == null || entry.products.Length < expectedCount)
            {
                Debug.LogError("Garage category " + entry.category + " has missing Inspector prices. Configure one price per visual option.", this);
                continue;
            }

            if (expectedCount > 0 && (entry.products[0] == null || entry.products[0].price != 0))
                Debug.LogError("The starter item price for " + entry.category + " must be 0.", this);

            for (var i = 1; i < expectedCount; i++)
            {
                if (entry.products[i] == null || entry.products[i].price < 0)
                    Debug.LogWarning("Price not configured for " + entry.category + " item " + i + "; the item remains unavailable until a real price is set in the Inspector.", this);
            }
        }

        // foreach (CustomizationCategory category in Enum.GetValues(typeof(CustomizationCategory)))
        // {
        //     if (!categoriesSeen.Contains(category))
        //         Debug.LogError("GarageCustomizationManager is missing the " + category + " category.", this);
        // }

        ValidateVehicleIdentityTable();
    }

    private int GetMaximumPrefabOptionCount(CustomizationCategory category)
    {
        if (carSelect == null || carSelect.cars == null)
            return 0;

        var maxCount = 0;
        foreach (var prefab in carSelect.cars)
        {
            if (prefab == null)
                continue;

            maxCount = Mathf.Max(maxCount, GetPrefabOptionCount(prefab, category));
        }

        return maxCount;
    }

    private static int GetPrefabOptionCount(GameObject prefab, CustomizationCategory category)
    {
        switch (category)
        {
            case CustomizationCategory.Color:
                return GetColorCount(new VehicleVisuals
                {
                    colors = prefab.GetComponentsInChildren<Car_Color>(true)
                });
            case CustomizationCategory.Roof:
                return GetObjectArrayCount(prefab.GetComponentsInChildren<Roof_Loader>(true), loader => loader.roofs);
            case CustomizationCategory.Spoiler:
                return GetObjectArrayCount(prefab.GetComponentsInChildren<Spoiler_Loader>(true), loader => loader.spoilers);
            case CustomizationCategory.Neon:
                return GetColorArrayCount(prefab.GetComponentsInChildren<Neon_Loader>(true), loader => loader.neons);
            case CustomizationCategory.Ring:
                return GetRingCount(prefab.GetComponentsInChildren<Ring_Loader>(true));
            default:
                Debug.LogError("Unsupported Garage customization category: " + category + ".", prefab);
                return 0;
        }
    }

    private void ValidateVehicleIdentityTable()
    {
        if (carSelect == null || carSelect.cars == null)
            return;

        var seenPrefabs = new HashSet<GameObject>();
        var seenStableIds = new HashSet<string>();
        var seenCarIds = new Dictionary<int, GameObject>();

        foreach (var identity in vehicleIdentities)
        {
            if (identity == null || identity.prefab == null || string.IsNullOrWhiteSpace(identity.stableId))
            {
                Debug.LogError("Garage vehicle identity entries require both a prefab reference and a stable identifier.", this);
                continue;
            }

            if (!seenPrefabs.Add(identity.prefab))
                Debug.LogError("Duplicate prefab in Garage vehicle identity table: " + identity.prefab.name + ".", this);
            if (!seenStableIds.Add(identity.stableId))
                Debug.LogError("Duplicate stable Garage vehicle identifier: " + identity.stableId + ".", this);

            var carId = identity.prefab.GetComponent<CarID>();
            if (carId == null)
            {
                Debug.LogError("Garage car prefab has no root CarID component: " + identity.prefab.name + ".", identity.prefab);
                continue;
            }

            if (carId.carID != identity.carId)
                Debug.LogError("Garage vehicle identity CarID no longer matches prefab " + identity.prefab.name + ".", identity.prefab);
            if (seenCarIds.TryGetValue(carId.carID, out var otherPrefab))
                Debug.LogWarning("Duplicate CarID " + carId.carID + " in Garage prefabs " + otherPrefab.name + " and " + identity.prefab.name + "; their independent stable identifiers are used for customization saves.", this);
            else
                seenCarIds.Add(carId.carID, identity.prefab);
        }

        foreach (var prefab in carSelect.cars)
        {
            if (prefab != null && !seenPrefabs.Contains(prefab))
                Debug.LogError("Garage CarSelect prefab has no entry in the stable vehicle identity table: " + prefab.name + ".", this);
        }
    }

    private void LogErrorOnce(string key, string message)
    {
        if (loggedMessages.Add(key))
            Debug.LogError(message, this);
    }

    private static Text GetButtonLabel(Button button)
    {
        if (button == null)
            return null;

        var shopLabel = button.transform.Find("GarageShopLabel");
        return shopLabel != null
            ? shopLabel.GetComponent<Text>()
            : button.GetComponentInChildren<Text>(true);
    }

#if UNITY_EDITOR
    private static Text garageShopTextTemplate;

    [ContextMenu("Configure Garage Shop Scene")]
    public void ConfigureGarageShopScene()
    {
        var carSelects = Resources.FindObjectsOfTypeAll<CarSelect>()
            .Where(component => component != null && component.gameObject.scene.IsValid())
            .ToArray();
        var suspensionComponents = Resources.FindObjectsOfTypeAll<Car_Suspension>()
            .Where(component => component != null && component.gameObject.scene.IsValid())
            .ToArray();

        if (carSelects.Length != 1 || suspensionComponents.Length != 1)
        {
            Debug.LogError("Garage setup requires exactly one scene CarSelect and one Car_Suspension category container.", this);
            return;
        }

        Undo.RecordObject(this, "Configure Garage Customization");
        carSelect = carSelects[0];
        garageShopTextTemplate = carSelect.GetComponentInChildren<Text>(true);
        suspensionSlider = suspensionComponents[0].suspensionDistance;
        var customizeRoot = suspensionComponents[0].transform;
        var menuRoot = customizeRoot.Find("Menu");
        var colorRoot = customizeRoot.Find("ColorPicker");
        if (menuRoot == null || colorRoot == null || colorRoot.Find("Panel") == null)
        {
            Debug.LogError("Garage customization UI is missing Menu or ColorPicker/Panel.", this);
            return;
        }

        var roofPanel = menuRoot.Find("Roof_List");
        var spoilerPanel = menuRoot.Find("Spoiler_List");
        var neonPanel = menuRoot.Find("Neon_List");
        var ringPanel = menuRoot.Find("Ring_List");
        if (roofPanel == null || spoilerPanel == null || neonPanel == null || ringPanel == null)
        {
            Debug.LogError("Garage is missing one of the configured Roof, Spoiler, Neon, or Ring product panels.", this);
            return;
        }

        var colorButton = customizeRoot.Find("Color")?.GetComponent<Button>();
        if (colorButton == null)
        {
            var roofButton = customizeRoot.Find("Roof")?.GetComponent<Button>();
            if (roofButton == null)
            {
                Debug.LogError("Garage has no Roof category button to use as a styling template.", this);
                return;
            }

            colorButton = CreateButtonClone(roofButton, customizeRoot, "Color");
            ConfigureRect(colorButton.GetComponent<RectTransform>(), new Vector2(390f, 187f), new Vector2(105.7f, 105.7f));
            SetButtonLabel(colorButton, "Color");
            ClearAllPersistentListeners(colorButton);
        }

        var itemTemplates = new Dictionary<CustomizationCategory, Button[]>
        {
            { CustomizationCategory.Color, GetButtons(colorRoot.Find("Panel")) },
            { CustomizationCategory.Roof, GetButtons(roofPanel) },
            { CustomizationCategory.Spoiler, GetButtons(spoilerPanel) },
            { CustomizationCategory.Neon, GetButtons(neonPanel) },
            { CustomizationCategory.Ring, GetButtons(ringPanel) }
        };

        var categoryButtons = new Dictionary<CustomizationCategory, Button>
        {
            { CustomizationCategory.Color, colorButton },
            { CustomizationCategory.Roof, customizeRoot.Find("Roof").GetComponent<Button>() },
            { CustomizationCategory.Spoiler, customizeRoot.Find("Spoiler").GetComponent<Button>() },
            { CustomizationCategory.Neon, customizeRoot.Find("Neon").GetComponent<Button>() },
            { CustomizationCategory.Ring, customizeRoot.Find("Ring").GetComponent<Button>() }
        };

        var startButton = carSelect.transform.Find("Start_Race")?.GetComponent<Button>();
        if (startButton == null)
        {
            Debug.LogError("Garage car selection panel has no Start_Race button to use as a style template.", carSelect);
            return;
        }

        var openCustomizationButton = EnsureActionButton(carSelect.transform, startButton, "GarageCustomizeButton", "CUSTOMIZE",
            new Vector2(260f, -199f), new Vector2(155f, 73f));
        ClearAllPersistentListeners(openCustomizationButton);
        var closeCustomizationButton = EnsureActionButton(customizeRoot, startButton, "GarageCustomizeClose", "CLOSE",
            new Vector2(560f, 300f), new Vector2(130f, 54f));
        ClearAllPersistentListeners(closeCustomizationButton);

        var panelRoots = new Dictionary<CustomizationCategory, GameObject>
        {
            { CustomizationCategory.Color, colorRoot.gameObject },
            { CustomizationCategory.Roof, roofPanel.gameObject },
            { CustomizationCategory.Spoiler, spoilerPanel.gameObject },
            { CustomizationCategory.Neon, neonPanel.gameObject },
            { CustomizationCategory.Ring, ringPanel.gameObject }
        };

        var contentRoots = new Dictionary<CustomizationCategory, Transform>
        {
            { CustomizationCategory.Color, colorRoot.Find("Panel") },
            { CustomizationCategory.Roof, roofPanel },
            { CustomizationCategory.Spoiler, spoilerPanel },
            { CustomizationCategory.Neon, neonPanel },
            { CustomizationCategory.Ring, ringPanel }
        };

        var previousProducts = categories
            .Where(entry => entry != null)
            .ToDictionary(entry => entry.category, entry => entry.products ?? Array.Empty<ProductPrice>());
        var configuredCategories = new List<CategoryEntry>();
        var orderedCategories = new[]
        {
            CustomizationCategory.Color,
            CustomizationCategory.Roof,
            CustomizationCategory.Spoiler,
            CustomizationCategory.Neon,
            CustomizationCategory.Ring
        };

        foreach (var category in orderedCategories)
        {
            var contentRoot = contentRoots[category];
            var optionCount = GetMaximumPrefabOptionCountForSetup(carSelect, category);
            var itemButtons = EnsureProductButtons(category, contentRoot, itemTemplates[category], optionCount);
            var panelRect = contentRoot.GetComponent<RectTransform>();
            if (panelRect == null)
            {
                Debug.LogError("Garage category content has no RectTransform: " + contentRoot.name + ".", contentRoot);
                return;
            }

            var colorLayout = category == CustomizationCategory.Color;
            ConfigureRect(panelRect, new Vector2(290f, -135f), new Vector2(620f, 260f));

            for (var i = 0; i < itemButtons.Length; i++)
            {
                if (itemButtons[i] == null)
                    continue;

                RemoveLegacySelectionListeners(itemButtons[i]);
                EnsureButtonLabel(itemButtons[i], category.ToString(), false);
                var itemLabel = GetButtonLabel(itemButtons[i]);
                if (itemLabel != null)
                {
                    itemLabel.color = colorLayout ? Color.white : Color.black;
                    itemLabel.fontSize = 11;
                }
                var labelBackground = EnsureButtonLabelBackground(itemButtons[i]);
                if (labelBackground != null)
                    labelBackground.color = colorLayout
                        ? new Color(0f, 0f, 0f, 0.6f)
                        : new Color(1f, 1f, 1f, 0.9f);

                if (colorLayout)
                {
                    var row = i / 7;
                    var rowCount = row == 0 ? 7 : 6;
                    var column = i % 7;
                    var x = (column - (rowCount - 1) * 0.5f) * 78f;
                    var y = row == 0 ? 72f : 5f;
                    ConfigureRect(itemButtons[i].GetComponent<RectTransform>(), new Vector2(x, y), new Vector2(76f, 56f));
                    if (itemLabel != null)
                        ConfigureRect(itemLabel.GetComponent<RectTransform>(), new Vector2(0f, -17f), new Vector2(76f, 20f));
                    if (labelBackground != null)
                        ConfigureRect(labelBackground.GetComponent<RectTransform>(), new Vector2(0f, -17f), new Vector2(76f, 20f));
                }
                else
                {
                    var x = (i - (itemButtons.Length - 1) * 0.5f) * 108f;
                    ConfigureRect(itemButtons[i].GetComponent<RectTransform>(), new Vector2(x, 48f), new Vector2(92f, 88f));
                    if (itemLabel != null)
                        ConfigureRect(itemLabel.GetComponent<RectTransform>(), new Vector2(0f, -28f), new Vector2(88f, 34f));
                    if (labelBackground != null)
                        ConfigureRect(labelBackground.GetComponent<RectTransform>(), new Vector2(0f, -28f), new Vector2(88f, 34f));
                }
            }

            var templateButton = itemButtons.FirstOrDefault(button => button != null);
            if (templateButton == null)
            {
                Debug.LogError("Garage category " + category + " has no product button template.", contentRoot);
                return;
            }

            const float controlY = -55f;
            var backButton = EnsureActionButton(contentRoot, templateButton, "GarageBack", "Back",
                new Vector2(-125f, controlY), new Vector2(140f, 44f));
            var purchaseButton = EnsureActionButton(contentRoot, templateButton, "GaragePurchase", "Buy / Install",
                new Vector2(125f, controlY), new Vector2(190f, 44f));
            var statusText = EnsureStatusText(contentRoot, templateButton, "GarageStatus",
                new Vector2(0f, -105f), new Vector2(520f, 28f));

            var products = CreateProducts(category, optionCount, previousProducts);
            configuredCategories.Add(new CategoryEntry
            {
                category = category,
                categoryButton = categoryButtons[category],
                panelRoot = panelRoots[category],
                contentRoot = contentRoot,
                itemButtons = itemButtons,
                products = products,
                purchaseButton = purchaseButton,
                backButton = backButton,
                //statusText = statusText
            });
        }

        categories = configuredCategories.ToArray();
        customizationRoot = customizeRoot.gameObject;
        this.openCustomizationButton = openCustomizationButton;
        this.closeCustomizationButton = closeCustomizationButton;
        vehicleIdentities = BuildVehicleIdentityTable(carSelect);
        balanceDisplays = FindBalanceDisplays(carSelect);

        var eventSystems = Resources.FindObjectsOfTypeAll<UnityEngine.EventSystems.EventSystem>()
            .Where(component => component != null && component.gameObject.scene.IsValid())
            .ToArray();
        if (eventSystems.Length != 1)
            Debug.LogError("Garage must contain exactly one EventSystem; found " + eventSystems.Length + ".", this);

        var canvas = Resources.FindObjectsOfTypeAll<Canvas>()
            .FirstOrDefault(component => component != null && component.gameObject.scene.IsValid());
        if (canvas == null || canvas.GetComponent<GraphicRaycaster>() == null)
            Debug.LogError("Garage Canvas is missing its GraphicRaycaster.", this);

        foreach (var entry in categories)
        {
            entry.panelRoot.SetActive(false);
            foreach (var button in entry.itemButtons)
            {
                if (button != null && button.targetGraphic != null)
                    button.targetGraphic.raycastTarget = true;
            }
        }

        if (colorButton.targetGraphic != null)
            colorButton.targetGraphic.raycastTarget = true;

        openCustomizationButton.gameObject.SetActive(false);
        customizationRoot.SetActive(false);
        EditorUtility.SetDirty(this);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log("Garage shop UI configured. Set each non-starter product price in the GarageCustomizationManager Inspector before enabling purchases.", this);
    }

    private static Button[] GetButtons(Transform root)
    {
        if (root == null)
            return Array.Empty<Button>();

        return root.GetComponentsInChildren<Button>(true)
            .Where(button => button.transform != root)
            .OrderBy(button => button.transform.GetSiblingIndex())
            .ToArray();
    }

    private Button[] EnsureProductButtons(CustomizationCategory category, Transform contentRoot, Button[] existingButtons, int optionCount)
    {
        var buttons = existingButtons.Where(button => button != null).ToList();
        if (buttons.Count == 0)
        {
            Debug.LogError("Garage category " + category + " has no existing product buttons.", contentRoot);
            return Array.Empty<Button>();
        }

        var template = buttons[0];
        while (buttons.Count < optionCount)
        {
            var newButton = CreateButtonClone(template, contentRoot, category + "_" + (buttons.Count + 1));
            buttons.Add(newButton);
        }

        return buttons.Take(optionCount).ToArray();
    }

    private static Button CreateButtonClone(Button template, Transform parent, string name)
    {
        var clone = Instantiate(template.gameObject, parent, false);
        Undo.RegisterCreatedObjectUndo(clone, "Create Garage customization button");
        clone.name = name;
        var button = clone.GetComponent<Button>();
        ClearAllPersistentListeners(button);
        return button;
    }

    private static Button EnsureActionButton(Transform parent, Button template, string name, string label, Vector2 position, Vector2 size)
    {
        var existing = parent.Find(name);
        var button = existing == null ? null : existing.GetComponent<Button>();
        if (button == null)
        {
            button = CreateButtonClone(template, parent, name);
            ClearAllPersistentListeners(button);
        }

        ConfigureRect(button.GetComponent<RectTransform>(), position, size);
        EnsureButtonLabel(button, label, false);
        SetButtonLabel(button, label);
        var buttonLabel = GetButtonLabel(button);
        if (buttonLabel != null)
        {
            buttonLabel.color = Color.black;
            buttonLabel.fontSize = 14;
            ConfigureRect(buttonLabel.GetComponent<RectTransform>(), Vector2.zero,
                new Vector2(Mathf.Max(1f, size.x - 8f), Mathf.Max(1f, size.y - 4f)));
        }

        var buttonImage = button.GetComponent<Image>();
        if (buttonImage != null)
        {
            buttonImage.sprite = null;
            buttonImage.color = Color.white;
        }

        if (button.targetGraphic != null)
            button.targetGraphic.raycastTarget = true;
        return button;
    }

    private static Text EnsureStatusText(Transform parent, Button template, string name, Vector2 position, Vector2 size)
    {
        var existing = parent.Find(name);
        var text = existing == null ? null : existing.GetComponent<Text>();
        if (text == null)
        {
            var statusObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            Undo.RegisterCreatedObjectUndo(statusObject, "Create Garage customization status");
            statusObject.transform.SetParent(parent, false);
            text = statusObject.GetComponent<Text>();
        }

        var source = GetButtonLabel(template);
        if (source == null || source.font == null)
            source = garageShopTextTemplate;

        if (text.font == null && source != null)
        {
            text.font = source.font;
            text.fontSize = source.fontSize;
            text.fontStyle = source.fontStyle;
        }
        else if (text.font == null)
        {
            Debug.LogError("Garage customization status text has no font reference.", text);
        }

        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.color = Color.white;
        text.raycastTarget = false;
        text.text = string.Empty;
        ConfigureRect(text.GetComponent<RectTransform>(), position, size);
        return text;
    }

    private static void EnsureButtonLabel(Button button, string initialText)
    {
        EnsureButtonLabel(button, initialText, true);
    }

    private static void EnsureButtonLabel(Button button, string initialText, bool productLabel)
    {
        var labelTransform = button.transform.Find("GarageShopLabel");
        if (labelTransform == null)
        {
            var previousLabel = button.transform.Find("GarageStateLabel");
            if (previousLabel != null)
            {
                previousLabel.name = "GarageShopLabel";
                labelTransform = previousLabel;
            }
        }

        Text label;
        if (labelTransform != null)
        {
            label = labelTransform.GetComponent<Text>();
        }
        else
        {
            var sourceText = garageShopTextTemplate;
            if (sourceText == null)
            {
                var sourceButton = Resources.FindObjectsOfTypeAll<CarSelect>()
                    .FirstOrDefault(component => component != null && component.gameObject.scene.IsValid());
                sourceText = sourceButton == null ? null : sourceButton.GetComponentInChildren<Text>(true);
                garageShopTextTemplate = sourceText;
            }

            if (sourceText == null || sourceText.font == null)
            {
                Debug.LogError("Garage product labels require a usable Text font from the scene CarSelect.", button);
                return;
            }

            var labelObject = new GameObject("GarageShopLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            Undo.RegisterCreatedObjectUndo(labelObject, "Create Garage product state label");
            labelObject.transform.SetParent(button.transform, false);
            label = labelObject.GetComponent<Text>();
            label.font = sourceText.font;
            label.fontSize = 14;
            label.color = sourceText.color;
            label.fontStyle = sourceText.fontStyle;
        }

        if (label == null)
        {
            Debug.LogError("Garage product label object has no Text component.", button);
            return;
        }

        foreach (var previousText in button.GetComponentsInChildren<Text>(true))
        {
            if (previousText != label)
                previousText.enabled = false;
        }

        var background = button.transform.Find("GarageShopLabelBackground");
        if (productLabel)
        {
            EnsureButtonLabelBackground(button);
        }
        else if (background != null)
        {
            var backgroundImage = background.GetComponent<Image>();
            if (backgroundImage != null)
                backgroundImage.enabled = false;
        }

        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        label.text = initialText;
        ConfigureRect(label.GetComponent<RectTransform>(), Vector2.zero, new Vector2(92f, 64f));
    }

    private static Image EnsureButtonLabelBackground(Button button)
    {
        var backgroundTransform = button.transform.Find("GarageShopLabelBackground");
        if (backgroundTransform == null)
        {
            var backgroundObject = new GameObject("GarageShopLabelBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(backgroundObject, "Create Garage product label background");
            backgroundObject.transform.SetParent(button.transform, false);
            backgroundTransform = backgroundObject.transform;
        }

        backgroundTransform.SetAsFirstSibling();
        var image = backgroundTransform.GetComponent<Image>();
        if (image == null)
        {
            Debug.LogError("Garage product label background has no Image component.", button);
            return null;
        }

        image.raycastTarget = false;
        return image;
    }

    private static void SetButtonLabel(Button button, string text)
    {
        var label = GetButtonLabel(button);
        if (label != null)
            label.text = text;
    }

    private static void ConfigureRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static void ClearAllPersistentListeners(Button button)
    {
        if (button == null)
            return;

        for (var i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(button.onClick, i);
    }

    private static void RemoveLegacySelectionListeners(Button button)
    {
        for (var i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            var methodName = button.onClick.GetPersistentMethodName(i);
            var target = button.onClick.GetPersistentTarget(i);
            var oldSelection = methodName == "Select_Color" ||
                               methodName == "Select_Roof" ||
                               methodName == "Select_Spoiler" ||
                               methodName == "Select_Neon" ||
                               methodName == "Select_Ring";
            var oldPanelClose = methodName == "SetFalse" && target is MainUtility;

            if (oldSelection || oldPanelClose)
                UnityEventTools.RemovePersistentListener(button.onClick, i);
        }
    }

    private static int GetMaximumPrefabOptionCountForSetup(CarSelect source, CustomizationCategory category)
    {
        if (source == null || source.cars == null)
        {
            Debug.LogError("Garage setup requires a configured CarSelect car array.", source);
            return 0;
        }

        var maxCount = 0;
        foreach (var prefab in source.cars)
        {
            if (prefab == null)
                continue;

            maxCount = Mathf.Max(maxCount, GetPrefabOptionCount(prefab, category));
        }

        return maxCount;
    }

    private static ProductPrice[] CreateProducts(
        CustomizationCategory category,
        int count,
        Dictionary<CustomizationCategory, ProductPrice[]> previousProducts)
    {
        var products = new ProductPrice[count];
        for (var i = 0; i < count; i++)
        {
            products[i] = new ProductPrice
            {
                itemName = category + " " + i,
                price = i == 0 ? 0 : -1
            };
        }

        if (previousProducts.TryGetValue(category, out var oldProducts))
        {
            for (var i = 0; i < Mathf.Min(oldProducts.Length, products.Length); i++)
            {
                if (oldProducts[i] != null)
                {
                    products[i].itemName = oldProducts[i].itemName;
                    products[i].price = oldProducts[i].price;
                }
            }

            if (count > 0)
                products[0].price = 0;
        }

        return products;
    }

    private static VehicleIdentity[] BuildVehicleIdentityTable(CarSelect source)
    {
        var identities = new List<VehicleIdentity>();
        foreach (var prefab in source.cars)
        {
            if (prefab == null)
            {
                Debug.LogError("CarSelect.cars contains a missing prefab; it cannot receive a stable Garage identity.", source);
                continue;
            }

            var assetPath = AssetDatabase.GetAssetPath(prefab);
            var guid = string.IsNullOrEmpty(assetPath) ? string.Empty : AssetDatabase.AssetPathToGUID(assetPath);
            var carId = prefab.GetComponent<CarID>();
            if (string.IsNullOrEmpty(guid))
                Debug.LogError("Unable to create a stable GUID-based Garage identity for car prefab " + prefab.name + ".", prefab);
            if (carId == null)
                Debug.LogError("Car prefab has no root CarID component: " + prefab.name + ".", prefab);

            identities.Add(new VehicleIdentity
            {
                prefab = prefab,
                stableId = guid,
                carId = carId == null ? int.MinValue : carId.carID
            });
        }

        return identities.ToArray();
    }

    private static Text[] FindBalanceDisplays(CarSelect source)
    {
        var displays = new List<Text>();
        if (source.TotalScores != null)
            displays.Add(source.TotalScores);

        var canvas = Resources.FindObjectsOfTypeAll<Canvas>()
            .FirstOrDefault(component => component != null && component.gameObject.scene.IsValid());
        if (canvas != null)
        {
            var mainMenuCoins = canvas.transform.Find("Main_Menu/Coins/Total_Coins_Text");
            if (mainMenuCoins != null && mainMenuCoins.GetComponent<Text>() != null)
                displays.Add(mainMenuCoins.GetComponent<Text>());
        }

        return displays.Distinct().ToArray();
    }
#endif
}
