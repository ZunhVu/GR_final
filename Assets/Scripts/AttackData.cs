using UnityEngine;
[System.Serializable]
public class AttackData
{
    public string name;
    [Tooltip("Animator state played when this attack starts")]
    public string animationState;

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

    [Header("effects on receiver")]
    public int damage;
    public int chipDamage;
    public int hitStunTicks;
    public int blockStunTicks;


    [Header("pushback(world units, total distance)")]
    public float hitPushback;
    public float blockPushback;
    public bool isValid => activeTicks > 0;

    [Header("counter hit")]
    public float counterDamageMultiplier = 1.2f;
    public int counterHitBonusStun = 2;

    [Header("punish counter")]
    public float punishDamageMultiplier = 1.2f;
    public int punishCounterBonusStun = 4;
}
