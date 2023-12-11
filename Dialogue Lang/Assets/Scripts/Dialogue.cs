using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Represents a collection of uniform variables. This class is used to store and manage a set of variables
/// where each variable is identified by a string key. It provides methods to set and retrieve variable values.
/// </summary>
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

/// <summary>
/// Represents a choice in a dialogue system. This class is used to store information about a specific choice
/// within a dialogue, including the text displayed to the user and the identifier of the next dialogue header
/// to navigate to when this choice is selected.
/// </summary>
public class DialogueChoice
{
    public string Text { get; set; }
    public string NextHeader { get; set; } 
}

/// <summary>
/// Represents a section of a dialogue in a narrative or dialogue system. This class contains the content of a
/// dialogue section, including the header (identifier), any redirection to other dialogue sections, the lines of 
/// dialogue, and the choices available to the user at the end of the section.
/// </summary>
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

    /// <summary>
    /// Sets a variable in the dialogue system. This method assigns a value to a specified key in the uniform variables collection.
    /// It also marks that the uniforms have changed.
    /// </summary>
    /// <param name="key">The key of the variable to set.</param>
    /// <param name="value">The value to assign to the variable.</param>
    public void SetDialogueVariable(string key, object value)
    {
        uniformVariables.SetVariable(key, value);
        uniformsChanged = true;
    }

    /// <summary>
    /// Retrieves a variable from the dialogue system. This method returns the value of a specified key from the uniform variables collection.
    /// </summary>
    /// <param name="key">The key of the variable to retrieve.</param>
    /// <returns>The value of the variable if it exists; otherwise, null.</returns>
    public object GetDialogueVariable(string key)
    {
        return uniformVariables.GetVariable(key);
    }

    /// <summary>
    /// Retrieves a variable from the dialogue system and converts it to an integer. This method attempts to convert the value of a specified key
    /// from the uniform variables collection to an integer. If the conversion is not possible, an exception is thrown.
    /// </summary>
    /// <param name="key">The key of the variable to retrieve and convert.</param>
    /// <returns>The integer value of the variable.</returns>
    /// <exception cref="ArgumentException">Thrown when the value cannot be converted to an integer.</exception>
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

    /// <summary>
    /// Handles the interaction with an NPC (non-player character) in a dialogue system. 
    /// This method initiates a dialogue if the player is not already in one. It sets up the dialogue environment,
    /// including cursor state and dialogue UI elements. It also parses and displays the conversation from the dialogue file.
    /// </summary>
    /// <remarks>
    /// The method performs several steps:
    /// - Checks if the player is already in a dialogue; if so, it returns immediately.
    /// - Sets the 'isInDialogue' flag to true to indicate the start of a dialogue.
    /// - Sets up the dialogue UI with the NPC's name and clears any existing text.
    /// - Saves the current cursor state and updates the cursor for dialogue interaction.
    /// - Parses the dialogue file if there were changes to the uniform variables since the last interaction.
    /// - Retrieves and displays the initial section of the dialogue.
    /// </remarks>
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

    /// <summary>
    /// Resolves any redirects within a dialogue section to find the final section to be displayed. 
    /// This method recursively follows the redirect chain in the dialogue until it reaches a section without a redirect.
    /// </summary>
    /// <param name="section">The initial dialogue section from which to start following redirects.</param>
    /// <returns>The final dialogue section after resolving all redirects.</returns>
    /// <remarks>
    /// This method checks if the provided section has a redirect. If it does, and the redirect points to a valid section key in the dialogue dictionary,
    /// the method calls itself recursively with the new section. This process repeats until a section without a redirect is reached,
    /// which is then returned as the final section to display.
    /// </remarks>
    private DialogueSection FollowRedirects(DialogueSection section)
    {
        if (section.Redirect != null && dialogue_dict.ContainsKey(section.Redirect))
        {
            return FollowRedirects(dialogue_dict[section.Redirect]);
        }
        return section;
    }

    /// <summary>
    /// Coroutine for displaying a conversation in the dialogue system. This method sequentially displays each sentence in the dialogue section,
    /// simulating the typing effect with a delay between letters and sentences. After displaying all sentences, it presents the player choices.
    /// </summary>
    /// <param name="ds">The dialogue section to display.</param>
    /// <returns>An enumerator needed for the coroutine execution.</returns>
    /// <remarks>
    /// The method performs the following steps:
    /// - Clears and destroys any existing choice buttons.
    /// - Sets the dialogue UI elements to active.
    /// - Follows any redirects to find the final dialogue section to display.
    /// - Iterates through each sentence in the dialogue section, displaying them one character at a time.
    /// - After displaying each sentence, there's a delay before moving to the next one.
    /// - Once all sentences are displayed, it displays the player choices for the current section.
    /// </remarks>
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

    /// <summary>
    /// Coroutine for displaying a conversation in a dialogue system. It sequentially shows each sentence in the dialogue section,
    /// giving a typewriter-like effect. After displaying all sentences, it shows player choices.
    /// </summary>
    /// <param name="ds">The dialogue section containing the sentences and choices to be displayed.</param>
    /// <returns>An IEnumerator required for coroutine execution in Unity.</returns>
    /// <remarks>
    /// The coroutine performs the following steps:
    /// - Clears existing choice buttons from the UI.
    /// - Activates necessary UI elements for displaying dialogue and choices.
    /// - Checks and follows any redirects in the dialogue section to ensure the correct section is displayed.
    /// - Iterates through each sentence in the dialogue section, adding characters one by one to simulate typing, with a delay defined by 'letterDelay'.
    /// - After each sentence, waits for a duration defined by 'sentenceDelay' before continuing to the next sentence.
    /// - Once all sentences are displayed, it invokes 'DisplayPlayerChoices' to show choices to the player based on the dialogue section.
    /// </remarks>
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

    /// <summary>
    /// Displays the player choices at the end of a dialogue section. This method generates interactive buttons for each choice,
    /// allowing the player to select their response or action.
    /// </summary>
    /// <param name="choices">A list of DialogueChoice objects representing the choices available to the player.</param>
    /// <remarks>
    /// The method performs the following steps:
    /// - Activates the UI element for displaying choices and deactivates the NPC dialogue text element.
    /// - Iterates through each choice in the provided list, creating a button for each one.
    /// - Sets the text of each button to match the text of the dialogue choice.
    /// - Adds an event listener to each button. The listener behavior depends on the 'NextHeader' property of the choice:
    ///   - If 'NextHeader' is "EXIT", the button will end the dialogue.
    ///   - If 'NextHeader' is "INVOKE", the button will end the dialogue and invoke a specific event (defined by 'onInvoke').
    ///   - Otherwise, the button will trigger the display of the next dialogue section indicated by 'NextHeader'.
    /// - Adds the newly created buttons to the UI and stores them in 'buttonList' for management.
    /// </remarks>
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

    /// <summary>
    /// Parses a dialogue file and constructs a dictionary of dialogue sections. This method reads and interprets the contents of a dialogue file,
    /// organizing it into a structured format that can be used in the dialogue system.
    /// </summary>
    /// <param name="resourceName">The name of the resource file containing the dialogue.</param>
    /// <returns>A dictionary where keys are section headers and values are DialogueSection objects, or null if the text asset cannot be loaded.</returns>
    /// <remarks>
    /// The method performs the following steps:
    /// - Loads the text asset using the provided resource name.
    /// - Splits the text asset into individual lines of dialogue.
    /// - Initializes a new dictionary to hold dialogue sections.
    /// - Processes each line to build up dialogue sections, including handling conditions and choices.
    /// - Adds the final dialogue section to the dictionary if it exists.
    /// - Returns the constructed dictionary of dialogue sections, or null if the resource cannot be found or loaded.
    /// </remarks>
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

    /// <summary>
    /// Parses the variables and initializes dialogue sections from the provided lines of dialogue. 
    /// This method is responsible for the first pass of dialogue parsing, setting up headers and variables.
    /// </summary>
    /// <param name="lines">The lines of dialogue to be parsed.</param>
    /// <param name="dialogueSections">A dictionary to store the initialized dialogue sections.</param>
    /// <param name="currentSection">The current dialogue section being processed.</param>
    /// <remarks>
    /// The method performs the following actions:
    /// - Iterates through each line, trimming leading and trailing whitespace.
    /// - Identifies and initializes dialogue section headers.
    /// - Parses and sets variables that haven't been defined before.
    /// </remarks>
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

    /// <summary>
    /// Processes the dialogue sections and choices based on the lines of dialogue. 
    /// This method is responsible for the second pass of dialogue parsing, handling dialogue text, sections, and choices.
    /// </summary>
    /// <param name="lines">The lines of dialogue to be processed.</param>
    /// <param name="dialogueSections">A dictionary storing dialogue sections.</param>
    /// <param name="lastIfConditionMet">A reference to a boolean flag indicating if the last 'if' condition was met.</param>
    /// <param name="currentSection">The current dialogue section being processed.</param>
    /// <remarks>
    /// The method performs the following actions:
    /// - Skips variable definitions processed in the first pass.
    /// - Processes headers, including saving the last section and setting the current section.
    /// - Handles conditional headers and redirects.
    /// - Parses choices, including conditional choices, and adds them to the current section.
    /// - Adds normal dialogue lines to the current section.
    /// </remarks>
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

    /// <summary>
    /// Splits the content of a TextAsset into individual lines for processing.
    /// </summary>
    /// <param name="textAsset">The TextAsset containing the dialogue text.</param>
    /// <returns>An array of strings, each representing a line in the TextAsset.</returns>
    /// <remarks>
    /// This method splits the text based on carriage returns and newlines, removing empty entries.
    /// It's used to prepare dialogue text for further parsing.
    /// </remarks>
    private string[] SplitDialogueLines(TextAsset textAsset)
    {
        return textAsset.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// Loads a TextAsset resource by its name.
    /// </summary>
    /// <param name="resourceName">The name of the resource to load.</param>
    /// <returns>The loaded TextAsset, or null if the resource cannot be found.</returns>
    /// <remarks>
    /// This method attempts to load a TextAsset using Unity's Resources.Load method.
    /// It logs an error if the resource cannot be loaded.
    /// </remarks>
    private TextAsset LoadTextAsset(string resourceName)
    {
        TextAsset textAsset = Resources.Load<UnityEngine.TextAsset>(resourceName);
        if (textAsset == null)
        {
            Debug.LogError($"Failed to load resource: {resourceName}");
        }
        return textAsset;
    }

    /// <summary>
    /// Evaluates a condition expressed as a string, comparing a variable from the uniform variables against a value.
    /// </summary>
    /// <param name="condition">The condition to evaluate, expressed in a format like "variable operator value".</param>
    /// <returns>True if the condition is met, false otherwise.</returns>
    /// <remarks>
    /// This method supports various operators (==, !=, <, <=, >, >=) and handles both equality/inequality and numerical comparisons.
    /// It logs an error if non-integer values are used for numerical comparisons.
    /// </remarks>
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
