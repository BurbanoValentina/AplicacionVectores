using ExitGames.Client.Photon.StructWrapping;
using UnityEngine;
using UnityEngine.InputSystem;

public class BoatMovement : MonoBehaviour
{
    [SerializeField] GameObject boat;
    [SerializeField] private GameObject player;
    [SerializeField] GameObject playerMovement;
    [SerializeField] GameObject playerTurn;

    [Header("Input Actions")]
    public InputActionReference leaveBoat;
    public InputActionReference moveAction;
    public InputActionReference rotateAction;

    [Header("Boat Settings")]
    [SerializeField] float moveSpeed = 5f;
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

    void Start()
    {
        VFM = GameObject.Find("VFM");
        manager = VFM.GetComponent<VectorField.VectorFieldManager>();
    }
    

    void Update()
    {
        if ((!isPlayerOnBoat) || (!boatMovingActive)) return;
        MoveBoat();
    }

    public void EnterBoat()
    {
        if (boat == null || player == null)
        {
            Debug.LogWarning("BoatMovement: Missing boat or player reference.");
            return;
        }

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
        Vector3 BoatMovement = Vector3.right * moveAmount * moveSpeed * Time.deltaTime;
        //Se calcula la fuerza del campo vectorial en la posición del barco.
        Vector3 fieldForce = manager.EvaluateFormula(boat.transform.position, GetOriginFromManager()) * fieldEffectStrength;
        //Validación de que el field force sea un vector válido
        if (!IsValidVector(fieldForce))
        {
            fieldForce = Vector3.zero;
            Debug.Log("Fuerza del campo inválida");
        }
        fieldForce = new Vector3(fieldForce.x, 0, fieldForce.y) * Time.deltaTime; //Se convierte a un vector 3D y se escala por deltaTime.
        fieldForce = Vector3.ClampMagnitude(fieldForce, 0.1f);
        //Se suman las fuerzas calculadas para obtener el movimiento final del barco.
        Vector3 finalMovement = BoatMovement + fieldForce;

        // Mover en la dirección frontal del barco
        boat.transform.Translate(
            finalMovement,
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

    private void Anchor()
    {
        boatMovingActive = false;
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
        player = GameManager.Instance.playerPrefab;
        playerMovement = player.transform.Find("Locomotion").Find("Move").gameObject;
        if(playerMovement == null)
        {
            Debug.LogWarning("BoatMovement: Player movement reference is null in GameManager.");
        }
        else
        {
            Debug.Log("BoatMovement: Player movement reference set from GameManager.");
        }
        playerTurn = player.transform.Find("Locomotion").Find("Turn").gameObject;
            if(playerTurn == null)
            {
                Debug.LogWarning("BoatMovement: Player turn reference is null in GameManager.");
            }
            else
            {
                Debug.Log("BoatMovement: Player turn reference set from GameManager.");
            }
        if(player == null)
        {
            Debug.LogWarning("BoatMovement: Player reference is null in GameManager.");
        }
        else
        {
            Debug.Log("BoatMovement: Player reference set from GameManager.");
        }
    }
}
