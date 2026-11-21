using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ModeButton : MonoBehaviour
{
    public GameMode mode;
    public GameObject levelSelectPanel;
    public GameObject lockedIcon;
    public GameObject lockedPanel;
    public GameObject progressPanel;
    public Button button;
    public Image[] images;
    public TextMeshProUGUI progressText;
    public bool isLocked;

    public void SetSelected(bool isSelected, Color selectedColor, Color unselectedColor)
    {
        if (button != null)
        {
            //button.interactable = !isSelected;

            foreach (var image in images)
            {
                if (image != null)
                {
                    image.color = isSelected ? selectedColor : unselectedColor;
                }
            }
        }
    }

    public void AssignButtonClickListener(UnityEngine.Events.UnityAction action)
    {
        if (button != null)
        {
            button.onClick.AddListener(action);
        }
    }

    public void UpdateProgressText(string text)
    {
        if (progressText != null)
        {
            progressText.text = text;
        }
    }
}
