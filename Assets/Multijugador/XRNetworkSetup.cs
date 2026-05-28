using System;
using UnityEngine;
using Photon.Pun;

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
    [SerializeField] private XRGazeAssistance gazeAssistant;

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
            if (mainCamera != null) mainCamera.gameObject.SetActive(true);
            if (leftHand != null) leftHand.SetActive(true);
            if (leftHandStabilized != null) leftHandStabilized.SetActive(true);
            if (rightHand != null) rightHand.SetActive(true);
            if (rightHandStabilized != null) rightHandStabilized.SetActive(true);
            if (LocomotionSystem != null) LocomotionSystem.SetActive(true);

            if (inputActionManager != null) inputActionManager.enabled = true;
            if (modalityManager != null) modalityManager.enabled = true;

            if (gazeAssistant != null)
            {
                bool hasGazeInteractor = HasGazeInteractor();
                if (hasGazeInteractor)
                {
                    gazeAssistant.enabled = true;
                }
                else
                {
                    gazeAssistant.enabled = false;
                    Debug.LogWarning("XRNetworkSetup: XRGazeInteractor missing. Gaze assistance disabled.");
                }
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetLocalPlayer(gameObject);
                Debug.Log("XRNetworkSetup: Local player set in GameManager.");
            }
            else
            {
                Debug.LogWarning("XRNetworkSetup: GameManager instance missing.");
            }
        }
        else
        {
            if (mainCamera != null) mainCamera.gameObject.SetActive(false);
            if (leftHand != null) leftHand.SetActive(false);
            if (leftHandStabilized != null) leftHandStabilized.SetActive(false);
            if (rightHand != null) rightHand.SetActive(false);
            if (rightHandStabilized != null) rightHandStabilized.SetActive(false);
            if (LocomotionSystem != null) LocomotionSystem.SetActive(false);
            
            if (inputActionManager != null) inputActionManager.enabled = false;
            if (modalityManager != null) modalityManager.enabled = false;
            if (gazeAssistant != null) gazeAssistant.enabled = false;
        }
    }

    private bool HasGazeInteractor()
    {
        // Resolve XRGazeInteractor via reflection to avoid compile-time dependency on optional package types.
        Type gazeType = Type.GetType("UnityEngine.XR.Interaction.Toolkit.Interactors.XRGazeInteractor, Unity.XR.Interaction.Toolkit");
        if (gazeType == null)
        {
            gazeType = Type.GetType("UnityEngine.XR.Interaction.Toolkit.Gaze.XRGazeInteractor, Unity.XR.Interaction.Toolkit");
        }

        return gazeType != null && GetComponentInChildren(gazeType, true) != null;
    }
}
