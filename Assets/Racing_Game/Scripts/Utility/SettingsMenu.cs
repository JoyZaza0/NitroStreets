//______________________________________________
// ALIyerEdon
// https://assetstore.unity.com/publishers/23606
//______________________________________________

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ALIyerEdon
{
    public class SettingsMenu : MonoBehaviour
    {
        // UI items in settings menu window
        public Slider musicVolume;
        public Text musicVolumeInfo;
        public Dropdown controlType;
        public Toggle localPosition;
        public Toggle SidePosition;
        public Toggle fpsCounter;

        public Dropdown QualityLevel;
        public Dropdown MotionBlur;
        public Dropdown DepthOfField;

        // Start is called before the first frame update
        void Start()
        {
            // Load initilial settings

            controlType.value = PlayerPrefs.GetInt("ControlType");

            QualityLevel.value = PlayerPrefs.GetInt("QualityLevel");
            MotionBlur.value = PlayerPrefs.GetInt("MotionBlur");
            DepthOfField.value = PlayerPrefs.GetInt("DepthOfField");

            musicVolume.value = PlayerPrefs.GetFloat("Music");

            // 3 => true  0 => false
            if (PlayerPrefs.GetInt("ShowLocalPosition") == 1)
                localPosition.isOn = true;
            else
                localPosition.isOn = false;

            // 3 => true  0 => false
            if (PlayerPrefs.GetInt("ShowPositionUI") == 1)
                SidePosition.isOn = true;
            else
                SidePosition.isOn = false;

            if (PlayerPrefs.GetInt("Show_FPS") == 1)
                fpsCounter.isOn = true;
            else
                fpsCounter.isOn = false;
        }

        public void Music_Volume()
        {
            PlayerPrefs.SetFloat("Music", musicVolume.value);
            musicVolumeInfo.text = musicVolume.value.ToString();
            FindFirstObjectByType<Load_Settings>().Update_MusicVolume(musicVolume.value);
        }

        // Control type : accelerometer , steering wheel , arrow keys
        // Control type : joystick or arrow keys
        public void Set_ControlType()
        {
            PlayerPrefs.SetInt("ControlType", controlType.value);

           /* if (FindFirstObjectByType<InputSystem>())
                FindFirstObjectByType<InputSystem>().Check_Control_Type();*/
        }

        // Screen resolution quality : best for performance
        public void Select_QualityLevel()
        {
            PlayerPrefs.SetInt("QualityLevel", QualityLevel.value);

            FindFirstObjectByType<Load_Settings>().
                Set_QualityLevel(PlayerPrefs.GetInt("QualityLevel"));
        }

        public void Set_MotionBlur()
        {
            PlayerPrefs.SetInt("MotionBlur", MotionBlur.value);

            if (PlayerPrefs.GetInt("MotionBlur") == 0)
                FindFirstObjectByType<Load_Settings>().Set_MotionBlur(false);
            if (PlayerPrefs.GetInt("MotionBlur") == 1)
                FindFirstObjectByType<Load_Settings>().Set_MotionBlur(true);
        }

        public void Set_DepthOfField()
        {
            PlayerPrefs.SetInt("DepthOfField", DepthOfField.value);

            if (PlayerPrefs.GetInt("DepthOfField") == 0)
                FindFirstObjectByType<Load_Settings>().Set_DepthOfField(false);
            if (PlayerPrefs.GetInt("DepthOfField") == 1)
                FindFirstObjectByType<Load_Settings>().Set_DepthOfField(true);
        }

        public void Toggle_LocalPosition()
        {
            StartCoroutine(Toggle_LocalPosition_Save());
        }

        IEnumerator Toggle_LocalPosition_Save()
        {
            yield return new WaitForEndOfFrame();

            if (localPosition.isOn)
                PlayerPrefs.SetInt("ShowLocalPosition", 1);
            else
                PlayerPrefs.SetInt("ShowLocalPosition", 0);


            foreach (Car_Position pos in FindObjectsOfType<Car_Position>())
            {
                if (PlayerPrefs.GetInt("ShowLocalPosition") == 1)
                    pos.displayPosition = true;
                else
                    pos.displayPosition = false;
            }
        }

        public void Toggle_SidePosition()
        {
            StartCoroutine(Toggle_SidePosition_Save());
        }

        IEnumerator Toggle_SidePosition_Save()
        {
            yield return new WaitForEndOfFrame();

            if (SidePosition.isOn)
                PlayerPrefs.SetInt("ShowPositionUI", 1);
            else
                PlayerPrefs.SetInt("ShowPositionUI", 0);

            // Enable or disable right side position ui
            if (PlayerPrefs.GetInt("ShowPositionUI") == 1)
                FindFirstObjectByType<Race_Manager>().positionUI.SetActive(true);
            else
                FindFirstObjectByType<Race_Manager>().positionUI.SetActive(false);
        }

        // Diplay fps
        public void Toggle_FPSCounter()
        {
            StartCoroutine(Toggle_FPSCounter_Save());
        }

        IEnumerator Toggle_FPSCounter_Save()
        {
            yield return new WaitForEndOfFrame();

            if (fpsCounter.isOn)
                PlayerPrefs.SetInt("Show_FPS", 1);
            else
                PlayerPrefs.SetInt("Show_FPS", 0);

            if (FindFirstObjectByType<FPSCounter>())
                FindFirstObjectByType<FPSCounter>().Update_Settings();

        }
    }
}