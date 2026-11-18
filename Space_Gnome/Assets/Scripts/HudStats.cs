using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Player feedback (2026-11-18, Tim: more feedback without overloading the HUD). Built at runtime
// by UI_Manager, so UI 2.prefab is not edited; once the look is settled it can become prefab objects.
//
// Readout under BOOST:
//   COMBO x3    coin multiplier from chained dance moves (1-5; resets on landing / coin pickup)
//   FALL  87    distance fallen since he last stood: white, yellow from 70% of the damage
//               threshold (150), red once a landing would hurt
//   SPEED 39    current fall speed (units/s; nose dive shows ~2.5x)
//   FLOORS 4    dance floors the current boost pays for (boost / 1500); red at 0
//
// Dance move names: when a move starts (or a basic move adds a combo step) its name pops up above
// the gnome, rises and fades. Names come from the animation clip file names (Gnome-TPose-NoBones@...),
// editable in the Inspector.
public class HudStats : MonoBehaviour
{
    [Header("Readout")]
    [Tooltip("Top-left corner, same centre-anchored space as the other HUD texts. BOOST's label sits at (-561, 335).")]
    public Vector2 position = new Vector2(-661f, 305f);
    public float fontSize = 28f;
    public Color warnColor = new Color(1f, 0.85f, 0.3f);
    public Color dangerColor = new Color(1f, 0.35f, 0.3f);
    [Tooltip("Seconds of smoothing on the speed number, so it doesn't flicker.")]
    public float speedSmoothing = 0.2f;

    [Header("Dance move names")]
    public float nameFontSize = 40f;
    [Tooltip("Screen pixels above the gnome's position.")]
    public float nameHeight = 140f;
    public float nameRise = 60f, nameHold = 0.6f, nameFade = 0.5f;
    public Color nameColor = new Color(0.55f, 0.9f, 1f);
    // From the clip files on each Animator state (Gnome_Controller, Base Layer). "Dancing" is the
    // RockDancing clip's own name; renamed "Rock Dancing" here since "Dancing" alone says little.
    public string breakDance1990 = "Breakdance 1990";
    public string headspin = "Breakdance Headspin";
    public string uprock = "Brooklyn Uprock";
    public string rockDancing = "Rock Dancing";
    public string flair = "Flair";
    public string hipHop = "Hip Hop Dancing";
    public string silly = "Silly Dancing";
    public string ending = "Breakdance Ending";

    TextMeshProUGUI text, moveName;
    RectTransform parentRect, moveRect;
    float lastY, speed, moveShownAt = -99f;
    bool haveLastY;
    FallDamage fall;
    Platforms platforms;
    int lastCombo;
    readonly Dictionary<string, bool> wasOn = new Dictionary<string, bool>();

    void Start()
    {
        var ui = Toolbox.instance.m_uIManager;
        Transform parent = ui != null && ui.scoreText != null ? ui.scoreText.transform.parent : transform;
        parentRect = parent as RectTransform;
        var style = Toolbox.instance.m_playerManager != null ? Toolbox.instance.m_playerManager.boostText : null;

        text = MakeText("HudStats (runtime)", parent, style, fontSize, new Vector2(0f, 1f), TextAlignmentOptions.TopLeft);
        ((RectTransform)text.transform).anchoredPosition = position;
        ((RectTransform)text.transform).sizeDelta = new Vector2(360f, 180f);

        moveName = MakeText("DanceMoveName (runtime)", parent, style, nameFontSize, new Vector2(0.5f, 0f), TextAlignmentOptions.Bottom);
        moveRect = (RectTransform)moveName.transform;
        moveRect.sizeDelta = new Vector2(700f, 80f);
        moveName.color = nameColor;
        moveName.enabled = false;
    }

    static TextMeshProUGUI MakeText(string name, Transform parent, TMP_Text style, float size, Vector2 pivot, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = pivot;
        var t = go.AddComponent<TextMeshProUGUI>();
        if (style != null) { t.font = style.font; t.fontSharedMaterial = style.fontSharedMaterial; t.color = style.color; }
        t.fontSize = size;
        t.alignment = align;
        t.enableWordWrapping = false;
        t.raycastTarget = false;
        return t;
    }

    void LateUpdate()
    {
        if (text == null) return;
        var ui = Toolbox.instance.m_uIManager;
        var gm = Toolbox.instance.m_gnomeMovement;
        bool show = ui != null && ui.State == UI_Manager.MenuState.Playing && gm != null && gm.isActiveAndEnabled;
        text.enabled = show;
        if (!show) { haveLastY = false; moveName.enabled = false; return; }

        if (fall == null) fall = gm.GetComponent<FallDamage>();
        if (platforms == null) platforms = gm.GetComponent<Platforms>();

        UpdateReadout(gm);
        UpdateMoveName(gm);
    }

    void UpdateReadout(GnomeMovement gm)
    {
        // Fall speed from how far he actually moved down this frame (covers nose dive, boost, landing).
        float y = gm.transform.position.y;
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        float raw = haveLastY ? Mathf.Max(0f, (lastY - y) / dt) : 0f;
        speed = Mathf.Lerp(speed, raw, Mathf.Clamp01(dt / Mathf.Max(0.01f, speedSmoothing)));
        lastY = y;
        haveLastY = true;

        int combo = Mathf.Clamp(gm.comboCount, 1, 5);
        string fallLine = "FALL  -";
        if (fall != null)
        {
            float f = fall.CurrentFall;
            string c = f >= fall.SafeFall ? Hex(dangerColor) : f >= fall.SafeFall * 0.7f ? Hex(warnColor) : null;
            fallLine = "FALL  " + Colored(f.ToString("0"), c);
        }
        string floorsLine = "";
        var pm = Toolbox.instance.m_playerManager;
        if (platforms != null && pm != null && platforms.BoostCost > 0f)
        {
            int n = Mathf.FloorToInt(pm.boost / platforms.BoostCost);
            floorsLine = "\nFLOORS " + Colored(n.ToString(), n == 0 ? Hex(dangerColor) : null);
        }

        text.text = "COMBO " + Colored("x" + combo, gm.comboCount > 1 ? Hex(warnColor) : null) + "\n" +
                    fallLine + "\n" +
                    "SPEED " + speed.ToString("0") +
                    floorsLine;
    }

    void UpdateMoveName(GnomeMovement gm)
    {
        // A move that just switched on, or a basic move that added a combo step (repeating the
        // same move keeps its flag on, but still counts).
        string started = null;
        Check("flair", gm.isFlair, flair, ref started);
        Check("hiphop", gm.isHipHopDancing, hipHop, ref started);
        Check("silly", gm.isSillyDancing, silly, ref started);
        Check("ending", gm.isBreakDanceEnding1, ending, ref started);
        Check("1990", gm.isBreakDance1990, breakDance1990, ref started);
        Check("headspin", gm.isHeadspin, headspin, ref started);
        Check("uprock", gm.isUprock, uprock, ref started);
        Check("rock", gm.isRockDancing, rockDancing, ref started);

        if (started == null && gm.comboCount > lastCombo) started = ActiveBasic(gm);
        lastCombo = gm.comboCount;

        if (started != null)
        {
            moveName.text = started + (gm.comboCount > 1 ? "  <size=75%>x" + Mathf.Min(gm.comboCount, 5) + "</size>" : "");
            moveShownAt = Time.time;
        }

        float age = Time.time - moveShownAt;
        if (age > nameHold + nameFade) { moveName.enabled = false; return; }
        moveName.enabled = true;

        // Follow the gnome on screen (overlay canvas: no camera needed for the conversion).
        var cam = Camera.main;
        if (cam != null && parentRect != null)
        {
            Vector3 sp = cam.WorldToScreenPoint(gm.transform.position);
            if (sp.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, sp, null, out Vector2 local))
                moveRect.anchoredPosition = local + Vector2.up * (nameHeight + nameRise * Mathf.Clamp01(age / (nameHold + nameFade)));
        }
        var c = nameColor;
        c.a = age <= nameHold ? 1f : 1f - (age - nameHold) / nameFade;
        moveName.color = c;
    }

    void Check(string key, bool on, string label, ref string started)
    {
        wasOn.TryGetValue(key, out bool was);
        if (on && !was && started == null) started = label;
        wasOn[key] = on;
    }

    string ActiveBasic(GnomeMovement gm) =>
        gm.isBreakDance1990 ? breakDance1990 : gm.isHeadspin ? headspin : gm.isUprock ? uprock : gm.isRockDancing ? rockDancing : null;

    static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
    static string Colored(string s, string hex) => hex == null ? s : "<color=" + hex + ">" + s + "</color>";
}
