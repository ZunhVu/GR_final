using System.Collections.Generic;
using UnityEngine;

public abstract class BaseFighter : MonoBehaviour
{
    [Header("facing")]
    [SerializeField] protected BaseFighter opponent;
    [SerializeField] private float facingDeadzone = 0.05f;
    [SerializeField] protected bool facingRight = true;

    [Header("stage")]
    [SerializeField] protected Collider2D leftWall;
    [SerializeField] protected Collider2D rightWall;

    [Header("hurtbox (sprite units, before scale, facing right)")]
    [SerializeField] protected Vector2 hurtboxOffset = new Vector2(0f, 0.155f);
    [SerializeField] protected Vector2 hurtboxSize = new Vector2(0.15f, 0.31f);
    private Health health;
    private int stunTicks;
    private bool stunStartedThisTick;   // so a stun of N ticks lasts N full ticks after the hit

    private float pushPerTick;//distanced mover when pushed
    private int pushTicksLeft;

    // Found on first use, so it doesn't depend on Awake order
    public Health FighterHealth => health != null ? health : (health = GetComponent<Health>());
    public bool IsKO => FighterHealth != null && FighterHealth.IsKO;
    public bool FacingRight => facingRight;
    public BaseFighter Opponent => opponent;

    // Set by MatchSetup when the fighters are spawned
    public void SetOpponent(BaseFighter other) => opponent = other;
    public void SetStage(Collider2D left, Collider2D right)
    {
        leftWall = left;
        rightWall = right;
    }

    // The area that can be hit, in world space. Hits are rectangle overlaps, so no colliders are needed.
    public virtual Rect Hurtbox => LocalBoxToWorld(hurtboxOffset, hurtboxSize);

    // Would this hitbox touch any part of this fighter that can be hit? Every hit check goes through here,
    // so a fighter with extra hurtboxes (e.g. an extended sword arm) overrides this to include them.
    public virtual bool IsHitBy(Rect hitbox) => hitbox.Overlaps(Hurtbox);

    // Turns a box measured on the sprite (facing right, before scale) into world space.
    // The fighter's scale is negative on x when facing left, which mirrors the offset automatically.
    protected Rect LocalBoxToWorld(Vector2 offset, Vector2 size)
    {
        Vector3 s = transform.lossyScale;
        Vector2 center = (Vector2)transform.position + new Vector2(offset.x * s.x, offset.y * s.y);
        Vector2 worldSize = new Vector2(Mathf.Abs(size.x * s.x), Mathf.Abs(size.y * s.y));
        return new Rect(center - worldSize / 2f, worldSize);
    }

    //stun
    public bool IsHitstun { get; private set; }
    public bool IsBlockstun { get; private set; }
    public bool IsStunned => IsHitstun || IsBlockstun;
    public int StunTicksLeft => IsStunned ? stunTicks : 0;

    // Ticks until this fighter's current action is over and it can act again (0 when free).
    // Used for the frame advantage log; a Fighter reports what's left of its attack.
    public virtual int RecoveryTicksLeft => 0;

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

    // A hit found this tick but not applied yet, so every fighter's hits come from the same moment
    public struct PendingHit
    {
        public BaseFighter target;
        public BaseFighter attacker;
        public AttackData attack;
        public float attackerX;
    }

    // One tick is split into phases. RoundManager runs each phase for every fighter before the next phase,
    // so two attacks landing on the same tick both hit (a trade) instead of whoever updates first winning.
    public virtual void ReadCommand() { }                          // 1. ask the controller what to do
    public virtual void Advance() { }                              // 2. start / advance attacks
    public virtual void CollectHits(List<PendingHit> hits) { }     // 3. add the hits this fighter lands
    public virtual void Move() { }                                 // 5. stun countdown, facing, movement
                                                                   // (4. all hits are applied in between)

    private readonly List<PendingHit> soloHits = new List<PendingHit>();

    // Fallback for scenes without a RoundManager driving this fighter: run the phases for this fighter alone
    protected virtual void FixedUpdate()
    {
        if (RoundManager.Drives(this)) return;
        ReadCommand();
        Advance();
        soloHits.Clear();
        CollectHits(soloHits);
        foreach (PendingHit h in soloHits) h.target.ReceiveHit(h.attack, h.attackerX);   // solo: no advantage log
        Move();
    }

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
            stunStartedThisTick = true;
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
            stunStartedThisTick = true;
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
        if (stunStartedThisTick)
        {
            stunStartedThisTick = false;   // the hit landed this tick, counting starts next tick
            return;
        }
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
        stunStartedThisTick = false;
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
        float dx = opponent.transform.position.x - transform.position.x;
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