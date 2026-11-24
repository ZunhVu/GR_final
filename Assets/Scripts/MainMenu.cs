using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The title screen. Play opens a second page with the two modes:
//   Versus   → against the CPU     Training → against the dummy
// Both then go to the character select screen. Back (or Escape) returns to the first page. Exit closes the game.
// Put it on any object in the Menu scene and drag the buttons and the two pages in.
public class MainMenu : MonoBehaviour
{
    [Header("first page")]
    [Tooltip("Holds Play and Exit; hidden while the mode page is open. Optional: leave empty to keep them on screen.")]
    [SerializeField] private GameObject mainPage;
    [SerializeField] private Button playButton;
    [SerializeField] private Button exitButton;

    [Header("mode page (shown after Play)")]
    [Tooltip("Holds Versus, Training and Back. Hidden at the start.")]
    [SerializeField] private GameObject modePage;
    [SerializeField] private Button versusButton;
    [SerializeField] private Button trainingButton;
    [SerializeField] private Button backButton;

    [Tooltip("Must match the scene's name and be in File > Build Profiles > Scene List")]
    [SerializeField] private string characterSelectScene = "CharacterSelect";

    private int modesOpenedFrame = -1;   // the frame the mode page opened: a click or key press can't count twice

    private void Start()
    {
        if (playButton != null) playButton.onClick.AddListener(() => ShowModes(true));
        if (exitButton != null) exitButton.onClick.AddListener(Exit);
        if (versusButton != null) versusButton.onClick.AddListener(() => Play(GameMode.Versus));
        if (trainingButton != null) trainingButton.onClick.AddListener(() => Play(GameMode.Training));
        if (backButton != null) backButton.onClick.AddListener(() => ShowModes(false));
        if (modePage == gameObject || mainPage == gameObject)
            Debug.LogWarning("MainMenu: put this script on an object that stays visible, not on a page it hides");
        if (playButton != null && (playButton == versusButton || playButton == trainingButton || playButton == backButton))
            Debug.LogError("MainMenu: Play Button is also dragged into Versus / Training / Back. Drag the buttons from ModePage into those fields");
        if (modePage != null && mainPage != null && modePage.transform.IsChildOf(mainPage.transform))
            Debug.LogError("MainMenu: ModePage is inside MainPage, so it is hidden with it. Move it out, directly under the Canvas");
        if (modePage != null && playButton != null && playButton.transform.IsChildOf(modePage.transform))
            Debug.LogError("MainMenu: Play Button is a button inside ModePage. Drag the Play button from MainPage instead");
        ShowModes(false);
    }

    private void Update()
    {
        // Escape on the mode page goes back, like the Back button
        if (modePage != null && modePage.activeSelf && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            ShowModes(false);
    }

    // Switches between the first page (Play / Exit) and the mode page (Versus / Training / Back)
    private void ShowModes(bool show)
    {
        if (show) modesOpenedFrame = Time.frameCount;
        if (modePage != null) modePage.SetActive(show);
        if (mainPage != null) mainPage.SetActive(!show);

        // Select the first button of the page, so the keyboard (arrows + Enter) works too
        Button first = show ? versusButton : playButton;
        if (first != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
    }

    private void Play(GameMode mode)
    {
        if (Time.frameCount <= modesOpenedFrame + 1) return;   // the same click / Enter that opened the page
        MatchSettings.mode = mode;
        MatchSettings.ClearPicks();   // start with fresh picks
        SceneManager.LoadScene(characterSelectScene);
    }

    private void Exit()
    {
#if UNITY_EDITOR
        // Application.Quit does nothing in the editor, so stop Play mode instead
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
