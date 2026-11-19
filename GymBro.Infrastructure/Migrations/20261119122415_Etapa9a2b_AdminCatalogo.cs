using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymBro.Infrastructure.Migrations
{
    /// <summary>
    /// Etapa 9a-2b: ejercicios desactivables (activo, por defecto true), autor del ejercicio propio
    /// (creado_por_usuario_id, nulo en las filas existentes, FK Restrict) y estado administrativo del socio
    /// (estado, texto Activo o Inactivo, por defecto Activo). Reversible: el Down quita las tres columnas.
    /// </summary>
    public partial class Etapa9a2b_AdminCatalogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "estado",
                table: "socios",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Activo"); // las fichas existentes quedan activas

            migrationBuilder.AddColumn<bool>(
                name: "activo",
                table: "ejercicios",
                type: "boolean",
                nullable: false,
                defaultValue: true); // los ejercicios existentes quedan activos

            migrationBuilder.AddColumn<Guid>(
                name: "creado_por_usuario_id",
                table: "ejercicios",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Socio_Estado",
                table: "socios",
                sql: "estado IN ('Activo', 'Inactivo')");

            migrationBuilder.CreateIndex(
                name: "ix_ejercicios_creado_por_usuario_id",
                table: "ejercicios",
                column: "creado_por_usuario_id");

            migrationBuilder.AddForeignKey(
                name: "fk_ejercicios_usuarios_creado_por_usuario_id",
                table: "ejercicios",
                column: "creado_por_usuario_id",
                principalTable: "usuarios",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_ejercicios_usuarios_creado_por_usuario_id",
                table: "ejercicios");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Socio_Estado",
                table: "socios");

            migrationBuilder.DropIndex(
                name: "ix_ejercicios_creado_por_usuario_id",
                table: "ejercicios");

            migrationBuilder.DropColumn(
                name: "estado",
                table: "socios");

            migrationBuilder.DropColumn(
                name: "activo",
                table: "ejercicios");

            migrationBuilder.DropColumn(
                name: "creado_por_usuario_id",
                table: "ejercicios");
        }
    }
}
