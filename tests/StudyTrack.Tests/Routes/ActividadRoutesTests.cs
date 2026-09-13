using System.Net;
using System.Net.Http.Json;
using StudyTrack.Api.Domain;
using StudyTrack.Api.Dtos;
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
        Assert.Empty((await LeerAsync<ActividadDto[]>(respuesta))!);
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
        var actividades = (await LeerAsync<ActividadDto[]>(respuesta))!;
        Assert.Equal(["raiz", "otra"], actividades.Select(a => a.Id));
        Assert.Equal("hija", Assert.Single(actividades[0].ActividadesHijas).Id);
    }

    // ---------- GET /actividades/{id} ----------

    [Fact]
    public async Task GetById_Existente_Devuelve200ConLaActividad()
    {
        await SembrarAsync(new ActividadBuilder().WithId("actividad-1").WithTipo(TipoActividad.Presentacion).Build());

        var respuesta = await Client.GetAsync("/actividades/actividad-1");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var actividad = (await LeerAsync<ActividadDto>(respuesta))!;
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
        var creada = (await LeerAsync<ActividadDto>(respuesta))!;
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
        Assert.Equal(Ahora, (await LeerAsync<ActividadDto>(respuesta))!.FechaInicio);
    }

    public static TheoryData<string, string> CamposInvalidos => new()
    {
        { """{ "titulo": "Sin usuario", "fechaFin": "2026-09-10T10:00:00" }""", "userId" },
        { """{ "userId": "usuario-1", "fechaFin": "2026-09-10T10:00:00" }""", "titulo" },
        { """{ "userId": "usuario-1", "titulo": "   ", "fechaFin": "2026-09-10T10:00:00" }""", "titulo" },
        { """{ "userId": "usuario-1", "titulo": "Sin fecha" }""", "fechaFin" },
        { """{ "userId": "usuario_1", "titulo": "Usuario invalido", "fechaFin": "2026-09-10T10:00:00" }""", "userId" },
        { """{ "userId": "usuario-1", "titulo": "Padre invalido", "fechaFin": "2026-09-10T10:00:00", "actividadPadreId": "padre#1" }""", "actividadPadreId" },
        { """{ "userId": "usuario-1", "titulo": "Con zona", "fechaFin": "2026-09-10T10:00:00Z" }""", "fechaFin" },
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
        var padre = (await Client.GetFromJsonAsync<ActividadDto>("/actividades/padre", Json))!;
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
        var actualizada = (await LeerAsync<ActividadDto>(respuesta))!;
        Assert.Equal("Titulo actualizado", actualizada.Titulo);
        Assert.Equal("Descripcion actualizada", actualizada.Descripcion);
        Assert.Equal(Ahora.AddDays(20), actualizada.FechaFin);
        Assert.Equal(TipoActividad.Examen, actualizada.Tipo);
        Assert.True(actualizada.Completada);
        Assert.Equal(Ahora, actualizada.FechaCompletada);
        Assert.Equal(ActividadBuilder.FechaBase, actualizada.FechaInicio);

        var persistida = (await Client.GetFromJsonAsync<ActividadDto>("/actividades/actividad-1", Json))!;
        Assert.Equal(actualizada, persistida with { ActividadesHijas = actualizada.ActividadesHijas });
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

        var respuesta = await Client.DeleteAsync("/actividades/raiz");

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
}
