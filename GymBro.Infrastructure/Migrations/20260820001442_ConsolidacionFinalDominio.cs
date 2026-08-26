using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GymBro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConsolidacionFinalDominio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_mediciones_socios_socio_id",
                table: "mediciones");

            migrationBuilder.DropPrimaryKey(
                name: "pk_mediciones",
                table: "mediciones");

            migrationBuilder.RenameTable(
                name: "mediciones",
                newName: "mediciones_fisicas");

            migrationBuilder.RenameIndex(
                name: "ix_mediciones_socio_id",
                table: "mediciones_fisicas",
                newName: "ix_mediciones_fisicas_socio_id");

            migrationBuilder.AddColumn<DateTime>(
                name: "fecha_nacimiento",
                table: "socios",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "genero",
                table: "socios",
                type: "character varying(1)",
                maxLength: 1,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "nivel",
                table: "socios",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "objetivo",
                table: "socios",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "frecuencia_cardiaca_reposo",
                table: "mediciones_fisicas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "horas_sueno_ultima_noche",
                table: "mediciones_fisicas",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "porcentaje_grasa",
                table: "mediciones_fisicas",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "presion_arterial_diastolica",
                table: "mediciones_fisicas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "presion_arterial_sistolica",
                table: "mediciones_fisicas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "tiene_diabetes",
                table: "mediciones_fisicas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "tiene_hipertension",
                table: "mediciones_fisicas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddPrimaryKey(
                name: "pk_mediciones_fisicas",
                table: "mediciones_fisicas",
                column: "id");

            migrationBuilder.CreateTable(
                name: "matrices_recomendacion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    objetivo = table.Column<string>(type: "text", nullable: false),
                    aplica_hipertension = table.Column<bool>(type: "boolean", nullable: false),
                    aplica_diabetes = table.Column<bool>(type: "boolean", nullable: false),
                    tipo_entrenamiento = table.Column<string>(type: "text", nullable: false),
                    sugerencia_dieta = table.Column<string>(type: "text", nullable: false),
                    recomendacion_general = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_matrices_recomendacion", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sesiones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    socio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    duracion_minutos = table.Column<int>(type: "integer", nullable: false),
                    calorias_quemadas = table.Column<int>(type: "integer", nullable: false),
                    frecuencia_cardiaca_maxima = table.Column<int>(type: "integer", nullable: false),
                    frecuencia_cardiaca_promedio = table.Column<int>(type: "integer", nullable: false),
                    fecha_alta = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usuario_alta = table.Column<int>(type: "integer", nullable: false),
                    fecha_modificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario_modificacion = table.Column<int>(type: "integer", nullable: true),
                    fecha_baja = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario_baja = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sesiones", x => x.id);
                    table.ForeignKey(
                        name: "fk_sesiones_socios_socio_id",
                        column: x => x.socio_id,
                        principalTable: "socios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sesiones_ejercicios",
                columns: table => new
                {
                    sesion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ejercicio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    series = table.Column<int>(type: "integer", nullable: false),
                    repeticiones = table.Column<int>(type: "integer", nullable: false),
                    peso_levantado_kg = table.Column<decimal>(type: "numeric", nullable: false),
                    tiempo_descanso_segundos = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sesiones_ejercicios", x => new { x.sesion_id, x.ejercicio_id });
                    table.ForeignKey(
                        name: "fk_sesiones_ejercicios_ejercicios_ejercicio_id",
                        column: x => x.ejercicio_id,
                        principalTable: "ejercicios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_sesiones_ejercicios_sesiones_sesion_id",
                        column: x => x.sesion_id,
                        principalTable: "sesiones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sesiones_socio_id",
                table: "sesiones",
                column: "socio_id");

            migrationBuilder.CreateIndex(
                name: "ix_sesiones_ejercicios_ejercicio_id",
                table: "sesiones_ejercicios",
                column: "ejercicio_id");

            migrationBuilder.AddForeignKey(
                name: "fk_mediciones_fisicas_socios_socio_id",
                table: "mediciones_fisicas",
                column: "socio_id",
                principalTable: "socios",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_mediciones_fisicas_socios_socio_id",
                table: "mediciones_fisicas");

            migrationBuilder.DropTable(
                name: "matrices_recomendacion");

            migrationBuilder.DropTable(
                name: "sesiones_ejercicios");

            migrationBuilder.DropTable(
                name: "sesiones");

            migrationBuilder.DropPrimaryKey(
                name: "pk_mediciones_fisicas",
                table: "mediciones_fisicas");

            migrationBuilder.DropColumn(
                name: "fecha_nacimiento",
                table: "socios");

            migrationBuilder.DropColumn(
                name: "genero",
                table: "socios");

            migrationBuilder.DropColumn(
                name: "nivel",
                table: "socios");

            migrationBuilder.DropColumn(
                name: "objetivo",
                table: "socios");

            migrationBuilder.DropColumn(
                name: "frecuencia_cardiaca_reposo",
                table: "mediciones_fisicas");

            migrationBuilder.DropColumn(
                name: "horas_sueno_ultima_noche",
                table: "mediciones_fisicas");

            migrationBuilder.DropColumn(
                name: "porcentaje_grasa",
                table: "mediciones_fisicas");

            migrationBuilder.DropColumn(
                name: "presion_arterial_diastolica",
                table: "mediciones_fisicas");

            migrationBuilder.DropColumn(
                name: "presion_arterial_sistolica",
                table: "mediciones_fisicas");

            migrationBuilder.DropColumn(
                name: "tiene_diabetes",
                table: "mediciones_fisicas");

            migrationBuilder.DropColumn(
                name: "tiene_hipertension",
                table: "mediciones_fisicas");

            migrationBuilder.RenameTable(
                name: "mediciones_fisicas",
                newName: "mediciones");

            migrationBuilder.RenameIndex(
                name: "ix_mediciones_fisicas_socio_id",
                table: "mediciones",
                newName: "ix_mediciones_socio_id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_mediciones",
                table: "mediciones",
                column: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_mediciones_socios_socio_id",
                table: "mediciones",
                column: "socio_id",
                principalTable: "socios",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
