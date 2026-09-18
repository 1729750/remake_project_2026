using System.Collections.Generic;

public sealed class PlayerPreparationData
{
    public ulong ClientId { get; }

    public CardDefinition[] CardCandidates { get; set; }

    public int SelectedCardIndex { get; set; } = -1;

    public bool CardSelectionCompleted { get; set; }

    public bool ConditionCompleted { get; set; }

    public List<CardDefinition> FinalDeck { get; }
        = new List<CardDefinition>();

    public PlayerPreparationData(
        ulong clientId)
    {
        ClientId = clientId;
    }
}