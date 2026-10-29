using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using static UnityEngine.Rendering.DebugUI.Table;

public class Myrmillo_Behavior : BaseFighter
{
    private enum FighterState { Idle, Attacking }
    private FighterState state = FighterState.Idle;

    [Header("combat")]
    [SerializeField] private Collider2D hitbox;
    [SerializeField] private Collider2D opponentHurtbox;
    
    private bool hitThisAttack;//A flag so that a strike can only hit once

    [Header("attacks")]
    [SerializeField] private AttackData standingLight;
    /*[SerializeField] private int startupTicks = 4;
    [SerializeField] private int activeTicks = 3;
    [SerializeField] private int recoveryTicks = 7;*/
    private AttackData currentAttack; //null when not attack
    private int attackTick;
    //Active when the character has passed the startup timing and before the recovery starts
    public bool HitboxActive =>
    state == FighterState.Attacking &&
    currentAttack != null && attackTick > currentAttack.startupTicks &&
    attackTick <= currentAttack.startupTicks + currentAttack.activeTicks;

    [Header("movement")]
    [SerializeField] private float forwardSpeed = 1.2f;
    [SerializeField] private float backwardSpeed = 1f;
   

   
    private Rigidbody2D rb;
    //private bool isGrounded = true;
    private Vector2 moveInput;
    private Vector2 velocity;
    private PlayerInput playerInput;
    private InputAction moveAction;
    private Collider2D myCollider;
    private InputAction attackAction;
    private bool attackPressed;
    public bool IsAttacking => state == FighterState.Attacking;
    private Vector2 startPos;
    private bool startFacingRight;

    public float MoveDirRelative// for animator to receive moving information
    {
        get
        {
            if (Mathf.Abs(velocity.x) < 0.01f) return 0f;
            return (velocity.x > 0f) == facingRight ? 1f : -1f;
        }
    }
    //block
    protected override bool WantsToBlock => IsHoldingBack(moveInput.x);
    protected override bool AllowBlock => state == FighterState.Idle;

    protected override void OnInterrupted()
    {
        state = FighterState.Idle;
        currentAttack = null;
        attackTick = 0;
        hitThisAttack = false;
        attackPressed = false;
    }
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        startPos = rb.position;
        startFacingRight = facingRight;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        playerInput = GetComponent<PlayerInput>();
        myCollider = GetComponent<Collider2D>();
        if (playerInput != null)
        {
            moveAction = playerInput.actions["Walk"];
            attackAction = playerInput.actions["Attack"];
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

        /*ATTACK STATE*/
        if (attackPressed)
        {
            if (state == FighterState.Idle && !IsStunned)
            {
                state = FighterState.Attacking;
                currentAttack = standingLight;
                attackTick = 0;
                hitThisAttack = false;
            }
            attackPressed = false;
        }

        if (state == FighterState.Attacking)
        {
            attackTick++;
            int recoveryStart = currentAttack.startupTicks + currentAttack.activeTicks;
            int totalTicks = recoveryStart + currentAttack.recoveryTicks;
            /*HIT CHECK*/
            if (HitboxActive && !hitThisAttack && hitbox != null && opponentHurtbox != null)
            {
                if (hitbox.bounds.Intersects(opponentHurtbox.bounds))
                {
                    hitThisAttack = true;   // stop it from hitting again during the same swing
                    BaseFighter oppFighter = opponentHurtbox.GetComponentInParent<BaseFighter>();
                    if (oppFighter != null) oppFighter.ReceiveHit(currentAttack,transform.position.x);

                }
            }
            if (attackTick > totalTicks)
            {
                state = FighterState.Idle;
                currentAttack = null;
            }
        }
        TickStun();

           

        /*FACING: only turn when not attacking*/
        if (state == FighterState.Idle && !IsStunned) UpdateFacing();
        
        /*WALKING*/
        float x = moveInput.x;
        if (state != FighterState.Idle || IsStunned)
        {
            velocity.x = 0f;                       // locked in place during the attack
        }
        else if (Mathf.Abs(x) > 0.1f)
        {
            bool moving = (x > 0f) == facingRight;
            float speed = moving ? forwardSpeed : backwardSpeed;
            velocity.x = Mathf.Sign(x) * speed;
        }
        else
        {
            velocity.x = 0f;
        }

        Vector2 newPos = rb.position + velocity * Time.fixedDeltaTime;
        //WALL restrict
        newPos.x += ConsumePush();
        newPos.x = ClampToStage(newPos.x, myCollider.bounds.extents.x);
        rb.MovePosition(newPos);
    }
    private void OnEnable()
    {
        if (attackAction != null) attackAction.performed += OnAttackPerformed;
    }

    private void OnDisable()
    {
        if (attackAction != null) attackAction.performed -= OnAttackPerformed;
    }

    private void OnAttackPerformed(InputAction.CallbackContext ctx)
    {
        attackPressed = true;   // just remember the press, FixedUpdate decides what to do with it
    }
    protected override void OnResetFighter()
    {
        state = FighterState.Idle;
        currentAttack = null;
        attackTick = 0;
        attackPressed = false;
        hitThisAttack = false;
        velocity = Vector2.zero;
        moveInput = Vector2.zero;
        rb.position = startPos;
        transform.position = startPos;
        SetFacing(startFacingRight);
    }
}