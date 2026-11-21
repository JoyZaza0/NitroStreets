using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[CustomEditor(typeof(GarageCarStatsDisplay))]
public sealed class GarageCarStatsDisplayEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var display = (GarageCarStatsDisplay)target;
        var serializedDisplay = new SerializedObject(display);
        var targetCanvas = serializedDisplay.FindProperty("targetCanvas").objectReferenceValue as Canvas;
        if (targetCanvas == null)
            targetCanvas = FindGarageCanvas(display);

        EditorGUILayout.Space();
        if (targetCanvas == null)
        {
            EditorGUILayout.HelpBox(
                "Assign the Garage Canvas, then create the panel. The panel will be saved in the scene and editable in the Hierarchy.",
                MessageType.Info);
        }

        using (new EditorGUI.DisabledScope(targetCanvas == null))
        {
            if (GUILayout.Button("Create Panel In Editor"))
                CreatePanel(display, targetCanvas);
        }
    }

    public static void CreatePanelInScene()
    {
        var display = FindAnyObjectByType<GarageCarStatsDisplay>();
        if (display == null)
        {
            Debug.LogError("No GarageCarStatsDisplay component was found in the active scene.");
            return;
        }

        var canvas = FindGarageCanvas(display);
        if (canvas == null)
        {
            Debug.LogError("Could not find an active scene Canvas named 'Canvas' for the garage stats panel.", display);
            return;
        }

        CreatePanel(display, canvas);
    }

    [MenuItem("Tools/Garage/Create Car Stats Panel")]
    private static void CreatePanelFromMenu()
    {
        CreatePanelInScene();
    }

    private static void CreatePanel(GarageCarStatsDisplay display, Canvas canvas)
    {
        var serializedDisplay = new SerializedObject(display);
        var panelProperty = serializedDisplay.FindProperty("panelRoot");
        GameObject panel = panelProperty.objectReferenceValue as GameObject;

        if (panel == null)
        {
            var existing = canvas.transform.Find("GarageCarStats");
            if (existing != null)
            {
                panel = existing.gameObject;
            }
            else
            {
                panel = CreatePanelHierarchy(display, canvas.transform);
            }
        }

        BindReferences(serializedDisplay, panel);
        serializedDisplay.FindProperty("targetCanvas").objectReferenceValue = canvas;
        serializedDisplay.ApplyModifiedProperties();
        EditorUtility.SetDirty(display);
        EditorSceneManager.MarkSceneDirty(display.gameObject.scene);
        Selection.activeGameObject = panel;
    }

    private static GameObject CreatePanelHierarchy(GarageCarStatsDisplay display, Transform canvas)
    {
        var serializedDisplay = new SerializedObject(display);
        var panel = new GameObject("GarageCarStats", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Undo.RegisterCreatedObjectUndo(panel, "Create Garage Car Stats Panel");
        panel.transform.SetParent(canvas, false);
        panel.layer = canvas.gameObject.layer;

        var panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = serializedDisplay.FindProperty("anchoredPosition").vector2Value;
        panelRect.sizeDelta = serializedDisplay.FindProperty("panelSize").vector2Value;

        var background = panel.GetComponent<Image>();
        background.color = new Color(0.035f, 0.045f, 0.06f, 0.86f);
        background.raycastTarget = false;

        CreateText(panelRect, "Header", "CAR PERFORMANCE", 18, TextAnchor.MiddleCenter,
            new Vector2(18f, -16f), new Vector2(-36f, 32f), new Vector2(0f, 1f), new Vector2(1f, 1f), panel.layer);
        CreateStatRow(panelRect, "Speed", "TOP SPEED", -55f, panel.layer);
        CreateStatSlider(panelRect, "SpeedSlider", -86f, panel.layer);
        CreateStatRow(panelRect, "EnginePower", "ENGINE POWER", -115f, panel.layer);
        CreateStatSlider(panelRect, "EnginePowerSlider", -146f, panel.layer);
        CreateStatRow(panelRect, "Handling", "HANDLING", -175f, panel.layer);
        CreateStatSlider(panelRect, "HandlingSlider", -206f, panel.layer);

        return panel;
    }

    private static Canvas FindGarageCanvas(GarageCarStatsDisplay display)
    {
        var canvases = Resources.FindObjectsOfTypeAll<Canvas>();
        for (int i = 0; i < canvases.Length; i++)
        {
            var canvas = canvases[i];
            if (canvas != null &&
                canvas.gameObject.scene == display.gameObject.scene &&
                canvas.gameObject.name == "Canvas")
            {
                return canvas;
            }
        }

        return null;
    }

    private static void BindReferences(SerializedObject serializedDisplay, GameObject panel)
    {
        serializedDisplay.FindProperty("panelRoot").objectReferenceValue = panel;
        serializedDisplay.FindProperty("speedValue").objectReferenceValue = FindText(panel, "SpeedValue");
        serializedDisplay.FindProperty("enginePowerValue").objectReferenceValue = FindText(panel, "EnginePowerValue");
        serializedDisplay.FindProperty("handlingValue").objectReferenceValue = FindText(panel, "HandlingValue");
        serializedDisplay.FindProperty("speedSlider").objectReferenceValue = FindSlider(panel, "SpeedSlider");
        serializedDisplay.FindProperty("enginePowerSlider").objectReferenceValue = FindSlider(panel, "EnginePowerSlider");
        serializedDisplay.FindProperty("handlingSlider").objectReferenceValue = FindSlider(panel, "HandlingSlider");
    }

    private static Text FindText(GameObject root, string name)
    {
        var child = root.transform.Find(name);
        return child != null ? child.GetComponent<Text>() : null;
    }

    private static Slider FindSlider(GameObject root, string name)
    {
        var child = root.transform.Find(name);
        return child != null ? child.GetComponent<Slider>() : null;
    }

    private static void CreateStatRow(Transform parent, string rowName, string label, float y, int layer)
    {
        CreateText(parent, rowName + "Label", label, 16, TextAnchor.MiddleLeft,
            new Vector2(18f, y), new Vector2(200f, 36f), new Vector2(0f, 1f), new Vector2(0f, 1f), layer);
        CreateText(parent, rowName + "Value", "—", 20, TextAnchor.MiddleRight,
            new Vector2(-16f, y), new Vector2(116f, 36f), new Vector2(1f, 1f), new Vector2(1f, 1f), layer);
    }

    private static Text CreateText(
        Transform parent,
        string objectName,
        string value,
        int fontSize,
        TextAnchor alignment,
        Vector2 position,
        Vector2 size,
        Vector2 anchorMin,
        Vector2 anchorMax,
        int layer)
    {
        var textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        Undo.RegisterCreatedObjectUndo(textObject, "Create Garage Car Stats Text");
        textObject.transform.SetParent(parent, false);
        textObject.layer = layer;

        var rect = (RectTransform)textObject.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = anchorMin;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        var text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = alignment;
        text.color = Color.white;
        text.text = value;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private static void CreateStatSlider(Transform parent, string objectName, float y, int layer)
    {
        var sliderObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Slider));
        Undo.RegisterCreatedObjectUndo(sliderObject, "Create Garage Car Stats Slider");
        sliderObject.transform.SetParent(parent, false);
        sliderObject.layer = layer;

        var sliderRect = (RectTransform)sliderObject.transform;
        sliderRect.anchorMin = new Vector2(0f, 1f);
        sliderRect.anchorMax = new Vector2(1f, 1f);
        sliderRect.pivot = new Vector2(0f, 1f);
        sliderRect.anchoredPosition = new Vector2(18f, y);
        sliderRect.sizeDelta = new Vector2(-36f, 14f);

        var background = sliderObject.GetComponent<Image>();
        background.color = new Color(0.18f, 0.2f, 0.23f, 1f);
        background.raycastTarget = false;

        var fillAreaObject = new GameObject("Fill Area", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(fillAreaObject, "Create Garage Car Stats Slider Fill Area");
        fillAreaObject.transform.SetParent(sliderObject.transform, false);
        fillAreaObject.layer = layer;
        var fillAreaRect = (RectTransform)fillAreaObject.transform;
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = new Vector2(2f, 2f);
        fillAreaRect.offsetMax = new Vector2(-2f, -2f);

        var fillObject = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Undo.RegisterCreatedObjectUndo(fillObject, "Create Garage Car Stats Slider Fill");
        fillObject.transform.SetParent(fillAreaObject.transform, false);
        fillObject.layer = layer;
        var fillRect = (RectTransform)fillObject.transform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        var fillImage = fillObject.GetComponent<Image>();
        fillImage.color = new Color(0.12f, 0.72f, 0.96f, 1f);
        fillImage.raycastTarget = false;

        var slider = sliderObject.GetComponent<Slider>();
        slider.fillRect = fillRect;
        slider.targetGraphic = background;
        slider.direction = Slider.Direction.LeftToRight;
        slider.interactable = false;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0f;
    }
}
