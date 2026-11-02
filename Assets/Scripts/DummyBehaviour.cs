using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DummyBehaviour : BaseFighter
{
    [SerializeField] private bool alwaysBlock = false;
    //Fake the attack phase the dummy is in, so hits on it count as counter (Startup/Active) or punish counter (Recovery)
    [SerializeField] private AttackPhase forcedPhase = AttackPhase.None;
    private Rigidbody2D rb;
    private Collider2D cld;
    //private bool isGrounded = true;
    private Vector2 startPos;
    protected override bool WantsToBlock => alwaysBlock;
    public override AttackPhase CurrentAttackPhase => forcedPhase;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        cld = GetComponent<Collider2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        startPos = rb.position;
        SetFacing(true);

    }
    private void FixedUpdate()
    {
        TickStun();
        if (!IsStunned) UpdateFacing();

        Vector2 newPos = rb.position;
        newPos.x += ConsumePush();
        newPos.x = ClampToStage(newPos.x, cld.bounds.extents.x);
        rb.MovePosition(newPos);
    }

    protected override void OnResetFighter()
    {
        rb.position = startPos;
        transform.position = startPos;
        SetFacing(true);
    }
}
