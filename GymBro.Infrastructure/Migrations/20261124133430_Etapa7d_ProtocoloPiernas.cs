using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymBro.Infrastructure.Migrations
{
    /// <summary>
    /// Etapa 7d, protocolo de piernas. Solo aditiva (sin DROP ni DELETE en el Up):
    ///   - socios: limitacion_rodilla y limitacion_lumbar (texto, por defecto SinLimitacion) y origen_datos (texto; las
    ///     filas existentes quedan en DatasetExterno, valor conservador fuera de los KPIs del estudio; la migración no
    ///     reclasifica nada más: lo corrige el autor con docs/pfc/scripts/clasificar-origen-datos.sql);
    ///   - prescripciones_entrenamiento: protocolo_codigo, protocolo_version, numero_generacion (CHECK &gt; 0),
    ///     origen_datos, excluida_estudio, es_modificada, entrada_snapshot (jsonb) e índice único parcial
    ///     (socio_id, protocolo_codigo, numero_generacion);
    ///   - prescripciones_ejercicios: rol, grupo, tempo_excentrico_seg (1 a 10), carga_sugerida_kg (&gt;= 0) y
    ///     motivo_seleccion (jsonb);
    ///   - sesiones_ejercicios: prescripcion_ejercicio_id (FK Restrict, con índice);
    ///   - tabla ejercicios_protocolo con la siembra de los 10 ejercicios curados (valores provisionales, sin validación
    ///     clínica). La siembra es INSERT ... SELECT sobre ejercicios por id con ON CONFLICT DO NOTHING: en una base donde
    ///     esos UUID no existen (todas salvo la del autor, porque DataSeeder usa Guid.NewGuid) inserta 0 filas.
    /// Reversible: el Down quita la tabla, los índices, las restricciones y las columnas.
    /// </summary>
    public partial class Etapa7d_ProtocoloPiernas : Migration
    {
        /// <summary>
        /// Siembra de piernas-1.0 (especificación 4.2): ejercicio, grupo, rol, clase, rodilla y lumbar "excluir desde"
        /// (null = no se excluye) y orden de rotación. Los UUID son los del catálogo del autor (Paso 0).
        /// </summary>
        public static readonly (Guid EjercicioId, string Grupo, string Rol, string Clase, string Rodilla, string Lumbar, short Orden)[] Siembra =
        {
            (new Guid("69e15aaf-ca0d-433b-be03-6b496450b1d2"), "Cuadriceps", "Referencia", "Multiarticular", "Alta", "Alta", 1),         // Prensa de piernas 45 grados
            (new Guid("c7499d5c-48c5-4d16-a691-54a96079ca49"), "Cuadriceps", "Referencia", "Multiarticular", "Alta", null, 2),           // Sentadilla Hack en máquina
            (new Guid("3203840d-5a79-4c84-9758-b40238f7c85f"), "Cuadriceps", "Variable", "Multiarticular", "Alta", "Moderada", 1),       // Sentadilla trasera con barra libre
            (new Guid("1537a321-40ab-4a60-b3b8-e73cab167c28"), "Cuadriceps", "Variable", "Multiarticular", "Alta", "Alta", 2),           // Sentadilla frontal con barra
            (new Guid("15a5f846-25f5-48a5-a570-264c2b894462"), "Cuadriceps", "Variable", "Aislamiento", "Moderada", null, 3),            // Extensión de cuádriceps en camilla
            (new Guid("14a284c5-5159-4bdb-83f7-733154e07228"), "Isquiotibiales", "Referencia", "Aislamiento", null, null, 1),            // Curl femoral sentado en máquina
            (new Guid("2e9d6c61-6be0-4f4a-8f1b-348869e07a65"), "Isquiotibiales", "Variable", "Aislamiento", null, null, 1),              // Curl femoral acostado en camilla
            (new Guid("a33d764a-d3f2-437e-a527-38e877fdba0f"), "Isquiotibiales", "Variable", "Multiarticular", null, "Moderada", 2),     // Peso muerto rumano con barra
            (new Guid("bca285fb-60ee-40a7-bdc9-771b201365a1"), "Pantorrillas", "Variable", "Aislamiento", null, null, 1),                // Elevación de talones de pie
            (new Guid("608e091b-1ddb-4a05-abb7-c0c03e48eee6"), "Pantorrillas", "Variable", "Aislamiento", null, null, 2),                // Elevación de talones sentado
        };

        private static string SqlSiembra()
        {
            static string Texto(string v) => v is null ? "NULL::text" : $"'{v}'";
            var filas = string.Join(",\n    ", Siembra.Select(s =>
                $"('{s.EjercicioId}'::uuid, '{s.Grupo}', '{s.Rol}', '{s.Clase}', {Texto(s.Rodilla)}, {Texto(s.Lumbar)}, {s.Orden}::smallint)"));
            return $@"INSERT INTO ejercicios_protocolo (id, protocolo_codigo, protocolo_version, ejercicio_id, grupo, rol, clase,
    rodilla_excluir_desde, lumbar_excluir_desde, orden_rotacion, fecha_alta, usuario_alta)
SELECT gen_random_uuid(), 'PIERNAS', 'piernas-1.0', e.id, v.grupo, v.rol, v.clase, v.rodilla, v.lumbar, v.orden, now(), 1
FROM (VALUES
    {filas}
) AS v(ejercicio_id, grupo, rol, clase, rodilla, lumbar, orden)
JOIN ejercicios e ON e.id = v.ejercicio_id AND e.fecha_baja IS NULL
ON CONFLICT DO NOTHING;";
        }

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "limitacion_lumbar",
                table: "socios",
                type: "text",
                nullable: false,
                defaultValue: "SinLimitacion");

            migrationBuilder.AddColumn<string>(
                name: "limitacion_rodilla",
                table: "socios",
                type: "text",
                nullable: false,
                defaultValue: "SinLimitacion");

            migrationBuilder.AddColumn<string>(
                name: "origen_datos",
                table: "socios",
                type: "text",
                nullable: false,
                defaultValue: "DatasetExterno"); // solo rellena las filas existentes; no reclasifica (P7)

            migrationBuilder.AddColumn<Guid>(
                name: "prescripcion_ejercicio_id",
                table: "sesiones_ejercicios",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entrada_snapshot",
                table: "prescripciones_entrenamiento",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "es_modificada",
                table: "prescripciones_entrenamiento",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "excluida_estudio",
                table: "prescripciones_entrenamiento",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "numero_generacion",
                table: "prescripciones_entrenamiento",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "origen_datos",
                table: "prescripciones_entrenamiento",
                type: "text",
                nullable: false,
                defaultValue: "DatasetExterno"); // solo rellena las filas existentes

            migrationBuilder.AddColumn<string>(
                name: "protocolo_codigo",
                table: "prescripciones_entrenamiento",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "protocolo_version",
                table: "prescripciones_entrenamiento",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "carga_sugerida_kg",
                table: "prescripciones_ejercicios",
                type: "numeric(7,2)",
                precision: 7,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "grupo",
                table: "prescripciones_ejercicios",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "motivo_seleccion",
                table: "prescripciones_ejercicios",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rol",
                table: "prescripciones_ejercicios",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "tempo_excentrico_seg",
                table: "prescripciones_ejercicios",
                type: "smallint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ejercicios_protocolo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    protocolo_codigo = table.Column<string>(type: "text", nullable: false),
                    protocolo_version = table.Column<string>(type: "text", nullable: false),
                    ejercicio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grupo = table.Column<string>(type: "text", nullable: false),
                    rol = table.Column<string>(type: "text", nullable: false),
                    clase = table.Column<string>(type: "text", nullable: false),
                    rodilla_excluir_desde = table.Column<string>(type: "text", nullable: true),
                    lumbar_excluir_desde = table.Column<string>(type: "text", nullable: true),
                    orden_rotacion = table.Column<short>(type: "smallint", nullable: false),
                    fecha_alta = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usuario_alta = table.Column<int>(type: "integer", nullable: false),
                    fecha_modificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario_modificacion = table.Column<int>(type: "integer", nullable: true),
                    fecha_baja = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario_baja = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ejercicios_protocolo", x => x.id);
                    table.CheckConstraint("CK_EjercicioProtocolo_Clase", "clase IN ('Multiarticular', 'Aislamiento')");
                    table.CheckConstraint("CK_EjercicioProtocolo_Grupo", "grupo IN ('Cuadriceps', 'Isquiotibiales', 'Pantorrillas')");
                    table.CheckConstraint("CK_EjercicioProtocolo_LumbarExcluirDesde", "lumbar_excluir_desde IS NULL OR lumbar_excluir_desde IN ('SinLimitacion', 'Moderada', 'Alta')");
                    table.CheckConstraint("CK_EjercicioProtocolo_RodillaExcluirDesde", "rodilla_excluir_desde IS NULL OR rodilla_excluir_desde IN ('SinLimitacion', 'Moderada', 'Alta')");
                    table.CheckConstraint("CK_EjercicioProtocolo_Rol", "rol IN ('Referencia', 'Variable')");
                    table.ForeignKey(
                        name: "fk_ejercicios_protocolo_ejercicios_ejercicio_id",
                        column: x => x.ejercicio_id,
                        principalTable: "ejercicios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Socio_LimitacionLumbar",
                table: "socios",
                sql: "limitacion_lumbar IN ('SinLimitacion', 'Moderada', 'Alta')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Socio_LimitacionRodilla",
                table: "socios",
                sql: "limitacion_rodilla IN ('SinLimitacion', 'Moderada', 'Alta')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Socio_OrigenDatos",
                table: "socios",
                sql: "origen_datos IN ('Sintetico', 'ValidacionFuncional', 'DatasetExterno', 'Real')");

            migrationBuilder.CreateIndex(
                name: "ix_sesiones_ejercicios_prescripcion_ejercicio_id",
                table: "sesiones_ejercicios",
                column: "prescripcion_ejercicio_id");

            migrationBuilder.CreateIndex(
                name: "ix_prescripciones_entrenamiento_socio_protocolo_generacion",
                table: "prescripciones_entrenamiento",
                columns: new[] { "socio_id", "protocolo_codigo", "numero_generacion" },
                unique: true,
                filter: "fecha_baja IS NULL AND protocolo_codigo IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PrescripcionEntrenamiento_NumeroGeneracion",
                table: "prescripciones_entrenamiento",
                sql: "numero_generacion IS NULL OR numero_generacion > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PrescripcionEntrenamiento_OrigenDatos",
                table: "prescripciones_entrenamiento",
                sql: "origen_datos IN ('Sintetico', 'ValidacionFuncional', 'DatasetExterno', 'Real')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PrescripcionEjercicio_CargaSugeridaKg",
                table: "prescripciones_ejercicios",
                sql: "carga_sugerida_kg IS NULL OR carga_sugerida_kg >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PrescripcionEjercicio_Grupo",
                table: "prescripciones_ejercicios",
                sql: "grupo IS NULL OR grupo IN ('Cuadriceps', 'Isquiotibiales', 'Pantorrillas')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PrescripcionEjercicio_Rol",
                table: "prescripciones_ejercicios",
                sql: "rol IS NULL OR rol IN ('Referencia', 'Variable')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PrescripcionEjercicio_TempoExcentricoSeg",
                table: "prescripciones_ejercicios",
                sql: "tempo_excentrico_seg IS NULL OR (tempo_excentrico_seg >= 1 AND tempo_excentrico_seg <= 10)");

            migrationBuilder.CreateIndex(
                name: "ix_ejercicios_protocolo_ejercicio_id",
                table: "ejercicios_protocolo",
                column: "ejercicio_id");

            migrationBuilder.CreateIndex(
                name: "ix_ejercicios_protocolo_protocolo_version_ejercicio_id",
                table: "ejercicios_protocolo",
                columns: new[] { "protocolo_version", "ejercicio_id" },
                unique: true,
                filter: "fecha_baja IS NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_sesiones_ejercicios_prescripciones_ejercicios_prescripcion_",
                table: "sesiones_ejercicios",
                column: "prescripcion_ejercicio_id",
                principalTable: "prescripciones_ejercicios",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // Siembra del protocolo (después de que el autor corrigió el catálogo con corregir-catalogo-piernas.sql).
            migrationBuilder.Sql(SqlSiembra());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_sesiones_ejercicios_prescripciones_ejercicios_prescripcion_",
                table: "sesiones_ejercicios");

            migrationBuilder.DropTable(
                name: "ejercicios_protocolo");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Socio_LimitacionLumbar",
                table: "socios");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Socio_LimitacionRodilla",
                table: "socios");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Socio_OrigenDatos",
                table: "socios");

            migrationBuilder.DropIndex(
                name: "ix_sesiones_ejercicios_prescripcion_ejercicio_id",
                table: "sesiones_ejercicios");

            migrationBuilder.DropIndex(
                name: "ix_prescripciones_entrenamiento_socio_protocolo_generacion",
                table: "prescripciones_entrenamiento");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PrescripcionEntrenamiento_NumeroGeneracion",
                table: "prescripciones_entrenamiento");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PrescripcionEntrenamiento_OrigenDatos",
                table: "prescripciones_entrenamiento");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PrescripcionEjercicio_CargaSugeridaKg",
                table: "prescripciones_ejercicios");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PrescripcionEjercicio_Grupo",
                table: "prescripciones_ejercicios");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PrescripcionEjercicio_Rol",
                table: "prescripciones_ejercicios");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PrescripcionEjercicio_TempoExcentricoSeg",
                table: "prescripciones_ejercicios");

            migrationBuilder.DropColumn(
                name: "limitacion_lumbar",
                table: "socios");

            migrationBuilder.DropColumn(
                name: "limitacion_rodilla",
                table: "socios");

            migrationBuilder.DropColumn(
                name: "origen_datos",
                table: "socios");

            migrationBuilder.DropColumn(
                name: "prescripcion_ejercicio_id",
                table: "sesiones_ejercicios");

            migrationBuilder.DropColumn(
                name: "entrada_snapshot",
                table: "prescripciones_entrenamiento");

            migrationBuilder.DropColumn(
                name: "es_modificada",
                table: "prescripciones_entrenamiento");

            migrationBuilder.DropColumn(
                name: "excluida_estudio",
                table: "prescripciones_entrenamiento");

            migrationBuilder.DropColumn(
                name: "numero_generacion",
                table: "prescripciones_entrenamiento");

            migrationBuilder.DropColumn(
                name: "origen_datos",
                table: "prescripciones_entrenamiento");

            migrationBuilder.DropColumn(
                name: "protocolo_codigo",
                table: "prescripciones_entrenamiento");

            migrationBuilder.DropColumn(
                name: "protocolo_version",
                table: "prescripciones_entrenamiento");

            migrationBuilder.DropColumn(
                name: "carga_sugerida_kg",
                table: "prescripciones_ejercicios");

            migrationBuilder.DropColumn(
                name: "grupo",
                table: "prescripciones_ejercicios");

            migrationBuilder.DropColumn(
                name: "motivo_seleccion",
                table: "prescripciones_ejercicios");

            migrationBuilder.DropColumn(
                name: "rol",
                table: "prescripciones_ejercicios");

            migrationBuilder.DropColumn(
                name: "tempo_excentrico_seg",
                table: "prescripciones_ejercicios");
        }
    }
}
