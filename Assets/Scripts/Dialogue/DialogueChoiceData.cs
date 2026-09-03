public struct DialogueChoiceData
{
    public string localizedChoiceText;
    public int choiceIndex;

    public DialogueChoiceData(string text, int index)
    {
        localizedChoiceText = text;
        choiceIndex = index;
    }
}