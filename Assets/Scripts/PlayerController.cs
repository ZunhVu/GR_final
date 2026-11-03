using UnityEngine;
using UnityEngine.InputSystem;

// Turns keyboard/gamepad input (PlayerInput's "Walk" and "Attack" actions) into fighter commands.
[RequireComponent(typeof(PlayerInput))]
public class PlayerController : MonoBehaviour, IFighterController
{
    private InputAction moveAction;
    private InputAction attackAction;
    private bool attackPressed;   // remembered until the next tick asks for a command

    private void Awake()
    {
        PlayerInput playerInput = GetComponent<PlayerInput>();
        moveAction = playerInput.actions["Walk"];
        attackAction = playerInput.actions["Attack"];
    }

    private void OnEnable()
    {
        if (attackAction != null) attackAction.performed += OnAttackPerformed;
    }

    private void OnDisable()
    {
        if (attackAction != null) attackAction.performed -= OnAttackPerformed;
        attackPressed = false;
    }

    private void OnAttackPerformed(InputAction.CallbackContext ctx) => attackPressed = true;

    public FighterCommand GetCommand(BaseFighter self)
    {
        FighterCommand cmd = FighterCommand.None;
        if (moveAction != null) cmd.moveX = moveAction.ReadValue<Vector2>().x;
        if (attackPressed) cmd.attack = 0;
        attackPressed = false;   // one press, one attack attempt
        return cmd;
    }
}
