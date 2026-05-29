using UnityEngine;
using Photon.Pun;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Gaze;

public class XRNetworkSetup : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;
    [SerializeField] private GameObject leftHand;
    [SerializeField] private GameObject leftHandStabilized;
    [SerializeField] private GameObject rightHand;
    [SerializeField] private GameObject rightHandStabilized;
    [SerializeField] private GameObject LocomotionSystem;
    [SerializeField] private InputActionManager inputActionManager;
    [SerializeField] private XRInputModalityManager modalityManager;
     void Awake()
    {
        // Desactivar componentes XR para todos los jugadores inicialmente
        mainCamera.gameObject.SetActive(false);
        leftHand.SetActive(false);
        leftHandStabilized.SetActive(false);
        rightHand.SetActive(false);
        rightHandStabilized.SetActive(false);
        LocomotionSystem.SetActive(false);
    }

     
    


    void Start()
    {
        PhotonView photonView = GetComponent<PhotonView>();
        if (photonView.IsMine)
        {
            Debug.Log("This is the local player. Enabling XR components.");
            mainCamera.gameObject.SetActive(true);
            leftHand.SetActive(true);
            leftHandStabilized.SetActive(true);
            rightHand.SetActive(true);
            rightHandStabilized.SetActive(true);
            LocomotionSystem.SetActive(true);

            inputActionManager.enabled = true;
            modalityManager.enabled = true;
        }
        else
        {
            mainCamera.gameObject.SetActive(false);
            leftHand.SetActive(false);
            leftHandStabilized.SetActive(false);
            rightHand.SetActive(false);
            rightHandStabilized.SetActive(false);
            LocomotionSystem.SetActive(false);
            
            inputActionManager.enabled = false;
            modalityManager.enabled = false;
        }

        GameManager.Instance.SetLocalPlayer(gameObject);
        Debug.Log("XRNetworkSetup: Local player set in GameManager.");
    }
}
