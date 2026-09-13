using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyTrack.Data;
using StudyTrack.Models;
using StudyTrack.Models.Dtos;
using StudyTrack.Services;

namespace StudyTrack.Controllers.Api
{
    [ApiController]
    [Route("api/eventos")]
    [Produces("application/json")]
    public class EventosController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly MotorDeReglas _motor;

        public EventosController(ApplicationDbContext db, MotorDeReglas motor)
        {
            _db = db;
            _motor = motor;
        }

        /// <summary>Lista los eventos, opcionalmente acotados a un rango de fechas.</summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<EventoDto>>> Listar(
            [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
        {
            var consulta = _db.Actividades.AsNoTracking().AsQueryable();

            // Un evento entra si se solapa con el rango, no solo si empieza dentro.
            if (desde is not null)
                consulta = consulta.Where(a => a.FechaFin >= desde);
            if (hasta is not null)
                consulta = consulta.Where(a => a.FechaInicio <= hasta);

            var eventos = await consulta
                .OrderBy(a => a.FechaInicio)
                .Select(a => EventoDto.De(a))
                .ToListAsync();

            return Ok(eventos);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<EventoDto>> Obtener(int id)
        {
            var actividad = await _db.Actividades.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
            if (actividad is null)
                return NotFound();

            return Ok(EventoDto.De(actividad));
        }

        /// <summary>
        /// Crea un evento y, segun su tipo, la cascada de eventos preparatorios
        /// (un examen genera sus sesiones de estudio hacia atras desde la fecha).
        /// </summary>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        public async Task<ActionResult<CascadaDto>> Crear([FromBody] EventoRequest req)
        {
            if (req.FechaFin < req.FechaInicio)
                return BadRequest(new { error = "FechaFin no puede ser anterior a FechaInicio." });

            var actividad = new Actividad
            {
                Titulo = req.Titulo,
                Descripcion = req.Descripcion,
                FechaInicio = req.FechaInicio,
                FechaFin = req.FechaFin,
                Tipo = req.Tipo
            };

            var generadas = _motor.Expandir(actividad, Disparador.AlCrear);

            _db.Actividades.Add(actividad);
            await _db.SaveChangesAsync();

            var respuesta = new CascadaDto(
                EventoDto.De(actividad),
                generadas.Select(EventoDto.De).ToList());

            return CreatedAtAction(nameof(Obtener), new { id = actividad.Id }, respuesta);
        }

        /// <summary>
        /// Edita un evento. Si cambia la fecha de inicio, los eventos que genero y que aun
        /// no estan completados se desplazan lo mismo, de modo que el plan se mantiene.
        /// </summary>
        [HttpPut("{id:int}")]
        public async Task<ActionResult<ReagendaDto>> Editar(int id, [FromBody] EventoRequest req)
        {
            if (req.FechaFin < req.FechaInicio)
                return BadRequest(new { error = "FechaFin no puede ser anterior a FechaInicio." });

            var actividad = await _db.Actividades.FirstOrDefaultAsync(a => a.Id == id);
            if (actividad is null)
                return NotFound();

            var desplazamiento = req.FechaInicio - actividad.FechaInicio;

            actividad.Titulo = req.Titulo;
            actividad.Descripcion = req.Descripcion;
            actividad.FechaInicio = req.FechaInicio;
            actividad.FechaFin = req.FechaFin;
            actividad.Tipo = req.Tipo;

            var reagendadas = desplazamiento == TimeSpan.Zero
                ? []
                : await ReagendarDescendientes(id, desplazamiento);

            await _db.SaveChangesAsync();

            return Ok(new ReagendaDto(
                EventoDto.De(actividad),
                reagendadas.Select(EventoDto.De).ToList()));
        }

        /// <summary>
        /// Marca el evento como completado y dispara lo que dependia de que se cumpliera
        /// (completar una entrega abre la siguiente, contada desde la fecha real).
        /// </summary>
        [HttpPost("{id:int}/completar")]
        public async Task<ActionResult<CascadaDto>> Completar(int id)
        {
            var actividad = await _db.Actividades.FirstOrDefaultAsync(a => a.Id == id);
            if (actividad is null)
                return NotFound();

            // Idempotente: volver a completar no vuelve a disparar la cascada.
            if (actividad.Completada)
                return Ok(new CascadaDto(EventoDto.De(actividad), []));

            actividad.Completada = true;
            actividad.FechaCompletada = DateTime.Now;

            var generadas = _motor.Expandir(actividad, Disparador.AlCompletar);
            _db.Actividades.AddRange(generadas.Where(g => g.ActividadPadre == actividad));

            await _db.SaveChangesAsync();

            return Ok(new CascadaDto(
                EventoDto.De(actividad),
                generadas.Select(EventoDto.De).ToList()));
        }

        /// <summary>Borra el evento y, en cascada, todo lo que se genero a partir de el.</summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Eliminar(int id)
        {
            var actividad = await _db.Actividades.FirstOrDefaultAsync(a => a.Id == id);
            if (actividad is null)
                return NotFound();

            _db.Actividades.Remove(actividad);
            await _db.SaveChangesAsync();

            return NoContent();
        }

        /// <summary>Recorre el arbol de derivados y desplaza los que siguen pendientes.</summary>
        private async Task<List<Actividad>> ReagendarDescendientes(int raizId, TimeSpan desplazamiento)
        {
            var afectadas = new List<Actividad>();
            var pendientes = new Queue<int>();
            pendientes.Enqueue(raizId);

            while (pendientes.Count > 0)
            {
                var padreId = pendientes.Dequeue();
                var hijas = await _db.Actividades.Where(a => a.ActividadPadreId == padreId).ToListAsync();

                foreach (var hija in hijas)
                {
                    pendientes.Enqueue(hija.Id);

                    // Lo ya cumplido no se mueve: es historia, no plan.
                    if (hija.Completada)
                        continue;

                    hija.FechaInicio += desplazamiento;
                    hija.FechaFin += desplazamiento;
                    afectadas.Add(hija);
                }
            }

            return afectadas;
        }
    }
}
