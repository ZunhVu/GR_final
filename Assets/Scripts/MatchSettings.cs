// Who controls player 2
public enum OpponentType
{
    Dummy,   // TrainingDummy: Idle / Attack / Block, switched with the number keys during the fight
    CPU,     // ScriptedCPU: the rule-based AI
}

// What the character select screen picked, carried over into the fight scene.
// A static class keeps its values when a new scene loads. Empty (null) when the fight scene is played directly.
public static class MatchSettings
{
    public static CharacterData p1;
    public static CharacterData p2;
    public static OpponentType p2Type = OpponentType.CPU;

    public static bool HasPicks => p1 != null && p2 != null;

    public static void Clear()
    {
        p1 = null;
        p2 = null;
        p2Type = OpponentType.CPU;
    }
}
