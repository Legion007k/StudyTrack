namespace StudyTrack.Models
{
    public class SesionEstudio
    {
        public int Id { get; set; }
        public DateTime Fecha { get; set; }
        public int MinutosEstudiados { get; set; }
        public string Metodo { get; set; } = "Pomodoro";

        public string ApplicationUserId { get; set; }
        public ApplicationUser ApplicationUser { get; set; }
    }


}
