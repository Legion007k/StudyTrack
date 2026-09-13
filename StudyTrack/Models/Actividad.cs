using System.ComponentModel.DataAnnotations;

namespace StudyTrack.Models
{
    /// <summary>
    /// Tipo del evento. El tipo es lo que decide que reglas de cascada aplican:
    /// un Examen desencadena sesiones de estudio, una Entrega desencadena la siguiente.
    /// </summary>
    public enum TipoActividad
    {
        General,
        Examen,
        Entrega,
        Presentacion
    }

    public class Actividad
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Titulo { get; set; } = string.Empty;

        public string? Descripcion { get; set; }

        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }

        public TipoActividad Tipo { get; set; } = TipoActividad.General;

        public bool Completada { get; set; }

        /// <summary>Fecha real en la que se marco como completada; es el ancla de las reglas AlCompletar.</summary>
        public DateTime? FechaCompletada { get; set; }

        /// <summary>true si la creo el motor de reglas en vez del usuario.</summary>
        public bool GeneradaAutomaticamente { get; set; }

        /// <summary>Evento que la desencadeno. Null si la creo el usuario directamente.</summary>
        public int? ActividadPadreId { get; set; }
        public Actividad? ActividadPadre { get; set; }

        public ICollection<Actividad> ActividadesHijas { get; set; } = new List<Actividad>();
    }
}
