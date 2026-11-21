using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Runs the fight: every tick it steps all fighters together, counts down the round timer, and ends the round
/// on a KO or time out. The first fighter to win roundsToWin rounds wins the match, then a new match starts.
/// fighters[0] is P1 and fighters[1] is P2 for rounds, timer and announcements.
/// Works with any script that inherits BaseFighter (player, dummy, CPU, AI).
/// </summary>
public class RoundManager : MonoBehaviour
{
    public enum Phase { Intro, Fighting, RoundOver, MatchOver }

    [SerializeField] private BaseFighter[] fighters;

    [Header("rules")]
    [SerializeField, Min(1)] private float roundTimeSeconds = 100f;
    [SerializeField, Min(1)] private int roundsToWin = 2;   // first to 2

    [Header("pauses (seconds)")]
    [Tooltip("Before each round, fighters can't act (shows \"ROUND n\" / \"FIGHT\"). Set 0 for training.")]
    [SerializeField, Min(0)] private float introDelay = 1f;
    [SerializeField, Min(0)] private float roundOverDelay = 1.5f;   // shows K.O. / TIME before the next round
    [SerializeField, Min(0)] private float matchOverDelay = 3f;     // shows the winner before a new match

    [Header("debug")]
    [Tooltip("Most ticks run in one frame after a slow frame. Higher catches up on lag by skipping ahead " +
             "(fighters jump); lower slows the game down instead, like most fighting games.")]
    [SerializeField, Min(1)] private int maxTicksPerFrame = 3;
    [Tooltip("Logs who can act first after every hit or block, e.g. \"P1 stab Blocked: P1 -1\"")]
    [SerializeField] private bool logFrameAdvantage = true;

    private static RoundManager active;   // the manager stepping the fighters in this scene
    private readonly List<BaseFighter.PendingHit> hits = new List<BaseFighter.PendingHit>();
    private readonly int[] wins = new int[2];

    private Phase phase;
    private int phaseTicksLeft;   // ticks left in Intro / RoundOver / MatchOver
    private int timerTicksLeft;   // round clock, counted in ticks so it is exact and repeatable

    // --- read by the HUD (and later the RL agent) ---
    public Phase CurrentPhase => phase;
    public int RoundNumber { get; private set; } = 1;
    public int RoundsToWin => roundsToWin;
    public int Wins(int player) => player >= 0 && player < wins.Length ? wins[player] : 0;
    public int TimerTicksLeft => timerTicksLeft;
    public int RoundTicks => SecondsToTicks(roundTimeSeconds);
    public int TimeLeftSeconds => Mathf.CeilToInt(timerTicksLeft * Time.fixedDeltaTime);   // 100 → 0 on screen
    public string Announcement { get; private set; } = "";   // "ROUND 1", "FIGHT!", "K.O.", "TIME", "P1 WINS" ...
    public int LastRoundWinner { get; private set; } = -1;    // 0 = P1, 1 = P2, -1 = draw

    // winner index: 0 = P1, 1 = P2, -1 = draw
    public event Action<int> RoundEnded;
    public event Action<int> MatchEnded;

    // True if a RoundManager steps this fighter, so the fighter shouldn't step itself
    public static bool Drives(BaseFighter f) =>
        active != null && active.isActiveAndEnabled && active.fighters != null && Array.IndexOf(active.fighters, f) >= 0;

    private void OnEnable()
    {
        active = this;
        // Unity normally runs up to a third of a second of ticks to catch up after a hitch
        Time.maximumDeltaTime = Time.fixedDeltaTime * maxTicksPerFrame;
    }

    private void OnDisable()
    {
        if (active == this) active = null;
    }

    private bool started;

    // Fighters set in the Inspector (no MatchSetup) start here; MatchSetup calls SetFighters earlier, in Awake
    private void Start()
    {
        if (!started && fighters != null && fighters.Length > 0) StartMatch();
    }

    // MatchSetup hands over the spawned fighters
    public void SetFighters(params BaseFighter[] newFighters)
    {
        fighters = newFighters;
        StartMatch();
    }

    private void FixedUpdate()
    {
        if (fighters == null) return;
        StepFighters();

        switch (phase)
        {
            case Phase.Intro:
                Announcement = phaseTicksLeft > SecondsToTicks(introDelay) / 2 ? "ROUND " + RoundNumber : "FIGHT!";
                if (--phaseTicksLeft <= 0) BeginFighting();
                break;

            case Phase.Fighting:
                if (timerTicksLeft > 0) timerTicksLeft--;
                CheckRoundEnd();
                break;

            case Phase.RoundOver:
                if (--phaseTicksLeft <= 0)
                {
                    if (MatchWinner() != -2) EndMatch();
                    else { RoundNumber++; StartRound(); }
                }
                break;

            case Phase.MatchOver:
                if (--phaseTicksLeft <= 0) StartMatch();
                break;
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
        if (phase == Phase.Fighting)   // after a KO or time out, a swing still in progress does nothing
        {
            foreach (BaseFighter.PendingHit h in hits)
            {
                BaseFighter.HitType type = h.target.ReceiveHit(h.attack, h.attackerX, h.attacker);
                if (logFrameAdvantage && type != BaseFighter.HitType.None && h.attacker != null) LogAdvantage(h, type);
            }
        }

        foreach (BaseFighter f in fighters) if (f != null) f.Move();
    }

    // ---------- rounds ----------

    private void CheckRoundEnd()
    {
        BaseFighter p1 = Fighter(0), p2 = Fighter(1);
        bool p1KO = p1 != null && p1.IsKO;
        bool p2KO = p2 != null && p2.IsKO;

        if (p1KO || p2KO)
        {
            // both KO on the same tick (a trade) is a draw
            int winner = p1KO && p2KO ? -1 : (p1KO ? 1 : 0);
            EndRound(winner, winner == -1 ? "DOUBLE K.O." : "K.O.");
        }
        else if (timerTicksLeft <= 0)
        {
            // time out: more health left (as a fraction of max) wins, equal is a draw
            float h1 = HealthFraction(p1), h2 = HealthFraction(p2);
            int winner = Mathf.Approximately(h1, h2) ? -1 : (h1 > h2 ? 0 : 1);
            EndRound(winner, "TIME");
        }
    }

    private void EndRound(int winner, string reason)
    {
        // a draw gives both fighters the round (so the match can't go on forever)
        if (winner == -1) { wins[0]++; wins[1]++; }
        else wins[winner]++;

        LastRoundWinner = winner;
        phase = Phase.RoundOver;
        phaseTicksLeft = Mathf.Max(1, SecondsToTicks(roundOverDelay));
        Announcement = reason;
        LockInput(true);

        Debug.Log($"Round {RoundNumber}: {reason}, {(winner == -1 ? "draw" : "P" + (winner + 1) + " wins")} " +
                  $"({wins[0]} - {wins[1]})");
        RoundEnded?.Invoke(winner);
    }

    // -2 = nobody has won yet, -1 = draw (both reached roundsToWin together), 0 / 1 = the winner
    private int MatchWinner()
    {
        bool p1Won = wins[0] >= roundsToWin, p2Won = wins[1] >= roundsToWin;
        if (p1Won && p2Won) return -1;
        if (p1Won) return 0;
        if (p2Won) return 1;
        return -2;
    }

    private void EndMatch()
    {
        int winner = MatchWinner();
        phase = Phase.MatchOver;
        phaseTicksLeft = Mathf.Max(1, SecondsToTicks(matchOverDelay));
        Announcement = winner == -1 ? "DRAW" : "P" + (winner + 1) + " WINS";
        Debug.Log("Match over: " + Announcement);
        MatchEnded?.Invoke(winner);
    }

    public void StartMatch()
    {
        started = true;
        wins[0] = wins[1] = 0;
        RoundNumber = 1;
        StartRound();
    }

    // Resets the fighters and the clock. Also what the AI training loop will call at the start of each episode.
    public void StartRound()
    {
        if (fighters != null)
            foreach (BaseFighter f in fighters)
                if (f != null) f.ResetFighter();

        timerTicksLeft = RoundTicks;
        LastRoundWinner = -1;

        int intro = SecondsToTicks(introDelay);
        if (intro > 0)
        {
            phase = Phase.Intro;
            phaseTicksLeft = intro;
            Announcement = "ROUND " + RoundNumber;
            LockInput(true);
        }
        else BeginFighting();
    }

    // Kept for old callers: restarts the current round
    public void ResetRound() => StartRound();

    private void BeginFighting()
    {
        phase = Phase.Fighting;
        Announcement = "";
        LockInput(false);
    }

    private void LockInput(bool locked)
    {
        if (fighters == null) return;
        foreach (BaseFighter f in fighters) if (f != null) f.InputLocked = locked;
    }

    // ---------- helpers ----------

    private BaseFighter Fighter(int i) => fighters != null && i < fighters.Length ? fighters[i] : null;

    private static float HealthFraction(BaseFighter f) =>
        f != null && f.FighterHealth != null && f.FighterHealth.Max > 0
            ? f.FighterHealth.Current / (float)f.FighterHealth.Max : 0f;

    private static int SecondsToTicks(float seconds) => Mathf.RoundToInt(seconds / Time.fixedDeltaTime);

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
        // Testing: R restarts the current round, Shift + R restarts the whole match
        Keyboard k = Keyboard.current;
        if (k != null && k.rKey.wasPressedThisFrame)
        {
            if (k.shiftKey.isPressed) StartMatch();
            else StartRound();
        }
    }
}
