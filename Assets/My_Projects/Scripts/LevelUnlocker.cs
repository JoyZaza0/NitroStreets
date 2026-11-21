using System.Collections;
using ALIyerEdon;
using UnityEngine;

public class LevelUnlocker : MonoBehaviour
{
    [SerializeField] private Race_Manager raceManager;
    [SerializeField] private string levelKey = "Level"; // Key prefix for PlayerPrefs
    [SerializeField] private int unlockLevelId = -1; // ID of the level to unlock


    public void UnlockLevel()
    {
        StartCoroutine(UnlockLevelCoroutine());
    }

    private IEnumerator UnlockLevelCoroutine()
    {
        yield return new WaitForSeconds(1f); // Wait for 1 second before unlocking the level
        
        Start_Finish_UI startFinishUI = FindAnyObjectByType<Start_Finish_UI>();

        if (raceManager != null)
        {
            if(unlockLevelId == -1)
            {
                yield break; // Exit the coroutine if unlockLevelId is not set
            }

            if(startFinishUI != null)
            {
                if(startFinishUI.goldIcon.activeSelf)
                {
                    PlayerPrefs.SetInt(levelKey + unlockLevelId.ToString() , 3);
                    Debug.Log("Level " + unlockLevelId + " unlocked with Gold!");
                }
                else if(startFinishUI.silverIcon.activeSelf)
                {
                    PlayerPrefs.SetInt(levelKey + unlockLevelId.ToString() , 3);
                    Debug.Log("Level " + unlockLevelId + " unlocked with Silver!");
                }
                else if(startFinishUI.bronzeIcon.activeSelf)
                {
                    PlayerPrefs.SetInt(levelKey + unlockLevelId.ToString() , 3);
                    Debug.Log("Level " + unlockLevelId + " unlocked with Bronze!");
                }
            }
            else
            {
                Debug.LogWarning("Start_Finish_UI reference is missing.");
            }
        }
        else
        {
            Debug.LogWarning("Race_Manager reference is missing.");
        }
    }
}
