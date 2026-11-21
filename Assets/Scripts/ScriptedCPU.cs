using UnityEngine;

// Rule-based opponent: the baseline the RL agent is compared against, and a training partner.
// It walks in, attacks when in reach, and sometimes blocks (crouching against lows) or backs off.
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
    [SerializeField] private int seed = 1;   // same seed, same choices, so evaluation runs can be repeated

    private System.Random rng;
    private int ticksUntilDecision;
    private int moveSign;           // held movement: +1 toward the opponent, -1 away, 0 stand
    private int pendingAttack = -1; // attack chosen at the last decision, sent for one tick only
    private bool crouchPlan;        // held crouch from the last decision (crouching block against a low)

    private void Awake() => rng = new System.Random(seed);

    // MatchSetup calls this right after adding the component
    public void Init(BaseFighter opponentFighter)
    {
        opponent = opponentFighter;
        rng = new System.Random(seed);
        ticksUntilDecision = 0;
        pendingAttack = -1;
        crouchPlan = false;
    }

    public FighterCommand GetCommand(BaseFighter self)
    {
        FighterCommand cmd = FighterCommand.None;
        if (opponent == null) return cmd;

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
        return cmd;
    }

    private void Decide(BaseFighter self, float dist)
    {
        moveSign = 1;
        pendingAttack = -1;
        crouchPlan = false;
        int reachable = PickReachableAttack(self, dist);

        if (IsThreatened(dist) && Roll(blockChance))
        {
            moveSign = -1;              // hold back = block
            crouchPlan = ThreatIsLow(); // crouch against a low
        }
        else if (reachable >= 0)
        {
            if (Roll(attackChance)) { moveSign = 0; pendingAttack = reachable; }
            else moveSign = Roll(backOffChance) ? -1 : 0;
        }
    }

    // A random attack whose hitbox would touch the opponent right now, or -1 if none reach
    private int PickReachableAttack(BaseFighter self, float dist)
    {
        if (!(self is Fighter f)) return dist <= attackRange ? 0 : -1;

        int count = f.AttackCount;
        if (count == 0) return -1;
        int start = rng.Next(count);
        for (int k = 0; k < count; k++)
        {
            int i = (start + k) % count;
            if (f.AttackWouldReach(i)) return i;
        }
        return -1;
    }

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
