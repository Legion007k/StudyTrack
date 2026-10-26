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
    [InlineData(nameof(Actividad.FechaEliminacion))]
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
    public void UsuarioTituloYFechaFin_TienenIndiceUnicoConFiltroDeNoEliminadas()
    {
        var indice = ModeloDeActividad().GetIndexes().Single(i => i.IsUnique);

        Assert.Equal(
            [nameof(Actividad.UserId), nameof(Actividad.Titulo), nameof(Actividad.FechaFin)],
            indice.Properties.Select(p => p.Name));
        Assert.Equal("\"Eliminada\" = false", indice.GetFilter());
    }

    [Fact]
    public void Actividad_TieneFiltroGlobalParaExcluirEliminadas()
    {
        var filtros = ModeloDeActividad().GetDeclaredQueryFilters();

        Assert.NotEmpty(filtros);
    }

    [Fact]
    public void Usuario_TieneClavePrimaria()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=modelo_sin_conexion")
            .Options;

        using var context = new ApplicationDbContext(options);
        var modelo = context.Model.FindEntityType(typeof(Usuario))!;

        Assert.Equal(nameof(Usuario.Id), modelo.FindPrimaryKey()!.Properties.Single().Name);
    }

    [Fact]
    public void ActividadCompartida_TieneClaveCompuesta()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=modelo_sin_conexion")
            .Options;

        using var context = new ApplicationDbContext(options);
        var modelo = context.Model.FindEntityType(typeof(ActividadCompartida))!;

        Assert.Equal(
            [nameof(ActividadCompartida.ActividadId), nameof(ActividadCompartida.UsuarioId)],
            modelo.FindPrimaryKey()!.Properties.Select(p => p.Name));
    }
}
