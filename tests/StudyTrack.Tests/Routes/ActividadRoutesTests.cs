using System.Net;
using System.Net.Http.Json;
using StudyTrack.Api.Domain;
using StudyTrack.Tests.Builders;

namespace StudyTrack.Tests.Routes;

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
        string titulo = "Titulo actualizado",
        bool completada = false,
        DateTime? fechaFin = null,
        string tipo = "Examen",
        string? actividadPadreId = null) => new
    {
        titulo,
        descripcion = "Descripcion actualizada",
        fechaFin = fechaFin ?? Ahora.AddDays(20),
        tipo,
        completada,
        actividadPadreId
    };

    // ---------- GET /actividades ----------

    [Fact]
    public async Task GetAll_SinActividades_Devuelve200ConArregloVacio()
    {
        var respuesta = await Client.GetAsync("/actividades");

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

        var respuesta = await Client.GetAsync("/actividades");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var actividades = (await LeerAsync<Actividad[]>(respuesta))!;
        Assert.Equal(["raiz", "otra"], actividades.Select(a => a.Id));
        Assert.Equal("hija", Assert.Single(actividades[0].ActividadesHijas).Id);
    }

    // ---------- GET /actividades/calendario ----------

    [Fact]
    public async Task GetCalendario_SinParametros_Devuelve400()
    {
        var respuesta = await Client.GetAsync("/actividades/calendario");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task GetCalendario_RangoInvertido_Devuelve422()
    {
        var desde = Uri.EscapeDataString(Ahora.AddDays(10).ToString("o"));
        var hasta = Uri.EscapeDataString(Ahora.AddDays(2).ToString("o"));

        var respuesta = await Client.GetAsync($"/actividades/calendario?desde={desde}&hasta={hasta}");

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

        var desde = Uri.EscapeDataString(Ahora.ToString("yyyy-MM-dd"));
        var hasta = Uri.EscapeDataString(Ahora.AddDays(10).ToString("yyyy-MM-dd"));

        var respuesta = await Client.GetAsync($"/actividades/calendario?desde={desde}&hasta={hasta}");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var actividades = (await LeerAsync<Actividad[]>(respuesta))!;
        var unica = Assert.Single(actividades);
        Assert.Equal("dentro", unica.Id);
    }

    [Fact]
    public async Task GetCalendario_ConJerarquiaPadreEHija_NoDuplicaHijaEnNivelSuperior()
    {
        var padre = new ActividadBuilder().WithId("cal-padre").WithTitulo("Padre Calendario")
            .WithTipo(TipoActividad.Entrega)
            .WithFechaInicio(Ahora.AddDays(1)).WithFechaFin(Ahora.AddDays(10)).Build();
        var hija = new ActividadBuilder().WithId("cal-hija").WithTitulo("Hija Calendario")
            .WithPadre(padre)
            .WithFechaInicio(Ahora.AddDays(2)).WithFechaFin(Ahora.AddDays(5)).Build();

        await SembrarAsync(padre, hija);

        var desde = Uri.EscapeDataString(Ahora.ToString("yyyy-MM-dd"));
        var hasta = Uri.EscapeDataString(Ahora.AddDays(12).ToString("yyyy-MM-dd"));

        var respuesta = await Client.GetAsync($"/actividades/calendario?desde={desde}&hasta={hasta}");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var actividades = (await LeerAsync<Actividad[]>(respuesta))!;

        // El arreglo raiz solo debe contener al padre
        var raiz = Assert.Single(actividades);
        Assert.Equal("cal-padre", raiz.Id);

        // La hija solo debe aparecer anidada dentro de ActividadesHijas del padre
        var hijaEncontrada = Assert.Single(raiz.ActividadesHijas);
        Assert.Equal("cal-hija", hijaEncontrada.Id);
    }

    // ---------- GET /actividades/{id} ----------

    [Fact]
    public async Task GetById_Existente_Devuelve200ConLaActividad()
    {
        await SembrarAsync(new ActividadBuilder().WithId("actividad-1").WithTipo(TipoActividad.Presentacion).Build());

        var respuesta = await Client.GetAsync("/actividades/actividad-1");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var actividad = (await LeerAsync<Actividad>(respuesta))!;
        Assert.Equal("actividad-1", actividad.Id);
        Assert.Equal(TipoActividad.Presentacion, actividad.Tipo);
    }

    [Fact]
    public async Task GetById_Inexistente_Devuelve404()
    {
        var respuesta = await Client.GetAsync("/actividades/no-existe");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Theory]
    [InlineData("id_con_guion_bajo")]
    [InlineData("id!")]
    [InlineData("id%20con%20espacios")]
    [InlineData("%C3%B1and%C3%BA")]
    public async Task GetById_FormatoDeIdInvalido_Devuelve400(string id)
    {
        var respuesta = await Client.GetAsync($"/actividades/{id}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
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

        var consulta = await Client.GetAsync(respuesta.Headers.Location);
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

    public static TheoryData<string, string> CamposInvalidos => new()
    {
        { """{ "titulo": "Sin usuario", "fechaFin": "2026-09-10T10:00:00" }""", "userId" },
        { """{ "userId": "usuario-1", "fechaFin": "2026-09-10T10:00:00" }""", "titulo" },
        { """{ "userId": "usuario-1", "titulo": "   ", "fechaFin": "2026-09-10T10:00:00" }""", "titulo" },
        { """{ "userId": "usuario-1", "titulo": "Sin fecha" }""", "fechaFin" },
        { """{ "userId": "usuario_1", "titulo": "Usuario invalido", "fechaFin": "2026-09-10T10:00:00" }""", "userId" },
        { """{ "userId": "usuario-1", "titulo": "Padre invalido", "fechaFin": "2026-09-10T10:00:00", "actividadPadreId": "padre#1" }""", "actividadPadreId" },
    };

    [Theory]
    [MemberData(nameof(CamposInvalidos))]
    public async Task Post_CamposFaltantesOInvalidos_Devuelve400(string json, string campoConError)
    {
        var respuesta = await Client.PostAsync("/actividades", new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var cuerpo = await LeerJsonAsync(respuesta);
        Assert.True(cuerpo.GetProperty("errors").TryGetProperty(campoConError, out _));
        Assert.Equal(0, await ContarActividadesAsync());
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
        var padre = (await Client.GetFromJsonAsync<Actividad>("/actividades/padre", Json))!;
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

        var persistida = (await Client.GetFromJsonAsync<Actividad>("/actividades/actividad-1", Json))!;
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

    // ---------- DELETE /actividades/{id} ----------

    [Fact]
    public async Task Delete_Existente_Devuelve204SinCuerpoYEliminaSusDescendientes()
    {
        var raiz = new ActividadBuilder().WithId("raiz").WithTitulo("Raiz").WithTipo(TipoActividad.Examen).Build();
        new ActividadBuilder().WithId("hija").WithTitulo("Hija").WithPadre(raiz).Build();
        await SembrarAsync(raiz);

        using var request = new HttpRequestMessage(HttpMethod.Delete, "/actividades/raiz")
        {
            Content = JsonContent.Create(new { userId = "usuario-1" })
        };
        var respuesta = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        Assert.Empty(await respuesta.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync("/actividades/raiz")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync("/actividades/hija")).StatusCode);
    }

    [Fact]
    public async Task Delete_Inexistente_Devuelve404()
    {
        var respuesta = await Client.DeleteAsync("/actividades/no-existe");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    // ---------- POST /actividades/{id}/compartir y Autorizacion ----------

    [Fact]
    public async Task Compartir_UsuarioNoDuenio_Devuelve403Forbidden()
    {
        var actividad = new ActividadBuilder().WithId("act-compartir-1").WithUserId("usuario-1").Build();
        await SembrarAsync(actividad);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/actividades/act-compartir-1/compartir")
        {
            Content = JsonContent.Create(new { usuarioId = "usuario-2" })
        };
        request.Headers.Add("X-User-Id", "usuario-ajeno");

        var respuesta = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task Compartir_DuenioComparte_Devuelve200Ok()
    {
        var actividad = new ActividadBuilder().WithId("act-compartir-2").WithUserId("usuario-1").Build();
        await SembrarAsync(actividad);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/actividades/act-compartir-2/compartir")
        {
            Content = JsonContent.Create(new { usuarioId = "usuario-2" })
        };
        request.Headers.Add("X-User-Id", "usuario-1");

        var respuesta = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task GetById_UsuarioAjeno_Devuelve403Forbidden()
    {
        var actividad = new ActividadBuilder().WithId("act-privada").WithUserId("usuario-1").Build();
        await SembrarAsync(actividad);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/actividades/act-privada");
        request.Headers.Add("X-User-Id", "usuario-ajeno");

        var respuesta = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task GetById_Colaborador_Devuelve200Ok()
    {
        var actividad = new ActividadBuilder().WithId("act-compartida").WithUserId("usuario-1").Build();
        actividad.CompartirCon("usuario-colaborador", DateTime.Now);
        await SembrarAsync(actividad);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/actividades/act-compartida");
        request.Headers.Add("X-User-Id", "usuario-colaborador");

        var respuesta = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task Delete_ColaboradorNoDuenio_Devuelve403Forbidden()
    {
        var actividad = new ActividadBuilder().WithId("act-no-borrar").WithUserId("usuario-1").Build();
        actividad.CompartirCon("usuario-colaborador", DateTime.Now);
        await SembrarAsync(actividad);

        using var request = new HttpRequestMessage(HttpMethod.Delete, "/actividades/act-no-borrar");
        request.Headers.Add("X-User-Id", "usuario-colaborador");

        var respuesta = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task Delete_ConBodyDeUsuarioDuenio_Devuelve204NoContent()
    {
        var raiz = new ActividadBuilder().WithId("raiz-body").WithTitulo("Raiz Body").WithTipo(TipoActividad.Examen).Build();
        new ActividadBuilder().WithId("hija-body").WithTitulo("Hija Body").WithPadre(raiz).Build();
        await SembrarAsync(raiz);

        using var request = new HttpRequestMessage(HttpMethod.Delete, "/actividades/raiz-body")
        {
            Content = JsonContent.Create(new { userId = "usuario-1" })
        };
        var respuesta = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        Assert.Empty(await respuesta.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync("/actividades/raiz-body")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync("/actividades/hija-body")).StatusCode);
    }

    [Fact]
    public async Task Delete_ConBodyDeUsuarioNoDuenio_Devuelve403Forbidden()
    {
        var actividad = new ActividadBuilder().WithId("act-no-duenio").WithUserId("usuario-1").Build();
        await SembrarAsync(actividad);

        using var request = new HttpRequestMessage(HttpMethod.Delete, "/actividades/act-no-duenio")
        {
            Content = JsonContent.Create(new { userId = "usuario-ajeno" })
        };
        var respuesta = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task Delete_SinUsuario_Devuelve403Forbidden()
    {
        var actividad = new ActividadBuilder().WithId("act-sin-header").WithUserId("usuario-1").Build();
        await SembrarAsync(actividad);

        var respuesta = await Client.DeleteAsync("/actividades/act-sin-header");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task Compartir_SinUsuario_Devuelve403Forbidden()
    {
        var actividad = new ActividadBuilder().WithId("act-compartir-sin-header").WithUserId("usuario-1").Build();
        await SembrarAsync(actividad);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/actividades/act-compartir-sin-header/compartir")
        {
            Content = JsonContent.Create(new { usuarioId = "usuario-2" })
        };

        var respuesta = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task Compartir_ConPropietarioEnBody_SinHeader_Devuelve200Ok()
    {
        var actividad = new ActividadBuilder().WithId("act-compartir-body").WithUserId("usuario-1").Build();
        await SembrarAsync(actividad);

        var respuesta = await Client.PostAsJsonAsync(
            "/actividades/act-compartir-body/compartir",
            new { propietarioId = "usuario-1", usuarioId = "usuario-2" });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task Compartir_ConUsuarioAjenoEnBody_SinHeader_Devuelve403Forbidden()
    {
        var actividad = new ActividadBuilder().WithId("act-compartir-ajeno").WithUserId("usuario-1").Build();
        await SembrarAsync(actividad);

        var respuesta = await Client.PostAsJsonAsync(
            "/actividades/act-compartir-ajeno/compartir",
            new { propietarioId = "usuario-ajeno", usuarioId = "usuario-2" });

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task Compartir_UsuarioDestinoNoExiste_Devuelve400BadRequest()
    {
        var actividad = new ActividadBuilder().WithId("act-compartir-inexistente").WithUserId("usuario-1").Build();
        await SembrarAsync(actividad);

        var respuesta = await Client.PostAsJsonAsync(
            "/actividades/act-compartir-inexistente/compartir",
            new { propietarioId = "usuario-1", usuarioId = "usuario-fantasma" });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var cuerpo = await LeerJsonAsync(respuesta);
        Assert.Equal(400, cuerpo.GetProperty("status").GetInt32());
        Assert.True(cuerpo.GetProperty("errors").TryGetProperty("usuarioId", out _));
    }

    [Fact]
    public async Task Put_ConUsuarioNoAutorizadoEnBody_Devuelve403Forbidden()
    {
        var actividad = new ActividadBuilder().WithId("act-put-privada").WithUserId("usuario-1").Build();
        await SembrarAsync(actividad);

        var respuesta = await Client.PutAsJsonAsync(
            "/actividades/act-put-privada",
            new
            {
                userId = "usuario-ajeno",
                titulo = "Intento Modificar",
                fechaFin = Ahora.AddDays(5)
            });

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    // ---------- Punto 6: Colaboradores directos en Creación y Actualización ----------

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
        Assert.NotNull(creada.Colaboradores);
        Assert.Equal(2, creada.Colaboradores.Count);
        Assert.Contains(creada.Colaboradores, c => c.UsuarioId == "usuario-2");
        Assert.Contains(creada.Colaboradores, c => c.UsuarioId == "usuario-colaborador");

        // Verificar que persiste en base de datos
        var persistida = (await Client.GetFromJsonAsync<Actividad>($"/actividades/{creada.Id}?userId=usuario-1", Json))!;
        Assert.NotNull(persistida.Colaboradores);
        Assert.Equal(2, persistida.Colaboradores.Count);
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
        var cuerpo = await LeerJsonAsync(respuesta);
        Assert.True(cuerpo.GetProperty("errors").TryGetProperty("colaboradores", out _));
    }

    [Fact]
    public async Task Put_DuenioActualizaColaboradores_Devuelve200Ok()
    {
        var actividad = new ActividadBuilder().WithId("act-put-colab").WithUserId("usuario-1").Build();
        await SembrarAsync(actividad);

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
        Assert.NotNull(actualizada.Colaboradores);
        Assert.Single(actualizada.Colaboradores);
        Assert.Equal("usuario-2", actualizada.Colaboradores.First().UsuarioId);

        var persistida = (await Client.GetFromJsonAsync<Actividad>("/actividades/act-put-colab?userId=usuario-1", Json))!;
        Assert.NotNull(persistida.Colaboradores);
        Assert.Single(persistida.Colaboradores);
        Assert.Equal("usuario-2", persistida.Colaboradores.First().UsuarioId);
    }

    [Fact]
    public async Task Put_ColaboradorIntentaModificarColaboradores_Devuelve403Forbidden()
    {
        var actividad = new ActividadBuilder().WithId("act-put-colab-forbid").WithUserId("usuario-1").Build();
        actividad.CompartirCon("usuario-colaborador", DateTime.UtcNow);
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

    // ---------- Punto 4: Filtro de Búsqueda y Calendario por Usuario ----------

    [Fact]
    public async Task GetAll_ConFiltroUsuario_DevuelveSoloActividadesPropiasYCompartidas()
    {
        var propia = new ActividadBuilder().WithId("getall-propia").WithUserId("usuario-1").Build();
        var compartida = new ActividadBuilder().WithId("getall-compartida").WithUserId("usuario-2").Build();
        compartida.CompartirCon("usuario-1", DateTime.UtcNow);
        var ajena = new ActividadBuilder().WithId("getall-ajena").WithUserId("usuario-ajeno").Build();

        await SembrarAsync(propia, compartida, ajena);

        var respuesta = await Client.GetAsync("/actividades?userId=usuario-1");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var actividades = (await LeerAsync<Actividad[]>(respuesta))!;
        var ids = actividades.Select(a => a.Id).ToList();

        Assert.Contains("getall-propia", ids);
        Assert.Contains("getall-compartida", ids);
        Assert.DoesNotContain("getall-ajena", ids);
    }

    [Fact]
    public async Task GetCalendario_ConFiltroUsuario_DevuelveSoloActividadesEnRangoDelUsuario()
    {
        var dentroPropia = new ActividadBuilder().WithId("cal-propia")
            .WithUserId("usuario-1")
            .WithFechaInicio(Ahora.AddDays(1)).WithFechaFin(Ahora.AddDays(5)).Build();
        var dentroCompartida = new ActividadBuilder().WithId("cal-compartida")
            .WithUserId("usuario-2")
            .WithFechaInicio(Ahora.AddDays(2)).WithFechaFin(Ahora.AddDays(6)).Build();
        dentroCompartida.CompartirCon("usuario-1", DateTime.UtcNow);
        var dentroAjena = new ActividadBuilder().WithId("cal-ajena")
            .WithUserId("usuario-ajeno")
            .WithFechaInicio(Ahora.AddDays(3)).WithFechaFin(Ahora.AddDays(7)).Build();

        await SembrarAsync(dentroPropia, dentroCompartida, dentroAjena);

        var desde = Uri.EscapeDataString(Ahora.ToString("yyyy-MM-dd"));
        var hasta = Uri.EscapeDataString(Ahora.AddDays(10).ToString("yyyy-MM-dd"));

        var respuesta = await Client.GetAsync($"/actividades/calendario?desde={desde}&hasta={hasta}&userId=usuario-1");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var actividades = (await LeerAsync<Actividad[]>(respuesta))!;
        var ids = actividades.Select(a => a.Id).ToList();

        Assert.Contains("cal-propia", ids);
        Assert.Contains("cal-compartida", ids);
        Assert.DoesNotContain("cal-ajena", ids);
    }
}
