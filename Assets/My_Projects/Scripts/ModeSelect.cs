using UnityEngine;
using UnityEngine.UI;
using ALIyerEdon;
using System.Linq;

public enum GameMode
{
    Career,
    Offroad
}
public class ModeSelect : MonoBehaviour
{
    public GameMode SelectedMode;

    [SerializeField] private LevelSelect careerLevelSelect;
    [SerializeField] private LevelSelect offroadLevelSelect;
    [SerializeField] private ModeButton careerModeButton;
    [SerializeField] private ModeButton offroadModeButton;
    [SerializeField] private Color selectedColor = Color.green;
    [SerializeField] private Color unselectedColor = Color.white;
    [SerializeField] private Text TotalScores;
    [SerializeField] private AudioSource buttonClickSound;

    private void Start()
    {
        UpdateModeButtons();

        careerModeButton.AssignButtonClickListener(SelectCareerMode);
        offroadModeButton.AssignButtonClickListener(SelectOffroadMode);

        TotalScores.text = PlayerPrefs.GetInt("TotalScores").ToString();

    }

    public void SelectCareerMode()
    {
        SelectedMode = GameMode.Career;
        NitroStreets.Fuel.ModeFuelManager.EnsureInstance().ActiveMode = GameMode.Career;
        UpdateModeButtons();

        if (offroadLevelSelect == null || careerLevelSelect == null)
        {
            Debug.LogError("Level select panels are not assigned.");
            return;
        }

        careerLevelSelect.gameMode = GameMode.Career;
        careerLevelSelect.gameObject.SetActive(true);
        offroadLevelSelect.gameObject.SetActive(false);

        buttonClickSound.Play();
        gameObject.SetActive(false);
    }

    public void SelectOffroadMode()
    {
        SelectedMode = GameMode.Offroad;
        NitroStreets.Fuel.ModeFuelManager.EnsureInstance().ActiveMode = GameMode.Offroad;
        UpdateModeButtons();

        if (offroadLevelSelect == null || careerLevelSelect == null)
        {
            Debug.LogError("Level select panels are not assigned.");
            return;
        }

        offroadLevelSelect.gameMode = GameMode.Offroad;
        careerLevelSelect.gameObject.SetActive(false);
        offroadLevelSelect.gameObject.SetActive(true);
        buttonClickSound.Play();
        gameObject.SetActive(false);
    }

    private void UpdateModeButtons()
    {
        if (careerModeButton.isLocked)
        {
            careerModeButton.button.interactable = false;
            careerModeButton.lockedIcon.SetActive(true);
            careerModeButton.lockedPanel.SetActive(true);
            //careerModeButton.progressPanel.SetActive(false);
        }
        else
        {
            careerModeButton.button.interactable = true;
            careerModeButton.lockedIcon.SetActive(false);
            careerModeButton.lockedPanel.SetActive(false);
            // careerModeButton.progressPanel.SetActive(true);
        }

        if (offroadModeButton.isLocked)
        {
            offroadModeButton.button.interactable = false;
            offroadModeButton.lockedIcon.SetActive(true);
            offroadModeButton.lockedPanel.SetActive(true);
            //offroadModeButton.progressPanel.SetActive(false);
        }
        else
        {
            offroadModeButton.button.interactable = true;
            offroadModeButton.lockedIcon.SetActive(false);
            offroadModeButton.lockedPanel.SetActive(false);
            //offroadModeButton.progressPanel.SetActive(true);
        }


        careerModeButton.SetSelected(SelectedMode == GameMode.Career, selectedColor, unselectedColor);
        offroadModeButton.SetSelected(SelectedMode == GameMode.Offroad, selectedColor, unselectedColor);


        var cuurentCareerLevelIndex = FindLastLevelIndex();
        var careerProgress = (cuurentCareerLevelIndex + 1) * 100 / careerLevelSelect.levelPrices.Length;



        careerModeButton.UpdateProgressText("Level: " + (cuurentCareerLevelIndex + 1).ToString() + "/" + careerLevelSelect.levelPrices.Length.ToString());
        offroadModeButton.UpdateProgressText("Comming Soon");
    }
    private int FindLastLevelIndex()
    {
        var lastLevelIndex = -1;
        for (int i = 0; i < careerLevelSelect.levelPrices.Length; i++)
        {
            if (PlayerPrefs.GetInt("Level" + i, 0) == 3)
            {
                lastLevelIndex = i;
            }
        }

        return lastLevelIndex;
    }
    private void ShowLevelSelectPanel()
    {
        if (SelectedMode == GameMode.Career)
        {
            careerLevelSelect.gameObject.SetActive(true);
        }
        else if (SelectedMode == GameMode.Offroad)
        {
            offroadLevelSelect.gameObject.SetActive(true);
        }
    }


}

