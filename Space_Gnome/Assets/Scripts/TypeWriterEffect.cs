using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class TypeWriterEffect : MonoBehaviour
{
   [SerializeField] float typeWriterSpeed;   // characters per second
   public Coroutine RunTextCoRoutine(string textToType, TMP_Text textLabel)
    {
        return StartCoroutine(TypeText(textToType,textLabel));
    }

    public void StopTyping(Coroutine typing)
    {
        if (typing != null) StopCoroutine(typing);
    }

    private IEnumerator TypeText(string textToType, TMP_Text textLabel)
    {
        textLabel.text = string.Empty;
        float t = 0;
        int charIndex = 0;

        while(charIndex< textToType.Length)
        {
            // 2026: unscaled time, so text still types while gameplay is frozen (menus, tutorial).
            t += Time.unscaledDeltaTime *typeWriterSpeed;
            charIndex = Mathf.FloorToInt(t);
            charIndex= Mathf.Clamp(charIndex,0, textToType.Length);

            textLabel.text =  textToType.Substring(0,charIndex);
            yield return null;
        }
        textLabel.text = textToType;
    }
}
