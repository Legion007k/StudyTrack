using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using StudyTrack.Api.Domain;
using StudyTrack.Api.Repositories;
using StudyTrack.Tests.Builders;

namespace StudyTrack.Tests.Routes;

/// <summary>
/// Clase base de las pruebas de rutas. xUnit crea una instancia por test, asi que cada test
/// levanta su propio host con su propia base en memoria: no hay estado compartido ni orden.
/// </summary>
public abstract class StudyTrackApiTests : IDisposable
{
    protected static readonly DateTime Ahora = ActividadBuilder.FechaBase;

    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _nombreBaseDeDatos = $"rutas-{Guid.NewGuid()}";

    protected StudyTrackApiTests()
    {
        TimeProvider = new FakeTimeProvider(new DateTimeOffset(Ahora, TimeSpan.Zero));
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                // Se reemplaza Npgsql por InMemory. Desde EF Core 9 tambien hay que quitar la
                // configuracion diferida, o quedarian dos proveedores registrados.
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(_nombreBaseDeDatos));

                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(TimeProvider);
            }));
        Client = Factory.CreateClient();
    }

    protected FakeTimeProvider TimeProvider { get; }

    protected WebApplicationFactory<Program> Factory { get; }

    protected HttpClient Client { get; }

    /// <summary>Cliente de un host derivado con servicios adicionales reemplazados.</summary>
    protected HttpClient CrearCliente(Action<IServiceCollection> configurarServicios) =>
        Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(configurarServicios)).CreateClient();

    protected async Task SembrarAsync(params Actividad[] actividades)
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Actividades.AddRange(actividades);
        await context.SaveChangesAsync();
    }

    protected async Task<int> ContarActividadesAsync()
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Actividades.CountAsync();
    }

    protected static Task<T?> LeerAsync<T>(HttpResponseMessage respuesta) =>
        respuesta.Content.ReadFromJsonAsync<T>(Json);

    protected static async Task<JsonElement> LeerJsonAsync(HttpResponseMessage respuesta) =>
        (await respuesta.Content.ReadFromJsonAsync<JsonElement>(Json));

    public void Dispose()
    {
        Client.Dispose();
        Factory.Dispose();
        GC.SuppressFinalize(this);
    }
}
