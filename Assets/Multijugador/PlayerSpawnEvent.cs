using UnityEngine;
using UnityEngine.Events;
using System.Collections;
public class PlayerSpawnEvent : MonoBehaviour
{
    public UnityEvent OnPlayerSpawn;
    public bool playerSpawend = false;
    void Start()
    {
        StartCoroutine(WaitForPlayerSpawn());
    }

    IEnumerator WaitForPlayerSpawn()
    {
        while (!playerSpawend)
        {
            yield return null;
        }
        OnPlayerSpawn.Invoke();
        Debug.Log("Player spawned, event invoked.");
    }
}
