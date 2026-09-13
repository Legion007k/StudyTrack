using Microsoft.EntityFrameworkCore;
using StudyTrack.Api.Domain;
using StudyTrack.Api.Repositories;
using StudyTrack.Tests.Builders;

namespace StudyTrack.Tests.Repositories;

public class ActividadRepositoryTests
{
    private static readonly DateTime Fecha = ActividadBuilder.FechaBase;

    // Cada instancia de la clase (una por test) tiene su propia base en memoria.
    private readonly DbContextOptions<ApplicationDbContext> _options =
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"repositorio-{Guid.NewGuid()}")
            .Options;

    private ApplicationDbContext NuevoContexto() => new(_options);

    private async Task SembrarAsync(params Actividad[] actividades)
    {
        await using var context = NuevoContexto();
        context.Actividades.AddRange(actividades);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetAllAsync_SinActividades_DevuelveListaVacia()
    {
        await using var context = NuevoContexto();

        var resultado = await new ActividadRepository(context).GetAllAsync();

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task GetAllAsync_DevuelveTodasEnListaPlanaOrdenada()
    {
        var raiz = new ActividadBuilder().WithId("b-raiz").WithTitulo("Raiz").WithFechaInicio(Fecha).Build();
        new ActividadBuilder().WithId("a-hija").WithTitulo("Hija").WithFechaInicio(Fecha).WithPadre(raiz).Build();
        var anterior = new ActividadBuilder().WithId("c-anterior").WithTitulo("Anterior").WithFechaInicio(Fecha.AddDays(-1)).Build();
        await SembrarAsync(raiz, anterior);

        await using var context = NuevoContexto();
        var resultado = await new ActividadRepository(context).GetAllAsync();

        Assert.Equal(["c-anterior", "a-hija", "b-raiz"], resultado.Select(a => a.Id));
    }

    [Fact]
    public async Task GetByIdAsync_Inexistente_DevuelveNull()
    {
        await using var context = NuevoContexto();

        Assert.Null(await new ActividadRepository(context).GetByIdAsync("no-existe"));
    }

    [Fact]
    public async Task GetByIdAsync_CargaElSubarbolCompleto()
    {
        var raiz = new ActividadBuilder().WithId("raiz").WithTitulo("Proyecto").WithTipo(TipoActividad.Entrega).Build();
        var fase = new ActividadBuilder().WithId("fase").WithTitulo("Fase").WithPadre(raiz).Build();
        var tarea = new ActividadBuilder().WithId("tarea").WithTitulo("Tarea").WithPadre(fase).Build();
        new ActividadBuilder().WithId("paso").WithTitulo("Paso").WithPadre(tarea).Build();
        await SembrarAsync(raiz);

        // Contexto nuevo: el arbol tiene que venir del Include, no de entidades ya rastreadas.
        await using var context = NuevoContexto();
        var resultado = await new ActividadRepository(context).GetByIdAsync("raiz");

        Assert.NotNull(resultado);
        Assert.Equal(Actividad.ProfundidadMaximaAbsoluta, resultado.Altura());
        var paso = resultado.ActividadesHijas.Single().ActividadesHijas.Single().ActividadesHijas.Single();
        Assert.Equal("paso", paso.Id);
    }

    [Fact]
    public async Task AddAsync_PersisteLaActividad()
    {
        var actividad = new ActividadBuilder()
            .WithId("nueva")
            .WithTipo(TipoActividad.Examen)
            .WithCompletada(Fecha.AddDays(1))
            .Build();

        await using (var context = NuevoContexto())
            await new ActividadRepository(context).AddAsync(actividad);

        await using var verificacion = NuevoContexto();
        var guardada = await verificacion.Actividades.SingleAsync();
        Assert.Equal("nueva", guardada.Id);
        Assert.Equal(actividad.UserId, guardada.UserId);
        Assert.Equal(actividad.Titulo, guardada.Titulo);
        Assert.Equal(actividad.Descripcion, guardada.Descripcion);
        Assert.Equal(actividad.FechaInicio, guardada.FechaInicio);
        Assert.Equal(actividad.FechaFin, guardada.FechaFin);
        Assert.Equal(TipoActividad.Examen, guardada.Tipo);
        Assert.True(guardada.Completada);
        Assert.Equal(Fecha.AddDays(1), guardada.FechaCompletada);
    }

    [Fact]
    public async Task UpdateAsync_EntidadRastreada_PersisteLosCambios()
    {
        await SembrarAsync(new ActividadBuilder().WithId("existente").Build());

        await using (var context = NuevoContexto())
        {
            var repositorio = new ActividadRepository(context);
            var actividad = (await repositorio.GetByIdAsync("existente"))!;
            actividad.Titulo = "Titulo nuevo";
            actividad.Tipo = TipoActividad.Presentacion;
            await repositorio.UpdateAsync(actividad);
        }

        await using var verificacion = NuevoContexto();
        var guardada = await verificacion.Actividades.SingleAsync();
        Assert.Equal("Titulo nuevo", guardada.Titulo);
        Assert.Equal(TipoActividad.Presentacion, guardada.Tipo);
    }

    [Fact]
    public async Task UpdateAsync_EntidadDesconectada_PersisteLosCambios()
    {
        await SembrarAsync(new ActividadBuilder().WithId("existente").Build());
        var desconectada = new ActividadBuilder().WithId("existente").WithTitulo("Desde fuera").Build();

        await using (var context = NuevoContexto())
            await new ActividadRepository(context).UpdateAsync(desconectada);

        await using var verificacion = NuevoContexto();
        Assert.Equal("Desde fuera", (await verificacion.Actividades.SingleAsync()).Titulo);
    }

    [Fact]
    public async Task DeleteAsync_EliminaLaActividadYSusDescendientes()
    {
        var raiz = new ActividadBuilder().WithId("raiz").WithTitulo("Raiz").Build();
        var hija = new ActividadBuilder().WithId("hija").WithTitulo("Hija").WithPadre(raiz).Build();
        new ActividadBuilder().WithId("nieta").WithTitulo("Nieta").WithPadre(hija).Build();
        var otra = new ActividadBuilder().WithId("otra").WithTitulo("Otra").Build();
        await SembrarAsync(raiz, otra);

        await using (var context = NuevoContexto())
        {
            var repositorio = new ActividadRepository(context);
            await repositorio.DeleteAsync((await repositorio.GetByIdAsync("raiz"))!);
        }

        await using var verificacion = NuevoContexto();
        Assert.Equal(["otra"], await verificacion.Actividades.Select(a => a.Id).ToListAsync());
    }

    [Fact]
    public async Task ExistsDuplicateAsync_MismoUsuarioTituloYFechaFin_DevuelveTrue()
    {
        await SembrarAsync(new ActividadBuilder().WithId("existente").Build());
        await using var context = NuevoContexto();

        var existe = await new ActividadRepository(context)
            .ExistsDuplicateAsync("usuario-1", "Examen de Calculo", Fecha.AddDays(7), excludeId: null);

        Assert.True(existe);
    }

    [Theory]
    [InlineData("usuario-2", "Examen de Calculo", 7, null)]
    [InlineData("usuario-1", "Otro titulo", 7, null)]
    [InlineData("usuario-1", "Examen de Calculo", 8, null)]
    [InlineData("usuario-1", "Examen de Calculo", 7, "existente")]
    public async Task ExistsDuplicateAsync_SinCoincidenciaOExcluyendoseASiMisma_DevuelveFalse(
        string userId, string titulo, int diasFechaFin, string? excludeId)
    {
        await SembrarAsync(new ActividadBuilder().WithId("existente").Build());
        await using var context = NuevoContexto();

        var existe = await new ActividadRepository(context)
            .ExistsDuplicateAsync(userId, titulo, Fecha.AddDays(diasFechaFin), excludeId);

        Assert.False(existe);
    }
}
