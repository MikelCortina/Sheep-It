using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class OnFootController : MonoBehaviour
{
    [Header("Movimiento")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float acceleration = 8f;
    [SerializeField] private float deceleration = 12f;
    [SerializeField] private float rotationSpeed = 10f;

    private Rigidbody rb;
    private Vector3 currentVelocity;
    private Vector3 inputDir;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotationX
                       | RigidbodyConstraints.FreezeRotationZ;
        rb.freezeRotation = false;
    }

    void Update()
    {
        // Leer WASD directamente del teclado, sin depender de action maps
        float h = 0f;
        float v = 0f;

        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) v += 1f;
        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) v -= 1f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) h += 1f;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) h -= 1f;

        inputDir = new Vector3(h, 0f, v).normalized;
    }

    void FixedUpdate()
    {
        Move();
        Rotate();
    }

    void Move()
    {
        Vector3 targetVelocity = inputDir * moveSpeed;
        float lerpSpeed = inputDir.magnitude > 0 ? acceleration : deceleration;

        currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity,
                                        Time.fixedDeltaTime * lerpSpeed);

        rb.linearVelocity = new Vector3(currentVelocity.x, rb.linearVelocity.y,
                                         currentVelocity.z);
    }

    void Rotate()
    {
        if (inputDir.sqrMagnitude < 0.01f) return;

        Quaternion targetRot = Quaternion.LookRotation(inputDir, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot,
                                               Time.fixedDeltaTime * rotationSpeed);
    }
}