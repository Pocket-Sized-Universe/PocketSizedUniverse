namespace PocketSizedUniverse.Util;

public static class TopicUtil
{
    private const int ApiVersion = 1;
    private const string PairingSuffix = "pairing";
    private const string DataSuffix = "chardata";
    private const string GalaxySuffix = "galaxy";
    private const string ChatSuffix = "chat";
    private static string Prefix => $"psu_api_v{ApiVersion}";
    
    public static Guid? TopicToPairedGuid(string topic) => Guid.TryParse(topic.Split('/')[1], out var guid) ? guid : null;
    public static string GetPairingTopic(Guid fromId, Guid toId) => $"{Prefix}/{fromId}/{toId}/{DataSuffix}";
    public static string GetChatTopic(Guid id) => $"{Prefix}/{id}/{ChatSuffix}";
    public static string GetGalaxyTopic(Guid id) => $"{Prefix}/{id}/{GalaxySuffix}";
}