using System.Net;
using System.Net.Http.Json;
using System.Text;
using StudyTrack.Api.Dtos;
using StudyTrack.Tests.Builders;

namespace StudyTrack.Tests.Routes;

public class CrossCuttingRoutesTests : StudyTrackApiTests
{
    private const string JsonValido = """{ "userId": "usuario-1", "titulo": "Tarea", "fechaFin": "2026-09-10T10:00:00" }""";

    // ---------- Content-Type ----------

    [Theory]
    [InlineData("GET", "/actividades")]
    [InlineData("GET", "/actividades/actividad-1")]
    [InlineData("POST", "/actividades")]
    [InlineData("PUT", "/actividades/actividad-1")]
    public async Task RespuestasExitosas_SonApplicationJson(string metodo, string ruta)
    {
        await SembrarAsync(new ActividadBuilder().WithId("actividad-1").WithTitulo("Existente").Build());
        var solicitud = new HttpRequestMessage(new HttpMethod(metodo), ruta);
        if (metodo is "POST" or "PUT")
            solicitud.Content = new StringContent(JsonValido, Encoding.UTF8, "application/json");

        var respuesta = await Client.SendAsync(solicitud);

        Assert.True(respuesta.IsSuccessStatusCode, $"{metodo} {ruta} respondio {respuesta.StatusCode}");
        Assert.Equal("application/json", respuesta.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("POST", "/actividades")]
    [InlineData("PUT", "/actividades/actividad-1")]
    public async Task CuerpoQueNoEsJson_Devuelve415(string metodo, string ruta)
    {
        await SembrarAsync(new ActividadBuilder().WithId("actividad-1").Build());
        var solicitud = new HttpRequestMessage(new HttpMethod(metodo), ruta)
        {
            Content = new StringContent(JsonValido, Encoding.UTF8, "text/plain")
        };

        var respuesta = await Client.SendAsync(solicitud);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        Assert.Equal(1, await ContarActividadesAsync());
    }

    [Fact]
    public async Task RespuestasDeError_SonProblemJson()
    {
        var respuesta = await Client.GetAsync("/actividades/no-existe");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
    }

    // ---------- Formato de ID en todos los {id} ----------

    public static TheoryData<string, string> EndpointsConIdInvalido()
    {
        var datos = new TheoryData<string, string>();
        foreach (var metodo in new[] { "GET", "PUT", "DELETE" })
        foreach (var id in new[] { "id_invalido", "id.con.puntos", "id%24", "id%0A" })
            datos.Add(metodo, id);
        return datos;
    }

    [Theory]
    [MemberData(nameof(EndpointsConIdInvalido))]
    public async Task IdConFormatoInvalido_Devuelve400SinLlegarAlDelegate(string metodo, string id)
    {
        await SembrarAsync(new ActividadBuilder().WithId("actividad-1").Build());
        var solicitud = new HttpRequestMessage(new HttpMethod(metodo), $"/actividades/{id}");
        if (metodo == "PUT")
            solicitud.Content = JsonContent.Create(new { titulo = "x", fechaFin = "2026-09-10T10:00:00" });

        var respuesta = await Client.SendAsync(solicitud);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var cuerpo = await LeerJsonAsync(respuesta);
        Assert.True(cuerpo.GetProperty("errors").TryGetProperty("actividadId", out _));
        Assert.Equal(1, await ContarActividadesAsync());
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("DELETE")]
    public async Task IdConFormatoValidoPeroInexistente_Devuelve404NoUn400(string metodo)
    {
        var respuesta = await Client.SendAsync(new HttpRequestMessage(new HttpMethod(metodo), "/actividades/ABC-123-xyz"));

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    // ---------- Concurrencia ----------

    [Fact]
    public async Task PostsEnParalelo_CreanTodasLasActividadesSinCorromperDatos()
    {
        const int total = 25;

        var respuestas = await Task.WhenAll(Enumerable.Range(0, total).Select(i =>
            Client.PostAsJsonAsync("/actividades", new
            {
                userId = "usuario-1",
                titulo = $"Tarea {i:D2}",
                fechaFin = Ahora.AddDays(1)
            })));

        Assert.All(respuestas, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        var creadas = await Task.WhenAll(respuestas.Select(r => LeerAsync<ActividadDto>(r)));
        Assert.Equal(total, creadas.Select(c => c!.Id).Distinct().Count());

        var listado = (await Client.GetFromJsonAsync<ActividadDto[]>("/actividades", Json))!;
        Assert.Equal(
            Enumerable.Range(0, total).Select(i => $"Tarea {i:D2}").Order(),
            listado.Select(a => a.Titulo).Order());
    }

    [Fact]
    public async Task PutsEnParaleloSobreLaMismaActividad_DejanUnEstadoCompletoDeUnaDeLasSolicitudes()
    {
        await SembrarAsync(new ActividadBuilder().WithId("actividad-1").Build());
        var titulos = Enumerable.Range(0, 10).Select(i => $"Version {i}").ToArray();

        var respuestas = await Task.WhenAll(titulos.Select(titulo =>
            Client.PutAsJsonAsync("/actividades/actividad-1", new
            {
                titulo,
                descripcion = $"Descripcion de {titulo}",
                fechaFin = Ahora.AddDays(3)
            })));

        Assert.All(respuestas, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var final = (await Client.GetFromJsonAsync<ActividadDto>("/actividades/actividad-1", Json))!;
        Assert.Contains(final.Titulo, titulos);
        Assert.Equal($"Descripcion de {final.Titulo}", final.Descripcion);
        Assert.Equal(1, await ContarActividadesAsync());
    }
}
