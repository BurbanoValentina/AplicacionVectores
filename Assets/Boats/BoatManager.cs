using UnityEngine;

public class BoatManager : MonoBehaviour
{
    private bool checkAvailability(GameObject boat)
    {
        // if (boat.GetComponent<BoatMovement>().isPlayerOnBoat)
        // {
        //     Debug.Log("El barco ya tiene un jugador a bordo.");
        //     return false;
        // }
        return true;
    }

    private void SetAviability(GameObject boat)
    {
        
    }
}
