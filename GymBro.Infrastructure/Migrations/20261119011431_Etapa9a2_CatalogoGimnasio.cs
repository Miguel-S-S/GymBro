using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymBro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Etapa9a2_CatalogoGimnasio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "gimnasio_id",
                table: "ejercicios",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tipo_equipamiento_id",
                table: "ejercicios",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "tipos_equipamiento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    clave = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    nombre = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    siempre_disponible = table.Column<bool>(type: "boolean", nullable: false),
                    gimnasio_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fecha_alta = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usuario_alta = table.Column<int>(type: "integer", nullable: false),
                    fecha_modificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario_modificacion = table.Column<int>(type: "integer", nullable: true),
                    fecha_baja = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario_baja = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tipos_equipamiento", x => x.id);
                    table.ForeignKey(
                        name: "fk_tipos_equipamiento_gimnasios_gimnasio_id",
                        column: x => x.gimnasio_id,
                        principalTable: "gimnasios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "equipamientos_gimnasio",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    gimnasio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_equipamiento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cantidad = table.Column<int>(type: "integer", nullable: true),
                    fecha_alta = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usuario_alta = table.Column<int>(type: "integer", nullable: false),
                    fecha_modificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario_modificacion = table.Column<int>(type: "integer", nullable: true),
                    fecha_baja = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario_baja = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_equipamientos_gimnasio", x => x.id);
                    table.CheckConstraint("CK_EquipamientoGimnasio_Cantidad", "cantidad IS NULL OR cantidad >= 1");
                    table.ForeignKey(
                        name: "fk_equipamientos_gimnasio_gimnasios_gimnasio_id",
                        column: x => x.gimnasio_id,
                        principalTable: "gimnasios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_equipamientos_gimnasio_tipos_equipamiento_tipo_equipamiento",
                        column: x => x.tipo_equipamiento_id,
                        principalTable: "tipos_equipamiento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ejercicios_gimnasio_id",
                table: "ejercicios",
                column: "gimnasio_id");

            migrationBuilder.CreateIndex(
                name: "ix_ejercicios_tipo_equipamiento_id",
                table: "ejercicios",
                column: "tipo_equipamiento_id");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Ejercicio_OrigenGimnasio",
                table: "ejercicios",
                sql: "(origen = 'Gimnasio' AND gimnasio_id IS NOT NULL) OR (origen <> 'Gimnasio' AND gimnasio_id IS NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_equipamientos_gimnasio_gimnasio_id_tipo_equipamiento_id",
                table: "equipamientos_gimnasio",
                columns: new[] { "gimnasio_id", "tipo_equipamiento_id" },
                unique: true,
                filter: "fecha_baja IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_equipamientos_gimnasio_tipo_equipamiento_id",
                table: "equipamientos_gimnasio",
                column: "tipo_equipamiento_id");

            migrationBuilder.CreateIndex(
                name: "ix_tipos_equipamiento_clave_global",
                table: "tipos_equipamiento",
                column: "clave",
                unique: true,
                filter: "gimnasio_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_tipos_equipamiento_gimnasio_id_clave",
                table: "tipos_equipamiento",
                columns: new[] { "gimnasio_id", "clave" },
                unique: true,
                filter: "gimnasio_id IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_ejercicios_gimnasios_gimnasio_id",
                table: "ejercicios",
                column: "gimnasio_id",
                principalTable: "gimnasios",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_ejercicios_tipos_equipamiento_tipo_equipamiento_id",
                table: "ejercicios",
                column: "tipo_equipamiento_id",
                principalTable: "tipos_equipamiento",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_ejercicios_gimnasios_gimnasio_id",
                table: "ejercicios");

            migrationBuilder.DropForeignKey(
                name: "fk_ejercicios_tipos_equipamiento_tipo_equipamiento_id",
                table: "ejercicios");

            migrationBuilder.DropTable(
                name: "equipamientos_gimnasio");

            migrationBuilder.DropTable(
                name: "tipos_equipamiento");

            migrationBuilder.DropIndex(
                name: "ix_ejercicios_gimnasio_id",
                table: "ejercicios");

            migrationBuilder.DropIndex(
                name: "ix_ejercicios_tipo_equipamiento_id",
                table: "ejercicios");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Ejercicio_OrigenGimnasio",
                table: "ejercicios");

            migrationBuilder.DropColumn(
                name: "gimnasio_id",
                table: "ejercicios");

            migrationBuilder.DropColumn(
                name: "tipo_equipamiento_id",
                table: "ejercicios");
        }
    }
}
