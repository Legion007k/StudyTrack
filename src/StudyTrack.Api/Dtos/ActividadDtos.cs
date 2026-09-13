using StudyTrack.Api.Domain;

namespace StudyTrack.Api.Dtos;

/// <summary>Representacion de una actividad con su subarbol de actividades hijas anidado.</summary>
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
    IReadOnlyList<ActividadDto> ActividadesHijas);

/// <summary>Cuerpo de POST /actividades. FechaInicio no se envia: es el momento de creacion.</summary>
public record CreateActividadDto
{
    public string UserId { get; init; } = string.Empty;
    public string Titulo { get; init; } = string.Empty;
    public string? Descripcion { get; init; }
    public DateTime FechaFin { get; init; }
    public TipoActividad Tipo { get; init; } = TipoActividad.General;
    public string? ActividadPadreId { get; init; }
}

/// <summary>Cuerpo de PUT /actividades/{actividadId}. Reemplaza todos los campos editables.</summary>
public record UpdateActividadDto
{
    public string Titulo { get; init; } = string.Empty;
    public string? Descripcion { get; init; }
    public DateTime FechaFin { get; init; }
    public TipoActividad Tipo { get; init; } = TipoActividad.General;
    public bool Completada { get; init; }
    public string? ActividadPadreId { get; init; }
}
