using System.ComponentModel.DataAnnotations;

namespace StudyTrack.Models
{
    public class Actividad
    {
        [Key]
        public int Actividad_Id { get; set; }
        public string Titulo { get; set; }
        public string Descripcion { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        public bool Notificar { get; set; }
        public bool Completada { get; set; }

        // FK al usuario (Identity)
        public string ApplicationUserId { get; set; }
        public ApplicationUser ApplicationUser { get; set; }
    }

}
