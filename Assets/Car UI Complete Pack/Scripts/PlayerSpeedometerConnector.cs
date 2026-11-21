using ALIyerEdon;
using System.Collections;
using UnityEngine;

namespace CarUICompletePack
{
    public sealed class PlayerSpeedometerConnector : MonoBehaviour
    {
        [SerializeField] private SpeedometerController speedometer;
        [SerializeField, Min(0f)] private float speedMultiplier = 1f;
        [SerializeField] private bool useVehicleMaxSpeed = true;
        [SerializeField, Min(1f)] private float manualMaximumSpeed = 200f;
        [SerializeField] private bool showGasProgress = true;

        private EasyCarController carController;

        private IEnumerator Start()
        {
            yield return new WaitForEndOfFrame();

            if (speedometer == null)
                speedometer = GetComponent<SpeedometerController>();

            if (speedometer == null)
                speedometer = FindObjectOfType<SpeedometerController>();

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                carController = player.GetComponent<EasyCarController>();

            if (carController == null)
                Debug.LogError("PlayerSpeedometerConnector could not find the player car controller.", this);
        }

        private void Update()
        {
            if (speedometer == null || carController == null)
                return;

            float displayedSpeed = carController.currentSpeed * speedMultiplier;
            float maximumSpeed = useVehicleMaxSpeed
                ? carController.maxSpeed * speedMultiplier
                : manualMaximumSpeed;

            speedometer.SpeedometerSliderValue =
                Mathf.Clamp01(displayedSpeed / Mathf.Max(1f, maximumSpeed));

            if (speedometer.currentSpeedText != null)
                speedometer.currentSpeedText.text = Mathf.RoundToInt(displayedSpeed).ToString();


            // speedometer.SpeedometerSliderValue = Mathf.RoundToInt(
            //         (Mathf.Lerp(carController.currentSpeed, carController.currentSpeed, Time.deltaTime * 2f) * speedMultiplier));



            // if (speedometer.currentSpeedText != null)
            //     speedometer.currentSpeedText.text = speedometer.SpeedometerSliderValue.ToString() + " km/h";


            if (speedometer.currentGearText != null)
                speedometer.currentGearText.text = Mathf.Clamp(carController.currentGear + 1, 1, 99).ToString();

            if (showGasProgress)
                speedometer.GasSliderValue = Mathf.Clamp01(Mathf.Abs(carController.throttleInput));
        }
    }
}
