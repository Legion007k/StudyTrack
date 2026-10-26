namespace StudyTrack.Api.Domain;

/// <summary>
/// Tabla intermedia que relaciona una actividad con los usuarios colaboradores
/// con los que ha sido compartida.
/// </summary>
public class ActividadCompartida
{
    public string ActividadId { get; set; } = string.Empty;

    public Actividad? Actividad { get; set; }

    public string UsuarioId { get; set; } = string.Empty;

    public Usuario? Usuario { get; set; }

    /// <summary>Fecha y hora en que se compartio la actividad.</summary>
    public DateTime FechaCompartida { get; set; }
}
