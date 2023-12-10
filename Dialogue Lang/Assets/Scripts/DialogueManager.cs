using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class DialogueManager : MonoBehaviour
{
    [Header("Global Settings")]
    public GameObject choiceButtonPrefab;
    public float letterDelay = 0.05f;
    public float sentenceDelay = 1.0f;
    public float letterSpeedMultiplier = 0.5f;

    [Header("UI Layout References")]
    public Transform dialogueObject;
    public Transform choiceButtonsObject;
    public TMP_Text dialogueText;
    public TMP_Text npcNameText;

    // Singleton
    static DialogueManager instance;

    // Start is called before the first frame update
    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            if (instance != this)
            {
                Destroy(instance.gameObject);
                instance = this;
            }
        }
    }

    public static DialogueManager GetInstance() { return instance; }
}
