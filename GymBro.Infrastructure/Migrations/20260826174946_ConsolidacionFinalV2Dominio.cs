using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymBro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConsolidacionFinalV2Dominio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "tiene_diabetes",
                table: "mediciones_fisicas");

            migrationBuilder.DropColumn(
                name: "tiene_hipertension",
                table: "mediciones_fisicas");

            migrationBuilder.RenameColumn(
                name: "tipo_entrenamiento",
                table: "matrices_recomendacion",
                newName: "categoria_rutina");

            migrationBuilder.RenameColumn(
                name: "grupo_muscular",
                table: "ejercicios",
                newName: "zona_objetivo");

            migrationBuilder.AlterColumn<decimal>(
                name: "presion_arterial_sistolica",
                table: "mediciones_fisicas",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<decimal>(
                name: "presion_arterial_diastolica",
                table: "mediciones_fisicas",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "condicion_medica",
                table: "mediciones_fisicas",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "frecuencia_entrenamiento_dias",
                table: "mediciones_fisicas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "imc",
                table: "mediciones_fisicas",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ingesta_agua_litros",
                table: "mediciones_fisicas",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "nivel_estres",
                table: "mediciones_fisicas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "presion_arterial_media",
                table: "mediciones_fisicas",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "tipo_entrenamiento",
                table: "mediciones_fisicas",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "categoria_imc",
                table: "matrices_recomendacion",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "edad",
                table: "matrices_recomendacion",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "fecha_alta",
                table: "matrices_recomendacion",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "fecha_baja",
                table: "matrices_recomendacion",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "fecha_modificacion",
                table: "matrices_recomendacion",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "genero",
                table: "matrices_recomendacion",
                type: "character varying(1)",
                maxLength: 1,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "usuario_alta",
                table: "matrices_recomendacion",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "usuario_baja",
                table: "matrices_recomendacion",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "usuario_modificacion",
                table: "matrices_recomendacion",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "dificultad",
                table: "ejercicios",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ejecucion",
                table: "ejercicios",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "estabilizadores",
                table: "ejercicios",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "musculo_principal",
                table: "ejercicios",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "preparacion",
                table: "ejercicios",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "sinergistas",
                table: "ejercicios",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "dosificaciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    parte_cuerpo = table.Column<string>(type: "text", nullable: false),
                    subzona_objetivo = table.Column<string>(type: "text", nullable: false),
                    zona_objetivo = table.Column<string>(type: "text", nullable: false),
                    series_minimas = table.Column<int>(type: "integer", nullable: false),
                    series_maximas = table.Column<int>(type: "integer", nullable: false),
                    repeticiones_minimas = table.Column<int>(type: "integer", nullable: false),
                    repeticiones_maximas = table.Column<int>(type: "integer", nullable: false),
                    fecha_alta = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usuario_alta = table.Column<int>(type: "integer", nullable: false),
                    fecha_modificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario_modificacion = table.Column<int>(type: "integer", nullable: true),
                    fecha_baja = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario_baja = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dosificaciones", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dosificaciones");

            migrationBuilder.DropColumn(
                name: "condicion_medica",
                table: "mediciones_fisicas");

            migrationBuilder.DropColumn(
                name: "frecuencia_entrenamiento_dias",
                table: "mediciones_fisicas");

            migrationBuilder.DropColumn(
                name: "imc",
                table: "mediciones_fisicas");

            migrationBuilder.DropColumn(
                name: "ingesta_agua_litros",
                table: "mediciones_fisicas");

            migrationBuilder.DropColumn(
                name: "nivel_estres",
                table: "mediciones_fisicas");

            migrationBuilder.DropColumn(
                name: "presion_arterial_media",
                table: "mediciones_fisicas");

            migrationBuilder.DropColumn(
                name: "tipo_entrenamiento",
                table: "mediciones_fisicas");

            migrationBuilder.DropColumn(
                name: "categoria_imc",
                table: "matrices_recomendacion");

            migrationBuilder.DropColumn(
                name: "edad",
                table: "matrices_recomendacion");

            migrationBuilder.DropColumn(
                name: "fecha_alta",
                table: "matrices_recomendacion");

            migrationBuilder.DropColumn(
                name: "fecha_baja",
                table: "matrices_recomendacion");

            migrationBuilder.DropColumn(
                name: "fecha_modificacion",
                table: "matrices_recomendacion");

            migrationBuilder.DropColumn(
                name: "genero",
                table: "matrices_recomendacion");

            migrationBuilder.DropColumn(
                name: "usuario_alta",
                table: "matrices_recomendacion");

            migrationBuilder.DropColumn(
                name: "usuario_baja",
                table: "matrices_recomendacion");

            migrationBuilder.DropColumn(
                name: "usuario_modificacion",
                table: "matrices_recomendacion");

            migrationBuilder.DropColumn(
                name: "dificultad",
                table: "ejercicios");

            migrationBuilder.DropColumn(
                name: "ejecucion",
                table: "ejercicios");

            migrationBuilder.DropColumn(
                name: "estabilizadores",
                table: "ejercicios");

            migrationBuilder.DropColumn(
                name: "musculo_principal",
                table: "ejercicios");

            migrationBuilder.DropColumn(
                name: "preparacion",
                table: "ejercicios");

            migrationBuilder.DropColumn(
                name: "sinergistas",
                table: "ejercicios");

            migrationBuilder.RenameColumn(
                name: "categoria_rutina",
                table: "matrices_recomendacion",
                newName: "tipo_entrenamiento");

            migrationBuilder.RenameColumn(
                name: "zona_objetivo",
                table: "ejercicios",
                newName: "grupo_muscular");

            migrationBuilder.AlterColumn<int>(
                name: "presion_arterial_sistolica",
                table: "mediciones_fisicas",
                type: "integer",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<int>(
                name: "presion_arterial_diastolica",
                table: "mediciones_fisicas",
                type: "integer",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

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
        }
    }
}
