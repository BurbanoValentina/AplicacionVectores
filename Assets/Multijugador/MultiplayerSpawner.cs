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
        if (playerIndex < 0 || playerIndex >= spawnPoints.Length)
        {
            Debug.LogError("Invalid player index for spawning: " + playerIndex);
            return;
        }
        Transform spawnPoint = spawnPoints[playerIndex];
        PhotonNetwork.Instantiate(playerPrefab.name, spawnPoint.position, spawnPoint.rotation);
        Debug.Log("PlayerSpawned");
        hasSpawned = true;
        PlayerEvent = GameObject.Find("PlayerSpawnEvent");
        PlayerEvent.GetComponent<PlayerSpawnEvent>().playerSpawend = true;
    }

    
    IEnumerator WaitForConnectionReady()
    {
        while (!PhotonNetwork.InRoom && !PhotonNetwork.IsConnectedAndReady)
        {
            Debug.Log("Waiting for connection to be ready...");
            yield return null;
        }
        yield return new WaitForSeconds(3f);
        SpawnPlayer(0); // Spawn player after connection is ready
    }
}
