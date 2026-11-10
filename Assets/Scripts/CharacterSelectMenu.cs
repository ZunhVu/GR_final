using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Character select screen: player 1 picks, then player 2 (the CPU) is picked and given a mode from a vertical list, then Start loads the fight.
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
    [Tooltip("A UI Button with a TMP text child. One is made per CPU mode.")]
    [SerializeField] private Button modeButtonPrefab;
    [Tooltip("Holds the mode buttons, e.g. an object with a Vertical Layout Group. Shown once both are picked.")]
    [SerializeField] private Transform modeContainer;
    [SerializeField] private Color selectedModeColor = new Color(0.83f, 0.7f, 0.12f);   // gold
    [SerializeField] private Button startButton;
    [SerializeField] private Button backButton;
    [Tooltip("How see-through Start and the character buttons are while they can't be pressed (0 = invisible, 1 = solid)")]
    [SerializeField, Range(0f, 1f)] private float unusableAlpha = 0.3f;

    private CharacterData p1, p2;
    private ScriptedCPU.Mode selectedMode = ScriptedCPU.Mode.Basic;
    private readonly List<Button> characterButtons = new List<Button>();
    private readonly List<Button> modeButtons = new List<Button>();
    private readonly List<ScriptedCPU.Mode> modeOfButton = new List<ScriptedCPU.Mode>();

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

        // One button per CPU mode, listed top to bottom in the container
        if (modeButtonPrefab != null && modeContainer != null)
        {
            foreach (ScriptedCPU.Mode m in System.Enum.GetValues(typeof(ScriptedCPU.Mode)))
            {
                Button b = Instantiate(modeButtonPrefab, modeContainer);
                b.name = m.ToString();
                TMP_Text label = b.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = m.ToString();
                ScriptedCPU.Mode mode = m;   // copy for the listener below
                b.onClick.AddListener(() => SelectMode(mode));
                modeButtons.Add(b);
                modeOfButton.Add(m);
            }
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
        MatchSettings.p2Mode = selectedMode;
        SceneManager.LoadScene(fightScene);
    }

    private void SelectMode(ScriptedCPU.Mode mode)
    {
        selectedMode = mode;
        Refresh();
    }

    // Updates the text, previews and which controls are shown for the current step
    private void Refresh()
    {
        bool bothPicked = p1 != null && p2 != null;
        if (instruction != null)
            instruction.text = p1 == null ? "Player 1: choose your fighter"
                             : p2 == null ? "Choose the CPU's fighter"
                             : p1.displayName + " vs " + p2.displayName;

        SetPreview(p1Preview, p1);
        SetPreview(p2Preview, p2);
        foreach (Button b in characterButtons) SetUsable(b, !bothPicked);   // faded once both are picked
        if (modeContainer != null) modeContainer.gameObject.SetActive(bothPicked);

        // The chosen mode's button turns gold, the others keep their normal colour
        for (int i = 0; i < modeButtons.Count; i++)
        {
            Graphic g = modeButtons[i].targetGraphic;
            if (g != null) g.color = modeOfButton[i] == selectedMode ? selectedModeColor : Color.white;
        }
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
