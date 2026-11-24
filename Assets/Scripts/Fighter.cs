using System.Collections.Generic;
using UnityEngine;

// The standard fighter: walks, blocks and uses the attacks listed in its CharacterData.
// Whoever controls it (player, scripted CPU, RL agent) only sends FighterCommands; the rules are all here and in BaseFighter.
// A character with unique mechanics (projectiles, dashes...) subclasses this and uses the hooks near the bottom.
public class Fighter : BaseFighter
{
    protected enum FighterState { Idle, Attacking }
    protected FighterState state = FighterState.Idle;

    [Header("character")]
    [Tooltip("Set by MatchSetup when spawned. Fill it in only for a fighter placed in a scene by hand.")]
    [SerializeField] private CharacterData data;

    private float forwardSpeed = 1f;
    private float backwardSpeed = 1f;
    private Vector2 crouchHurtboxOffset = new Vector2(0f, 0.105f);
    private Vector2 crouchHurtboxSize = new Vector2(0.15f, 0.21f);
    private bool crouching;                  // posture: changes only while free (or in blockstun, to switch block height)

    protected AttackData currentAttack;      // null when not attacking
    protected int currentAttackIndex = -1;
    protected int attackTick;                // ticks since the attack started
    private bool hitThisAttack;              // a swing can only hit once
    private int followUpTicksLeft;           // > 0: the follow-up can be started (an attack with Can Follow Up just connected)
    private bool followUpAfterBlock;         // the window was opened by a blocked attack (the follow-up won't combo)

    // currentAttackIndex while the follow-up is running (it isn't in either attack list)
    public const int FollowUpIndex = -2;

    private Rigidbody2D rb;
    private Vector2 moveInput;
    private Vector2 velocity;
    private float plannedY;
    private Vector2 startPos;
    private bool startFacingRight;
    private IFighterController controller;   // player, scripted CPU or RL agent on this GameObject
    private bool warnedNoController;
    private FighterCommand cmd;              // this tick's command, read in ReadCommand

    public CharacterData Data => data;
    public override FighterCommand LastCommand => cmd;
    public IFighterController Controller => controller;
    public int AttackCount => data != null ? data.attacks.Count : 0;
    public int CrouchAttackCount => data != null ? data.crouchAttacks.Count : 0;
    public override bool IsCrouching => crouching;
    public bool IsAttacking => state == FighterState.Attacking;
    public AttackData CurrentAttack => currentAttack;
    public int AttackSerial { get; private set; }   // goes up by one every time an attack starts (the animator watches it)

    // Follow-up (target combo): for a few ticks after an attack with Can Follow Up hits, the follow-up can be started,
    // cancelling whatever is left of that attack. Players press attack again; the RL agent / CPU send cmd.followUp.
    public bool HasFollowUp => data != null && data.followUp != null && data.followUp.isValid;
    public bool FollowUpAvailable => followUpTicksLeft > 0 && HasFollowUp && CanAct;
    public int FollowUpTicksLeft => FollowUpAvailable ? followUpTicksLeft : 0;
    // True when the open window came from a blocked attack: the follow-up is then a risk, not a guaranteed combo
    public bool FollowUpAfterBlock => FollowUpAvailable && followUpAfterBlock;
    public bool IsFollowUp => state == FighterState.Attacking && currentAttackIndex == FollowUpIndex;

    //Active when the character has passed the startup timing and before the recovery starts
    public bool HitboxActive =>
        state == FighterState.Attacking &&
        currentAttack != null && attackTick > currentAttack.startupTicks &&
        attackTick <= currentAttack.startupTicks + currentAttack.activeTicks;

    public float MoveDirRelative// for animator to receive moving information
    {
        get
        {
            if (Mathf.Abs(velocity.x) < 0.01f) return 0f;
            return (velocity.x > 0f) == facingRight ? 1f : -1f;
        }
    }

    //block
    // Holding back, or guarding in place (the dummy's Block state)
    protected override bool WantsToBlock => cmd.guard || IsHoldingBack(moveInput.x);
    protected override bool AllowBlock => state == FighterState.Idle;
    // A standing block stops mids only; a crouching block stops mids and lows
    protected override bool CanBlock(AttackData atk) => atk.height != AttackHeight.Low || crouching;

    // Crouching uses the shorter crouch hurtbox
    public override Rect Hurtbox => crouching ? LocalBoxToWorld(crouchHurtboxOffset, crouchHurtboxSize) : base.Hurtbox;

    protected override void OnInterrupted()
    {
        state = FighterState.Idle;
        currentAttack = null;
        currentAttackIndex = -1;
        attackTick = 0;
        hitThisAttack = false;
        followUpTicksLeft = 0;   // got hit: the combo is broken
    }

    // Our attack reached the opponent (hit or blocked, not a whiff). If the attack allows it, this opens the follow-up window.
    protected override void OnHitLanded(HitType type, AttackData atk)
    {
        if (type == HitType.None) return;
        if (!CanAct || atk == null || !atk.canFollowUp || !HasFollowUp) return;   // e.g. a trade: we were hit too
        followUpTicksLeft = data.followUpWindowTicks;
        followUpAfterBlock = type == HitType.Blocked;
    }

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        startPos = rb.position;
        startFacingRight = facingRight;
        SetFacing(facingRight);   // match the sprite to the Facing Right checkbox
        if (data != null) Setup(data);
    }

    // Applies a character's stats. MatchSetup calls this right after spawning.
    public virtual void Setup(CharacterData character)
    {
        data = character;
        forwardSpeed = character.forwardSpeed;
        backwardSpeed = character.backwardSpeed;
        hurtboxOffset = character.hurtboxOffset;
        hurtboxSize = character.hurtboxSize;
        crouchHurtboxOffset = character.crouchHurtboxOffset;
        crouchHurtboxSize = character.crouchHurtboxSize;
        knockdownFallTicks = character.knockdownFallTicks;
        knockdownDownTicks = character.knockdownDownTicks;
        knockdownGetUpTicks = character.knockdownGetUpTicks;
        if (FighterHealth != null) FighterHealth.SetMax(character.maxHealth);
    }

    // Spawn position and facing, also used for every round reset
    public void SetStart(Vector2 position, bool faceRight)
    {
        startPos = position;
        startFacingRight = faceRight;
        rb.position = position;
        transform.position = position;
        SetFacing(faceRight);
    }

    // Uses the first enabled controller on this GameObject, so you swap who's in control by enabling one and disabling the others
    private IFighterController FindController()
    {
        foreach (IFighterController c in GetComponents<IFighterController>())
            if (c is Behaviour b && b.enabled) return c;
        return null;
    }

    // Lets other code (match setup, training) change who controls this fighter at runtime
    public void SetController(IFighterController newController) => controller = newController;

    // Attack number index from the standing list, or the crouching list; null if there's no such attack
    public AttackData GetAttack(int index, bool crouchingAttack = false)
    {
        if (data == null) return null;
        List<AttackData> list = crouchingAttack ? data.crouchAttacks : data.attacks;
        if (index < 0 || index >= list.Count) return null;
        return list[index];
    }

    // Where an attack's hitbox is right now, in world space
    public Rect AttackHitbox(AttackData atk) => LocalBoxToWorld(atk.hitboxOffset, atk.hitboxSize);

    // Would this attack's hitbox touch the opponent if it were active now? (The CPU uses it to judge range.)
    public bool AttackWouldReach(int index, bool crouchingAttack = false)
    {
        AttackData atk = GetAttack(index, crouchingAttack);
        return atk != null && opponent != null && opponent.IsHitBy(AttackHitbox(atk));
    }

    // The current attack's extended hurtbox exists on this tick
    public bool ExtraHurtboxActive
    {
        get
        {
            if (state != FighterState.Attacking || currentAttack == null || !currentAttack.hasExtraHurtbox) return false;
            int end = currentAttack.extraHurtboxEndTick > 0 ? currentAttack.extraHurtboxEndTick : TotalTicks(currentAttack);// the last tick the extra hurtbox is active, 0 iis active until the attack ends
            return attackTick >= currentAttack.extraHurtboxStartTick && attackTick <= end;
        }
    }

    // Where the extended hurtbox is right now, in world space (only meaningful while ExtraHurtboxActive)
    public Rect ExtraHurtbox => LocalBoxToWorld(currentAttack.extraHurtboxOffset, currentAttack.extraHurtboxSize);

    // Body hurtbox, plus the attack's extended hurtbox on the ticks it exists (nothing while knocked down)
    public override bool IsHitBy(Rect hitbox) =>
        !IsKnockedDown && (base.IsHitBy(hitbox) || (ExtraHurtboxActive && hitbox.Overlaps(ExtraHurtbox)));

    /*COMMAND*/
    public override void ReadCommand()
    {
        if (controller == null)
        {
            controller = FindController();   // controllers can be added after spawning
            if (controller == null && !warnedNoController)
            {
                Debug.LogWarning(name + ": no enabled IFighterController found, fighter will stand still");
                warnedNoController = true;
            }
        }
        cmd = controller != null ? controller.GetCommand(this) : FighterCommand.None;
        if (InputLocked) cmd = FighterCommand.None;   // still ask the controller, so its own timers keep running
        moveInput = new Vector2(cmd.moveX, 0f);
    }

    /*ATTACK STATE*/
    public override void Advance()
    {
        // The attack used its last tick on the previous tick, so this tick is free (a new attack can start right away)
        if (state == FighterState.Attacking && attackTick >= TotalTicks(currentAttack))
        {
            state = FighterState.Idle;
            currentAttack = null;
            currentAttackIndex = -1;
        }

        // Posture: free to stand or crouch while idle. In blockstun too, so the block height can be switched;
        // locked during an attack and in hitstun.
        if (IsKnockedDown) crouching = false;
        else if (state == FighterState.Idle && !IsHitstun) crouching = cmd.crouch;

        if (cmd.followUp && FollowUpAvailable)
        {
            // Target combo: cancels the rest of the attack that hit (or starts right away if it already ended)
            crouching = false;   // the follow-up is done standing
            StartAttack(data.followUp, FollowUpIndex);
        }
        else if (cmd.attack >= 0 && state == FighterState.Idle && CanAct)
        {
            AttackData atk = GetAttack(cmd.attack, crouching);   // crouching → the crouching attack list
            if (atk != null && atk.isValid) StartAttack(atk, cmd.attack);
        }

        if (followUpTicksLeft > 0) followUpTicksLeft--;

        if (state == FighterState.Attacking)
        {
            attackTick++;   // 1 = first startup tick ... TotalTicks = last recovery tick
            OnAttackTick(currentAttackIndex, attackTick);
        }
    }

    private void StartAttack(AttackData atk, int index)
    {
        state = FighterState.Attacking;
        currentAttack = atk;
        currentAttackIndex = index;
        attackTick = 0;
        hitThisAttack = false;
        followUpTicksLeft = 0;   // starting anything closes the window
        AttackSerial++;
        OnAttackStarted(currentAttackIndex);
    }

    private static int TotalTicks(AttackData atk) => atk.startupTicks + atk.activeTicks + atk.recoveryTicks;

    // Ticks of the current attack still to come after this one
    public override int RecoveryTicksLeft =>
        state == FighterState.Attacking && currentAttack != null ? Mathf.Max(0, TotalTicks(currentAttack) - attackTick) : 0;

    /*HIT CHECK: only records the hit, RoundManager applies every fighter's hits together*/
    public override void CollectHits(List<PendingHit> hits)
    {
        if (!HitboxActive || hitThisAttack || opponent == null || opponent == this) return;
        if (!opponent.IsHitBy(AttackHitbox(currentAttack))) return;

        hitThisAttack = true;   // stop it from hitting again during the same swing
        hits.Add(new PendingHit { target = opponent, attacker = this, attack = currentAttack, attackerX = transform.position.x });
    }

    public override void Move()
    {
        TickStun();

        /*FACING: only turn when not attacking*/
        if (state == FighterState.Idle && CanAct) UpdateFacing();

        /*WALKING*/
        float x = moveInput.x;
        if (state != FighterState.Idle || !CanAct || crouching)
        {
            velocity.x = 0f;                       // locked in place while attacking, stunned, knocked down or crouching
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

        LastX = rb.position.x;
        Vector2 newPos = rb.position + velocity * Time.fixedDeltaTime;
        newPos.x += AttackStep();   // attacks that slide forward (e.g. the follow-up)

        newPos.x += ConsumePush();
        //WALL restrict
        newPos.x = ClampToStage(newPos.x, BodyHalfWidth);
        PlannedX = newPos.x;   // applied in CommitMove, after the bodies are separated
        plannedY = newPos.y;
    }

    // This tick's share of the current attack's forward slide, in world x (spread evenly over its ticks)
    private float AttackStep()
    {
        if (state != FighterState.Attacking || currentAttack == null || currentAttack.forwardMove == 0f) return 0f;
        int start = Mathf.Max(1, currentAttack.moveStartTick);
        int end = currentAttack.moveEndTick > 0 ? currentAttack.moveEndTick
                                                : currentAttack.startupTicks + currentAttack.activeTicks;
        if (end < start || attackTick < start || attackTick > end) return 0f;
        float perTick = currentAttack.forwardMove / (end - start + 1);
        return facingRight ? perTick : -perTick;
    }

    public override void CommitMove() => rb.MovePosition(new Vector2(PlannedX, plannedY));

    public override AttackPhase CurrentAttackPhase
    {
        get
        {
            if (state != FighterState.Attacking || currentAttack==null) return AttackPhase.None;
            if (attackTick <= currentAttack.startupTicks) return AttackPhase.Startup;
            if (attackTick <= currentAttack.startupTicks + currentAttack.activeTicks) return AttackPhase.Active;
            return AttackPhase.Recovery;
        }
    }

    protected override void OnResetFighter()
    {
        OnInterrupted();
        crouching = false;
        velocity = Vector2.zero;
        moveInput = Vector2.zero;
        rb.position = startPos;
        transform.position = startPos;
        SetFacing(startFacingRight);
    }

    // ---- Hooks for characters with unique mechanics (override in a subclass) ----

    // Called on the tick an attack starts (index = FollowUpIndex for the follow-up)
    protected virtual void OnAttackStarted(int index) { }

    // Called every tick of an attack, tick 1 being the first. E.g. spawn a projectile on a given tick.
    // Projectile hits can be added by overriding CollectHits: call base.CollectHits(hits), then add your own.
    protected virtual void OnAttackTick(int index, int tick) { }

    // Hurtbox in green (extended hurtbox in light green), the current attack's hitbox in yellow (red on the ticks it can hit).
    // Shows in the Scene view, and in the Game view with Gizmos turned on.
    protected virtual void OnDrawGizmos()
    {
        Rect hurt = Hurtbox;
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(hurt.center, hurt.size);

        if (currentAttack == null) return;
        if (ExtraHurtboxActive)
        {
            Rect extra = ExtraHurtbox;
            Gizmos.color = new Color(0.4f, 1f, 0.4f, 0.8f);   // lighter green: the extended hurtbox
            Gizmos.DrawWireCube(extra.center, extra.size);
        }

        Rect hit = AttackHitbox(currentAttack);
        Gizmos.color = HitboxActive ? Color.red : new Color(1f, 0.9f, 0f, 0.5f);
        Gizmos.DrawWireCube(hit.center, hit.size);
    }
}
