//______________________________________________
// ALIyerEdon
// https://assetstore.unity.com/publishers/23606
//______________________________________________

using UnityEngine;
using UnityEngine.UI;

namespace ALIyerEdon
{
    public class Pause_Menu : MonoBehaviour
    {

        public GameObject pauseMenu;
        public GameObject raceUI;
        public GameObject mobileControls;

        public Text Loading;

        public string GarageScene = "Garage";

        [HideInInspector] public bool raceIsStarted = false;
        [HideInInspector] public bool canPause = true;

        public void Pause()
        {
            if (raceIsStarted)
            {
                if (canPause)
                {
                    FindFirstObjectByType<ControllerMouseSimulator>().Update_CursorIcon(true);
                    AudioListener.volume = 0;
                    Time.timeScale = 0;
                    pauseMenu.SetActive(true);
                    raceUI.SetActive(false);
                    mobileControls.SetActive(false);
                }
            }
        }

        public void Resume()
        {
            FindFirstObjectByType<ControllerMouseSimulator>().Update_CursorIcon(false);
            AudioListener.volume = 1f;
            Time.timeScale = FindObjectOfType<Race_Manager>().timeScale;
            pauseMenu.SetActive(false);
            raceUI.SetActive(true);

            if (FindFirstObjectByType<InputSystem>().controlType == InputType.Mobile)
                mobileControls.SetActive(true);
        }

        public void Restart()
        {
            FindFirstObjectByType<ControllerMouseSimulator>().Update_CursorIcon(true);
            AudioListener.volume = 0;
            Time.timeScale = FindObjectOfType<Race_Manager>().timeScale;
            if (Loading != null)
                Loading.text = "Loading...";

            string activeSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

            if (IsRaceFinishPanelOpen() && NitroStreets.Ads.AdMobManager.Instance != null)
            {
                NitroStreets.Ads.AdMobManager.Instance.OnRaceFinished(() =>
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene(activeSceneName);
                });
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(activeSceneName);
            }
        }

        public void Exit()
        {
            FindFirstObjectByType<ControllerMouseSimulator>().Update_CursorIcon(true);
            AudioListener.volume = 0;
            Time.timeScale = FindObjectOfType<Race_Manager>().timeScale;
            if (Loading != null)
                Loading.text = "Loading...";

            if (NitroStreets.Ads.AdMobManager.Instance != null)
            {
                NitroStreets.Ads.AdMobManager.Instance.OnRaceFinished(() =>
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene(GarageScene);
                });
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(GarageScene);
            }
        }

        private bool IsRaceFinishPanelOpen()
        {
            Start_Finish_UI finishUI = FindFirstObjectByType<Start_Finish_UI>();
            if (finishUI != null && finishUI.finishRaceMenu != null && finishUI.finishRaceMenu.activeInHierarchy)
                return true;

            Race_Manager rm = FindFirstObjectByType<Race_Manager>();
            if (rm != null && rm.raceFinishUI != null && rm.raceFinishUI.activeInHierarchy)
                return true;

            return false;
        }

        public void Enable_Object(GameObject target)
        {
            target.SetActive(true);
        }

        public void Disable_Object(GameObject target)
        {
            target.SetActive(false);
        }
    }
}