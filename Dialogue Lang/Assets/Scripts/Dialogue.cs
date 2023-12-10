using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UniformVariables
{
    public Dictionary<string, object> Variables { get; private set; }

    public UniformVariables()
    {
        Variables = new Dictionary<string, object>();
    }

    public void SetVariable(string key, object value)
    {
        Variables[key] = value;
    }

    public object GetVariable(string key)
    {
        Variables.TryGetValue(key, out var value);
        return value;
    }
}

public class DialogueChoice
{
    public string Text { get; set; }
    public string NextHeader { get; set; } 
}

public class DialogueSection
{
    public string Header { get; set; }
    public string Redirect { get; set; }
    public List<string> Lines { get; set; } = new List<string>();
    public List<DialogueChoice> Choices { get; set; } = new List<DialogueChoice>();
}

public class Dialogue : MonoBehaviour
{

    // Dialogue dictionary object
    Dictionary<string, DialogueSection> dialogue_dict;

    // Received from manager
    DialogueManager dm;
    GameObject choiceButtonPrefab;
    Transform choiceButtons;
    Transform dialogue;
    TMP_Text dialogue_npcText;
    TMP_Text dialogue_npcName;
    float letterDelay;
    float sentenceDelay;
    float letterSpeedMultiplier;

    // Private dialogue variables
    List<GameObject> buttonList = new List<GameObject>();
    UniformVariables uniformVariables = new UniformVariables();
    bool uniformsChanged = false;
    bool uniformVariablesAlreadyDefined = false;

    CursorLockMode cursor_previousLockMode;
    bool cursor_previousVisible;

    // Public dialogue set up per npc
    [Header("Dialogue Setup")]
    public string npc_display_name;
    public string dialogue_file_name;
    public UnityEvent onInvoke;
    public static bool isInDialogue = false;


    private void Start()
    {
        // Grab dialogue manager
        dm = DialogueManager.GetInstance();

        // Load from dialogue manager
        choiceButtonPrefab = dm.choiceButtonPrefab;
        choiceButtons = dm.choiceButtonsObject;
        dialogue = dm.dialogueObject;
        dialogue_npcText = dm.dialogueText;
        dialogue_npcName = dm.npcNameText;
        letterDelay = dm.letterDelay;
        sentenceDelay = dm.sentenceDelay;
        letterSpeedMultiplier = dm.letterSpeedMultiplier;

        // Parse dialogue
        dialogue_dict = ParseDialogueFile(dialogue_file_name);
    }

    private void Update()
    {
        if(isInDialogue)
        {
            if ((Input.GetMouseButton(0)))
            {
                letterDelay = dm.letterDelay * letterSpeedMultiplier;
            }
            else
            {
                letterDelay = dm.letterDelay;
            }
        }
    }

    public void SetDialogueVariable(string key, object value)
    {
        uniformVariables.SetVariable(key, value);
        uniformsChanged = true;
    }

    public object GetDialogueVariable(string key)
    {
        return uniformVariables.GetVariable(key);
    }

    public int GetDialogueVariableAsInt(string key)
    {
        try
        {
            int result = Convert.ToInt32(uniformVariables.GetVariable(key));
            return result;
        }
        catch (FormatException)
        {
            throw new ArgumentException("Input cannot be converted to an integer.");
        }
    }
    

    public void Interact()
    {
        if (isInDialogue)
        {
            return;
        }
        isInDialogue = true;

        // Setup dialogue
        dialogue_npcName.text = npc_display_name;
        dialogue_npcText.text = "";

        // Allow for cursor to be reset
        cursor_previousLockMode = Cursor.lockState;
        cursor_previousVisible = Cursor.visible;

        // Prepare cursor for dialogue choices
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Display conversation (Get first header's lines)
        if (uniformsChanged)
        {
            dialogue_dict.Clear();
            dialogue_dict = ParseDialogueFile(dialogue_file_name);
            uniformsChanged = false;
        }
        DialogueSection ds;
        if (dialogue_dict["initial"] != null)
        {
            ds = dialogue_dict["initial"];
        }
        else
        {
            ds = dialogue_dict[dialogue_dict.First().Key];
        }
        StartCoroutine(DisplayConversation(ds));
;    }

    private DialogueSection FollowRedirects(DialogueSection section)
    {
        if (section.Redirect != null && dialogue_dict.ContainsKey(section.Redirect))
        {
            return FollowRedirects(dialogue_dict[section.Redirect]);
        }
        return section;
    }

    IEnumerator DisplayConversation(DialogueSection ds)
    {
        foreach (GameObject go in buttonList)
        {
            Destroy(go);
        }
        buttonList.Clear();

        // Set actives
        choiceButtons.gameObject.SetActive(false);
        dialogue.gameObject.SetActive(true);
        dialogue_npcText.gameObject.SetActive(true);

        // Check for redirects
        if (ds.Redirect != null)
        {
            ds = FollowRedirects(dialogue_dict[ds.Redirect]);
        }


        List<string> sentences = ds.Lines;
        dialogue.gameObject.SetActive(true);

        foreach (string sentence in sentences)
        {
            dialogue_npcText.text = "";

            for (int i = 0; i < sentence.Length; i++)
            {
                dialogue_npcText.text += sentence[i];
                yield return new WaitForSeconds(letterDelay);
            }
            yield return new WaitForSeconds(sentenceDelay);
        }
        DisplayPlayerChoices(ds.Choices);
    }

    void DisplayPlayerChoices(List<DialogueChoice> choices)
    {
        // Set actives
        choiceButtons.gameObject.SetActive(true);
        dialogue_npcText.gameObject.SetActive(false);

        // Generate choice buttons
        foreach (DialogueChoice dc in choices)
        {
            GameObject go = Instantiate(choiceButtonPrefab);
            Button b = go.GetComponent<Button>();
            b.transform.GetChild(0).GetComponent<TMP_Text>().text = dc.Text;
            if (dc.NextHeader == "EXIT")
            {
                b.onClick.AddListener(() => 
                {
                    ExitDialogue();
                });
            }
            else if (dc.NextHeader == "INVOKE")
            {
                b.onClick.AddListener(() =>
                {
                    ExitDialogue();
                    onInvoke.Invoke();
                });
            }
            else
            {
                DialogueSection ds = dialogue_dict[dc.NextHeader];
                b.onClick.AddListener(() => { StartCoroutine(DisplayConversation(ds)); });
            }
            go.transform.SetParent(choiceButtons, false);
            buttonList.Add(go);
        }

    }
    
    void ExitDialogue()
    {
        // Exit dialogue part
        dialogue.gameObject.SetActive(false);
        isInDialogue = false;

        // Exit and reset choices part
        foreach (GameObject go in buttonList)
        {
            Destroy(go);
        }
        buttonList.Clear();
        choiceButtons.parent.gameObject.SetActive(false);
        choiceButtons.gameObject.SetActive(false);

        // Reset cursor
        Cursor.lockState = cursor_previousLockMode;
        Cursor.visible = cursor_previousVisible;
    }

    private Dictionary<string, DialogueSection> ParseDialogueFile(string resourceName)
    {
        TextAsset textAsset = LoadTextAsset(resourceName);
        if (textAsset == null)
        {
            return null;
        }

        string[] lines = SplitDialogueLines(textAsset);
        Dictionary<string, DialogueSection> dialogueSections = new Dictionary<string, DialogueSection>();
        bool lastIfConditionMet = false;

        DialogueSection currentSection = null;
        ParseVariablesAndInitializeSections(lines, dialogueSections, currentSection);
        ProcessDialogueSectionsAndChoices(lines, dialogueSections, ref lastIfConditionMet, currentSection);

        // Add the final section
        if (currentSection != null)
            dialogueSections[currentSection.Header] = currentSection;

        return dialogueSections;
    }

    private void ParseVariablesAndInitializeSections(string[] lines, Dictionary<string, DialogueSection> dialogueSections, DialogueSection currentSection)
    {
        bool firstHeader = true;

        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();

            // Parse headers for later reference
            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                string header = line.Substring(1, line.Length - 2).Trim();
                if (!dialogueSections.ContainsKey(header))
                {
                    dialogueSections.Add(header, new DialogueSection { Header = header });
                    if (firstHeader)
                    {
                        firstHeader = false;
                        currentSection = dialogueSections[header];
                    }
                }
            }

            if (uniformVariablesAlreadyDefined)
                continue;
            // Parse and assign variables
            else if (line.Contains("=") && !line.StartsWith("if"))
            {
                var parts = line.Split('=');
                var key = parts[0].Trim();
                var valueStr = parts[1].Trim();
                object value = valueStr.ToLower() == "true" || valueStr.ToLower() == "false"
                                ? bool.Parse(valueStr)
                                : Convert.ChangeType(valueStr, typeof(object));
                uniformVariables.SetVariable(key, value);
            }
        }
        uniformVariablesAlreadyDefined = true;
    }

    private void ProcessDialogueSectionsAndChoices(string[] lines, Dictionary<string, DialogueSection> dialogueSections, ref bool lastIfConditionMet, DialogueSection currentSection)
    {

        // Second pass: Process dialogue sections and choices
        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();

            // Skip variable definitions
            if (line.Contains("=") && !line.StartsWith("if"))
                continue;

            // Handle headers from pass 1
            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                // Save the last section
                if (currentSection != null)
                {
                    dialogueSections[currentSection.Header] = currentSection;
                }

                string header = line.Substring(1, line.Length - 2).Trim();
                currentSection = dialogueSections[header];
                continue;
            }

            // Handle header conditions
            if (line.StartsWith("if") && line.Contains("else"))
            {
                bool isIf = line.StartsWith("if");
                var condition = isIf ? line.Substring(4, line.IndexOf(']') - 4).Trim() : "";
                lastIfConditionMet = isIf ? EvaluateCondition(condition) : !lastIfConditionMet;

                if (lastIfConditionMet)
                {
                    int targetSectionStart = line.IndexOf(']') + 1;
                    int targetSectionEnd = line.Length;
                    if (isIf && line.Contains("else"))
                    {
                        targetSectionEnd = line.IndexOf("else", StringComparison.Ordinal);
                    }
                    var targetSection = line.Substring(targetSectionStart, targetSectionEnd - targetSectionStart).Trim();

                    if (targetSection.StartsWith("[") && targetSection.EndsWith("]"))
                    {
                        targetSection = targetSection.Substring(1, targetSection.Length - 2).Trim();
                    }

                    // Save the last section
                    if (currentSection != null)
                    {
                        dialogueSections[currentSection.Header] = currentSection;
                    }

                    // If section doesn't exist, create it
                    if (!dialogueSections.ContainsKey(targetSection))
                    {
                        dialogueSections[targetSection] = new DialogueSection { Header = targetSection };
                        currentSection = dialogueSections[targetSection];
                    }
                    else if (dialogueSections.ContainsKey(targetSection))
                    {
                        currentSection = dialogueSections[targetSection];
                    }
                }
                else
                {
                    // Save the last section
                    if (currentSection != null)
                    {
                        dialogueSections[currentSection.Header] = currentSection;
                    }

                    // Get current section
                    int targetSectionStart = line.IndexOf(']') + 1;
                    int targetSectionEnd = line.Length;
                    if (isIf && line.Contains("else"))
                    {
                        targetSectionEnd = line.IndexOf("else", StringComparison.Ordinal);
                    }
                    var targetSection = line.Substring(targetSectionStart, targetSectionEnd - targetSectionStart).Trim();

                    if (targetSection.StartsWith("[") && targetSection.EndsWith("]"))
                    {
                        targetSection = targetSection.Substring(1, targetSection.Length - 2).Trim();
                    }

                    // Get redirect
                    targetSectionStart = targetSectionEnd;
                    targetSectionEnd = line.Length - 1; // Removes last ']'
                    targetSectionStart += 6; // Increases index and removed the first '['
                    var redirectSection = line.Substring(targetSectionStart, targetSectionEnd - targetSectionStart).Trim();

                    // If section doesn't exist, create it
                    if (!dialogueSections.ContainsKey(targetSection))
                    {
                        dialogueSections[targetSection] = new DialogueSection { Header = targetSection };
                        currentSection = dialogueSections[targetSection];
                    }
                    else if (dialogueSections.ContainsKey(targetSection))
                    {
                        currentSection = dialogueSections[targetSection];
                    }

                    currentSection.Redirect = redirectSection;
                }
                continue;
            }

            // Skip lines if in a skipped section
            if (currentSection == null) continue;

            // Parse headers
            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                if (currentSection != null)
                {
                    dialogueSections[currentSection.Header] = currentSection;
                }
                currentSection = new DialogueSection { Header = line.Substring(1, line.Length - 2) };
                continue;
            }

            // Parse choices, including conditional choices
            if (line.Contains("[") && line.Contains("]"))
            {
                // Handle conditional choices
                if (line.StartsWith("if"))
                {
                    var condition = line.Substring(4, line.IndexOf(']') - 4).Trim();
                    if (!EvaluateCondition(condition))
                        continue;

                    line = line.Substring(line.IndexOf(']') + 1).Trim();
                }

                int indexStart = line.LastIndexOf('[');
                string choiceText = line.Substring(0, indexStart).Trim();
                string nextHeader = line.Substring(indexStart + 1, line.Length - indexStart - 2);

                currentSection.Choices.Add(new DialogueChoice { Text = choiceText, NextHeader = nextHeader });
            }
            else
            {
                currentSection.Lines.Add(line);
            }
        }
    }

    private string[] SplitDialogueLines(TextAsset textAsset)
    {
        return textAsset.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
    }

    private TextAsset LoadTextAsset(string resourceName)
    {
        TextAsset textAsset = Resources.Load<UnityEngine.TextAsset>(resourceName);
        if (textAsset == null)
        {
            Debug.LogError($"Failed to load resource: {resourceName}");
        }
        return textAsset;
    }


    private bool EvaluateCondition(string condition)
    {
        string[] operators = { "==", "!=", "<", "<=", ">", ">=" };
        string usedOperator = operators.FirstOrDefault(op => condition.Contains(op));
        if (string.IsNullOrEmpty(usedOperator)) return false;

        var parts = condition.Split(new[] { usedOperator }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2) return false;

        var variableName = parts[0].Trim();
        var valueStr = parts[1].Trim();

        if (!uniformVariables.Variables.ContainsKey(variableName)) return false;

        var variableValueStr = uniformVariables.Variables[variableName].ToString();

        // Handle equality and inequality for all types
        if (usedOperator == "==" || usedOperator == "!=")
        {
            bool isEqual = variableValueStr.Equals(valueStr, StringComparison.OrdinalIgnoreCase);
            return usedOperator == "==" ? isEqual : !isEqual;
        }

        // Parse integer values for comparison
        if (!int.TryParse(variableValueStr, out int variableValue) || !int.TryParse(valueStr, out int value))
        {
            Debug.LogError("Non-integer values used for comparison operator.");
            return false;
        }

        // Handle numerical comparisons
        switch (usedOperator)
        {
            case "<":
                return variableValue < value;
            case "<=":
                return variableValue <= value;
            case ">":
                return variableValue > value;
            case ">=":
                return variableValue >= value;
            default:
                return false;
        }
    }


}
