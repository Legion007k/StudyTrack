using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyTrack.Api.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AgregarUsuariosYActividadesCompartidas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ActividadesCompartidas",
                columns: table => new
                {
                    ActividadId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UsuarioId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FechaCompartida = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActividadesCompartidas", x => new { x.ActividadId, x.UsuarioId });
                    table.ForeignKey(
                        name: "FK_ActividadesCompartidas_Actividades_ActividadId",
                        column: x => x.ActividadId,
                        principalTable: "Actividades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ActividadesCompartidas_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActividadesCompartidas_UsuarioId",
                table: "ActividadesCompartidas",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActividadesCompartidas");

            migrationBuilder.DropTable(
                name: "Usuarios");
        }
    }
}
