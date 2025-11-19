using UnityEngine;
using TMPro;

public class ScoreboardManager : MonoBehaviour
{

    public static ScoreboardManager instance;

    public TextMeshProUGUI teamBlueScoreText;
    public TextMeshProUGUI teamPurpleScoreText;

    private int teamBlueScore = 0;
    private int teamPurpleScore = 0;

    private void Awake()
    {
        // Singleton pattern to ensure only one instance of ScoreboardManager exists
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        teamBlueScoreText.text = "Blue: 0";
        teamPurpleScoreText.text = "Purple: 0";
    }

    // Update is called when ball collides with goal
    public void AddPointBlue()
    {
        teamBlueScore++;
        teamBlueScoreText.text = "Blue: " + teamBlueScore.ToString();
    }

    public void AddPointPurple()
    {
        teamPurpleScore++;
        teamPurpleScoreText.text = "Purple: " + teamPurpleScore.ToString();
    }
}