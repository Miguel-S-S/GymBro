using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymBro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AccesoYTenants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "consentimiento_datos_salud_fecha",
                table: "socios",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "dni",
                table: "socios",
                type: "character varying(9)",
                maxLength: 9,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "gimnasio_id",
                table: "socios",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rol = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    gimnasio_id = table.Column<Guid>(type: "uuid", nullable: true),
                    accion = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    entidad = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    entidad_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    detalle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "gimnasios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    codigo = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    requiere_aprobacion_rutinas = table.Column<bool>(type: "boolean", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    fecha_alta = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usuario_alta = table.Column<int>(type: "integer", nullable: false),
                    fecha_modificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario_modificacion = table.Column<int>(type: "integer", nullable: true),
                    fecha_baja = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario_baja = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gimnasios", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "revisiones_prescripcion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    prescripcion_entrenamiento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revisor_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    comentario = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_revisiones_prescripcion", x => x.id);
                    table.ForeignKey(
                        name: "fk_revisiones_prescripcion_prescripciones_entrenamiento_prescr",
                        column: x => x.prescripcion_entrenamiento_id,
                        principalTable: "prescripciones_entrenamiento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "solicitudes_vinculacion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    socio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gimnasio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre_socio = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    dni_declarado = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    resuelta_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fecha_resolucion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    fecha_alta = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usuario_alta = table.Column<int>(type: "integer", nullable: false),
                    fecha_modificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario_modificacion = table.Column<int>(type: "integer", nullable: true),
                    fecha_baja = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario_baja = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_solicitudes_vinculacion", x => x.id);
                    table.ForeignKey(
                        name: "fk_solicitudes_vinculacion_gimnasios_gimnasio_id",
                        column: x => x.gimnasio_id,
                        principalTable: "gimnasios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_solicitudes_vinculacion_socios_socio_id",
                        column: x => x.socio_id,
                        principalTable: "socios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "usuarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    nombre_completo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    rol = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    gimnasio_id = table.Column<Guid>(type: "uuid", nullable: true),
                    socio_id = table.Column<Guid>(type: "uuid", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    fecha_alta = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usuario_alta = table.Column<int>(type: "integer", nullable: false),
                    fecha_modificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario_modificacion = table.Column<int>(type: "integer", nullable: true),
                    fecha_baja = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario_baja = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuarios", x => x.id);
                    table.CheckConstraint("CK_Usuario_Rol_Alcance", "(rol = 'SuperAdministrador' AND gimnasio_id IS NULL AND socio_id IS NULL) OR (rol IN ('AdministradorGimnasio','Entrenador') AND gimnasio_id IS NOT NULL AND socio_id IS NULL) OR (rol = 'Socio' AND socio_id IS NOT NULL AND gimnasio_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_usuarios_gimnasios_gimnasio_id",
                        column: x => x.gimnasio_id,
                        principalTable: "gimnasios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_usuarios_socios_socio_id",
                        column: x => x.socio_id,
                        principalTable: "socios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "asignaciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    gimnasio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entrenador_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    socio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_alta = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usuario_alta = table.Column<int>(type: "integer", nullable: false),
                    fecha_modificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario_modificacion = table.Column<int>(type: "integer", nullable: true),
                    fecha_baja = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario_baja = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asignaciones", x => x.id);
                    table.ForeignKey(
                        name: "fk_asignaciones_gimnasios_gimnasio_id",
                        column: x => x.gimnasio_id,
                        principalTable: "gimnasios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_asignaciones_socios_socio_id",
                        column: x => x.socio_id,
                        principalTable: "socios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_asignaciones_usuarios_entrenador_usuario_id",
                        column: x => x.entrenador_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_socios_dni",
                table: "socios",
                column: "dni",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_socios_gimnasio_id",
                table: "socios",
                column: "gimnasio_id");

            migrationBuilder.CreateIndex(
                name: "ix_asignaciones_entrenador_usuario_id_socio_id",
                table: "asignaciones",
                columns: new[] { "entrenador_usuario_id", "socio_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_asignaciones_gimnasio_id",
                table: "asignaciones",
                column: "gimnasio_id");

            migrationBuilder.CreateIndex(
                name: "ix_asignaciones_socio_id",
                table: "asignaciones",
                column: "socio_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_fecha",
                table: "audit_logs",
                column: "fecha");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_gimnasio_id",
                table: "audit_logs",
                column: "gimnasio_id");

            migrationBuilder.CreateIndex(
                name: "ix_gimnasios_codigo",
                table: "gimnasios",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_revisiones_prescripcion_prescripcion_entrenamiento_id",
                table: "revisiones_prescripcion",
                column: "prescripcion_entrenamiento_id");

            migrationBuilder.CreateIndex(
                name: "ix_solicitudes_vinculacion_gimnasio_id_estado",
                table: "solicitudes_vinculacion",
                columns: new[] { "gimnasio_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "ix_solicitudes_vinculacion_socio_id_gimnasio_id",
                table: "solicitudes_vinculacion",
                columns: new[] { "socio_id", "gimnasio_id" },
                unique: true,
                filter: "estado = 'Pendiente'");

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_email",
                table: "usuarios",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_gimnasio_id",
                table: "usuarios",
                column: "gimnasio_id");

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_socio_id",
                table: "usuarios",
                column: "socio_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_socios_gimnasios_gimnasio_id",
                table: "socios",
                column: "gimnasio_id",
                principalTable: "gimnasios",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_socios_gimnasios_gimnasio_id",
                table: "socios");

            migrationBuilder.DropTable(
                name: "asignaciones");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "revisiones_prescripcion");

            migrationBuilder.DropTable(
                name: "solicitudes_vinculacion");

            migrationBuilder.DropTable(
                name: "usuarios");

            migrationBuilder.DropTable(
                name: "gimnasios");

            migrationBuilder.DropIndex(
                name: "ix_socios_dni",
                table: "socios");

            migrationBuilder.DropIndex(
                name: "ix_socios_gimnasio_id",
                table: "socios");

            migrationBuilder.DropColumn(
                name: "consentimiento_datos_salud_fecha",
                table: "socios");

            migrationBuilder.DropColumn(
                name: "dni",
                table: "socios");

            migrationBuilder.DropColumn(
                name: "gimnasio_id",
                table: "socios");
        }
    }
}
