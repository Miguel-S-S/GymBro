"""Etapa 9b-1: pruebas de las metricas con resultados conocidos (python -m unittest discover -s GymBro.IA/evaluacion)."""
import csv
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from metricas import rango_reciproco, recall_en_k, resumir, separar_ids  # noqa: E402

RAIZ = Path(__file__).resolve().parents[2]


class MetricasTests(unittest.TestCase):
    def test_recall_en_k(self):
        devueltos = ["a", "b", "c", "d", "e"]
        self.assertEqual(recall_en_k(devueltos, ["c"], 1), 0.0)
        self.assertEqual(recall_en_k(devueltos, ["c"], 3), 1.0)
        self.assertEqual(recall_en_k(devueltos, ["a", "z"], 5), 0.5)
        self.assertEqual(recall_en_k(devueltos, ["A"], 1), 1.0)  # sin distinguir mayusculas (ids GUID)

    def test_rango_reciproco(self):
        self.assertEqual(rango_reciproco(["a", "b", "c"], ["b", "c"]), 0.5)
        self.assertEqual(rango_reciproco(["a", "b", "c"], ["z"]), 0.0)
        self.assertEqual(rango_reciproco(["a"], ["a"]), 1.0)

    def test_resumen_con_valores_conocidos(self):
        r = resumir([(["a", "b", "c"], ["a"]), (["x", "y", "z"], ["z"]), (["p", "q"], ["r"])])
        self.assertEqual(r["n"], 3)
        self.assertAlmostEqual(r["recall@1"], 1 / 3)
        self.assertAlmostEqual(r["recall@3"], 2 / 3)
        self.assertAlmostEqual(r["mrr"], (1 + 1 / 3 + 0) / 3)

    def test_sin_etiquetas_no_hay_metricas(self):
        with self.assertRaises(ValueError):
            recall_en_k(["a"], [], 1)
        with self.assertRaises(ValueError):
            resumir([])

    def test_separar_ids(self):
        self.assertEqual(separar_ids(""), [])
        self.assertEqual(separar_ids(None), [])
        self.assertEqual(separar_ids(" a ; b,c "), ["a", "b", "c"])

    def test_el_banco_tiene_al_menos_30_consultas_y_etiquetas_con_formato_valido(self):
        ruta = RAIZ / "docs" / "pfc" / "evaluacion" / "banco-consultas.csv"
        with ruta.open(encoding="utf-8", newline="") as f:
            filas = list(csv.DictReader(f))
        self.assertGreaterEqual(len(filas), 30)
        self.assertEqual(len({f["id"] for f in filas}), len(filas))
        for f in filas:
            self.assertTrue(f["consulta_es"].strip() and f["consulta_en"].strip(), f["id"])
        # Las etiquetas las ponen los autores y la Personal Trainer: vacias o ids con formato GUID, nunca otra cosa.
        import uuid
        for f in filas:
            for i in separar_ids(f["ids_relevantes"]):
                uuid.UUID(i)


if __name__ == "__main__":
    unittest.main()
