using GymBro.Application.DTOs;
using GymBro.Application.Services;
using GymBro.Domain.Entities;
using GymBro.Domain.Exceptions;
using Xunit;

namespace GymBro.Tests
{
    /// <summary>Etapa 7d (6.3): resolución de la línea de prescripción al registrar una sesión. Datos SINTÉTICOS.</summary>
    public class VinculoSesionLineaTests
    {
        private static readonly Guid Prensa = Guid.NewGuid();
        private static readonly Guid Curl = Guid.NewGuid();

        private static PrescripcionEntrenamiento Rutina(params (Guid Ejercicio, int Orden)[] lineas)
        {
            var p = new PrescripcionEntrenamiento();
            foreach (var (ejercicio, orden) in lineas)
                p.EjerciciosPrescritos.Add(new PrescripcionEjercicio { EjercicioId = ejercicio, Orden = orden, PrescripcionEntrenamientoId = p.Id });
            return p;
        }

        private static SesionEjercicioRequestDto Ej(Guid ejercicio, int orden, Guid? linea = null) =>
            new() { EjercicioId = ejercicio, Orden = orden, PrescripcionEjercicioId = linea };

        [Fact]
        public void Sin_prescripcion_no_hay_linea()
        {
            Assert.Null(SesionEntrenamientoService.ResolverLinea(null, Ej(Prensa, 1)));
        }

        [Fact]
        public void Con_una_sola_linea_del_ejercicio_se_vincula_aunque_el_orden_sea_otro()
        {
            var r = Rutina((Prensa, 1), (Curl, 2));
            Assert.Equal(r.EjerciciosPrescritos.First().Id, SesionEntrenamientoService.ResolverLinea(r, Ej(Prensa, 7)));
        }

        [Fact]
        public void Con_dos_lineas_del_mismo_ejercicio_decide_el_orden_y_si_no_coincide_queda_sin_vinculo()
        {
            var r = Rutina((Prensa, 1), (Prensa, 3));
            Assert.Equal(r.EjerciciosPrescritos.Last().Id, SesionEntrenamientoService.ResolverLinea(r, Ej(Prensa, 3)));
            Assert.Null(SesionEntrenamientoService.ResolverLinea(r, Ej(Prensa, 2)));
        }

        [Fact]
        public void Un_ejercicio_que_no_esta_en_la_rutina_queda_sin_vinculo()
        {
            Assert.Null(SesionEntrenamientoService.ResolverLinea(Rutina((Prensa, 1)), Ej(Curl, 1)));
        }

        [Fact]
        public void El_id_explicito_debe_pertenecer_a_la_rutina_y_ser_del_mismo_ejercicio()
        {
            var r = Rutina((Prensa, 1), (Curl, 2));
            var curl = r.EjerciciosPrescritos.Last().Id;
            Assert.Equal(curl, SesionEntrenamientoService.ResolverLinea(r, Ej(Curl, 9, curl)));
            Assert.Throws<ReglaNegocioException>(() => SesionEntrenamientoService.ResolverLinea(r, Ej(Prensa, 1, curl)));
            Assert.Throws<ReglaNegocioException>(() => SesionEntrenamientoService.ResolverLinea(r, Ej(Prensa, 1, Guid.NewGuid())));
        }
    }
}
