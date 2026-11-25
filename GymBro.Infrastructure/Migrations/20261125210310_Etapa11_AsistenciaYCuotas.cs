using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymBro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Etapa11_AsistenciaYCuotas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "asistencias",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    socio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gimnasio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    instante = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    metodo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    registrado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    origen_datos = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    fecha_alta = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usuario_alta = table.Column<int>(type: "integer", nullable: false),
                    fecha_modificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario_modificacion = table.Column<int>(type: "integer", nullable: true),
                    fecha_baja = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario_baja = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asistencias", x => x.id);
                    table.CheckConstraint("CK_Asistencia_Metodo", "metodo IN ('Qr', 'Manual')");
                    table.CheckConstraint("CK_Asistencia_OrigenDatos", "origen_datos IN ('Sintetico', 'ValidacionFuncional', 'DatasetExterno', 'Real')");
                    table.ForeignKey(
                        name: "fk_asistencias_gimnasios_gimnasio_id",
                        column: x => x.gimnasio_id,
                        principalTable: "gimnasios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_asistencias_socios_socio_id",
                        column: x => x.socio_id,
                        principalTable: "socios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_asistencias_usuarios_registrado_por_usuario_id",
                        column: x => x.registrado_por_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cuotas_socio",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    socio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gimnasio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    anio = table.Column<int>(type: "integer", nullable: false),
                    mes = table.Column<int>(type: "integer", nullable: false),
                    monto = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    vencimiento = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_pago = table.Column<DateOnly>(type: "date", nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    origen_datos = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    fecha_alta = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usuario_alta = table.Column<int>(type: "integer", nullable: false),
                    fecha_modificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario_modificacion = table.Column<int>(type: "integer", nullable: true),
                    fecha_baja = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario_baja = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cuotas_socio", x => x.id);
                    table.CheckConstraint("CK_CuotaSocio_Estado", "estado IN ('Pendiente', 'Pagada')");
                    table.CheckConstraint("CK_CuotaSocio_Monto", "monto > 0");
                    table.CheckConstraint("CK_CuotaSocio_OrigenDatos", "origen_datos IN ('Sintetico', 'ValidacionFuncional', 'DatasetExterno', 'Real')");
                    table.CheckConstraint("CK_CuotaSocio_Pago", "(estado = 'Pagada' AND fecha_pago IS NOT NULL) OR (estado = 'Pendiente' AND fecha_pago IS NULL)");
                    table.CheckConstraint("CK_CuotaSocio_Periodo", "anio >= 2000 AND anio <= 2100 AND mes >= 1 AND mes <= 12");
                    table.ForeignKey(
                        name: "fk_cuotas_socio_gimnasios_gimnasio_id",
                        column: x => x.gimnasio_id,
                        principalTable: "gimnasios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cuotas_socio_socios_socio_id",
                        column: x => x.socio_id,
                        principalTable: "socios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_asistencias_gimnasio_id_fecha",
                table: "asistencias",
                columns: new[] { "gimnasio_id", "fecha" });

            migrationBuilder.CreateIndex(
                name: "ix_asistencias_registrado_por_usuario_id",
                table: "asistencias",
                column: "registrado_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_asistencias_socio_id_fecha",
                table: "asistencias",
                columns: new[] { "socio_id", "fecha" },
                unique: true,
                filter: "fecha_baja IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_cuotas_socio_gimnasio_id_anio_mes",
                table: "cuotas_socio",
                columns: new[] { "gimnasio_id", "anio", "mes" });

            migrationBuilder.CreateIndex(
                name: "ix_cuotas_socio_gimnasio_id_estado_vencimiento",
                table: "cuotas_socio",
                columns: new[] { "gimnasio_id", "estado", "vencimiento" });

            migrationBuilder.CreateIndex(
                name: "ix_cuotas_socio_socio_id_anio_mes",
                table: "cuotas_socio",
                columns: new[] { "socio_id", "anio", "mes" },
                unique: true,
                filter: "fecha_baja IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "asistencias");

            migrationBuilder.DropTable(
                name: "cuotas_socio");
        }
    }
}
