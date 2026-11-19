using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using DG.Tweening;

// Start menu, pause and game over.
//
// 2026 rewrite. The 2023 version had one StartButton() toggle that flipped between "menu" and
// "playing" depending on whether Time.timeScale was 0 or 1, called from the Start button AND from
// every UI Click (left mouse button or gamepad Start) at any time. The scene also began with time
// running behind the menu, so the first press "paused" instead of starting, a mouse click fired
// it twice, and any later click into the Game view paused the game. Now there are explicit states.
public class UI_Manager : MonoBehaviour
{
    public enum MenuState { StartMenu, Playing, Paused, GameOver, Tutorial }

    [Header("Tutorial (2026)")]
    [Tooltip("Plays the DialogueUI's tutorial dialogue. Found automatically if empty.")]
    [SerializeField] DialogueUI dialogueUI;
    [Tooltip("Play the tutorial automatically when the first round of a session starts.")]
    [SerializeField] bool tutorialOnFirstRound = true;
    static bool tutorialShownThisSession;   // survives the scene reload on Restart
    MenuState stateAfterTutorial;
    InputAction replayTutorialAction;       // while paused: Y (North) or T replays the tutorial

    public TMP_Text scoreText;
    public TMP_Text finalScoreText;
    public TMP_Text previousHighScoreText;

    public TMP_Text highScoreText;

    public GameObject startPanel;     // used as the start menu and the pause menu
    public GameObject gameOverPanel;

    [SerializeField] GameObject gnome;

    [Tooltip("Button selected when the start/pause menu opens (keyboard and gamepad). Found by name 'StartButton' if empty.")]
    [SerializeField] Button startMenuFirstButton;
    [Tooltip("Button selected when the game over panel opens. Found by name 'RestartButton' if empty.")]
    [SerializeField] Button gameOverFirstButton;

    public SpriteRenderer plus1;
    public SpriteRenderer plus2;
    public SpriteRenderer plus3;
    public SpriteRenderer plus4;
    public SpriteRenderer plus5;

    public GameObject numbersParent;

    public SpriteRenderer healthUIText;
    public SpriteRenderer pointsUIText;
    public SpriteRenderer timeUIText;

    public SGInput input;

    public int round;
    public float timeRoundStarted;

    public MenuState State { get; private set; }

    // Menu input is handled here directly instead of relying only on the EventSystem, so starting
    // works however the UI module behaves. Every menu action below is safe to run twice.
    //   Pause:   Escape, or gamepad Start. Gamepad Start also starts from the start menu; Escape does not.
    //   Confirm: Enter / keypad Enter / gamepad A, presses the selected menu button (Start by default).
    //   Mouse:   a left click on a menu button presses it.
    InputAction pauseAction;
    InputAction confirmAction;
    bool restarting;

    private void Awake()
    {
        input = new SGInput();

        pauseAction = new InputAction("Pause", InputActionType.Button);
        pauseAction.AddBinding("<Keyboard>/escape");
        pauseAction.AddBinding("<Gamepad>/start");
        pauseAction.performed += ctx => OnPausePressed(ctx.control.device is Gamepad);

        confirmAction = new InputAction("Confirm", InputActionType.Button);
        confirmAction.AddBinding("<Keyboard>/enter");
        confirmAction.AddBinding("<Keyboard>/numpadEnter");
        confirmAction.AddBinding("<Gamepad>/buttonSouth");
        confirmAction.performed += ctx => OnConfirmPressed();

        replayTutorialAction = new InputAction("ReplayTutorial", InputActionType.Button);
        replayTutorialAction.AddBinding("<Keyboard>/t");
        replayTutorialAction.AddBinding("<Gamepad>/buttonNorth");
        replayTutorialAction.performed += ctx => { if (State == MenuState.Paused) PlayTutorial(MenuState.Paused); };

        if (dialogueUI == null) dialogueUI = FindObjectOfType<DialogueUI>(true);
        if (GetComponent<HudStats>() == null) gameObject.AddComponent<HudStats>(); // combo / fall readout (2026-11-18)
        if (GetComponent<SpeedLines>() == null) gameObject.AddComponent<SpeedLines>(); // falling speed lines (2026-11-18)

        if (startMenuFirstButton == null) startMenuFirstButton = FindButton(startPanel, "StartButton");
        if (gameOverFirstButton == null) gameOverFirstButton = FindButton(gameOverPanel, "RestartButton");

        EnterStartMenu();
    }

    void Start()
    {
        // EventSystem.current is not always set yet in Awake.
        if (State == MenuState.StartMenu) Select(startMenuFirstButton);
    }

    void OnEnable()
    {
        input.UI.Enable();
        pauseAction.Enable();
        confirmAction.Enable();
        replayTutorialAction.Enable();
    }

    void OnDisable()
    {
        input.UI.Disable();
        pauseAction.Disable();
        confirmAction.Disable();
        replayTutorialAction.Disable();
    }

    void OnDestroy()
    {
        pauseAction?.Dispose();
        confirmAction?.Dispose();
        replayTutorialAction?.Dispose();
    }

    public void Update()
    {
        // Whole number with separators. The 2023 line used ToString(), which prints large scores as
        // decimals or in E notation (e.g. 1.73742E+06).
        scoreText.text = Toolbox.instance.m_playerManager.score.ToString("N0");

        if (resetArmedUntil > 0f && Time.unscaledTime > resetArmedUntil) DisarmReset();

        // PlayerManager.GameOver() turns the game over panel on directly.
        if (State != MenuState.GameOver && gameOverPanel != null && gameOverPanel.activeInHierarchy)
            EnterGameOver();

        if (State == MenuState.GameOver && input.UI.Cancel.triggered)
            RestartButton();

        // Mouse: press the menu button under the pointer on release.
        var mouse = Mouse.current;
        if (State != MenuState.Playing && mouse != null && mouse.leftButton.wasReleasedThisFrame)
        {
            var button = ButtonUnderPointer(mouse.position.ReadValue());
            if (button != null) button.onClick.Invoke();
        }
    }

    // ------------------------------------------------------------------ states

    void EnterStartMenu()
    {
        State = MenuState.StartMenu;
        Time.timeScale = 0f;              // nothing runs behind the start menu (timer included)
        startPanel.SetActive(true);
        if (gnome != null) gnome.SetActive(false);
        Select(startMenuFirstButton);
    }

    // Show the tutorial with gameplay frozen (gnome visible, time stopped), then go to `after`.
    void PlayTutorial(MenuState after)
    {
        if (dialogueUI == null || dialogueUI.TutorialDialogue == null) { if (after == MenuState.Playing) StartPlaying(); return; }
        stateAfterTutorial = after;
        State = MenuState.Tutorial;
        Time.timeScale = 0f;
        startPanel.SetActive(false);
        if (gnome != null) gnome.SetActive(true);
        Select(null);
        dialogueUI.ShowDialogue(dialogueUI.TutorialDialogue, OnTutorialFinished);
    }

    void OnTutorialFinished()
    {
        tutorialShownThisSession = true;
        if (stateAfterTutorial == MenuState.Paused) { State = MenuState.Playing; Pause(); }
        else { State = MenuState.StartMenu; StartPlaying(); } // first round: start playing now
    }

    void StartPlaying()
    {
        // First round of the session: tutorial first, then play.
        if (State == MenuState.StartMenu && tutorialOnFirstRound && !tutorialShownThisSession
            && dialogueUI != null && dialogueUI.TutorialDialogue != null)
        {
            PlayTutorial(MenuState.Playing);
            return;
        }

        bool firstStart = State == MenuState.StartMenu;
        State = MenuState.Playing;
        startPanel.SetActive(false);
        if (gnome != null) gnome.SetActive(true);
        Time.timeScale = 1f;
        Select(null);                     // so Submit / A cannot press a hidden button
        if (firstStart) timeRoundStarted = Time.time;
    }

    void Pause()
    {
        State = MenuState.Paused;
        Time.timeScale = 0f;
        startPanel.SetActive(true);       // the gnome stays visible while paused
        Select(startMenuFirstButton);
    }

    void EnterGameOver()
    {
        State = MenuState.GameOver;
        startPanel.SetActive(false);
        Select(gameOverFirstButton);
    }

    void OnPausePressed(bool fromGamepad)
    {
        switch (State)
        {
            case MenuState.StartMenu: if (fromGamepad) StartPlaying(); break; // Escape does not start the game
            case MenuState.Playing: Pause(); break;
            case MenuState.Paused: StartPlaying(); break;
            case MenuState.GameOver: RestartButton(); break; // 2026: Start restarts (was only Select)
            case MenuState.Tutorial: break;                  // dialogue has its own Next / Skip
        }
    }

    void OnConfirmPressed()
    {
        if (State == MenuState.Playing) return; // A is Jump while playing

        var es = EventSystem.current;
        var selected = es != null && es.currentSelectedGameObject != null
            ? es.currentSelectedGameObject.GetComponent<Button>() : null;

        if (selected != null && selected.isActiveAndEnabled && selected.interactable) selected.onClick.Invoke();
        else if (State == MenuState.StartMenu || State == MenuState.Paused) StartPlaying();
        else if (State == MenuState.GameOver) RestartButton();
    }

    // ------------------------------------------------------------------ buttons (wired in the UI prefab)

    public void Quit()
    {
        Debug.Log("User quit.");
        Application.Quit();
    }

    // Start button: starts from the start menu, resumes from pause. Never pauses.
    public void StartButton()
    {
        Debug.Log("StartButton used.");
        if (State == MenuState.StartMenu || State == MenuState.Paused) StartPlaying();
    }

    // Two presses: the first arms it ("Press again to reset" for 3 s), the second resets.
    // 2023 deleted the keys only: no Save, no on-screen update, in-memory copies untouched, so it
    // looked like it did nothing.
    float resetArmedUntil = -1f;
    float resetArmedAt;
    TMP_Text resetLabel;
    string resetLabelOriginal;

    public void ResetHighScore()
    {
        if (resetLabel == null)
        {
            var b = FindButton(gameOverPanel, "ResetHighScoresButton");
            if (b != null && b.name == "ResetHighScoresButton") resetLabel = b.GetComponentInChildren<TMP_Text>(true);
        }

        if (resetArmedUntil < 0f || Time.unscaledTime > resetArmedUntil)
        {
            resetArmedAt = Time.unscaledTime;
            resetArmedUntil = Time.unscaledTime + 3f;
            if (resetLabel != null)
            {
                if (resetLabelOriginal == null) resetLabelOriginal = resetLabel.text;
                resetLabel.text = "Press again to reset";
            }
            Debug.Log("Reset high scores: press again within 3 s to confirm.");
            return;
        }
        // The same click can arrive twice in one frame (mouse handler + EventSystem): not a confirm.
        if (Time.unscaledTime - resetArmedAt < 0.25f) return;

        PlayerPrefs.DeleteKey("HighScore");
        PlayerPrefs.DeleteKey("PreviousHighScore");
        PlayerPrefs.Save();
        var hs = Toolbox.instance.m_highScores;
        if (hs != null) { hs.currentHighScore = 0f; hs.previousHighScore = 0f; }
        if (highScoreText != null) highScoreText.text = "0";
        if (previousHighScoreText != null) previousHighScoreText.text = "0";
        DisarmReset();
        Debug.Log("High scores reset (saved).");
    }

    void DisarmReset()
    {
        resetArmedUntil = -1f;
        if (resetLabel != null && resetLabelOriginal != null) resetLabel.text = resetLabelOriginal;
    }

    public void RestartButton()
    {
        if (restarting) return;           // may be pressed by both the EventSystem and Confirm
        restarting = true;
        Toolbox.instance.m_playerManager.score = 0;
        gameOverPanel.SetActive(false);
        Time.timeScale = 1f;
        SceneManager.LoadScene(0);        // Awake on the reloaded scene opens the start menu again
    }

    // ------------------------------------------------------------------ helpers

    Button ButtonUnderPointer(Vector2 screenPos)
    {
        GameObject panel = State == MenuState.GameOver ? gameOverPanel : startPanel;
        if (panel == null || !panel.activeInHierarchy) return null;
        foreach (var b in panel.GetComponentsInChildren<Button>(false))
        {
            if (!b.interactable) continue;
            var canvas = b.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)b.transform, screenPos, cam)) return b;
        }
        return null;
    }

    static Button FindButton(GameObject root, string name)
    {
        if (root == null) return null;
        foreach (var b in root.GetComponentsInChildren<Button>(true))
            if (b.name == name) return b;
        return root.GetComponentInChildren<Button>(true);
    }

    static void Select(Button button)
    {
        var es = EventSystem.current;
        if (es == null) return;
        es.SetSelectedGameObject(null);
        if (button != null) es.SetSelectedGameObject(button.gameObject);
    }
}
