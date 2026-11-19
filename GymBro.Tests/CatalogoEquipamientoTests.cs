using GymBro.Application.Services;
using GymBro.Domain.Exceptions;
using GymBro.Domain.Rules;
using Xunit;

namespace GymBro.Tests
{
    /// <summary>
    /// Etapa 9a-2: mapeo del texto de equipamiento a tipos normalizados, texto semántico y validación de textos.
    /// Los valores son los 36 distintos de ejercicios.equipamiento en la base local (evidencia etapa9a2-estado-previo.txt),
    /// con sus espacios de ancho cero (U+200B) y dobles espacios tal como están.
    /// </summary>
    public class CatalogoEquipamientoTests
    {
        private const string Zw = "​";

        /// <summary>Valores reales (incluye los 2 que usa Core: Body Weight y Cable) y el tipo esperado.</summary>
        public static TheoryData<string, string> ValoresReales() => new()
        {
            { "Assisted", CatalogoEquipamiento.MaquinaAsistida },
            { "Assisted  Chest Dip", CatalogoEquipamiento.MaquinaAsistida },
            { "Assisted (machine)", CatalogoEquipamiento.MaquinaAsistida },
            { "Assisted (partner)", CatalogoEquipamiento.PesoCorporal },
            { "Band-assisted", CatalogoEquipamiento.Banda },
            { "Barbell", CatalogoEquipamiento.Barra },
            { "Body Weight", CatalogoEquipamiento.PesoCorporal },
            { "Cable", CatalogoEquipamiento.Polea },
            { "Cable (pull side)", CatalogoEquipamiento.Polea },
            { "Cable  Standing Fly", CatalogoEquipamiento.Polea },
            { "Dumbbell", CatalogoEquipamiento.Mancuerna },
            { "Dynamic Stretch", CatalogoEquipamiento.Estiramiento },
            { "Lever", CatalogoEquipamiento.MaquinaOtra },
            { "Lever (plate loaded)", CatalogoEquipamiento.MaquinaDiscos },
            { "Lever (selectorized)", CatalogoEquipamiento.MaquinaSelectorizada },
            { "Lever (selectorized)" + string.Concat(Enumerable.Repeat(Zw, 7)), CatalogoEquipamiento.MaquinaSelectorizada },
            { "Lever (selectorized)  Chest Dip", CatalogoEquipamiento.MaquinaSelectorizada },
            { "Plyometric", CatalogoEquipamiento.PesoCorporal },
            { "Self-assisted", CatalogoEquipamiento.PesoCorporal },
            { "Self-assisted" + string.Concat(Enumerable.Repeat(Zw, 7)), CatalogoEquipamiento.PesoCorporal },
            { "Sled", CatalogoEquipamiento.MaquinaOtra },
            { "Sled (plate loaded)", CatalogoEquipamiento.MaquinaDiscos },
            { "Sled (selectorized)", CatalogoEquipamiento.MaquinaSelectorizada },
            { "Smith", CatalogoEquipamiento.Smith },
            { "Smith" + string.Concat(Enumerable.Repeat(Zw, 7)), CatalogoEquipamiento.Smith },
            { "Stretch", CatalogoEquipamiento.Estiramiento },
            { "StretchBroomstickDoorway", CatalogoEquipamiento.Estiramiento },
            { "Stretch  Fixed Bar Hugging", CatalogoEquipamiento.Estiramiento },
            { "Stretch (Gluteus Medius & Minimus)", CatalogoEquipamiento.Estiramiento },
            { "Stretch (Iliopsoas)", CatalogoEquipamiento.Estiramiento },
            { "Stretch (Piriformis)", CatalogoEquipamiento.Estiramiento },
            { "Stretch (Tensor Fasciae Latae)", CatalogoEquipamiento.Estiramiento },
            { "Suspended", CatalogoEquipamiento.Suspension },
            { "Suspension", CatalogoEquipamiento.Suspension },
            { "Weighted", CatalogoEquipamiento.PesoAdicional },
            { "Weighted  Chest Dip", CatalogoEquipamiento.PesoAdicional },
        };

        [Theory]
        [MemberData(nameof(ValoresReales))]
        public void Cada_valor_real_del_catalogo_tiene_tipo(string valor, string claveEsperada)
        {
            Assert.Equal(claveEsperada, CatalogoEquipamiento.Mapear(valor));
        }

        [Fact]
        public void Los_valores_reales_son_36_distintos()
        {
            Assert.Equal(36, ValoresReales().Select(f => (string)f[0]).Distinct().Count());
        }

        [Theory]
        [InlineData("Kettlebell")]
        [InlineData("Lever (hydraulic)")]   // variante entre paréntesis desconocida: no se asigna a MAQUINA_OTRA en silencio
        [InlineData("Cable (push side)")]
        [InlineData("")]
        public void Un_valor_desconocido_lanza_una_excepcion_explicita(string valor)
        {
            var ex = Assert.Throws<EquipamientoDesconocidoException>(() => CatalogoEquipamiento.Mapear(valor));
            Assert.Equal(valor, ex.Valor);
        }

        [Fact]
        public void La_normalizacion_quita_ancho_cero_colapsa_espacios_y_pasa_a_minusculas()
        {
            Assert.Equal("lever (selectorized) chest dip", CatalogoEquipamiento.Normalizar($"  Lever (selectorized){Zw}  Chest Dip ﻿"));
        }

        [Fact]
        public void Los_tipos_globales_tienen_clave_e_id_unicos_y_solo_dos_son_siempre_disponibles()
        {
            var tipos = CatalogoEquipamiento.TiposGlobales();
            Assert.Equal(13, tipos.Count);
            Assert.Equal(tipos.Count, tipos.Select(t => t.Id).Distinct().Count());
            Assert.Equal(tipos.Count, tipos.Select(t => t.Clave).Distinct().Count());
            Assert.Equal(new[] { CatalogoEquipamiento.PesoCorporal, CatalogoEquipamiento.Estiramiento },
                tipos.Where(t => t.SiempreDisponible).Select(t => t.Clave));
            Assert.All(CatalogoEquipamiento.Reglas, r => CatalogoEquipamiento.IdDe(r.Clave));
        }

        [Fact]
        public void La_comparacion_de_nombres_ignora_mayusculas_acentos_y_espacios()
        {
            Assert.Equal(CatalogoEquipamiento.ClaveDeComparacion("Elevación  de piernas"),
                CatalogoEquipamiento.ClaveDeComparacion("ELEVACION DE PIERNAS"));
        }

        // ---- Texto semántico: debe coincidir con GymBro.IA/generador_embeddings.py ----

        [Fact]
        public void El_texto_semantico_tiene_el_formato_del_generador_de_python()
        {
            Assert.Equal(
                "Ejercicio: Plancha. Zona objetivo: Core. Músculo principal: Recto abdominal. Técnica y ejecución: Sostener la posición.",
                EjercicioTextoSemantico.Componer("Plancha", "Core", "Recto abdominal", "Sostener la posición."));
        }

        // ---- Validación de textos del Dueño ----

        [Theory]
        [InlineData("Press\u0007 militar")]   // control
        [InlineData("Press​ militar")]   // ancho cero
        [InlineData("Press‮ militar")]   // marca de dirección
        [InlineData("Press\nmilitar")]        // salto de línea en un campo de una línea
        public void Un_texto_con_caracteres_de_control_o_invisibles_se_rechaza(string valor)
        {
            Assert.Throws<ReglaNegocioException>(() => TextoValidado.Validar(valor, "El nombre", 120, obligatorio: true));
        }

        [Fact]
        public void Un_texto_multilinea_admite_saltos_y_se_recorta()
        {
            Assert.Equal("Paso 1.\nPaso 2.", TextoValidado.Validar("  Paso 1.  \r\n  Paso 2.  ", "La ejecución", 2000, true, multilinea: true));
        }

        [Fact]
        public void Un_texto_obligatorio_vacio_o_demasiado_largo_se_rechaza()
        {
            Assert.Throws<ReglaNegocioException>(() => TextoValidado.Validar("   ", "El nombre", 120, obligatorio: true));
            Assert.Throws<ReglaNegocioException>(() => TextoValidado.Validar(new string('a', 121), "El nombre", 120, obligatorio: true));
            Assert.Equal("", TextoValidado.Validar(null, "La preparación", 2000, obligatorio: false));
        }

        [Theory]
        [InlineData("kettlebell", "GIM_KETTLEBELL")]
        [InlineData("barra olimpica 20 kg", "GIM_BARRA_OLIMPICA_20_KG")]
        [InlineData("---", "GIM_TIPO")]
        public void La_clave_de_un_tipo_propio_se_genera_del_nombre(string nombre, string esperada)
        {
            Assert.Equal(esperada, EquipamientoService.GenerarClave(nombre));
        }
    }
}
