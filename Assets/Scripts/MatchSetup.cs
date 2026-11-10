using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Builds the match when the fight scene starts: spawns both characters and wires up everything
// that used to be dragged in by hand (opponents, walls, controllers, health bars, round manager).
public class MatchSetup : MonoBehaviour
{
    [Header("who fights (used when the scene is played without the select screen)")]
    [SerializeField] private CharacterData defaultP1;
    [SerializeField] private CharacterData defaultP2;
    [SerializeField] private ScriptedCPU.Mode defaultP2Mode = ScriptedCPU.Mode.Basic;

    [Header("scene")]
    [SerializeField] private Transform p1Spawn;
    [SerializeField] private Transform p2Spawn;
    [SerializeField] private Collider2D leftWall;
    [SerializeField] private Collider2D rightWall;
    [SerializeField] private HealthBar p1HealthBar;
    [SerializeField] private HealthBar p2HealthBar;
    [SerializeField] private RoundManager roundManager;
    [SerializeField] private InputActionAsset playerActions;

    [Header("menu")]
    [SerializeField] private string menuScene = "CharacterSelect";   // Escape goes back here

    public Fighter P1 { get; private set; }
    public Fighter P2 { get; private set; }

    private void Awake()
    {
        CharacterData c1 = MatchSettings.HasPicks ? MatchSettings.p1 : defaultP1;
        CharacterData c2 = MatchSettings.HasPicks ? MatchSettings.p2 : defaultP2;
        ScriptedCPU.Mode mode = MatchSettings.HasPicks ? MatchSettings.p2Mode : defaultP2Mode;
        if (c1 == null || c2 == null)
        {
            Debug.LogError("MatchSetup: no characters picked and no defaults set");
            return;
        }

        P1 = Spawn(c1, p1Spawn, true, "P1");
        P2 = Spawn(c2, p2Spawn, false, "P2");
        if (P1 == null || P2 == null) return;

        P1.SetOpponent(P2);
        P2.SetOpponent(P1);

        // Player 1: human. Player 2: CPU.
        SetEnabled<ScriptedCPU>(P1, false);
        PlayerController human = GetOrAdd<PlayerController>(P1);
        human.enabled = true;
        human.Init(playerActions);
        P1.SetController(human);

        SetEnabled<PlayerController>(P2, false);
        ScriptedCPU cpu = GetOrAdd<ScriptedCPU>(P2);
        cpu.enabled = true;
        cpu.Init(P1, mode);
        P2.SetController(cpu);

        // Mirror match: player 2 gets the alternate colours
        ColorPalette p2Palette = c1 == c2 ? c2.altPalette : null;
        SpriteRecolor recolor = P2.GetComponent<SpriteRecolor>();
        if (p2Palette != null && recolor == null) recolor = P2.gameObject.AddComponent<SpriteRecolor>();
        if (recolor != null) recolor.SetPalette(p2Palette);
        SpriteRecolor p1Recolor = P1.GetComponent<SpriteRecolor>();
        if (p1Recolor != null) p1Recolor.SetPalette(null);

        if (p1HealthBar != null) p1HealthBar.SetTarget(P1.FighterHealth, c1.portrait);
        if (p2HealthBar != null) p2HealthBar.SetTarget(P2.FighterHealth, c2.portrait);
        if (roundManager != null) roundManager.SetFighters(P1, P2);
    }

    private Fighter Spawn(CharacterData character, Transform spawn, bool faceRight, string label)
    {
        if (character.prefab == null)
        {
            Debug.LogError("MatchSetup: " + character.displayName + " has no prefab");
            return null;
        }
        Vector3 pos = spawn != null ? spawn.position : Vector3.zero;
        GameObject go = Instantiate(character.prefab, pos, Quaternion.identity);
        go.name = label + " " + character.displayName;

        Fighter fighter = go.GetComponent<Fighter>();
        if (fighter == null)
        {
            Debug.LogError("MatchSetup: the prefab of " + character.displayName + " has no Fighter on its root");
            return null;
        }
        fighter.Setup(character);
        fighter.SetStage(leftWall, rightWall);
        fighter.SetStart(pos, faceRight);
        return fighter;
    }

    private static T GetOrAdd<T>(Component c) where T : Component =>
        c.GetComponent<T>() != null ? c.GetComponent<T>() : c.gameObject.AddComponent<T>();

    private static void SetEnabled<T>(Component c, bool on) where T : Behaviour
    {
        T b = c.GetComponent<T>();
        if (b != null) b.enabled = on;
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame
            && Application.CanStreamedLevelBeLoaded(menuScene))
            SceneManager.LoadScene(menuScene);
    }
}
