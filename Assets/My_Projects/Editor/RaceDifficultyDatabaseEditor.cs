#if UNITY_EDITOR
using System.IO;
using NitroStreets.Difficulty;
using UnityEditor;
using UnityEngine;

namespace NitroStreets.EditorTools
{
    [InitializeOnLoad]
    public static class RaceDifficultyDatabaseMenu
    {
        public const string DefaultAssetPath = "Assets/Resources/RaceDifficultyDatabase.asset";

        static RaceDifficultyDatabaseMenu()
        {
            EditorApplication.delayCall += EnsureAssetExistsSilently;
        }

        private static void EnsureAssetExistsSilently()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (AssetDatabase.LoadAssetAtPath<RaceDifficultyDatabase>(DefaultAssetPath) == null)
            {
                CreateOrLoadDatabaseAsset(selectInProject: false);
            }
        }

        [MenuItem("Tools/NitroStreets/Open Race Difficulty Database")]
        public static void OpenOrCreateDatabase()
        {
            CreateOrLoadDatabaseAsset(selectInProject: true);
        }

        public static RaceDifficultyDatabase CreateOrLoadDatabaseAsset(bool selectInProject)
        {
            RaceDifficultyDatabase db = AssetDatabase.LoadAssetAtPath<RaceDifficultyDatabase>(DefaultAssetPath);
            if (db == null)
            {
                string directory = Path.GetDirectoryName(DefaultAssetPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    AssetDatabase.Refresh();
                }

                db = ScriptableObject.CreateInstance<RaceDifficultyDatabase>();
                db.EnsureDefaultModesInitialized();
                AssetDatabase.CreateAsset(db, DefaultAssetPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[RaceDifficultyDatabase] Created default asset at {DefaultAssetPath}", db);
            }
            else if (db.EnsureDefaultModesInitialized())
            {
                EditorUtility.SetDirty(db);
                AssetDatabase.SaveAssets();
            }

            if (selectInProject)
            {
                Selection.activeObject = db;
                EditorGUIUtility.PingObject(db);
            }

            return db;
        }
    }

    [CustomEditor(typeof(RaceDifficultyDatabase))]
    public sealed class RaceDifficultyDatabaseEditor : Editor
    {
        private SerializedProperty modesProp;
        private int selectedModeTab = 0;
        private bool showGeneratorFoldout = false;

        private GUIStyle headerStyle;
        private GUIStyle levelTitleStyle;
        private GUIStyle miniLabelStyle;

        private void OnEnable()
        {
            modesProp = serializedObject.FindProperty("modes");
        }

        private void EnsureStyles()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 12
                };
            }

            if (levelTitleStyle == null)
            {
                levelTitleStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleLeft
                };
            }

            if (miniLabelStyle == null)
            {
                miniLabelStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleLeft
                };
            }
        }

        public override void OnInspectorGUI()
        {
            EnsureStyles();
            serializedObject.Update();

            var db = (RaceDifficultyDatabase)target;

            DrawLocationNotice(db);

            if (modesProp == null || modesProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox("Список режимов пуст. Нажмите кнопку ниже, чтобы инициализировать режимы.", MessageType.Warning);
                if (GUILayout.Button("Initialize Default Modes", GUILayout.Height(28f)))
                {
                    Undo.RecordObject(db, "Initialize Race Difficulty Modes");
                    db.EnsureDefaultModesInitialized();
                    EditorUtility.SetDirty(db);
                    serializedObject.Update();
                }

                serializedObject.ApplyModifiedProperties();
                return;
            }

            // Tab selector for GameModes
            string[] tabLabels = new string[modesProp.arraySize];
            for (int i = 0; i < modesProp.arraySize; i++)
            {
                SerializedProperty modeEntry = modesProp.GetArrayElementAtIndex(i);
                SerializedProperty modeEnumProp = modeEntry.FindPropertyRelative("mode");
                SerializedProperty applyProp = modeEntry.FindPropertyRelative("applyOverrides");
                string modeName = modeEnumProp.enumDisplayNames[modeEnumProp.enumValueIndex];
                tabLabels[i] = applyProp.boolValue ? $"{modeName} (ON)" : $"{modeName} (OFF)";
            }

            selectedModeTab = Mathf.Clamp(selectedModeTab, 0, modesProp.arraySize - 1);
            selectedModeTab = GUILayout.Toolbar(selectedModeTab, tabLabels, GUILayout.Height(26f));

            EditorGUILayout.Space(8f);

            SerializedProperty currentModeProp = modesProp.GetArrayElementAtIndex(selectedModeTab);
            DrawModeSettings(db, currentModeProp, selectedModeTab);

            EditorGUILayout.Space(10f);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Sync Missing GameModes", GUILayout.Width(180f)))
                {
                    Undo.RecordObject(db, "Sync Missing GameModes");
                    if (db.EnsureDefaultModesInitialized())
                    {
                        EditorUtility.SetDirty(db);
                        serializedObject.Update();
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawLocationNotice(RaceDifficultyDatabase db)
        {
            string assetPath = AssetDatabase.GetAssetPath(db);
            if (!string.IsNullOrEmpty(assetPath) &&
                !assetPath.Equals(RaceDifficultyDatabaseMenu.DefaultAssetPath, System.StringComparison.OrdinalIgnoreCase))
            {
                EditorGUILayout.HelpBox(
                    $"Для автоматической загрузки в Race_Manager без ручной привязки на сценах ассет должен лежать по пути:\n{RaceDifficultyDatabaseMenu.DefaultAssetPath}\nТекущий путь: {assetPath}",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Race_Manager автоматически подхватывает эту базу из Resources по активному GameMode и levelID.",
                    MessageType.Info);
            }
        }

        private void DrawModeSettings(RaceDifficultyDatabase db, SerializedProperty modeProp, int modeIndex)
        {
            SerializedProperty modeEnumProp = modeProp.FindPropertyRelative("mode");
            SerializedProperty applyOverridesProp = modeProp.FindPropertyRelative("applyOverrides");
            SerializedProperty clampProp = modeProp.FindPropertyRelative("clampOutOfRangeLevel");
            SerializedProperty generatorProp = modeProp.FindPropertyRelative("generator");
            SerializedProperty levelsProp = modeProp.FindPropertyRelative("levels");

            string modeName = modeEnumProp.enumDisplayNames[modeEnumProp.enumValueIndex];

            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField($"Mode: {modeName}", headerStyle);
                EditorGUILayout.PropertyField(applyOverridesProp, new GUIContent("Apply Overrides"));

                if (!applyOverridesProp.boolValue)
                {
                    EditorGUILayout.HelpBox(
                        $"Переопределение для режима {modeName} выключено. Гонщики используют оригинальные настройки своих префабов.",
                        MessageType.None);
                    return;
                }

                EditorGUILayout.PropertyField(clampProp, new GUIContent("Clamp Out-Of-Range Level"));
            }

            EditorGUILayout.Space(6f);

            // Progression generator foldout
            using (new EditorGUILayout.VerticalScope("box"))
            {
                showGeneratorFoldout = EditorGUILayout.Foldout(
                    showGeneratorFoldout,
                    "Auto-Generate Levels (1..N) From Progression",
                    true);

                if (showGeneratorFoldout)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(generatorProp, new GUIContent("Generator Settings"), true);
                    EditorGUI.indentLevel--;

                    EditorGUILayout.Space(4f);
                    if (GUILayout.Button($"Generate Levels For {modeName}", GUILayout.Height(26f)))
                    {
                        bool proceed = levelsProp.arraySize == 0 || EditorUtility.DisplayDialog(
                            "Generate Levels",
                            $"Перезаписать все уровни ({levelsProp.arraySize} шт.) для режима {modeName} значениями из генератора прогрессии?",
                            "Перезаписать",
                            "Отмена");

                        if (proceed)
                        {
                            serializedObject.ApplyModifiedProperties();
                            Undo.RecordObject(db, $"Generate {modeName} Difficulty Levels");
                            db.modes[modeIndex].GenerateLevelsFromProgression();
                            EditorUtility.SetDirty(db);
                            serializedObject.Update();
                        }
                    }
                }
            }

            EditorGUILayout.Space(6f);

            // Levels header + controls
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Levels ({levelsProp.arraySize})", headerStyle);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("+ Add Level", GUILayout.Width(95f)))
                {
                    int newIndex = levelsProp.arraySize;
                    levelsProp.InsertArrayElementAtIndex(newIndex);
                    SerializedProperty added = levelsProp.GetArrayElementAtIndex(newIndex);
                    added.FindPropertyRelative("label").stringValue = $"Level {newIndex + 1}";
                    if (newIndex == 0)
                    {
                        added.FindPropertyRelative("enginePower").floatValue = 2000f;
                        added.FindPropertyRelative("speedMin").floatValue = 50f;
                        added.FindPropertyRelative("speedMax").floatValue = 95f;
                        added.FindPropertyRelative("speedChangeInterval").floatValue = 5f;
                        added.FindPropertyRelative("enableNitro").boolValue = false;
                        added.FindPropertyRelative("nitroBoost").enumValueIndex = 1;
                        added.FindPropertyRelative("nitroRandomizerDelay").floatValue = 5f;
                    }
                }

                using (new EditorGUI.DisabledScope(levelsProp.arraySize == 0))
                {
                    if (GUILayout.Button("- Remove Last", GUILayout.Width(100f)))
                    {
                        levelsProp.DeleteArrayElementAtIndex(levelsProp.arraySize - 1);
                    }
                }
            }

            EditorGUILayout.Space(4f);

            if (levelsProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox(
                    "Список уровней пуст. Раскройте блок Auto-Generate выше или добавьте уровни кнопкой '+ Add Level'.",
                    MessageType.Info);
                return;
            }

            // Draw each level compactly
            float prevLabelWidth = EditorGUIUtility.labelWidth;
            for (int i = 0; i < levelsProp.arraySize; i++)
            {
                SerializedProperty levelProp = levelsProp.GetArrayElementAtIndex(i);
                DrawLevelRow(levelProp, i);
            }
            EditorGUIUtility.labelWidth = prevLabelWidth;
        }

        private void DrawLevelRow(SerializedProperty levelProp, int index)
        {
            SerializedProperty engineProp = levelProp.FindPropertyRelative("enginePower");
            SerializedProperty speedMinProp = levelProp.FindPropertyRelative("speedMin");
            SerializedProperty speedMaxProp = levelProp.FindPropertyRelative("speedMax");
            SerializedProperty intervalProp = levelProp.FindPropertyRelative("speedChangeInterval");
            SerializedProperty enableNitroProp = levelProp.FindPropertyRelative("enableNitro");
            SerializedProperty nitroBoostProp = levelProp.FindPropertyRelative("nitroBoost");
            SerializedProperty nitroDelayProp = levelProp.FindPropertyRelative("nitroRandomizerDelay");

            using (new EditorGUILayout.VerticalScope("box"))
            {
                // Row 1: Level title + Engine + Speed Min/Max + Interval
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"Lvl {index + 1}", levelTitleStyle, GUILayout.Width(44f));

                    EditorGUIUtility.labelWidth = 44f;
                    EditorGUILayout.PropertyField(engineProp, new GUIContent("Power"), GUILayout.MinWidth(95f));

                    EditorGUIUtility.labelWidth = 30f;
                    EditorGUILayout.PropertyField(speedMinProp, new GUIContent("Min"), GUILayout.MinWidth(72f));

                    EditorGUIUtility.labelWidth = 32f;
                    EditorGUILayout.PropertyField(speedMaxProp, new GUIContent("Max"), GUILayout.MinWidth(74f));

                    EditorGUIUtility.labelWidth = 46f;
                    EditorGUILayout.PropertyField(intervalProp, new GUIContent("Delay"), GUILayout.MinWidth(82f));
                }

                // Row 2: Nitro toggle + Nitro parameters
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(46f);

                    EditorGUIUtility.labelWidth = 40f;
                    EditorGUILayout.PropertyField(enableNitroProp, new GUIContent("Nitro"), GUILayout.Width(60f));

                    using (new EditorGUI.DisabledScope(!enableNitroProp.boolValue))
                    {
                        EditorGUIUtility.labelWidth = 42f;
                        EditorGUILayout.PropertyField(nitroBoostProp, new GUIContent("Boost"), GUILayout.MinWidth(95f));

                        EditorGUIUtility.labelWidth = 74f;
                        EditorGUILayout.PropertyField(nitroDelayProp, new GUIContent("Nitro Delay"), GUILayout.MinWidth(115f));
                    }

                    if (!enableNitroProp.boolValue)
                    {
                        EditorGUILayout.LabelField("(Racer_Nitro disabled)", miniLabelStyle, GUILayout.Width(125f));
                    }
                }
            }
        }
    }
}
#endif
