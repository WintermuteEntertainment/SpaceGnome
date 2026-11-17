using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class PlayerManager : MonoBehaviour
{
    [SerializeField] GameObject player;

    [Header("Basic Variables")]
    public int hP;
    public float time;
    public int points;
    public float score;
    [Header("Boost Variables")]
    public float boost;
    [SerializeField] float defaultBoost;
    [SerializeField] float defaultScore;
    public TMP_Text boostText;
    [SerializeField] float boostCap;

   public static HighScores m_highScores;

    public TMP_Text pointsCoinsCollectedText;
    public TMP_Text hPCoinsCollectedText;
    public TMP_Text timeCoinsCollectedText;


    private void Awake()
    {
        defaultScore = 0;
        boost = defaultBoost;
        score = defaultScore;
        
    }
    public void GameOver()
    { 
        player.SetActive(false);

        if (Toolbox.instance.m_highScores.newScore > (PlayerPrefs.GetFloat("HighScore")))
        {
            Toolbox.instance.m_highScores.previousHighScore = PlayerPrefs.GetFloat("HighScore");
            PlayerPrefs.SetFloat("PreviousHighScore", Toolbox.instance.m_highScores.previousHighScore);
            PlayerPrefs.SetFloat("HighScore", Toolbox.instance.m_highScores.newScore);
        }

        pointsCoinsCollectedText.text = Toolbox.instance.m_coins.totalPointsCoinsCollected.ToString();
        hPCoinsCollectedText.text = Toolbox.instance.m_coins.totalHealthCoinsCollected.ToString();
        timeCoinsCollectedText.text = Toolbox.instance.m_coins.totalTimeCoinsCollected.ToString();

        if (Toolbox.instance.m_uIManager.finalScoreText.text != null) 
        {
            Toolbox.instance.m_uIManager.finalScoreText.text = score.ToString("f0");
        }


        Toolbox.instance.m_uIManager.gameOverPanel.SetActive(true);
        Toolbox.instance.m_uIManager.highScoreText.text = PlayerPrefs.GetFloat("HighScore").ToString("f0");
        Toolbox.instance.m_uIManager.previousHighScoreText.text = PlayerPrefs.GetFloat("PreviousHighScore").ToString("f0");
        Debug.Log("Game over, man!");
        Debug.Log("Score was " + score + " at Game Over.");
       
    }
    private void Update()
    {
        hP = Toolbox.instance.m_coins.hP;
        time = Toolbox.instance.m_coins.time;
        points = Toolbox.instance.m_coins.points;

        boostText.text = boost.ToString();
        if (boost >= boostCap)
        {
            boost = boostCap;
        }
        if (boost<= 0) { boost= 0; }

        score = ComputeScore();
    }

    [Header("Score (2026)")]
    [Tooltip("Round-length bonus: score is multiplied by 1 + (seconds survived / this). " +
             "At 60, a 1-minute round is x2 and a 3-minute round is x4.")]
    [SerializeField] float secondsPerExtraMultiplier = 60f;

    // Score = points x health x round-length multiplier (Tim, 2026-11-17).
    // 2023 formula was (points + hP/2) * (timeRoundStarted - Time.time), which went negative as
    // the round went on. Health counts as at least 1, so a round that ends with 0 HP still scores
    // its points (with HP 0 the product would otherwise always be 0).
    public float ComputeScore()
    {
        float seconds = Mathf.Max(0f, Time.time - Toolbox.instance.m_uIManager.timeRoundStarted);
        float roundMultiplier = 1f + seconds / Mathf.Max(1f, secondsPerExtraMultiplier);
        return points * Mathf.Max(hP, 1) * roundMultiplier;
    }
}
