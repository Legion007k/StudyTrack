using System.ComponentModel.DataAnnotations;

namespace StudyTrack.Models.ViewModels
{
    public class ActividadViewModel
    {
        public int Actividadd_Id { get; set; }

        [Required]
        public string Titulo { get; set; }

        public string Descripcion { get; set; }

        [Required]
        [DataType(DataType.DateTime)]
        public DateTime FechaInicio { get; set; }

        [Required]
        [DataType(DataType.DateTime)]
        public DateTime FechaFin { get; set; }

        public bool Notificar { get; set; }

    }
}
