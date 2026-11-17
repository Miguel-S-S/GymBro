using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymBro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Etapa8b_BienestarDiario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "registros_bienestar_diario",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    socio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    horas_sueno = table.Column<decimal>(type: "numeric(3,1)", precision: 3, scale: 1, nullable: true),
                    estres = table.Column<short>(type: "smallint", nullable: true),
                    fatiga = table.Column<short>(type: "smallint", nullable: true),
                    peso_kg = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: true),
                    nutricion_categoria = table.Column<short>(type: "smallint", nullable: true),
                    ingesta_agua_litros = table.Column<decimal>(type: "numeric(3,1)", precision: 3, scale: 1, nullable: true),
                    nutricion_notas = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    frecuencia_cardiaca_reposo = table.Column<short>(type: "smallint", nullable: true),
                    fecha_alta = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usuario_alta = table.Column<int>(type: "integer", nullable: false),
                    fecha_modificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario_modificacion = table.Column<int>(type: "integer", nullable: true),
                    fecha_baja = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario_baja = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_registros_bienestar_diario", x => x.id);
                    table.CheckConstraint("CK_RegistroBienestar_Estres", "estres IS NULL OR (estres >= 1 AND estres <= 7)");
                    table.CheckConstraint("CK_RegistroBienestar_Fatiga", "fatiga IS NULL OR (fatiga >= 1 AND fatiga <= 7)");
                    table.CheckConstraint("CK_RegistroBienestar_FcReposo", "frecuencia_cardiaca_reposo IS NULL OR (frecuencia_cardiaca_reposo >= 30 AND frecuencia_cardiaca_reposo <= 120)");
                    table.CheckConstraint("CK_RegistroBienestar_HorasSueno", "horas_sueno IS NULL OR (horas_sueno >= 0 AND horas_sueno <= 24 AND mod(horas_sueno * 2, 1) = 0)");
                    table.CheckConstraint("CK_RegistroBienestar_IngestaAgua", "ingesta_agua_litros IS NULL OR (ingesta_agua_litros >= 0 AND ingesta_agua_litros <= 10)");
                    table.CheckConstraint("CK_RegistroBienestar_NutricionCategoria", "nutricion_categoria IS NULL OR nutricion_categoria IN (1, 2, 3)");
                    table.CheckConstraint("CK_RegistroBienestar_PesoKg", "peso_kg IS NULL OR (peso_kg >= 1 AND peso_kg <= 400)");
                    table.ForeignKey(
                        name: "fk_registros_bienestar_diario_socios_socio_id",
                        column: x => x.socio_id,
                        principalTable: "socios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_registros_bienestar_diario_socio_id_fecha",
                table: "registros_bienestar_diario",
                columns: new[] { "socio_id", "fecha" },
                unique: true,
                filter: "fecha_baja IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "registros_bienestar_diario");
        }
    }
}
