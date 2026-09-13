using Microsoft.AspNetCore.Mvc;

namespace StudyTrack.Controllers
{
    /// <summary>
    /// Solo sirve la pagina del calendario. Todo el CRUD lo hace la vista
    /// contra /api/eventos, asi que aqui no queda logica de datos.
    /// </summary>
    public class CalendarController : Controller
    {
        public IActionResult Index() => View();
    }
}
