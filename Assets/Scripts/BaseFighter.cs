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
    public enum AttackPhase {None, Startup,Active,Recovery}
    public enum HitType { None, Blocked, Normal, Counter, PunishCounter}
    public virtual AttackPhase CurrentAttackPhase => AttackPhase.None;
    public event System.Action<HitType, AttackData> HitReceived;
    //Got hit
    public HitType ReceiveHit(AttackData atk, float attackerX)
    {
        if (IsKO) return HitType.None;

        AttackPhase phase = CurrentAttackPhase;
        bool blocked = !IsHitstun && AllowBlock && WantsToBlock;
        float pushDir = transform.position.x >= attackerX ? 1f : -1f;//push direction
        HitType type;
        if (blocked)
        {
            type= HitType.Blocked;
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
            int damage = atk.damage;
            int stun = atk.hitStunTicks;

            if (phase ==AttackPhase.Startup ||phase==AttackPhase.Active)
            {
                type = HitType.Counter;
                damage = Mathf.RoundToInt(damage * SafeMult(atk.counterDamageMultiplier));
                stun += atk.counterHitBonusStun;
            }
            else if (phase==AttackPhase.Recovery)
            {
                type = HitType.PunishCounter;
                damage = Mathf.RoundToInt(damage * SafeMult(atk.punishDamageMultiplier));
                stun += atk.punishCounterBonusStun;
            }
            else
            {
                type = HitType.Normal;
            }
            if (FighterHealth != null) FighterHealth.TakeDamage(damage);
            IsHitstun = true;
            IsBlockstun = false;
            stunTicks = stun;
            StartPush(pushDir, atk.hitPushback, atk.hitStunTicks);
            
        }
        Debug.Log(name + " " + type + " by " + atk.name);
        OnInterrupted();
        HitReceived?.Invoke(type, atk);
        return type;
    }
    private static float SafeMult(float m) => m > 0f ? m : 1f;

    [Header("debug")]
    [SerializeField] private AttackData debugHit;
    //Right-click the component (or its ⋮ menu) in Play mode. Hold back while clicking to test blockstun.
    [ContextMenu("Debug: Receive Hit")]
    private void DebugReceiveHit()
    {
        if (debugHit == null || !debugHit.isValid) { Debug.LogWarning(name + ": fill in Debug Hit first"); return; }
        ReceiveHit(debugHit, transform.position.x + (facingRight ? 1f : -1f));
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
        float maxX = rightWall.bounds.min.x - halfWidth;
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