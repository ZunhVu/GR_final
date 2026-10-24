using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;


public class Myrmillo_Behavior : MonoBehaviour
{
    [Header("facing")]
    [SerializeField] private Transform opponent;
    [SerializeField] private float facingDeadzone = 0.05f;
    [Header("movement")]
    [SerializeField] private float forwardSpeed = 1.2f;
    [SerializeField] private float backwardSpeed = 1f;
    [SerializeField] private bool facingRight = true;
    [Header("stage")]
    [SerializeField] private Collider2D leftWall;
    [SerializeField] private Collider2D rightWall;
    
    private Rigidbody2D rb;
    private bool isGrounded = true;
    private Vector2 moveInput;
    private Vector2 velocity;
    private PlayerInput playerInput;
    private InputAction moveAction;
    private Collider2D myCollider;

    private Collider2D oppCollider;
    private Rigidbody2D oppRb;
    public bool FacingRight => facingRight;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        //rb.gravityScale = 0f;
        playerInput = GetComponent<PlayerInput>();
        myCollider = GetComponent<Collider2D>();
        if (playerInput != null)
        {
            moveAction = playerInput.actions["Walk"];
        }
        if (opponent != null)
        {
            oppCollider = opponent.GetComponent<Collider2D>();
            oppRb = opponent.GetComponent<Rigidbody2D>();
        }

    }

    public void SetFacing(bool right)
    {
        //Change  facing based on right
        facingRight = right;
        Vector3 s = transform.localScale;
        s.x = Mathf.Abs(s.x) * (right ? 1f : -1f);
        transform.localScale = s;
    }

    private void UpdateFacing()
    {
        if (opponent == null) return;

        float dx = opponent.position.x - transform.position.x;// characters distance
        if (Mathf.Abs(dx) < facingDeadzone) return;   // keep the current facing when overlapping

        bool shouldFaceRight = dx > 0f;
        if (shouldFaceRight != facingRight)//if the distance change sign = swap facing
            SetFacing(shouldFaceRight);
    }
    public float MoveDirRelative// for animator to receive facing information
    {
        get
        {
            if (Mathf.Abs(velocity.x) < 0.01f) return 0f;
            return (velocity.x > 0f) == facingRight ? 1f : -1f;
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
        //Moving direction
        if (Mathf.Abs(x) > 0.1f)
        {
            // Is the player pressing toward the way the fighter is facing?
            bool moving = (x > 0f) == facingRight;
            float speed = moving ? forwardSpeed : backwardSpeed;

            velocity.x = Mathf.Sign(x) * speed;
        }
        else
        {
            velocity.x = 0f;
        }

        Vector2 newPos = rb.position + velocity * Time.fixedDeltaTime;
        //WALK INTO OPPONENT
        if(oppCollider != null && oppRb != null)
        {
            float minGap = myCollider.bounds.extents.x + oppCollider.bounds.extents.x;
            float gap = newPos.x - oppRb.position.x;
            // gap is positive if the player is on the left while opponent is on the right, negative if the player is on the right while opponent is on the left
            float overlap = minGap - Mathf.Abs(gap);
            if (overlap > 0f)
            {
                float side = Mathf.Abs(gap) > 0.0001f ? Mathf.Sign(gap) : (facingRight ? -1f : 1f);//which side will the pushforce will aim 
                float oppX = oppRb.position.x - side * overlap;//opponent's horizontal coordinate being pushed BACK
                if(leftWall!=null && rightWall != null)
                {
                    float oppHalf = oppCollider.bounds.extents.x;
                    oppX = Mathf.Clamp(oppX, leftWall.bounds.max.x + oppHalf, rightWall.bounds.min.x - oppHalf);
                }
                oppRb.MovePosition(new Vector2(oppX, oppRb.position.y));
                newPos.x = oppX + side * minGap;
            }
            

        }

        if(leftWall != null && rightWall != null)
        {
            float halfWidth = myCollider.bounds.extents.x;
            float minX = leftWall.bounds.max.x + halfWidth;
            float maxX = rightWall.bounds.min.x - halfWidth;
            newPos.x = Mathf.Clamp(newPos.x, minX, maxX);
        }

        rb.MovePosition(newPos);
    }
}