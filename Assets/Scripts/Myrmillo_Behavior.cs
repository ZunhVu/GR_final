using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using static UnityEngine.Rendering.DebugUI.Table;
[RequireComponent(typeof(Rigidbody2D))]
public class Myrmillo_Behavior : MonoBehaviour
{
    [Header("facing")]
    [SerializeField] private Transform opponent;
    [SerializeField] private float facingDeadzone = 0.05f;
    [Header("movement")]
    [SerializeField] private float forwardSpeed = 1.2f;
    [SerializeField] private float backwardSpeed = 1f;
    [SerializeField] private bool facingRight = true;
    private Rigidbody2D rb;
    private bool isGrounded = true;
    private Vector2 moveInput;
    private Vector2 velocity;
    private PlayerInput playerInput;
    private InputAction moveAction;

    public bool FacingRight => facingRight;

    public void SetFacing(bool right)
    {
        facingRight = right;
        Vector3 s = transform.localScale;
        s.x = Mathf.Abs(s.x) * (right ? 1f : -1f);
        transform.localScale = s;
    }

    private void UpdateFacing()
    {
        if (opponent == null) return;

        float dx = opponent.position.x - transform.position.x;
        if (Mathf.Abs(dx) < facingDeadzone) return;   // keep the current facing when overlapping

        bool shouldFaceRight = dx > 0f;
        if (shouldFaceRight != facingRight)
            SetFacing(shouldFaceRight);
    }
    public float MoveDirRelative
    {
        get
        {
            if (Mathf.Abs(velocity.x) < 0.01f) return 0f;
            return (velocity.x > 0f) == facingRight ? 1f : -1f;
        }
    }
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        playerInput = GetComponent<PlayerInput>();
        if (playerInput != null)
        {
            moveAction = playerInput.actions["Walk"];
        }

    }
    /*
    public void OnMove(InputAction.CallbackContext ctx)
    {
        moveInput = ctx.ReadValue<Vector2>();
        Debug.Log("phase: " + ctx.phase + "  input: " + moveInput);
    }
    */
    private void FixedUpdate()
    {
    /*WALKING*/
        if (moveAction != null) moveInput = moveAction.ReadValue<Vector2>();
        UpdateFacing();
        float x = moveInput.x;

        if (Mathf.Abs(x) > 0.1f)
        {
            // Is the player pressing toward the way the fighter is facing?
            bool movingForward = (x > 0f) == facingRight;
            float speed = movingForward ? forwardSpeed : backwardSpeed;

            velocity.x = Mathf.Sign(x) * speed;
        }
        else
        {
            velocity.x = 0f;
        }

        rb.MovePosition(rb.position + velocity * Time.fixedDeltaTime);
    }
}