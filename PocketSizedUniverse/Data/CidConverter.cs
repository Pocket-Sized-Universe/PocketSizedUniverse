using Ipfs;
using Newtonsoft.Json;

namespace PocketSizedUniverse.Data;

public class CidConverter : JsonConverter<Cid>
{
    public override void WriteJson(JsonWriter writer, Cid? value, JsonSerializer serializer)
    {
        writer.WriteValue(value?.ToString());
    }

    public override Cid? ReadJson(JsonReader reader, Type objectType, Cid? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        var s = reader.Value as string;
        return s == null ? null : (Cid)s;
    }
}
