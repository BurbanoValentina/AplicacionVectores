using UnityEngine;
using System.Collections;
using Photon.Pun;

public class MultiplayerSpawner : MonoBehaviour
{

    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private GameObject playerPrefab;
    private bool hasSpawned = false;
    private GameObject PlayerEvent;
     
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        StartCoroutine(WaitForConnectionReady());
    }

    public void SpawnPlayer(int playerIndex)
    {
        if (hasSpawned)
        {
            Debug.LogWarning("Player has already spawned. Skipping spawn.");
           return; 
        } 
        if (!PhotonNetwork.InRoom)
        {
            Debug.LogWarning("SpawnPlayer skipped: not in room yet.");
            return;
        }
        if (playerIndex < 0 || playerIndex >= spawnPoints.Length)
        {
            Debug.LogError("Invalid player index for spawning: " + playerIndex);
            return;
        }
        if (playerPrefab == null)
        {
            Debug.LogError("Player prefab is not assigned in MultiplayerSpawner.");
            return;
        }
        Transform spawnPoint = spawnPoints[playerIndex];
        PhotonNetwork.Instantiate(playerPrefab.name, spawnPoint.position, spawnPoint.rotation);
        Debug.Log("PlayerSpawned");
        hasSpawned = true;
        PlayerEvent = GameObject.Find("PlayerSpawnEvent");
        if (PlayerEvent != null)
        {
            var evt = PlayerEvent.GetComponent<PlayerSpawnEvent>();
            if (evt != null)
                evt.playerSpawend = true;
        }
    }

    
    IEnumerator WaitForConnectionReady()
    {
        while (!PhotonNetwork.InRoom)
        {
            Debug.Log("Waiting for connection to be ready...");
            yield return null;
        }
        SpawnPlayer(0); // Spawn player after connection is ready
    }

}
