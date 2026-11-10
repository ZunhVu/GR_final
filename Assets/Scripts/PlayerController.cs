using UnityEngine;
using UnityEngine.InputSystem;

// Turns keyboard/gamepad input (the "Walk" and "Attack" actions) into fighter commands.
// MatchSetup adds it to the human player's fighter and calls Init with the project's Input Actions asset.
public class PlayerController : MonoBehaviour, IFighterController
{
    [Tooltip("Only needed for a fighter placed by hand; MatchSetup passes it in through Init")]
    [SerializeField] private InputActionAsset actions;

    [Tooltip("How many ticks a press is remembered. Pressing a little early (during stun or recovery) " +
             "still comes out on the first tick the fighter can act.")]
    [SerializeField, Min(1)] private int bufferTicks = 6;

    private InputAction moveAction;
    private InputAction attackAction;
    private bool attackPressed;    // set by the input event, picked up on the next tick
    private int bufferLeft;        // ticks the latest press is still valid for
    private int serialAtPress;     // the fighter's AttackSerial when pressed, to see when the attack came out

    private void Awake()
    {
        if (actions != null) Init(actions);
    }

    public void Init(InputActionAsset inputActions)
    {
        Unhook();
        actions = inputActions;
        moveAction = actions.FindAction("Walk", true);
        attackAction = actions.FindAction("Attack", true);
        moveAction.Enable();
        attackAction.Enable();
        if (isActiveAndEnabled) Hook();
    }

    private void OnEnable() => Hook();

    private void OnDisable()
    {
        Unhook();
        attackPressed = false;
        bufferLeft = 0;
    }

    private void Hook()
    {
        Unhook();   // never subscribe twice
        if (attackAction != null) attackAction.performed += OnAttackPerformed;
    }

    private void Unhook()
    {
        if (attackAction != null) attackAction.performed -= OnAttackPerformed;
    }

    private void OnAttackPerformed(InputAction.CallbackContext ctx) => attackPressed = true;

    public FighterCommand GetCommand(BaseFighter self)
    {
        FighterCommand cmd = FighterCommand.None;
        if (moveAction != null) cmd.moveX = moveAction.ReadValue<Vector2>().x;

        int serial = self is Fighter f ? f.AttackSerial : 0;
        if (attackPressed)
        {
            attackPressed = false;
            bufferLeft = bufferTicks;
            serialAtPress = serial;
        }
        else if (serial != serialAtPress)
        {
            bufferLeft = 0;   // the buffered attack came out, so one press gives one attack
        }

        if (bufferLeft > 0)
        {
            cmd.attack = 0;
            bufferLeft--;
        }
        return cmd;
    }
}
