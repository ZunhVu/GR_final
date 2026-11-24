using UnityEngine;

// Rule-based opponent: the baseline the RL agent is compared against, and a training partner.
// It walks in, attacks when in reach (standing or crouching attacks, going low against a standing blocker),
// and sometimes blocks (crouching against lows) or backs off.
// Fixed behaviours for testing (stand still, always attack, always block) are in TrainingDummy.
// It only reads public game state and only acts through FighterCommand, so the fighter's own rules
// (startup, stun, blocking) apply to it exactly as to a player.
public class ScriptedCPU : MonoBehaviour, IFighterController
{
    [SerializeField] private BaseFighter opponent;

    [Header("distances (world units)")]
    [Tooltip("Only used if this fighter can't check its real attack reach (it isn't a Fighter)")]
    [SerializeField] private float attackRange = 0.28f;
    [SerializeField] private float threatRange = 0.5f;    // opponent attacks closer than this get blocked

    [Header("decisions")]
    [SerializeField, Min(1)] private int reactionTicks = 8;   // a new decision every N ticks, held in between
    [SerializeField, Range(0f, 1f)] private float attackChance = 0.6f;
    [SerializeField, Range(0f, 1f)] private float blockChance = 0.5f;
    [SerializeField, Range(0f, 1f)] private float backOffChance = 0.3f;
    [Tooltip("When the opponent is blocking standing, the chance to pick a crouching attack (a low gets past a standing block)")]
    [SerializeField, Range(0f, 1f)] private float lowVsStandingBlockChance = 0.7f;
    [Tooltip("How long (ticks) the CPU remembers that the opponent blocked standing")]
    [SerializeField, Min(0)] private int rememberBlockTicks = 60;
    [Tooltip("After one of its attacks hits, the chance to finish with the follow-up (knockdown)")]
    [SerializeField, Range(0f, 1f)] private float followUpChance = 0.7f;
    [Tooltip("Ticks it takes to notice the hit before pressing the follow-up")]
    [SerializeField, Min(0)] private int followUpDelayTicks = 2;
    [SerializeField] private int seed = 1;   // same seed, same choices, so evaluation runs can be repeated

    private System.Random rng;
    private int ticksUntilDecision;
    private int moveSign;           // held movement: +1 toward the opponent, -1 away, 0 stand
    private int pendingAttack = -1; // attack chosen at the last decision, sent for one tick only
    private bool crouchPlan;        // held crouch from the last decision (crouching block, or a crouching attack)
    private int standingBlockMemory; // ticks left of "the opponent just blocked standing"
    private int followUpWait = -1;   // -1: no follow-up window seen yet; otherwise ticks left before pressing
    private bool followUpGo;         // decided once per window: use the follow-up this time?

    private void Awake() => rng = new System.Random(seed);

    // MatchSetup calls this right after adding the component
    public void Init(BaseFighter opponentFighter)
    {
        opponent = opponentFighter;
        rng = new System.Random(seed);
        ticksUntilDecision = 0;
        pendingAttack = -1;
        crouchPlan = false;
        standingBlockMemory = 0;
        followUpWait = -1;
    }

    public FighterCommand GetCommand(BaseFighter self)
    {
        FighterCommand cmd = FighterCommand.None;
        if (opponent == null) return cmd;

        // Watch every tick (not only on decisions): a standing block is remembered for a while
        if (opponent.IsBlockstun && !opponent.IsCrouching) standingBlockMemory = rememberBlockTicks;
        else if (opponent.IsHitstun || opponent.IsCrouching) standingBlockMemory = 0;   // it got hit or ducked
        else if (standingBlockMemory > 0) standingBlockMemory--;

        float dx = opponent.transform.position.x - self.transform.position.x;
        float toward = dx >= 0f ? 1f : -1f;

        if (--ticksUntilDecision <= 0)
        {
            ticksUntilDecision = reactionTicks;
            Decide(self, Mathf.Abs(dx));
        }

        // Direction is worked out every tick from the held choice, so it stays right if the fighters cross
        cmd.moveX = moveSign * toward;
        cmd.crouch = crouchPlan;
        if (pendingAttack >= 0)
        {
            cmd.attack = pendingAttack;
            pendingAttack = -1;   // one press per decision, like a player tapping the button
        }

        // Follow-up: checked every tick, since its window is short. Decide once per window, press after a short delay.
        if (self is Fighter f && f.FollowUpAvailable)
        {
            if (followUpWait < 0)
            {
                followUpWait = followUpDelayTicks;
                followUpGo = Roll(followUpChance);
            }
            if (followUpGo && followUpWait-- <= 0) cmd.followUp = true;
        }
        else followUpWait = -1;
        return cmd;
    }

    private void Decide(BaseFighter self, float dist)
    {
        moveSign = 1;
        pendingAttack = -1;
        crouchPlan = false;

        if (IsThreatened(dist) && Roll(blockChance))
        {
            moveSign = -1;              // hold back = block
            crouchPlan = ThreatIsLow(); // crouch against a low
        }
        else if (PickReachableAttack(self, dist, out int attack, out bool crouching))
        {
            if (Roll(attackChance))
            {
                moveSign = 0;
                pendingAttack = attack;
                crouchPlan = crouching;   // crouch and press on the same tick: the fighter takes the crouching list
            }
            else moveSign = Roll(backOffChance) ? -1 : 0;
        }
    }

    // A random attack whose hitbox would touch the opponent right now, from the standing or the crouching list.
    // Against a standing blocker it tries the crouching attacks first (most of the time), since lows beat a standing block.
    private bool PickReachableAttack(BaseFighter self, float dist, out int attack, out bool crouching)
    {
        attack = -1;
        crouching = false;
        if (!(self is Fighter f))
        {
            if (dist > attackRange) return false;
            attack = 0;
            return true;
        }

        bool preferLow = OpponentBlockingStanding() && Roll(lowVsStandingBlockChance);
        if (preferLow && PickFrom(f, true, out attack)) { crouching = true; return true; }

        // otherwise any reachable attack from both lists, chosen at random
        int standCount = f.AttackCount, total = standCount + f.CrouchAttackCount;
        if (total == 0) return false;
        int start = rng.Next(total);
        for (int k = 0; k < total; k++)
        {
            int i = (start + k) % total;
            bool low = i >= standCount;          // indexes past the standing list are crouching attacks
            int index = low ? i - standCount : i;
            if (f.AttackWouldReach(index, low))
            {
                attack = index;
                crouching = low;
                return true;
            }
        }
        return false;
    }

    // A random reachable attack from one list
    private bool PickFrom(Fighter f, bool crouchList, out int attack)
    {
        attack = -1;
        int count = crouchList ? f.CrouchAttackCount : f.AttackCount;
        if (count == 0) return false;
        int start = rng.Next(count);
        for (int k = 0; k < count; k++)
        {
            int i = (start + k) % count;
            if (f.AttackWouldReach(i, crouchList)) { attack = i; return true; }
        }
        return false;
    }

    // The opponent blocked standing recently: it is likely to block high again, so a low should get through
    private bool OpponentBlockingStanding() => standingBlockMemory > 0;

    // The opponent is close and its attack is starting up or active (recovery can't hurt)
    private bool IsThreatened(float dist)
    {
        BaseFighter.AttackPhase phase = opponent.CurrentAttackPhase;
        return dist < threatRange &&
               (phase == BaseFighter.AttackPhase.Startup || phase == BaseFighter.AttackPhase.Active);
    }

    // The opponent's current attack has to be blocked crouching
    private bool ThreatIsLow() =>
        opponent is Fighter f && f.CurrentAttack != null && f.CurrentAttack.height == AttackHeight.Low;

    private bool Roll(float chance) => rng.NextDouble() < chance;
}
