using TMPro;
using UnityEngine;

public class StatisticsManager : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI highscoreText;
    [SerializeField] TextMeshProUGUI currentScoreText;
    [SerializeField] TextMeshProUGUI gamesWonText;
    [SerializeField] TextMeshProUGUI gamesLostText;

    void Start()
    {
        LoadStats();
    }

    void LoadStats()
    {
        int highscore = PlayerPrefs.GetInt("Highscore");
        highscoreText.text = highscore.ToString();

        int currentScore = PlayerPrefs.GetInt("Score");
        currentScoreText.text = currentScore.ToString();

        int gamesWon = PlayerPrefs.GetInt("GamesWon");
        gamesWonText.text = gamesWon.ToString();

        int gamesLost = PlayerPrefs.GetInt("GamesLost");
        gamesLostText.text = gamesLost.ToString();
    }

    public void DeleteStats()
    {
        PlayerPrefs.DeleteKey("Highscore");
        PlayerPrefs.DeleteKey("Score");
        PlayerPrefs.DeleteKey("GamesWon");
        PlayerPrefs.DeleteKey("GamesLost");

        LoadStats();
    }
}
