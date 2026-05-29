using UnityEngine;

public class BoatMenu : MonoBehaviour
{
  [SerializeField] Transform ResetBoatPoint;
  [SerializeField] Transform ResetPlayerPoint;
  [SerializeField] BoatMovement boat;
  [Header("CanvasElements")]
    [SerializeField] GameObject StartButton;
    [SerializeField] GameObject StopButton;

    //-------------------------------------------------------------------
    public void ResetBoatPosition()
    {
        boat.resetBoatPosition(ResetBoatPoint, ResetPlayerPoint);
    }

    public void StopBoat()
    {
        boat.Anchor();
        StartButton.SetActive(false);
        StopButton.SetActive(true);
    }

    public void StartBoat()
    {
        boat.ReleaseAnchor();
        StartButton.SetActive(true);
        StopButton.SetActive(false);
        
    }

    public void Ducks()
    {
        //Logic to be implemented
    }
}
