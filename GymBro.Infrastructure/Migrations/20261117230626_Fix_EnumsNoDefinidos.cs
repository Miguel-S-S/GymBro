using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymBro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Fix_EnumsNoDefinidos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "tipo_entrenamiento",
                table: "mediciones_fisicas",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            // Corrección de datos previa a las restricciones: valores "0" guardados porque el enum no se asignaba.
            // - Socios creados antes de 4844f6b / af30c1c: el alta actual usa Principiante y Mantenimiento.
            // - Mediciones cargadas desde la app: el tipo de entrenamiento no se pide; se desconoce, queda NULL (sin dato).
            // Solo se corrige el "0" conocido: cualquier otro valor inválido hace fallar la restricción (no se reescribe a ciegas).
            migrationBuilder.Sql("UPDATE socios SET nivel = 'Principiante' WHERE nivel = '0';");
            migrationBuilder.Sql("UPDATE socios SET objetivo = 'Mantenimiento' WHERE objetivo = '0';");
            migrationBuilder.Sql("UPDATE mediciones_fisicas SET tipo_entrenamiento = NULL WHERE tipo_entrenamiento = '0';");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Socio_Nivel",
                table: "socios",
                sql: "nivel IN ('Principiante', 'Intermedio', 'Avanzado')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Socio_Objetivo",
                table: "socios",
                sql: "objetivo IN ('PerdidaPeso', 'GananciaMuscular', 'Mantenimiento')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MedicionFisica_TipoEntrenamiento",
                table: "mediciones_fisicas",
                sql: "tipo_entrenamiento IS NULL OR tipo_entrenamiento IN ('Fuerza', 'Cardio', 'HIIT')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Socio_Nivel",
                table: "socios");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Socio_Objetivo",
                table: "socios");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MedicionFisica_TipoEntrenamiento",
                table: "mediciones_fisicas");

            // Vuelve al estado anterior (la columna no admitía NULL y el valor sin asignar era "0").
            migrationBuilder.Sql("UPDATE mediciones_fisicas SET tipo_entrenamiento = '0' WHERE tipo_entrenamiento IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "tipo_entrenamiento",
                table: "mediciones_fisicas",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
