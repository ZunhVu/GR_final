using UnityEngine;

// What a fighter is told to do for one tick. Players, scripted CPUs and RL agents all speak through this, so the fighter enforces the same rules (startup, stun, block) whoever is in control.
public struct FighterCommand
{
    public float moveX;   // -1 left, 0 none, +1 right (screen direction, not facing)
    public int attack;    // -1 = no attack, otherwise the attack index (from the crouching list while crouching)
    public bool crouch;   // hold to crouch: can't walk, can still block (and blocks lows), attacks use the crouching list
    public bool guard;    // block in place without holding back (the training dummy's Block state; players block by holding back)

    public static FighterCommand None => new FighterCommand { moveX = 0f, attack = -1, crouch = false, guard = false };
}

// Anything that can drive a fighter. Put the component on the same GameObject as the fighter.
// Controllers may read any game state, but can only change the game through the command they return.
public interface IFighterController
{
    // Called once per tick from Fighter.ReadCommand
    FighterCommand GetCommand(BaseFighter self);
}
