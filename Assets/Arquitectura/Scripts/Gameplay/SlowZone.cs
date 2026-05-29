using UnityEngine;

public class SlowZone : MonoBehaviour
{
    [SerializeField] float slowMultiplayer = 0.05f;

    private void OnTriggerEnter(Collider other)
    {
        BoatMovement boatMovement = other.GetComponent<BoatMovement>();
        if (boatMovement != null)
        {
            boatMovement.SetSpeedMultiplier(slowMultiplayer);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        BoatMovement boat =
            other.GetComponent<BoatMovement>();

        if (boat != null)
        {
            boat.SetSpeedMultiplier(1f);
        }
    }
}
