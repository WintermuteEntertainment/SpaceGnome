using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using DG.Tweening;

public class Coins : MonoBehaviour
{
    [Header("Main Coin Variables")]

    public int points;
    public int hP;
    public float time;
    public float totalTime;

    public bool gotAPoint;
    public bool gotAHP;
    public bool gotATime;

    [Header("Time Coin Bonuses")]
    [SerializeField] float timeBonus; //Bonuses awarded for picking up time coin.
    [SerializeField] float timeBonus2;
    [SerializeField] float timeBonus3;

    [SerializeField] int danceBonus; //Total Dance Combos executed (1-4), determines coin pickup bonus.

    [Header("Text Objects")]
    [SerializeField] TMP_Text pointsText;
    [SerializeField] TMP_Text hPText;
    [SerializeField] TMP_Text totalTimeCoinsCollectedText;
    [SerializeField] TMP_Text totalHealthCoinsCollectedText;
    [SerializeField] TMP_Text totalPointsCoinsCollectedText;

    [Header("Statistics")]
    public float roundTotalTime;
    public int totalTimeCoinsCollected;
    public int totalHealthCoinsCollected;
    public int totalPointsCoinsCollected;
    [Tooltip("Coins that existed this round: active Coin1-3 objects in the scene at start + every coin the Spawner creates (2026-11-18).")]
    public int coinsSpawned;
    public int CoinsCollected => totalPointsCoinsCollected + totalHealthCoinsCollected + totalTimeCoinsCollected;

    public GnomeMovement m_gnomeMovement;

    [Header("Active Combo Level")]
    bool combo1;
    bool combo2;
    bool combo3;
    bool combo4;
    bool combo5;

    public IEnumerator waitOneSecond;

    [Header("Coin Pick Up Bonus Sprite GOs")]
    // Reference to the plus1, plus2, plus3, plus4, and plus5 GameObjects in the scene
    public GameObject plus1; 
    public GameObject plus2; 
    public GameObject plus3; 
    public GameObject plus4; 
    public GameObject plus5;

    [Header("Coin Pickup Type GOs")]
    // Reference to the UI elements for points, hP, and time coins
    public GameObject pointsCoinTextObject;
    public GameObject hPCoinTextObject;
    public GameObject timeCoinTextObject;
    
    private void Start()
    {
        points = 0;
        danceBonus = 0;

        // Coins already placed in the scene count as part of the level (GYM_03 has one of each).
        // FindGameObjectsWithTag only returns active objects, so inactive templates are skipped.
        coinsSpawned = 0;
        foreach (var tag in new[] { "Coin1", "Coin2", "Coin3" }) coinsSpawned += GameObject.FindGameObjectsWithTag(tag).Length;

        // Deactivate all the plus GameObjects at the start
        plus1.SetActive(false);
        plus2.SetActive(false);
        plus3.SetActive(false);
        plus4.SetActive(false);
        plus5.SetActive(false);

        // Deactivate all the UI elements at the start
        pointsCoinTextObject.SetActive(false);
        hPCoinTextObject.SetActive(false);
        timeCoinTextObject.SetActive(false);
    }
    [Header("+N number display (2026)")]
    [SerializeField] float plusHoldSeconds = 1f;
    [SerializeField] float plusFadeSeconds = 0.5f;

    // Show exactly one +N number: hide the others, then hold `amount`'s number for plusHoldSeconds
    // and fade it out. 2023 bugs this replaces: the Time coin branch never faded/hid its number
    // (code commented out), and DOTween.Kill(plusN) targeted the GameObjects while the fades ran on
    // their SpriteRenderers, so old fades kept running and numbers overlapped.
    void ShowPlus(int amount)
    {
        GameObject[] all = { plus1, plus2, plus3, plus4, plus5 };
        foreach (var go in all)
        {
            if (go == null) continue;
            var r = go.GetComponent<SpriteRenderer>();
            if (r != null) r.DOKill();
            go.SetActive(false);
        }
        if (amount < 1) return;

        var shown = all[Mathf.Clamp(amount, 1, 5) - 1];
        if (shown == null) return;
        shown.SetActive(true);
        var sr = shown.GetComponent<SpriteRenderer>();
        if (sr == null) return;
        var c = sr.color; c.a = 1f; sr.color = c;
        sr.DOFade(0f, plusFadeSeconds).SetDelay(plusHoldSeconds).OnComplete(() => shown.SetActive(false));
    }

    [Header("Coin rewards (2026)")]
    [Tooltip("Points per points coin, times the combo multiplier (1-5): 1000-5000.")]
    [SerializeField] int pointsPerCoin = 1000;
    [Tooltip("HP per health coin, times the combo multiplier (1-5).")]
    [SerializeField] int hpPerCoin = 1;
    [Tooltip("Seconds the FIRST time coin of a round adds, times the combo multiplier.")]
    [SerializeField] float timeCoinBaseSeconds = 3f;
    [Tooltip("Each later time coin is worth this fraction of the one before (diminishing returns). " +
             "Total time a round can ever gain = base x multiplier / (1 - this): at 3 s and 0.85, " +
             "20 s at x1 and 100 s at x5, so a round cannot last forever.")]
    [SerializeField, Range(0.5f, 0.99f)] float timeCoinDecay = 0.85f;

    // 2026 rewrite of coin pickup. The 2023 version: points coins gave 1-5 points, health coins
    // added the combo bonus twice, and time coins added to Timer.timerTime, which the timer only
    // reads once at the start, so they never extended the round.
    private void OnCollisionEnter(Collision collision)
    {
        string coinTag = collision.gameObject.tag;
        if (coinTag != "Coin1" && coinTag != "Coin2" && coinTag != "Coin3") return;

        // Stop the same coin being collected twice before Destroy takes effect.
        if (collision.collider != null) collision.collider.enabled = false;

        // Combo bonus = number of dance moves chained (Tim, 2026-11-17): 3 moves = +3. At least +1
        // (no combo), at most +5. 2023/early-2026 used danceBonus + 1, so 3 moves gave x4.
        int multiplier = Mathf.Clamp(m_gnomeMovement != null ? m_gnomeMovement.comboCount : danceBonus, 1, 5);

        // Time coins show the exact seconds added under the timer instead of the +N sprite, since
        // their value shrinks with every time coin collected this round.
        if (coinTag != "Coin3") ShowPlus(multiplier); else ShowPlus(0);
        ShowCoinType(coinTag);

        switch (coinTag)
        {
            case "Coin1": // POINTS (GOLD)
                int pointsGained = pointsPerCoin * multiplier;
                points += pointsGained;
                totalPointsCoinsCollected += 1;
                gotAPoint = true;
                pointsText.text = points.ToString();
                Toolbox.instance.m_audio.pointsCoin.Play();
                Debug.Log("Points coin: +" + pointsGained + " (x" + multiplier + "). Points " + points + ".");
                break;

            case "Coin2": // HEALTH (SILVER)
                int hpGained = hpPerCoin * multiplier;
                hP += hpGained;
                totalHealthCoinsCollected += 1;
                gotAHP = true;
                hPText.text = hP.ToString();
                Toolbox.instance.m_audio.hPCoin.Play();
                Debug.Log("Health coin: +" + hpGained + " HP (x" + multiplier + "). HP " + hP + ".");
                break;

            case "Coin3": // TIME (BRONZE)
                float seconds = timeCoinBaseSeconds * Mathf.Pow(timeCoinDecay, totalTimeCoinsCollected) * multiplier;
                Toolbox.instance.m_timer.AddTime(seconds);
                ShowTimeGain(seconds, multiplier);
                totalTimeCoinsCollected += 1;
                gotATime = true;
                Toolbox.instance.m_audio.timeCoin.Play();
                Debug.Log("Time coin #" + totalTimeCoinsCollected + ": +" + seconds.ToString("0.0") + " s (x" + multiplier + ").");
                break;
        }

        Destroy(collision.gameObject);

        // Reset coin pickup bonuses from dance combos after collecting a coin.
        danceBonus = 0;
        m_gnomeMovement.comboCount = 0;
        ResetCombos();
    }

    // "+2.6 s" just under the timer: rises for a second, then fades out and is destroyed.
    void ShowTimeGain(float seconds, int multiplier)
    {
        var timerText = Toolbox.instance.m_timer != null ? Toolbox.instance.m_timer.TimerText : null;
        if (timerText == null) { Debug.Log("Time coin added " + seconds.ToString("0.0") + " s (no timer text to show it next to)."); return; }

        var go = new GameObject("TimeGainPopup", typeof(RectTransform));
        go.transform.SetParent(timerText.transform.parent, false);
        var rt = (RectTransform)go.transform;
        var src = timerText.rectTransform;
        rt.anchorMin = src.anchorMin;
        rt.anchorMax = src.anchorMax;
        rt.pivot = src.pivot;
        rt.sizeDelta = src.sizeDelta;
        rt.anchoredPosition = src.anchoredPosition + new Vector2(0f, -src.rect.height);

        var label = go.AddComponent<TextMeshProUGUI>();
        // TMP's default font: the timer's font (Damage Inc) is missing characters such as - and :
        label.font = TMP_Settings.defaultFontAsset != null ? TMP_Settings.defaultFontAsset : timerText.font;
        label.fontSize = timerText.fontSize * 0.8f;
        label.alignment = timerText.alignment;
        label.color = new Color(0.55f, 1f, 0.55f, 1f);
        label.raycastTarget = false;
        // Time coins have no +N sprite, so show the combo multiplier here when there is one.
        label.text = "+" + seconds.ToString("0.0") + " s" + (multiplier > 1 ? "  x" + multiplier : "");
        StartCoroutine(RiseFadeAndDestroy(label, 1f, 0.6f));
    }

    IEnumerator RiseFadeAndDestroy(TMP_Text label, float holdSeconds, float fadeSeconds)
    {
        float t = 0f;
        while (t < holdSeconds + fadeSeconds && label != null)
        {
            t += Time.unscaledDeltaTime;
            label.rectTransform.anchoredPosition += Vector2.up * 25f * Time.unscaledDeltaTime;
            if (t > holdSeconds)
            {
                var c = label.color;
                c.a = 1f - (t - holdSeconds) / fadeSeconds;
                label.color = c;
            }
            yield return null;
        }
        if (label != null) Destroy(label.gameObject);
    }

    // Pops the coin type label (points / health / time) the same way the 2023 code did.
    void ShowCoinType(string coinTag)
    {
        GameObject label = coinTag == "Coin1" ? pointsCoinTextObject : coinTag == "Coin2" ? hPCoinTextObject : timeCoinTextObject;
        if (label == null) return;
        label.SetActive(true);
        label.transform.DOScale(Vector3.one, 0.5f).From().OnComplete(() => label.SetActive(false));
        var sr = label.GetComponent<SpriteRenderer>();
        if (sr != null) sr.DOFade(1f, 0.5f).From();
    }

    private void OnCollisionExit(Collision collision)       //Reset coin collection bool for appropriate coin when exited collision with that coin type.
    {
        if (collision.gameObject.CompareTag("Coin1"))
        {
            gotAPoint = false;
        }
        if (collision.gameObject.CompareTag("Coin2"))
        {
            gotAHP = false;
        }
        if (collision.gameObject.CompareTag("Coin3"))
        {
            gotATime = false;
        }
    }
    private void Update()
    {
        if (points <= 0) { points = 0; }                //UI updates and game over conditions.
        time = Toolbox.instance.m_timer.startTime;
        hPText.text = hP.ToString();
        pointsText.text = points.ToString();
        if (hP <= 0) { Toolbox.instance.m_playerManager.GameOver(); }
       
        if (m_gnomeMovement.comboCount == 1)            //Depending on how many combos previously performed in GnomeMovement script, set the dance bonus in the next method.  Not sure why I don't do it more dfirectly instead of using two separate methods though.
        {
            combo1 = true;
            ApplyCombos();
        }
        else if (m_gnomeMovement.comboCount == 2)
        {
            combo2 = true;
            ApplyCombos();
        }
        else if (m_gnomeMovement.comboCount == 3)
        {
            combo3 = true;
            ApplyCombos();
        }
        else if (m_gnomeMovement.comboCount == 4)
        {
            combo4 = true;
            ApplyCombos();
        }
        else if (m_gnomeMovement.comboCount == 5)
        {
            combo5 = true;
            ApplyCombos();
        }
        else { return; }

     
    }
    void ApplyCombos()
    {
        if (combo1)
        {
            danceBonus = 1;
           ResetCombos();
        }
        if (combo2)
        {
            danceBonus = 2;
            ResetCombos();
        }
        if (combo3)
        {
            danceBonus = 3;
            ResetCombos();
        }
        if (combo4)
        {
            danceBonus = 4;
            ResetCombos();
        }
        if (combo5)
        {
            danceBonus = 5;
            ResetCombos();
        }
    }
    // Clears the coin multiplier (called when the gnome lands, GDD 3.2.2).
    public void ResetCombo()
    {
        danceBonus = 0;
        ResetCombos();
    }

    void ResetCombos()
    {
        combo1 = false;
        combo2 = false;
        combo3 = false;
        combo4 = false;
        combo5 = false;
    }
}

