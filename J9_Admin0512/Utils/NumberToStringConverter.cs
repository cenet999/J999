// 在类的开始处添加自定义转换器
using System.Text.Json;
using System.Text.Json.Serialization;


namespace J9_Admin.Utils;


public class NumberToStringConverter : JsonConverter<Dictionary<string, string>>
{
    public override Dictionary<string, string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dictionary = new Dictionary<string, string>();

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected StartObject token");
        }

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected PropertyName token");
            }

            string propertyName = reader.GetString()!;
            reader.Read();

            // 将所有值转换为字符串
            string value = reader.TokenType switch
            {
                JsonTokenType.String => reader.GetString()!,
                JsonTokenType.Number => reader.GetDecimal().ToString(),
                JsonTokenType.True => "true",
                JsonTokenType.False => "false",
                JsonTokenType.Null => "",
                JsonTokenType.StartObject => throw new JsonException($"不支持的复杂对象类型在属性 '{propertyName}' 中"),
                JsonTokenType.StartArray => throw new JsonException($"不支持的数组类型在属性 '{propertyName}' 中"),
                _ => throw new JsonException($"不支持的 JSON 令牌类型: {reader.TokenType}")
            };

            dictionary[propertyName] = value;
        }

        return dictionary;
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<string, string> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var kvp in value)
        {
            writer.WriteString(kvp.Key, kvp.Value);
        }
        writer.WriteEndObject();
    }
}
