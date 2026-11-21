#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using NitroStreets.Ads;

namespace NitroStreets.EditorTools
{
    /// <summary>
    /// Автоматически определяет наличие плагина Google Mobile Ads в проекте и выставляет символ компиляции GOOGLE_MOBILE_ADS,
    /// а также добавляет удобное окно настройки в меню Tools -> AdMob.
    /// </summary>
    [InitializeOnLoad]
    public static class AdMobDefineSymbolAutoDetector
    {
        private const string DefineSymbol = "GOOGLE_MOBILE_ADS";

        static AdMobDefineSymbolAutoDetector()
        {
            EditorApplication.delayCall += SyncDefineSymbol;
        }

        public static bool IsGoogleMobileAdsSdkInstalled()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (assembly.GetType("GoogleMobileAds.Api.MobileAds", false) != null)
                        return true;
                }
                catch
                {
                    // Игнорируем динамические сборки
                }
            }
            return false;
        }

        public static void SyncDefineSymbol()
        {
            bool sdkInstalled = IsGoogleMobileAdsSdkInstalled();

            NamedBuildTarget[] targets = new[]
            {
                NamedBuildTarget.Android,
                NamedBuildTarget.iOS,
                NamedBuildTarget.Standalone
            };

            foreach (NamedBuildTarget target in targets)
            {
                try
                {
                    PlayerSettings.GetScriptingDefineSymbols(target, out string[] defines);
                    bool hasDefine = defines.Contains(DefineSymbol);

                    if (sdkInstalled && !hasDefine)
                    {
                        var updated = defines.ToList();
                        updated.Add(DefineSymbol);
                        PlayerSettings.SetScriptingDefineSymbols(target, updated.ToArray());
                        Debug.Log($"[AdMob Setup] Обнаружен Google Mobile Ads SDK. Символ '{DefineSymbol}' автоматически активирован для {target.TargetName}.");
                    }
                    else if (!sdkInstalled && hasDefine)
                    {
                        var updated = defines.Where(d => d != DefineSymbol).ToArray();
                        PlayerSettings.SetScriptingDefineSymbols(target, updated);
                        Debug.Log($"[AdMob Setup] Плагин Google Mobile Ads не найден. Символ '{DefineSymbol}' снят для {target.TargetName} (включён безопасный режим симуляции).");
                    }
                }
                catch
                {
                    // Игнорируем неактивные платформы
                }
            }
        }
    }

    public class AdMobSetupWindow : EditorWindow
    {
        private const string OfficialReleasesUrl = "https://github.com/googleads/googleads-mobile-unity/releases";
        private const string ResourcesPrefabPath = "Assets/Resources/AdMobManager.prefab";

        [MenuItem("Tools/AdMob/Панель настройки AdMob", priority = 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<AdMobSetupWindow>("AdMob Setup");
            window.minSize = new Vector2(440, 380);
            window.Show();
        }

        [MenuItem("Tools/AdMob/Создать префаб AdMobManager в Resources", priority = 101)]
        public static void CreateOrSelectResourcesPrefab()
        {
            if (!Directory.Exists("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ResourcesPrefabPath);
            if (existingPrefab != null)
            {
                Selection.activeObject = existingPrefab;
                EditorGUIUtility.PingObject(existingPrefab);
                Debug.Log("[AdMob Setup] Префаб AdMobManager уже существует в Assets/Resources/AdMobManager.prefab и выделен в окне Project.");
                return;
            }

            GameObject tempGo = new GameObject("AdMobManager");
            tempGo.AddComponent<AdMobManager>();

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(tempGo, ResourcesPrefabPath);
            DestroyImmediate(tempGo);

            AssetDatabase.SaveAssets();
            Selection.activeObject = savedPrefab;
            EditorGUIUtility.PingObject(savedPrefab);
            Debug.Log("[AdMob Setup] Создан префаб Assets/Resources/AdMobManager.prefab! Вы можете настроить свои Ad Unit ID прямо в его Инспекторе.");
        }

        [MenuItem("Tools/AdMob/Добавить AdMobManager на текущую сцену", priority = 102)]
        public static void CreateOnCurrentScene()
        {
            AdMobManager existing = UnityEngine.Object.FindFirstObjectByType<AdMobManager>();
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing.gameObject);
                return;
            }

            GameObject go = new GameObject("[AdMobManager]");
            Undo.RegisterCreatedObjectUndo(go, "Create AdMobManager");
            go.AddComponent<AdMobManager>();
            Selection.activeGameObject = go;
        }

        private void OnGUI()
        {
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Интеграция Google AdMob (NitroStreets)", EditorStyles.boldLabel);
            GUILayout.Space(6);

            bool sdkInstalled = AdMobDefineSymbolAutoDetector.IsGoogleMobileAdsSdkInstalled();

            if (sdkInstalled)
            {
                EditorGUILayout.HelpBox(
                    "Плагин Google Mobile Ads SDK обнаружен и активен (GOOGLE_MOBILE_ADS включён).\n" +
                    "Не забудьте указать ваш App ID в меню: Assets -> Google Mobile Ads -> Settings.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Плагин Google Mobile Ads (.unitypackage) ещё не импортирован в проект.\n" +
                    "Сейчас работает безопасный режим симуляции (все кнопки и вызовы рекламы работают без ошибок компиляции).\n" +
                    "Как только вы импортируете GoogleMobileAds.unitypackage, реальный SDK включится автоматически!",
                    MessageType.Warning);

                if (GUILayout.Button("Скачать официальный Google Mobile Ads Unity Plugin (GitHub)", GUILayout.Height(30)))
                {
                    Application.OpenURL(OfficialReleasesUrl);
                }
            }

            GUILayout.Space(12);
            EditorGUILayout.LabelField("Быстрая настройка:", EditorStyles.boldLabel);

            if (GUILayout.Button("Создать / Выделить префаб AdMobManager (Assets/Resources)", GUILayout.Height(32)))
            {
                CreateOrSelectResourcesPrefab();
            }

            if (GUILayout.Button("Добавить [AdMobManager] на текущую сцену", GUILayout.Height(28)))
            {
                CreateOnCurrentScene();
            }

            if (GUILayout.Button("Проверить статус SDK повторно", GUILayout.Height(24)))
            {
                AdMobDefineSymbolAutoDetector.SyncDefineSymbol();
            }

            GUILayout.Space(12);
            EditorGUILayout.LabelField("Что уже подключено в коде:", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "1. Баннер (Banner): автоматически показывается в сцене Garage и скрывается во время гонки.\n" +
                "2. Межстраничная реклама (Interstitial): показывается при нажатии кнопок 'Exit' (Выход в меню) или 'Restart' (Рестарт) на панели окончания заезда (Pause_Menu / GameOver), после чего загружается выбранная сцена.\n" +
                "3. Вознаграждаемая реклама (Rewarded):\n" +
                "   • Экран GameOver: кнопка 'Watch Ad' восстанавливает здоровье машины и продолжает гонку.\n" +
                "   • Экран Финиша (Start_Finish_UI): метод WatchAdToDoubleReward() удваивает призовые монеты.\n" +
                "   • Любая кнопка в Гараже: повесьте компонент 'RewardedAdButton' на любую UI-кнопку для выдачи бонусных монет!",
                MessageType.None);
        }
    }
}
#endif
