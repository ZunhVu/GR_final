using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Shows the round timer, the rounds each player has won, and the big centre text (ROUND 1, FIGHT!, K.O., P1 WINS).
// Everything is optional: leave a field empty and that part is just not shown.
public class RoundHUD : MonoBehaviour
{
    [SerializeField] private RoundManager roundManager;

    [Header("timer")]
    [SerializeField] private TMP_Text timerText;                     // "99"
    [SerializeField] private Color timerColor = Color.white;
    [SerializeField] private Color timerLowColor = new Color(0.9f, 0.2f, 0.2f);
    [SerializeField] private int lowTimeSeconds = 10;                // turns red at 10 and below

    [Header("rounds won")]
    [Tooltip("One Image per round needed to win (2 for first to 2). Filled in from the left as rounds are won.")]
    [SerializeField] private Image[] p1RoundMarkers;
    [SerializeField] private Image[] p2RoundMarkers;
    [SerializeField] private Color markerWonColor = new Color(0.83f, 0.7f, 0.12f);   // gold
    [SerializeField] private Color markerEmptyColor = new Color(1f, 1f, 1f, 0.25f);

    [Header("announcement")]
    [SerializeField] private TMP_Text announcementText;              // big text in the middle of the screen

    private void Awake()
    {
        if (roundManager == null) roundManager = FindAnyObjectByType<RoundManager>();

        // The announcement is always centred in its box, whatever the Inspector says
        if (announcementText != null)
        {
            announcementText.alignment = TextAlignmentOptions.Center;
            announcementText.textWrappingMode = TextWrappingModes.NoWrap;   // "DOUBLE K.O." stays on one line
        }
    }

    private void Update()
    {
        if (roundManager == null) return;

        if (timerText != null)
        {
            int t = roundManager.TimeLeftSeconds;
            timerText.text = t.ToString();
            timerText.color = t <= lowTimeSeconds ? timerLowColor : timerColor;
        }

        SetMarkers(p1RoundMarkers, roundManager.Wins(0));
        SetMarkers(p2RoundMarkers, roundManager.Wins(1));

        if (announcementText != null)
        {
            string a = roundManager.Announcement;
            announcementText.text = a;
            announcementText.enabled = !string.IsNullOrEmpty(a);
        }
    }

    private void SetMarkers(Image[] markers, int won)
    {
        if (markers == null) return;
        for (int i = 0; i < markers.Length; i++)
            if (markers[i] != null) markers[i].color = i < won ? markerWonColor : markerEmptyColor;
    }
}
