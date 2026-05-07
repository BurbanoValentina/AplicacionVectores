using UnityEngine;
using UnityEngine.InputSystem;

public class BoatMovement : MonoBehaviour
{
    [SerializeField] GameObject boat;
    [SerializeField] GameObject player;
    [SerializeField] GameObject playerMovement;
    [SerializeField] GameObject playerTurn;

    [Header("Input Actions")]
    public InputActionReference leaveBoat;
    public InputActionReference moveAction;
    public InputActionReference rotateAction;

    [Header("Boat Settings")]
    [SerializeField] float moveSpeed = 5f;
    [SerializeField] float rotationSpeed = 100f;

    private bool isPlayerOnBoat = false;

    private Vector2 moveInput;
    private Vector2 rotateInput;

    void OnEnable()
    {
        leaveBoat.action.performed += LeaveBoat;
        leaveBoat.action.Enable();

        moveAction.action.performed += OnMove;
        moveAction.action.canceled += OnMove;
        moveAction.action.Enable();

        rotateAction.action.performed += OnRotate;
        rotateAction.action.canceled += OnRotate;
        rotateAction.action.Enable();
    }

    void OnDisable()
    {
        leaveBoat.action.performed -= LeaveBoat;

        moveAction.action.performed -= OnMove;
        moveAction.action.canceled -= OnMove;

        rotateAction.action.performed -= OnRotate;
        rotateAction.action.canceled -= OnRotate;
    }

    void Update()
    {
        if (!isPlayerOnBoat) return;

        Vector2 rotationTest = rotateAction.action.ReadValue<Vector2>();

        Debug.Log(rotationTest);

        MoveBoat();
    }

    public void EnterBoat()
    {
        isPlayerOnBoat = true;

        player.transform.SetParent(boat.transform);

        DisablePlayerMovementAndRotation();
        // Posición del jugador dentro del barco
        player.transform.localPosition = new Vector3(0, 0.7f, 0);

        // El jugador mira hacia el frente del barco
        player.transform.localRotation = Quaternion.identity * Quaternion.Euler(0, -90, 0);
    }

    private void LeaveBoat(InputAction.CallbackContext context)
    {
        if (!isPlayerOnBoat) return;

        isPlayerOnBoat = false;

        player.transform.SetParent(null);
        EnablePlayerMovementAndRotation();
    }

    private void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    private void OnRotate(InputAction.CallbackContext context)
    {
        rotateInput = context.ReadValue<Vector2>();
        if (rotateInput != null)
        {
            Debug.Log("el valor de rotación es: " + rotateInput);
        }
        else
        {
            Debug.Log("rotateInput es null");
        }
    }

    private void MoveBoat()
    {
        // Adelante / atrás
        float moveAmount = -moveInput.y;

        // Rotación
        float rotationAmount = moveInput.x;

        // Mover en la dirección frontal del barco
        boat.transform.Translate(
            Vector3.right * moveAmount * moveSpeed * Time.deltaTime,
            Space.Self
        );

        // Rotar barco
        boat.transform.Rotate(
            Vector3.up,
            rotationAmount * rotationSpeed * Time.deltaTime
        );
    }

    private void DisablePlayerMovementAndRotation()
    {
        playerMovement.SetActive(false);
        playerTurn.SetActive(false);
    }

    private void EnablePlayerMovementAndRotation()
    {
        playerMovement.SetActive(true);
        playerTurn.SetActive(true);
    }
}