namespace Tower.Dummy;

public class PlayerBase(uint entityId, uint clientId, string characterName)
{
    public string CharacterName { get; init; } = characterName;
    public uint EntityId { get; init; } = entityId;
    public uint ClientId { get; init; } = clientId;
}