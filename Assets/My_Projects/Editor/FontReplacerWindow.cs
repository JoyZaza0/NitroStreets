using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
#if TMP_PRESENT || UNITY_2018_1_OR_NEWER
using TMPro;
#endif

namespace MyProjects.Editor
{
    public class FontReplacerWindow : EditorWindow
    {
        [Header("TextMeshPro Settings")]
        [SerializeField] private TMP_FontAsset targetTmpFont;
        [SerializeField] private bool replaceTmp = true;
        [SerializeField] private bool filterByCurrentTmpFont = false;
        [SerializeField] private TMP_FontAsset sourceTmpFont;

        [Header("Legacy UI Text Settings")]
        [SerializeField] private Font targetLegacyFont;
        [SerializeField] private bool replaceLegacyText = true;
        [SerializeField] private bool filterByCurrentLegacyFont = false;
        [SerializeField] private Font sourceLegacyFont;

        [Header("General Settings")]
        [SerializeField] private bool includeInactive = true;

        private Vector2 scrollPos;

        [MenuItem("Tools/Font Replacer")]
        public static void ShowWindow()
        {
            var window = GetWindow<FontReplacerWindow>("Font Replacer");
            window.minSize = new Vector2(360, 480);
        }

        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Замена шрифтов в текущей сцене", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Выберите новый шрифт и нажмите кнопку внизу, чтобы обновить все тексты в активной сцене. Изменения поддерживают отмену (Ctrl+Z).", MessageType.Info);
            EditorGUILayout.Space(10);

            includeInactive = EditorGUILayout.ToggleLeft(" Включать неактивные объекты", includeInactive);
            EditorGUILayout.Space(10);

            // --- TextMeshPro Section ---
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            replaceTmp = EditorGUILayout.ToggleLeft(" Заменять TextMeshPro (TMP)", replaceTmp, EditorStyles.boldLabel);
            if (replaceTmp)
            {
                EditorGUI.indentLevel++;
                targetTmpFont = (TMP_FontAsset)EditorGUILayout.ObjectField("Новый TMP шрифт:", targetTmpFont, typeof(TMP_FontAsset), false);

                filterByCurrentTmpFont = EditorGUILayout.ToggleLeft("Заменять только конкретный TMP шрифт", filterByCurrentTmpFont);
                if (filterByCurrentTmpFont)
                {
                    sourceTmpFont = (TMP_FontAsset)EditorGUILayout.ObjectField("Заменяемый TMP шрифт:", sourceTmpFont, typeof(TMP_FontAsset), false);
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // --- Legacy UI Text Section ---
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            replaceLegacyText = EditorGUILayout.ToggleLeft(" Заменять обычный UI Text (Legacy)", replaceLegacyText, EditorStyles.boldLabel);
            if (replaceLegacyText)
            {
                EditorGUI.indentLevel++;
                targetLegacyFont = (Font)EditorGUILayout.ObjectField("Новый Legacy шрифт:", targetLegacyFont, typeof(Font), false);

                filterByCurrentLegacyFont = EditorGUILayout.ToggleLeft("Заменять только конкретный Legacy шрифт", filterByCurrentLegacyFont);
                if (filterByCurrentLegacyFont)
                {
                    sourceLegacyFont = (Font)EditorGUILayout.ObjectField("Заменяемый Legacy шрифт:", sourceLegacyFont, typeof(Font), false);
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(20);

            bool canReplace = (replaceTmp && targetTmpFont != null) || (replaceLegacyText && targetLegacyFont != null);
            GUI.enabled = canReplace;

            if (GUILayout.Button("Применить замену шрифтов в сцене", GUILayout.Height(36)))
            {
                ReplaceFontsInCurrentScene();
            }

            GUI.enabled = true;

            if (!canReplace)
            {
                EditorGUILayout.HelpBox("Укажите хотя бы один целевой шрифт для замены.", MessageType.Warning);
            }

            EditorGUILayout.EndScrollView();
        }

        private void ReplaceFontsInCurrentScene()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (!activeScene.isLoaded)
            {
                EditorUtility.DisplayDialog("Ошибка", "Активная сцена не загружена!", "OK");
                return;
            }

            GameObject[] rootObjects = activeScene.GetRootGameObjects();
            int tmpModifiedCount = 0;
            int legacyModifiedCount = 0;

            foreach (var root in rootObjects)
            {
                // Замена TextMeshPro
                if (replaceTmp && targetTmpFont != null)
                {
                    var tmpTexts = root.GetComponentsInChildren<TMP_Text>(includeInactive);
                    foreach (var tmp in tmpTexts)
                    {
                        if (filterByCurrentTmpFont && tmp.font != sourceTmpFont)
                            continue;

                        if (tmp.font != targetTmpFont)
                        {
                            Undo.RecordObject(tmp, "Replace TMP Font");
                            tmp.font = targetTmpFont;
                            EditorUtility.SetDirty(tmp);
                            tmpModifiedCount++;
                        }
                    }
                }

                // Замена Legacy Text
                if (replaceLegacyText && targetLegacyFont != null)
                {
                    var legacyTexts = root.GetComponentsInChildren<Text>(includeInactive);
                    foreach (var txt in legacyTexts)
                    {
                        if (filterByCurrentLegacyFont && txt.font != sourceLegacyFont)
                            continue;

                        if (txt.font != targetLegacyFont)
                        {
                            Undo.RecordObject(txt, "Replace Legacy Font");
                            txt.font = targetLegacyFont;
                            EditorUtility.SetDirty(txt);
                            legacyModifiedCount++;
                        }
                    }
                }
            }

            if (tmpModifiedCount > 0 || legacyModifiedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorUtility.DisplayDialog(
                    "Готово",
                    $"Шрифты успешно заменены в сцене \"{activeScene.name}\"!\n\n" +
                    $"• TextMeshPro: {tmpModifiedCount} объектов\n" +
                    $"• UI Text (Legacy): {legacyModifiedCount} объектов\n\n" +
                    $"Не забудьте сохранить сцену (Ctrl+S). Изменения можно отменить через Ctrl+Z.",
                    "OK"
                );
            }
            else
            {
                EditorUtility.DisplayDialog("Информация", "Подходящих текстовых компонентов для замены не найдено.", "OK");
            }
        }
    }
}
