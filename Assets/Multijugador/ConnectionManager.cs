using UnityEngine;
using Photon.Pun;
public class ConnectionManager : MonoBehaviourPunCallbacks
{
    
    public static ConnectionManager Instance { get; private set; }
    private string roomName;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(this);
        }
    }
    void Start()
    {
        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("Conectado al servidor de Photon.");
        PhotonNetwork.JoinLobby();
    }

   public override void OnJoinedLobby()
    {
        base.OnJoinedLobby();
        Debug.Log("Unido al lobby.");
        JoinOrCreateRoom();
    }

    private void JoinOrCreateRoom()
    {
        if (string.IsNullOrEmpty(roomName))
        {
            roomName = "Room_" + Random.Range(1000, 9999);
        }

        PhotonNetwork.JoinOrCreateRoom(roomName, new Photon.Realtime.RoomOptions { MaxPlayers = 4 }, null);
    }
}
