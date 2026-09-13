using StudyTrack.Models;

namespace StudyTrack.Services
{
    /// <summary>Momento en el que una regla se dispara.</summary>
    public enum Disparador
    {
        /// <summary>Al dar de alta el evento ancla (ej. crear un examen genera sus sesiones de estudio).</summary>
        AlCrear,

        /// <summary>Al marcar el evento ancla como completado (ej. entregar la fase 1 genera la fase 2).</summary>
        AlCompletar
    }

    /// <summary>
    /// Un evento derivado. El offset se cuenta desde la fecha del ancla: negativo para
    /// preparar algo antes (estudiar), positivo para lo que viene despues (siguiente entrega).
    /// En el titulo y la descripcion, {titulo} se sustituye por el del evento ancla.
    /// </summary>
    public record PasoRegla(
        string TituloPlantilla,
        string DescripcionPlantilla,
        int OffsetDias,
        int DuracionMinutos,
        TipoActividad TipoGenerado = TipoActividad.General);

    public record Regla(
        TipoActividad TipoAncla,
        Disparador Cuando,
        IReadOnlyList<PasoRegla> Pasos);

    /// <summary>
    /// Motor de eventos en cascada. Las reglas viven aqui, en codigo: son pocas y muy
    /// contextuales, y asi el servicio no necesita tablas ni configuracion externa.
    /// </summary>
    public class MotorDeReglas
    {
        /// <summary>Tope de saltos en cadena, para que una regla mal puesta no genere eventos sin fin.</summary>
        private const int ProfundidadMaxima = 3;

        public static readonly IReadOnlyList<Regla> Catalogo =
        [
            // Un examen se prepara hacia atras desde su fecha.
            new Regla(TipoActividad.Examen, Disparador.AlCrear,
            [
                new PasoRegla("Estudiar para {titulo}", "Primera pasada al temario.", -7, 120),
                new PasoRegla("Estudiar para {titulo}", "Segunda pasada y ejercicios.", -3, 120),
                new PasoRegla("Repaso final: {titulo}", "Repaso ligero, sin material nuevo.", -1, 60),
            ]),

            // Una entrega tambien se prepara hacia atras...
            new Regla(TipoActividad.Entrega, Disparador.AlCrear,
            [
                new PasoRegla("Avanzar {titulo}", "Bloque de trabajo sobre la entrega.", -5, 120),
                new PasoRegla("Revisar y pulir {titulo}", "Ultima revision antes de entregar.", -1, 90),
            ]),

            // ...y al cumplirse abre la siguiente fase, contada desde la fecha real de entrega.
            new Regla(TipoActividad.Entrega, Disparador.AlCompletar,
            [
                new PasoRegla("Siguiente fase de {titulo}", "Generada al completar la fase anterior.", 30, 60, TipoActividad.Entrega),
            ]),

            new Regla(TipoActividad.Presentacion, Disparador.AlCrear,
            [
                new PasoRegla("Preparar material de {titulo}", "Armar diapositivas y guion.", -3, 120),
                new PasoRegla("Ensayar {titulo}", "Ensayo cronometrado.", -1, 60),
            ]),
        ];

        /// <summary>
        /// Expande la cascada completa del evento ancla. Devuelve las actividades nuevas ya
        /// enlazadas a su padre, pero sin persistir: quien llama decide cuando guardar.
        /// </summary>
        public List<Actividad> Expandir(Actividad ancla, Disparador disparador, DateTime? ahora = null)
        {
            var generadas = new List<Actividad>();
            Expandir(ancla, disparador, ahora ?? DateTime.Now, profundidad: 0, generadas);
            return generadas;
        }

        private void Expandir(Actividad ancla, Disparador disparador, DateTime ahora, int profundidad, List<Actividad> acumulador)
        {
            if (profundidad >= ProfundidadMaxima)
                return;

            var regla = Catalogo.FirstOrDefault(r => r.TipoAncla == ancla.Tipo && r.Cuando == disparador);
            if (regla is null)
                return;

            // AlCrear cuelga de la fecha planeada; AlCompletar, de la fecha real de cumplimiento,
            // para que un retraso arrastre al resto del plan en vez de dejarlo en el pasado.
            var fechaAncla = disparador == Disparador.AlCompletar
                ? ancla.FechaCompletada ?? ahora
                : ancla.FechaInicio;

            foreach (var paso in regla.Pasos)
            {
                var inicio = fechaAncla.AddDays(paso.OffsetDias);

                // Si el ancla esta demasiado cerca, los pasos que caerian en el pasado se omiten.
                if (inicio < ahora)
                    continue;

                var hija = new Actividad
                {
                    Titulo = paso.TituloPlantilla.Replace("{titulo}", ancla.Titulo),
                    Descripcion = paso.DescripcionPlantilla.Replace("{titulo}", ancla.Titulo),
                    FechaInicio = inicio,
                    FechaFin = inicio.AddMinutes(paso.DuracionMinutos),
                    Tipo = paso.TipoGenerado,
                    GeneradaAutomaticamente = true,
                    ActividadPadre = ancla
                };

                ancla.ActividadesHijas.Add(hija);
                acumulador.Add(hija);

                // Una hija puede ser a su vez ancla: la siguiente entrega genera su propia preparacion.
                Expandir(hija, Disparador.AlCrear, ahora, profundidad + 1, acumulador);
            }
        }
    }
}
