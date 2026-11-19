using System.Net;
using System.Net.Http.Json;
using System.Text;
using StudyTrack.Api.Domain;
using StudyTrack.Tests.Builders;

namespace StudyTrack.Tests.Routes;

/// <summary>
/// Contrato: el solicitante es siempre "userId", en la query string (GET y DELETE) o en el cuerpo (POST y PUT).
/// Ninguna ruta lee headers para identificar al usuario.
/// </summary>
public class ActividadRoutesTests : StudyTrackApiTests
{
    private static object CreateBody(
        string userId = "usuario-1",
        string titulo = "Proyecto final",
        string tipo = "Entrega",
        string? actividadPadreId = null) => new
    {
        userId,
        titulo,
        descripcion = "Microservicio REST",
        fechaFin = Ahora.AddDays(10),
        tipo,
        actividadPadreId
    };

    private static object UpdateBody(
        string userId = "usuario-1",
        string titulo = "Titulo actualizado",
        bool completada = false,
        DateTime? fechaFin = null,
        string tipo = "Examen",
        string? actividadPadreId = null) => new
    {
        userId,
        titulo,
        descripcion = "Descripcion actualizada",
        fechaFin = fechaFin ?? Ahora.AddDays(20),
        tipo,
        completada,
        actividadPadreId
    };

    /// <summary>Fecha en formato ISO 8601 con "Z", lista para la query string.</summary>
    private static string Iso(DateTime fecha) => Uri.EscapeDataString(fecha.ToString("o"));

    private static StringContent JsonCrudo(string json) => new(json, Encoding.UTF8, "application/json");

    // ---------- GET /actividades ----------

    [Fact]
    public async Task GetAll_SinActividades_Devuelve200ConArregloVacio()
    {
        var respuesta = await Client.GetAsync("/actividades?userId=usuario-1");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Empty((await LeerAsync<Actividad[]>(respuesta))!);
    }

    [Fact]
    public async Task GetAll_ConActividades_Devuelve200ConActividadesYSusHijasAnidadas()
    {
        var raiz = new ActividadBuilder().WithId("raiz").WithTitulo("Raiz").WithTipo(TipoActividad.Examen).Build();
        new ActividadBuilder().WithId("hija").WithTitulo("Hija").WithPadre(raiz).Build();
        var otra = new ActividadBuilder().WithId("otra").WithTitulo("Otra").WithFechaInicio(Ahora.AddDays(1)).Build();
        await SembrarAsync(raiz, otra);

        var respuesta = await Client.GetAsync("/actividades?userId=usuario-1");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var actividades = (await LeerAsync<Actividad[]>(respuesta))!;
        Assert.Equal(["raiz", "otra"], actividades.Select(a => a.Id));
        Assert.Equal("hija", Assert.Single(actividades[0].ActividadesHijas).Id);
    }

    [Theory]
    [InlineData("/actividades")]
    [InlineData("/actividades?userId=")]
    [InlineData("/actividades?userId=usuario_1")]
    public async Task GetAll_SinUsuarioOConFormatoInvalido_Devuelve400(string ruta)
    {
        var respuesta = await Client.GetAsync(ruta);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.True((await LeerJsonAsync(respuesta)).GetProperty("errors").TryGetProperty("userId", out _));
    }

    [Fact]
    public async Task GetAll_HeaderXUserId_SeIgnora()
    {
        await SembrarAsync(new ActividadBuilder().WithId("actividad-1").Build());
        using var request = new HttpRequestMessage(HttpMethod.Get, "/actividades");
        request.Headers.Add("X-User-Id", "usuario-1");

        var respuesta = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task GetAll_ConFiltroUsuario_DevuelveSoloActividadesPropiasYCompartidas()
    {
        var propia = new ActividadBuilder().WithId("getall-propia").WithUserId("usuario-1").Build();
        var compartida = new ActividadBuilder().WithId("getall-compartida").WithUserId("usuario-2").Build();
        compartida.CompartirCon("usuario-1", Ahora);
        var ajena = new ActividadBuilder().WithId("getall-ajena").WithUserId("usuario-ajeno").Build();

        await SembrarAsync(propia, compartida, ajena);

        var respuesta = await Client.GetAsync("/actividades?userId=usuario-1");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var ids = (await LeerAsync<Actividad[]>(respuesta))!.Select(a => a.Id).ToList();
        Assert.Contains("getall-propia", ids);
        Assert.Contains("getall-compartida", ids);
        Assert.DoesNotContain("getall-ajena", ids);
    }

    [Fact]
    public async Task GetAll_SubactividadCompartidaSinSuPadre_ApareceComoRaizParaElColaborador()
    {
        var padre = new ActividadBuilder().WithId("padre").WithUserId("usuario-1").WithTipo(TipoActividad.Examen).Build();
        var hija = new ActividadBuilder().WithId("hija").WithUserId("usuario-1").WithTitulo("Hija").WithPadre(padre).Build();
        hija.CompartirCon("usuario-2", Ahora);
        await SembrarAsync(padre);

        var actividades = (await Client.GetFromJsonAsync<Actividad[]>("/actividades?userId=usuario-2", Json))!;

        Assert.Equal("hija", Assert.Single(actividades).Id);
    }

    [Fact]
    public async Task GetAll_RaizCompartida_IncluyeSusSubactividadesAnidadas()
    {
        var raiz = new ActividadBuilder().WithId("raiz").WithUserId("usuario-1").WithTipo(TipoActividad.Entrega).Build();
        var fase = new ActividadBuilder().WithId("fase").WithUserId("usuario-1").WithTitulo("Fase").WithPadre(raiz).Build();
        new ActividadBuilder().WithId("tarea").WithUserId("usuario-1").WithTitulo("Tarea").WithPadre(fase).Build();
        raiz.CompartirCon("usuario-2", Ahora);
        await SembrarAsync(raiz);

        var actividades = (await Client.GetFromJsonAsync<Actividad[]>("/actividades?userId=usuario-2", Json))!;

        var unica = Assert.Single(actividades);
        Assert.Equal("tarea", Assert.Single(Assert.Single(unica.ActividadesHijas).ActividadesHijas).Id);
    }

    // ---------- GET /actividades/calendario ----------

    [Fact]
    public async Task GetCalendario_SinParametros_Devuelve400()
    {
        var respuesta = await Client.GetAsync("/actividades/calendario");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task GetCalendario_SinUsuario_Devuelve400()
    {
        var respuesta = await Client.GetAsync($"/actividades/calendario?desde={Iso(Ahora)}&hasta={Iso(Ahora.AddDays(1))}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.True((await LeerJsonAsync(respuesta)).GetProperty("errors").TryGetProperty("userId", out _));
    }

    [Theory]
    [InlineData("2026-09-01", "desde")]
    [InlineData("2026-09-01T00:00:00", "desde")]
    [InlineData("no-es-fecha", "desde")]
    public async Task GetCalendario_FechaSinZonaOInvalida_Devuelve400(string desde, string campo)
    {
        var respuesta = await Client.GetAsync(
            $"/actividades/calendario?desde={Uri.EscapeDataString(desde)}&hasta={Iso(Ahora.AddDays(1))}&userId=usuario-1");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.True((await LeerJsonAsync(respuesta)).GetProperty("errors").TryGetProperty(campo, out _));
    }

    [Fact]
    public async Task GetCalendario_RangoInvertido_Devuelve422()
    {
        var respuesta = await Client.GetAsync(
            $"/actividades/calendario?desde={Iso(Ahora.AddDays(10))}&hasta={Iso(Ahora.AddDays(2))}&userId=usuario-1");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
    }

    [Fact]
    public async Task GetCalendario_ConRangoValido_Devuelve200ConActividadesFiltradas()
    {
        var dentro = new ActividadBuilder().WithId("dentro")
            .WithFechaInicio(Ahora.AddDays(2)).WithFechaFin(Ahora.AddDays(5)).Build();
        var fuera = new ActividadBuilder().WithId("fuera")
            .WithFechaInicio(Ahora.AddDays(20)).WithFechaFin(Ahora.AddDays(25)).Build();
        await SembrarAsync(dentro, fuera);

        var respuesta = await Client.GetAsync(
            $"/actividades/calendario?desde={Iso(Ahora)}&hasta={Iso(Ahora.AddDays(10))}&userId=usuario-1");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("dentro", Assert.Single((await LeerAsync<Actividad[]>(respuesta))!).Id);
    }

    [Fact]
    public async Task GetCalendario_ConOffset_SeInterpretaComoInstanteUtc()
    {
        // Vence 2026-09-01 23:30 en Tijuana (-07:00) = 2026-09-02 06:30Z.
        var actividad = new ActividadBuilder().WithId("borde")
            .WithFechaInicio(Ahora).WithFechaFin(new DateTime(2026, 9, 2, 6, 30, 0, DateTimeKind.Utc)).Build();
        await SembrarAsync(actividad);

        var diaDos = await Client.GetFromJsonAsync<Actividad[]>(
            "/actividades/calendario?desde=2026-09-02T00:00:00-07:00&hasta=2026-09-02T23:59:59-07:00&userId=usuario-1", Json);
        var diaUno = await Client.GetFromJsonAsync<Actividad[]>(
            "/actividades/calendario?desde=2026-09-01T00:00:00-07:00&hasta=2026-09-01T23:59:59-07:00&userId=usuario-1", Json);

        Assert.Empty(diaDos!);
        Assert.Equal("borde", Assert.Single(diaUno!).Id);
    }

    [Fact]
    public async Task GetCalendario_ConJerarquiaPadreEHija_NoDuplicaHijaEnNivelSuperior()
    {
        var padre = new ActividadBuilder().WithId("cal-padre").WithTitulo("Padre Calendario")
            .WithTipo(TipoActividad.Entrega)
            .WithFechaInicio(Ahora.AddDays(1)).WithFechaFin(Ahora.AddDays(10)).Build();
        new ActividadBuilder().WithId("cal-hija").WithTitulo("Hija Calendario")
            .WithPadre(padre)
            .WithFechaInicio(Ahora.AddDays(2)).WithFechaFin(Ahora.AddDays(5)).Build();
        await SembrarAsync(padre);

        var respuesta = await Client.GetAsync(
            $"/actividades/calendario?desde={Iso(Ahora)}&hasta={Iso(Ahora.AddDays(12))}&userId=usuario-1");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var raiz = Assert.Single((await LeerAsync<Actividad[]>(respuesta))!);
        Assert.Equal("cal-padre", raiz.Id);
        Assert.Equal("cal-hija", Assert.Single(raiz.ActividadesHijas).Id);
    }

    [Fact]
    public async Task GetCalendario_ConFiltroUsuario_DevuelveSoloActividadesEnRangoDelUsuario()
    {
        var dentroPropia = new ActividadBuilder().WithId("cal-propia").WithUserId("usuario-1")
            .WithFechaInicio(Ahora.AddDays(1)).WithFechaFin(Ahora.AddDays(5)).Build();
        var dentroCompartida = new ActividadBuilder().WithId("cal-compartida").WithUserId("usuario-2")
            .WithFechaInicio(Ahora.AddDays(2)).WithFechaFin(Ahora.AddDays(6)).Build();
        dentroCompartida.CompartirCon("usuario-1", Ahora);
        var dentroAjena = new ActividadBuilder().WithId("cal-ajena").WithUserId("usuario-ajeno")
            .WithFechaInicio(Ahora.AddDays(3)).WithFechaFin(Ahora.AddDays(7)).Build();
        await SembrarAsync(dentroPropia, dentroCompartida, dentroAjena);

        var respuesta = await Client.GetAsync(
            $"/actividades/calendario?desde={Iso(Ahora)}&hasta={Iso(Ahora.AddDays(10))}&userId=usuario-1");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var ids = (await LeerAsync<Actividad[]>(respuesta))!.Select(a => a.Id).ToList();
        Assert.Contains("cal-propia", ids);
        Assert.Contains("cal-compartida", ids);
        Assert.DoesNotContain("cal-ajena", ids);
    }

    // ---------- GET /actividades/{id} ----------

    [Fact]
    public async Task GetById_Existente_Devuelve200ConLaActividad()
    {
        await SembrarAsync(new ActividadBuilder().WithId("actividad-1").WithTipo(TipoActividad.Presentacion).Build());

        var respuesta = await Client.GetAsync("/actividades/actividad-1?userId=usuario-1");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var actividad = (await LeerAsync<Actividad>(respuesta))!;
        Assert.Equal("actividad-1", actividad.Id);
        Assert.Equal(TipoActividad.Presentacion, actividad.Tipo);
    }

    [Fact]
    public async Task GetById_Inexistente_Devuelve404()
    {
        var respuesta = await Client.GetAsync("/actividades/no-existe?userId=usuario-1");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task GetById_SinUsuario_Devuelve400()
    {
        await SembrarAsync(new ActividadBuilder().WithId("actividad-1").Build());

        var respuesta = await Client.GetAsync("/actividades/actividad-1");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Theory]
    [InlineData("id_con_guion_bajo")]
    [InlineData("id!")]
    [InlineData("id%20con%20espacios")]
    [InlineData("%C3%B1and%C3%BA")]
    public async Task GetById_FormatoDeIdInvalido_Devuelve400(string id)
    {
        var respuesta = await Client.GetAsync($"/actividades/{id}?userId=usuario-1");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task GetById_UsuarioAjeno_Devuelve403Forbidden()
    {
        await SembrarAsync(new ActividadBuilder().WithId("act-privada").WithUserId("usuario-1").Build());

        var respuesta = await Client.GetAsync("/actividades/act-privada?userId=usuario-ajeno");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task GetById_Colaborador_Devuelve200Ok()
    {
        var actividad = new ActividadBuilder().WithId("act-compartida").WithUserId("usuario-1").Build();
        actividad.CompartirCon("usuario-colaborador", Ahora);
        await SembrarAsync(actividad);

        var respuesta = await Client.GetAsync("/actividades/act-compartida?userId=usuario-colaborador");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    // ---------- POST /actividades ----------

    [Fact]
    public async Task Post_Valido_Devuelve201ConLocation()
    {
        var respuesta = await Client.PostAsJsonAsync("/actividades", CreateBody());

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var creada = (await LeerAsync<Actividad>(respuesta))!;
        Assert.True(Guid.TryParse(creada.Id, out _));
        Assert.Equal($"/actividades/{creada.Id}", respuesta.Headers.Location?.OriginalString);
        Assert.Equal("usuario-1", creada.UserId);
        Assert.Equal(Ahora, creada.FechaInicio);
        Assert.Equal(TipoActividad.Entrega, creada.Tipo);
        Assert.False(creada.Completada);

        var consulta = await Client.GetAsync($"{respuesta.Headers.Location}?userId=usuario-1");
        Assert.Equal(HttpStatusCode.OK, consulta.StatusCode);
    }

    [Fact]
    public async Task Post_FechaInicioEnviadaPorElCliente_SeIgnoraYSeUsaLaHoraDeCreacion()
    {
        var body = new { userId = "usuario-1", titulo = "Tarea", fechaInicio = Ahora.AddYears(-1), fechaFin = Ahora.AddDays(1) };

        var respuesta = await Client.PostAsJsonAsync("/actividades", body);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        Assert.Equal(Ahora, (await LeerAsync<Actividad>(respuesta))!.FechaInicio);
    }

    [Fact]
    public async Task Post_CamposDelServidorEnviadosPorElCliente_SeIgnoran()
    {
        var json = """
            { "id": "id-del-cliente", "userId": "usuario-1", "titulo": "Intento", "fechaFin": "2026-09-10T10:00:00Z",
              "eliminada": true, "fechaEliminacion": "2020-01-01T00:00:00Z", "completada": true,
              "fechaCompletada": "2020-01-01T00:00:00Z", "generadaAutomaticamente": true }
            """;

        var respuesta = await Client.PostAsync("/actividades", JsonCrudo(json));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var creada = (await LeerAsync<Actividad>(respuesta))!;
        Assert.NotEqual("id-del-cliente", creada.Id);
        Assert.False(creada.Eliminada);
        Assert.Null(creada.FechaEliminacion);
        Assert.False(creada.Completada);
        Assert.Null(creada.FechaCompletada);
        Assert.False(creada.GeneradaAutomaticamente);
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync($"/actividades/{creada.Id}?userId=usuario-1")).StatusCode);
    }

    [Fact]
    public async Task Post_ConActividadesHijasAnidadas_Devuelve400YNoCreaNada()
    {
        var json = """
            { "userId": "usuario-1", "titulo": "Raiz", "fechaFin": "2026-09-10T10:00:00Z",
              "actividadesHijas": [ { "userId": "usuario-2", "titulo": "Hija", "fechaFin": "2020-01-01T00:00:00Z" } ] }
            """;

        var respuesta = await Client.PostAsync("/actividades", JsonCrudo(json));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.True((await LeerJsonAsync(respuesta)).GetProperty("errors").TryGetProperty("actividadesHijas", out _));
        Assert.Equal(0, await ContarActividadesAsync());
    }

    public static TheoryData<string, string> CamposInvalidos => new()
    {
        { """{ "titulo": "Sin usuario", "fechaFin": "2026-09-10T10:00:00Z" }""", "userId" },
        { """{ "userId": "usuario-1", "fechaFin": "2026-09-10T10:00:00Z" }""", "titulo" },
        { """{ "userId": "usuario-1", "titulo": "   ", "fechaFin": "2026-09-10T10:00:00Z" }""", "titulo" },
        { """{ "userId": "usuario-1", "titulo": "Sin fecha" }""", "fechaFin" },
        { """{ "userId": "usuario_1", "titulo": "Usuario invalido", "fechaFin": "2026-09-10T10:00:00Z" }""", "userId" },
        { """{ "userId": "usuario-1", "titulo": "Padre invalido", "fechaFin": "2026-09-10T10:00:00Z", "actividadPadreId": "padre#1" }""", "actividadPadreId" },
        { """{ "userId": "usuario-1", "titulo": "Sin zona", "fechaFin": "2026-09-10T10:00:00" }""", "fechaFin" },
        { """{ "userId": "usuario-1", "titulo": "Colaborador vacio", "fechaFin": "2026-09-10T10:00:00Z", "colaboradores": [""] }""", "colaboradores[0]" },
    };

    [Theory]
    [MemberData(nameof(CamposInvalidos))]
    public async Task Post_CamposFaltantesOInvalidos_Devuelve400(string json, string campoConError)
    {
        var respuesta = await Client.PostAsync("/actividades", JsonCrudo(json));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var cuerpo = await LeerJsonAsync(respuesta);
        Assert.True(cuerpo.GetProperty("errors").TryGetProperty(campoConError, out _));
        Assert.Equal(0, await ContarActividadesAsync());
    }

    [Theory]
    [InlineData("[5]")]
    [InlineData("[true]")]
    [InlineData("[[\"usuario-2\"]]")]
    [InlineData("[{ \"usuarioId\": 5 }]")]
    public async Task Post_ColaboradorConFormatoInvalido_Devuelve400NoUn500(string colaboradores)
    {
        var json = $$"""{ "userId": "usuario-1", "titulo": "T", "fechaFin": "2026-09-10T10:00:00Z", "colaboradores": {{colaboradores}} }""";

        var respuesta = await Client.PostAsync("/actividades", JsonCrudo(json));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal(0, await ContarActividadesAsync());
    }

    [Fact]
    public async Task Post_FechaFinConOffset_SeGuardaComoInstanteUtc()
    {
        var json = """{ "userId": "usuario-1", "titulo": "Con offset", "fechaFin": "2026-12-15T23:59:00-08:00" }""";

        var respuesta = await Client.PostAsync("/actividades", JsonCrudo(json));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var creada = (await LeerAsync<Actividad>(respuesta))!;
        Assert.Equal(new DateTime(2026, 12, 16, 7, 59, 0, DateTimeKind.Utc), creada.FechaFin.ToUniversalTime());
    }

    [Fact]
    public async Task Post_TituloDemasiadoLargo_Devuelve400()
    {
        var respuesta = await Client.PostAsJsonAsync("/actividades", CreateBody(titulo: new string('a', 201)));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Post_Duplicado_Devuelve422()
    {
        Assert.Equal(HttpStatusCode.Created, (await Client.PostAsJsonAsync("/actividades", CreateBody())).StatusCode);

        var respuesta = await Client.PostAsJsonAsync("/actividades", CreateBody());

        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
        Assert.True((await LeerJsonAsync(respuesta)).GetProperty("errors").TryGetProperty("titulo", out _));
        Assert.Equal(1, await ContarActividadesAsync());
    }

    [Fact]
    public async Task Post_FechaFinAnteriorAFechaInicio_Devuelve422()
    {
        var body = new { userId = "usuario-1", titulo = "Vencida", fechaFin = Ahora.AddMinutes(-1) };

        var respuesta = await Client.PostAsJsonAsync("/actividades", body);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
        Assert.True((await LeerJsonAsync(respuesta)).GetProperty("errors").TryGetProperty("fechaFin", out _));
    }

    [Fact]
    public async Task Post_ConPadre_QuedaAnidadaEnElPadre()
    {
        await SembrarAsync(new ActividadBuilder().WithId("padre").WithTitulo("Padre").WithTipo(TipoActividad.Examen).Build());

        var respuesta = await Client.PostAsJsonAsync("/actividades", CreateBody(actividadPadreId: "padre"));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var padre = (await Client.GetFromJsonAsync<Actividad>("/actividades/padre?userId=usuario-1", Json))!;
        Assert.Equal("Proyecto final", Assert.Single(padre.ActividadesHijas).Titulo);
    }

    [Fact]
    public async Task Post_PadreInexistente_Devuelve422()
    {
        var respuesta = await Client.PostAsJsonAsync("/actividades", CreateBody(actividadPadreId: "no-existe"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
    }

    [Fact]
    public async Task Post_SuperaLaProfundidadMaxima_Devuelve422()
    {
        var raiz = new ActividadBuilder().WithId("raiz").WithTitulo("Raiz").WithTipo(TipoActividad.General).Build();
        new ActividadBuilder().WithId("hija").WithTitulo("Hija").WithPadre(raiz).Build();
        await SembrarAsync(raiz);

        var respuesta = await Client.PostAsJsonAsync("/actividades", CreateBody(actividadPadreId: "hija"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
        Assert.True((await LeerJsonAsync(respuesta)).GetProperty("errors").TryGetProperty("actividadPadreId", out _));
    }

    [Fact]
    public async Task Post_ConColaboradoresEnFormatoString_CreaYDevuelveColaboradores()
    {
        await SembrarUsuariosAsync();
        var body = new
        {
            userId = "usuario-1",
            titulo = "Proyecto Final con Colaboradores",
            descripcion = "Entrega grupal",
            fechaFin = Ahora.AddDays(15),
            tipo = "Entrega",
            colaboradores = new[] { "usuario-2", "usuario-colaborador" }
        };

        var respuesta = await Client.PostAsJsonAsync("/actividades", body);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var creada = (await LeerAsync<Actividad>(respuesta))!;
        Assert.Equal(["usuario-2", "usuario-colaborador"], creada.Colaboradores!.Select(c => c.UsuarioId).Order());

        var persistida = (await Client.GetFromJsonAsync<Actividad>($"/actividades/{creada.Id}?userId=usuario-1", Json))!;
        Assert.Equal(2, persistida.Colaboradores!.Count);
    }

    [Fact]
    public async Task Post_ConColaboradoresEnFormatoObjeto_IgnoraActividadIdYFechaDelCliente()
    {
        await SembrarUsuariosAsync();
        var json = """
            { "userId": "usuario-1", "titulo": "Objetos", "fechaFin": "2026-09-10T10:00:00Z",
              "colaboradores": [ { "usuarioId": "usuario-2", "actividadId": "otra", "fechaCompartida": "2000-01-01T00:00:00Z" } ] }
            """;

        var respuesta = await Client.PostAsync("/actividades", JsonCrudo(json));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var creada = (await LeerAsync<Actividad>(respuesta))!;
        var colaborador = Assert.Single(creada.Colaboradores!);
        Assert.Equal(creada.Id, colaborador.ActividadId);
        Assert.Equal(Ahora, colaborador.FechaCompartida);
    }

    [Fact]
    public async Task Post_ConColaboradorInexistente_Devuelve400BadRequest()
    {
        var body = new
        {
            userId = "usuario-1",
            titulo = "Proyecto Invalido",
            fechaFin = Ahora.AddDays(15),
            tipo = "Entrega",
            colaboradores = new[] { "usuario-fantasma-xyz" }
        };

        var respuesta = await Client.PostAsJsonAsync("/actividades", body);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.True((await LeerJsonAsync(respuesta)).GetProperty("errors").TryGetProperty("colaboradores", out _));
    }

    // ---------- PUT /actividades/{id} ----------

    [Fact]
    public async Task Put_Valido_Devuelve200ConLaActividadActualizada()
    {
        await SembrarAsync(new ActividadBuilder().WithId("actividad-1").Build());

        var respuesta = await Client.PutAsJsonAsync("/actividades/actividad-1", UpdateBody(completada: true));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var actualizada = (await LeerAsync<Actividad>(respuesta))!;
        Assert.Equal("Titulo actualizado", actualizada.Titulo);
        Assert.Equal("Descripcion actualizada", actualizada.Descripcion);
        Assert.Equal(Ahora.AddDays(20), actualizada.FechaFin);
        Assert.Equal(TipoActividad.Examen, actualizada.Tipo);
        Assert.True(actualizada.Completada);
        Assert.Equal(Ahora, actualizada.FechaCompletada);
        Assert.Equal(ActividadBuilder.FechaBase, actualizada.FechaInicio);

        var persistida = (await Client.GetFromJsonAsync<Actividad>("/actividades/actividad-1?userId=usuario-1", Json))!;
        Assert.Equal(actualizada.Colaboradores!.Select(c => c.UsuarioId), persistida.Colaboradores!.Select(c => c.UsuarioId));
        Assert.Equal(actualizada, persistida with
        {
            ActividadesHijas = actualizada.ActividadesHijas,
            Colaboradores = actualizada.Colaboradores
        });
    }

    [Fact]
    public async Task Put_Inexistente_Devuelve404()
    {
        var respuesta = await Client.PutAsJsonAsync("/actividades/no-existe", UpdateBody());

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Put_CamposInvalidos_Devuelve400()
    {
        await SembrarAsync(new ActividadBuilder().WithId("actividad-1").Build());

        var respuesta = await Client.PutAsJsonAsync("/actividades/actividad-1", UpdateBody(titulo: ""));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.True((await LeerJsonAsync(respuesta)).GetProperty("errors").TryGetProperty("titulo", out _));
    }

    [Fact]
    public async Task Put_SinUsuario_Devuelve400YNoModifica()
    {
        var actividad = new ActividadBuilder().WithId("actividad-1").Build();
        actividad.CompartirCon("usuario-2", Ahora);
        await SembrarAsync(actividad);

        var json = """{ "titulo": "Anonimo", "fechaFin": "2026-09-20T10:00:00Z", "colaboradores": [] }""";
        var respuesta = await Client.PutAsync("/actividades/actividad-1", JsonCrudo(json));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.True((await LeerJsonAsync(respuesta)).GetProperty("errors").TryGetProperty("userId", out _));
        var persistida = (await Client.GetFromJsonAsync<Actividad>("/actividades/actividad-1?userId=usuario-1", Json))!;
        Assert.Equal("Examen de Calculo", persistida.Titulo);
        Assert.Single(persistida.Colaboradores!);
    }

    [Fact]
    public async Task Put_FechaFinAnteriorAFechaInicio_Devuelve422()
    {
        await SembrarAsync(new ActividadBuilder().WithId("actividad-1").Build());

        var respuesta = await Client.PutAsJsonAsync("/actividades/actividad-1", UpdateBody(fechaFin: Ahora.AddDays(-1)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
    }

    [Fact]
    public async Task Put_PadreQueEsSuDescendiente_Devuelve422()
    {
        var raiz = new ActividadBuilder().WithId("raiz").WithTitulo("Raiz").WithTipo(TipoActividad.Entrega).Build();
        new ActividadBuilder().WithId("hija").WithTitulo("Hija").WithPadre(raiz).Build();
        await SembrarAsync(raiz);

        var respuesta = await Client.PutAsJsonAsync("/actividades/raiz", UpdateBody(tipo: "Entrega", actividadPadreId: "hija"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
    }

    [Fact]
    public async Task Put_ConUsuarioNoAutorizadoEnBody_Devuelve403Forbidden()
    {
        await SembrarAsync(new ActividadBuilder().WithId("act-put-privada").WithUserId("usuario-1").Build());

        var respuesta = await Client.PutAsJsonAsync("/actividades/act-put-privada", UpdateBody(userId: "usuario-ajeno"));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task Put_DuenioActualizaColaboradores_Devuelve200Ok()
    {
        await SembrarAsync(new ActividadBuilder().WithId("act-put-colab").WithUserId("usuario-1").Build());

        var respuesta = await Client.PutAsJsonAsync(
            "/actividades/act-put-colab",
            new
            {
                userId = "usuario-1",
                titulo = "Proyecto Actualizado",
                fechaFin = Ahora.AddDays(20),
                tipo = "Examen",
                colaboradores = new[] { "usuario-2" }
            });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var actualizada = (await LeerAsync<Actividad>(respuesta))!;
        Assert.Equal("usuario-2", Assert.Single(actualizada.Colaboradores!).UsuarioId);

        var persistida = (await Client.GetFromJsonAsync<Actividad>("/actividades/act-put-colab?userId=usuario-1", Json))!;
        Assert.Equal("usuario-2", Assert.Single(persistida.Colaboradores!).UsuarioId);
    }

    [Fact]
    public async Task Put_ColaboradorIntentaModificarColaboradores_Devuelve403Forbidden()
    {
        var actividad = new ActividadBuilder().WithId("act-put-colab-forbid").WithUserId("usuario-1").Build();
        actividad.CompartirCon("usuario-colaborador", Ahora);
        await SembrarAsync(actividad);

        var respuesta = await Client.PutAsJsonAsync(
            "/actividades/act-put-colab-forbid",
            new
            {
                userId = "usuario-colaborador",
                titulo = "Intento Modificar Colaboradores",
                fechaFin = Ahora.AddDays(20),
                tipo = "Examen",
                colaboradores = new[] { "usuario-ajeno" }
            });

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task Put_ColaboradorEditaSinTocarColaboradores_Devuelve200()
    {
        var actividad = new ActividadBuilder().WithId("act-colab-edita").WithUserId("usuario-1").Build();
        actividad.CompartirCon("usuario-colaborador", Ahora);
        await SembrarAsync(actividad);

        var respuesta = await Client.PutAsJsonAsync(
            "/actividades/act-colab-edita", UpdateBody(userId: "usuario-colaborador", titulo: "Editada por colaborador"));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("Editada por colaborador", (await LeerAsync<Actividad>(respuesta))!.Titulo);
    }

    // ---------- DELETE /actividades/{id} ----------

    [Fact]
    public async Task Delete_Existente_Devuelve204SinCuerpoYEliminaSusDescendientes()
    {
        var raiz = new ActividadBuilder().WithId("raiz").WithTitulo("Raiz").WithTipo(TipoActividad.Examen).Build();
        new ActividadBuilder().WithId("hija").WithTitulo("Hija").WithPadre(raiz).Build();
        await SembrarAsync(raiz);

        var respuesta = await Client.DeleteAsync("/actividades/raiz?userId=usuario-1");

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        Assert.Empty(await respuesta.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync("/actividades/raiz?userId=usuario-1")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync("/actividades/hija?userId=usuario-1")).StatusCode);
    }

    [Fact]
    public async Task Delete_Inexistente_Devuelve404()
    {
        var respuesta = await Client.DeleteAsync("/actividades/no-existe?userId=usuario-1");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Delete_SinUsuario_Devuelve400YNoElimina()
    {
        await SembrarAsync(new ActividadBuilder().WithId("act-sin-usuario").WithUserId("usuario-1").Build());

        var respuesta = await Client.DeleteAsync("/actividades/act-sin-usuario");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.True((await LeerJsonAsync(respuesta)).GetProperty("errors").TryGetProperty("userId", out _));
        Assert.Equal(1, await ContarActividadesAsync());
    }

    [Fact]
    public async Task Delete_SinUsuarioSobreUnIdInexistente_Devuelve400AntesQue404()
    {
        var respuesta = await Client.DeleteAsync("/actividades/no-existe");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Delete_UsuarioSoloEnHeaderOBody_Devuelve400()
    {
        await SembrarAsync(new ActividadBuilder().WithId("act-1").WithUserId("usuario-1").Build());
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/actividades/act-1")
        {
            Content = JsonContent.Create(new { userId = "usuario-1" })
        };
        request.Headers.Add("X-User-Id", "usuario-1");

        var respuesta = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal(1, await ContarActividadesAsync());
    }

    [Fact]
    public async Task Delete_UsuarioNoDuenio_Devuelve403Forbidden()
    {
        await SembrarAsync(new ActividadBuilder().WithId("act-no-duenio").WithUserId("usuario-1").Build());

        var respuesta = await Client.DeleteAsync("/actividades/act-no-duenio?userId=usuario-ajeno");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task Delete_ColaboradorNoDuenio_Devuelve403Forbidden()
    {
        var actividad = new ActividadBuilder().WithId("act-no-borrar").WithUserId("usuario-1").Build();
        actividad.CompartirCon("usuario-colaborador", Ahora);
        await SembrarAsync(actividad);

        var respuesta = await Client.DeleteAsync("/actividades/act-no-borrar?userId=usuario-colaborador");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    // ---------- POST /actividades/{id}/compartir ----------

    [Fact]
    public async Task Compartir_DuenioComparte_Devuelve200Ok()
    {
        await SembrarAsync(new ActividadBuilder().WithId("act-compartir").WithUserId("usuario-1").Build());

        var respuesta = await Client.PostAsJsonAsync(
            "/actividades/act-compartir/compartir", new { userId = "usuario-1", usuarioId = "usuario-2" });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var compartida = (await Client.GetFromJsonAsync<Actividad>("/actividades/act-compartir?userId=usuario-2", Json))!;
        Assert.Equal("usuario-2", Assert.Single(compartida.Colaboradores!).UsuarioId);
    }

    [Fact]
    public async Task Compartir_UsuarioNoDuenio_Devuelve403Forbidden()
    {
        await SembrarAsync(new ActividadBuilder().WithId("act-compartir").WithUserId("usuario-1").Build());

        var respuesta = await Client.PostAsJsonAsync(
            "/actividades/act-compartir/compartir", new { userId = "usuario-ajeno", usuarioId = "usuario-2" });

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task Compartir_SinUsuario_Devuelve400()
    {
        await SembrarAsync(new ActividadBuilder().WithId("act-compartir").WithUserId("usuario-1").Build());
        using var request = new HttpRequestMessage(HttpMethod.Post, "/actividades/act-compartir/compartir")
        {
            Content = JsonContent.Create(new { usuarioId = "usuario-2" })
        };
        request.Headers.Add("X-User-Id", "usuario-1");

        var respuesta = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.True((await LeerJsonAsync(respuesta)).GetProperty("errors").TryGetProperty("userId", out _));
    }

    [Fact]
    public async Task Compartir_UsuarioDestinoNoExiste_Devuelve400BadRequest()
    {
        await SembrarAsync(new ActividadBuilder().WithId("act-compartir").WithUserId("usuario-1").Build());

        var respuesta = await Client.PostAsJsonAsync(
            "/actividades/act-compartir/compartir", new { userId = "usuario-1", usuarioId = "usuario-fantasma" });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var cuerpo = await LeerJsonAsync(respuesta);
        Assert.Equal(400, cuerpo.GetProperty("status").GetInt32());
        Assert.True(cuerpo.GetProperty("errors").TryGetProperty("usuarioId", out _));
    }
}
