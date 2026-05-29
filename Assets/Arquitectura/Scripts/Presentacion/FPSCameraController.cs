using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Controlador de camara FPS que permite:
/// - WASD / Flechas: Mover la camara
/// - Click DERECHO mantenido + mover raton: Rotar la vista
/// - Click IZQUIERDO: Interactuar con el panel World Space (dropdowns, botones)
/// - Scroll: Sobre el panel = scroll del contenido. Fuera = subir/bajar camara.
/// - Q/E o Espacio/Ctrl: Subir/bajar
/// - Shift: Sprint
///
/// El cursor esta SIEMPRE visible para poder interactuar con el panel.
/// Solo se rota la camara mientras se mantiene presionado el click derecho.
/// </summary>
public class FPSCameraController : MonoBehaviour
{
    [Header("Movimiento")]
    public float moveSpeed = 10f;
    public float sprintMultiplier = 2.5f;
    public float verticalSpeed = 6f;

    [Header("Rotacion (click derecho)")]
    public float mouseSensitivity = 2.5f;

    float _rotX = 0f;
    float _rotY = 0f;

    void Start()
    {
        // Cursor SIEMPRE visible para interactuar con el panel
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Inicializar angulos desde la rotacion actual
        Vector3 angles = transform.eulerAngles;
        _rotY = angles.y;
        _rotX = angles.x;
        if (_rotX > 180f) _rotX -= 360f;
    }

    void Update()
    {
        HandleMovement();
        HandleRotation();
    }

    void HandleMovement()
    {
        float speed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            speed *= sprintMultiplier;

        float h = 0f, v = 0f, vert = 0f;

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))    v =  1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))  v = -1f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  h = -1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h =  1f;

        if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Space)) vert =  1f;
        if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftControl)) vert = -1f;

        // Scroll para subir/bajar solo cuando NO estamos sobre UI
        if (!IsPointerOverUI())
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
                vert += scroll * 15f;
        }

        Vector3 forward = transform.forward;
        Vector3 right   = transform.right;
        Vector3 move = (forward * v + right * h).normalized * speed * Time.deltaTime;
        move.y += vert * verticalSpeed * Time.deltaTime;

        transform.position += move;
    }

    void HandleRotation()
    {
        // SOLO rotar cuando se mantiene click DERECHO
        if (!Input.GetMouseButton(1)) return;

        float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
        float my = Input.GetAxis("Mouse Y") * mouseSensitivity;

        _rotY += mx;
        _rotX -= my;
        _rotX = Mathf.Clamp(_rotX, -89f, 89f);

        transform.rotation = Quaternion.Euler(_rotX, _rotY, 0f);
    }

    static bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
