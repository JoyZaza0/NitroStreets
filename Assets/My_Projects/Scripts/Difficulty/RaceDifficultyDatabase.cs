using System;
using System.Collections.Generic;
using ALIyerEdon;
using UnityEngine;

namespace NitroStreets.Difficulty
{
    [Serializable]
    public class LevelRacerConfig
    {
        [HideInInspector] public string label = "Level 1";

        [Min(0f)]
        public float enginePower = 2000f;

        [Min(1f)]
        public float speedMin = 50f;

        [Min(1f)]
        public float speedMax = 95f;

        [Min(0.1f)]
        public float speedChangeInterval = 5f;

        public bool enableNitro = false;

        public NitroBoostPower nitroBoost = NitroBoostPower.X1;

        [Min(0.5f)]
        public float nitroRandomizerDelay = 5f;

        public void Validate(int zeroBasedIndex)
        {
            label = $"Level {zeroBasedIndex + 1}";
            enginePower = Mathf.Max(0f, enginePower);
            speedMin = Mathf.Max(1f, speedMin);
            speedMax = Mathf.Max(speedMin, speedMax);
            speedChangeInterval = Mathf.Max(0.1f, speedChangeInterval);
            nitroRandomizerDelay = Mathf.Max(0.5f, nitroRandomizerDelay);
        }

        public void ApplyTo(GameObject racerObject)
        {
            if (racerObject == null)
                return;

            float clampedEnginePower = Mathf.Max(0f, enginePower);
            float clampedSpeedMin = Mathf.Max(1f, speedMin);
            float clampedSpeedMax = Mathf.Max(clampedSpeedMin, speedMax);
            float clampedInterval = Mathf.Max(0.1f, speedChangeInterval);

            EasyCarController carController = racerObject.GetComponent<EasyCarController>();
            Random_Car_Speed randomCarSpeed = racerObject.GetComponent<Random_Car_Speed>();

            if (carController != null)
            {
                carController.enginePower = clampedEnginePower;
                carController.enginePowerTemp = clampedEnginePower;

                float initialSpeed = randomCarSpeed != null
                    ? UnityEngine.Random.Range(clampedSpeedMin, clampedSpeedMax)
                    : clampedSpeedMax;

                carController.maxSpeed = initialSpeed;
                carController.originalMaxSpeed = initialSpeed;
            }

            if (randomCarSpeed != null)
            {
                randomCarSpeed.speedMin = clampedSpeedMin;
                randomCarSpeed.speedMax = clampedSpeedMax;
                randomCarSpeed.delayInterval = clampedInterval;
            }

            Racer_Nitro racerNitro = racerObject.GetComponent<Racer_Nitro>();
            if (racerNitro != null)
            {
                if (enableNitro)
                {
                    racerNitro.nitroBoost = nitroBoost;
                    racerNitro.randomizerDelay = Mathf.Max(0.5f, nitroRandomizerDelay);
                    racerNitro.enabled = true;
                }
                else
                {
                    DisableNitroEffects(racerNitro);
                    racerNitro.raceIsStarted = false;
                    racerNitro.enabled = false;
                }
            }
        }

        private static void DisableNitroEffects(Racer_Nitro racerNitro)
        {
            if (racerNitro.nitroParticles != null)
            {
                for (int i = 0; i < racerNitro.nitroParticles.Length; i++)
                {
                    GameObject particleObj = racerNitro.nitroParticles[i];
                    if (particleObj == null)
                        continue;

                    ParticleSystem ps = particleObj.GetComponent<ParticleSystem>();
                    if (ps != null)
                    {
                        var emission = ps.emission;
                        emission.enabled = false;
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    }
                }
            }

            if (racerNitro.nitroSource != null && racerNitro.nitroSource.isPlaying)
            {
                racerNitro.nitroSource.Stop();
            }
        }
    }

    [Serializable]
    public class ModeProgressionGenerator
    {
        [Min(1)] public int levelCount = 30;

        [Header("Engine Power")]
        [Min(0f)] public float startEnginePower = 1900f;
        [Min(0f)] public float endEnginePower = 3400f;

        [Header("Random_Car_Speed Range")]
        [Min(1f)] public float startSpeedMin = 50f;
        [Min(1f)] public float endSpeedMin = 95f;
        [Min(1f)] public float startSpeedMax = 90f;
        [Min(1f)] public float endSpeedMax = 175f;
        [Min(0.1f)] public float speedChangeInterval = 5f;

        [Header("Progression Curve")]
        public AnimationCurve progressionCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Header("Nitro Progression")]
        public bool useNitroProgression = true;
        [Min(1)] public int nitroX1StartLevel = 6;
        [Min(1)] public int nitroX2StartLevel = 15;
        [Min(1)] public int nitroX3StartLevel = 24;
        [Min(0.5f)] public float startNitroDelay = 7f;
        [Min(0.5f)] public float endNitroDelay = 3.5f;
    }

    [Serializable]
    public class ModeDifficultySettings
    {
        public GameMode mode;

        [Tooltip("Если выключено, характеристики префабов гонщиков в этом режиме не изменяются.")]
        public bool applyOverrides = false;

        [Tooltip("Если levelID выходит за пределы списка уровней, использовать ближайший крайний уровень.")]
        public bool clampOutOfRangeLevel = true;

        public ModeProgressionGenerator generator = new ModeProgressionGenerator();

        public List<LevelRacerConfig> levels = new List<LevelRacerConfig>();

        public bool TryGetLevelConfig(int levelIndex, out LevelRacerConfig config)
        {
            config = null;

            if (!applyOverrides || levels == null || levels.Count == 0)
                return false;

            int resolvedIndex = levelIndex;
            if (resolvedIndex < 0 || resolvedIndex >= levels.Count)
            {
                if (!clampOutOfRangeLevel)
                    return false;

                resolvedIndex = Mathf.Clamp(resolvedIndex, 0, levels.Count - 1);
            }

            config = levels[resolvedIndex];
            return config != null;
        }

        public void GenerateLevelsFromProgression()
        {
            if (generator == null)
                generator = new ModeProgressionGenerator();

            int count = Mathf.Max(1, generator.levelCount);
            if (levels == null)
                levels = new List<LevelRacerConfig>(count);
            else
                levels.Clear();

            AnimationCurve curve = generator.progressionCurve != null && generator.progressionCurve.length >= 2
                ? generator.progressionCurve
                : AnimationCurve.Linear(0f, 0f, 1f, 1f);

            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? (float)i / (count - 1) : 0f;
                float eval = Mathf.Clamp01(curve.Evaluate(t));

                int oneBasedLevel = i + 1;
                float enginePower = Mathf.Round(Mathf.Lerp(generator.startEnginePower, generator.endEnginePower, eval) / 10f) * 10f;
                float speedMin = Mathf.Round(Mathf.Lerp(generator.startSpeedMin, generator.endSpeedMin, eval));
                float speedMax = Mathf.Round(Mathf.Lerp(generator.startSpeedMax, generator.endSpeedMax, eval));
                if (speedMax < speedMin)
                    speedMax = speedMin;

                bool enableNitro = generator.useNitroProgression && oneBasedLevel >= generator.nitroX1StartLevel;
                NitroBoostPower nitroBoost = NitroBoostPower.X1;
                if (oneBasedLevel >= generator.nitroX3StartLevel)
                    nitroBoost = NitroBoostPower.X3;
                else if (oneBasedLevel >= generator.nitroX2StartLevel)
                    nitroBoost = NitroBoostPower.X2;

                float nitroDelay = Mathf.Round(Mathf.Lerp(generator.startNitroDelay, generator.endNitroDelay, eval) * 10f) / 10f;

                var levelConfig = new LevelRacerConfig
                {
                    label = $"Level {oneBasedLevel}",
                    enginePower = enginePower,
                    speedMin = speedMin,
                    speedMax = speedMax,
                    speedChangeInterval = generator.speedChangeInterval,
                    enableNitro = enableNitro,
                    nitroBoost = nitroBoost,
                    nitroRandomizerDelay = nitroDelay
                };

                levelConfig.Validate(i);
                levels.Add(levelConfig);
            }
        }

        public void Validate()
        {
            if (generator == null)
                generator = new ModeProgressionGenerator();

            generator.levelCount = Mathf.Max(1, generator.levelCount);
            generator.startEnginePower = Mathf.Max(0f, generator.startEnginePower);
            generator.endEnginePower = Mathf.Max(0f, generator.endEnginePower);
            generator.startSpeedMin = Mathf.Max(1f, generator.startSpeedMin);
            generator.endSpeedMin = Mathf.Max(1f, generator.endSpeedMin);
            generator.startSpeedMax = Mathf.Max(generator.startSpeedMin, generator.startSpeedMax);
            generator.endSpeedMax = Mathf.Max(generator.endSpeedMin, generator.endSpeedMax);
            generator.speedChangeInterval = Mathf.Max(0.1f, generator.speedChangeInterval);
            generator.nitroX1StartLevel = Mathf.Max(1, generator.nitroX1StartLevel);
            generator.nitroX2StartLevel = Mathf.Max(generator.nitroX1StartLevel, generator.nitroX2StartLevel);
            generator.nitroX3StartLevel = Mathf.Max(generator.nitroX2StartLevel, generator.nitroX3StartLevel);
            generator.startNitroDelay = Mathf.Max(0.5f, generator.startNitroDelay);
            generator.endNitroDelay = Mathf.Max(0.5f, generator.endNitroDelay);

            if (levels == null)
                return;

            for (int i = 0; i < levels.Count; i++)
            {
                if (levels[i] == null)
                    levels[i] = new LevelRacerConfig();

                levels[i].Validate(i);
            }
        }
    }

    [CreateAssetMenu(
        fileName = "RaceDifficultyDatabase",
        menuName = "NitroStreets/Race Difficulty Database",
        order = 1)]
    public class RaceDifficultyDatabase : ScriptableObject
    {
        public const string DefaultResourcesPath = "RaceDifficultyDatabase";

        public List<ModeDifficultySettings> modes = new List<ModeDifficultySettings>();

        public static RaceDifficultyDatabase LoadDefault()
        {
            return Resources.Load<RaceDifficultyDatabase>(DefaultResourcesPath);
        }

        public ModeDifficultySettings GetModeSettings(GameMode mode)
        {
            if (modes == null)
                return null;

            for (int i = 0; i < modes.Count; i++)
            {
                if (modes[i] != null && modes[i].mode == mode)
                    return modes[i];
            }

            return null;
        }

        public bool TryGetConfig(GameMode mode, int levelIndex, out LevelRacerConfig config)
        {
            ModeDifficultySettings modeSettings = GetModeSettings(mode);
            if (modeSettings == null)
            {
                config = null;
                return false;
            }

            return modeSettings.TryGetLevelConfig(levelIndex, out config);
        }

        public bool EnsureDefaultModesInitialized()
        {
            if (modes == null)
                modes = new List<ModeDifficultySettings>();

            bool changed = false;

            foreach (GameMode gameMode in Enum.GetValues(typeof(GameMode)))
            {
                if (GetModeSettings(gameMode) != null)
                    continue;

                modes.Add(CreateDefaultModeSettings(gameMode));
                changed = true;
            }

            return changed;
        }

        private static ModeDifficultySettings CreateDefaultModeSettings(GameMode mode)
        {
            int defaultLevels = GameModeSaveKeys.GetDefaultMaxLevelsForMode(mode);

            if (mode == GameMode.Career)
            {
                return new ModeDifficultySettings
                {
                    mode = GameMode.Career,
                    applyOverrides = false,
                    clampOutOfRangeLevel = true,
                    generator = new ModeProgressionGenerator
                    {
                        levelCount = defaultLevels,
                        startEnginePower = 1900f,
                        endEnginePower = 3400f,
                        startSpeedMin = 50f,
                        endSpeedMin = 90f,
                        startSpeedMax = 95f,
                        endSpeedMax = 170f,
                        speedChangeInterval = 5f,
                        useNitroProgression = false
                    },
                    levels = new List<LevelRacerConfig>()
                };
            }

            if (mode == GameMode.Formula1)
            {
                var settings = new ModeDifficultySettings
                {
                    mode = GameMode.Formula1,
                    applyOverrides = true,
                    clampOutOfRangeLevel = true,
                    generator = new ModeProgressionGenerator
                    {
                        levelCount = defaultLevels,
                        startEnginePower = 1900f,
                        endEnginePower = 3400f,
                        startSpeedMin = 50f,
                        endSpeedMin = 95f,
                        startSpeedMax = 90f,
                        endSpeedMax = 175f,
                        speedChangeInterval = 5f,
                        progressionCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f),
                        useNitroProgression = true,
                        nitroX1StartLevel = 4,
                        nitroX2StartLevel = 8,
                        nitroX3StartLevel = 12,
                        startNitroDelay = 7f,
                        endNitroDelay = 3.5f
                    }
                };

                settings.GenerateLevelsFromProgression();
                return settings;
            }

            var offroadSettings = new ModeDifficultySettings
            {
                mode = mode,
                applyOverrides = true,
                clampOutOfRangeLevel = true,
                generator = new ModeProgressionGenerator
                {
                    levelCount = defaultLevels,
                    startEnginePower = 1800f,
                    endEnginePower = 3100f,
                    startSpeedMin = 45f,
                    endSpeedMin = 85f,
                    startSpeedMax = 80f,
                    endSpeedMax = 150f,
                    speedChangeInterval = 5f,
                    progressionCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f),
                    useNitroProgression = true,
                    nitroX1StartLevel = 6,
                    nitroX2StartLevel = 15,
                    nitroX3StartLevel = 24,
                    startNitroDelay = 7f,
                    endNitroDelay = 3.5f
                }
            };

            offroadSettings.GenerateLevelsFromProgression();
            return offroadSettings;
        }

        private void OnValidate()
        {
            if (modes == null)
                return;

            for (int i = 0; i < modes.Count; i++)
            {
                modes[i]?.Validate();
            }
        }
    }
}
