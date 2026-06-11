using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace J9_NeoAdmin.Utils;

public static class JsonExtensions
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        DateFormatString = "yyyy-MM-dd HH:mm:ss"
    };

    public static string ToJson(this object obj) => JsonConvert.SerializeObject(obj, Settings);

    public static T? Deserialize<T>(this string json) => JsonConvert.DeserializeObject<T>(json, Settings);
}
