using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyTrack.Api.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AgregarSoftDeleteActividad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Actividades_UserId_Titulo_FechaFin",
                table: "Actividades");

            migrationBuilder.AddColumn<bool>(
                name: "Eliminada",
                table: "Actividades",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaEliminacion",
                table: "Actividades",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Actividades_UserId_Titulo_FechaFin",
                table: "Actividades",
                columns: new[] { "UserId", "Titulo", "FechaFin" },
                unique: true,
                filter: "\"Eliminada\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Actividades_UserId_Titulo_FechaFin",
                table: "Actividades");

            migrationBuilder.DropColumn(
                name: "Eliminada",
                table: "Actividades");

            migrationBuilder.DropColumn(
                name: "FechaEliminacion",
                table: "Actividades");

            migrationBuilder.CreateIndex(
                name: "IX_Actividades_UserId_Titulo_FechaFin",
                table: "Actividades",
                columns: new[] { "UserId", "Titulo", "FechaFin" },
                unique: true);
        }
    }
}
