using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymBro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPrescripcionEntrenamiento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "prescripcion_entrenamiento_id",
                table: "sesiones",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "prescripciones_entrenamiento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    socio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    estado = table.Column<string>(type: "text", nullable: false),
                    origen_generacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    observaciones = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    fecha_alta = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usuario_alta = table.Column<int>(type: "integer", nullable: false),
                    fecha_modificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario_modificacion = table.Column<int>(type: "integer", nullable: true),
                    fecha_baja = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario_baja = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_prescripciones_entrenamiento", x => x.id);
                    table.ForeignKey(
                        name: "fk_prescripciones_entrenamiento_socios_socio_id",
                        column: x => x.socio_id,
                        principalTable: "socios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "prescripciones_ejercicios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    prescripcion_entrenamiento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ejercicio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    series_objetivo = table.Column<int>(type: "integer", nullable: false),
                    repeticiones_minimas = table.Column<int>(type: "integer", nullable: false),
                    repeticiones_maximas = table.Column<int>(type: "integer", nullable: false),
                    rir_objetivo = table.Column<int>(type: "integer", nullable: true),
                    descanso_objetivo_segundos = table.Column<int>(type: "integer", nullable: false),
                    observaciones = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    fecha_alta = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usuario_alta = table.Column<int>(type: "integer", nullable: false),
                    fecha_modificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario_modificacion = table.Column<int>(type: "integer", nullable: true),
                    fecha_baja = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario_baja = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_prescripciones_ejercicios", x => x.id);
                    table.CheckConstraint("CK_PrescripcionEjercicio_DescansoObjetivoSegundos", "descanso_objetivo_segundos > 0");
                    table.CheckConstraint("CK_PrescripcionEjercicio_Orden", "orden > 0");
                    table.CheckConstraint("CK_PrescripcionEjercicio_RepeticionesMaximas", "repeticiones_maximas >= repeticiones_minimas");
                    table.CheckConstraint("CK_PrescripcionEjercicio_RepeticionesMinimas", "repeticiones_minimas > 0");
                    table.CheckConstraint("CK_PrescripcionEjercicio_RIRObjetivo", "rir_objetivo IS NULL OR (rir_objetivo >= 0 AND rir_objetivo <= 5)");
                    table.CheckConstraint("CK_PrescripcionEjercicio_SeriesObjetivo", "series_objetivo > 0");
                    table.ForeignKey(
                        name: "fk_prescripciones_ejercicios_ejercicios_ejercicio_id",
                        column: x => x.ejercicio_id,
                        principalTable: "ejercicios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_prescripciones_ejercicios_prescripciones_entrenamiento_pres",
                        column: x => x.prescripcion_entrenamiento_id,
                        principalTable: "prescripciones_entrenamiento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sesiones_prescripcion_entrenamiento_id",
                table: "sesiones",
                column: "prescripcion_entrenamiento_id");

            migrationBuilder.CreateIndex(
                name: "ix_prescripciones_ejercicios_ejercicio_id",
                table: "prescripciones_ejercicios",
                column: "ejercicio_id");

            migrationBuilder.CreateIndex(
                name: "ix_prescripciones_ejercicios_prescripcion_entrenamiento_id_ord",
                table: "prescripciones_ejercicios",
                columns: new[] { "prescripcion_entrenamiento_id", "orden" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_prescripciones_entrenamiento_socio_id",
                table: "prescripciones_entrenamiento",
                column: "socio_id");

            migrationBuilder.AddForeignKey(
                name: "fk_sesiones_prescripciones_entrenamiento_prescripcion_entrenam",
                table: "sesiones",
                column: "prescripcion_entrenamiento_id",
                principalTable: "prescripciones_entrenamiento",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_sesiones_prescripciones_entrenamiento_prescripcion_entrenam",
                table: "sesiones");

            migrationBuilder.DropTable(
                name: "prescripciones_ejercicios");

            migrationBuilder.DropTable(
                name: "prescripciones_entrenamiento");

            migrationBuilder.DropIndex(
                name: "ix_sesiones_prescripcion_entrenamiento_id",
                table: "sesiones");

            migrationBuilder.DropColumn(
                name: "prescripcion_entrenamiento_id",
                table: "sesiones");
        }
    }
}
