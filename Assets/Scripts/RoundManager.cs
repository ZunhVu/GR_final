using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Minimal round loop: watches every fighter for a KO, waits a moment, then resets everyone.
/// Works with any script that inherits FighterBase (player, dummy, opponent, AI).
/// ResetRound() is also what the AI training loop will call at the start of each episode.
/// </summary>
public class RoundManager : MonoBehaviour
{
    [SerializeField] private BaseFighter[] fighters;
    [SerializeField] private float resetDelay = 1.5f;   // seconds to show the KO before restarting

    private bool roundOver;
    private float resetTimer;

    private void FixedUpdate()
    {
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