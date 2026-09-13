using System.ComponentModel.DataAnnotations;

namespace StudyTrack.Models.Dtos
{
    /// <summary>Cuerpo de POST y PUT /api/eventos.</summary>
    public class EventoRequest
    {
        [Required(ErrorMessage = "El titulo es obligatorio.")]
        public string Titulo { get; set; } = string.Empty;

        public string? Descripcion { get; set; }

        [Required]
        public DateTime FechaInicio { get; set; }

        [Required]
        public DateTime FechaFin { get; set; }

        public TipoActividad Tipo { get; set; } = TipoActividad.General;
    }

    public record EventoDto(
        int Id,
        string Titulo,
        string? Descripcion,
        DateTime FechaInicio,
        DateTime FechaFin,
        TipoActividad Tipo,
        bool Completada,
        DateTime? FechaCompletada,
        bool GeneradaAutomaticamente,
        int? ActividadPadreId)
    {
        public static EventoDto De(Actividad a) => new(
            a.Id, a.Titulo, a.Descripcion, a.FechaInicio, a.FechaFin, a.Tipo,
            a.Completada, a.FechaCompletada, a.GeneradaAutomaticamente, a.ActividadPadreId);
    }

    /// <summary>Respuesta de las operaciones que disparan la cascada.</summary>
    public record CascadaDto(EventoDto Evento, IReadOnlyList<EventoDto> Generados);

    /// <summary>Respuesta de PUT: el evento y los derivados que se movieron con el.</summary>
    public record ReagendaDto(EventoDto Evento, IReadOnlyList<EventoDto> Reagendados);
}
