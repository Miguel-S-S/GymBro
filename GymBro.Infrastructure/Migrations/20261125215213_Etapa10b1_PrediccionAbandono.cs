using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymBro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Etapa10b1_PrediccionAbandono : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "prediccion_abandono",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    socio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    puntaje = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    version_modelo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
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
                    table.PrimaryKey("pk_prediccion_abandono", x => x.id);
                    table.CheckConstraint("CK_PrediccionAbandono_OrigenDatos", "origen_datos IN ('Sintetico', 'ValidacionFuncional', 'DatasetExterno', 'Real')");
                    table.CheckConstraint("CK_PrediccionAbandono_Puntaje", "puntaje >= 0 AND puntaje <= 1");
                    table.ForeignKey(
                        name: "fk_prediccion_abandono_socios_socio_id",
                        column: x => x.socio_id,
                        principalTable: "socios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_prediccion_abandono_socio_id_fecha_version_modelo",
                table: "prediccion_abandono",
                columns: new[] { "socio_id", "fecha", "version_modelo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "prediccion_abandono");
        }
    }
}
