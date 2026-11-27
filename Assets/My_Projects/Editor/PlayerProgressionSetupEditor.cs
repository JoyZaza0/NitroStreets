#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NitroStreets.EditorTools
{
    [InitializeOnLoad]
    public static class PlayerProgressionSetupEditor
    {
        private const string RaceManagerPrefabPath = "Assets/Racing_Game/Prefabs/Manager/Race_Manager.prefab";
        private const string TopUiProgressionPanelName = "ProgressionPanel";

        static PlayerProgressionSetupEditor()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    return;

                EnsureProgressionPanelInRaceManagerTopUI(false);

                if (EditorSceneManager.GetActiveScene().name == "Garage")
                {
                    SyncProgressionUI(false);
                }
            };

            EditorSceneManager.sceneOpened += (scene, mode) =>
            {
                if (scene.name == "Garage")
                {
                    SyncProgressionUI(false);
                }
            };
        }

        [MenuItem("Tools/NitroStreets/Setup Race Progression In TopUI")]
        public static void SetupRaceProgressionInTopUIMenu()
        {
            EnsureProgressionPanelInRaceManagerTopUI(true);
        }

        public static void EnsureProgressionPanelInRaceManagerTopUI(bool openPrefab)
        {
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(RaceManagerPrefabPath);
            if (prefabAsset == null)
            {
                if (openPrefab)
                    EditorUtility.DisplayDialog("NitroStreets", $"Не найден префаб: {RaceManagerPrefabPath}", "OK");
                return;
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(RaceManagerPrefabPath);
            bool saved = false;
            try
            {
                Transform topUi = FindTopUiInRaceManager(prefabRoot.transform);
                if (topUi == null)
                {
                    Debug.LogWarning($"[PlayerProgression] Не найден Start_UI/TopUI внутри {RaceManagerPrefabPath}.");
                    return;
                }

                RaceProgressionPanelView existingView = topUi.GetComponentInChildren<RaceProgressionPanelView>(true);
                if (existingView == null)
                {
                    Font uiFont = FindFontInTopUi(topUi);
                    BuildTopUiProgressionPanel(topUi, uiFont);
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, RaceManagerPrefabPath);
                    AssetDatabase.SaveAssets();
                    saved = true;
                    Debug.Log($"[PlayerProgression] Панель прогрессии добавлена в {RaceManagerPrefabPath} -> Start_UI/TopUI.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            if (openPrefab)
            {
                GameObject updatedAsset = AssetDatabase.LoadAssetAtPath<GameObject>(RaceManagerPrefabPath);
                if (updatedAsset != null)
                {
                    Selection.activeObject = updatedAsset;
                    EditorGUIUtility.PingObject(updatedAsset);
                    AssetDatabase.OpenAsset(updatedAsset);
                }

                if (saved)
                {
                    EditorUtility.DisplayDialog("NitroStreets", "Панель прогрессии добавлена в Race_Manager -> Racing_UI -> Start_UI -> TopUI. Теперь можно визуально настроить её прямо рядом с Total Coins.", "OK");
                }
            }
        }

        private static Transform FindTopUiInRaceManager(Transform root)
        {
            Transform direct = root.Find("Racing_UI/Start_UI/TopUI");
            if (direct != null)
                return direct;

            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == "TopUI" && all[i].parent != null && all[i].parent.name == "Start_UI")
                    return all[i];
            }

            return null;
        }

        private static Font FindFontInTopUi(Transform topUi)
        {
            Text[] texts = topUi.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].font != null)
                    return texts[i].font;
            }

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static GameObject BuildTopUiProgressionPanel(Transform topUi, Font font)
        {
            Image panel = CreateUiImage(TopUiProgressionPanelName, topUi, new Color(0.09f, 0.13f, 0.19f, 0.85f));
            RectTransform panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(0f, 0.5f);
            panelRect.anchorMax = new Vector2(0f, 0.5f);
            panelRect.pivot = new Vector2(0f, 0.5f);
            panelRect.anchoredPosition = new Vector2(16f, 0f);
            panelRect.sizeDelta = new Vector2(360f, 56f);

            RaceProgressionPanelView view = panel.gameObject.AddComponent<RaceProgressionPanelView>();

            Text levelText = CreateUiText("LevelText", panel.transform, "LV. 1",
                new Vector2(12f, -4f), new Vector2(140f, 26f), 20, TextAnchor.MiddleLeft, font, new Color(1f, 0.84f, 0.25f, 1f));
            levelText.fontStyle = FontStyle.Bold;

            Text experienceText = CreateUiText("ExperienceText", panel.transform, "0/100 XP",
                new Vector2(-12f, -4f), new Vector2(180f, 26f), 16, TextAnchor.MiddleRight, font, new Color(0.92f, 0.95f, 1f, 1f));
            RectTransform expRect = experienceText.rectTransform;
            expRect.anchorMin = new Vector2(1f, 1f);
            expRect.anchorMax = new Vector2(1f, 1f);
            expRect.pivot = new Vector2(1f, 1f);
            expRect.anchoredPosition = new Vector2(-12f, -4f);

            Image sliderBg = CreateUiImage("ExperienceSlider", panel.transform, new Color(0.035f, 0.055f, 0.085f, 1f));
            RectTransform sliderRect = sliderBg.rectTransform;
            sliderRect.anchorMin = new Vector2(0f, 0f);
            sliderRect.anchorMax = new Vector2(1f, 0f);
            sliderRect.pivot = new Vector2(0.5f, 0f);
            sliderRect.anchoredPosition = new Vector2(0f, 8f);
            sliderRect.sizeDelta = new Vector2(-24f, 14f);

            Image fill = CreateUiImage("Fill", sliderBg.transform, new Color(0.12f, 0.55f, 0.9f, 1f));
            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            Slider slider = sliderBg.gameObject.AddComponent<Slider>();
            slider.targetGraphic = sliderBg;
            slider.fillRect = fillRect;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = PlayerProgression.ExperiencePerLevel;
            slider.wholeNumbers = true;
            slider.interactable = false;
            slider.SetValueWithoutNotify(0f);

            view.experienceSlider = slider;
            view.levelText = levelText;
            view.experienceText = experienceText;

            return panel.gameObject;
        }

        private static Image CreateUiImage(string objectName, Transform parent, Color color)
        {
            GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text CreateUiText(string objectName, Transform parent, string defaultText, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment, Font font, Color color)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer));
            textObject.transform.SetParent(parent, false);

            Text text = textObject.AddComponent<Text>();
            text.text = defaultText;
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;

            RectTransform rectTransform = text.rectTransform;
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.anchoredPosition = position;
            rectTransform.sizeDelta = size;
            return text;
        }

        [MenuItem("Tools/NitroStreets/Sync Progression UI Across All Menus")]
        public static void SyncProgressionUIMenu()
        {
            SyncProgressionUI(true);
        }

        public static void SyncProgressionUI(bool showNotification)
        {
            Scene activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.name != "Garage")
            {
                if (showNotification)
                    EditorUtility.DisplayDialog("NitroStreets", "Откройте сцену 'Garage' перед запуском синхронизации.", "OK");
                return;
            }

            Canvas mainCanvas = Object.FindFirstObjectByType<Canvas>();
            if (mainCanvas == null)
            {
                Debug.LogWarning("[PlayerProgression] Canvas не найден в сцене Garage.");
                return;
            }

            Transform[] allTransforms = mainCanvas.GetComponentsInChildren<Transform>(true);
            List<Transform> allProfiles = new List<Transform>();
            Transform sourceProfile = null;

            foreach (Transform t in allTransforms)
            {
                if (t != null && t.name == "Player_Profile")
                {
                    allProfiles.Add(t);
                    if (t.Find("ProgressionBar") != null && t.Find("Player_Name_Panel") != null)
                    {
                        sourceProfile = t;
                    }
                }
            }

            if (sourceProfile == null)
            {
                if (showNotification)
                    EditorUtility.DisplayDialog("NitroStreets", "Не найден эталонный Player_Profile (с настроенными ProgressionBar и Player_Name_Panel).", "OK");
                return;
            }

            Transform sourceProgBar = sourceProfile.Find("ProgressionBar");
            Transform sourceNamePanel = sourceProfile.Find("Player_Name_Panel");

            var otherEntries = new List<(Slider slider, Text levelText, Text expText)>();
            bool modifiedAny = false;

            foreach (Transform profile in allProfiles)
            {
                if (profile == null)
                    continue;

                // 1. Синхронизируем положение плашки профиля
                RectTransform profileRect = profile.GetComponent<RectTransform>();
                if (profileRect != null && Mathf.Abs(profileRect.anchoredPosition.x - 413.9f) > 0.01f)
                {
                    Undo.RecordObject(profileRect, "Align Player_Profile");
                    profileRect.anchoredPosition = new Vector2(413.9f, profileRect.anchoredPosition.y);
                    modifiedAny = true;
                }

                if (profile == sourceProfile)
                    continue;

                // 2. Копируем Player_Name_Panel, если его нет
                Transform targetNamePanel = profile.Find("Player_Name_Panel");
                if (targetNamePanel == null && sourceNamePanel != null)
                {
                    GameObject clonedPanel = Object.Instantiate(sourceNamePanel.gameObject, profile);
                    Undo.RegisterCreatedObjectUndo(clonedPanel, "Create Player_Name_Panel");
                    clonedPanel.name = "Player_Name_Panel";
                    targetNamePanel = clonedPanel.transform;
                    targetNamePanel.SetSiblingIndex(1);

                    RectTransform targetPanelRect = clonedPanel.GetComponent<RectTransform>();
                    RectTransform srcPanelRect = sourceNamePanel.GetComponent<RectTransform>();
                    targetPanelRect.anchorMin = srcPanelRect.anchorMin;
                    targetPanelRect.anchorMax = srcPanelRect.anchorMax;
                    targetPanelRect.anchoredPosition = srcPanelRect.anchoredPosition;
                    targetPanelRect.sizeDelta = srcPanelRect.sizeDelta;
                    targetPanelRect.pivot = srcPanelRect.pivot;

                    // Удаляем склонированные дочерние элементы
                    for (int i = targetNamePanel.childCount - 1; i >= 0; i--)
                    {
                        Object.DestroyImmediate(targetNamePanel.GetChild(i).gameObject);
                    }

                    // Переносим существующий Player_Name_Text внутрь Player_Name_Panel (сохраняя связь в PlayerNameDisplay)
                    Text existingNameText = null;
                    for (int i = 0; i < profile.childCount; i++)
                    {
                        Transform child = profile.GetChild(i);
                        if (child != targetNamePanel && (child.name == "Player_Name_Text" || child.name.Contains("Name")))
                        {
                            existingNameText = child.GetComponent<Text>();
                            if (existingNameText != null)
                                break;
                        }
                    }

                    if (existingNameText != null)
                    {
                        RectTransform nameRect = existingNameText.rectTransform;
                        Undo.SetTransformParent(nameRect, targetNamePanel, "Reparent Player_Name_Text");
                        nameRect.anchorMin = Vector2.zero;
                        nameRect.anchorMax = Vector2.one;
                        nameRect.anchoredPosition = Vector2.zero;
                        nameRect.sizeDelta = new Vector2(-50f, -20f);
                        nameRect.pivot = new Vector2(0.5f, 0.5f);
                        existingNameText.alignment = TextAnchor.MiddleLeft;
                    }

                    modifiedAny = true;
                }

                // 3. Копируем ProgressionBar, если его нет
                Transform targetProgBar = profile.Find("ProgressionBar");
                if (targetProgBar == null && sourceProgBar != null)
                {
                    GameObject clonedBar = Object.Instantiate(sourceProgBar.gameObject, profile);
                    Undo.RegisterCreatedObjectUndo(clonedBar, "Create ProgressionBar");
                    clonedBar.name = "ProgressionBar";
                    targetProgBar = clonedBar.transform;

                    RectTransform targetBarRect = clonedBar.GetComponent<RectTransform>();
                    RectTransform srcBarRect = sourceProgBar.GetComponent<RectTransform>();
                    targetBarRect.anchorMin = srcBarRect.anchorMin;
                    targetBarRect.anchorMax = srcBarRect.anchorMax;
                    targetBarRect.anchoredPosition = srcBarRect.anchoredPosition;
                    targetBarRect.sizeDelta = srcBarRect.sizeDelta;
                    targetBarRect.pivot = srcBarRect.pivot;

                    modifiedAny = true;
                }

                // 4. Запоминаем UI-компоненты для PlayerProgressionDisplay
                if (targetProgBar != null)
                {
                    Slider s = targetProgBar.GetComponentInChildren<Slider>(true);
                    Text lvl = targetProgBar.Find("LevelBadge")?.GetComponent<Text>();
                    Text exp = targetProgBar.Find("ExperienceText")?.GetComponent<Text>();
                    if (s != null || lvl != null || exp != null)
                    {
                        otherEntries.Add((s, lvl, exp));
                    }
                }
            }

            // 5. Настраиваем PlayerProgressionDisplay на объекте Canvas
            PlayerProgressionDisplay progressionDisplay = mainCanvas.GetComponent<PlayerProgressionDisplay>();
            if (progressionDisplay == null)
            {
                progressionDisplay = Undo.AddComponent<PlayerProgressionDisplay>(mainCanvas.gameObject);
                modifiedAny = true;
            }

            SerializedObject so = new SerializedObject(progressionDisplay);

            Slider mainSlider = sourceProgBar != null ? sourceProgBar.GetComponentInChildren<Slider>(true) : null;
            Text mainLevelText = sourceProgBar != null ? sourceProgBar.Find("LevelBadge")?.GetComponent<Text>() : null;
            Text mainExpText = sourceProgBar != null ? sourceProgBar.Find("ExperienceText")?.GetComponent<Text>() : null;

            so.FindProperty("experienceSlider").objectReferenceValue = mainSlider;
            so.FindProperty("levelText").objectReferenceValue = mainLevelText;
            so.FindProperty("experienceText").objectReferenceValue = mainExpText;
            so.FindProperty("autoAttachToPlayerProfiles").boolValue = false;

            SerializedProperty additionalProp = so.FindProperty("additionalUiEntries");
            additionalProp.arraySize = otherEntries.Count;
            for (int i = 0; i < otherEntries.Count; i++)
            {
                SerializedProperty element = additionalProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("experienceSlider").objectReferenceValue = otherEntries[i].slider;
                element.FindPropertyRelative("levelText").objectReferenceValue = otherEntries[i].levelText;
                element.FindPropertyRelative("experienceText").objectReferenceValue = otherEntries[i].expText;
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(progressionDisplay);
            modifiedAny = true;

            if (modifiedAny)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveScene(activeScene);
                Debug.Log($"[PlayerProgression] Успешно скопированы и привязаны элементы прогрессии для всех {allProfiles.Count} меню Гаража!");
                if (showNotification)
                {
                    EditorUtility.DisplayDialog("NitroStreets", $"Готово! Прогрессия успешно скопирована и настроена для всех {allProfiles.Count} меню Гаража.", "OK");
                }
            }
        }
    }
}
#endif
