using GymBro.Domain.Enums;

namespace GymBro.Application.Sintetico
{
    /// <summary>
    /// Etapa 7e: parametros del generador sintetico. TODOS son decisiones de diseno del autor sin fuente bibliografica:
    /// sirven para producir datos con verdad conocida y verificar el procedimiento (KPIs, analitica), no para describir
    /// personas. Documentados en docs/pfc/sintetico-parametros.md; si se cambia un valor por defecto, actualizar ese archivo.
    /// </summary>
    public sealed class ParametrosSinteticos
    {
        // ---- Tamano y calendario ----
        public int Semilla { get; set; } = 42;
        public int Socios { get; set; } = 60;
        public int Semanas { get; set; } = 12;
        public int SesionesPorSemana { get; set; } = 2;
        /// <summary>Lunes de la primera semana (dia local). Fijo para que la misma semilla produzca los mismos datos.</summary>
        public DateOnly Inicio { get; set; } = new(2026, 1, 5);

        // ---- Perfil de los socios ----
        public int EdadMinima { get; set; } = 18;
        public int EdadMaxima { get; set; } = 60;
        /// <summary>Probabilidades de Principiante, Intermedio y Avanzado (suman 1).</summary>
        public double[] ProporcionNivel { get; set; } = [0.4, 0.4, 0.2];
        public double ProbabilidadRodillaModerada { get; set; } = 0.15;
        public double ProbabilidadLumbarModerada { get; set; } = 0.15;

        // ---- Carga ----
        /// <summary>Carga inicial (kg) de un ejercicio multiarticular por nivel; el aislamiento usa la mitad y las pantorrillas 0,7.</summary>
        public double[] CargaInicialMultiarticularKg { get; set; } = [40, 70, 100];
        public double FactorAislamiento { get; set; } = 0.5;
        public double FactorPantorrillas { get; set; } = 0.7;
        /// <summary>Factor individual de carga: uniforme entre estos extremos.</summary>
        public double FactorIndividualMin { get; set; } = 0.8;
        public double FactorIndividualMax { get; set; } = 1.2;
        /// <summary>Ganancia de carga por semana (fraccion) para Principiante, Intermedio y Avanzado.</summary>
        public double[] GananciaSemanalPorNivel { get; set; } = [0.02, 0.01, 0.005];
        /// <summary>Ruido multiplicativo de la carga de cada serie (desvio estandar, fraccion).</summary>
        public double RuidoCarga { get; set; } = 0.03;
        /// <summary>Redondeo de la carga registrada (kg).</summary>
        public double RedondeoCargaKg { get; set; } = 0.5;

        // ---- Repeticiones y RIR ----
        /// <summary>Desplazamiento de la media de repeticiones respecto del centro del rango prescrito.</summary>
        public double DesplazamientoRepeticiones { get; set; } = 0.0;
        public double DesvioRepeticiones { get; set; } = 2.0;
        /// <summary>Sesgo del RIR registrado respecto del objetivo (positivo = mas reserva).</summary>
        public double SesgoRir { get; set; } = 0.5;
        public double DesvioRir { get; set; } = 1.0;
        /// <summary>Efecto de la fatiga del dia sobre el RIR por punto respecto de 4 (escala 1 a 7).</summary>
        public double EfectoFatigaSobreRir { get; set; } = 0.2;
        /// <summary>Efecto de la ingesta del dia (Baja, Habitual, Alta) sobre el RIR.</summary>
        public double[] EfectoIngestaSobreRir { get; set; } = [0.5, 0.0, -0.25];
        /// <summary>Probabilidad de completar todas las series prescritas en una sesion (si no, falta una serie).</summary>
        public double ProbabilidadSeriesCompletas { get; set; } = 0.85;

        // ---- Adherencia y abandono ----
        /// <summary>Adherencia individual (probabilidad de asistir a cada sesion programada): uniforme entre estos extremos.</summary>
        public double AdherenciaMin { get; set; } = 0.5;
        public double AdherenciaMax { get; set; } = 1.0;
        /// <summary>
        /// Riesgo semanal de abandono: logistica(b0 + b1 * (1 - adherencia realizada de las ultimas 4 semanas) + b2 * moroso).
        /// </summary>
        public double AbandonoB0 { get; set; } = -5.0;
        public double AbandonoB1 { get; set; } = 4.0;
        public double AbandonoB2 { get; set; } = 1.5;

        // ---- Cuotas ----
        public decimal MontoCuota { get; set; } = 15000m;
        public int DiaVencimiento { get; set; } = 10;
        public double ProbabilidadPagoPuntual { get; set; } = 0.75;
        /// <summary>Si no paga a tiempo: probabilidad de pagar tarde (entre 1 y 20 dias despues del vencimiento).</summary>
        public double ProbabilidadPagoTardio { get; set; } = 0.6;

        // ---- Bienestar ----
        /// <summary>Probabilidad de cargar el registro de bienestar cada dia.</summary>
        public double ProbabilidadBienestarDiario { get; set; } = 0.6;
        public double FcReposoMedia { get; set; } = 62;
        public double FcReposoDesvioEntreSocios { get; set; } = 6;
        public double FcReposoDesvioDiario { get; set; } = 3;
        /// <summary>Probabilidades de ingesta Baja, Habitual y Alta (suman 1).</summary>
        public double[] ProporcionIngesta { get; set; } = [0.2, 0.6, 0.2];
        public double FatigaMedia { get; set; } = 4.0;
        public double FatigaDesvio { get; set; } = 1.2;

        // ---- Horarios (asistencia) ----
        /// <summary>Franjas de inicio (hora local) y su probabilidad: manana 7 a 9, mediodia 12 a 14 y tarde 18 a 21.</summary>
        public double[] ProporcionFranja { get; set; } = [0.3, 0.2, 0.5];

        public void Validar()
        {
            if (Socios < 1 || Socios > 2000) throw new ArgumentException("Socios debe estar entre 1 y 2000.");
            if (Semanas < 1 || Semanas > 104) throw new ArgumentException("Semanas debe estar entre 1 y 104.");
            if (SesionesPorSemana < 1 || SesionesPorSemana > 5) throw new ArgumentException("SesionesPorSemana debe estar entre 1 y 5.");
            if (Inicio.DayOfWeek != DayOfWeek.Monday) throw new ArgumentException("Inicio debe ser un lunes.");
            if (EdadMinima < 16 || EdadMaxima > 90 || EdadMinima > EdadMaxima) throw new ArgumentException("Rango de edad no valido.");
        }

        public int IndiceNivel(NivelExperiencia n) => (int)n - 1;
    }
}
