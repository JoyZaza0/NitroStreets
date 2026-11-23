using System.Globalization;
using ALIyerEdon;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GarageCarStatsDisplay : MonoBehaviour
{
    private const float MilesToKilometers = 1.609344f;

    [SerializeField] private CarSelect carSelect;
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Vector2 anchoredPosition = new Vector2(36f, -180f);
    [SerializeField] private Vector2 panelSize = new Vector2(350f, 250f);

    [SerializeField] private Text speedValue;
    [SerializeField] private Text enginePowerValue;
    [SerializeField] private Text handlingValue;
    [SerializeField] private Slider speedSlider;
    [SerializeField] private Slider enginePowerSlider;
    [SerializeField] private Slider handlingSlider;
    private int displayedCarIndex = -1;

    private void Awake()
    {
        CarSelect parentCarSelect = GetComponentInParent<CarSelect>(true);
        if (parentCarSelect != null)
            carSelect = parentCarSelect;
    }

    private void OnEnable()
    {
        CarSelect parentCarSelect = GetComponentInParent<CarSelect>(true);
        if (parentCarSelect != null)
            carSelect = parentCarSelect;

        displayedCarIndex = -1;
        if (carSelect != null && speedValue != null && enginePowerValue != null && handlingValue != null &&
            speedSlider != null && enginePowerSlider != null && handlingSlider != null)
        {
            RefreshStats();
        }
    }

    private void Start()
    {
        CarSelect parentCarSelect = GetComponentInParent<CarSelect>(true);
        if (parentCarSelect != null)
            carSelect = parentCarSelect;
        else if (carSelect == null)
            carSelect = FindAnyObjectByType<CarSelect>();

        if (carSelect == null)
        {
            Debug.LogError("GarageCarStatsDisplay requires a CarSelect reference.", this);
            enabled = false;
            return;
        }

        if (targetCanvas == null || panelRoot == null ||
            speedValue == null || enginePowerValue == null || handlingValue == null ||
            speedSlider == null || enginePowerSlider == null || handlingSlider == null)
        {
            Debug.LogError("GarageCarStatsDisplay is missing its editor-created panel references. Use Create Panel In Editor in the Inspector.", this);
            enabled = false;
            return;
        }

        RefreshStats();
    }

    private void Update()
    {
        if (carSelect != null && carSelect.ID != displayedCarIndex)
            RefreshStats();
    }

#if UNITY_EDITOR
    [ContextMenu("Create Panel In Editor")]
    public void CreatePanelInEditor()
    {
        if (!UnityEditor.EditorApplication.ExecuteMenuItem("Tools/Garage/Create Car Stats Panel"))
            Debug.LogError("Could not run the Garage Car Stats Panel editor command.", this);
    }
#endif

    private void RefreshStats()
    {
        displayedCarIndex = carSelect.ID;

        if (carSelect.cars == null ||
            displayedCarIndex < 0 ||
            displayedCarIndex >= carSelect.cars.Length ||
            carSelect.cars[displayedCarIndex] == null)
        {
            SetUnavailable();
            Debug.LogError("GarageCarStatsDisplay cannot resolve the selected car prefab.", this);
            return;
        }

        var controller = carSelect.cars[displayedCarIndex].GetComponent<EasyCarController>();
        if (controller == null)
        {
            SetUnavailable();
            Debug.LogError("The selected garage car prefab has no EasyCarController.", carSelect.cars[displayedCarIndex]);
            return;
        }

        float speedKph = controller.maxSpeed * MilesToKilometers;
        enginePowerValue.text = controller.enginePower.ToString("N0", CultureInfo.CurrentCulture) + " units";
        speedValue.text = Mathf.RoundToInt(speedKph).ToString(CultureInfo.CurrentCulture) + " km/h";
        handlingValue.text = controller.maxSteer.ToString("0.#", CultureInfo.CurrentCulture) + " deg";

        SetSliderValue(speedSlider, speedKph, GetStatRange(Stat.Speed, speedKph));
        SetSliderValue(enginePowerSlider, controller.enginePower, GetStatRange(Stat.EnginePower, controller.enginePower));
        SetSliderValue(handlingSlider, controller.maxSteer, GetStatRange(Stat.Handling, controller.maxSteer));
    }

    private void SetUnavailable()
    {
        speedValue.text = "—";
        enginePowerValue.text = "—";
        handlingValue.text = "—";
        speedSlider.value = 0f;
        enginePowerSlider.value = 0f;
        handlingSlider.value = 0f;
    }

    private enum Stat
    {
        Speed,
        EnginePower,
        Handling
    }

    private Vector2 GetStatRange(Stat stat, float selectedValue)
    {
        float minimum = selectedValue;
        float maximum = selectedValue;

        for (int i = 0; i < carSelect.cars.Length; i++)
        {
            var prefab = carSelect.cars[i];
            if (prefab == null)
                continue;

            var controller = prefab.GetComponent<EasyCarController>();
            if (controller == null)
                continue;

            float value;
            switch (stat)
            {
                case Stat.Speed:
                    value = controller.maxSpeed * MilesToKilometers;
                    break;
                case Stat.EnginePower:
                    value = controller.enginePower;
                    break;
                default:
                    value = controller.maxSteer;
                    break;
            }

            minimum = Mathf.Min(minimum, value);
            maximum = Mathf.Max(maximum, value);
        }

        float padding = (maximum - minimum) * 0.1f;
        if (padding <= Mathf.Epsilon)
            padding = Mathf.Max(Mathf.Abs(minimum) * 0.1f, 1f);

        return new Vector2(minimum - padding, maximum + padding);
    }

    private static void SetSliderValue(Slider slider, float value, Vector2 range)
    {
        slider.minValue = range.x;
        slider.maxValue = range.y;
        slider.SetValueWithoutNotify(value);
    }
}
