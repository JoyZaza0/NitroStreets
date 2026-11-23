#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ALIyerEdon;
using NitroStreets.Fuel;

namespace NitroStreets.EditorTools
{
    public class FuelSystemSetupEditor : EditorWindow
    {
        [MenuItem("Tools/Fuel System/Панель настройки и теста топлива", priority = 110)]
        public static void ShowWindow()
        {
            var window = GetWindow<FuelSystemSetupEditor>("Fuel System");
            window.minSize = new Vector2(440, 360);
            window.Show();
        }

        [MenuItem("Tools/Fuel System/Настроить UI топлива на сцене (Все режимы)", priority = 111)]
        public static void SetupCareerFuelUIOnScene()
        {
            LevelSelect[] levelSelects = Object.FindObjectsByType<LevelSelect>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (levelSelects == null || levelSelects.Length == 0)
            {
                Debug.LogWarning("[Fuel System] На текущей сцене не найден ни один объект с компонентом LevelSelect. Откройте сцену Garage.");
                return;
            }

            int configuredCount = 0;
            foreach (LevelSelect ls in levelSelects)
            {
                if (ls == null)
                    continue;

                Undo.RecordObject(ls, "Configure LevelSelect Fuel");

                if (ls.gameObject.name.IndexOf("Formula", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    ls.gameObject.name.IndexOf("F1", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    ls.gameMode = GameMode.Formula1;
                }
                else if (ls.gameObject.name.IndexOf("Offroad", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    ls.gameMode = GameMode.Offroad;
                }
                else
                {
                    ls.gameMode = GameMode.Career;
                }

                ls.useFuelSystem = true;
                ModeFuelUI fuelUI = ls.GetComponent<ModeFuelUI>();
                if (fuelUI == null)
                {
                    fuelUI = Undo.AddComponent<ModeFuelUI>(ls.gameObject);
                }

                fuelUI.TargetMode = ls.gameMode;
                fuelUI.EnsureUIExists();
                ls.modeFuelUI = fuelUI;

                EditorUtility.SetDirty(fuelUI);
                EditorUtility.SetDirty(ls);
                Selection.activeGameObject = ls.gameObject;
                configuredCount++;
            }

            if (configuredCount > 0 && !Application.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            }

            Debug.Log($"[Fuel System] UI топлива успешно создан и привязан на панелях выбора уровней ({configuredCount} шт.).");
        }

        [MenuItem("Tools/Fuel System/Тест: Установить 0/10 топлива (Career)", priority = 130)]
        public static void DebugSetZeroFuelCareer()
        {
            ModeFuelManager.SetFuelStatic(GameMode.Career, 0, 10);
            Debug.Log("[Fuel System] Установлено 0/10 топлива для режима Career.");
        }

        [MenuItem("Tools/Fuel System/Тест: Восстановить 10/10 топлива (Career)", priority = 131)]
        public static void DebugRefillFuelCareer()
        {
            ModeFuelManager.SetFuelStatic(GameMode.Career, 10, 10);
            Debug.Log("[Fuel System] Восстановлено 10/10 топлива для режима Career.");
        }

        [MenuItem("Tools/Fuel System/Тест: Установить 0/10 топлива (Formula 1)", priority = 132)]
        public static void DebugSetZeroFuelFormula1()
        {
            ModeFuelManager.SetFuelStatic(GameMode.Formula1, 0, 10);
            Debug.Log("[Fuel System] Установлено 0/10 топлива для режима Formula1.");
        }

        [MenuItem("Tools/Fuel System/Тест: Восстановить 10/10 топлива (Formula 1)", priority = 133)]
        public static void DebugRefillFuelFormula1()
        {
            ModeFuelManager.SetFuelStatic(GameMode.Formula1, 10, 10);
            Debug.Log("[Fuel System] Восстановлено 10/10 топлива для режима Formula1.");
        }

        private void OnGUI()
        {
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Система топлива по режимам (Mode Fuel System)", EditorStyles.boldLabel);
            GUILayout.Space(6);

            EditorGUILayout.HelpBox(
                "• Максимум топлива: 10/10 для каждого режима отдельно (Career / Formula1 / Offroad).\n" +
                "• Восстановление: +1 ед. каждые 10 минут (включая офлайн-таймер).\n" +
                "• Списание: -1 ед. в момент старта загрузки трассы (кнопка Restart в гонке топливо не тратит).\n" +
                "• При 0/10 топлива: клик по открытому уровню блокирует вход и показывает окно просмотра рекламы за +1 топливо.",
                MessageType.Info);

            GUILayout.Space(10);
            EditorGUILayout.LabelField("Авто-генерация UI на сцене Garage:", EditorStyles.boldLabel);

            if (GUILayout.Button("Сгенерировать / Настроить UI топлива для всех режимов", GUILayout.Height(34)))
            {
                SetupCareerFuelUIOnScene();
            }

            GUILayout.Space(14);
            EditorGUILayout.LabelField("Быстрое тестирование (Career):", EditorStyles.boldLabel);

            int currentCareer = PlayerPrefs.GetInt("ModeFuel_Career_Amount", 10);
            int currentFormula1 = PlayerPrefs.GetInt("ModeFuel_Formula1_Amount", 10);
            EditorGUILayout.LabelField($"Текущее топливо в PlayerPrefs — Career: {currentCareer}/10 | Formula 1: {currentFormula1}/10");

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Установить 0/10 (Career)", GUILayout.Height(28)))
            {
                DebugSetZeroFuelCareer();
            }
            if (GUILayout.Button("Списать -1 (Career)", GUILayout.Height(28)))
            {
                int next = Mathf.Max(0, PlayerPrefs.GetInt("ModeFuel_Career_Amount", 10) - 1);
                ModeFuelManager.SetFuelStatic(GameMode.Career, next, 10);
            }
            if (GUILayout.Button("Полный бак 10/10 (Career)", GUILayout.Height(28)))
            {
                DebugRefillFuelCareer();
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(8);
            EditorGUILayout.LabelField("Быстрое тестирование (Formula 1):", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Установить 0/10 (Formula 1)", GUILayout.Height(26)))
            {
                DebugSetZeroFuelFormula1();
            }
            if (GUILayout.Button("Списать -1 (Formula 1)", GUILayout.Height(26)))
            {
                int next = Mathf.Max(0, PlayerPrefs.GetInt("ModeFuel_Formula1_Amount", 10) - 1);
                ModeFuelManager.SetFuelStatic(GameMode.Formula1, next, 10);
            }
            if (GUILayout.Button("Полный бак 10/10 (Formula 1)", GUILayout.Height(26)))
            {
                DebugRefillFuelFormula1();
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif
