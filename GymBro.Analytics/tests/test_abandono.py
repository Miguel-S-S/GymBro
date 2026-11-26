"""Etapa 10b-1: pruebas de la extraccion, de las variables (sin informacion futura) y de la separacion temporal.

python -m unittest discover -s GymBro.Analytics/tests
La prueba contra una base real se ejecuta solo si ANALITICA_PG_CLAVE esta definida (si no, se informa como omitida).
"""
import os
import sys
import unittest
from pathlib import Path

import numpy as np
import pandas as pd

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from gymbro_analytics import extraccion  # noqa: E402
from gymbro_analytics.modelos import entrenar_y_evaluar, metricas, separar_temporal  # noqa: E402
from gymbro_analytics.variables import VARIABLES, construir, fechas_de_corte  # noqa: E402


class CursorFalso:
    def __init__(self, filas, columnas):
        self._filas, self.description, self.ejecutado = filas, [(c,) for c in columnas], None

    def execute(self, sql, parametros):
        self.ejecutado = (sql, parametros)

    def fetchall(self):
        return self._filas

    def close(self):
        pass


class ConexionFalsa:
    def __init__(self, filas, columnas):
        self.cursor_ = CursorFalso(filas, columnas)

    def cursor(self):
        return self.cursor_


def socios_df(n=1, alta="2025-01-01", nacimiento="1990-06-15"):
    return pd.DataFrame({
        "socio_id": [f"s{i}" for i in range(n)], "gimnasio_id": ["g"] * n,
        "fecha_nacimiento": pd.to_datetime([nacimiento] * n), "nivel": ["Intermedio"] * n,
        "fecha_alta": pd.to_datetime([alta] * n), "origen_datos": ["Sintetico"] * n, "nivel_ordinal": pd.array([2] * n, dtype="Int64"),
    })


def asis(socio, fechas):
    return pd.DataFrame({"socio_id": socio, "gimnasio_id": "g", "fecha": pd.to_datetime(fechas)})


CUOTAS_VACIAS = pd.DataFrame(columns=["socio_id", "vencimiento", "fecha_pago"])


class ExtraccionTests(unittest.TestCase):
    def test_socios_normaliza_tipos_y_usa_parametros_sin_sql_dinamico(self):
        con = ConexionFalsa([("a", "g", "1990-01-01", "Avanzado", "2025-03-01 02:00:00+00", "Sintetico")],
                            ["socio_id", "gimnasio_id", "fecha_nacimiento", "nivel", "fecha_alta", "origen_datos"])
        df = extraccion.socios(con, gimnasio="g")
        self.assertEqual(df.loc[0, "nivel_ordinal"], 3)
        self.assertEqual(df.loc[0, "fecha_alta"], pd.Timestamp("2025-02-28"))  # 02:00 UTC es 23:00 del dia anterior en Buenos Aires
        sql, parametros = con.cursor_.ejecutado
        self.assertIn("analitica.v_socios", sql)
        self.assertEqual(parametros, {"gimnasio": "g"})

    def test_cuotas_convierte_fechas_y_montos(self):
        con = ConexionFalsa([("a", "g", 2026, 1, "15000.00", "2026-01-10", None, "Pendiente")],
                            ["socio_id", "gimnasio_id", "anio", "mes", "monto", "vencimiento", "fecha_pago", "estado"])
        df = extraccion.cuotas(con)
        self.assertTrue(pd.isna(df.loc[0, "fecha_pago"]))
        self.assertEqual(df.loc[0, "monto"], 15000.0)
        self.assertIn("analitica.v_cuotas", con.cursor_.ejecutado[0])


class VariablesTests(unittest.TestCase):
    def test_variables_con_valores_conocidos(self):
        t = pd.Timestamp("2026-03-02")  # lunes
        a = asis("s0", ["2026-02-03", "2026-02-10", "2026-02-20", "2026-02-25", "2026-03-05"])
        cuotas = pd.DataFrame({"socio_id": ["s0", "s0", "s0"],
                               "vencimiento": pd.to_datetime(["2026-01-10", "2026-02-10", "2026-03-10"]),
                               "fecha_pago": pd.to_datetime(["2026-01-05", None, None])})
        f = construir(socios_df(), a, cuotas, [t]).iloc[0]
        self.assertEqual(f["frecuencia_4s"], 4 / 4)          # 03/02, 10/02, 20/02 y 25/02 estan en [02/02, 02/03)
        self.assertEqual(f["dias_desde_ultima"], 5)          # 25/02 -> 02/03
        self.assertEqual(f["tendencia"], 2 - 2)              # [16/02, 02/03): 20 y 25; [02/02, 16/02): 03 y 10
        self.assertEqual(f["cuotas_vencidas"], 1)            # la de febrero; marzo todavia no vencio; enero pagada
        self.assertEqual(f["edad"], 35)
        self.assertEqual(f["antiguedad_dias"], (t - pd.Timestamp("2025-01-01")).days)
        self.assertEqual(f["abandono"], 0)                    # asiste el 05/03

    def test_sin_fuga_las_variables_no_cambian_con_eventos_posteriores_al_corte(self):
        t = pd.Timestamp("2026-03-02")
        base = asis("s0", ["2026-02-20", "2026-02-25"])
        futuro = pd.concat([base, asis("s0", ["2026-03-02", "2026-03-03", "2026-03-20"])])
        cuotas = pd.DataFrame({"socio_id": ["s0"], "vencimiento": pd.to_datetime(["2026-02-10"]),
                               "fecha_pago": pd.to_datetime(["2026-03-05"])})  # se paga despues del corte: al corte estaba vencida
        a = construir(socios_df(), base, cuotas, [t], con_etiqueta=False)
        b = construir(socios_df(), futuro, cuotas, [t], con_etiqueta=False)
        pd.testing.assert_frame_equal(a[VARIABLES], b[VARIABLES])
        self.assertEqual(a.iloc[0]["cuotas_vencidas"], 1)
        self.assertNotIn("abandono", a.columns)

    def test_etiqueta_abandono_y_poblacion_activa(self):
        t = pd.Timestamp("2026-03-02")
        activos = pd.concat([asis("s0", ["2026-02-25"]), asis("s1", ["2026-02-26", "2026-03-31"]),
                             asis("s2", ["2026-01-05"])])  # s2 no asistio en los 28 dias previos: fuera de la poblacion
        df = construir(socios_df(3), activos, CUOTAS_VACIAS, [t])
        self.assertEqual(sorted(df["socio_id"]), ["s0", "s1"])
        self.assertEqual(df.set_index("socio_id").loc["s0", "abandono"], 1)   # nada en [02/03, 01/04)
        self.assertEqual(df.set_index("socio_id").loc["s1", "abandono"], 0)   # 31/03 esta dentro de la ventana

    def test_los_cortes_son_lunes_con_ventana_completa(self):
        a = asis("s0", pd.date_range("2026-01-05", "2026-06-28", freq="3D"))
        cortes = fechas_de_corte(a)
        self.assertTrue(all(c.weekday() == 0 for c in cortes))
        self.assertGreaterEqual(min(cortes), pd.Timestamp("2026-02-02"))
        self.assertLessEqual(max(cortes) + pd.Timedelta(days=30), a["fecha"].max())


class SeparacionTests(unittest.TestCase):
    def datos(self):
        rng = np.random.default_rng(0)
        cortes = pd.date_range("2026-02-02", periods=16, freq="7D")
        filas = []
        for t in cortes:
            for s in range(40):
                x = rng.normal()
                filas.append({"socio_id": f"s{s}", "fecha_corte": t, "frecuencia_4s": 2 + x, "dias_desde_ultima": 3 - x,
                              "tendencia": 0, "cuotas_vencidas": int(x < -1), "antiguedad_dias": 100, "edad": 30, "nivel": 2,
                              "abandono": int(x + rng.normal(0, 0.5) < -0.8)})
        return pd.DataFrame(filas)

    def test_sin_fuga_temporal_la_prueba_es_posterior_y_la_ventana_de_entrenamiento_no_la_alcanza(self):
        sep = separar_temporal(self.datos())
        self.assertTrue((sep.entrenamiento["fecha_corte"] + pd.Timedelta(days=30) <= sep.fecha_separacion).all())
        self.assertTrue((sep.prueba["fecha_corte"] >= sep.fecha_separacion).all())
        self.assertLess(sep.entrenamiento["fecha_corte"].max(), sep.prueba["fecha_corte"].min())
        self.assertGreater(sep.descartadas, 0)  # brecha entre ambos conjuntos

    def test_los_modelos_superan_a_la_linea_base_en_datos_con_senal_conocida(self):
        r = entrenar_y_evaluar(separar_temporal(self.datos()), semilla=1, repeticiones=50)["resultados"]
        self.assertAlmostEqual(r["linea_base_clase_mayoritaria"]["metricas"]["roc_auc"], 0.5)
        self.assertGreater(r["regresion_logistica"]["metricas"]["roc_auc"], 0.8)
        lo, hi = r["regresion_logistica"]["intervalos"]["roc_auc"]
        self.assertLessEqual(lo, r["regresion_logistica"]["metricas"]["roc_auc"])
        self.assertGreaterEqual(hi, r["regresion_logistica"]["metricas"]["roc_auc"])

    def test_metricas_con_valores_conocidos(self):
        m = metricas(np.array([1, 1, 0, 0]), np.array([0.9, 0.2, 0.6, 0.1]))
        self.assertEqual((m["tp"], m["fn"], m["fp"], m["tn"]), (1, 1, 1, 1))
        self.assertAlmostEqual(m["accuracy"], 0.5)
        self.assertAlmostEqual(m["roc_auc"], 0.75)


@unittest.skipUnless(os.environ.get("ANALITICA_PG_CLAVE"), "sin base de analitica configurada (ANALITICA_PG_CLAVE)")
class ExtraccionRealTests(unittest.TestCase):
    def test_la_base_solo_devuelve_datos_sinteticos_y_la_sesion_es_de_solo_lectura(self):
        from gymbro_analytics.conexion import conectar
        with conectar() as con:
            socios = extraccion.socios(con)
            self.assertTrue((socios["origen_datos"] == "Sintetico").all())
            cur = con.cursor()
            cur.execute("SHOW transaction_read_only")
            self.assertEqual(cur.fetchone()[0], "on")
            cur.close()


if __name__ == "__main__":
    unittest.main()
