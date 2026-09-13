using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using StudyTrack.Api.Delegates;
using StudyTrack.Api.Dtos;
using StudyTrack.Tests.Routes;

namespace StudyTrack.Tests.Middleware;

public class ValidationMiddlewareTests : StudyTrackApiTests
{
    private Task<HttpResponseMessage> PostJsonAsync(string json) =>
        Client.PostAsync("/actividades", new StringContent(json, Encoding.UTF8, "application/json"));

    private static string[] Propiedades(JsonElement cuerpo) =>
        cuerpo.EnumerateObject().Select(p => p.Name).ToArray();

    [Fact]
    public async Task ErrorDeValidacion_TieneElFormatoProblemDetailsExactoDelContrato()
    {
        var respuesta = await PostJsonAsync("""{ "userId": "", "titulo": "", "fechaFin": "2026-09-10T10:00:00" }""");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);

        var cuerpo = await LeerJsonAsync(respuesta);
        Assert.Equal(["type", "title", "status", "errors"], Propiedades(cuerpo));
        Assert.Equal("https://tools.ietf.org/html/rfc7231#section-6.5.1", cuerpo.GetProperty("type").GetString());
        Assert.Equal("Validation Failed", cuerpo.GetProperty("title").GetString());
        Assert.Equal(400, cuerpo.GetProperty("status").GetInt32());

        var errores = cuerpo.GetProperty("errors");
        Assert.Equal(["userId", "titulo"], Propiedades(errores));
        Assert.All(errores.EnumerateObject(), campo =>
        {
            Assert.Equal(JsonValueKind.Array, campo.Value.ValueKind);
            Assert.All(campo.Value.EnumerateArray(), mensaje => Assert.False(string.IsNullOrWhiteSpace(mensaje.GetString())));
        });
    }

    [Theory]
    [InlineData("""{ "userId": "usuario-1", "titulo": """)]
    [InlineData("no es json")]
    [InlineData("""{ "userId": "usuario-1", "titulo": "Tarea", "fechaFin": "no-es-fecha" }""")]
    [InlineData("""{ "userId": "usuario-1", "titulo": "Tarea", "fechaFin": "2026-09-10T10:00:00", "tipo": "Inexistente" }""")]
    [InlineData("""{ "userId": "usuario-1", "titulo": "Tarea", "fechaFin": "2026-09-10T10:00:00", "tipo": 1 }""")]
    public async Task CuerpoMalformadoOConTiposIncorrectos_Devuelve400ConElMismoFormato(string json)
    {
        var respuesta = await PostJsonAsync(json);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var cuerpo = await LeerJsonAsync(respuesta);
        Assert.Equal(["type", "title", "status", "errors"], Propiedades(cuerpo));
        Assert.Equal("Validation Failed", cuerpo.GetProperty("title").GetString());
        Assert.True(cuerpo.GetProperty("errors").TryGetProperty("body", out _));
    }

    [Fact]
    public async Task EnumComoTexto_SeAceptaYSeDevuelveComoTexto()
    {
        var respuesta = await PostJsonAsync("""{ "userId": "usuario-1", "titulo": "Tarea", "fechaFin": "2026-09-10T10:00:00", "tipo": "Presentacion" }""");

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var cuerpo = await LeerJsonAsync(respuesta);
        Assert.Equal("Presentacion", cuerpo.GetProperty("tipo").GetString());
        Assert.True(cuerpo.TryGetProperty("fechaInicio", out _), "Las propiedades deben viajar en camelCase.");
    }

    [Fact]
    public async Task ReglaDeNegocio_Devuelve422ConErroresPorCampo()
    {
        var respuesta = await PostJsonAsync("""{ "userId": "usuario-1", "titulo": "Vencida", "fechaFin": "2026-08-01T10:00:00" }""");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
        var cuerpo = await LeerJsonAsync(respuesta);
        Assert.Equal(["type", "title", "status", "errors"], Propiedades(cuerpo));
        Assert.Equal(422, cuerpo.GetProperty("status").GetInt32());
        Assert.True(cuerpo.GetProperty("errors").TryGetProperty("fechaFin", out _));
    }

    [Fact]
    public async Task NoEncontrado_Devuelve404ConProblemDetails()
    {
        var respuesta = await Client.PutAsJsonAsync("/actividades/no-existe", new { titulo = "x", fechaFin = "2026-09-10T10:00:00" });

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        var cuerpo = await LeerJsonAsync(respuesta);
        Assert.Equal(404, cuerpo.GetProperty("status").GetInt32());
        Assert.Equal("Not Found", cuerpo.GetProperty("title").GetString());
    }

    [Fact]
    public async Task ExcepcionNoControlada_Devuelve500SinExponerDetallesInternos()
    {
        var delegateMock = new Mock<IActividadDelegate>();
        delegateMock.Setup(d => d.GetAllAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("detalle interno secreto"));
        using var cliente = CrearCliente(services =>
        {
            services.RemoveAll<IActividadDelegate>();
            services.AddScoped(_ => delegateMock.Object);
        });

        var respuesta = await cliente.GetAsync("/actividades");

        Assert.Equal(HttpStatusCode.InternalServerError, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        var texto = await respuesta.Content.ReadAsStringAsync();
        Assert.DoesNotContain("secreto", texto);
        var cuerpo = JsonDocument.Parse(texto).RootElement;
        Assert.Equal(500, cuerpo.GetProperty("status").GetInt32());
        Assert.Equal("Internal Server Error", cuerpo.GetProperty("title").GetString());
    }

    [Fact]
    public async Task ValidacionFallida_NoLlamaAlDelegate()
    {
        var delegateMock = new Mock<IActividadDelegate>(MockBehavior.Strict);
        using var cliente = CrearCliente(services =>
        {
            services.RemoveAll<IActividadDelegate>();
            services.AddScoped(_ => delegateMock.Object);
        });

        var respuesta = await cliente.PostAsJsonAsync("/actividades", new { userId = "", titulo = "" });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        delegateMock.Verify(d => d.CreateAsync(It.IsAny<CreateActividadDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
