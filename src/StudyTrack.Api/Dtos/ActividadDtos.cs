using StudyTrack.Api.Domain;

namespace StudyTrack.Api.Dtos;

/// <summary>
/// Representacion de lectura publica de una actividad con su subarbol jerarquico anidado y colaboradores.
/// Actua como proyeccion inmutable hacia el cliente, previniendo ciclos de serializacion y ocultando metodos mutadores de dominio.
/// </summary>
public record ActividadDto(
    string Id,
    string UserId,
    string Titulo,
    string? Descripcion,
    DateTime FechaInicio,
    DateTime FechaFin,
    TipoActividad Tipo,
    bool Completada,
    DateTime? FechaCompletada,
    bool GeneradaAutomaticamente,
    string? ActividadPadreId,
    IReadOnlyList<ActividadDto> ActividadesHijas,
    IReadOnlyList<string>? Colaboradores = null)
{
    public virtual bool Equals(ActividadDto? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return Id == other.Id
            && UserId == other.UserId
            && Titulo == other.Titulo
            && Descripcion == other.Descripcion
            && FechaInicio == other.FechaInicio
            && FechaFin == other.FechaFin
            && Tipo == other.Tipo
            && Completada == other.Completada
            && FechaCompletada == other.FechaCompletada
            && GeneradaAutomaticamente == other.GeneradaAutomaticamente
            && ActividadPadreId == other.ActividadPadreId
            && ActividadesHijas.SequenceEqual(other.ActividadesHijas)
            && ((Colaboradores == null && other.Colaboradores == null) ||
                (Colaboradores != null && other.Colaboradores != null && Colaboradores.SequenceEqual(other.Colaboradores)));
    }

    public override int GetHashCode() => HashCode.Combine(Id, UserId, Titulo, FechaInicio, FechaFin);
}

/// <summary>
/// Contrato de entrada para POST /actividades/{actividadId}/compartir.
/// Modelo atómico de intencion que solo expone el identificador del colaborador a invitar.
/// </summary>
public record CompartirActividadDto
{
    public string UsuarioId { get; init; } = string.Empty;
}

/// <summary>
/// Contrato de entrada para POST /actividades (creacion).
/// Bajo DDD, actua como Anti-Corruption Layer (ACL): no expone campos calculados por el sistema 
/// como Id, FechaInicio, Completada, Eliminada o GeneradaAutomaticamente, evitando vulnerabilidades de Over-Posting.
/// </summary>
public record CreateActividadDto
{
    public string UserId { get; init; } = string.Empty;
    public string Titulo { get; init; } = string.Empty;
    public string? Descripcion { get; init; }
    public DateTime FechaFin { get; init; }
    public TipoActividad Tipo { get; init; } = TipoActividad.General;
    public string? ActividadPadreId { get; init; }
}

/// <summary>
/// Contrato de entrada para PUT /actividades/{actividadId} (actualizacion total editable).
/// Segrega estrictamente los campos mutables por el usuario de las propiedades protegidas por las invariantes de dominio.
/// </summary>
public record UpdateActividadDto
{
    public string Titulo { get; init; } = string.Empty;
    public string? Descripcion { get; init; }
    public DateTime FechaFin { get; init; }
    public TipoActividad Tipo { get; init; } = TipoActividad.General;
    public bool Completada { get; init; }
    public string? ActividadPadreId { get; init; }
}