using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    public enum PlayerId { One, Two }

    [Header("Player")]
    public PlayerId playerId = PlayerId.One;

    [Header("Movement")]
    public float moveSpeed = 6f;
    public float jumpForce = 8f;

    [Header("Gravity")]
    public float gravityStrength = 25f;
    public float gravityScale = 1f;

    [Header("Ground Check")]
    public float groundCheckDistance = 1.1f;
    public LayerMask groundMask = ~0;

    Rigidbody rb;
    bool isGrounded;
    float inputX;
    bool jumpQueued;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
    }

    void Update()
    {
        var kb = Keyboard.current;
        float x = 0f;
        bool jump = false;

        if (playerId == PlayerId.One)
        {
            if (kb != null)
            {
                if (kb.aKey.isPressed) x -= 1f;
                if (kb.dKey.isPressed) x += 1f;
                if (kb.wKey.wasPressedThisFrame) jump = true;
            }
            if (TouchButton.Held(TouchButton.Action.P1Left)) x -= 1f;
            if (TouchButton.Held(TouchButton.Action.P1Right)) x += 1f;
            if (TouchButton.Consume(TouchButton.Action.P1Jump)) jump = true;
        }
        else
        {
            if (kb != null)
            {
                if (kb.leftArrowKey.isPressed) x -= 1f;
                if (kb.rightArrowKey.isPressed) x += 1f;
                if (kb.upArrowKey.wasPressedThisFrame) jump = true;
            }
            if (TouchButton.Held(TouchButton.Action.P2Left)) x -= 1f;
            if (TouchButton.Held(TouchButton.Action.P2Right)) x += 1f;
            if (TouchButton.Consume(TouchButton.Action.P2Jump)) jump = true;
        }

        inputX = Mathf.Clamp(x, -1f, 1f);
        if (jump) jumpQueued = true;
    }

    void FixedUpdate()
    {
        Vector3 gDir = gravityScale >= 0 ? Vector3.down : Vector3.up;
        isGrounded = Physics.Raycast(transform.position, gDir, groundCheckDistance,
                                     groundMask, QueryTriggerInteraction.Ignore);

        Vector3 v = rb.linearVelocity;
        v.x = inputX * moveSpeed;

        if (jumpQueued && isGrounded && gravityScale != 0f)
            v.y = Mathf.Sign(gravityScale) * jumpForce;
        jumpQueued = false;

        rb.linearVelocity = v;
        rb.AddForce(Vector3.down * gravityStrength * gravityScale, ForceMode.Acceleration);
    }

    public void SetGravityScale(float scale)
    {
        gravityScale = scale;
        transform.rotation = Quaternion.Euler(scale < 0 ? 180f : 0f, 0f, 0f);
    }
}