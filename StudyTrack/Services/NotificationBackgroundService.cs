using Microsoft.EntityFrameworkCore;
using StudyTrack.Data;

public class NotificationBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IEmailService _emailService;

    public NotificationBackgroundService(IServiceProvider serviceProvider, IEmailService emailService)
    {
        _serviceProvider = serviceProvider;
        _emailService = emailService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // actividades a notificar en los próximos 30 minutos
            var ahora = DateTime.Now;
            var proximas = await db.Actividades
                .Include(a => a.ApplicationUser)
                .Where(a => a.Notificar == true &&
                            a.FechaInicio > ahora &&
                            a.FechaInicio <= ahora.AddMinutes(30))
                .ToListAsync();

            foreach (var act in proximas)
            {
                await _emailService.SendEmail(
                    act.ApplicationUser.Email,
                    $"Recordatorio: {act.Titulo}",
                    $"Tu actividad <b>{act.Titulo}</b> inicia a las <b>{act.FechaInicio}</b>."
                );

                // Desactiva notificación para no volverla a mandar
                act.Notificar = false;
            }
            Console.WriteLine("Background service running...");

            await db.SaveChangesAsync();

            // revisar cada 60 segundos
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}
