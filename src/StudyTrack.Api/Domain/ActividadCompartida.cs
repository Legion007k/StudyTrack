using System.Text.Json.Serialization;

namespace StudyTrack.Api.Domain;

/// <summary>
/// Tabla intermedia que relaciona una actividad con los usuarios colaboradores
/// con los que ha sido compartida.
/// </summary>
[JsonConverter(typeof(ActividadCompartidaJsonConverter))]
public class ActividadCompartida
{
    public string ActividadId { get; set; } = string.Empty;

    [JsonIgnore]
    public Actividad? Actividad { get; set; }

    public string UsuarioId { get; set; } = string.Empty;

    [JsonIgnore]
    public Usuario? Usuario { get; set; }

    /// <summary>Fecha y hora en que se compartio la actividad.</summary>
    public DateTime FechaCompartida { get; set; }
}
