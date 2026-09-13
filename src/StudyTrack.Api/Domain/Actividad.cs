namespace StudyTrack.Api.Domain;

/// <summary>
/// Tipo de la actividad. En una actividad raiz tambien decide cuantos niveles
/// de subactividades puede colgar de ella.
/// </summary>
public enum TipoActividad
{
    General,
    Examen,
    Entrega,
    Presentacion
}

public class Actividad
{
    /// <summary>Mayor profundidad que admite cualquier tipo; acota la carga de subarboles.</summary>
    public const int ProfundidadMaximaAbsoluta = 3;

    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Dueno de la actividad. No hay tabla de usuarios: es un identificador opaco.</summary>
    public string UserId { get; set; } = string.Empty;

    public string Titulo { get; set; } = string.Empty;

    public string? Descripcion { get; set; }

    /// <summary>Dia y hora en que se creo la actividad. La fija el servidor y no cambia.</summary>
    public DateTime FechaInicio { get; set; }

    public DateTime FechaFin { get; set; }

    public TipoActividad Tipo { get; set; } = TipoActividad.General;

    public bool Completada { get; set; }

    /// <summary>Dia y hora en que se marco como completada. La fija el servidor.</summary>
    public DateTime? FechaCompletada { get; set; }

    /// <summary>true si la creo el sistema en vez del usuario.</summary>
    public bool GeneradaAutomaticamente { get; set; }

    /// <summary>Actividad de la que cuelga. Null si es una actividad raiz.</summary>
    public string? ActividadPadreId { get; set; }

    public Actividad? ActividadPadre { get; set; }

    public ICollection<Actividad> ActividadesHijas { get; set; } = new List<Actividad>();

    /// <summary>
    /// Niveles de subactividades que admite un arbol cuya raiz es de este tipo.
    /// Una entrega se parte en fases, y las fases en tareas; un pendiente general no se subdivide tanto.
    /// </summary>
    public static int ProfundidadMaxima(TipoActividad tipoRaiz) => tipoRaiz switch
    {
        TipoActividad.General => 1,
        TipoActividad.Examen => 2,
        TipoActividad.Presentacion => 2,
        TipoActividad.Entrega => ProfundidadMaximaAbsoluta,
        _ => throw new ArgumentOutOfRangeException(nameof(tipoRaiz), tipoRaiz, "Tipo de actividad desconocido.")
    };

    /// <summary>
    /// Indica si un nodo colocado en <paramref name="nivel"/> (la raiz es el nivel 0), con un subarbol
    /// de <paramref name="alturaSubarbol"/> niveles por debajo, respeta el limite del tipo de la raiz.
    /// </summary>
    public static bool CabeEnProfundidad(TipoActividad tipoRaiz, int nivel, int alturaSubarbol) =>
        nivel + alturaSubarbol <= ProfundidadMaxima(tipoRaiz);

    public bool TieneRangoDeFechasValido() => FechaFin >= FechaInicio;

    /// <summary>Marca la actividad como completada. Repetirlo conserva la fecha original.</summary>
    public void MarcarCompletada(DateTime ahora)
    {
        if (Completada)
            return;

        Completada = true;
        FechaCompletada = ahora;
    }

    public void MarcarPendiente()
    {
        Completada = false;
        FechaCompletada = null;
    }

    /// <summary>Niveles de subactividades por debajo de esta. Una actividad sin hijas tiene altura 0.</summary>
    public int Altura() =>
        ActividadesHijas.Count == 0 ? 0 : 1 + ActividadesHijas.Max(hija => hija.Altura());

    public bool ContieneDescendiente(string id) =>
        ActividadesHijas.Any(hija => hija.Id == id || hija.ContieneDescendiente(id));
}
