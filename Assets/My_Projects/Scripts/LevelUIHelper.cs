using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using ALIyerEdon;
using PrimeTween;


public class LevelUIHelper : MonoBehaviour
{
    private const int LevelsPerChapter = 5;

    [SerializeField] private LevelSelect levelSelect;
    [SerializeField] private Text[] levelTexts;
    //[SerializeField] private GameObject[] levelLocedTexts;
    [SerializeField] private Button nextChapterButton;
    [SerializeField] private Button previousChapterButton;
    [SerializeField] private float levelActivationDelay = 0.1f;
    [SerializeField] private float levelScaleDuration = 0.25f;

    private Button[] levelButtons;
    private UIButtonScaleHelper[] levelButtonScaleHelpers;
    private Sequence[] levelScaleTweens;
    private int currentChapter;
    private bool initialized;
    private Coroutine levelActivationCoroutine;

    private IEnumerator Start()
    {
        yield return new WaitForEndOfFrame();

        if (!TryInitialize())
        {
            enabled = false;
            yield break;
        }

        nextChapterButton.onClick.AddListener(ShowNextChapter);
        previousChapterButton.onClick.AddListener(ShowPreviousChapter);
        initialized = true;
        RefreshLevelStates();

    }

    private void OnEnable()
    {
        NitroStreets.Fuel.ModeFuelManager fuelMgr = NitroStreets.Fuel.ModeFuelManager.EnsureInstance();
        if (fuelMgr != null)
        {
            fuelMgr.OnFuelChanged += HandleFuelChanged;
        }

        if (initialized)
            RefreshLevelStates();
    }

    private void OnDisable()
    {
        if (NitroStreets.Fuel.ModeFuelManager.Instance != null)
        {
            NitroStreets.Fuel.ModeFuelManager.Instance.OnFuelChanged -= HandleFuelChanged;
        }

        if (levelActivationCoroutine != null)
        {
            StopCoroutine(levelActivationCoroutine);
            levelActivationCoroutine = null;
        }

        if (levelScaleTweens == null)
            return;

        for (int i = 0; i < levelScaleTweens.Length; i++)
        {
            if (levelScaleTweens[i].isAlive)
                levelScaleTweens[i].Stop();

            if (levelButtons != null && levelButtons[i] != null)
            {
                levelButtons[i].transform.localScale = Vector3.one;
                if (levelButtonScaleHelpers[i] != null)
                    levelButtonScaleHelpers[i].enabled = true;
            }
        }
    }

    private void OnDestroy()
    {
        if (nextChapterButton != null)
            nextChapterButton.onClick.RemoveListener(ShowNextChapter);

        if (previousChapterButton != null)
            previousChapterButton.onClick.RemoveListener(ShowPreviousChapter);
    }

    private void HandleFuelChanged(GameMode changedMode, int current, int max)
    {
        if (!initialized || levelSelect == null)
            return;

        if (changedMode == levelSelect.gameMode)
        {
            UpdateLevelCardVisuals();
        }
    }

    public void UpdateLevelCardVisuals()
    {
        if (!initialized || levelSelect == null)
            return;

        bool hasFuel = !levelSelect.useFuelSystem ||
                       NitroStreets.Fuel.ModeFuelManager.EnsureInstance().HasFuel(levelSelect.gameMode, 1);

        for (int i = 0; i < levelSelect.levelLocks.Length; i++)
        {
            bool isLocked = levelSelect.levelLocks[i].activeSelf;
            levelTexts[i].text = $"{i + 1:00}";
            levelTexts[i].gameObject.SetActive(!isLocked);

            if (isLocked)
            {
                levelSelect.levelPriceText[i].text = "LOCKED";
                levelSelect.levelPriceText[i].color = Color.gray;
            }
            else if (!hasFuel)
            {
                levelSelect.levelPriceText[i].text = "NO FUEL";
                levelSelect.levelPriceText[i].color = new Color(1f, 0.45f, 0.3f, 1f);
            }
            else
            {
                levelSelect.levelPriceText[i].text = "LEVEL";
                levelSelect.levelPriceText[i].color = Color.white;
            }

            if (levelButtons != null && levelButtons.Length > i && levelButtons[i] != null)
            {
                // Открытые уровни остаются кликабельными даже при 0 топлива,
                // чтобы клик в LevelSelect.SelectLevel блокировал старт и открывал окно рекламы.
                levelButtons[i].interactable = !isLocked;
            }
        }
    }

    public void RefreshLevelStates()
    {
        if (!initialized)
            return;

        UpdateLevelCardVisuals();

        if (levelActivationCoroutine != null)
            StopCoroutine(levelActivationCoroutine);

        levelActivationCoroutine = StartCoroutine(ActivateCurrentChapterLevels());

        previousChapterButton.interactable = currentChapter > 0;
        nextChapterButton.interactable = CanOpenNextChapter();
    }

    private IEnumerator ActivateCurrentChapterLevels()
    {
        for (int i = 0; i < levelButtons.Length; i++)
        {
            if (levelScaleTweens[i].isAlive)
                levelScaleTweens[i].Stop();

            levelButtons[i].gameObject.SetActive(false);
            levelButtons[i].transform.localScale = Vector3.one;
            if (levelButtonScaleHelpers[i] != null)
                levelButtonScaleHelpers[i].enabled = true;
        }

        float delay = Mathf.Max(0f, levelActivationDelay);
        float scaleDuration = Mathf.Max(0f, levelScaleDuration);
        for (int i = 0; i < levelButtons.Length; i++)
        {
            if (i / LevelsPerChapter != currentChapter)
                continue;

            int levelIndex = i;
            if (scaleDuration > 0f && levelButtonScaleHelpers[i] != null)
                levelButtonScaleHelpers[i].enabled = false;

            levelButtons[i].gameObject.SetActive(true);
            if (scaleDuration > 0f)
            {
                levelButtons[i].transform.localScale = Vector3.zero;
                levelScaleTweens[i] = Tween.Scale(
                    levelButtons[i].transform,
                    Vector3.one * 1.2f,
                    scaleDuration,
                    Ease.OutCubic)
                    .Chain(Tween.Scale(
                        levelButtons[i].transform,
                        Vector3.one,
                        scaleDuration * 0.3f,
                        Ease.OutQuad))
                    .OnComplete(this, helper => helper.CompleteLevelCardScale(levelIndex));
            }
            else
            {
                levelButtons[i].transform.localScale = Vector3.one;
                if (levelButtonScaleHelpers[i] != null)
                    levelButtonScaleHelpers[i].enabled = true;
            }

            if (delay > 0f)
                yield return new WaitForSeconds(delay);
        }

        levelActivationCoroutine = null;
    }

    private void CompleteLevelCardScale(int index)
    {
        levelButtons[index].transform.localScale = Vector3.one;
        if (levelButtonScaleHelpers[index] != null)
            levelButtonScaleHelpers[index].enabled = true;
    }

    private bool TryInitialize()
    {
        if (levelSelect == null)
        {
            Debug.LogError("LevelUIHelper requires a LevelSelect reference.", this);
            return false;
        }

        if (levelSelect.levelLocks == null)
        {
            Debug.LogError("LevelSelect has no level lock array.", this);
            return false;
        }

        int levelCount = levelSelect.levelLocks.Length;
        if (levelCount == 0 ||
            levelTexts == null ||
            levelTexts.Length != levelCount)
        //levelLocedTexts == null ||
        //levelLocedTexts.Length != levelCount)
        {
            Debug.LogError("LevelUIHelper requires matching lock, level-text, and locked-text arrays.", this);
            return false;
        }

        if (nextChapterButton == null || previousChapterButton == null)
        {
            Debug.LogError("Assign both chapter arrow buttons to LevelUIHelper.", this);
            return false;
        }

        levelButtons = new Button[levelCount];
        levelButtonScaleHelpers = new UIButtonScaleHelper[levelCount];
        levelScaleTweens = new Sequence[levelCount];
        for (int i = 0; i < levelCount; i++)
        {
            if (levelSelect.levelLocks[i] == null || levelTexts[i] == null)//|| levelLocedTexts[i] == null)
            {
                Debug.LogError($"LevelUIHelper has a missing level reference at index {i}.", this);
                return false;
            }

            levelButtons[i] = levelSelect.levelLocks[i].GetComponentInParent<Button>(true);
            if (levelButtons[i] == null)
            {
                Debug.LogError($"Could not find the level button for level index {i}.", this);
                return false;
            }

            levelButtonScaleHelpers[i] = levelButtons[i].GetComponent<UIButtonScaleHelper>();
        }

        return true;
    }

    private bool CanOpenNextChapter()
    {
        int lastLevelInChapter = (currentChapter + 1) * LevelsPerChapter - 1;
        return lastLevelInChapter + 1 < levelSelect.levelLocks.Length &&
               !levelSelect.levelLocks[lastLevelInChapter].activeSelf;
    }

    private void ShowNextChapter()
    {
        if (!CanOpenNextChapter())
            return;

        currentChapter++;
        RefreshLevelStates();
    }

    private void ShowPreviousChapter()
    {
        if (currentChapter == 0)
            return;

        currentChapter--;
        RefreshLevelStates();
    }
   
}
