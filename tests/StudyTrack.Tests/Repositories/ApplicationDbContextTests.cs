using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using StudyTrack.Api.Domain;
using StudyTrack.Api.Repositories;

namespace StudyTrack.Tests.Repositories;

/// <summary>Verifica el mapeo relacional con el proveedor real (Npgsql). Construir el modelo no abre conexion.</summary>
public class ApplicationDbContextTests
{
    private static IEntityType ModeloDeActividad()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=modelo_sin_conexion")
            .Options;

        using var context = new ApplicationDbContext(options);
        return context.Model.FindEntityType(typeof(Actividad))!;
    }

    [Theory]
    [InlineData(nameof(Actividad.FechaInicio))]
    [InlineData(nameof(Actividad.FechaFin))]
    [InlineData(nameof(Actividad.FechaCompletada))]
    public void PropiedadesDateTime_SeMapeanATimestampSinZonaHoraria(string propiedad)
    {
        Assert.Equal("timestamp without time zone", ModeloDeActividad().FindProperty(propiedad)!.GetColumnType());
    }

    [Fact]
    public void ActividadPadre_BorraEnCascadaASusHijas()
    {
        var foranea = ModeloDeActividad().GetForeignKeys().Single();

        Assert.Equal(nameof(Actividad.ActividadPadreId), foranea.Properties.Single().Name);
        Assert.Equal(DeleteBehavior.Cascade, foranea.DeleteBehavior);
    }

    [Fact]
    public void UsuarioTituloYFechaFin_TienenIndiceUnico()
    {
        var indice = ModeloDeActividad().GetIndexes().Single(i => i.IsUnique);

        Assert.Equal(
            [nameof(Actividad.UserId), nameof(Actividad.Titulo), nameof(Actividad.FechaFin)],
            indice.Properties.Select(p => p.Name));
    }
}
