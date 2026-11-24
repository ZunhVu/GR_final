using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The menu shown when a match is over: Retry starts a new match with the same fighters, Quit goes back to the main menu.
// Put it on the Canvas (or any always-active object), make a panel holding the two buttons, and drag them in.
// The panel is hidden during the fight and shown once the winner has been announced.
public class MatchEndMenu : MonoBehaviour
{
    [SerializeField] private RoundManager roundManager;
    [Tooltip("The object holding the buttons (and maybe a background). Shown and hidden by this script.")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button quitButton;
    [Tooltip("Scene Quit loads. Must be in File > Build Profiles > Scene List")]
    [SerializeField] private string quitScene = "Menu";

    private bool shown;

    private void Awake()
    {
        if (roundManager == null) roundManager = FindAnyObjectByType<RoundManager>();
        if (retryButton != null) retryButton.onClick.AddListener(Retry);
        if (quitButton != null) quitButton.onClick.AddListener(Quit);
        if (panel == gameObject)
            Debug.LogWarning("MatchEndMenu: put this script on the Canvas, not on the panel it hides " +
                             "(a hidden object stops running its scripts, so the menu would never come back)");
        SetShown(false);
    }

    private void Update()
    {
        bool show = roundManager != null && roundManager.WaitingForRetry;
        if (show != shown) SetShown(show);
    }

    private void SetShown(bool show)
    {
        shown = show;
        if (panel != null) panel.SetActive(show);

        // Select Retry so the menu also works with the keyboard (arrows + Enter / Space)
        if (show && retryButton != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(retryButton.gameObject);
    }

    private void Retry()
    {
        if (roundManager != null) roundManager.StartMatch();   // same fighters, score back to 0 - 0
        SetShown(false);
    }

    private void Quit()
    {
        if (Application.CanStreamedLevelBeLoaded(quitScene)) SceneManager.LoadScene(quitScene);
        else Debug.LogError("MatchEndMenu: scene \"" + quitScene + "\" is not in the Build Profiles scene list");
    }
}
