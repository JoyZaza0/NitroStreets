using System.Collections;
using ALIyerEdon;
using UnityEngine;
using UnityEngine.UI;

public class PlayerNameDisplay : MonoBehaviour
{
    [SerializeField] private Text[] playerNamesFromGarage;

    private void Awake()
    {
        Race_Manager race_Manager = FindAnyObjectByType<Race_Manager>();

        if (race_Manager != null)
        {
            for (int i = 0; i < race_Manager.playerPrefabs.Length; i++)
            {
                race_Manager.playerPrefabs[i].GetComponent<Car_Position>().RacerName = PlayerProfileStore.PlayerName;
            }
        }
    }

    private IEnumerator Start()
    {
        var playerCar = GameObject.FindWithTag("Player");
        yield return new WaitWhile(() => playerCar == null);

        if (playerCar != null)
        {
            if (playerCar.TryGetComponent(out Car_Position carPosition))
                carPosition.RacerName = PlayerProfileStore.PlayerName;
        }



        Start_Cutscene start_Cutscene = FindAnyObjectByType<Start_Cutscene>();

        if (start_Cutscene != null)
            start_Cutscene.detailInfo.text = $"Driver Name: {PlayerProfileStore.PlayerName}";

        if (playerNamesFromGarage != null && playerNamesFromGarage.Length > 0)

            for (int i = 0; i < playerNamesFromGarage.Length; i++)
            {
                playerNamesFromGarage[i].text = PlayerProfileStore.PlayerName;
            }
    }
}
