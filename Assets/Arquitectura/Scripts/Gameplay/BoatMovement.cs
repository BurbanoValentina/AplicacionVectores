using ExitGames.Client.Photon.StructWrapping;
using UnityEngine;
using UnityEngine.InputSystem;

public class BoatMovement : MonoBehaviour
{
    [SerializeField] GameObject boat;
    [SerializeField] Rigidbody rb;
    [SerializeField] private GameObject player;
    [SerializeField] CharacterController characterController;
    [SerializeField] GameObject playerMovement;
    [SerializeField] GameObject playerTurn;

    [Header("Input Actions")]
    public InputActionReference leaveBoat;
    public InputActionReference moveAction;
    public InputActionReference rotateAction;

    [Header("Boat Settings")]
    [SerializeField] float moveSpeed = 5f;
    private float currentSpeedMultiplier = 1f;
     [SerializeField] float fieldRotationStrength = 2f;

    [SerializeField] float rotationSpeed = 100f;

    [Header("Field Settings")]

    [SerializeField] float fieldEffectStrength = 2f;
    private GameObject VFM; //Referencia al VectorFieldManager para obtener el origen de evaluación.
    private VectorField.VectorFieldManager manager;
    private Vector2 evalOrigin = Vector2.zero; // Origen para evaluar el campo vectorial

    #pragma warning disable 0414 // El valor nunca se usa, pero se deja para posible lógica futura
    private bool boatAffectedByField = false;
    #pragma warning restore 0414


    private bool isPlayerOnBoat = false;
    private bool boatMovingActive = false;

    private Vector2 moveInput;
    private Vector2 rotateInput;

    void OnEnable()
    {
       // The line is performing a null check on the `leaveBoat` variable and its `action` property.
        if (leaveBoat != null && leaveBoat.action != null)
        {
            leaveBoat.action.performed += LeaveBoat;
            leaveBoat.action.Enable();
        }

        if (moveAction != null && moveAction.action != null)
        {
            moveAction.action.performed += OnMove;
            moveAction.action.canceled += OnMove;
            moveAction.action.Enable();
        }

        if (rotateAction != null && rotateAction.action != null)
        {
            rotateAction.action.performed += OnRotate;
            rotateAction.action.canceled += OnRotate;
            rotateAction.action.Enable();
        }
    }

    void OnDisable()
    {
        if (leaveBoat != null && leaveBoat.action != null)
        {
            leaveBoat.action.performed -= LeaveBoat;
        }

        if (moveAction != null && moveAction.action != null)
        {
            moveAction.action.performed -= OnMove;
            moveAction.action.canceled -= OnMove;
        }

        if (rotateAction != null && rotateAction.action != null)
        {
            rotateAction.action.performed -= OnRotate;
            rotateAction.action.canceled -= OnRotate;
        }
    }

    void Awake()
    {
        rb = boat.GetComponent<Rigidbody>();
    }

    void Start()
    {
        VFM = GameObject.Find("VFM");
        manager = VFM.GetComponent<VectorField.VectorFieldManager>();
    }


    void FixedUpdate()
    {
        if ((!isPlayerOnBoat) && (!boatMovingActive)) return;
        MoveBoat();
    }

    public void EnterBoat()
    {
        if (boat == null || player == null)
        {
            Debug.LogWarning("BoatMovement: Missing boat or player reference.");
            return;
        }
        characterController = player.GetComponent<CharacterController>();
        characterController.enabled = false; // Desactivar el CharacterController para evitar conflictos con la física del barco
        isPlayerOnBoat = true;
        boatMovingActive = true;

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

        if (player == null)
        {
            Debug.LogWarning("BoatMovement: Missing player reference.");
            return;
        }

        isPlayerOnBoat = false;
        boatMovingActive = false;
        moveInput = Vector2.zero;
        rotateInput = Vector2.zero;

        player.transform.SetParent(null);
        characterController = player.GetComponent<CharacterController>();
        characterController.enabled = true; // Reactivar el CharacterController al salir del barco
        EnablePlayerMovementAndRotation();
    }

    private void ManualLeaveBoat()
    {
        if (!isPlayerOnBoat) return;

        if (player == null)
        {
            Debug.LogWarning("BoatMovement: Missing player reference.");
            return;
        }

        isPlayerOnBoat = false;
        boatMovingActive = false;
        moveInput = Vector2.zero;
        rotateInput = Vector2.zero;

        player.transform.SetParent(null);
        characterController = player.GetComponent<CharacterController>();
        characterController.enabled = true; // Reactivar el CharacterController al salir del barco
        EnablePlayerMovementAndRotation();
    }

    private void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    private void OnRotate(InputAction.CallbackContext context)
    {
        rotateInput = context.ReadValue<Vector2>();
    }

    private void MoveBoat()
    {
        if (boat == null)
        {
            Debug.LogWarning("BoatMovement: Missing boat reference.");
            boatMovingActive = false;
            return;
        }

        // Adelante / atrás
        float moveAmount = -moveInput.y;

        // Rotación
        float rotationAmount = rotateInput.sqrMagnitude > 0f ? rotateInput.x : moveInput.x;

        //Se calcula el movimiento del barco
        Vector3 BoatMovement = boat.transform.right * moveAmount * moveSpeed;
        //Se calcula la fuerza del campo vectorial en la posición del barco.
        Vector2 boatPosition2D = new Vector2(boat.transform.position.x, boat.transform.position.z);
        Vector3 fieldForce = manager.EvaluateFormula(boatPosition2D, GetOriginFromManager());
         Debug.Log("Fuerza del campo evaluada antes de la conversión: " + fieldForce);
        //Validación de que el field force sea un vector válido
        if (!IsValidVector(fieldForce))
        {
            fieldForce = Vector3.zero;
            Debug.Log("Fuerza del campo inválida");
        }
        fieldForce = new Vector3(fieldForce.x, 0, fieldForce.y);
        Debug.Log("Fuerza del campo evaluada después de la conversión: " + fieldForce);
        fieldForce *= fieldEffectStrength; // Se ajusta la fuerza del campo con un multiplicador para controlar su impacto en el movimiento del barco.
        Debug.Log("Fuerza del campo después de aplicar el multiplicador: " + fieldForce);
        //Se suman las fuerzas calculadas para obtener el movimiento final del barco.
        Vector3 finalMovement = BoatMovement + fieldForce * currentSpeedMultiplier;
         Debug.Log("Movimiento final del barco antes de aplicar el multiplicador de velocidad: " + finalMovement);

        // Mover en la dirección frontal del barco
        rb.linearVelocity = new Vector3(finalMovement.x, rb.linearVelocity.y, finalMovement.z);

        //RORACIÓN
    //     Vector3 boatForward = boat.transform.right;
    //     Vector3 fieldDirection = fieldForce.normalized;
    //     float angleDifference = Vector3.SignedAngle(boatForward, fieldDirection, Vector3.up);
    //     float fieldRotationForce = angleDifference / 180f;        rotationAmount -= fieldRotationForce * fieldRotationStrength;
    //     // Rotar barco
    //    boat.transform.Rotate(Vector3.up, rotationAmount * rotationSpeed * Time.deltaTime);
        //----------------------------------------------------
        // rb.MoveRotation(rb.rotation * Quaternion.Euler(0, rotationAmount * rotationSpeed *Time.fixedDeltaTime, 0));
        //  Debug.Log("Rotación aplicada al barco: " + rotationAmount * rotationSpeed * Time.deltaTime);
        //  Debug.Log("Rotación total del barco después de aplicar la rotación: " + boat.transform.rotation.eulerAngles);
        //----------------------------------------------------
        // Vector3 fieldDirection = fieldForce.normalized; 
        // Quaternion targetRotation = Quaternion.LookRotation(fieldDirection) * Quaternion.Euler(0, -90, 0); // Rotar barco boat.transform.rotation = Quaternion.Slerp( boat.transform.rotation, targetRotation, rotationSpeed * Time.deltaTime );
        // boat.transform.rotation = Quaternion.Slerp(boat.transform.rotation, targetRotation, fieldRotationStrength * Time.deltaTime);
        //-----------------------------------------------------
        Vector3 fieldDirection = fieldForce.normalized;
        Quaternion targetRotation = Quaternion.LookRotation(fieldDirection, Vector3.up) * Quaternion.Euler(0, 90, 0);
        Quaternion newRotation = Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        rb.MoveRotation(newRotation);
        Debug.Log("Rotación aplicada al barco: " + rotationAmount * rotationSpeed * Time.deltaTime);
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        currentSpeedMultiplier = multiplier;
    }

    private void DisablePlayerMovementAndRotation()
    {
        if (playerMovement != null)
        {
            playerMovement.SetActive(false);
        }

        if (playerTurn != null)
        {
            playerTurn.SetActive(false);
        }
    }

    private void EnablePlayerMovementAndRotation()
    {
        if (playerMovement != null)
        {
            playerMovement.SetActive(true);
        }

        if (playerTurn != null)
        {
            playerTurn.SetActive(true);
        }
    }

    public void resetBoatPosition(Transform resetPoint, Transform playerResetPoint)
    {
        ManualLeaveBoat(); // Asegura que el jugador salga del barco antes de resetear la posición
        boat.transform.position = resetPoint.position;
        boat.transform.rotation = resetPoint.rotation;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        player.transform.position = playerResetPoint.position;
        player.transform.rotation = playerResetPoint.rotation;
        Debug.Log("Boat and player positions have been reset.");
    }

    public void Anchor()
    {
        boatMovingActive = false;
        Debug.Log("Boat anchored. Movement disabled.");
    }

    public void ReleaseAnchor()
    {
        boatMovingActive = true;
        Debug.Log("Boat released. Movement enabled.");
    }

    private Vector2 GetOriginFromManager()
    {
        if (manager != null)
            return manager.GetEvalOrigin();
        return Vector2.zero;
    }

    private bool IsValidVector(Vector3 v)
    {
    return
        !float.IsNaN(v.x) &&
        !float.IsNaN(v.y) &&
        !float.IsNaN(v.z) &&
        !float.IsInfinity(v.x) &&
        !float.IsInfinity(v.y) &&
        !float.IsInfinity(v.z);
    }

    public void SetPlayer()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogWarning("BoatMovement: GameManager.Instance is null.");
            return;
        }

        player = GameManager.Instance.playerPrefab;
        if (player == null)
        {
            Debug.LogWarning("BoatMovement: Player reference is null in GameManager.");
            return;
        }

        var locomotion = player.transform.Find("Locomotion");
        if (locomotion != null)
        {
            var move = locomotion.Find("Move");
            if (move != null)
                playerMovement = move.gameObject;

            var turn = locomotion.Find("Turn");
            if (turn != null)
                playerTurn = turn.gameObject;
        }

        if (playerMovement == null)
        {
            Debug.LogWarning("BoatMovement: Player movement reference is null in GameManager.");
        }
        else
        {
            Debug.Log("BoatMovement: Player movement reference set from GameManager.");
        }
        if (playerTurn == null)
        {
            Debug.LogWarning("BoatMovement: Player turn reference is null in GameManager.");
        }
        else
        {
            Debug.Log("BoatMovement: Player turn reference set from GameManager.");
        }
    }
}
