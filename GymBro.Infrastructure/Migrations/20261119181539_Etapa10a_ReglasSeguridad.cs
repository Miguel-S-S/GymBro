using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymBro.Infrastructure.Migrations
{
    /// <summary>
    /// Etapa 10a: tabla alertas_seguridad (reglas deterministas de seguridad; datos de salud) con CHECK de regla, tipo,
    /// severidad y estado, e índice único (socio_id, regla_codigo, clave_origen) para la idempotencia; y columna jsonb
    /// violaciones_limites en revisiones_prescripcion (R-LIM). Reversible: el Down borra la tabla y la columna.
    /// </summary>
    public partial class Etapa10a_ReglasSeguridad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "violaciones_limites",
                table: "revisiones_prescripcion",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "alertas_seguridad",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    socio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gimnasio_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sesion_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sesion_ejercicio_id = table.Column<Guid>(type: "uuid", nullable: true),
                    registro_bienestar_id = table.Column<Guid>(type: "uuid", nullable: true),
                    regla_codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    clave_origen = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    severidad = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    mensaje = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    detalle = table.Column<string>(type: "jsonb", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    vista_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    vista_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    fecha_alta = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usuario_alta = table.Column<int>(type: "integer", nullable: false),
                    fecha_modificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario_modificacion = table.Column<int>(type: "integer", nullable: true),
                    fecha_baja = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario_baja = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_alertas_seguridad", x => x.id);
                    table.CheckConstraint("CK_AlertaSeguridad_Estado", "estado IN ('Pendiente', 'Vista')");
                    table.CheckConstraint("CK_AlertaSeguridad_ReglaCodigo", "regla_codigo IN ('R-RIR-1', 'R-PROG-1', 'R-FC-1')");
                    table.CheckConstraint("CK_AlertaSeguridad_Severidad", "severidad IN ('Informativa', 'Advertencia')");
                    table.CheckConstraint("CK_AlertaSeguridad_Tipo", "tipo IN ('Alerta', 'Sugerencia')");
                    table.ForeignKey(
                        name: "fk_alertas_seguridad_socios_socio_id",
                        column: x => x.socio_id,
                        principalTable: "socios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_alertas_seguridad_registro_bienestar_id",
                table: "alertas_seguridad",
                column: "registro_bienestar_id");

            migrationBuilder.CreateIndex(
                name: "ix_alertas_seguridad_sesion_id",
                table: "alertas_seguridad",
                column: "sesion_id");

            migrationBuilder.CreateIndex(
                name: "ix_alertas_seguridad_socio_id_regla_codigo_clave_origen",
                table: "alertas_seguridad",
                columns: new[] { "socio_id", "regla_codigo", "clave_origen" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alertas_seguridad");

            migrationBuilder.DropColumn(
                name: "violaciones_limites",
                table: "revisiones_prescripcion");
        }
    }
}
