using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymBro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Etapa9a1_CatalogoCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "origen",
                table: "ejercicios",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Dataset");

            migrationBuilder.AddColumn<string>(
                name: "tipo_medida",
                table: "ejercicios",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Repeticiones");

            migrationBuilder.AddColumn<string>(
                name: "tipo_medida",
                table: "dosificaciones",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Repeticiones");

            // Las filas existentes quedan como Dataset / Repeticiones por el valor por defecto de las columnas nuevas.

            // Corrección de la dosificación de abdominales (Etapa 9a-1, sección 4.1). Valores reales verificados en
            // gymbro-db el 17/11/2026 (docs/pfc/evidencia/etapa9a1-estado-previo.txt): 6 filas con parte_cuerpo = 'Abs'
            // y zona_objetivo = 'Legs'; una de ellas (Abs/Lower, 30-60 series x 1 repetición) es la plancha ("Tablón")
            // de series.csv. SQL idempotente: si no hay filas que cumplan la condición, no hace nada.
            // 1) La fila de la plancha 30-60 x 1 no se interpreta ni se reutiliza: baja lógica (usuario 1 = sistema).
            migrationBuilder.Sql(@"UPDATE dosificaciones
SET fecha_baja = now(), usuario_baja = 1
WHERE lower(parte_cuerpo) IN ('abs', 'abdominales')
  AND series_minimas = 30 AND series_maximas = 60
  AND repeticiones_minimas = 1 AND repeticiones_maximas = 1
  AND fecha_baja IS NULL;");
            // 2) Todas las filas de abdominales pasan a la zona Core (dejan de figurar en Piernas).
            migrationBuilder.Sql(@"UPDATE dosificaciones
SET zona_objetivo = 'Core', fecha_modificacion = now(), usuario_modificacion = 1
WHERE lower(parte_cuerpo) IN ('abs', 'abdominales')
  AND zona_objetivo <> 'Core';");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Ejercicio_Origen",
                table: "ejercicios",
                sql: "origen IN ('Dataset', 'Proyecto', 'Gimnasio')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Ejercicio_TipoMedida",
                table: "ejercicios",
                sql: "tipo_medida IN ('Repeticiones', 'Segundos')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Dosificacion_TipoMedida",
                table: "dosificaciones",
                sql: "tipo_medida IN ('Repeticiones', 'Segundos')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revierte la corrección de abdominales: vuelven a Legs y la fila 30-60 x 1 se reactiva.
            // Las filas de Core propias del proyecto (CatalogoProyectoSeeder) se eliminan para no dejarlas en Legs.
            migrationBuilder.Sql(@"DELETE FROM dosificaciones WHERE parte_cuerpo = 'Core';");
            migrationBuilder.Sql(@"DELETE FROM ejercicios WHERE origen = 'Proyecto'
  AND NOT EXISTS (SELECT 1 FROM prescripciones_ejercicios pe WHERE pe.ejercicio_id = ejercicios.id)
  AND NOT EXISTS (SELECT 1 FROM sesiones_ejercicios se WHERE se.ejercicio_id = ejercicios.id);");
            migrationBuilder.Sql(@"UPDATE dosificaciones
SET fecha_baja = NULL, usuario_baja = NULL
WHERE lower(parte_cuerpo) IN ('abs', 'abdominales')
  AND series_minimas = 30 AND series_maximas = 60
  AND repeticiones_minimas = 1 AND repeticiones_maximas = 1;");
            migrationBuilder.Sql(@"UPDATE dosificaciones SET zona_objetivo = 'Legs'
WHERE lower(parte_cuerpo) IN ('abs', 'abdominales') AND zona_objetivo = 'Core';");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Ejercicio_Origen",
                table: "ejercicios");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Ejercicio_TipoMedida",
                table: "ejercicios");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Dosificacion_TipoMedida",
                table: "dosificaciones");

            migrationBuilder.DropColumn(
                name: "origen",
                table: "ejercicios");

            migrationBuilder.DropColumn(
                name: "tipo_medida",
                table: "ejercicios");

            migrationBuilder.DropColumn(
                name: "tipo_medida",
                table: "dosificaciones");
        }
    }
}
