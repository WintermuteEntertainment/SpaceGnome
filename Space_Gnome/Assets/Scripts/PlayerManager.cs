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
    bool isGameOver;

    public void GameOver()
    {
        // 2026: runs once (Coins called it every frame while HP was 0), freezes the score, and
        // saves the high score explicitly. 2023 never called PlayerPrefs.Save(), compared a score
        // that kept growing, and old negative scores from the previous formula could sit in the
        // store (the Editor registry held -2.0).
        if (isGameOver) return;
        isGameOver = true;

        score = ComputeScore();
        Debug.Log("Final score: " + ScoreBreakdown());
        if (Toolbox.instance.m_highScores != null) Toolbox.instance.m_highScores.newScore = score;

        float best = Mathf.Max(0f, PlayerPrefs.GetFloat("HighScore", 0f));
        if (score > best)
        {
            PlayerPrefs.SetFloat("PreviousHighScore", best);
            PlayerPrefs.SetFloat("HighScore", score);
            PlayerPrefs.Save();
            if (Toolbox.instance.m_highScores != null)
            {
                Toolbox.instance.m_highScores.previousHighScore = best;
                Toolbox.instance.m_highScores.currentHighScore = score;
            }
            Debug.Log("New high score " + score.ToString("f0") + " (previous " + best.ToString("f0") + "), saved.");
        }
        else
        {
            Debug.Log("Score " + score.ToString("f0") + " is not a high score (best " + best.ToString("f0") + ").");
        }

        player.SetActive(false);

        pointsCoinsCollectedText.text = Toolbox.instance.m_coins.totalPointsCoinsCollected.ToString();
        hPCoinsCollectedText.text = Toolbox.instance.m_coins.totalHealthCoinsCollected.ToString();
        timeCoinsCollectedText.text = Toolbox.instance.m_coins.totalTimeCoinsCollected.ToString();

        if (Toolbox.instance.m_uIManager.finalScoreText.text != null)
        {
            Toolbox.instance.m_uIManager.finalScoreText.text = score.ToString("N0");
        }
        ShowCoinsRecovered();


        Toolbox.instance.m_uIManager.gameOverPanel.SetActive(true);
        Toolbox.instance.m_uIManager.highScoreText.text = Mathf.Max(0f, PlayerPrefs.GetFloat("HighScore")).ToString("N0");
        Toolbox.instance.m_uIManager.previousHighScoreText.text = Mathf.Max(0f, PlayerPrefs.GetFloat("PreviousHighScore")).ToString("N0");
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

        if (!isGameOver) score = ComputeScore(); // frozen once the round is over
    }

    // "Coins recovered: 12 / 40 (30%)" under the final score (2026-11-18). A copy of the final
    // score text (same font, style, parent), moved down by its own height; built once, at runtime,
    // so UI 2.prefab is not edited. Nudge it with 'coinsRecoveredOffset' if it overlaps.
    [Tooltip("Position of the coins-recovered line relative to the final score text.")]
    [SerializeField] Vector2 coinsRecoveredOffset = new Vector2(0f, -70f);
    [SerializeField] float coinsRecoveredFontScale = 0.7f;
    TMP_Text coinsRecoveredText;

    void ShowCoinsRecovered()
    {
        var coins = Toolbox.instance.m_coins;
        var final = Toolbox.instance.m_uIManager.finalScoreText;
        if (coins == null) return;
        int got = coins.CoinsCollected, total = Mathf.Max(coins.coinsSpawned, got); // never over 100%
        float pct = total > 0 ? 100f * got / total : 0f;
        string line = "Coins recovered: " + got + " / " + total + " (" + pct.ToString("0") + "%)";
        Debug.Log(line + " [points " + coins.totalPointsCoinsCollected + ", health " + coins.totalHealthCoinsCollected +
                  ", time " + coins.totalTimeCoinsCollected + "; spawned " + coins.coinsSpawned + "]");
        if (final == null) return;

        if (coinsRecoveredText == null)
        {
            var go = Instantiate(final.gameObject, final.transform.parent);
            go.name = "CoinsRecoveredText (runtime)";
            coinsRecoveredText = go.GetComponent<TMP_Text>();
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = ((RectTransform)final.transform).anchoredPosition + coinsRecoveredOffset;
            rt.sizeDelta = new Vector2(Mathf.Max(rt.sizeDelta.x, 700f), rt.sizeDelta.y);
            coinsRecoveredText.fontSize = final.fontSize * coinsRecoveredFontScale;
            coinsRecoveredText.enableWordWrapping = false;
        }
        coinsRecoveredText.text = line;
    }

    // Brief red flash on the boost number, e.g. when a platform is refused for lack of boost.
    public void FlashBoostWarning()
    {
        if (boostText == null) return;
        StopCoroutine(nameof(BoostWarning));
        StartCoroutine(nameof(BoostWarning));
    }

    IEnumerator BoostWarning()
    {
        if (!boostColorKnown) { boostNormalColor = boostText.color; boostColorKnown = true; }
        for (int i = 0; i < 3; i++)
        {
            boostText.color = Color.red;
            yield return new WaitForSecondsRealtime(0.12f);
            boostText.color = boostNormalColor;
            yield return new WaitForSecondsRealtime(0.08f);
        }
    }
    Color boostNormalColor;
    bool boostColorKnown;

    [Header("Score (2026)")]
    [Tooltip("Round-length bonus: score is multiplied by 1 + (seconds survived / this). " +
             "At 60, a 1-minute round is x2 and a 3-minute round is x4.")]
    [SerializeField] float secondsPerExtraMultiplier = 60f;

    // Score = points x health x round-length multiplier (Tim, 2026-11-17).
    // 2023 formula was (points + hP/2) * (timeRoundStarted - Time.time), which went negative as
    // the round went on. Health counts as at least 1, so a round that ends with 0 HP still scores
    // its points (with HP 0 the product would otherwise always be 0).
    // Every input to the score, for the log at game over (checking "the scores feel off").
    public string ScoreBreakdown()
    {
        float seconds = Mathf.Max(0f, Time.time - Toolbox.instance.m_uIManager.timeRoundStarted);
        float roundMultiplier = 1f + seconds / Mathf.Max(1f, secondsPerExtraMultiplier);
        return "points " + points.ToString("N0") + " x HP " + Mathf.Max(hP, 1) + (hP < 1 ? " (HP " + hP + " counted as 1)" : "") +
               " x time bonus " + roundMultiplier.ToString("0.00") + " (" + seconds.ToString("0.0") + " s, round started at t " +
               Toolbox.instance.m_uIManager.timeRoundStarted.ToString("0.0") + ") = " + ComputeScore().ToString("N0");
    }

    public float ComputeScore()
    {
        float seconds = Mathf.Max(0f, Time.time - Toolbox.instance.m_uIManager.timeRoundStarted);
        float roundMultiplier = 1f + seconds / Mathf.Max(1f, secondsPerExtraMultiplier);
        return points * Mathf.Max(hP, 1) * roundMultiplier;
    }
}
