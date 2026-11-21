using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace NitroStreets.Fuel
{
    [Serializable]
    public class ModeFuelConfig
    {
        public GameMode mode = GameMode.Career;
        [Tooltip("Включена ли система топлива для этого режима")]
        public bool enabled = true;
        [Tooltip("Максимальный запас топлива в этом режиме")]
        [Min(1)]
        public int maxFuel = 10;
        [Tooltip("Время восстановления 1 единицы топлива в секундах (600 сек = 10 минут)")]
        [Min(5)]
        public int restoreIntervalSeconds = 600;
        [Tooltip("Сколько единиц топлива восстанавливается за просмотр одной рекламы")]
        [Min(1)]
        public int adRewardAmount = 1;
    }

    /// <summary>
    /// Менеджер системы топлива с независимым запасом и таймером для каждого игрового режима (GameMode).
    /// Поддерживает офлайн-восстановление через DateTime.UtcNow и сохранение в PlayerPrefs.
    /// </summary>
    public class ModeFuelManager : MonoBehaviour
    {
        public static ModeFuelManager Instance { get; private set; }

        [Header("Конфигурация топлива по режимам")]
        [SerializeField]
        private List<ModeFuelConfig> modeConfigs = new List<ModeFuelConfig>
        {
            new ModeFuelConfig
            {
                mode = GameMode.Career,
                enabled = true,
                maxFuel = 10,
                restoreIntervalSeconds = 600,
                adRewardAmount = 1
            },
            new ModeFuelConfig
            {
                mode = GameMode.Offroad,
                enabled = true,
                maxFuel = 10,
                restoreIntervalSeconds = 600,
                adRewardAmount = 1
            }
        };

        /// <summary>
        /// Текущий выбранный режим, для которого будет списано топливо при старте загрузки трассы.
        /// </summary>
        public GameMode ActiveMode { get; set; } = GameMode.Career;

        /// <summary>
        /// Событие изменения запаса топлива: (режим, текущее количество, максимум).
        /// </summary>
        public event Action<GameMode, int, int> OnFuelChanged;

        private float nextTickCheckTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            EnsureInstance();
        }

        public static ModeFuelManager EnsureInstance()
        {
            if (Instance != null)
                return Instance;

            ModeFuelManager existing = FindFirstObjectByType<ModeFuelManager>();
            if (existing != null)
            {
                Instance = existing;
                return Instance;
            }

            ModeFuelManager prefab = Resources.Load<ModeFuelManager>("ModeFuelManager");
            if (prefab != null)
            {
                Instance = Instantiate(prefab);
            }
            else
            {
                GameObject go = new GameObject("[ModeFuelManager]");
                Instance = go.AddComponent<ModeFuelManager>();
            }

            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }

            EnsureAllModesConfigured();
            ProcessAllModesRegeneration();
        }

        private void Update()
        {
            if (Time.unscaledTime >= nextTickCheckTime)
            {
                nextTickCheckTime = Time.unscaledTime + 0.5f;
                ProcessAllModesRegeneration();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                ProcessAllModesRegeneration();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (!pauseStatus)
            {
                ProcessAllModesRegeneration();
            }
        }

        private void EnsureAllModesConfigured()
        {
            foreach (GameMode mode in Enum.GetValues(typeof(GameMode)))
            {
                GetConfig(mode);
                InitializeModeStorageIfNeeded(mode);
            }
        }

        public ModeFuelConfig GetConfig(GameMode mode)
        {
            for (int i = 0; i < modeConfigs.Count; i++)
            {
                if (modeConfigs[i] != null && modeConfigs[i].mode == mode)
                    return modeConfigs[i];
            }

            ModeFuelConfig fallback = new ModeFuelConfig
            {
                mode = mode,
                enabled = true,
                maxFuel = 10,
                restoreIntervalSeconds = 600,
                adRewardAmount = 1
            };
            modeConfigs.Add(fallback);
            return fallback;
        }

        public bool IsFuelEnabled(GameMode mode)
        {
            return GetConfig(mode).enabled;
        }

        public int GetMaxFuel(GameMode mode)
        {
            return Mathf.Max(1, GetConfig(mode).maxFuel);
        }

        public int GetAdRewardAmount(GameMode mode)
        {
            return Mathf.Max(1, GetConfig(mode).adRewardAmount);
        }

        public int GetRestoreIntervalSeconds(GameMode mode)
        {
            return Mathf.Max(5, GetConfig(mode).restoreIntervalSeconds);
        }

        public int GetCurrentFuel(GameMode mode)
        {
            InitializeModeStorageIfNeeded(mode);
            ProcessRegeneration(mode);

            int max = GetMaxFuel(mode);
            int current = PlayerPrefs.GetInt(GetAmountKey(mode), max);
            return Mathf.Clamp(current, 0, max);
        }

        public bool HasFuel(GameMode mode, int required = 1)
        {
            if (!IsFuelEnabled(mode))
                return true;

            return GetCurrentFuel(mode) >= required;
        }

        /// <summary>
        /// Списывает указанное количество топлива для режима (вызывается в момент старта загрузки трассы).
        /// </summary>
        public bool TryConsumeFuel(GameMode mode, int amount = 1)
        {
            if (!IsFuelEnabled(mode) || amount <= 0)
                return true;

            InitializeModeStorageIfNeeded(mode);
            ProcessRegeneration(mode);

            int max = GetMaxFuel(mode);
            int current = Mathf.Clamp(PlayerPrefs.GetInt(GetAmountKey(mode), max), 0, max);

            if (current < amount)
                return false;

            bool wasFull = current >= max;
            current -= amount;

            PlayerPrefs.SetInt(GetAmountKey(mode), current);

            long lastTicks = GetStoredLastRestoreTicks(mode);
            if (current < max && (wasFull || lastTicks <= 0))
            {
                SetStoredLastRestoreTicks(mode, DateTime.UtcNow.Ticks);
            }

            PlayerPrefs.Save();
            OnFuelChanged?.Invoke(mode, current, max);
            Debug.Log($"[ModeFuelManager] Списано {amount} топлива в режиме {mode}. Осталось: {current}/{max}");
            return true;
        }

        /// <summary>
        /// Начисляет топливо для указанного режима (например, +1 за просмотр Rewarded Ad).
        /// </summary>
        public void AddFuel(GameMode mode, int amount = 1)
        {
            if (amount <= 0)
                return;

            InitializeModeStorageIfNeeded(mode);
            ProcessRegeneration(mode);

            int max = GetMaxFuel(mode);
            int current = Mathf.Clamp(PlayerPrefs.GetInt(GetAmountKey(mode), max), 0, max);
            int updated = Mathf.Clamp(current + amount, 0, max);

            PlayerPrefs.SetInt(GetAmountKey(mode), updated);

            if (updated >= max)
            {
                SetStoredLastRestoreTicks(mode, 0);
            }
            else if (GetStoredLastRestoreTicks(mode) <= 0)
            {
                SetStoredLastRestoreTicks(mode, DateTime.UtcNow.Ticks);
            }

            PlayerPrefs.Save();
            OnFuelChanged?.Invoke(mode, updated, max);
            Debug.Log($"[ModeFuelManager] Добавлено +{amount} топлива в режиме {mode}. Текущий запас: {updated}/{max}");
        }

        /// <summary>
        /// Устанавливает точное значение топлива для режима (удобно для тестирования и отладки).
        /// </summary>
        public static void SetFuelStatic(GameMode mode, int targetFuel, int maxFuelDefault = 10)
        {
            if (Instance != null)
            {
                Instance.SetFuel(mode, targetFuel);
                return;
            }

            int clamped = Mathf.Clamp(targetFuel, 0, maxFuelDefault);
            PlayerPrefs.SetInt(GetAmountKey(mode), clamped);
            PlayerPrefs.SetString(
                GetLastTicksKey(mode),
                (clamped < maxFuelDefault ? DateTime.UtcNow.Ticks : 0L).ToString(CultureInfo.InvariantCulture));
            PlayerPrefs.Save();
        }

        public void SetFuel(GameMode mode, int targetFuel)
        {
            InitializeModeStorageIfNeeded(mode);
            int max = GetMaxFuel(mode);
            int clamped = Mathf.Clamp(targetFuel, 0, max);

            PlayerPrefs.SetInt(GetAmountKey(mode), clamped);
            if (clamped >= max)
            {
                SetStoredLastRestoreTicks(mode, 0);
            }
            else
            {
                SetStoredLastRestoreTicks(mode, DateTime.UtcNow.Ticks);
            }

            PlayerPrefs.Save();
            OnFuelChanged?.Invoke(mode, clamped, max);
        }

        /// <summary>
        /// Возвращает количество секунд до восстановления следующей 1 единицы топлива.
        /// Если бак уже полон (10/10), возвращает false и 0 секунд.
        /// </summary>
        public bool TryGetSecondsUntilNextFuel(GameMode mode, out int remainingSeconds)
        {
            remainingSeconds = 0;
            if (!IsFuelEnabled(mode))
                return false;

            InitializeModeStorageIfNeeded(mode);
            ProcessRegeneration(mode);

            int max = GetMaxFuel(mode);
            int current = Mathf.Clamp(PlayerPrefs.GetInt(GetAmountKey(mode), max), 0, max);
            if (current >= max)
                return false;

            long lastTicks = GetStoredLastRestoreTicks(mode);
            long nowTicks = DateTime.UtcNow.Ticks;
            if (lastTicks <= 0 || lastTicks > nowTicks)
            {
                lastTicks = nowTicks;
                SetStoredLastRestoreTicks(mode, lastTicks);
                PlayerPrefs.Save();
            }

            int intervalSeconds = GetRestoreIntervalSeconds(mode);
            double elapsedSeconds = TimeSpan.FromTicks(nowTicks - lastTicks).TotalSeconds;
            int left = Mathf.CeilToInt((float)(intervalSeconds - elapsedSeconds));
            remainingSeconds = Mathf.Clamp(left, 1, intervalSeconds);
            return true;
        }

        public void ProcessAllModesRegeneration()
        {
            for (int i = 0; i < modeConfigs.Count; i++)
            {
                if (modeConfigs[i] != null)
                {
                    ProcessRegeneration(modeConfigs[i].mode);
                }
            }
        }

        public void ProcessRegeneration(GameMode mode)
        {
            if (!IsFuelEnabled(mode))
                return;

            InitializeModeStorageIfNeeded(mode);

            int max = GetMaxFuel(mode);
            string amountKey = GetAmountKey(mode);
            int current = Mathf.Clamp(PlayerPrefs.GetInt(amountKey, max), 0, max);

            if (current >= max)
            {
                if (GetStoredLastRestoreTicks(mode) != 0)
                {
                    SetStoredLastRestoreTicks(mode, 0);
                    PlayerPrefs.Save();
                }
                return;
            }

            long nowTicks = DateTime.UtcNow.Ticks;
            long lastTicks = GetStoredLastRestoreTicks(mode);

            if (lastTicks <= 0 || lastTicks > nowTicks)
            {
                SetStoredLastRestoreTicks(mode, nowTicks);
                PlayerPrefs.Save();
                return;
            }

            long intervalTicks = TimeSpan.FromSeconds(GetRestoreIntervalSeconds(mode)).Ticks;
            long elapsedTicks = nowTicks - lastTicks;

            if (elapsedTicks < intervalTicks)
                return;

            int restoredUnits = (int)(elapsedTicks / intervalTicks);
            if (restoredUnits <= 0)
                return;

            int newFuel = Mathf.Min(max, current + restoredUnits);
            PlayerPrefs.SetInt(amountKey, newFuel);

            if (newFuel >= max)
            {
                SetStoredLastRestoreTicks(mode, 0);
            }
            else
            {
                SetStoredLastRestoreTicks(mode, lastTicks + restoredUnits * intervalTicks);
            }

            PlayerPrefs.Save();
            OnFuelChanged?.Invoke(mode, newFuel, max);
            Debug.Log($"[ModeFuelManager] Авто-восстановление топлива ({mode}): +{newFuel - current} -> {newFuel}/{max}");
        }

        private void InitializeModeStorageIfNeeded(GameMode mode)
        {
            string amountKey = GetAmountKey(mode);
            if (!PlayerPrefs.HasKey(amountKey))
            {
                int max = GetMaxFuel(mode);
                PlayerPrefs.SetInt(amountKey, max);
                SetStoredLastRestoreTicks(mode, 0);
                PlayerPrefs.Save();
            }
        }

        private static string GetAmountKey(GameMode mode)
        {
            return $"ModeFuel_{mode}_Amount";
        }

        private static string GetLastTicksKey(GameMode mode)
        {
            return $"ModeFuel_{mode}_LastTicksUtc";
        }

        private static long GetStoredLastRestoreTicks(GameMode mode)
        {
            string raw = PlayerPrefs.GetString(GetLastTicksKey(mode), "0");
            if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long ticks))
                return ticks;
            return 0L;
        }

        private static void SetStoredLastRestoreTicks(GameMode mode, long ticks)
        {
            PlayerPrefs.SetString(GetLastTicksKey(mode), ticks.ToString(CultureInfo.InvariantCulture));
        }
    }
}
