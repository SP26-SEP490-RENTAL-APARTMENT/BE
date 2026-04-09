using System.Text.Json;
using System.Text.Json.Serialization;

namespace Common.Utils
{
    public sealed class VietnamDateTimeJsonConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException("Expected a string token for DateTime.");
            }

            var raw = reader.GetString();
            if (string.IsNullOrWhiteSpace(raw))
            {
                throw new JsonException("DateTime value cannot be null or empty.");
            }

            return VietnamTime.ParseToVietnamTime(raw);
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            var vietnamOffset = VietnamTime.ToVietnamOffset(value);
            writer.WriteStringValue(vietnamOffset.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz"));
        }
    }

    public sealed class VietnamNullableDateTimeJsonConverter : JsonConverter<DateTime?>
    {
        private readonly VietnamDateTimeJsonConverter _inner = new();

        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            return _inner.Read(ref reader, typeof(DateTime), options);
        }

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (!value.HasValue)
            {
                writer.WriteNullValue();
                return;
            }

            _inner.Write(writer, value.Value, options);
        }
    }
}
