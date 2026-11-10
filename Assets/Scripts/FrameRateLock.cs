using UnityEngine;

// Caps rendering at 60 frames per second in every scene, with nothing to add to a scene.
// The game logic already runs at exactly 60 ticks per second (Fixed Timestep = 1/60 in Project Settings > Time);
// this makes the picture match it, so each rendered frame shows one tick.
public static class FrameRateLock
{
    public const int TargetFps = 60;

    // Unity calls this once when the game starts, before the first scene loads
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        QualitySettings.vSyncCount = 0;             // VSync would ignore targetFrameRate and follow the monitor (e.g. 144 Hz)
        Application.targetFrameRate = TargetFps;
        Time.fixedDeltaTime = 1f / TargetFps;       // keep the tick rate at 60 even if Project Settings get changed
    }
}
