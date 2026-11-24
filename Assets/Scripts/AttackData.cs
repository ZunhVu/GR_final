using UnityEngine;

// How an attack must be blocked
public enum AttackHeight
{
    Mid,   // blocked standing or crouching
    Low,   // only blocked crouching
}

[System.Serializable]
public class AttackData
{
    public string name;
    [Tooltip("Animator state played when this attack starts")]
    public string animationState;
    [Tooltip("Mid: blocked standing or crouching. Low: only blocked crouching.")]
    public AttackHeight height = AttackHeight.Mid;

    [Header("frame data (ticks)")]
    public int startupTicks;
    public int activeTicks;
    public int recoveryTicks;

    [Header("hitbox (sprite units, before the fighter's scale, facing right)")]
    public Vector2 hitboxOffset;
    public Vector2 hitboxSize = new Vector2(0.1f, 0.1f);

    [Header("extended hurtbox (the arm/sword sticking out can be hit; size 0 = none)")]
    [Tooltip("Centre of the extra hurtbox, measured like the hitbox (sprite units, facing right, from the feet)")]
    public Vector2 extraHurtboxOffset;
    public Vector2 extraHurtboxSize;
    [Tooltip("First attack tick it exists on (1 = first startup tick)")]
    public int extraHurtboxStartTick = 1;
    [Tooltip("Last attack tick it exists on. 0 = until the attack ends")]
    public int extraHurtboxEndTick;
    public bool hasExtraHurtbox => extraHurtboxSize.x > 0f && extraHurtboxSize.y > 0f;

    [Header("movement during the attack (world units, toward the opponent; 0 = stays in place)")]
    [Tooltip("Total distance the attacker slides forward (negative = backwards). Bodies still can't overlap: it pushes the opponent.")]
    public float forwardMove;
    [Tooltip("First attack tick of the slide (1 = first startup tick)")]
    public int moveStartTick = 1;
    [Tooltip("Last attack tick of the slide. 0 = until the active ticks end")]
    public int moveEndTick;

    [Header("effects on receiver")]
    public int damage;
    public int chipDamage;
    public int hitStunTicks;
    public int blockStunTicks;


    [Header("pushback(world units, total distance)")]
    public float hitPushback;
    public float blockPushback;
    public bool isValid => activeTicks > 0;

    [Header("target combo / knockdown")]
    [Tooltip("After this attack connects (hit or blocked, not a whiff), the character's follow-up can be started for a few ticks")]
    public bool canFollowUp;
    [Tooltip("A clean hit (not blocked) knocks the receiver down: it falls, lies on the ground and gets up, and can't be hit meanwhile")]
    public bool knockdown;
    [Tooltip("Knockdown only: ticks the receiver lies on the ground (a harder knockdown = longer). " +
             "0 = the receiver's own default. Falling and getting up always take the receiver's own time, to match its animations.")]
    [Min(0)] public int knockdownDownTicks;

    [Header("counter hit")]
    public float counterDamageMultiplier = 1.2f;
    public int counterHitBonusStun = 2;

    [Header("punish counter")]
    public float punishDamageMultiplier = 1.2f;
    public int punishCounterBonusStun = 4;
}
