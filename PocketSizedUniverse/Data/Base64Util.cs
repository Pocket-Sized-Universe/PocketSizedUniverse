using System.Text;
using Ipfs;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace PocketSizedUniverse.Data;

public static class Base64Util
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        ContractResolver = new OrderedContractResolver(),
        Formatting = Formatting.None,
        NullValueHandling = NullValueHandling.Ignore,
        Converters = [new CidConverter()]
    };

    public static string ToBase64(object obj)
    {
        var json = JsonConvert.SerializeObject(obj, Settings);
        var bytes = Encoding.UTF8.GetBytes(json);
        return Convert.ToBase64String(bytes);
    }

    public static T? FromBase64<T>(string base64)
    {
        var bytes = Convert.FromBase64String(base64);
        var json = Encoding.UTF8.GetString(bytes);
        return JsonConvert.DeserializeObject<T>(json, Settings);
    }

    private class OrderedContractResolver : DefaultContractResolver
    {
        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        {
            return base.CreateProperties(type, memberSerialization)
                .OrderBy(p => p.PropertyName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}