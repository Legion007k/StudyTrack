using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyTrack.Migrations
{
    /// <inheritdoc />
    public partial class Actualizacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Actividades",
                newName: "Actividad_Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Actividad_Id",
                table: "Actividades",
                newName: "Id");
        }
    }
}
