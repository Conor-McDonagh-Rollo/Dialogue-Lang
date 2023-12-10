using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Demo : MonoBehaviour
{
    Dialogue dialogue;

    private void Start()
    {
        dialogue = GetComponent<Dialogue>();
    }

    public void UpdateDialogue()
    {
        dialogue.SetDialogueVariable("count", dialogue.GetDialogueVariableAsInt("count") + 1);
    }
}
