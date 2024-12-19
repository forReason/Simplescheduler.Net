using System.Text.Json;
using System.Text.Json.Serialization;

namespace SimpleScheduler.Net.util.json;

public class DayOfWeekHashSetConverter : JsonConverter<HashSet<DayOfWeek>>
{
    public override HashSet<DayOfWeek> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var set = new HashSet<DayOfWeek>();
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray) break;
                if (reader.TokenType == JsonTokenType.String)
                {
                    if (Enum.TryParse<DayOfWeek>(reader.GetString(), true, out var day))
                    {
                        set.Add(day);
                    }
                    else
                    {
                        throw new JsonException($"Invalid DayOfWeek value: {reader.GetString()}");
                    }
                }
            }
        }
        return set;
    }

    public override void Write(Utf8JsonWriter writer, HashSet<DayOfWeek> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var day in value)
        {
            writer.WriteStringValue(day.ToString());
        }
        writer.WriteEndArray();
    }
}
