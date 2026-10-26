using StudyTrack.Api.Domain;
using StudyTrack.Tests.Builders;

namespace StudyTrack.Tests.Domain;

public class ActividadTests
{
    private static readonly DateTime Fecha = ActividadBuilder.FechaBase;

    [Fact]
    public void Id_PorDefecto_EsUnGuidDistintoPorInstancia()
    {
        var primera = new Actividad();
        var segunda = new Actividad();

        Assert.True(Guid.TryParse(primera.Id, out _));
        Assert.NotEqual(primera.Id, segunda.Id);
    }

    [Fact]
    public void NuevaActividad_TieneValoresPorDefecto()
    {
        var actividad = new Actividad();

        Assert.Equal(TipoActividad.General, actividad.Tipo);
        Assert.False(actividad.Completada);
        Assert.Null(actividad.FechaCompletada);
        Assert.False(actividad.Eliminada);
        Assert.Null(actividad.FechaEliminacion);
        Assert.False(actividad.GeneradaAutomaticamente);
        Assert.Null(actividad.ActividadPadreId);
        Assert.Empty(actividad.ActividadesHijas);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, true)]
    [InlineData(-1, false)]
    public void TieneRangoDeFechasValido_ComparaFechaFinContraFechaInicio(int minutosDeDiferencia, bool esperado)
    {
        var actividad = new ActividadBuilder()
            .WithFechaInicio(Fecha)
            .WithFechaFin(Fecha.AddMinutes(minutosDeDiferencia))
            .Build();

        Assert.Equal(esperado, actividad.TieneRangoDeFechasValido());
    }

    [Fact]
    public void MarcarCompletada_FijaCompletadaYFecha()
    {
        var actividad = new ActividadBuilder().Build();

        actividad.MarcarCompletada(Fecha.AddDays(2));

        Assert.True(actividad.Completada);
        Assert.Equal(Fecha.AddDays(2), actividad.FechaCompletada);
    }

    [Fact]
    public void MarcarCompletada_YaCompletada_ConservaLaFechaOriginal()
    {
        var actividad = new ActividadBuilder().WithCompletada(Fecha.AddDays(1)).Build();

        actividad.MarcarCompletada(Fecha.AddDays(5));

        Assert.True(actividad.Completada);
        Assert.Equal(Fecha.AddDays(1), actividad.FechaCompletada);
    }

    [Fact]
    public void MarcarPendiente_LimpiaCompletadaYFecha()
    {
        var actividad = new ActividadBuilder().WithCompletada(Fecha.AddDays(1)).Build();

        actividad.MarcarPendiente();

        Assert.False(actividad.Completada);
        Assert.Null(actividad.FechaCompletada);
    }

    [Fact]
    public void MarcarEliminada_FijaEliminadaYFecha()
    {
        var actividad = new ActividadBuilder().Build();

        actividad.MarcarEliminada(Fecha.AddDays(3));

        Assert.True(actividad.Eliminada);
        Assert.Equal(Fecha.AddDays(3), actividad.FechaEliminacion);
    }

    [Fact]
    public void MarcarEliminada_YaEliminada_ConservaLaFechaOriginal()
    {
        var actividad = new ActividadBuilder().Build();
        actividad.MarcarEliminada(Fecha.AddDays(1));

        actividad.MarcarEliminada(Fecha.AddDays(5));

        Assert.Equal(Fecha.AddDays(1), actividad.FechaEliminacion);
    }

    [Fact]
    public void Restaurar_LimpiaEliminadaYFecha()
    {
        var actividad = new ActividadBuilder().Build();
        actividad.MarcarEliminada(Fecha.AddDays(1));

        actividad.Restaurar();

        Assert.False(actividad.Eliminada);
        Assert.Null(actividad.FechaEliminacion);
    }

    [Theory]
    [InlineData(TipoActividad.General, 1)]
    [InlineData(TipoActividad.Examen, 2)]
    [InlineData(TipoActividad.Presentacion, 2)]
    [InlineData(TipoActividad.Entrega, 3)]
    public void ProfundidadMaxima_DependeDelTipo(TipoActividad tipo, int esperada)
    {
        Assert.Equal(esperada, Actividad.ProfundidadMaxima(tipo));
    }

    [Fact]
    public void ProfundidadMaxima_TipoDesconocido_Lanza()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Actividad.ProfundidadMaxima((TipoActividad)99));
    }

    [Fact]
    public void ProfundidadMaximaAbsoluta_EsLaMayorDeTodosLosTipos()
    {
        var mayor = Enum.GetValues<TipoActividad>().Max(Actividad.ProfundidadMaxima);

        Assert.Equal(Actividad.ProfundidadMaximaAbsoluta, mayor);
    }

    [Theory]
    [InlineData(TipoActividad.General, 1, 0, true)]
    [InlineData(TipoActividad.General, 2, 0, false)]
    [InlineData(TipoActividad.General, 0, 1, true)]
    [InlineData(TipoActividad.General, 1, 1, false)]
    [InlineData(TipoActividad.Examen, 2, 0, true)]
    [InlineData(TipoActividad.Examen, 1, 2, false)]
    [InlineData(TipoActividad.Entrega, 3, 0, true)]
    [InlineData(TipoActividad.Entrega, 2, 2, false)]
    public void CabeEnProfundidad_SumaNivelYAlturaContraElLimiteDeLaRaiz(
        TipoActividad tipoRaiz, int nivel, int altura, bool esperado)
    {
        Assert.Equal(esperado, Actividad.CabeEnProfundidad(tipoRaiz, nivel, altura));
    }

    [Fact]
    public void Altura_SinHijas_EsCero()
    {
        Assert.Equal(0, new ActividadBuilder().Build().Altura());
    }

    [Fact]
    public void Altura_ConRamasDesiguales_TomaLaRamaMasLarga()
    {
        var raiz = new ActividadBuilder().WithId("raiz").Build();
        var corta = new ActividadBuilder().WithId("corta").WithPadre(raiz).Build();
        var larga = new ActividadBuilder().WithId("larga").WithPadre(raiz).Build();
        new ActividadBuilder().WithId("nieta").WithPadre(larga).Build();

        Assert.Equal(2, raiz.Altura());
        Assert.Equal(0, corta.Altura());
        Assert.Equal(1, larga.Altura());
    }

    [Fact]
    public void ContieneDescendiente_EncuentraHijasYNietas()
    {
        var raiz = new ActividadBuilder().WithId("raiz").Build();
        var hija = new ActividadBuilder().WithId("hija").WithPadre(raiz).Build();
        new ActividadBuilder().WithId("nieta").WithPadre(hija).Build();

        Assert.True(raiz.ContieneDescendiente("hija"));
        Assert.True(raiz.ContieneDescendiente("nieta"));
        Assert.False(raiz.ContieneDescendiente("raiz"));
        Assert.False(raiz.ContieneDescendiente("otra"));
        Assert.False(hija.ContieneDescendiente("raiz"));
    }

    [Fact]
    public void Builder_WithPadre_EnlazaAmbosLados()
    {
        var padre = new ActividadBuilder().WithId("padre").Build();
        var hija = new ActividadBuilder().WithId("hija").WithPadre(padre).Build();

        Assert.Equal("padre", hija.ActividadPadreId);
        Assert.Same(padre, hija.ActividadPadre);
        Assert.Contains(hija, padre.ActividadesHijas);
    }

    [Fact]
    public void EsPropietario_DevuelveTrueSoloParaElDuenio()
    {
        var actividad = new ActividadBuilder().WithUserId("usuario-1").Build();

        Assert.True(actividad.EsPropietario("usuario-1"));
        Assert.False(actividad.EsPropietario("usuario-2"));
    }

    [Fact]
    public void EsAccesiblePor_DevuelveTrueParaDuenioYColaboradores_FalseParaAjeno()
    {
        var actividad = new ActividadBuilder().WithUserId("usuario-1").Build();
        actividad.CompartirCon("usuario-colaborador", Fecha);

        Assert.True(actividad.EsAccesiblePor("usuario-1"));
        Assert.True(actividad.EsAccesiblePor("usuario-colaborador"));
        Assert.False(actividad.EsAccesiblePor("usuario-ajeno"));
    }

    [Fact]
    public void CompartirCon_AgregaColaborador_SiEsValido()
    {
        var actividad = new ActividadBuilder().WithUserId("usuario-1").Build();

        var compartido = actividad.CompartirCon("usuario-2", Fecha);

        Assert.True(compartido);
        var colaborador = Assert.Single(actividad.Colaboradores);
        Assert.Equal("usuario-2", colaborador.UsuarioId);
        Assert.Equal(Fecha, colaborador.FechaCompartida);
    }

    [Fact]
    public void CompartirCon_DuenioOYaCompartido_DevuelveFalse()
    {
        var actividad = new ActividadBuilder().WithUserId("usuario-1").Build();

        Assert.False(actividad.CompartirCon("usuario-1", Fecha));

        Assert.True(actividad.CompartirCon("usuario-2", Fecha));
        Assert.False(actividad.CompartirCon("usuario-2", Fecha));
    }
}
