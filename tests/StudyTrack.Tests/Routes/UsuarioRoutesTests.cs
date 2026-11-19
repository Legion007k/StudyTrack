using System.Net;
using System.Net.Http.Json;
using System.Text;
using StudyTrack.Api.Domain;

namespace StudyTrack.Tests.Routes;

public class UsuarioRoutesTests : StudyTrackApiTests
{
    [Fact]
    public async Task Post_Valido_Devuelve201ConLocationYElUsuario()
    {
        var respuesta = await Client.PostAsJsonAsync("/usuarios", new { id = "estudiante-1", nombre = "Ana", email = "ana@test.com" });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        Assert.Equal("/usuarios/estudiante-1", respuesta.Headers.Location?.OriginalString);
        var usuario = (await LeerAsync<Usuario>(respuesta))!;
        Assert.Equal("Ana", usuario.Nombre);

        var consulta = await Client.GetFromJsonAsync<Usuario>("/usuarios/estudiante-1", Json);
        Assert.Equal("ana@test.com", consulta!.Email);
    }

    [Fact]
    public async Task Post_IdRepetido_Devuelve409()
    {
        await Client.PostAsJsonAsync("/usuarios", new { id = "estudiante-1", nombre = "Ana", email = "ana@test.com" });

        var respuesta = await Client.PostAsJsonAsync("/usuarios", new { id = "estudiante-1", nombre = "Otra", email = "otra@test.com" });

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("""{ "nombre": "Ana", "email": "ana@test.com" }""", "id")]
    [InlineData("""{ "id": "mal_id", "nombre": "Ana", "email": "ana@test.com" }""", "id")]
    [InlineData("""{ "id": "estudiante-1", "email": "ana@test.com" }""", "nombre")]
    [InlineData("""{ "id": "estudiante-1", "nombre": "Ana", "email": "no-es-email" }""", "email")]
    public async Task Post_Invalido_Devuelve400(string json, string campo)
    {
        var respuesta = await Client.PostAsync("/usuarios", new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.True((await LeerJsonAsync(respuesta)).GetProperty("errors").TryGetProperty(campo, out _));
    }

    [Fact]
    public async Task Get_Inexistente_Devuelve404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync("/usuarios/no-existe")).StatusCode);
    }

    [Fact]
    public async Task UsuarioRegistrado_PuedeRecibirActividadesCompartidas()
    {
        await Client.PostAsJsonAsync("/usuarios", new { id = "estudiante-2", nombre = "Beto", email = "beto@test.com" });
        var creada = await Client.PostAsJsonAsync("/actividades", new { userId = "estudiante-1", titulo = "Tarea", fechaFin = Ahora.AddDays(1) });
        var id = (await LeerAsync<Actividad>(creada))!.Id;

        var respuesta = await Client.PostAsJsonAsync(
            $"/actividades/{id}/compartir", new { userId = "estudiante-1", usuarioId = "estudiante-2" });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }
}
