using UnityEngine;
using UnityEngine.InputSystem;

public class BoatMovement : MonoBehaviour
{
    [SerializeField] GameObject boat;
    [SerializeField] GameObject player;

    [Header("Input Actions")]
    // public InputActionReference forward;
    // public InputActionReference backward;
    // public InputActionReference left;
    // public InputActionReference right;
    public InputActionReference leaveBoat;
    public InputActionReference moveAction;
    private bool isPlayerOnBoat = false;

    void OnEnable()
    {
        leaveBoat.action.performed += LeaveBoat;
        leaveBoat.action.Enable();
        moveAction.action.performed += MoveBoat;
        moveAction.action.Enable();
    }
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (isPlayerOnBoat == true)
        {
            MoveBoat(new InputAction.CallbackContext());
        }

    }

    public void EnterBoat()
    {
        isPlayerOnBoat = true;
        player.transform.SetParent(boat.transform);
    }

    private void LeaveBoat(InputAction.CallbackContext context)
    {
        if (!isPlayerOnBoat) return;
        isPlayerOnBoat = false;
        player.transform.SetParent(null);
    }

    private void MoveBoat(InputAction.CallbackContext context)
    {
        if (!isPlayerOnBoat) return;
        Vector2 input = context.ReadValue<Vector2>();
        Vector3 movement = new Vector3(input.x, 0, input.y);
        boat.transform.Translate(movement * Time.deltaTime * 5f);
    }
}
