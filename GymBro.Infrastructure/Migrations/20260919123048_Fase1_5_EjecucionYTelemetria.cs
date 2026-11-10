using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymBro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Fase1_5_EjecucionYTelemetria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_sesiones_ejercicios_ejercicios_ejercicio_id",
                table: "sesiones_ejercicios");

            migrationBuilder.DropPrimaryKey(
                name: "pk_sesiones_ejercicios",
                table: "sesiones_ejercicios");

            migrationBuilder.DropColumn(
                name: "peso_levantado_kg",
                table: "sesiones_ejercicios");

            migrationBuilder.DropColumn(
                name: "repeticiones",
                table: "sesiones_ejercicios");

            migrationBuilder.DropColumn(
                name: "duracion_minutos",
                table: "sesiones");

            migrationBuilder.RenameColumn(
                name: "tiempo_descanso_segundos",
                table: "sesiones_ejercicios",
                newName: "usuario_alta");

            migrationBuilder.RenameColumn(
                name: "series",
                table: "sesiones_ejercicios",
                newName: "orden");

            migrationBuilder.RenameColumn(
                name: "fecha",
                table: "sesiones",
                newName: "inicio");

            migrationBuilder.AddColumn<Guid>(
                name: "id",
                table: "sesiones_ejercicios",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "fecha_alta",
                table: "sesiones_ejercicios",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "fecha_baja",
                table: "sesiones_ejercicios",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "fecha_modificacion",
                table: "sesiones_ejercicios",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "observaciones",
                table: "sesiones_ejercicios",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "usuario_baja",
                table: "sesiones_ejercicios",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "usuario_modificacion",
                table: "sesiones_ejercicios",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "fin",
                table: "sesiones",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "pk_sesiones_ejercicios",
                table: "sesiones_ejercicios",
                column: "id");

            migrationBuilder.CreateTable(
                name: "series_realizadas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sesion_ejercicio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    orden_serie = table.Column<int>(type: "integer", nullable: false),
                    peso_levantado_kg = table.Column<decimal>(type: "numeric", nullable: false),
                    repeticiones = table.Column<int>(type: "integer", nullable: false),
                    rir = table.Column<int>(type: "integer", nullable: true),
                    es_calentamiento = table.Column<bool>(type: "boolean", nullable: false),
                    tiempo_descanso_segundos = table.Column<int>(type: "integer", nullable: true),
                    fecha_alta = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usuario_alta = table.Column<int>(type: "integer", nullable: false),
                    fecha_modificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario_modificacion = table.Column<int>(type: "integer", nullable: true),
                    fecha_baja = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario_baja = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_series_realizadas", x => x.id);
                    table.CheckConstraint("CK_SerieRealizada_OrdenSerie", "orden_serie > 0");
                    table.CheckConstraint("CK_SerieRealizada_PesoLevantadoKg", "peso_levantado_kg >= 0");
                    table.CheckConstraint("CK_SerieRealizada_Repeticiones", "repeticiones > 0");
                    table.CheckConstraint("CK_SerieRealizada_RIR", "rir IS NULL OR (rir >= 0 AND rir <= 5)");
                    table.CheckConstraint("CK_SerieRealizada_TiempoDescansoSegundos", "tiempo_descanso_segundos IS NULL OR tiempo_descanso_segundos >= 0");
                    table.ForeignKey(
                        name: "fk_series_realizadas_sesiones_ejercicios_sesion_ejercicio_id",
                        column: x => x.sesion_ejercicio_id,
                        principalTable: "sesiones_ejercicios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sesiones_ejercicios_sesion_id",
                table: "sesiones_ejercicios",
                column: "sesion_id");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SesionEjercicio_Orden",
                table: "sesiones_ejercicios",
                sql: "orden > 0");

            migrationBuilder.CreateIndex(
                name: "ix_series_realizadas_sesion_ejercicio_id",
                table: "series_realizadas",
                column: "sesion_ejercicio_id");

            migrationBuilder.AddForeignKey(
                name: "fk_sesiones_ejercicios_ejercicios_ejercicio_id",
                table: "sesiones_ejercicios",
                column: "ejercicio_id",
                principalTable: "ejercicios",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_sesiones_ejercicios_ejercicios_ejercicio_id",
                table: "sesiones_ejercicios");

            migrationBuilder.DropTable(
                name: "series_realizadas");

            migrationBuilder.DropPrimaryKey(
                name: "pk_sesiones_ejercicios",
                table: "sesiones_ejercicios");

            migrationBuilder.DropIndex(
                name: "ix_sesiones_ejercicios_sesion_id",
                table: "sesiones_ejercicios");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SesionEjercicio_Orden",
                table: "sesiones_ejercicios");

            migrationBuilder.DropColumn(
                name: "id",
                table: "sesiones_ejercicios");

            migrationBuilder.DropColumn(
                name: "fecha_alta",
                table: "sesiones_ejercicios");

            migrationBuilder.DropColumn(
                name: "fecha_baja",
                table: "sesiones_ejercicios");

            migrationBuilder.DropColumn(
                name: "fecha_modificacion",
                table: "sesiones_ejercicios");

            migrationBuilder.DropColumn(
                name: "observaciones",
                table: "sesiones_ejercicios");

            migrationBuilder.DropColumn(
                name: "usuario_baja",
                table: "sesiones_ejercicios");

            migrationBuilder.DropColumn(
                name: "usuario_modificacion",
                table: "sesiones_ejercicios");

            migrationBuilder.DropColumn(
                name: "fin",
                table: "sesiones");

            migrationBuilder.RenameColumn(
                name: "usuario_alta",
                table: "sesiones_ejercicios",
                newName: "tiempo_descanso_segundos");

            migrationBuilder.RenameColumn(
                name: "orden",
                table: "sesiones_ejercicios",
                newName: "series");

            migrationBuilder.RenameColumn(
                name: "inicio",
                table: "sesiones",
                newName: "fecha");

            migrationBuilder.AddColumn<decimal>(
                name: "peso_levantado_kg",
                table: "sesiones_ejercicios",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "repeticiones",
                table: "sesiones_ejercicios",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "duracion_minutos",
                table: "sesiones",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "pk_sesiones_ejercicios",
                table: "sesiones_ejercicios",
                columns: new[] { "sesion_id", "ejercicio_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_sesiones_ejercicios_ejercicios_ejercicio_id",
                table: "sesiones_ejercicios",
                column: "ejercicio_id",
                principalTable: "ejercicios",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
