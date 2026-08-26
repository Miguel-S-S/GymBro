using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace GymBro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EstandarizacionAuditoriaYSnakeCase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "ejercicios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "text", nullable: false),
                    grupo_muscular = table.Column<string>(type: "text", nullable: false),
                    equipamiento = table.Column<string>(type: "text", nullable: false),
                    embedding = table.Column<Vector>(type: "vector", nullable: true),
                    fecha_alta = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usuario_alta = table.Column<int>(type: "integer", nullable: false),
                    fecha_modificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario_modificacion = table.Column<int>(type: "integer", nullable: true),
                    fecha_baja = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario_baja = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ejercicios", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "socios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "text", nullable: false),
                    apellido = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    fecha_alta = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usuario_alta = table.Column<int>(type: "integer", nullable: false),
                    fecha_modificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario_modificacion = table.Column<int>(type: "integer", nullable: true),
                    fecha_baja = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario_baja = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_socios", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "mediciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    socio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    peso_kg = table.Column<decimal>(type: "numeric", nullable: false),
                    altura_cm = table.Column<decimal>(type: "numeric", nullable: false),
                    fecha_alta = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usuario_alta = table.Column<int>(type: "integer", nullable: false),
                    fecha_modificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario_modificacion = table.Column<int>(type: "integer", nullable: true),
                    fecha_baja = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario_baja = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mediciones", x => x.id);
                    table.ForeignKey(
                        name: "fk_mediciones_socios_socio_id",
                        column: x => x.socio_id,
                        principalTable: "socios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_mediciones_socio_id",
                table: "mediciones",
                column: "socio_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ejercicios");

            migrationBuilder.DropTable(
                name: "mediciones");

            migrationBuilder.DropTable(
                name: "socios");
        }
    }
}
