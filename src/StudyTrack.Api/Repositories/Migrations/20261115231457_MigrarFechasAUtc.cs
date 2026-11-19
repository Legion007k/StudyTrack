using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyTrack.Api.Repositories.Migrations
{
    /// <summary>
    /// Pasa las fechas de "timestamp without time zone" a "timestamp with time zone".
    /// Los valores existentes son horas de pared de Tijuana (asi los guardaba la version anterior),
    /// por eso se convierten con AT TIME ZONE en vez de dejar que PostgreSQL use la zona de la sesion,
    /// que cambia segun el servidor donde se aplique la migracion.
    /// </summary>
    public partial class MigrarFechasAUtc : Migration
    {
        /// <summary>Zona horaria en la que se capturaron las fechas guardadas sin zona.</summary>
        private const string ZonaOriginal = "America/Tijuana";

        private static readonly (string Tabla, string Columna)[] Columnas =
        [
            ("ActividadesCompartidas", "FechaCompartida"),
            ("Actividades", "FechaInicio"),
            ("Actividades", "FechaFin"),
            ("Actividades", "FechaEliminacion"),
            ("Actividades", "FechaCompletada"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // timestamp AT TIME ZONE 'zona' interpreta la hora de pared en esa zona y devuelve el instante (timestamptz).
            foreach (var (tabla, columna) in Columnas)
            {
                migrationBuilder.Sql(
                    $"ALTER TABLE \"{tabla}\" ALTER COLUMN \"{columna}\" TYPE timestamp with time zone " +
                    $"USING \"{columna}\" AT TIME ZONE '{ZonaOriginal}';");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // timestamptz AT TIME ZONE 'zona' devuelve la hora de pared en esa zona: deshace exactamente el Up.
            foreach (var (tabla, columna) in Columnas)
            {
                migrationBuilder.Sql(
                    $"ALTER TABLE \"{tabla}\" ALTER COLUMN \"{columna}\" TYPE timestamp without time zone " +
                    $"USING \"{columna}\" AT TIME ZONE '{ZonaOriginal}';");
            }
        }
    }
}
