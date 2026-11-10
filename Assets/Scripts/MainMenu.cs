using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The title screen: Play opens the character select screen, Exit closes the game.
// Put it on any object in the Menu scene and drag the two buttons in.
public class MainMenu : MonoBehaviour
{
    [SerializeField] private Button playButton;
    [SerializeField] private Button exitButton;
    [Tooltip("Must match the scene's name and be in File > Build Profiles > Scene List")]
    [SerializeField] private string characterSelectScene = "CharacterSelect";

    private void Start()
    {
        if (playButton != null) playButton.onClick.AddListener(Play);
        if (exitButton != null) exitButton.onClick.AddListener(Exit);
    }

    private void Play()
    {
        MatchSettings.Clear();   // start the next match with fresh picks
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
