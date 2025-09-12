using System.Text;
using Newtonsoft.Json;

namespace PocketSizedUniverse.Models.Data;

public static class Base64Util
{
    public static string ToBase64(object obj)
    {
        var json = JsonConvert.SerializeObject(obj);
        var bytes = Encoding.UTF8.GetBytes(json);
        return Convert.ToBase64String(bytes);
    }
    public static T? FromBase64<T>(string base64)
    {
        var bytes = Convert.FromBase64String(base64);
        var json = Encoding.UTF8.GetString(bytes);
        return JsonConvert.DeserializeObject<T>(json);
    }
}