using UnityEngine;

// What a fighter is told to do for one tick. Players, scripted CPUs and RL agents all speak through this, so the fighter enforces the same rules (startup, stun, block) whoever is in control.
public struct FighterCommand
{
    public float moveX;   // -1 left, 0 none, +1 right (screen direction, not facing)
    public int attack;    // -1 = no attack, otherwise the attack index 

    public static FighterCommand None => new FighterCommand { moveX = 0f, attack = -1 };
}

// Anything that can drive a fighter. Put the component on the same GameObject as the fighter.
// Controllers may read any game state, but can only change the game through the command they return.
public interface IFighterController
{
    // Called once per tick from the fighter's FixedUpdate
    FighterCommand GetCommand(BaseFighter self);
}
