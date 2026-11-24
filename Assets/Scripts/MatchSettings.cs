// The two ways to play, picked on the main menu
public enum GameMode
{
    Training = 0,   // against the TrainingDummy (Idle / Attack / Block with 1 / 2 / 3): no timer, no rounds, health refills
    Versus = 1,     // against the ScriptedCPU: 100 s rounds, first to 2, Retry / Quit, match logs
}

// What the menus picked, carried over into the fight scene.
// A static class keeps its values when a new scene loads. The picks are empty (null) when the fight scene is played directly.
public static class MatchSettings
{
    public static GameMode mode = GameMode.Versus;
    public static CharacterData p1;
    public static CharacterData p2;

    public static bool HasPicks => p1 != null && p2 != null;

    // Forget the characters (the mode stays, it was just chosen on the main menu)
    public static void ClearPicks()
    {
        p1 = null;
        p2 = null;
    }
}
