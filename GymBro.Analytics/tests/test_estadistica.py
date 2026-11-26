"""Etapa 12: pruebas de las funciones estadisticas con datos de resultado conocido (python -m unittest discover -s GymBro.Analytics/tests)."""
import sys
import unittest
from pathlib import Path

import numpy as np
import pandas as pd

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from gymbro_analytics.estadistica import (agregar_por_socio_modal, asistencia_vs_antiguedad, comparar_grupos,  # noqa: E402
                                          correlacion, descriptiva, horas_pico, morosidad_por_periodo,
                                          retencion_por_cohorte)


class DescriptivaTests(unittest.TestCase):
    def test_conteos_faltantes_y_cuartiles(self):
        d = descriptiva(pd.DataFrame({"x": [1, 2, 3, 4, None]}), ["x"]).iloc[0]
        self.assertEqual((d["n"], d["faltantes"]), (4, 1))
        self.assertAlmostEqual(d["faltantes_%"], 20.0)
        self.assertAlmostEqual(d["media"], 2.5)
        self.assertAlmostEqual(d["mediana"], 2.5)
        self.assertAlmostEqual(d["desvio"], np.std([1, 2, 3, 4], ddof=1))


class CorrelacionTests(unittest.TestCase):
    def test_relacion_lineal_perfecta(self):
        c = correlacion(pd.Series([1, 2, 3, 4, 5]), pd.Series([2, 4, 6, 8, 10]))
        self.assertEqual(c.n, 5)
        self.assertAlmostEqual(c.pearson_r, 1.0)
        self.assertAlmostEqual(c.spearman_rho, 1.0)

    def test_monotona_no_lineal_spearman_1_pearson_menor(self):
        x = pd.Series([1, 2, 3, 4, 5, 6])
        c = correlacion(x, x ** 3)
        self.assertAlmostEqual(c.spearman_rho, 1.0)
        self.assertLess(c.pearson_r, 1.0)

    def test_valor_de_referencia(self):
        # Pares conocidos: r de Pearson = 0,8 exacto por construccion (x e y estandarizables).
        x = pd.Series([1, 2, 3, 4, 5])
        y = pd.Series([2, 1, 4, 3, 5])
        self.assertAlmostEqual(correlacion(x, y).pearson_r, 0.8)

    def test_descarta_pares_incompletos_y_n_insuficiente(self):
        c = correlacion(pd.Series([1, None, 3]), pd.Series([1, 2, None]))
        self.assertEqual(c.n, 1)
        self.assertTrue(np.isnan(c.pearson_r))


class GruposTests(unittest.TestCase):
    def test_anova_de_libro(self):
        # Tres grupos de 3 con medias 2, 5 y 8: F = 27 (SSb = 54, SSw = 6, gl 2 y 6) y eta cuadrado = 54 / 60 = 0,9.
        v = pd.Series([1, 2, 3, 4, 5, 6, 7, 8, 9])
        g = pd.Series(["a"] * 3 + ["b"] * 3 + ["c"] * 3)
        r = comparar_grupos(v, g)
        self.assertAlmostEqual(r.anova_f, 27.0)
        self.assertAlmostEqual(r.anova_p, 0.001, places=3)
        self.assertAlmostEqual(r.eta_cuadrado, 0.9)
        self.assertAlmostEqual(r.kruskal_h, 7.2)
        self.assertTrue(r.supuestos_ok)
        self.assertEqual(r.prueba_recomendada, "ANOVA")

    def test_varianzas_muy_distintas_recomiendan_kruskal(self):
        rng = np.random.default_rng(1)
        v = pd.Series(np.concatenate([rng.normal(0, 0.1, 40), rng.normal(0, 10, 40)]))
        g = pd.Series(["a"] * 40 + ["b"] * 40)
        r = comparar_grupos(v, g)
        self.assertLess(r.levene_p, 0.05)
        self.assertEqual(r.prueba_recomendada, "Kruskal-Wallis")

    def test_agregacion_por_socio_una_fila_por_socio_con_su_categoria_modal(self):
        df = pd.DataFrame({"socio": ["s1", "s1", "s1", "s2", "s2"],
                           "cat": ["Baja", "Baja", "Alta", "Alta", "Baja"],
                           "res": [1.0, 2.0, 3.0, 4.0, 6.0]})
        a = agregar_por_socio_modal(df, "socio", "cat", "res").set_index("socio")
        self.assertEqual(len(a), 2)
        self.assertEqual(a.loc["s1", "cat"], "Baja")
        self.assertAlmostEqual(a.loc["s1", "res"], 2.0)
        self.assertEqual(a.loc["s2", "cat"], "Alta")  # empate: orden alfabetico
        self.assertAlmostEqual(a.loc["s2", "res"], 5.0)


class GestionTests(unittest.TestCase):
    def test_retencion_por_cohorte(self):
        a = pd.DataFrame({"socio_id": ["a", "a", "b", "c"],
                          "fecha": pd.to_datetime(["2026-01-05", "2026-02-10", "2026-01-20", "2026-02-03"])})
        t = retencion_por_cohorte(a)
        self.assertEqual(t.loc["2026-01", "socios"], 2)
        self.assertEqual(t.loc["2026-01", "mes 0"], 100.0)
        self.assertEqual(t.loc["2026-01", "mes 1"], 50.0)   # solo "a" volvio en febrero
        self.assertEqual(t.loc["2026-02", "mes 0"], 100.0)

    def test_morosidad_derivada(self):
        c = pd.DataFrame({"socio_id": ["a", "b", "c"], "anio": [2026] * 3, "mes": [1] * 3,
                          "vencimiento": pd.to_datetime(["2026-01-10"] * 3),
                          "fecha_pago": pd.to_datetime(["2026-01-05", "2026-01-15", None])})
        m = morosidad_por_periodo(c, pd.Timestamp("2026-02-01")).iloc[0]
        self.assertEqual((m["cuotas"], m["vencidas"], m["pagadas_tarde"]), (3, 1, 1))
        self.assertAlmostEqual(m["vencidas_%"], 33.3)

    def test_horas_pico_en_hora_local(self):
        a = pd.DataFrame({"instante": pd.to_datetime(["2026-03-02 21:30:00+00:00", "2026-03-02 21:45:00+00:00"], utc=True)})
        h = horas_pico(a)
        self.assertEqual(len(h), 1)
        self.assertEqual((h.iloc[0]["dia"], h.iloc[0]["hora"], h.iloc[0]["asistencias"]), (0, 18, 2))  # lunes 18 h en Buenos Aires

    def test_asistencia_vs_antiguedad(self):
        socios = pd.DataFrame({"socio_id": ["a", "b"], "fecha_alta": pd.to_datetime(["2025-12-01", "2026-01-01"])})
        a = pd.DataFrame({"socio_id": ["a"] * 4, "fecha": pd.to_datetime(["2026-01-01", "2026-01-08", "2026-01-15", "2026-01-15"])})
        d = asistencia_vs_antiguedad(socios, a).set_index("socio_id")
        self.assertAlmostEqual(d.loc["a", "asistencias_por_semana"], 4 / 2)
        self.assertEqual(d.loc["b", "asistencias_por_semana"], 0)
        self.assertEqual(d.loc["a", "antiguedad_dias"], 45)


if __name__ == "__main__":
    unittest.main()
