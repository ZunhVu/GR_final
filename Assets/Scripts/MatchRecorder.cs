using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

// Writes a log of every match to a JSON file: the setup (characters, controllers, rules), every input change
// of both fighters stamped with its tick, what happened (attacks, hits, blocks, knockdowns, rounds), and a summary.
//
// The game is deterministic (fixed 60 ticks per second, no randomness inside the fighters), so the setup plus the
// inputs are enough to play the match again exactly: a replay feeds the recorded commands back in, tick by tick.
// The round results (health and positions at every round end) let a replay check that it came out the same.
//
// Put it on the same GameObject as the RoundManager. Turn it off (or remove it) for RL training, which plays
// thousands of matches.
public class MatchRecorder : MonoBehaviour
{
    [SerializeField] private RoundManager roundManager;
    [Tooltip("Write a log for every match")]
    [SerializeField] private bool record = true;
    [Tooltip("Also save matches that didn't finish (restarted with Shift + R, or left through Quit / Escape)")]
    [SerializeField] private bool saveUnfinished = true;
    [Tooltip("Also log Training mode (endless fights against the dummy). Off: only Versus matches are logged")]
    [SerializeField] private bool recordTraining = false;
    [Tooltip("Add every attack that starts to the event list (not only hits and blocks)")]
    [SerializeField] private bool logAttackStarts = true;
    [Tooltip("Folder for the logs. In the Editor it is inside the project folder (next to Assets); in a build, in Application.persistentDataPath")]
    [SerializeField] private string folderName = "MatchLogs";

    public const int FormatVersion = 1;
    public const string InputFormat = "tick,player,moveX,attack,crouch,guard,followUp";

    public string LastSavedPath { get; private set; }
    public MatchLog CurrentLog => log;

    private MatchLog log;
    private bool saved;               // written to disk: the rest of the match (winner screen) isn't recorded
    private bool Recording => log != null && !saved;
    private int roundStartTick;
    private int[] lastInput;          // per player: the last command written, packed (see Pack)
    private int[] lastAttackSerial;

    // ---------- the log format (JsonUtility needs plain [Serializable] classes) ----------

    [Serializable]
    public class MatchLog
    {
        public int formatVersion = FormatVersion;
        public string date;                 // when the match started, local time
        public string gameVersion;
        public string unityVersion;
        public bool finished;               // false: the match was restarted or left before it ended
        public string endedBy;              // "match over", "restart", "quit"
        public string result = "";          // "P1 WINS", "P2 WINS", "DRAW", or "" when unfinished
        public int winner = -2;             // 0 = P1, 1 = P2, -1 = draw, -2 = not finished
        public int[] roundWins = new int[2];
        public int totalTicks;              // ticks played, including intros and pauses
        public Rules rules = new Rules();
        public PlayerInfo[] players;
        public List<RoundResult> rounds = new List<RoundResult>();

        [Tooltip("Each line: " + InputFormat + ". Written only when a player's command changes; " +
                 "it then stays the same until the next line for that player. moveX: -1 left, 0, +1 right (screen direction). " +
                 "attack: -1 none, otherwise the attack index. crouch / guard / followUp: 0 or 1.")]
        public string inputFormat = InputFormat;
        public List<string> inputs = new List<string>();

        // "tick  what happened", e.g. "  412  hit       P1 stab -> P2: Counter, 5 damage"
        public List<string> events = new List<string>();
    }

    [Serializable]
    public class Rules
    {
        public int ticksPerSecond;
        public int roundTicks;
        public int roundsToWin;
        public int introTicks;
        public int roundOverTicks;
        public int matchOverTicks;
    }

    [Serializable]
    public class PlayerInfo
    {
        public string label;                // "P1" / "P2"
        public string character;            // CharacterData asset name (what a replay loads)
        public string displayName;
        public string controller;           // PlayerController, ScriptedCPU, TrainingDummy, an RL agent...
        public string dataHash;             // changes if the character's gameplay numbers change: a replay needs the same numbers
        public float startX;
        public bool startFacingRight;
        public FighterStats stats = new FighterStats();
    }

    [Serializable]
    public class FighterStats
    {
        public int attacksStarted;
        public int hits;                    // clean hits landed (normal + counter + punish)
        public int counterHits;
        public int punishCounters;
        public int attacksBlocked;          // this fighter's attacks that the opponent blocked
        public int blocks;                  // opponent's attacks this fighter blocked
        public int damageDealt;             // including chip damage
        public int damageTaken;
        public int knockdowns;              // knockdowns caused
        public int followUps;               // follow-ups started
        public List<MoveStats> moves = new List<MoveStats>();
    }

    [Serializable]
    public class MoveStats
    {
        public string move;
        public int used;
        public int hit;
        public int blocked;
    }

    [Serializable]
    public class RoundResult
    {
        public int round;
        public int startTick;
        public int endTick;
        public float seconds;               // fighting time, intro included
        public int winner;                  // 0 = P1, 1 = P2, -1 = draw
        public string reason;               // K.O., DOUBLE K.O., TIME
        public int[] health = new int[2];   // left at the end of the round
        public float[] x = new float[2];    // positions at the end of the round (a replay should match them)
    }

    // ---------- hooking into the RoundManager ----------

    private void Awake()
    {
        if (roundManager == null) roundManager = GetComponent<RoundManager>();
        if (roundManager == null) roundManager = FindAnyObjectByType<RoundManager>();
    }

    private void OnEnable()
    {
        if (roundManager == null) return;
        roundManager.MatchStarted += OnMatchStarted;
        roundManager.RoundStarted += OnRoundStarted;
        roundManager.CommandsRead += OnCommandsRead;
        roundManager.HitApplied += OnHitApplied;
        roundManager.TickFinished += OnTickFinished;
        roundManager.RoundEnded += OnRoundEnded;
        roundManager.MatchEnded += OnMatchEnded;
        roundManager.ManualRestart += OnManualRestart;
    }

    private void OnDisable()
    {
        if (roundManager == null) return;
        roundManager.MatchStarted -= OnMatchStarted;
        roundManager.RoundStarted -= OnRoundStarted;
        roundManager.CommandsRead -= OnCommandsRead;
        roundManager.HitApplied -= OnHitApplied;
        roundManager.TickFinished -= OnTickFinished;
        roundManager.RoundEnded -= OnRoundEnded;
        roundManager.MatchEnded -= OnMatchEnded;
        roundManager.ManualRestart -= OnManualRestart;
    }

    // MatchSetup may start the first match before this script has subscribed (Awake order isn't fixed): catch up here
    private void Start()
    {
        if (log == null && roundManager != null && roundManager.FighterCount > 0 && roundManager.MatchTick == 0)
        {
            OnMatchStarted();
            OnRoundStarted(roundManager.RoundNumber);
        }
    }

    private void OnDestroy() => FinishUnfinished("quit");     // scene change (Quit, Escape) or leaving Play mode

    // ---------- events ----------

    private void OnMatchStarted()
    {
        FinishUnfinished("restart");   // Retry finds the last match already saved; Shift + R doesn't
        if (!record || roundManager == null || (roundManager.IsTrainingMode && !recordTraining)) { log = null; return; }

        int n = Mathf.Max(2, roundManager.FighterCount);
        log = new MatchLog
        {
            date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            gameVersion = Application.version,
            unityVersion = Application.unityVersion,
            players = new PlayerInfo[n],
        };
        log.rules.ticksPerSecond = Mathf.RoundToInt(1f / Time.fixedDeltaTime);
        log.rules.roundTicks = roundManager.RoundTicks;
        log.rules.roundsToWin = roundManager.RoundsToWin;
        log.rules.introTicks = roundManager.IntroTicks;
        log.rules.roundOverTicks = roundManager.RoundOverTicks;
        log.rules.matchOverTicks = roundManager.MatchOverTicks;

        for (int i = 0; i < n; i++)
        {
            BaseFighter f = roundManager.GetFighter(i);
            var info = new PlayerInfo { label = "P" + (i + 1) };
            if (f is Fighter fighter)
            {
                CharacterData d = fighter.Data;
                info.character = d != null ? d.name : "";
                info.displayName = d != null ? d.displayName : f.name;
                info.controller = fighter.Controller != null ? fighter.Controller.GetType().Name : "none";
                info.dataHash = d != null ? DataHash(d) : "";
            }
            else if (f != null)
            {
                info.displayName = f.name;
                info.controller = f.GetType().Name;
            }
            log.players[i] = info;
        }

        saved = false;
        lastInput = new int[n];
        lastAttackSerial = new int[n];
        for (int i = 0; i < n; i++) { lastInput[i] = int.MinValue; lastAttackSerial[i] = AttackSerialOf(i); }
        AddEvent(roundManager.MatchTick, "match", Players());
    }

    private void OnRoundStarted(int round)
    {
        if (!Recording) return;
        roundStartTick = roundManager.MatchTick;   // the round's first tick is the next one to run
        if (round == 1 && log.rounds.Count == 0)
            for (int i = 0; i < log.players.Length; i++)
            {
                BaseFighter f = roundManager.GetFighter(i);
                if (f == null) continue;
                log.players[i].startX = f.transform.position.x;
                log.players[i].startFacingRight = f.FacingRight;
            }
        AddEvent(roundStartTick, "round", "Round " + round + " starts");
    }

    // Every tick: write down each fighter's command if it differs from its last one
    private void OnCommandsRead()
    {
        if (!Recording) return;
        int tick = roundManager.MatchTick;
        for (int i = 0; i < log.players.Length; i++)
        {
            BaseFighter f = roundManager.GetFighter(i);
            if (f == null) continue;
            FighterCommand c = f.LastCommand;
            int packed = Pack(c);
            if (packed == lastInput[i]) continue;
            lastInput[i] = packed;
            log.inputs.Add(tick + "," + i + "," + MoveSign(c.moveX) + "," + c.attack + "," +
                           B(c.crouch) + "," + B(c.guard) + "," + B(c.followUp));
        }
    }

    private void OnHitApplied(BaseFighter.PendingHit h, BaseFighter.HitType type, int damage)
    {
        if (!Recording) return;
        int a = IndexOf(h.attacker), t = IndexOf(h.target);
        string move = h.attack != null ? h.attack.name : "?";
        bool knockdown = h.target != null && h.target.IsKnockedDown && type != BaseFighter.HitType.Blocked;

        if (a >= 0)
        {
            FighterStats s = log.players[a].stats;
            MoveStats m = Move(s, move);
            s.damageDealt += damage;
            if (type == BaseFighter.HitType.Blocked) { s.attacksBlocked++; m.blocked++; }
            else
            {
                s.hits++; m.hit++;
                if (type == BaseFighter.HitType.Counter) s.counterHits++;
                if (type == BaseFighter.HitType.PunishCounter) s.punishCounters++;
                if (knockdown) s.knockdowns++;
            }
        }
        if (t >= 0)
        {
            FighterStats s = log.players[t].stats;
            s.damageTaken += damage;
            if (type == BaseFighter.HitType.Blocked) s.blocks++;
        }

        AddEvent(roundManager.MatchTick, type == BaseFighter.HitType.Blocked ? "block" : "hit",
            Label(a) + " " + move + " -> " + Label(t) + ": " + type + ", " + damage + " damage" + (knockdown ? ", KNOCKDOWN" : ""));
    }

    // After the tick: which fighters started an attack this tick
    private void OnTickFinished()
    {
        if (!Recording) return;
        log.totalTicks = roundManager.MatchTick + 1;
        for (int i = 0; i < log.players.Length; i++)
        {
            if (!(roundManager.GetFighter(i) is Fighter f) || f.AttackSerial == lastAttackSerial[i]) continue;
            lastAttackSerial[i] = f.AttackSerial;
            if (f.CurrentAttack == null) continue;

            FighterStats s = log.players[i].stats;
            s.attacksStarted++;
            if (f.IsFollowUp) s.followUps++;
            Move(s, f.CurrentAttack.name).used++;
            if (logAttackStarts)
                AddEvent(roundManager.MatchTick, "attack", Label(i) + " " + f.CurrentAttack.name + (f.IsCrouching ? " (crouching)" : ""));
        }
    }

    private void OnRoundEnded(int winner)
    {
        if (!Recording) return;
        int tick = roundManager.MatchTick;
        var r = new RoundResult
        {
            round = roundManager.RoundNumber,
            startTick = roundStartTick,
            endTick = tick,
            seconds = (tick - roundStartTick + 1) * Time.fixedDeltaTime,
            winner = winner,
            reason = roundManager.LastRoundReason,
        };
        for (int i = 0; i < 2; i++)
        {
            BaseFighter f = roundManager.GetFighter(i);
            if (f == null) continue;
            r.health[i] = f.FighterHealth != null ? f.FighterHealth.Current : 0;
            r.x[i] = f.transform.position.x;
        }
        log.rounds.Add(r);
        log.roundWins[0] = roundManager.Wins(0);
        log.roundWins[1] = roundManager.Wins(1);
        AddEvent(tick, "round", "Round " + r.round + ": " + r.reason + ", " +
                 (winner == -1 ? "draw" : Label(winner) + " wins") + " (" + log.roundWins[0] + " - " + log.roundWins[1] + ")");
    }

    private void OnMatchEnded(int winner)
    {
        if (!Recording) return;
        log.finished = true;
        log.endedBy = "match over";
        log.winner = winner;
        log.result = roundManager.Announcement;
        AddEvent(roundManager.MatchTick, "match", "Match over: " + log.result);
        Save();
    }

    // R / Shift + R: written as events so a replay can restart at the same tick
    private void OnManualRestart(bool wholeMatch)
    {
        if (!Recording) return;
        AddEvent(roundManager.MatchTick, "restart", wholeMatch
            ? "Match restarted (Shift + R) before this tick"
            : "Round restarted (R) before this tick");
    }

    // ---------- saving ----------

    private void FinishUnfinished(string reason)
    {
        if (log == null || saved) return;
        if (saveUnfinished && (log.inputs.Count > 0 || log.rounds.Count > 0))
        {
            log.finished = false;
            log.endedBy = reason;
            Save();
        }
        log = null;
    }

    private void Save()
    {
        if (log == null || saved) return;
        saved = true;

        try
        {
            string folder = Folder();
            Directory.CreateDirectory(folder);
            string p1 = log.players.Length > 0 ? log.players[0].displayName : "P1";
            string p2 = log.players.Length > 1 ? log.players[1].displayName : "P2";
            string baseName = "match_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Safe(p1) + "_vs_" + Safe(p2);
            string path = Path.Combine(folder, baseName + ".json");
            for (int n = 2; File.Exists(path); n++) path = Path.Combine(folder, baseName + "_" + n + ".json");

            File.WriteAllText(path, JsonUtility.ToJson(log, true), Encoding.UTF8);
            LastSavedPath = path;
            Debug.Log("Match log saved: " + path + (log.finished ? "" : " (unfinished: " + log.endedBy + ")"));
        }
        catch (Exception e)
        {
            Debug.LogError("MatchRecorder: couldn't save the match log: " + e.Message);
        }
    }

    private string Folder() =>
        Application.isEditor
            ? Path.Combine(Path.GetDirectoryName(Application.dataPath), folderName)   // <project>/MatchLogs
            : Path.Combine(Application.persistentDataPath, folderName);

    // ---------- helpers ----------

    private void AddEvent(int tick, string type, string text) =>
        log.events.Add(tick.ToString().PadLeft(6) + "  " + type.PadRight(8) + "  " + text);

    private string Players()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < log.players.Length; i++)
        {
            if (i > 0) sb.Append(" vs ");
            sb.Append(log.players[i].label).Append(' ').Append(log.players[i].displayName)
              .Append(" (").Append(log.players[i].controller).Append(')');
        }
        return sb.ToString();
    }

    private int IndexOf(BaseFighter f)
    {
        for (int i = 0; i < roundManager.FighterCount; i++)
            if (roundManager.GetFighter(i) == f) return i;
        return -1;
    }

    private string Label(int i) => i >= 0 && log != null && i < log.players.Length ? log.players[i].label : "?";

    private int AttackSerialOf(int i) => roundManager.GetFighter(i) is Fighter f ? f.AttackSerial : 0;

    private static MoveStats Move(FighterStats s, string name)
    {
        MoveStats m = s.moves.Find(x => x.move == name);
        if (m == null) { m = new MoveStats { move = name }; s.moves.Add(m); }
        return m;
    }

    // The fighter only cares whether moveX is past ±0.1 and which way, so -1 / 0 / +1 replays exactly
    private static int MoveSign(float x) => Mathf.Abs(x) > 0.1f ? (x > 0f ? 1 : -1) : 0;
    private static int B(bool b) => b ? 1 : 0;

    private static int Pack(FighterCommand c) =>
        ((MoveSign(c.moveX) + 1) | (B(c.crouch) << 2) | (B(c.guard) << 3) | (B(c.followUp) << 4)) + ((c.attack + 2) << 5);

    private static string Safe(string s)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s.Replace(' ', '_');
    }

    // A short fingerprint of a character's gameplay numbers (not its art): if it differs, an old log won't replay the same
    [Serializable]
    private class GameplayNumbers
    {
        public int maxHealth;
        public float forwardSpeed, backwardSpeed;
        public Vector2 hurtboxOffset, hurtboxSize, crouchHurtboxOffset, crouchHurtboxSize;
        public List<AttackData> attacks, crouchAttacks;
        public AttackData followUp;
        public int followUpWindowTicks, knockdownFallTicks, knockdownDownTicks, knockdownGetUpTicks;
    }

    private static string DataHash(CharacterData d)
    {
        var g = new GameplayNumbers
        {
            maxHealth = d.maxHealth, forwardSpeed = d.forwardSpeed, backwardSpeed = d.backwardSpeed,
            hurtboxOffset = d.hurtboxOffset, hurtboxSize = d.hurtboxSize,
            crouchHurtboxOffset = d.crouchHurtboxOffset, crouchHurtboxSize = d.crouchHurtboxSize,
            attacks = d.attacks, crouchAttacks = d.crouchAttacks, followUp = d.followUp,
            followUpWindowTicks = d.followUpWindowTicks, knockdownFallTicks = d.knockdownFallTicks,
            knockdownDownTicks = d.knockdownDownTicks, knockdownGetUpTicks = d.knockdownGetUpTicks,
        };
        uint hash = 2166136261;   // FNV-1a
        foreach (char c in JsonUtility.ToJson(g)) { hash ^= c; hash *= 16777619; }
        return hash.ToString("x8");
    }
}
