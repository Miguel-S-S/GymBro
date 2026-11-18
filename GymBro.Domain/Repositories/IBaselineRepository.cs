using GymBro.Domain.Entities;
using GymBro.Domain.Enums;

namespace GymBro.Domain.Repositories
{
    /// <summary>Ejercicio del catálogo sin el vector de embedding (no se necesita para el baseline).</summary>
    public record EjercicioCandidato(
        Guid Id, string Nombre, string Equipamiento, string ZonaObjetivo, string MusculoPrincipal, int Dificultad,
        TipoMedida TipoMedida = TipoMedida.Repeticiones);

    /// <summary>
    /// Accesos de lectura al catálogo y escritura de la prescripción para la generación determinista
    /// de la rutina baseline (Fase 2). Las consultas devuelven siempre el mismo resultado para el mismo
    /// contenido de la base: no hay orden aleatorio.
    /// </summary>
    public interface IBaselineRepository
    {
        /// <summary>Socio sin tracking (el filtro de tenant limita la consulta al gimnasio del usuario).</summary>
        Task<Socio?> GetSocioAsync(Guid socioId);

        Task<MedicionFisica?> GetUltimaMedicionAsync(Guid socioId);

        /// <summary>
        /// Fila de la matriz con coincidencia en género, patologías, categoría de IMC y objetivo, y la edad más
        /// cercana. Si no existe, usa la fila por defecto (mantenimiento, sin patologías, IMC normal).
        /// Devuelve null si la matriz está vacía.
        /// </summary>
        Task<MatrizRecomendacion?> BuscarMatrizAsync(
            string genero, int edad, bool hipertension, bool diabetes, CategoriaIMC categoriaImc, ObjetivoEntrenamiento objetivo);

        Task<bool> HayDosificacionesAsync();

        /// <summary>
        /// Primera fila de dosificación (orden fijo: parte del cuerpo, subzona, id) cuya zona coincide con alguno de los
        /// alias, sin distinguir mayúsculas, y cuyo tipo de medida es el pedido (Etapa 9a-1).
        /// </summary>
        Task<DosificacionVolumetrica?> BuscarDosificacionAsync(IReadOnlyCollection<string> aliasZona, TipoMedida tipoMedida);

        /// <summary>Ejercicios de la zona en orden fijo: dificultad, nombre, id.</summary>
        Task<IReadOnlyList<EjercicioCandidato>> GetEjerciciosDeZonaAsync(IReadOnlyCollection<string> aliasZona);

        Task<bool> TienePendienteDeRevisionAsync(Guid socioId);

        Task AddPrescripcionAsync(PrescripcionEntrenamiento prescripcion);

        /// <summary>Prescripciones del socio con sus ejercicios, de la más reciente a la más antigua.</summary>
        Task<IReadOnlyList<PrescripcionEntrenamiento>> GetPrescripcionesDeSocioAsync(Guid socioId);

        Task SaveChangesAsync();
    }
}
