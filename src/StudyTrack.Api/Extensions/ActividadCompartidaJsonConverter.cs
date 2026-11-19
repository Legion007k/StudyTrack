using System.Text.Json;
using System.Text.Json.Serialization;
using StudyTrack.Api.Domain;

namespace StudyTrack.Api.Extensions;

/// <summary>
/// Permite recibir colaboradores como strings simples ("usuario-2") o como objetos ({ "usuarioId": "usuario-2" }).
/// Se leen usuarioId, actividadId y fechaCompartida, pero al crear o actualizar el delegate solo usa
/// el usuarioId: actividadId y fechaCompartida los vuelve a fijar el servidor.
/// Al serializar siempre produce un objeto. Vive en la capa HTTP para que Domain no dependa del formato JSON.
/// </summary>
public sealed class ActividadCompartidaJsonConverter : JsonConverter<ActividadCompartida>
{
    public override ActividadCompartida Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return new ActividadCompartida { UsuarioId = reader.GetString() ?? string.Empty };

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var compartida = new ActividadCompartida();

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Name.Equals("usuarioId", StringComparison.OrdinalIgnoreCase))
                    compartida.UsuarioId = LeerTexto(prop);
                else if (prop.Name.Equals("actividadId", StringComparison.OrdinalIgnoreCase))
                    compartida.ActividadId = LeerTexto(prop);
                else if (prop.Name.Equals("fechaCompartida", StringComparison.OrdinalIgnoreCase)
                         && prop.Value.ValueKind == JsonValueKind.String && prop.Value.TryGetDateTime(out var fecha))
                    compartida.FechaCompartida = fecha;
            }

            return compartida;
        }

        // Un numero, booleano o arreglo no es un colaborador: se responde 400 en vez de fallar mas adelante.
        throw new JsonException("Cada colaborador debe ser un texto con el usuarioId o un objeto { \"usuarioId\": \"...\" }.");
    }

    private static string LeerTexto(JsonProperty prop) =>
        prop.Value.ValueKind == JsonValueKind.String
            ? prop.Value.GetString() ?? string.Empty
            : throw new JsonException($"El campo '{prop.Name}' de un colaborador debe ser un texto.");

    public override void Write(Utf8JsonWriter writer, ActividadCompartida value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("actividadId", value.ActividadId);
        writer.WriteString("usuarioId", value.UsuarioId);
        writer.WriteString("fechaCompartida", DateTime.SpecifyKind(value.FechaCompartida, DateTimeKind.Utc));
        writer.WriteEndObject();
    }
}
