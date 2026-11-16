using Microsoft.Extensions.Time.Testing;
using Moq;
using StudyTrack.Api.Delegates;
using StudyTrack.Api.Delegates.Exceptions;
using StudyTrack.Api.Domain;
using StudyTrack.Api.Repositories;
using StudyTrack.Tests.Builders;

namespace StudyTrack.Tests.Delegates;

public class ActividadDelegateTests
{
    private static readonly DateTime Ahora = ActividadBuilder.FechaBase;

    private readonly Mock<IActividadRepository> _repoMock;
    private readonly FakeTimeProvider _timeProvider;
    private readonly IActividadDelegate _delegate;

    public ActividadDelegateTests()
    {
        _repoMock = new Mock<IActividadRepository>();
        _repoMock.Setup(r => r.ExisteUsuarioAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(Ahora, TimeSpan.Zero));
        _delegate = new ActividadDelegate(_repoMock.Object, _timeProvider);
    }

    /// <summary>Hace que el repositorio encuentre estas actividades por ID.</summary>
    private void ConfigurarExistentes(params Actividad[] actividades)
    {
        foreach (var actividad in actividades)
        {
            _repoMock.Setup(r => r.GetByIdAsync(actividad.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(actividad);
        }
    }

    private static Actividad CreateActividadValida(string? actividadPadreId = null) => new()
    {
        UserId = "usuario-1",
        Titulo = "Proyecto final",
        Descripcion = "Microservicio REST",
        FechaFin = Ahora.AddDays(10),
        Tipo = TipoActividad.Entrega,
        ActividadPadreId = actividadPadreId
    };

    private static Actividad UpdateActividadDe(Actividad actividad) => new()
    {
        Id = actividad.Id,
        UserId = actividad.UserId,
        Titulo = actividad.Titulo,
        Descripcion = actividad.Descripcion,
        FechaFin = actividad.FechaFin,
        Tipo = actividad.Tipo,
        Completada = actividad.Completada,
        ActividadPadreId = actividad.ActividadPadreId
    };

    // ---------- GetByIdAsync ----------

    [Fact]
    public async Task GetByIdAsync_Existente_DevuelveDtoMapeado()
    {
        var actividad = new ActividadBuilder()
            .WithId("actividad-1")
            .WithTipo(TipoActividad.Examen)
            .WithCompletada(Ahora.AddDays(1))
            .WithGeneradaAutomaticamente(true)
            .Build();
        ConfigurarExistentes(actividad);

        var dto = await _delegate.GetByIdAsync("actividad-1");

        Assert.NotNull(dto);
        Assert.Equal("actividad-1", dto.Id);
        Assert.Equal(actividad.UserId, dto.UserId);
        Assert.Equal(actividad.Titulo, dto.Titulo);
        Assert.Equal(actividad.Descripcion, dto.Descripcion);
        Assert.Equal(actividad.FechaInicio, dto.FechaInicio);
        Assert.Equal(actividad.FechaFin, dto.FechaFin);
        Assert.Equal(TipoActividad.Examen, dto.Tipo);
        Assert.True(dto.Completada);
        Assert.Equal(Ahora.AddDays(1), dto.FechaCompletada);
        Assert.True(dto.GeneradaAutomaticamente);
        Assert.Null(dto.ActividadPadreId);
        Assert.Empty(dto.ActividadesHijas);
        _repoMock.Verify(r => r.GetByIdAsync("actividad-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_Inexistente_DevuelveNull()
    {
        _repoMock.Setup(r => r.GetByIdAsync("no-existe", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Actividad?)null);

        Assert.Null(await _delegate.GetByIdAsync("no-existe"));
        _repoMock.Verify(r => r.GetByIdAsync("no-existe", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_MapeaEntidadesAnidadasEnOrden()
    {
        var raiz = new ActividadBuilder().WithId("raiz").WithTipo(TipoActividad.Entrega).Build();
        var segunda = new ActividadBuilder().WithId("fase-b").WithFechaInicio(Ahora.AddHours(2)).WithPadre(raiz).Build();
        var primera = new ActividadBuilder().WithId("fase-a").WithFechaInicio(Ahora.AddHours(1)).WithPadre(raiz).Build();
        new ActividadBuilder().WithId("tarea").WithPadre(primera).Build();
        ConfigurarExistentes(raiz);

        var dto = await _delegate.GetByIdAsync("raiz");

        Assert.NotNull(dto);
        Assert.Equal(["fase-a", "fase-b"], dto.ActividadesHijas.Select(h => h.Id));
        var tarea = Assert.Single(dto.ActividadesHijas[0].ActividadesHijas);
        Assert.Equal("tarea", tarea.Id);
        Assert.Equal("fase-a", tarea.ActividadPadreId);
        Assert.Empty(dto.ActividadesHijas[1].ActividadesHijas);
        Assert.Equal(segunda.Titulo, dto.ActividadesHijas[1].Titulo);
        _repoMock.Verify(r => r.GetByIdAsync("raiz", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------- GetAllAsync ----------

    [Fact]
    public async Task GetAllAsync_SinActividades_DevuelveListaVacia()
    {
        _repoMock.Setup(r => r.GetAllAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        Assert.Empty(await _delegate.GetAllAsync());
        _repoMock.Verify(r => r.GetAllAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllAsync_VariasActividades_DevuelveRaicesConSusHijasAnidadas()
    {
        // Lista plana y sin navegaciones cargadas, como la devuelve el repositorio.
        var raizTardia = new ActividadBuilder().WithId("raiz-b").WithTitulo("Tardia").WithFechaInicio(Ahora.AddDays(1)).Build();
        var raizTemprana = new ActividadBuilder().WithId("raiz-a").WithTitulo("Temprana").WithFechaInicio(Ahora).Build();
        var hija = new ActividadBuilder().WithId("hija").WithTitulo("Hija").Build();
        hija.ActividadPadreId = "raiz-a";
        _repoMock.Setup(r => r.GetAllAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([raizTardia, hija, raizTemprana]);

        var resultado = await _delegate.GetAllAsync();

        Assert.Equal(["raiz-a", "raiz-b"], resultado.Select(a => a.Id));
        Assert.Equal("Temprana", resultado[0].Titulo);
        var hijaDto = Assert.Single(resultado[0].ActividadesHijas);
        Assert.Equal("hija", hijaDto.Id);
        Assert.Equal("raiz-a", hijaDto.ActividadPadreId);
        Assert.Empty(resultado[1].ActividadesHijas);
        _repoMock.Verify(r => r.GetAllAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllAsync_ConUsuario_PropagaFiltroAlRepositorio()
    {
        _repoMock.Setup(r => r.GetAllAsync("usuario-juan", It.IsAny<CancellationToken>())).ReturnsAsync([]);

        await _delegate.GetAllAsync(callerUserId: "usuario-juan");

        _repoMock.Verify(r => r.GetAllAsync("usuario-juan", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------- GetCalendarioAsync ----------

    [Fact]
    public async Task GetCalendarioAsync_RangoInvertido_LanzaBusinessRuleException()
    {
        var desde = Ahora.AddDays(5);
        var hasta = Ahora.AddDays(2);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _delegate.GetCalendarioAsync(desde, hasta));

        Assert.Equal("rango", ex.Errors.Keys.Single());
        _repoMock.Verify(r => r.GetPorRangoFechasAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetCalendarioAsync_RangoValido_ConsultaRepositorioYDevuelveDtosOrdenados()
    {
        var desde = Ahora;
        var hasta = Ahora.AddDays(10);

        var actividad1 = new ActividadBuilder().WithId("act-1").WithFechaInicio(Ahora.AddDays(1)).Build();
        var actividad2 = new ActividadBuilder().WithId("act-2").WithFechaInicio(Ahora.AddDays(3)).Build();

        _repoMock.Setup(r => r.GetPorRangoFechasAsync(desde, hasta, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([actividad2, actividad1]);

        var resultado = await _delegate.GetCalendarioAsync(desde, hasta);

        Assert.Equal(["act-1", "act-2"], resultado.Select(a => a.Id));
        _repoMock.Verify(r => r.GetPorRangoFechasAsync(desde, hasta, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCalendarioAsync_ConUsuario_PropagaFiltroAlRepositorio()
    {
        var desde = Ahora;
        var hasta = Ahora.AddDays(10);

        _repoMock.Setup(r => r.GetPorRangoFechasAsync(desde, hasta, "usuario-juan", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await _delegate.GetCalendarioAsync(desde, hasta, callerUserId: "usuario-juan");

        _repoMock.Verify(r => r.GetPorRangoFechasAsync(desde, hasta, "usuario-juan", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCalendarioAsync_ConActividadPadreEHija_NoDuplicaHijaEnNivelSuperior()
    {
        var desde = Ahora;
        var hasta = Ahora.AddDays(15);

        var raiz = new ActividadBuilder().WithId("raiz").WithTitulo("Padre").WithTipo(TipoActividad.Entrega)
            .WithFechaInicio(Ahora.AddDays(1)).WithFechaFin(Ahora.AddDays(14)).Build();
        var hija = new ActividadBuilder().WithId("hija").WithTitulo("Hija").WithPadre(raiz)
            .WithFechaInicio(Ahora.AddDays(2)).WithFechaFin(Ahora.AddDays(5)).Build();

        _repoMock.Setup(r => r.GetPorRangoFechasAsync(desde, hasta, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([raiz, hija]);

        var resultado = await _delegate.GetCalendarioAsync(desde, hasta);

        // La raiz debe ser el unico elemento en el nivel superior
        var unicoEnRaiz = Assert.Single(resultado);
        Assert.Equal("raiz", unicoEnRaiz.Id);

        // La hija debe estar anidada dentro de la coleccion ActividadesHijas
        var hijaAnidada = Assert.Single(unicoEnRaiz.ActividadesHijas);
        Assert.Equal("hija", hijaAnidada.Id);
    }

    [Fact]
    public async Task GetCalendarioAsync_HijaEnRangoPeroPadreFueraDeRango_DevuelveHijaEnNivelSuperior()
    {
        var desde = Ahora.AddDays(10);
        var hasta = Ahora.AddDays(15);

        // El padre esta en otro mes, solo la hija cae en este rango de calendario
        var hijaSola = new ActividadBuilder().WithId("hija-sola")
            .WithFechaInicio(Ahora.AddDays(11)).WithFechaFin(Ahora.AddDays(13)).Build();
        hijaSola.ActividadPadreId = "padre-fuera-de-rango";

        _repoMock.Setup(r => r.GetPorRangoFechasAsync(desde, hasta, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([hijaSola]);

        var resultado = await _delegate.GetCalendarioAsync(desde, hasta);

        var unica = Assert.Single(resultado);
        Assert.Equal("hija-sola", unica.Id);
    }

    // ---------- CreateAsync ----------

    [Fact]
    public async Task CreateAsync_GeneraIdGuardaEnRepositorioYLoDevuelve()
    {
        Actividad? guardada = null;
        _repoMock.Setup(r => r.AddAsync(It.IsAny<Actividad>(), It.IsAny<CancellationToken>()))
            .Callback<Actividad, CancellationToken>((a, _) => guardada = a)
            .Returns(Task.CompletedTask);
        var dto = CreateActividadValida();

        var id = await _delegate.CreateAsync(dto);

        Assert.True(Guid.TryParse(id, out _));
        Assert.NotNull(guardada);
        Assert.Equal(id, guardada.Id);
        Assert.Equal(dto.UserId, guardada.UserId);
        Assert.Equal(dto.Titulo, guardada.Titulo);
        Assert.Equal(dto.Descripcion, guardada.Descripcion);
        Assert.Equal(Ahora, guardada.FechaInicio);
        Assert.Equal(dto.FechaFin, guardada.FechaFin);
        Assert.Equal(TipoActividad.Entrega, guardada.Tipo);
        Assert.False(guardada.Completada);
        Assert.Null(guardada.FechaCompletada);
        Assert.False(guardada.GeneradaAutomaticamente);
        Assert.Null(guardada.ActividadPadreId);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<Actividad>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_FechaInicioEsElMomentoDeCreacion()
    {
        Actividad? guardada = null;
        _repoMock.Setup(r => r.AddAsync(It.IsAny<Actividad>(), It.IsAny<CancellationToken>()))
            .Callback<Actividad, CancellationToken>((a, _) => guardada = a);
        _timeProvider.Advance(TimeSpan.FromHours(3));

        await _delegate.CreateAsync(CreateActividadValida());

        Assert.Equal(Ahora.AddHours(3), guardada!.FechaInicio);
        Assert.Equal(DateTimeKind.Utc, guardada.FechaInicio.Kind);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<Actividad>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_ValidaRestriccionDeUnicidad()
    {
        var dto = CreateActividadValida();
        _repoMock.Setup(r => r.ExistsDuplicateAsync(dto.UserId, dto.Titulo, dto.FechaFin, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var excepcion = await Assert.ThrowsAsync<BusinessRuleException>(() => _delegate.CreateAsync(dto));

        Assert.Contains("titulo", excepcion.Errors.Keys);
        _repoMock.Verify(r => r.ExistsDuplicateAsync(dto.UserId, dto.Titulo, dto.FechaFin, null, It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<Actividad>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_FechaFinAnteriorALaCreacion_Lanza()
    {
        var dto = CreateActividadValida() with { FechaFin = Ahora.AddMinutes(-1) };

        var excepcion = await Assert.ThrowsAsync<BusinessRuleException>(() => _delegate.CreateAsync(dto));

        Assert.Contains("fechaFin", excepcion.Errors.Keys);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<Actividad>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ConPadreValido_EnlazaAlPadre()
    {
        var padre = new ActividadBuilder().WithId("padre").WithTipo(TipoActividad.Examen).Build();
        ConfigurarExistentes(padre);
        Actividad? guardada = null;
        _repoMock.Setup(r => r.AddAsync(It.IsAny<Actividad>(), It.IsAny<CancellationToken>()))
            .Callback<Actividad, CancellationToken>((a, _) => guardada = a);

        await _delegate.CreateAsync(CreateActividadValida("padre"));

        Assert.Equal("padre", guardada!.ActividadPadreId);
        _repoMock.Verify(r => r.AddAsync(It.Is<Actividad>(a => a.ActividadPadreId == "padre"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_PadreInexistente_Lanza()
    {
        var excepcion = await Assert.ThrowsAsync<BusinessRuleException>(() => _delegate.CreateAsync(CreateActividadValida("no-existe")));

        Assert.Contains("actividadPadreId", excepcion.Errors.Keys);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<Actividad>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_PadreDeOtroUsuario_Lanza()
    {
        ConfigurarExistentes(new ActividadBuilder().WithId("ajena").WithUserId("usuario-2").Build());

        var excepcion = await Assert.ThrowsAsync<BusinessRuleException>(() => _delegate.CreateAsync(CreateActividadValida("ajena")));

        Assert.Contains("otro usuario", excepcion.Errors["actividadPadreId"][0]);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<Actividad>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_SuperaLaProfundidadDelTipoDeLaRaiz_Lanza()
    {
        // Raiz General (limite 1) -> hija en nivel 1: una nieta quedaria en nivel 2.
        var raiz = new ActividadBuilder().WithId("raiz").WithTipo(TipoActividad.General).Build();
        var hija = new ActividadBuilder().WithId("hija").WithPadre(raiz).Build();
        ConfigurarExistentes(raiz, hija);

        var excepcion = await Assert.ThrowsAsync<BusinessRuleException>(() => _delegate.CreateAsync(CreateActividadValida("hija")));

        Assert.Contains("profundidad maxima", excepcion.Errors["actividadPadreId"][0]);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<Actividad>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_EntregaAdmiteTresNivelesDeSubactividades()
    {
        var raiz = new ActividadBuilder().WithId("raiz").WithTipo(TipoActividad.Entrega).Build();
        var fase = new ActividadBuilder().WithId("fase").WithTipo(TipoActividad.General).WithPadre(raiz).Build();
        var tarea = new ActividadBuilder().WithId("tarea").WithPadre(fase).Build();
        ConfigurarExistentes(raiz, fase, tarea);

        await _delegate.CreateAsync(CreateActividadValida("tarea"));

        _repoMock.Verify(r => r.AddAsync(It.Is<Actividad>(a => a.ActividadPadreId == "tarea"), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------- UpdateAsync ----------

    [Fact]
    public async Task UpdateAsync_ActualizaTodosLosCamposYDevuelveDtoActualizado()
    {
        var actividad = new ActividadBuilder().WithId("actividad-1").Build();
        var nuevoPadre = new ActividadBuilder().WithId("nuevo-padre").WithTipo(TipoActividad.Examen).Build();
        ConfigurarExistentes(actividad, nuevoPadre);
        var dto = new Actividad
        {
            Titulo = "Titulo nuevo",
            Descripcion = "Descripcion nueva",
            FechaFin = Ahora.AddDays(30),
            Tipo = TipoActividad.Presentacion,
            Completada = true,
            ActividadPadreId = "nuevo-padre"
        };

        var resultado = await _delegate.UpdateAsync("actividad-1", dto);

        Assert.Equal("Titulo nuevo", resultado.Titulo);
        Assert.Equal("Descripcion nueva", resultado.Descripcion);
        Assert.Equal(Ahora.AddDays(30), resultado.FechaFin);
        Assert.Equal(TipoActividad.Presentacion, resultado.Tipo);
        Assert.True(resultado.Completada);
        Assert.Equal(Ahora, resultado.FechaCompletada);
        Assert.Equal("nuevo-padre", resultado.ActividadPadreId);
        Assert.Equal(ActividadBuilder.FechaBase, resultado.FechaInicio);
        _repoMock.Verify(r => r.UpdateAsync(
            It.Is<Actividad>(a => a.Titulo == "Titulo nuevo" && a.ActividadPadreId == "nuevo-padre"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_Inexistente_LanzaNotFound()
    {
        var dto = UpdateActividadDe(new ActividadBuilder().Build());

        await Assert.ThrowsAsync<NotFoundException>(() => _delegate.UpdateAsync("no-existe", dto));

        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<Actividad>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_AlCompletar_FijaFechaCompletadaConLaHoraActual()
    {
        var actividad = new ActividadBuilder().Build();
        ConfigurarExistentes(actividad);
        _timeProvider.Advance(TimeSpan.FromDays(2));

        var resultado = await _delegate.UpdateAsync(actividad.Id, UpdateActividadDe(actividad) with { Completada = true });

        Assert.Equal(Ahora.AddDays(2), resultado.FechaCompletada);
        _repoMock.Verify(r => r.UpdateAsync(It.Is<Actividad>(a => a.Completada && a.FechaCompletada == Ahora.AddDays(2)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_YaCompletada_ConservaLaFechaCompletadaOriginal()
    {
        var actividad = new ActividadBuilder().WithCompletada(Ahora.AddDays(1)).Build();
        ConfigurarExistentes(actividad);
        _timeProvider.Advance(TimeSpan.FromDays(5));

        var resultado = await _delegate.UpdateAsync(actividad.Id, UpdateActividadDe(actividad));

        Assert.Equal(Ahora.AddDays(1), resultado.FechaCompletada);
        _repoMock.Verify(r => r.UpdateAsync(It.Is<Actividad>(a => a.Completada && a.FechaCompletada == Ahora.AddDays(1)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_AlReabrir_LimpiaFechaCompletada()
    {
        var actividad = new ActividadBuilder().WithCompletada(Ahora.AddDays(1)).Build();
        ConfigurarExistentes(actividad);

        var resultado = await _delegate.UpdateAsync(actividad.Id, UpdateActividadDe(actividad) with { Completada = false });

        Assert.False(resultado.Completada);
        Assert.Null(resultado.FechaCompletada);
        _repoMock.Verify(r => r.UpdateAsync(It.Is<Actividad>(a => !a.Completada && a.FechaCompletada == null), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_FechaFinAnteriorAFechaInicio_Lanza()
    {
        var actividad = new ActividadBuilder().Build();
        ConfigurarExistentes(actividad);

        var excepcion = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _delegate.UpdateAsync(actividad.Id, UpdateActividadDe(actividad) with { FechaFin = actividad.FechaInicio.AddDays(-1) }));

        Assert.Contains("fechaFin", excepcion.Errors.Keys);
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<Actividad>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_Duplicado_ExcluyendoseASiMisma_Lanza()
    {
        var actividad = new ActividadBuilder().Build();
        ConfigurarExistentes(actividad);
        var dto = UpdateActividadDe(actividad) with { Titulo = "Repetido" };
        _repoMock.Setup(r => r.ExistsDuplicateAsync(actividad.UserId, "Repetido", actividad.FechaFin, actividad.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var excepcion = await Assert.ThrowsAsync<BusinessRuleException>(() => _delegate.UpdateAsync(actividad.Id, dto));

        Assert.Contains("titulo", excepcion.Errors.Keys);
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<Actividad>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_PadreEsLaMismaActividad_Lanza()
    {
        var actividad = new ActividadBuilder().WithId("actividad-1").Build();
        ConfigurarExistentes(actividad);

        var excepcion = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _delegate.UpdateAsync("actividad-1", UpdateActividadDe(actividad) with { ActividadPadreId = "actividad-1" }));

        Assert.Contains("propia actividad padre", excepcion.Errors["actividadPadreId"][0]);
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<Actividad>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_PadreEsUnaDescendiente_Lanza()
    {
        var raiz = new ActividadBuilder().WithId("raiz").WithTipo(TipoActividad.Entrega).Build();
        var hija = new ActividadBuilder().WithId("hija").WithPadre(raiz).Build();
        ConfigurarExistentes(raiz, hija);

        var excepcion = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _delegate.UpdateAsync("raiz", UpdateActividadDe(raiz) with { ActividadPadreId = "hija" }));

        Assert.Contains("subactividades", excepcion.Errors["actividadPadreId"][0]);
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<Actividad>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_MoverSubarbolFueraDelLimite_Lanza()
    {
        // Mover una actividad con una hija bajo una raiz Examen (limite 2) que ya tiene un nivel:
        // la actividad quedaria en nivel 2 y su hija en nivel 3.
        var raizExamen = new ActividadBuilder().WithId("examen").WithTipo(TipoActividad.Examen).Build();
        var tema = new ActividadBuilder().WithId("tema").WithPadre(raizExamen).Build();
        var movida = new ActividadBuilder().WithId("movida").WithTitulo("Movida").Build();
        new ActividadBuilder().WithId("hija-movida").WithPadre(movida).Build();
        ConfigurarExistentes(raizExamen, tema, movida);

        var excepcion = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _delegate.UpdateAsync("movida", UpdateActividadDe(movida) with { ActividadPadreId = "tema" }));

        Assert.Contains("profundidad maxima", excepcion.Errors["actividadPadreId"][0]);
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<Actividad>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_CambiarTipoDeRaizDejaHijasFueraDelLimite_Lanza()
    {
        var raiz = new ActividadBuilder().WithId("raiz").WithTipo(TipoActividad.Examen).Build();
        var tema = new ActividadBuilder().WithId("tema").WithPadre(raiz).Build();
        new ActividadBuilder().WithId("subtema").WithPadre(tema).Build();
        ConfigurarExistentes(raiz);

        var excepcion = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _delegate.UpdateAsync("raiz", UpdateActividadDe(raiz) with { Tipo = TipoActividad.General }));

        Assert.Contains("tipo", excepcion.Errors.Keys);
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<Actividad>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_QuitarElPadre_LaConvierteEnRaiz()
    {
        var padre = new ActividadBuilder().WithId("padre").Build();
        var hija = new ActividadBuilder().WithId("hija").WithTitulo("Hija").WithPadre(padre).Build();
        ConfigurarExistentes(padre, hija);

        var resultado = await _delegate.UpdateAsync("hija", UpdateActividadDe(hija) with { ActividadPadreId = null });

        Assert.Null(resultado.ActividadPadreId);
        _repoMock.Verify(r => r.UpdateAsync(It.Is<Actividad>(a => a.ActividadPadreId == null), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------- DeleteAsync ----------

    [Fact]
    public async Task DeleteAsync_Existente_EliminaMedianteElRepositorio()
    {
        var actividad = new ActividadBuilder().Build();
        ConfigurarExistentes(actividad);

        await _delegate.DeleteAsync(actividad.Id, callerUserId: actividad.UserId);

        _repoMock.Verify(r => r.DeleteAsync(actividad, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_Inexistente_LanzaNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _delegate.DeleteAsync("no-existe", callerUserId: "usuario-1"));

        _repoMock.Verify(r => r.DeleteAsync(It.IsAny<Actividad>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_ManejaIntegridadReferencial_EntregaElSubarbolAlRepositorio()
    {
        var raiz = new ActividadBuilder().WithId("raiz").Build();
        new ActividadBuilder().WithId("hija").WithPadre(raiz).Build();
        ConfigurarExistentes(raiz);

        await _delegate.DeleteAsync("raiz", callerUserId: raiz.UserId);

        _repoMock.Verify(r => r.DeleteAsync(
            It.Is<Actividad>(a => a.Id == "raiz" && a.ActividadesHijas.Single().Id == "hija"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DeleteAsync_SinUsuario_LanzaForbiddenException(string? callerUserId)
    {
        var actividad = new ActividadBuilder().WithId("act-1").WithUserId("usuario-1").Build();
        ConfigurarExistentes(actividad);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => _delegate.DeleteAsync("act-1", callerUserId: callerUserId));

        _repoMock.Verify(r => r.DeleteAsync(It.IsAny<Actividad>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------- Autorizacion y Compartir ----------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CompartirAsync_SinUsuario_LanzaForbiddenException(string? callerUserId)
    {
        var actividad = new ActividadBuilder().WithId("act-1").WithUserId("usuario-1").Build();
        ConfigurarExistentes(actividad);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => _delegate.CompartirAsync("act-1", "usuario-2", callerUserId: callerUserId));

        _repoMock.Verify(r => r.CompartirAsync(It.IsAny<Actividad>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CompartirAsync_UsuarioNoEsDuenio_LanzaForbiddenException()
    {
        var actividad = new ActividadBuilder().WithId("act-1").WithUserId("usuario-1").Build();
        ConfigurarExistentes(actividad);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => _delegate.CompartirAsync("act-1", "usuario-2", callerUserId: "usuario-ajeno"));

        _repoMock.Verify(r => r.CompartirAsync(It.IsAny<Actividad>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CompartirAsync_CompartirConUnoMismo_LanzaBusinessRuleException()
    {
        var actividad = new ActividadBuilder().WithId("act-1").WithUserId("usuario-1").Build();
        ConfigurarExistentes(actividad);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _delegate.CompartirAsync("act-1", "usuario-1", callerUserId: "usuario-1"));

        Assert.Equal("usuarioId", ex.Errors.Keys.Single());
        _repoMock.Verify(r => r.CompartirAsync(It.IsAny<Actividad>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CompartirAsync_UsuarioDestinoNoExiste_LanzaBadRequestException()
    {
        var actividad = new ActividadBuilder().WithId("act-1").WithUserId("usuario-1").Build();
        ConfigurarExistentes(actividad);
        _repoMock.Setup(r => r.ExisteUsuarioAsync("usuario-inexistente", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var excepcion = await Assert.ThrowsAsync<BadRequestException>(
            () => _delegate.CompartirAsync("act-1", "usuario-inexistente", callerUserId: "usuario-1"));

        Assert.Equal("usuarioId", excepcion.Campo);
        _repoMock.Verify(r => r.CompartirAsync(It.IsAny<Actividad>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CompartirAsync_DuenioComparte_LlamaRepositorio()
    {
        var actividad = new ActividadBuilder().WithId("act-1").WithUserId("usuario-1").Build();
        ConfigurarExistentes(actividad);

        await _delegate.CompartirAsync("act-1", "usuario-2", callerUserId: "usuario-1");

        _repoMock.Verify(r => r.CompartirAsync(actividad, "usuario-2", Ahora, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_UsuarioAjeno_LanzaForbiddenException()
    {
        var actividad = new ActividadBuilder().WithId("act-1").WithUserId("usuario-1").Build();
        ConfigurarExistentes(actividad);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => _delegate.GetByIdAsync("act-1", callerUserId: "usuario-ajeno"));
    }

    [Fact]
    public async Task GetByIdAsync_Colaborador_DevuelveDto()
    {
        var actividad = new ActividadBuilder().WithId("act-1").WithUserId("usuario-1").Build();
        actividad.CompartirCon("usuario-2", Ahora);
        ConfigurarExistentes(actividad);

        var dto = await _delegate.GetByIdAsync("act-1", callerUserId: "usuario-2");

        Assert.NotNull(dto);
        Assert.Equal("act-1", dto.Id);
        Assert.NotNull(dto.Colaboradores);
        Assert.Contains(dto.Colaboradores, c => c.UsuarioId == "usuario-2");
    }

    [Fact]
    public async Task DeleteAsync_ColaboradorIntentaEliminar_LanzaForbiddenException()
    {
        var actividad = new ActividadBuilder().WithId("act-1").WithUserId("usuario-1").Build();
        actividad.CompartirCon("usuario-colaborador", Ahora);
        ConfigurarExistentes(actividad);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => _delegate.DeleteAsync("act-1", callerUserId: "usuario-colaborador"));

        _repoMock.Verify(r => r.DeleteAsync(It.IsAny<Actividad>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------- Punto 6: Colaboradores en Creación y Actualización ----------

    [Fact]
    public async Task CreateAsync_ConColaboradoresValidos_AsociaColaboradoresYGuarda()
    {
        var actividad = CreateActividadValida();
        actividad.Colaboradores = new List<ActividadCompartida>
        {
            new() { UsuarioId = "usuario-2" },
            new() { UsuarioId = "usuario-3" }
        };

        var id = await _delegate.CreateAsync(actividad);

        Assert.Equal(actividad.Id, id);
        _repoMock.Verify(r => r.AddAsync(
            It.Is<Actividad>(a =>
                a.Colaboradores != null &&
                a.Colaboradores.Count == 2 &&
                a.Colaboradores.All(c => c.ActividadId == a.Id && c.FechaCompartida == Ahora)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_ConColaboradorInexistente_LanzaBadRequestException()
    {
        _repoMock.Setup(r => r.ExisteUsuarioAsync("no-existe", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var actividad = CreateActividadValida();
        actividad.Colaboradores = new List<ActividadCompartida>
        {
            new() { UsuarioId = "no-existe" }
        };

        var excepcion = await Assert.ThrowsAsync<BadRequestException>(() => _delegate.CreateAsync(actividad));

        Assert.Equal("colaboradores", excepcion.Campo);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<Actividad>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ConCreadorComoColaborador_LanzaBusinessRuleException()
    {
        var actividad = CreateActividadValida();
        actividad.Colaboradores = new List<ActividadCompartida>
        {
            new() { UsuarioId = actividad.UserId }
        };

        var excepcion = await Assert.ThrowsAsync<BusinessRuleException>(() => _delegate.CreateAsync(actividad));

        Assert.Contains("colaboradores", excepcion.Errors.Keys);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<Actividad>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_ConNuevosColaboradores_ActualizaColaboradores()
    {
        var actividad = new ActividadBuilder().WithId("act-1").WithUserId("usuario-1").Build();
        actividad.CompartirCon("usuario-2", Ahora.AddDays(-1));
        ConfigurarExistentes(actividad);

        var dto = new Actividad
        {
            Titulo = "Titulo modificado",
            FechaFin = Ahora.AddDays(15),
            Tipo = TipoActividad.General,
            Colaboradores = new List<ActividadCompartida>
            {
                new() { UsuarioId = "usuario-3" }
            }
        };

        var resultado = await _delegate.UpdateAsync("act-1", dto, callerUserId: "usuario-1");

        Assert.NotNull(resultado.Colaboradores);
        Assert.Single(resultado.Colaboradores);
        Assert.Contains(resultado.Colaboradores, c => c.UsuarioId == "usuario-3");
        _repoMock.Verify(r => r.UpdateAsync(
            It.Is<Actividad>(a =>
                a.Colaboradores != null &&
                a.Colaboradores.Count == 1 &&
                a.Colaboradores.Any(c => c.UsuarioId == "usuario-3")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ColaboradorIntentaModificarColaboradores_LanzaForbiddenException()
    {
        var actividad = new ActividadBuilder().WithId("act-1").WithUserId("usuario-1").Build();
        actividad.CompartirCon("usuario-colaborador", Ahora);
        ConfigurarExistentes(actividad);

        var dto = new Actividad
        {
            Titulo = "Titulo modificado",
            FechaFin = Ahora.AddDays(15),
            Tipo = TipoActividad.General,
            Colaboradores = new List<ActividadCompartida>
            {
                new() { UsuarioId = "usuario-ajeno" }
            }
        };

        await Assert.ThrowsAsync<ForbiddenException>(
            () => _delegate.UpdateAsync("act-1", dto, callerUserId: "usuario-colaborador"));

        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<Actividad>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
