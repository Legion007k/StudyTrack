using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyTrack.Data;
using StudyTrack.Models;
using StudyTrack.Models.ViewModels;

[Authorize]
public class CalendarController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public CalendarController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }
    //  Mostrar calendario + lista
    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User);

        var actividades = await _db.Actividades
            .Where(a => a.ApplicationUserId == userId)
            .ToListAsync();

        return View(actividades);
    }

    //  CREATE GET
    public IActionResult Crear()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Crear(ActividadViewModel vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

        var actividad = new Actividad
        {
            Titulo = vm.Titulo,
            Descripcion = vm.Descripcion,
            FechaInicio = vm.FechaInicio,
            FechaFin = vm.FechaFin,
            Notificar = vm.Notificar,
            ApplicationUserId = _userManager.GetUserId(User)
        };

        _db.Actividades.Add(actividad);
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    //  EDIT
    public async Task<IActionResult> Editar(int id)
    {
        var actividad = await _db.Actividades.FindAsync(id);
        if (actividad == null) return NotFound();
        if (actividad.ApplicationUserId != _userManager.GetUserId(User)) return Forbid();
        var vm = new ActividadViewModel
        {
            Actividadd_Id = actividad.Actividad_Id,
            Titulo = actividad.Titulo,
            Descripcion = actividad.Descripcion,
            FechaInicio = actividad.FechaInicio,
            FechaFin = actividad.FechaFin,
            Notificar = actividad.Notificar
        };
        Console.WriteLine("Editing activity with ID: " + vm.Actividadd_Id);

        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Editar(ActividadViewModel actividad)
    {
        if (!ModelState.IsValid)
            return View(actividad);
        Console.WriteLine("Editing activity with ID: " + actividad.Actividadd_Id);
        var old = await _db.Actividades.FindAsync(actividad.Actividadd_Id);
        if (old == null) return NotFound();
        if (old.ApplicationUserId != _userManager.GetUserId(User)) return Forbid();

        old.Titulo = actividad.Titulo;
        old.Descripcion = actividad.Descripcion;
        old.FechaInicio = actividad.FechaInicio;
        old.FechaFin = actividad.FechaFin;
        old.Notificar = actividad.Notificar;

        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    //  DELETE
    [HttpPost]
    public async Task<IActionResult> Eliminar(ActividadViewModel id)
    {
        Console.WriteLine("Deleting activity with ID: " + id.Actividadd_Id);
        var actividad = await _db.Actividades.FindAsync(id.Actividadd_Id);
        if (actividad == null) return NotFound();
        if (actividad.ApplicationUserId != _userManager.GetUserId(User)) return Forbid();

        _db.Actividades.Remove(actividad);
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }
}
