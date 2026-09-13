using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyTrack.Api.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Actividades",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FechaInicio = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    FechaFin = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Completada = table.Column<bool>(type: "boolean", nullable: false),
                    FechaCompletada = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    GeneradaAutomaticamente = table.Column<bool>(type: "boolean", nullable: false),
                    ActividadPadreId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Actividades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Actividades_Actividades_ActividadPadreId",
                        column: x => x.ActividadPadreId,
                        principalTable: "Actividades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Actividades_ActividadPadreId",
                table: "Actividades",
                column: "ActividadPadreId");

            migrationBuilder.CreateIndex(
                name: "IX_Actividades_UserId_Titulo_FechaFin",
                table: "Actividades",
                columns: new[] { "UserId", "Titulo", "FechaFin" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Actividades");
        }
    }
}
