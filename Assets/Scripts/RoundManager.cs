using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Runs the fight: every tick it steps all fighters together, then watches for a KO, waits a moment, and resets everyone.
/// Works with any script that inherits BaseFighter (player, dummy, opponent, AI).
/// ResetRound() is also what the AI training loop will call at the start of each episode.
/// </summary>
public class RoundManager : MonoBehaviour
{
    [SerializeField] private BaseFighter[] fighters;
    [SerializeField] private float resetDelay = 1.5f;   // seconds to show the KO before restarting
    [Tooltip("Most ticks run in one frame after a slow frame. Higher catches up on lag by skipping ahead " +
             "(fighters jump); lower slows the game down instead, like most fighting games.")]
    [SerializeField, Min(1)] private int maxTicksPerFrame = 3;
    [Tooltip("Logs who can act first after every hit or block, e.g. \"P1 stab Blocked: P1 -1\"")]
    [SerializeField] private bool logFrameAdvantage = true;

    private static RoundManager active;   // the manager stepping the fighters in this scene
    private readonly List<BaseFighter.PendingHit> hits = new List<BaseFighter.PendingHit>();
    private bool roundOver;
    private float resetTimer;

    // True if a RoundManager steps this fighter, so the fighter shouldn't step itself
    public static bool Drives(BaseFighter f) =>
        active != null && active.isActiveAndEnabled && active.fighters != null && System.Array.IndexOf(active.fighters, f) >= 0;

    private void OnEnable()
    {
        active = this;
        // Unity normally runs up to a third of a second of ticks to catch up after a hitch
        Time.maximumDeltaTime = Time.fixedDeltaTime * maxTicksPerFrame;
    }

    // MatchSetup hands over the spawned fighters
    public void SetFighters(params BaseFighter[] newFighters)
    {
        fighters = newFighters;
        roundOver = false;
    }

    private void OnDisable()
    {
        if (active == this) active = null;
    }

    private void FixedUpdate()
    {
        StepFighters();

        if (!roundOver)
        {
            foreach (BaseFighter f in fighters)
            {
                if (f != null && f.IsKO)
                {
                    roundOver = true;
                    resetTimer = resetDelay;
                    Debug.Log(f.name + " is KO");
                    break;
                }
            }
        }
        else
        {
            resetTimer -= Time.fixedDeltaTime;
            if (resetTimer <= 0f) ResetRound();
        }
    }

    // One tick for everyone, phase by phase. Each phase finishes for every fighter before the next starts,
    // so no fighter sees another's changes from this tick early, and hits landing on the same tick all count.
    private void StepFighters()
    {
        foreach (BaseFighter f in fighters) if (f != null) f.ReadCommand();
        foreach (BaseFighter f in fighters) if (f != null) f.Advance();

        hits.Clear();
        foreach (BaseFighter f in fighters) if (f != null) f.CollectHits(hits);
        foreach (BaseFighter.PendingHit h in hits)
        {
            BaseFighter.HitType type = h.target.ReceiveHit(h.attack, h.attackerX);
            if (logFrameAdvantage && type != BaseFighter.HitType.None && h.attacker != null) LogAdvantage(h, type);
        }

        foreach (BaseFighter f in fighters) if (f != null) f.Move();
    }

    // Frame advantage: how many ticks earlier the attacker can act than the defender after this hit.
    // + means the attacker is free first (can keep attacking), - means the defender is (can punish or interrupt).
    private void LogAdvantage(BaseFighter.PendingHit h, BaseFighter.HitType type)
    {
        int defenderBusy = h.target.StunTicksLeft;
        int attackerBusy = Mathf.Max(h.attacker.RecoveryTicksLeft, h.attacker.StunTicksLeft);   // a trade stuns the attacker too
        int advantage = defenderBusy - attackerBusy;
        Debug.Log($"{h.attacker.name} {h.attack.name} {type}: {h.attacker.name} {advantage:+0;-0;0} " +
                  $"(defender busy {defenderBusy}, attacker busy {attackerBusy})");
    }

    private void Update()
    {
        // Manual reset for testing
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            ResetRound();
    }

    public void ResetRound()
    {
        roundOver = false;

        foreach (BaseFighter f in fighters)
            if (f != null) f.ResetFighter();

        Debug.Log("round reset");
    }
}
