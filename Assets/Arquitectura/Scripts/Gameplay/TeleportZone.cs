using UnityEngine;
using System.Collections.Generic;

public class TeleportZone : MonoBehaviour
{
    [SerializeField] private List<Transform> teleportDestinations;
    private void OnTriggerEnter(Collider other)
    {
        Rigidbody rb = other.GetComponent<Rigidbody>();
        if (teleportDestinations.Count == 0) return;

        int randomIndex = Random.Range(0, teleportDestinations.Count);
        Transform teleportDestination = teleportDestinations[randomIndex];
        rb.linearVelocity = Vector3.zero; // Detener el movimiento del objeto antes de teletransportarlo
        rb.angularVelocity = Vector3.zero; // Detener la rotación del objeto antes de teletransportarlo

        
        other.transform.position = teleportDestination.position;
    }
}
