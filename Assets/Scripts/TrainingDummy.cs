using UnityEngine;
using UnityEngine.InputSystem;

// A practice opponent with a fixed behaviour, for testing moves, frame data and blocking.
// Its state can be changed during the fight with the number keys (1 Idle, 2 Attack, 3 Block).
// Like every controller it only sends FighterCommands, so the fighter's normal rules still apply.
public class TrainingDummy : MonoBehaviour, IFighterController
{
    public enum State
    {
        Idle,     // stands still
        Attack,   // stands still and attacks again as soon as it can (test counter hits and punishes)
        Block,    // guards in place without walking back; crouches against lows so every attack is blocked
    }

    [SerializeField] private BaseFighter opponent;
    [SerializeField] private State state = State.Idle;
    [Tooltip("Number keys 1 / 2 / 3 switch the state during the fight")]
    [SerializeField] private bool keysChangeState = true;
    [Tooltip("Shows the dummy's state in the top-left corner of the Game view")]
    [SerializeField] private bool showStateLabel = true;
    [SerializeField] private int seed = 1;   // picks which attack to use when it has several

    private System.Random rng;

    public State CurrentState => state;

    private void Awake() => rng = new System.Random(seed);

    // MatchSetup calls this right after adding the component
    public void Init(BaseFighter opponentFighter, State startState = State.Idle)
    {
        opponent = opponentFighter;
        state = startState;
        rng = new System.Random(seed);
    }

    public void SetState(State newState) => state = newState;

    private void Update()
    {
        if (!keysChangeState || Keyboard.current == null) return;
        if (Keyboard.current.digit1Key.wasPressedThisFrame) SetState(State.Idle);
        if (Keyboard.current.digit2Key.wasPressedThisFrame) SetState(State.Attack);
        if (Keyboard.current.digit3Key.wasPressedThisFrame) SetState(State.Block);
    }

    public FighterCommand GetCommand(BaseFighter self)
    {
        FighterCommand cmd = FighterCommand.None;
        switch (state)
        {
            case State.Attack:
                // Ask every tick; the fighter only starts one when it's free, so attacks come out back to back
                if (self is Fighter f && f.AttackCount > 0) cmd.attack = rng.Next(f.AttackCount);
                else cmd.attack = 0;
                break;

            case State.Block:
                // Guard in place (no walking back) and crouch against lows, so everything gets blocked
                cmd.guard = true;
                cmd.crouch = OpponentAttackIsLow();
                break;
        }
        return cmd;
    }

    private bool OpponentAttackIsLow() =>
        opponent is Fighter f && f.CurrentAttack != null && f.CurrentAttack.height == AttackHeight.Low;

    private void OnGUI()
    {
        if (!showStateLabel) return;
        string keys = keysChangeState ? "   (1 Idle  2 Attack  3 Block)" : "";
        GUI.Label(new Rect(10, 10, 500, 25), "Dummy: " + state + keys);
    }
}
