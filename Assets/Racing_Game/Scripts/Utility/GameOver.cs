using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NitroStreets.Ads;

namespace ALIyerEdon
{
    public class GameOver : MonoBehaviour
    {
        public static GameOver instance;
        public GameObject gameOverMenu,watchAD,resume;
        public GameObject mobileControls;

        public Slider healthBar;
        public Image fillArea;
        public Text Loading; 
        
        public GameObject[] raceUI;


        private void Start()
        {
            if (instance == null)
            {
                instance = this;
            }

            UpdateAdAvailabilityUI();
        }

        private void UpdateAdAvailabilityUI()
        {
            bool adReady = AdMobManager.Instance != null && AdMobManager.Instance.IsRewardedAdReady();
            if (adReady)
            {
                if (Loading != null)
                    Loading.text = "Watch ad to continue race";
                if (watchAD != null)
                    watchAD.SetActive(true);
                if (resume != null)
                    resume.SetActive(false);
            }
            else
            {
                if (Loading != null)
                    Loading.text = "Ad is not available";
                if (watchAD != null)
                    watchAD.SetActive(false);
                if (resume != null)
                    resume.SetActive(true);
            }
        }

        public void Game_Over_Now()
        {
            FindFirstObjectByType<ControllerMouseSimulator>().Update_CursorIcon(true);
            
            FindFirstObjectByType<Pause_Menu>().canPause = false;

            UpdateAdAvailabilityUI();
            gameOverMenu.SetActive(true);

            foreach(GameObject aaa in raceUI)
                    aaa.SetActive(false);

            mobileControls.SetActive(false);
            Time.timeScale = 0;
            AudioListener.volume = 0;
        }

        public void Watch_Ad()
        {
            if (AdMobManager.Instance != null)
            {
                AdMobManager.Instance.ShowRewardedAd(() =>
                {
                    if (watchAD != null)
                        watchAD.SetActive(false);
                    if (resume != null)
                        resume.SetActive(true);

                    Car_Health health = FindFirstObjectByType<Car_Health>();
                    if (health != null)
                        health.Refill_Health();
                });
            }
            else
            {
                if (watchAD != null)
                    watchAD.SetActive(false);
                if (resume != null)
                    resume.SetActive(true);

                Car_Health health = FindFirstObjectByType<Car_Health>();
                if (health != null)
                    health.Refill_Health();
            }
        }

        public void Resume()
        {
            watchAD.SetActive(true);
            FindFirstObjectByType<Car_Health>().Refill_Health();

            FindFirstObjectByType<Pause_Menu>().canPause = true;
            AudioListener.volume = 1f;
            Time.timeScale = FindObjectOfType<Race_Manager>().timeScale;
            gameOverMenu.SetActive(false);

            foreach (GameObject aaa in raceUI)
                aaa.SetActive(true);

            mobileControls.SetActive(true);
        }

        public void Exit()
        {
            AudioListener.volume = 0;
            Time.timeScale = FindObjectOfType<Race_Manager>().timeScale;
            if (Loading != null)
                Loading.text = "Loading...";

            if (AdMobManager.Instance != null)
            {
                AdMobManager.Instance.OnRaceFinished(() =>
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene("Garage");
                });
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("Garage");
            }
        }

        public void Retry()
        {
            AudioListener.volume = 0;
            Time.timeScale = FindObjectOfType<Race_Manager>().timeScale;
            if (Loading != null)
                Loading.text = "Loading...";

            string activeSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

            if (AdMobManager.Instance != null)
            {
                AdMobManager.Instance.OnRaceFinished(() =>
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene(activeSceneName);
                });
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(activeSceneName);
            }
        }
    }
}