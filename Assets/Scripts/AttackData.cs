
using UnityEngine;
[System.Serializable]
public class AttackData 
{
    public string name;
    [Header("frame data (ticks)")]
    public int startupTicks;
    public int activeTicks;
    public int recoveryTicks;
    [Header("effects on receiver")]
    public int damage;
    public int chipDamage;
    public int hitStunTicks;
    public int blockStunTicks;


    [Header("pushback(world units, total distance)")]
    public float hitPushback;
    public float blockPushback;
    public bool isValid => activeTicks > 0;
}
