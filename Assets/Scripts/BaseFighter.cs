using UnityEngine;

public abstract class BaseFighter : MonoBehaviour
{
    [Header("facing")]
    [SerializeField] protected Transform opponent;
    [SerializeField] private float facingDeadzone = 0.05f;
    [SerializeField] protected bool facingRight = true;

    [Header("stage")]
    [SerializeField] protected Collider2D leftWall;
    [SerializeField] protected Collider2D rightWall;
    private Health health;
    private int stunTicks;

    private float pushPerTick;//distanced mover when pushed
    private int pushTicksLeft;

    // Found on first use, so it doesn't depend on Awake order
    public Health FighterHealth => health != null ? health : (health = GetComponent<Health>());
    public bool IsKO => FighterHealth != null && FighterHealth.IsKO;
    //Resets the fighter's own state, then its health. Called by the round manager.
    public bool FacingRight => facingRight;

    //stun
    public bool IsHitstun { get; private set; }
    public bool IsBlockstun { get; private set; }
    public bool IsStunned => IsHitstun || IsBlockstun;

    //FILL IT YOURSELF
    //Each fighter script resets its own position, facing, state, and so on here.
    protected abstract void OnResetFighter();
    protected abstract bool WantsToBlock { get; }
    protected virtual bool AllowBlock => true;
    protected virtual void OnInterrupted() { }

    //Got hit
    public void ReceiveHit(AttackData atk, float attackerX)
    {
        if (IsKO) return;
        bool blocked = !IsHitstun && AllowBlock && WantsToBlock;
        float pushDir = transform.position.x >= attackerX ? 1f : -1f;//push direction
        if (blocked)
        {
            if (atk.chipDamage > 0 && FighterHealth != null)
                FighterHealth.TakeDamage(atk.chipDamage);
            IsHitstun = false;
            IsBlockstun = true;
            stunTicks = atk.blockStunTicks;
            StartPush(pushDir, atk.blockPushback, atk.blockStunTicks);
            Debug.Log(name + " Blocked " + atk.name);
        }
        else
        {
            if (FighterHealth != null) FighterHealth.TakeDamage(atk.damage);
            IsHitstun = true;
            IsBlockstun = false;
            stunTicks = atk.hitStunTicks;
            StartPush(pushDir, atk.hitPushback, atk.hitStunTicks);
            Debug.Log(name + "Hit by " + atk.name);
            
        }
        OnInterrupted();
    }

    protected void TickStun()
    {
        if (!IsStunned) return;
        stunTicks--;
        if (stunTicks <= 0)
        {
            IsHitstun = false;
            IsBlockstun = false;
        }
    }

    private void StartPush(float dir, float distance, int ticks)
    {
        pushTicksLeft = Mathf.Max(1, ticks);
        pushPerTick = dir * distance / pushTicksLeft;
    }

    protected float ConsumePush()
    {
        if (pushTicksLeft <= 0) return 0f;
        pushTicksLeft--;
        return pushPerTick;
    }

    protected float ClampToStage(float x, float halfWidth)
    {
        if (leftWall == null || rightWall == null) return x;
        float minX = leftWall.bounds.max.x + halfWidth;
        float maxX = rightWall.bounds.min.x + halfWidth;
        return Mathf.Clamp(x, minX, maxX);
    }
    public void ResetFighter()
    {
        IsHitstun = false;
        IsBlockstun = false;
        stunTicks = 0;
        pushTicksLeft=0;
        pushPerTick = 0f;
        OnResetFighter();
        if (FighterHealth != null) FighterHealth.ResetHealth();
    }

    

    public void SetFacing(bool right)
    {
        facingRight = right;
        Vector3 s = transform.localScale;
        s.x = Mathf.Abs(s.x) * (right ? 1f : -1f);
        transform.localScale = s;
    }
    protected void UpdateFacing()
    {
        if (opponent == null) return;
        float dx = opponent.position.x - transform.position.x;
        if (Mathf.Abs(dx) < facingDeadzone) return;
        bool shouldFaceRight = dx > 0f;
        if (shouldFaceRight != facingRight)
            SetFacing(shouldFaceRight);
    }
    protected bool IsHoldingBack(float inputX)
    {
        if (Mathf.Abs(inputX) <= 0.1f) return false;
        return (inputX > 0f) != facingRight;
    }
}