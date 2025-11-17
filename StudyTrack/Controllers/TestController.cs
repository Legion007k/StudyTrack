using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using StudyTrack.Models;

public class TestController : Controller
{
    private readonly IEmailService _emailService;
    private readonly UserManager<ApplicationUser> _userManager;

    public TestController(IEmailService emailService, UserManager<ApplicationUser> userManager)
    {
        _emailService = emailService;
        _userManager = userManager;
    }

    public async Task<IActionResult> ProbarCorreo()
    {
        var user = await _userManager.GetUserAsync(User);

        await _emailService.SendEmail(
            user.Email,
            " Notificación de prueba",
            "Si recibes este correo, tu sistema StudyTrack puede enviar notificaciones correctamente."
        );

        return Content("Correo enviado — revisa tu bandeja");
    }
}
