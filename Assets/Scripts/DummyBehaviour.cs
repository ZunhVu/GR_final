using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DummyBehaviour : MonoBehaviour
{
    private Rigidbody2D rb;
    private bool isGrounded = true;
    [SerializeField] private Transform player;
    [SerializeField] private float facingDeadzone = 0.05f;
    private bool facingRight=true;
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        SetFacing(true);

    }
    private void FixedUpdate()
    {
        if (player == null) return;

        float dx = player.position.x - transform.position.x;
        if (Mathf.Abs(dx) < facingDeadzone) return;

        bool shouldFaceRight = dx > 0f;
        if (shouldFaceRight != facingRight) SetFacing(shouldFaceRight);
    }
    private void SetFacing(bool right)
    {
        facingRight = right;
        Vector3 s = transform.localScale;
        s.x=Mathf.Abs(s.x)*(right?1f:-1f);
        transform.localScale = s;
    }

}
