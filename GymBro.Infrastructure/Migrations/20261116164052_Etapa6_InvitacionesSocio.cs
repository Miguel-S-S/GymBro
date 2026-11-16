using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymBro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Etapa6_InvitacionesSocio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "invitaciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    gimnasio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    socio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expira_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usada_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revocada_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    fecha_alta = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usuario_alta = table.Column<int>(type: "integer", nullable: false),
                    fecha_modificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario_modificacion = table.Column<int>(type: "integer", nullable: true),
                    fecha_baja = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario_baja = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_invitaciones", x => x.id);
                    table.ForeignKey(
                        name: "fk_invitaciones_gimnasios_gimnasio_id",
                        column: x => x.gimnasio_id,
                        principalTable: "gimnasios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_invitaciones_socios_socio_id",
                        column: x => x.socio_id,
                        principalTable: "socios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_invitaciones_gimnasio_id",
                table: "invitaciones",
                column: "gimnasio_id");

            migrationBuilder.CreateIndex(
                name: "ix_invitaciones_socio_id",
                table: "invitaciones",
                column: "socio_id");

            migrationBuilder.CreateIndex(
                name: "ix_invitaciones_token_hash",
                table: "invitaciones",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "invitaciones");
        }
    }
}
