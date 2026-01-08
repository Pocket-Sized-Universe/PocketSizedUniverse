namespace PocketSizedUniverse.Data;

public class PlayerData
{
    public string PlayerName { get; set; } = "";
    public uint WorldId { get; set; }
    public string? CustomizeState { get; set; }
    public string? GlamourerState { get; set; }
    public string? HeelsState { get; set; }
    public string? HonorificTitle { get; set; }
    public string? MoodlesState { get; set; }
    public string? PetNameState { get; set; }
    public string? MetaManipulations { get; set; }
    public List<CustomAsset> ModFiles { get; set; } = [];
    public List<AssetSwap> AssetSwaps { get; set; } = [];
    public DateTime LastModified { get; set; }
}