using System.Text.Json;
using System.Text.Json.Serialization;

namespace StudyTrack.Api.Domain;

/// <summary>
/// Permite deserializar colaboradores tanto si se envían como strings simples ("usuario-2")
/// o como objetos ({ "usuarioId": "usuario-2" }).
/// Al serializar, siempre produce un objeto JSON consistente.
/// </summary>
public class ActividadCompartidaJsonConverter : JsonConverter<ActividadCompartida>
{
    public override ActividadCompartida? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var usuarioId = reader.GetString();
            return new ActividadCompartida
            {
                UsuarioId = usuarioId ?? string.Empty
            };
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;
            var usuarioId = string.Empty;
            var actividadId = string.Empty;
            DateTime? fechaCompartida = null;

            foreach (var prop in root.EnumerateObject())
            {
                if (prop.NameEquals("usuarioId") || prop.NameEquals("UsuarioId"))
                    usuarioId = prop.Value.GetString() ?? string.Empty;
                else if (prop.NameEquals("actividadId") || prop.NameEquals("ActividadId"))
                    actividadId = prop.Value.GetString() ?? string.Empty;
                else if (prop.NameEquals("fechaCompartida") || prop.NameEquals("FechaCompartida"))
                {
                    if (prop.Value.TryGetDateTime(out var dt))
                        fechaCompartida = dt;
                }
            }

            return new ActividadCompartida
            {
                UsuarioId = usuarioId,
                ActividadId = actividadId,
                FechaCompartida = fechaCompartida ?? DateTime.UtcNow
            };
        }

        return null;
    }

    public override void Write(Utf8JsonWriter writer, ActividadCompartida value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("actividadId", value.ActividadId);
        writer.WriteString("usuarioId", value.UsuarioId);
        writer.WriteString("fechaCompartida", value.FechaCompartida.Kind == DateTimeKind.Utc
            ? value.FechaCompartida
            : DateTime.SpecifyKind(value.FechaCompartida, DateTimeKind.Utc));
        writer.WriteEndObject();
    }
}

