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
        static PlayerProgressionSetupEditor()
        {
            EditorApplication.delayCall += () =>
            {
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
