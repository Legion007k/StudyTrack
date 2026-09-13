using Microsoft.AspNetCore.Mvc;
using StudyTrack.Services;

namespace StudyTrack.Controllers.Api
{
    /// <summary>
    /// Catalogo de reglas de cascada. Es de solo lectura: las reglas viven en codigo,
    /// este endpoint existe para que el cliente sepa que va a pasar al crear o completar.
    /// </summary>
    [ApiController]
    [Route("api/reglas")]
    [Produces("application/json")]
    public class ReglasController : ControllerBase
    {
        [HttpGet]
        public ActionResult<IEnumerable<Regla>> Listar() => Ok(MotorDeReglas.Catalogo);
    }
}
