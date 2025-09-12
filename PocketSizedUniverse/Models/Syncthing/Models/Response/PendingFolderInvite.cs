using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Syncthing.Models.Response;

/// <summary>
/// Represents a pending folder invitation from a remote star.
/// </summary>
public class PendingFolderInvite
{
    [JsonProperty("time")]
    public DateTime Time { get; set; }
    
    [JsonProperty("label")]
    public string Label { get; set; } = string.Empty;
    
    [JsonProperty("receiveEncrypted")]
    public bool ReceiveEncrypted { get; set; }
    
    [JsonProperty("remoteEncrypted")]
    public bool RemoteEncrypted { get; set; }
}

/// <summary>
/// Represents a pending folder with all star invitations.
/// </summary>
public class PendingFolder
{
    [JsonProperty("offeredBy")]
    public Dictionary<string, PendingFolderInvite> OfferedBy { get; set; } = new();
    
    /// <summary>
    /// Gets the folder ID for this pending folder.
    /// </summary>
    public string FolderId { get; set; } = string.Empty;
}

/// <summary>
/// Custom JSON converter that handles both array and object responses from the pending folders API.
/// </summary>
public class PendingFoldersResponseConverter : JsonConverter<PendingFoldersResponse>
{
    public override void WriteJson(JsonWriter writer, PendingFoldersResponse? value, JsonSerializer serializer)
    {
        if (value == null || !value.HasPendingFolders)
        {
            writer.WriteStartArray();
            writer.WriteEndArray();
        }
        else
        {
            serializer.Serialize(writer, value.Folders);
        }
    }

    public override PendingFoldersResponse ReadJson(JsonReader reader, Type objectType, PendingFoldersResponse? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        var token = JToken.Load(reader);
        var response = new PendingFoldersResponse();

        if (token.Type == JTokenType.Array)
        {
            // Empty array case - no pending folders
            response.Folders = new Dictionary<string, PendingFolder>();
        }
        else if (token.Type == JTokenType.Object)
        {
            // Object case - has pending folders
            var folders = token.ToObject<Dictionary<string, PendingFolder>>(serializer) ?? new Dictionary<string, PendingFolder>();
            
            // Set the folder ID on each PendingFolder for reference
            foreach (var kvp in folders)
            {
                kvp.Value.FolderId = kvp.Key;
            }
            
            response.Folders = folders;
        }

        return response;
    }
}

/// <summary>
/// Represents the complete response from GET /rest/cluster/pending/folders.
/// Can be either an empty array when no pending folders exist, or an object with folder IDs as keys.
/// </summary>
[JsonConverter(typeof(PendingFoldersResponseConverter))]
public class PendingFoldersResponse
{
    public Dictionary<string, PendingFolder> Folders { get; set; } = new();
    
    /// <summary>
    /// Returns true if there are any pending folders.
    /// </summary>
    public bool HasPendingFolders => Folders.Any();
    
    /// <summary>
    /// Returns the number of pending folders.
    /// </summary>
    public int Count => Folders.Count;
}
