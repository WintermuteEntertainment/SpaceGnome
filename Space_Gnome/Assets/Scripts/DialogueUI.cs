using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

// Shows a DialogueObject (a list of text lines) one line at a time with a typewriter effect.
//
// 2026 rewrite. The 2023 version showed testDialogue once when the scene loaded, waited for the
// Enter key only (old Input class, no gamepad), could not be skipped or replayed, and typed on game
// time, so it froze whenever the game was paused. Now UI_Manager decides when to play it:
//   Next line:  Enter / keypad Enter / gamepad A (finishes the current line first if still typing)
//   Skip all:   Backspace / gamepad B
// It runs on unscaled time, so it works while gameplay is frozen.
public class DialogueUI : MonoBehaviour
{
    [SerializeField] private TMP_Text textLabel;
    [Tooltip("The tutorial lines (a DialogueObject asset). Kept under its 2023 field name.")]
    [SerializeField] private DialogueObject testDialogue;
    [SerializeField] private GameObject dialogueBox;

    private TypeWriterEffect typeWriterEffect;
    private InputAction nextAction;
    private InputAction skipAction;
    private bool nextPressed;
    private bool skipPressed;
    private Action onFinished;

    public DialogueObject TutorialDialogue => testDialogue;
    public bool IsShowing { get; private set; }

    private void Awake()
    {
        typeWriterEffect = GetComponent<TypeWriterEffect>();

        nextAction = new InputAction("DialogueNext", InputActionType.Button);
        nextAction.AddBinding("<Keyboard>/enter");
        nextAction.AddBinding("<Keyboard>/numpadEnter");
        nextAction.AddBinding("<Gamepad>/buttonSouth");
        nextAction.performed += ctx => { if (IsShowing) nextPressed = true; };

        skipAction = new InputAction("DialogueSkip", InputActionType.Button);
        skipAction.AddBinding("<Keyboard>/backspace");
        skipAction.AddBinding("<Gamepad>/buttonEast");
        skipAction.performed += ctx => { if (IsShowing) skipPressed = true; };
    }

    private void OnEnable() { nextAction.Enable(); skipAction.Enable(); }
    private void OnDisable() { nextAction.Disable(); skipAction.Disable(); }
    private void OnDestroy() { nextAction?.Dispose(); skipAction?.Dispose(); }

    private void Start()
    {
        CloseDialogueBox();
    }

    // Play `dialogueObject`; `finished` runs when the last line is dismissed or the dialogue is skipped.
    public void ShowDialogue(DialogueObject dialogueObject, Action finished = null)
    {
        StopAllCoroutines();
        onFinished = finished;
        if (dialogueObject == null || dialogueObject.Dialogue == null || dialogueObject.Dialogue.Length == 0)
        {
            Finish();
            return;
        }
        if (dialogueBox != null) dialogueBox.SetActive(true);
        IsShowing = true;
        nextPressed = skipPressed = false;
        StartCoroutine(StepThroughDialogue(dialogueObject));
    }

    private IEnumerator StepThroughDialogue(DialogueObject dialogueObject)
    {
        foreach (string line in dialogueObject.Dialogue)
        {
            nextPressed = false;
            var typing = typeWriterEffect.RunTextCoRoutine(line, textLabel);

            // While typing: Next shows the whole line at once, Skip ends the dialogue.
            while (textLabel.text != line)
            {
                if (skipPressed) { Finish(); yield break; }
                if (nextPressed)
                {
                    typeWriterEffect.StopTyping(typing);
                    textLabel.text = line;
                    nextPressed = false;
                    break;
                }
                yield return null;
            }

            // Line complete: wait for Next (or Skip).
            while (!nextPressed && !skipPressed) yield return null;
            if (skipPressed) { Finish(); yield break; }
        }
        Finish();
    }

    private void Finish()
    {
        StopAllCoroutines();
        if (typeWriterEffect != null) typeWriterEffect.StopAllCoroutines(); // typing runs on that component
        CloseDialogueBox();
        IsShowing = false;
        var done = onFinished;
        onFinished = null;
        done?.Invoke();
    }

    private void CloseDialogueBox()
    {
        if (dialogueBox != null) dialogueBox.SetActive(false);
        if (textLabel != null) textLabel.text = string.Empty;
    }
}
