using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Character select screen: player 1 picks, then the opponent's character (the CPU in Versus, the dummy in Training), then Start loads the fight.
// The mode itself was chosen on the main menu (MatchSettings.mode).
// Builds one button per character in the roster, so new characters show up without touching this script.
public class CharacterSelectMenu : MonoBehaviour
{
    [SerializeField] private CharacterRoster roster;
    [SerializeField] private string fightScene = "FightingScene";
    [Tooltip("Where Back goes when nothing is picked yet")]
    [SerializeField] private string menuScene = "Menu";

    [Header("UI")]
    [Tooltip("A UI Button. Its text is set to the character's name; a child Image named \"Portrait\" gets the portrait.")]
    [SerializeField] private Button characterButtonPrefab;
    [SerializeField] private Transform buttonContainer;    // e.g. an object with a Grid Layout Group
    [SerializeField] private TMP_Text instruction;         // "Player 1: choose your fighter" etc.
    [SerializeField] private Image p1Preview;              // optional
    [SerializeField] private Image p2Preview;              // optional
    [SerializeField] private Button startButton;
    [SerializeField] private Button backButton;
    [Tooltip("How see-through Start and the character buttons are while they can't be pressed (0 = invisible, 1 = solid)")]
    [SerializeField, Range(0f, 1f)] private float unusableAlpha = 0.3f;

    private CharacterData p1, p2;
    private readonly List<Button> characterButtons = new List<Button>();

    private void Start()
    {
        foreach (CharacterData c in roster.characters)
        {
            if (c == null) continue;
            Button b = Instantiate(characterButtonPrefab, buttonContainer);
            b.name = c.displayName;
            TMP_Text label = b.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = c.displayName;
            Transform portrait = b.transform.Find("Portrait");
            if (portrait != null && portrait.TryGetComponent(out Image img) && c.portrait != null) img.sprite = c.portrait;
            CharacterData picked = c;   // copy for the listener below
            b.onClick.AddListener(() => Pick(picked));
            characterButtons.Add(b);
        }

        if (startButton != null) startButton.onClick.AddListener(StartMatch);
        if (backButton != null) backButton.onClick.AddListener(Back);
        Refresh();
    }

    private void Pick(CharacterData c)
    {
        if (p1 == null) p1 = c;
        else if (p2 == null) p2 = c;
        Refresh();
    }

    private void Back()
    {
        // Undo the last pick; with nothing picked, leave for the title screen
        if (p2 != null) p2 = null;
        else if (p1 != null) p1 = null;
        else
        {
            SceneManager.LoadScene(menuScene);
            return;
        }
        Refresh();
    }

    private void StartMatch()
    {
        if (p1 == null || p2 == null) return;
        MatchSettings.p1 = p1;
        MatchSettings.p2 = p2;
        SceneManager.LoadScene(fightScene);
    }

    // Updates the text, previews and which controls are shown for the current step
    private void Refresh()
    {
        bool bothPicked = p1 != null && p2 != null;
        bool training = MatchSettings.mode == GameMode.Training;
        if (instruction != null)
            instruction.text = (training ? "TRAINING - " : "VERSUS - ") +
                               (p1 == null ? "choose your fighter"
                              : p2 == null ? (training ? "choose the dummy's fighter" : "choose the CPU's fighter")
                              : p1.displayName + " vs " + p2.displayName);

        SetPreview(p1Preview, p1);
        SetPreview(p2Preview, p2);
        foreach (Button b in characterButtons) SetUsable(b, !bothPicked);   // faded once both are picked
        SetUsable(startButton, bothPicked);
        SetUsable(backButton, true);   // always usable: undoes a pick, or returns to the menu
    }

    // Keeps the button on screen; when it can't be used yet it's faded out and ignores clicks
    private void SetUsable(Button button, bool usable)
    {
        if (button == null) return;
        button.interactable = usable;
        // A Canvas Group fades the whole button, its text included
        CanvasGroup group = button.GetComponent<CanvasGroup>();
        if (group == null) group = button.gameObject.AddComponent<CanvasGroup>();
        group.alpha = usable ? 1f : unusableAlpha;
    }

    private static void SetPreview(Image img, CharacterData c)
    {
        if (img == null) return;
        img.enabled = c != null && c.portrait != null;
        if (img.enabled) img.sprite = c.portrait;
    }
}
