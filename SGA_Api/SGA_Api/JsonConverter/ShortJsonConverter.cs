using System.Text.Json.Serialization;
using System.Text.Json;

namespace SGA_Api.JsonConverter
{
	public class ShortJsonConverter : JsonConverter<short>
	{
		public override short Read(ref Utf8JsonReader reader,
								   Type typeToConvert,
								   JsonSerializerOptions options)
		{
			// Si viene como número, intenta leerlo directo
			if (reader.TokenType == JsonTokenType.Number &&
				reader.TryGetInt16(out var value))
			{
				return value;
			}

			// Si viene como string, intenta parsear
			if (reader.TokenType == JsonTokenType.String &&
				short.TryParse(reader.GetString(), out value))
			{
				return value;
			}

			throw new JsonException($"No se pudo convertir el token {reader.GetString()} a Int16.");
		}

		public override void Write(Utf8JsonWriter writer,
								   short value,
								   JsonSerializerOptions options)
		{
			// siempre lo serializamos como número
			writer.WriteNumberValue(value);
		}
	}
}
