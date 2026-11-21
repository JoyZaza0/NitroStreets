//______________________________________________
// ALIyerEdon
// https://assetstore.unity.com/publishers/23606
//______________________________________________


using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ALIyerEdon
{
	public class Load_Settings : MonoBehaviour
	{

		public AudioSource music;
        public Camera[] cutSceneCameras;
        
        void Start()
		{

			music.volume = PlayerPrefs.GetFloat("Music");

			if (FindFirstObjectByType<Race_Manager>())
			{
				if (PlayerPrefs.GetInt("ShowLocalPosition") == 1)
					FindFirstObjectByType<Race_Manager>().showLocalPosition = true;
				else
					FindFirstObjectByType<Race_Manager>().showLocalPosition = false;
			}

			Set_QualityLevel(PlayerPrefs.GetInt("QualityLevel"));

			if (PlayerPrefs.GetInt("MotionBlur") == 0)
				Set_MotionBlur(false);
			if (PlayerPrefs.GetInt("MotionBlur") == 1)
				Set_MotionBlur(true);

			if (PlayerPrefs.GetInt("DepthOfField") == 0)
				Set_DepthOfField(false);
			if (PlayerPrefs.GetInt("DepthOfField") == 1)
				Set_DepthOfField(true);

		}

		public void Update_MusicVolume(float volume)
		{
			music.volume = volume;
		}

        public void Set_QualityLevel(int level)
        {
            QualitySettings.SetQualityLevel(level);

            if (level == 0) // Very Low
            {
                foreach (Camera cam in FindObjectsByType<Camera>())
                {
                    cam.GetComponent<UniversalAdditionalCameraData>()
                        .antialiasing = AntialiasingMode.None;
                }
            }
            if (level == 1) // Low
            {
                foreach (Camera cam in FindObjectsByType<Camera>())
                {
                    cam.GetComponent<UniversalAdditionalCameraData>()
                        .antialiasing = AntialiasingMode.None;
                }
            }
            if (level == 2) // Medium
            {
                foreach (Camera cam in FindObjectsByType<Camera>())
                {
                    cam.GetComponent<UniversalAdditionalCameraData>()
                        .antialiasing = AntialiasingMode.TemporalAntiAliasing;
                    cam.GetComponent<UniversalAdditionalCameraData>()
                            .taaSettings.quality = TemporalAAQuality.High;
                }
            }
            if (level == 3) // High
            {
                foreach (Camera cam in FindObjectsByType<Camera>())
                {
                    cam.GetComponent<UniversalAdditionalCameraData>()
                        .antialiasing = AntialiasingMode.TemporalAntiAliasing;
                    cam.GetComponent<UniversalAdditionalCameraData>()
                            .taaSettings.quality = TemporalAAQuality.VeryHigh;
                }
            }
            if (level == 4) // Ultra
            {
                foreach (Camera cam in FindObjectsByType<Camera>())
                {
                    cam.GetComponent<UniversalAdditionalCameraData>()
                        .antialiasing = AntialiasingMode.TemporalAntiAliasing;
                    cam.GetComponent<UniversalAdditionalCameraData>()
                            .taaSettings.quality = TemporalAAQuality.VeryHigh;
                }
            }
            if (level == 5) // Max
            {
                foreach (Camera cam in FindObjectsByType<Camera>())
                {
                    cam.GetComponent<UniversalAdditionalCameraData>()
                        .antialiasing = AntialiasingMode.TemporalAntiAliasing;
                    cam.GetComponent<UniversalAdditionalCameraData>()
                            .taaSettings.quality = TemporalAAQuality.VeryHigh;
                }
            }
        }

        public void Set_MotionBlur(bool enabled)
		{
			MotionBlur mb;

			Volume volume = FindFirstObjectByType<Volume>();

			volume.profile.TryGet<MotionBlur>(out mb);

			mb.active = enabled;
		}

		public void Set_DepthOfField(bool enabled)
		{
			DepthOfField dof;

			Volume volume = FindFirstObjectByType<Volume>();

			volume.profile.TryGet<DepthOfField>(out dof);

			dof.active = enabled;
		}
	}
}