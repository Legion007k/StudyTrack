using System.Reflection;
using System.Text.RegularExpressions;
using NetArchTest.Rules;

namespace StudyTrack.Tests.Arquitectura;

/// <summary>
/// Las capas comparten ensamblado, asi que el compilador no impide que una use a otra.
/// Estas pruebas hacen cumplir las fronteras como si fueran proyectos separados, de dos formas:
/// sobre el IL compilado (dependencias reales, incluidos cuerpos de metodos y lambdas) y sobre
/// el codigo fuente (lo que un archivo menciona aunque no llegue a compilarse como dependencia).
/// </summary>
public class CapasTests
{
    private const string Raiz = "StudyTrack.Api";
    private const string Domain = Raiz + ".Domain";
    private const string Repositories = Raiz + ".Repositories";
    private const string Delegates = Raiz + ".Delegates";
    private const string Dtos = Raiz + ".Dtos";
    private const string Routes = Raiz + ".Routes";
    private const string Validators = Raiz + ".Validators";
    private const string Extensions = Raiz + ".Extensions";

    private const string EfCore = "Microsoft.EntityFrameworkCore";
    private const string AspNetCore = "Microsoft.AspNetCore";

    private static readonly Assembly Api = typeof(Program).Assembly;

    private static void AssertSinDependencias(ConditionList condiciones, string descripcion)
    {
        var resultado = condiciones.GetResult();
        var infractores = string.Join(", ", resultado.FailingTypeNames ?? []);
        Assert.True(resultado.IsSuccessful, $"{descripcion}. Tipos que la violan: {infractores}");
    }

    // ---------- Controles positivos: sin ellos, una regla podria pasar sin revisar nada ----------

    [Theory]
    [InlineData(Domain)]
    [InlineData(Repositories)]
    [InlineData(Delegates)]
    [InlineData(Dtos)]
    [InlineData(Routes)]
    [InlineData(Validators)]
    [InlineData(Extensions)]
    public void CadaCapa_TieneTipos(string espacioDeNombres)
    {
        Assert.NotEmpty(Types.InAssembly(Api).That().ResideInNamespace(espacioDeNombres).GetTypes());
    }

    [Fact]
    public void DeteccionDeDependencias_EncuentraUsosDentroDeMetodosAsync()
    {
        // ActividadRepository solo usa EF Core dentro de cuerpos de metodos async y lambdas.
        var resultado = Types.InAssembly(Api).That().HaveName("ActividadRepository")
            .Should().HaveDependencyOn(EfCore).GetResult();
        Assert.True(resultado.IsSuccessful);

        // ActividadDelegate solo usa la interfaz del repositorio dentro de metodos async.
        resultado = Types.InAssembly(Api).That().HaveName("ActividadDelegate")
            .Should().HaveDependencyOn($"{Repositories}.IActividadRepository").GetResult();
        Assert.True(resultado.IsSuccessful);
    }

    // ---------- Dependencias en el IL ----------

    [Fact]
    public void Domain_NoDependeDeInfraestructuraNiDeOtrasCapas()
    {
        AssertSinDependencias(
            Types.InAssembly(Api).That().ResideInNamespace(Domain).ShouldNot().HaveDependencyOnAny(
                EfCore, AspNetCore, "Npgsql", "FluentValidation", "System.ComponentModel.DataAnnotations",
                Repositories, Delegates, Dtos, Routes, Validators, Extensions),
            "Domain solo puede contener entidades puras");
    }

    [Fact]
    public void Repositories_NoDependeDeCapasSuperiores()
    {
        AssertSinDependencias(
            Types.InAssembly(Api).That().ResideInNamespace(Repositories).ShouldNot().HaveDependencyOnAny(
                AspNetCore, "FluentValidation", Delegates, Dtos, Routes, Validators, Extensions),
            "Repositories solo puede depender de Domain y de EF Core");
    }

    [Fact]
    public void Delegates_NoConoceEfCoreNiImplementacionesDeRepositorios()
    {
        AssertSinDependencias(
            Types.InAssembly(Api).That().ResideInNamespace(Delegates).ShouldNot().HaveDependencyOnAny(
                EfCore, "Npgsql", AspNetCore, "FluentValidation",
                $"{Repositories}.ApplicationDbContext", $"{Repositories}.ActividadRepository", $"{Repositories}.Migrations",
                Routes, Validators, Extensions),
            "Delegates solo puede usar las interfaces de Repositories");
    }

    [Fact]
    public void Dtos_NoDependenDeInfraestructuraNiDeCapasHttp()
    {
        AssertSinDependencias(
            Types.InAssembly(Api).That().ResideInNamespace(Dtos).ShouldNot().HaveDependencyOnAny(
                EfCore, AspNetCore, Repositories, Delegates, Routes, Validators, Extensions),
            "Dtos solo puede depender de Domain");
    }

    [Fact]
    public void CapaHttp_NoDependeDeEfCoreNiDeRepositories()
    {
        AssertSinDependencias(
            Types.InAssembly(Api).That()
                .ResideInNamespace(Routes).Or().ResideInNamespace(Validators).Or().ResideInNamespace(Extensions)
                .Or().HaveName("Program")
                .ShouldNot().HaveDependencyOnAny(EfCore, "Npgsql", Repositories),
            "Routes, Validators, Extensions y Program solo pueden llegar a los datos a traves de Delegates");
    }

    [Fact]
    public void Routes_SoloUsaInterfacesDeDelegates()
    {
        AssertSinDependencias(
            Types.InAssembly(Api).That().ResideInNamespace(Routes).ShouldNot().HaveDependencyOnAny(
                $"{Delegates}.ActividadDelegate", $"{Delegates}.DelegateServiceCollectionExtensions"),
            "Routes debe llamar a IActividadDelegate, no a su implementacion");
    }

    // ---------- Menciones en el codigo fuente ----------

    public static TheoryData<string, string> MencionesProhibidas => new()
    {
        { "Domain", @"\bMicrosoft\.EntityFrameworkCore\b" },
        { "Domain", @"\bMicrosoft\.AspNetCore\b" },
        { "Domain", @"\[(Key|Required|Table|Column|ForeignKey|MaxLength|StringLength|Index)\b" },
        { "Delegates", @"\bMicrosoft\.EntityFrameworkCore\b" },
        { "Delegates", @"\bApplicationDbContext\b" },
        { "Delegates", @"\bDbContext\b|\bDbSet\b" },
        { "Routes", @"\bMicrosoft\.EntityFrameworkCore\b" },
        { "Routes", @"\bApplicationDbContext\b" },
        { "Routes", @"\bRepositories\b" },
    };

    [Theory]
    [MemberData(nameof(MencionesProhibidas))]
    public void CodigoFuente_NoMencionaLoQueSuCapaNoPuedeConocer(string carpeta, string patron)
    {
        var archivos = Directory.GetFiles(Path.Combine(CarpetaDelProyectoApi(), carpeta), "*.cs", SearchOption.AllDirectories);
        Assert.NotEmpty(archivos);

        var infractores = archivos
            .Where(archivo => Regex.IsMatch(File.ReadAllText(archivo), patron))
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(infractores.Count == 0, $"{carpeta}/ menciona /{patron}/ en: {string.Join(", ", infractores)}");
    }

    private static string CarpetaDelProyectoApi()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null && !File.Exists(Path.Combine(directorio.FullName, "StudyTrack.slnx")))
            directorio = directorio.Parent;

        Assert.NotNull(directorio);
        return Path.Combine(directorio.FullName, "src", "StudyTrack.Api");
    }
}
